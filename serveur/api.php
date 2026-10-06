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
header('Access-Control-Allow-Methods: POST, OPTIONS');

function repondre(bool $ok, string $message = '', array $extra = [], int $code = 200): void {
    http_response_code($code);
    echo json_encode(array_merge(['ok' => $ok, 'message' => $message], $extra),
                     JSON_UNESCAPED_UNICODE);
    exit;
}

if ($_SERVER['REQUEST_METHOD'] === 'OPTIONS') {
    repondre(true);
}
if ($_SERVER['REQUEST_METHOD'] !== 'POST') {
    repondre(false, 'Méthode non autorisée.', [], 405);
}

$configPath = __DIR__ . DIRECTORY_SEPARATOR . 'config.php';
if (!is_file($configPath)) {
    repondre(false, 'API non configurée : copiez config.php.example en config.php.', [], 500);
}
$config = require $configPath;

$corps = file_get_contents('php://input') ?: '';
$donnees = json_decode($corps, true);
if (!is_array($donnees)) {
    $donnees = $_POST;
}
$action = (string)($donnees['action'] ?? '');

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
    repondre(false, 'Connexion à la base impossible.', [], 503);
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
    if (strlen($motdepasse) < 6) {
        repondre(false, 'Le mot de passe doit contenir au moins 6 caractères.', [], 400);
    }

    try {
        $requete = $pdo->prepare(
            'INSERT INTO membre (pseudo, motdepasse, email) '
            . 'VALUES (:pseudo, :motdepasse, :email)'
        );
        $requete->execute([
            ':pseudo' => $pseudo,
            ':motdepasse' => password_hash($motdepasse, PASSWORD_DEFAULT),
            ':email' => $email,
        ]);
    } catch (PDOException $erreur) {
        // 23000 = contrainte UNIQUE (pseudo ou email deja utilise).
        if ($erreur->getCode() === '23000') {
            repondre(false, 'Ce pseudo ou cet email est déjà utilisé.', [], 409);
        }
        repondre(false, 'Inscription impossible.', [], 500);
    }
    repondre(true, 'Inscription réussie.');
}

if ($action === 'login') {
    $pseudo = trim((string)($donnees['pseudo'] ?? ''));
    $motdepasse = (string)($donnees['password'] ?? '');
    if ($pseudo === '' || $motdepasse === '') {
        repondre(false, 'Pseudo et mot de passe obligatoires.', [], 400);
    }

    $requete = $pdo->prepare(
        'SELECT id, pseudo, motdepasse FROM membre WHERE pseudo = :pseudo LIMIT 1'
    );
    $requete->execute([':pseudo' => $pseudo]);
    $membre = $requete->fetch();
    if (!$membre || !password_verify($motdepasse, (string)$membre['motdepasse'])) {
        repondre(false, 'Pseudo ou mot de passe incorrect.', [], 401);
    }
    repondre(true, 'Connexion réussie.', [
        'membre' => ['id' => (int)$membre['id'], 'pseudo' => (string)$membre['pseudo']],
    ]);
}

repondre(false, 'Action inconnue.', [], 400);
