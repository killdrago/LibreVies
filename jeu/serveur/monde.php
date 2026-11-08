<?php
// Monde partage : maisons (monde_maison) et elements hors maisons (monde_element).
// Lecture : tout joueur connecte. Ecriture : administrateurs seulement, controle
// fait dans api.php (droit=1, relu en base a chaque requete).

function types_monde_valides() {
    return array('EAU', 'HERBE', 'CAILLOU', 'ARBRE', 'PONT',
        'CAILLOU_SPHERE', 'CAILLOU_ROCHER', 'ARBRE_FEUILLU', 'ARBRE_PIN');
}

function nombre_monde($valeur, $min, $max) {
    if (is_string($valeur)) $valeur = str_replace(',', '.', trim($valeur));
    if (!is_numeric($valeur)) throw new InvalidArgumentException('Valeur numerique attendue.');
    $nombre = (float)$valeur;
    if (!is_finite($nombre) || $nombre < $min || $nombre > $max) {
        throw new InvalidArgumentException('Valeur hors des limites du monde.');
    }
    return round($nombre, 3);
}

function valider_element_monde($donnees) {
    $type = isset($donnees['type']) ? strtoupper(trim((string)$donnees['type'])) : '';
    if (!in_array($type, types_monde_valides(), true)) {
        throw new InvalidArgumentException('Type de monde inconnu.');
    }
    return array(
        'type' => $type,
        'x' => nombre_monde(isset($donnees['x']) ? $donnees['x'] : 0, -130, 130),
        'z' => nombre_monde(isset($donnees['z']) ? $donnees['z'] : 0, -130, 130),
        'rotation' => nombre_monde(isset($donnees['rotation']) ? $donnees['rotation'] : 0, -360, 360),
        'taille' => nombre_monde(isset($donnees['taille']) ? $donnees['taille'] : 1, 0.5, 10),
    );
}

function valider_maison_monde($donnees) {
    $id = isset($donnees['id']) ? strtolower(trim((string)$donnees['id'])) : '';
    if (!preg_match('/^[a-z0-9_]{3,100}$/', $id)) {
        throw new InvalidArgumentException('Identifiant de maison invalide (lettres, chiffres, _).');
    }
    $nom = isset($donnees['nom']) ? mb_substr(trim((string)$donnees['nom']), 0, 60) : '';
    $reglages = isset($donnees['donnees']) ? $donnees['donnees'] : '{}';
    if (!is_string($reglages) || strlen($reglages) > 20000 || !is_array(json_decode($reglages, true))) {
        throw new InvalidArgumentException('Reglages de maison invalides.');
    }
    return array(
        'id' => $id,
        'nom' => $nom,
        'x' => nombre_monde(isset($donnees['x']) ? $donnees['x'] : 0, -130, 130),
        'z' => nombre_monde(isset($donnees['z']) ? $donnees['z'] : 0, -130, 130),
        'rotation' => nombre_monde(isset($donnees['rotation']) ? $donnees['rotation'] : 0, -360, 360),
        'donnees' => $reglages,
    );
}

function lire_monde($pdo) {
    $elements = array();
    $requete = $pdo->query('SELECT id, type, position_x, position_z, rotation_y, taille FROM monde_element ORDER BY id');
    foreach ($requete->fetchAll() as $ligne) {
        $elements[] = array(
            'id' => (int)$ligne['id'],
            'type' => (string)$ligne['type'],
            'x' => (float)$ligne['position_x'],
            'z' => (float)$ligne['position_z'],
            'rotation' => (float)$ligne['rotation_y'],
            'taille' => (float)$ligne['taille'],
        );
    }
    $maisons = array();
    $requete = $pdo->query('SELECT id, nom, position_x, position_z, rotation_y, donnees FROM monde_maison ORDER BY id');
    foreach ($requete->fetchAll() as $ligne) {
        $maisons[] = array(
            'id' => (string)$ligne['id'],
            'nom' => (string)$ligne['nom'],
            'x' => (float)$ligne['position_x'],
            'z' => (float)$ligne['position_z'],
            'rotation' => (float)$ligne['rotation_y'],
            'donnees' => (string)$ligne['donnees'],
        );
    }
    return array('elements' => $elements, 'maisons' => $maisons);
}
