<?php
/**
 * API HTTP minimale de LibreVies.
 *
 * Compatible avec PHP 5.6 et les versions plus recentes.
 * Le mot de passe MySQL reste sur le serveur et n'est jamais distribue aux joueurs.
 */

header('Content-Type: application/json; charset=utf-8');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Headers: Content-Type');

require_once __DIR__ . DIRECTORY_SEPARATOR . 'securite.php';
require_once __DIR__ . DIRECTORY_SEPARATOR . 'personnage.php';
require_once __DIR__ . DIRECTORY_SEPARATOR . 'npc.php';
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');

function journal_api($message) {
    // Le journal est place hors de eds-www pour ne pas etre telechargeable
    // par HTTP. Aucun mot de passe n'est jamais ecrit.
    $chemin = dirname(dirname(dirname(__FILE__))) . DIRECTORY_SEPARATOR . 'journal_api.log';
    $ligne = '[' . date('Y-m-d H:i:s') . '] ' . $message . PHP_EOL;
    if (@file_put_contents($chemin, $ligne, FILE_APPEND | LOCK_EX) === false) {
        // Secours si EasyPHP refuse l'ecriture dans le dossier parent.
        @file_put_contents(__DIR__ . DIRECTORY_SEPARATOR . 'journal_api.log',
                           $ligne, FILE_APPEND | LOCK_EX);
    }
}

function texte_recu($value, $limite) {
    if (!is_string($value)) {
        return '';
    }
    $value = trim($value);
    if ($value === '' || strlen($value) > (int)$limite
        || preg_match('/[\\x00-\\x1F\\x7F]/', $value)
        || @preg_match('//u', $value) !== 1) {
        return '';
    }
    return $value;
}

function repondre($ok, $message = '', $extra = array(), $code = 200) {
    journal_api('REPONSE code=' . (int)$code . ' ok=' . ($ok ? 'true' : 'false')
        . ' message=' . $message);
    http_response_code($code);
    $contenu = array_merge(array('ok' => $ok, 'message' => $message), $extra);
    $json = json_encode($contenu, JSON_UNESCAPED_UNICODE);
    if ($json === false) {
        journal_api('ERREUR json_encode code=' . (int)json_last_error());
        http_response_code(500);
        $json = '{"ok":false,"message":"Reponse JSON impossible."}';
    }
    echo $json;
    exit;
}

function detail_erreur($erreur, $config) {
    error_log('[LibreVies API] ' . $erreur->getMessage());
    return !empty($config['debug'])
        ? 'Erreur serveur : ' . $erreur->getMessage()
        : 'Connexion a la base impossible.';
}

function doublons_membre($pdo, $pseudo, $email) {
    // Les deux requetes laissent MySQL appliquer sa collation,
    // notamment pour les majuscules/minuscules.
    $requetePseudo = $pdo->prepare(
        'SELECT id FROM membre WHERE pseudo = :pseudo LIMIT 1'
    );
    $requetePseudo->execute(array(':pseudo' => $pseudo));
    $pseudoExiste = (bool)$requetePseudo->fetchColumn();

    $requeteEmail = $pdo->prepare(
        'SELECT id FROM membre WHERE email = :email LIMIT 1'
    );
    $requeteEmail->execute(array(':email' => $email));
    $emailExiste = (bool)$requeteEmail->fetchColumn();

    return array('pseudo' => $pseudoExiste, 'email' => $emailExiste);
}

function repondre_doublon($doublons) {
    if (!empty($doublons['pseudo']) && !empty($doublons['email'])) {
        repondre(false, "Le pseudo et l'email existent deja.", array(), 409);
    }
    if (!empty($doublons['pseudo'])) {
        repondre(false, 'Le pseudo existe deja.', array(), 409);
    }
    if (!empty($doublons['email'])) {
        repondre(false, "L'email existe deja.", array(), 409);
    }
}

function membre_public($membre) {
    return array('id' => (int)$membre['id'], 'pseudo' => (string)$membre['pseudo'],
        'valider' => (string)$membre['valider'], 'droit' => (int)$membre['droit'],
        'bani' => (string)$membre['bani']);
}

function valider_position_joueur($valeur) {
    if (is_string($valeur)) {
        if (strlen($valeur) > 512) throw new InvalidArgumentException('Position trop volumineuse.');
        $valeur = json_decode($valeur, true);
    }
    if (!is_array($valeur)) throw new InvalidArgumentException('Position x/y/z attendue.');
    $resultat = array();
    foreach (array('x', 'y', 'z') as $axe) {
        if (!isset($valeur[$axe]) || (!is_int($valeur[$axe]) && !is_float($valeur[$axe]))
            || !is_finite((float)$valeur[$axe])) {
            throw new InvalidArgumentException('Coordonnee de position invalide : ' . $axe);
        }
        $nombre = (float)$valeur[$axe];
        $limite = $axe === 'y' ? 500.0 : 125.0;
        if (abs($nombre) > $limite) throw new InvalidArgumentException('Position hors du monde.');
        $resultat[$axe] = round($nombre, 4);
    }
    return $resultat; // Les id/droit/autres champs recus sont ignores.
}

function position_publique($valeur) {
    if ($valeur === null || $valeur === '') return null;
    try { return valider_position_joueur($valeur); }
    catch (InvalidArgumentException $erreur) {
        // Un vieux JSON corrompu ne doit pas bloquer le compte ni ecraser la BDD.
        journal_api('POSITION stockee invalide : depart par defaut');
        return null;
    }
}

function lire_position_joueur($pdo, $id) {
    $query = $pdo->prepare('SELECT position FROM membre WHERE id = :id LIMIT 1');
    $query->execute(array(':id' => $id));
    return position_publique($query->fetchColumn());
}

function verifier_bannissement($membre) {
    // Seul 'non' autorise le jeu. L'identite et ce statut viennent de MySQL,
    // jamais d'un droit/bani envoye par le client ou d'un ancien cache.
    if ((string)$membre['bani'] !== 'non') {
        repondre(false, "Joueur bani veuillez contacter l'administrateur", array(
            'code' => 'membre_bani', 'membre' => membre_public($membre)
        ), 403);
    }
}

function verifier_administrateur($membre) {
    if ((int)$membre['droit'] !== 1) {
        repondre(false, 'Administration des joueurs reservee aux administrateurs.', array(), 403);
    }
}

function id_joueur_recu($donnees) {
    $id = isset($donnees['player_id']) ? $donnees['player_id'] : '';
    if (is_int($id)) $id = (string)$id; // JSON entier ou formulaire texte.
    if (!is_string($id) || !preg_match('/^[1-9][0-9]{0,9}$/D', $id)
        || (float)$id > 4294967295) {
        repondre(false, 'Identifiant joueur invalide.', array(), 400);
    }
    return (int)$id;
}

function lire_joueur_administration($pdo, $id) {
    // Lecture admin seulement : aucun mot de passe/hash ni session retourne.
    $query = $pdo->prepare('SELECT id, pseudo, email, valider, droit, bani, position FROM membre WHERE id = :id LIMIT 1');
    $query->execute(array(':id' => $id));
    $membre = $query->fetch();
    if (!$membre) return null;
    $public = membre_public($membre);
    $public['email'] = (string)$membre['email'];
    $query = $pdo->prepare('SELECT * FROM personnage WHERE id = :id LIMIT 1');
    $query->execute(array(':id' => $id));
    $personnage = $query->fetch();
    $erreurProfil = '';
    try {
        // Un vieux compte sans profil reste consultable sans INSERT implicite.
        $personnage = $personnage ? personnage_public($personnage) : array('id' => $id, 'default' => 1);
    } catch (InvalidArgumentException $erreur) {
        $personnage = null;
        $erreurProfil = $erreur->getMessage();
    }
    $query = $pdo->prepare('SELECT experience, chasse, territoire FROM classement WHERE id = :id LIMIT 1');
    $query->execute(array(':id' => $id));
    $classement = $query->fetch();
    // Transport texte : preserve les BIGINT UNSIGNED sans arrondi JSON/Unity.
    if ($classement) foreach ($classement as $cle => $valeur) $classement[$cle] = (string)$valeur;
    return array('membre' => $public, 'personnage' => $personnage,
        'classement' => $classement ?: null, 'erreur_personnage' => $erreurProfil,
        'position' => position_publique($membre['position']));
}

function verifier_validation_email($membre, $config) {
    // Inscription temporairement validee ('oui'), pas d'envoi d'email encore.
    // Les anciens non restent autorises quand require_email_validation est false.
    if (!empty($config['require_email_validation']) && (string)$membre['valider'] !== 'oui') {
        repondre(false, 'Validez votre compte par email avant de vous connecter.', array(), 403);
    }
}

function demarrer_session_api($jeton = null, $nouvelle = false) {
    // PHP garde la session cote serveur : aucune table ni cle secrete a
    // distribuer. Le jeton ne circule que dans les corps POST, jamais l'URL.
    // Seul creer_session_membre peut creer un id, genere cote serveur.
    // Les jetons recus du client passent toujours en mode strict.
    ini_set('session.use_strict_mode', $nouvelle ? '0' : '1');
    ini_set('session.use_cookies', '0');
    ini_set('session.use_only_cookies', '0');
    ini_set('session.use_trans_sid', '0');
    ini_set('session.gc_maxlifetime', '86400');
    session_name('LIBREVIES');
    if ($jeton !== null) session_id($jeton);
    if (!@session_start()) {
        repondre(false, 'Impossible d ouvrir la session du compte.', array(), 503);
    }
}

function creer_session_membre($membre) {
    // Le helper utilise aussi le CSPRNG du systeme si PHP 5.6 ne fournit
    // pas random_bytes et si OpenSSL est desactive (EasyPHP/Windows).
    try {
        $aleatoire = octets_aleatoires_securises(32);
    } catch (Exception $erreur) {
        journal_api('SESSION generateur inaccessible PHP=' . PHP_VERSION . ' OS=' . PHP_OS);
        repondre(false, 'Impossible de securiser la session : generateur du systeme inaccessible.', array(), 503);
    }
    $jeton = bin2hex($aleatoire);
    demarrer_session_api($jeton, true);
    if (session_id() !== $jeton) {
        session_destroy();
        repondre(false, 'Impossible de securiser la session du compte.', array(), 503);
    }
    $expire = time() + 86400;
    $_SESSION = array('id_membre' => (int)$membre['id'], 'expire_le' => $expire);
    session_write_close();
    return array('token' => $jeton, 'expires_at' => $expire);
}

function membre_authentifie($pdo, $config, $donnees) {
    if (!isset($_SERVER['REQUEST_METHOD']) || $_SERVER['REQUEST_METHOD'] !== 'POST') {
        repondre(false, 'Les appels du compte exigent un POST, sans session dans une URL.', array(), 405);
    }
    $jeton = isset($donnees['session_token']) ? $donnees['session_token'] : '';
    if (!is_string($jeton) || !preg_match('/^[a-zA-Z0-9,-]{16,128}$/D', $jeton)) {
        repondre(false, 'Session manquante : reconnectez-vous depuis le launcher.', array(), 401);
    }
    demarrer_session_api($jeton);
    $id = isset($_SESSION['id_membre']) ? (int)$_SESSION['id_membre'] : 0;
    $expire = isset($_SESSION['expire_le']) ? (int)$_SESSION['expire_le'] : 0;
    if ($id <= 0 || $expire <= time()) {
        $_SESSION = array();
        session_destroy();
        repondre(false, 'Session expiree : reconnectez-vous depuis le launcher.', array(), 401);
    }
    // Liberer le verrou avant les acces SQL et les autres requetes du jeu.
    session_write_close();
    try {
        $requete = $pdo->prepare('SELECT id, pseudo, valider, droit, bani FROM membre WHERE id = :id LIMIT 1');
        $requete->execute(array(':id' => $id));
        $membre = $requete->fetch();
    } catch (PDOException $erreur) {
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    if (!$membre) {
        repondre(false, 'Compte du joueur introuvable.', array(), 401);
    }
    verifier_bannissement($membre);
    verifier_validation_email($membre, $config);
    return $membre;
}

$method = isset($_SERVER['REQUEST_METHOD']) ? $_SERVER['REQUEST_METHOD'] : 'GET';
if ($method === 'OPTIONS') {
    repondre(true, 'Pre-requete acceptee.');
}
if ($method !== 'GET' && $method !== 'POST') {
    repondre(false, 'Methode non autorisee.', array(), 405);
}

$configPath = __DIR__ . DIRECTORY_SEPARATOR . 'config.php';
if (!is_file($configPath)) {
    repondre(false, 'API non configuree : copiez config.php.example en config.php.', array(), 500);
}
$config = require $configPath;

if ($method === 'GET') {
    // Diagnostic sans modifier la base : ouvrir api.php?action=health.
    $action = isset($_GET['action']) ? texte_recu($_GET['action'], 40) : 'health';
    $donnees = $_GET;
} else {
    $corps = file_get_contents('php://input');
    if ($corps === false) {
        $corps = '';
    }
    if (strlen($corps) > 65536) repondre(false, 'Requete trop volumineuse.', array(), 413);
    $donnees = json_decode($corps, true);
    if (!is_array($donnees)) {
        $donnees = $_POST;
    }
    $action = isset($donnees['action']) ? texte_recu($donnees['action'], 40) : '';
}

if ($method !== 'POST' && $action !== 'health') {
    repondre(false, 'POST requis pour les comptes et leur administration.', array(), 405);
}

journal_api('REQUETE methode=' . $method . ' action=' . $action
    . ' email=' . (isset($donnees['email']) ? (string)$donnees['email'] : '')
    . ' pseudo=' . (isset($donnees['pseudo']) ? (string)$donnees['pseudo'] : ''));

try {
    $dsn = sprintf(
        'mysql:host=%s;port=%d;dbname=%s;charset=utf8mb4',
        (string)$config['db_host'],
        (int)$config['db_port'],
        (string)$config['db_name']
    );
    $pdo = new PDO($dsn, (string)$config['db_user'], (string)$config['db_password'], array(
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        PDO::ATTR_EMULATE_PREPARES => false,
    ));
} catch (Exception $erreur) {
    repondre(false, detail_erreur($erreur, $config), array(), 503);
}

// La migration est volontairement manuelle : aucun ALTER TABLE automatique.
// Refuser plutot que lancer un jeu dont le bannissement ne serait pas verifie.
try {
    $pdo->query('SELECT bani, valider, position FROM membre LIMIT 0');
} catch (PDOException $erreur) {
    journal_api('SCHEMA membre bani/valider/position indisponible');
    repondre(false, 'Schema membre incomplet : bani, valider non/oui et position requis.', array(), 500);
}

if ($action === 'health') {
    repondre(true, 'API et base de donnees accessibles.');
}

if ($method !== 'POST') {
    repondre(false, 'Action GET inconnue.', array(), 400);
}

if ($action === 'check_account') {
    $email = trim(isset($donnees['email']) ? (string)$donnees['email'] : '');
    $pseudo = trim(isset($donnees['pseudo']) ? (string)$donnees['pseudo'] : '');
    if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
        repondre(false, 'Adresse email invalide.', array(), 400);
    }
    if (!preg_match('/^[[:alnum:]_ -]{3,30}$/u', $pseudo)) {
        repondre(false, 'Pseudo invalide : 3 a 30 caracteres.', array(), 400);
    }
    try {
        repondre_doublon(doublons_membre($pdo, $pseudo, $email));
    } catch (PDOException $erreur) {
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    repondre(true, 'Email et pseudo disponibles.');
}

if ($action === 'registration_status') {
    $email = trim(isset($donnees['email']) ? (string)$donnees['email'] : '');
    $pseudo = trim(isset($donnees['pseudo']) ? (string)$donnees['pseudo'] : '');
    if ($email === '' || $pseudo === '') {
        repondre(false, 'Email et pseudo obligatoires.', array(), 400);
    }

    try {
        $requete = $pdo->prepare(
            'SELECT id, pseudo, email FROM membre '
            . 'WHERE email = :email AND pseudo = :pseudo LIMIT 1'
        );
        $requete->execute(array(':email' => $email, ':pseudo' => $pseudo));
        $membre = $requete->fetch();
    } catch (PDOException $erreur) {
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    if (!$membre) {
        repondre(false, 'Inscription encore en attente.', array(), 404);
    }
    repondre(true, 'Inscription verifiee.', array(
        'membre' => array(
            'id' => (int)$membre['id'],
            'pseudo' => (string)$membre['pseudo'],
            'email' => (string)$membre['email'],
        ),
    ));
}

if ($action === 'register') {
    $email = trim(isset($donnees['email']) ? (string)$donnees['email'] : '');
    $pseudo = trim(isset($donnees['pseudo']) ? (string)$donnees['pseudo'] : '');
    $motdepasse = isset($donnees['password']) ? (string)$donnees['password'] : '';

    if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
        repondre(false, 'Adresse email invalide.', array(), 400);
    }
    if (!preg_match('/^[[:alnum:]_ -]{3,30}$/u', $pseudo)) {
        repondre(false, 'Pseudo invalide : 3 a 30 caracteres.', array(), 400);
    }
    if (strlen($motdepasse) < 8) {
        repondre(false, 'Le mot de passe doit contenir au moins 8 caracteres.', array(), 400);
    }

    // Verification juste avant l INSERT. La contrainte UNIQUE reste
    // necessaire pour les inscriptions simultanees et est geree plus bas.
    try {
        repondre_doublon(doublons_membre($pdo, $pseudo, $email));
    } catch (PDOException $erreur) {
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }

    // PHP 5.6 utilise PASSWORD_DEFAULT (bcrypt). Les versions plus recentes
    // peuvent proposer Argon2id ; dans tous les cas, aucun MD5 n'est utilise.
    $algorithme = defined('PASSWORD_ARGON2ID') ? PASSWORD_ARGON2ID : PASSWORD_DEFAULT;
    $hash = password_hash($motdepasse, $algorithme);
    if ($hash === false) {
        repondre(false, 'Impossible de securiser le mot de passe.', array(), 500);
    }

    $etapeInscription = 'membre';
    try {
        $pdo->beginTransaction();
        journal_api('INSCRIPTION tentative INSERT email=' . $email . ' pseudo=' . $pseudo);
        $requete = $pdo->prepare(
            'INSERT INTO membre (pseudo, motdepasse, email, valider, droit, bani, position) '
            . "VALUES (:pseudo, :motdepasse, :email, 'oui', 0, 'non', NULL)"
        );
        $requete->execute(array(
            ':pseudo' => $pseudo,
            ':motdepasse' => $hash,
            ':email' => $email,
        ));

        // Verification apres insertion par les deux valeurs saisies par le
        // joueur. On ne depend pas de lastInsertId(), car certaines tables
        // anciennes de LibreVies n'ont pas encore un AUTO_INCREMENT fiable.
        $verification = $pdo->prepare(
            'SELECT id, pseudo, email FROM membre '
            . 'WHERE email = :email AND pseudo = :pseudo LIMIT 1'
        );
        $verification->execute(array(
            ':email' => $email,
            ':pseudo' => $pseudo,
        ));
        $membre = $verification->fetch();
        if (!$membre) {
            $pdo->rollBack();
            repondre(false, 'Inscription echouee, veuillez contacter un administrateur.', array(), 500);
        }

        // Le personnage initial reprend le meme id que le compte. Seuls
        // id et `default` sont fournis ; tous les reglages restent NULL.
        $etapeInscription = 'personnage';
        $personnage = $pdo->prepare(
            'INSERT INTO personnage (id, `default`) '
            . 'VALUES (:id, 1)'
        );
        $personnage->execute(array(
            ':id' => (int)$membre['id'],
        ));
        // Un seul classement par joueur, lie au meme id que le membre.
        // Une panne de cette insertion annule aussi membre et personnage.
        $etapeInscription = 'classement';
        $classement = $pdo->prepare(
            'INSERT INTO classement (id, experience, chasse, territoire) '
            . 'VALUES (:id, 0, 0, 0)'
        );
        $classement->execute(array(':id' => (int)$membre['id']));
        $pdo->commit();
    } catch (PDOException $erreur) {
        if ($pdo->inTransaction()) {
            $pdo->rollBack();
        }
        // 1062 est le doublon MySQL. Un doublon sur personnage/classement
        // doit rester une erreur serveur, pas un faux pseudo deja utilise.
        if ($etapeInscription === 'membre' && $erreur->getCode() === '23000' && isset($erreur->errorInfo[1])
            && (int)$erreur->errorInfo[1] === 1062) {
            repondre(false, "Le pseudo ou l'email existe deja.", array(), 409);
        }
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    repondre(true, 'Inscription reussie.', array(
        'membre' => array(
            'id' => (int)$membre['id'],
            'pseudo' => (string)$membre['pseudo'],
            'email' => (string)$membre['email'],
        ),
    ));
}

if ($action === 'search_players' || $action === 'get_player' || $action === 'save_player_ban') {
    $admin = membre_authentifie($pdo, $config, $donnees);
    verifier_administrateur($admin); // Relu en BDD a chaque requete, exactement droit=1.
    try {
        if ($action === 'search_players') {
            $recherche = isset($donnees['search']) ? texte_recu($donnees['search'], 50) : '';
            $joueurs = array();
            if ($recherche !== '') {
                // Recherche partielle litterale : %, _ et ! ne sont pas des jokers client.
                $motif = '%' . str_replace(array('!', '%', '_'), array('!!', '!%', '!_'), $recherche) . '%';
                $query = $pdo->prepare("SELECT id, pseudo, valider, droit, bani FROM membre WHERE pseudo LIKE :search ESCAPE '!' ORDER BY pseudo, id LIMIT 30");
                $query->execute(array(':search' => $motif));
                foreach ($query->fetchAll() as $ligne) $joueurs[] = membre_public($ligne);
            }
            repondre(true, 'Recherche terminee.', array('membre' => membre_public($admin), 'players' => $joueurs));
        }
        $id = id_joueur_recu($donnees);
        if ($action === 'save_player_ban') {
            $bani = isset($donnees['bani']) ? $donnees['bani'] : null;
            if (!is_string($bani) || ($bani !== 'non' && $bani !== 'oui')) {
                repondre(false, 'Statut bani invalide : non ou oui attendu.', array(), 400);
            }
            $pdo->beginTransaction();
            // Verrouiller et revalider l'acteur pendant l'ecriture (InnoDB).
            $query = $pdo->prepare('SELECT id, pseudo, valider, droit, bani FROM membre WHERE id = :id LIMIT 1 FOR UPDATE');
            $query->execute(array(':id' => (int)$admin['id']));
            $acteur = $query->fetch();
            if (!$acteur || (int)$acteur['droit'] !== 1 || (string)$acteur['bani'] !== 'non') {
                $pdo->rollBack();
                if ($acteur) verifier_bannissement($acteur);
                repondre(false, 'Autorisation administrateur retiree.', array(), 403);
            }
            $query = $pdo->prepare('SELECT id FROM membre WHERE id = :id LIMIT 1 FOR UPDATE');
            $query->execute(array(':id' => $id));
            if (!$query->fetchColumn()) {
                $pdo->rollBack();
                repondre(false, 'Joueur introuvable.', array(), 404);
            }
            // Une seule colonne modifiee, jamais droit, motdepasse, scores ou profil.
            $query = $pdo->prepare('UPDATE membre SET bani = :bani WHERE id = :id');
            $query->execute(array(':bani' => $bani, ':id' => $id));
        }
        $joueur = lire_joueur_administration($pdo, $id);
        if (!$joueur) repondre(false, 'Joueur introuvable.', array(), 404);
        if ($action === 'save_player_ban') {
            // Si un admin se bannit lui-meme, le client recoit aussitot le vrai statut.
            if ((int)$admin['id'] === $id) $admin['bani'] = $bani;
            $pdo->commit();
        }
        repondre(true, $action === 'save_player_ban' ? 'Bannissement enregistre.' : 'Joueur charge.',
            array('membre' => membre_public($admin), 'player' => $joueur));
    } catch (Exception $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
}

if ($action === 'save_npc') {
    $membre = membre_authentifie($pdo, $config, $donnees);
    if ((int)$membre['droit'] !== 1) {
        repondre(false, 'Modification NPC reservee aux administrateurs.', array(), 403);
    }
    $idNpc = isset($donnees['npc_id']) ? texte_recu($donnees['npc_id'], 100) : '';
    if (!in_array($idNpc, identifiants_npc_village(), true)) {
        repondre(false, 'Identifiant NPC inconnu.', array(), 400);
    }
    try {
        $reglages = isset($donnees['personnage']) ? $donnees['personnage'] : null;
        if (is_string($reglages)) $reglages = json_decode($reglages, true);
        $profil = valider_profil_personnage($reglages);
        $pdo->beginTransaction();
        enregistrer_npc($pdo, $idNpc, $profil);
        $npcs = lire_npcs_village($pdo);
        $pdo->commit();
    } catch (InvalidArgumentException $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        repondre(false, $erreur->getMessage(), array(), 400);
    } catch (Exception $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    repondre(true, 'NPC enregistre.', array('membre' => membre_public($membre), 'npcs' => $npcs));
}

if ($action === 'get_npcs') {
    $membre = membre_authentifie($pdo, $config, $donnees);
    try {
        $npcs = lire_npcs_village($pdo);
    } catch (InvalidArgumentException $erreur) {
        repondre(false, $erreur->getMessage(), array(), 409);
    } catch (Exception $erreur) {
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    repondre(true, 'Profils NPC charges.', array('membre' => membre_public($membre), 'npcs' => $npcs));
}

if ($action === 'save_position') {
    $membre = membre_authentifie($pdo, $config, $donnees);
    try {
        $position = valider_position_joueur(isset($donnees['position']) ? $donnees['position'] : null);
        $pdo->beginTransaction();
        // Bloquer aussi un ban concurrent entre l'authentification et l'UPDATE.
        $query = $pdo->prepare('SELECT id, pseudo, valider, droit, bani FROM membre WHERE id = :id LIMIT 1 FOR UPDATE');
        $query->execute(array(':id' => (int)$membre['id']));
        $actuel = $query->fetch();
        if (!$actuel) { $pdo->rollBack(); repondre(false, 'Compte introuvable.', array(), 401); }
        if ((string)$actuel['bani'] !== 'non') { $pdo->rollBack(); verifier_bannissement($actuel); }
        $query = $pdo->prepare('UPDATE membre SET position = :position WHERE id = :id');
        $query->execute(array(':position' => json_encode($position), ':id' => (int)$membre['id']));
        $position = lire_position_joueur($pdo, (int)$membre['id']);
        $pdo->commit();
    } catch (InvalidArgumentException $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        repondre(false, $erreur->getMessage(), array(), 400);
    } catch (Exception $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    repondre(true, 'Position enregistree.', array('membre' => membre_public($actuel), 'position' => $position));
}

if ($action === 'get_character' || $action === 'save_character') {
    // L'id et le pseudo fournis par le client ne sont jamais une preuve
    // d'identite. Seule la session ouverte apres password_verify fait foi.
    $membre = membre_authentifie($pdo, $config, $donnees);
    try {
        if ($action === 'save_character') {
            $reglages = isset($donnees['personnage']) ? $donnees['personnage'] : null;
            if (is_string($reglages)) $reglages = json_decode($reglages, true);
            $profil = valider_profil_personnage($reglages);
            $pdo->beginTransaction();
            $personnage = enregistrer_personnage($pdo, (int)$membre['id'], $profil);
            $pdo->commit();
        } else {
            $personnage = lire_personnage($pdo, (int)$membre['id']);
        }
    } catch (InvalidArgumentException $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        repondre(false, $erreur->getMessage(), array(), $action === 'save_character' ? 400 : 409);
    } catch (Exception $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    repondre(true, $action === 'save_character' ? 'Personnage enregistre.' : 'Personnage charge.', array(
        'membre' => membre_public($membre),
        'personnage' => $personnage,
        'position' => lire_position_joueur($pdo, (int)$membre['id']),
    ));
}

if ($action === 'login') {
    $pseudo = trim(isset($donnees['pseudo']) ? (string)$donnees['pseudo'] : '');
    $motdepasse = isset($donnees['password']) ? (string)$donnees['password'] : '';
    if ($pseudo === '' || $motdepasse === '') {
        repondre(false, 'Pseudo et mot de passe obligatoires.', array(), 400);
    }

    try {
        $requete = $pdo->prepare(
            'SELECT id, pseudo, motdepasse, valider, droit, bani FROM membre WHERE pseudo = :pseudo LIMIT 1'
        );
        $requete->execute(array(':pseudo' => $pseudo));
        $membre = $requete->fetch();
    } catch (PDOException $erreur) {
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    if (!$membre || !password_verify($motdepasse, (string)$membre['motdepasse'])) {
        repondre(false, 'Pseudo ou mot de passe incorrect.', array(), 401);
    }
    verifier_bannissement($membre);
    verifier_validation_email($membre, $config);
    try {
        $personnage = lire_personnage($pdo, (int)$membre['id']);
    } catch (InvalidArgumentException $erreur) {
        repondre(false, 'Personnage enregistre invalide : ' . $erreur->getMessage(), array(), 409);
    } catch (Exception $erreur) {
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    $session = creer_session_membre($membre);
    repondre(true, 'Connexion reussie.', array(
        'membre' => membre_public($membre),
        'session' => $session,
        'personnage' => $personnage,
        'position' => lire_position_joueur($pdo, (int)$membre['id']),
    ));
}

repondre(false, 'Action inconnue.', array(), 400);
