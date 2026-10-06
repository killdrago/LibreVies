<?php
/**
 * API HTTP minimale de LibreVies.
 *
 * Copier ce dossier dans C:\\xampp\\htdocs\\librevies, puis copier
 * config.php.example en config.php et renseigner la base locale.
 * Le launcher appelle cette API en HTTP ; le mot de passe MySQL reste ici,
 * sur le serveur, et n'est jamais distribue aux joueurs.
 */
declare(strict_types=1);

header('Content-Type: application/json; charset=utf-8');
header('Access-Control-Allow-Origin: *');
header('Access-Control-Allow-Headers: Content-Type');
header('Access-Control-Allow-Methods: GET, POST, OPTIONS');

function repondre(bool $ok, string $message = '', array $extra = [], int $code = 200): void {
    http_response_code($code);
    echo json_encode(array_merge(['ok' => $ok, 'message' => $message], $extra),
                     JSON_UNESCAPED_UNICODE);
    exit;
}

function detail_erreur(Throwable $erreur, array $config): string {
    error_log('[LibreVies API] ' . $erreur->getMessage());
    return !empty($config['debug'])
        ? 'Erreur serveur : ' . $erreur->getMessage()
        : 'Connexion à la base impossible.';
}

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    repondre(true, 'Pré-requête acceptée.');
}
if (!in_array($_SERVER['REQUEST_METHOD'], ['GET', 'POST'], true)) {
    repondre(false, 'Méthode non autorisée.', [], 405);
}

$configPath = __DIR__ . DIRECTORY_SEPARATOR . 'config.php';
if (!is_file($configPath)) {
    repondre(false, 'API non configurée : copiez config.php.example en config.php.', [], 500);
}
$config = require $configPath;

if ($_SERVER['REQUEST_METHOD'] === 'GET') {
    // Diagnostic sans modifier la base : ouvrir cette URL dans un navigateur.
    $action = (string)($_GET['action'] ?? 'health');
    $donnees = $_GET;
} else {
    $corps = file_get_contents('php://input') ?: '';
    $donnees = json_decode($corps, true);
    if (!is_array($donnees)) {
        $donnees = $_POST;
    }
    $action = (string)($donnees['action'] ?? '');
}

try {
    $dsn = sprintf(
        'mysql:host=%s;port=%d;dbname=%s;charset=utf8mb4',
        (string)$config['db_host'],
        (int)$config['db_port'],
        (string)$config['db_name']
    );
    $pdo = new PDO($dsn, (string)$config['db_user'], (string)$config['db_password'], [
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        PDO::ATTR_EMULATE_PREPARES => false,
    ]);
} catch (Throwable $erreur) {
    repondre(false, detail_erreur($erreur, $config), [], 503);
}

if ($action === 'health') {
    repondre(true, 'API et base de données accessibles.');
}

if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    repondre(false, 'Action GET inconnue.', [], 400);
}

if ($action === 'register') {
    $email = trim((string)($donnees['email'] ?? ''));
    $pseudo = trim((string)($donnees['pseudo'] ?? ''));
    $motdepasse = (string)($donnees['password'] ?? '');

    if (!filter_var($email, FILTER_VALIDATE_EMAIL)) {
        repondre(false, 'Adresse email invalide.', [], 400);
    }
    if (!preg_match('/^[[:alnum:]_ -]{3,30}$/u', $pseudo)) {
        repondre(false, 'Pseudo invalide : 3 à 30 caractères.', [], 400);
    }
    if (strlen($motdepasse) < 8) {
        repondre(false, 'Le mot de passe doit contenir au moins 8 caractères.', [], 400);
    }

    // Argon2id est utilisé s'il est disponible ; sinon PHP choisit son
    // algorithme moderne par défaut (bcrypt sur les versions courantes).
    $algorithme = defined('PASSWORD_ARGON2ID') ? PASSWORD_ARGON2ID : PASSWORD_DEFAULT;
    $hash = password_hash($motdepasse, $algorithme);
    if ($hash === false) {
        repondre(false, 'Impossible de sécuriser le mot de passe.', [], 500);
    }

    try {
        $requete = $pdo->prepare(
            'INSERT INTO membre (pseudo, motdepasse, email) '
            . 'VALUES (:pseudo, :motdepasse, :email)'
        );
        $requete->execute([
            ':pseudo' => $pseudo,
            ':motdepasse' => $hash,
            ':email' => $email,
        ]);
    } catch (PDOException $erreur) {
        // 23000 = contrainte UNIQUE (pseudo ou email déjà utilisé).
        if ($erreur->getCode() === '23000') {
            repondre(false, 'Ce pseudo ou cet email est déjà utilisé.', [], 409);
        }
        repondre(false, detail_erreur($erreur, $config), [], 500);
    }
    repondre(true, 'Inscription réussie.');
}

if ($action === 'login') {
    $pseudo = trim((string)($donnees['pseudo'] ?? ''));
    $motdepasse = (string)($donnees['password'] ?? '');
    if ($pseudo === '' || $motdepasse === '') {
        repondre(false, 'Pseudo et mot de passe obligatoires.', [], 400);
    }

    try {
        $requete = $pdo->prepare(
            'SELECT id, pseudo, motdepasse FROM membre WHERE pseudo = :pseudo LIMIT 1'
        );
        $requete->execute([':pseudo' => $pseudo]);
        $membre = $requete->fetch();
    } catch (PDOException $erreur) {
        repondre(false, detail_erreur($erreur, $config), [], 500);
    }
    if (!$membre || !password_verify($motdepasse, (string)$membre['motdepasse'])) {
        repondre(false, 'Pseudo ou mot de passe incorrect.', [], 401);
    }
    repondre(true, 'Connexion réussie.', [
        'membre' => ['id' => (int)$membre['id'], 'pseudo' => (string)$membre['pseudo']],
    ]);
}

repondre(false, 'Action inconnue.', [], 400);
