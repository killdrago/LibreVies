<?php
// Pont entre l'API et le serveur IA local (jeu/serveur/IA/serveur_ia.py).
// L'adresse vient de config.php ('ia_url') : elle reste sur le serveur et ne
// part jamais avec le jeu. Le joueur n'appelle que api.php, comme les autres actions.

function appeler_ia_locale($config, $corps) {
    $url = (isset($config['ia_url']) && is_string($config['ia_url']) && $config['ia_url'] !== '')
        ? $config['ia_url'] : 'http://127.0.0.1:8765/npc_chat';
    $reponse = false;
    if (function_exists('curl_init')) {
        $curl = curl_init($url);
        curl_setopt_array($curl, array(
            CURLOPT_POST => true,
            CURLOPT_POSTFIELDS => $corps,
            CURLOPT_HTTPHEADER => array('Content-Type: application/json; charset=utf-8'),
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_CONNECTTIMEOUT => 5,
            CURLOPT_TIMEOUT => 90,
        ));
        $reponse = curl_exec($curl);
        curl_close($curl);
    } else {
        $contexte = stream_context_create(array('http' => array(
            'method' => 'POST',
            'header' => "Content-Type: application/json; charset=utf-8\r\n",
            'content' => $corps,
            'timeout' => 90,
            'ignore_errors' => true,
        )));
        $reponse = @file_get_contents($url, false, $contexte);
    }
    if (!is_string($reponse) || $reponse === '') {
        return null;
    }
    $decode = json_decode($reponse, true);
    return is_array($decode) ? $decode : null;
}
