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
    $donnees = json_decode($corps, true);
    if (!is_array($donnees)) {
        $donnees = $_POST;
    }
    $action = isset($donnees['action']) ? texte_recu($donnees['action'], 40) : '';
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

    try {
        $pdo->beginTransaction();
        journal_api('INSCRIPTION tentative INSERT email=' . $email . ' pseudo=' . $pseudo);
        $requete = $pdo->prepare(
            'INSERT INTO membre (pseudo, motdepasse, email, valider) '
            . 'VALUES (:pseudo, :motdepasse, :email, 0)'
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

        // Le personnage initial reprend le meme id que le compte. Les
        // reglages sont des JSON vides, prets a etre remplis par le createur.
        $personnage = $pdo->prepare(
            'INSERT INTO personnage (id, `default`) '
            . 'VALUES (:id, 1)'
        );
        $personnage->execute(array(
            ':id' => (int)$membre['id'],
        ));
        $pdo->commit();
    } catch (PDOException $erreur) {
        if ($pdo->inTransaction()) {
            $pdo->rollBack();
        }
        if ($erreur->getCode() === '23000') {
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

if ($action === 'login') {
    $pseudo = trim(isset($donnees['pseudo']) ? (string)$donnees['pseudo'] : '');
    $motdepasse = isset($donnees['password']) ? (string)$donnees['password'] : '';
    if ($pseudo === '' || $motdepasse === '') {
        repondre(false, 'Pseudo et mot de passe obligatoires.', array(), 400);
    }

    try {
        $requete = $pdo->prepare(
            'SELECT id, pseudo, motdepasse FROM membre WHERE pseudo = :pseudo LIMIT 1'
        );
        $requete->execute(array(':pseudo' => $pseudo));
        $membre = $requete->fetch();
    } catch (PDOException $erreur) {
        repondre(false, detail_erreur($erreur, $config), array(), 500);
    }
    if (!$membre || !password_verify($motdepasse, (string)$membre['motdepasse'])) {
        repondre(false, 'Pseudo ou mot de passe incorrect.', array(), 401);
    }
    repondre(true, 'Connexion reussie.', array(
        'membre' => array('id' => (int)$membre['id'], 'pseudo' => (string)$membre['pseudo']),
    ));
}

repondre(false, 'Action inconnue.', array(), 400);
