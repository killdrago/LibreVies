<?php
// Recette des sources d'alea : php compilation/outils/test_securite.php
// Le chemin Windows/proc_open est simule, pas execute sur le poste de test.
namespace {
    require __DIR__ . '/../../jeu/serveur/securite.php';
    function verifier_alea($condition, $message) {
        if (!$condition) throw new \RuntimeException($message);
        echo 'OK ' . $message . PHP_EOL;
    }
    $a = octets_aleatoires_securises(32);
    $b = octets_aleatoires_securises(32);
    verifier_alea(strlen($a) === 32 && strlen($b) === 32 && $a !== $b,
        '32 octets, jeton different a chaque appel');
    foreach (array(0, -1, 1025, '32') as $taille) {
        $refuse = false;
        try { octets_aleatoires_securises($taille); }
        catch (\InvalidArgumentException $erreur) { $refuse = true; }
        verifier_alea($refuse, 'taille invalide refusee : ' . $taille);
    }
}

namespace LibreViesAleaWindowsTest {
    const DIRECTORY_SEPARATOR = '\\';
    $sortie = base64_encode(str_repeat("\xAB", 32));
    $code = 0;
    $commande = '';
    $autorise = true;
    function function_exists($nom) { return $nom === 'proc_open'; }
    function getenv($nom) { return 'C:\\Windows'; }
    function is_file($chemin) { return true; }
    function proc_open($cmd, $descripteurs, &$pipes, $cwd, $env, $options) {
        global $commande, $sortie, $autorise;
        $commande = $cmd;
        \verifier_alea($options['bypass_shell'] === true, 'PowerShell sans shell intermediaire');
        if (!$autorise) return false;
        $pipes = array(fopen('php://temp', 'w+b'), fopen('php://temp', 'w+b'), fopen('php://temp', 'w+b'));
        fwrite($pipes[1], $sortie);
        rewind($pipes[1]);
        return fopen('php://temp', 'w+b');
    }
    function proc_close($processus) {
        global $code;
        fclose($processus);
        return $code;
    }
}

namespace {
    $source = file_get_contents(__DIR__ . '/../../jeu/serveur/securite.php');
    // Memes fonctions que la production, avec seulement l'OS/les I/O simules.
    eval('namespace LibreViesAleaWindowsTest; use \RuntimeException; use \InvalidArgumentException; use \Exception;' . substr($source, 5));
    $octets = \LibreViesAleaWindowsTest\octets_aleatoires_securises(32);
    verifier_alea($octets === str_repeat("\xAB", 32), 'PHP sans random_bytes/OpenSSL utilise le CSPRNG Windows');
    $commande = $GLOBALS['commande'];
    $arguments = explode(' -EncodedCommand ', $commande);
    $script = str_replace("\0", '', base64_decode($arguments[1], true));
    verifier_alea(strpos($script, 'RNGCryptoServiceProvider') !== false
        && strpos($script, '$rng.GetBytes($octets)') !== false, 'source Windows cryptographique, pas Random');
    verifier_alea(strpos($commande, '-NoProfile -NonInteractive') !== false,
        'PowerShell sans profil ni saisie interactive');
    verifier_alea(strpos($script, 'password') === false && strpos($script, 'session_token') === false,
        'aucune donnee de compte dans la commande du generateur');

    foreach (array('pas-du-base64', base64_encode('trop-court')) as $sortieInvalide) {
        $GLOBALS['sortie'] = $sortieInvalide;
        $refuse = false;
        try { \LibreViesAleaWindowsTest\octets_aleatoires_securises(32); }
        catch (\RuntimeException $erreur) { $refuse = true; }
        verifier_alea($refuse, 'sortie du generateur invalide refusee');
    }
    $GLOBALS['sortie'] = base64_encode(str_repeat("\xAB", 32));
    $GLOBALS['code'] = 1;
    $refuse = false;
    try { \LibreViesAleaWindowsTest\octets_aleatoires_securises(32); }
    catch (\RuntimeException $erreur) { $refuse = true; }
    verifier_alea($refuse, 'erreur du processus refusee meme avec une sortie de bonne longueur');
    $GLOBALS['autorise'] = false;
    $refuse = false;
    try { \LibreViesAleaWindowsTest\octets_aleatoires_securises(32); }
    catch (\RuntimeException $erreur) { $refuse = true; }
    verifier_alea($refuse, 'processus indisponible : aucun jeton faible de secours');
}
