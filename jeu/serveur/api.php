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

function repondre($ok, $message = '', $extra = array(), $code = 200) {
    http_response_code($code);
    echo json_encode(array_merge(array('ok' => $ok, 'message' => $message), $extra),
                     JSON_UNESCAPED_UNICODE);
    exit;
}

function detail_erreur($erreur, $config) {
    error_log('[LibreVies API] ' . $erreur->getMessage());
    return !empty($config['debug'])
        ? 'Erreur serveur : ' . $erreur->getMessage()
        : 'Connexion a la base impossible.';
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
    $action = isset($_GET['action']) ? (string)$_GET['action'] : 'health';
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
    $action = isset($donnees['action']) ? (string)$donnees['action'] : '';
}

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

    // PHP 5.6 utilise PASSWORD_DEFAULT (bcrypt). Les versions plus recentes
    // peuvent proposer Argon2id ; dans tous les cas, aucun MD5 n'est utilise.
    $algorithme = defined('PASSWORD_ARGON2ID') ? PASSWORD_ARGON2ID : PASSWORD_DEFAULT;
    $hash = password_hash($motdepasse, $algorithme);
    if ($hash === false) {
        repondre(false, 'Impossible de securiser le mot de passe.', array(), 500);
    }

    try {
        $requete = $pdo->prepare(
            'INSERT INTO membre (pseudo, motdepasse, email) '
            . 'VALUES (:pseudo, :motdepasse, :email)'
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
            repondre(false, 'Inscription echouee, veuillez contacter un administrateur.', array(), 500);
        }
    } catch (PDOException $erreur) {
        if ($erreur->getCode() === '23000') {
            repondre(false, 'Ce pseudo ou cet email est deja utilise.', array(), 409);
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
