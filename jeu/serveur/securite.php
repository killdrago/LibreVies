<?php
/**
 * Alea cryptographique compatible PHP 5.6, y compris EasyPHP/Windows sans
 * extension OpenSSL activee. Aucun recours a rand, mt_rand, uniqid ou au temps.
 */

function octets_aleatoires_windows($taille) {
    if (DIRECTORY_SEPARATOR !== '\\' || !function_exists('proc_open')) return false;
    $windows = getenv('SystemRoot');
    if (!is_string($windows) || $windows === '') return false;
    $powershell = $windows . '\\System32\\WindowsPowerShell\\v1.0\\powershell.exe';
    if (!is_file($powershell)) return false;

    // Commande fixe : aucune donnee HTTP, aucun mot de passe ni jeton ne
    // passe a PowerShell. La source est le CSPRNG Windows fourni par .NET.
    $script = '$ErrorActionPreference = "Stop"; '
        . '$octets = New-Object byte[] ' . (int)$taille . '; '
        . '$rng = New-Object System.Security.Cryptography.RNGCryptoServiceProvider; '
        . 'try { $rng.GetBytes($octets); '
        . '[Console]::Out.Write([Convert]::ToBase64String($octets)); } '
        . 'finally { $rng.Dispose(); }';
    // EncodedCommand attend du UTF-16LE. Le script est uniquement ASCII :
    // ne pas dependre des extensions iconv/mbstring sur les anciens PHP.
    $utf16 = '';
    for ($i = 0; $i < strlen($script); $i++) $utf16 .= $script[$i] . "\0";
    $commande = escapeshellarg($powershell)
        . ' -NoLogo -NoProfile -NonInteractive -EncodedCommand ' . base64_encode($utf16);
    $descripteurs = array(array('pipe', 'r'), array('pipe', 'w'), array('pipe', 'w'));
    $pipes = array();
    $processus = @proc_open($commande, $descripteurs, $pipes, null, null, array('bypass_shell' => true));
    if (!is_resource($processus)) return false;
    fclose($pipes[0]);
    $sortie = stream_get_contents($pipes[1], 8192);
    fclose($pipes[1]);
    // Les erreurs du generateur ne sont pas melangees a la reponse JSON.
    stream_get_contents($pipes[2], 8192);
    fclose($pipes[2]);
    $code = proc_close($processus);
    if ($code !== 0 || !is_string($sortie)) return false;
    $octets = base64_decode(trim($sortie), true);
    return is_string($octets) && strlen($octets) === $taille ? $octets : false;
}

function octets_aleatoires_securises($taille) {
    if (!is_int($taille) || $taille < 1 || $taille > 1024) {
        throw new InvalidArgumentException('Taille d alea invalide.');
    }
    if (function_exists('random_bytes')) {
        try {
            $octets = random_bytes($taille);
            if (is_string($octets) && strlen($octets) === $taille) return $octets;
        } catch (Exception $erreur) { }
    }
    if (function_exists('openssl_random_pseudo_bytes')) {
        try {
            $fort = false;
            $octets = @openssl_random_pseudo_bytes($taille, $fort);
            if ($fort && is_string($octets) && strlen($octets) === $taille) return $octets;
        } catch (Exception $erreur) { }
    }
    if (DIRECTORY_SEPARATOR !== '\\') {
        // Lecture exacte du generateur du noyau, jamais d'un fichier de
        // configuration ni d'une source d'alea choisie par le client.
        $flux = @fopen('/dev/urandom', 'rb');
        if (is_resource($flux)) {
            $octets = '';
            while (strlen($octets) < $taille) {
                $partie = @fread($flux, $taille - strlen($octets));
                if (!is_string($partie) || $partie === '') break;
                $octets .= $partie;
            }
            fclose($flux);
            if (strlen($octets) === $taille) return $octets;
        }
    } else {
        $octets = octets_aleatoires_windows($taille);
        if ($octets !== false) return $octets;
    }
    throw new RuntimeException('Aucun generateur cryptographique du systeme accessible.');
}
