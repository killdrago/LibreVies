<?php
// Monde partage, stocke dans la base mondeLV (voir mondeLV.sql). Chaque
// modification recoit un numero de revision : le jeu demande "depuis" son dernier
// numero et ne recoit que ce qui a change (ajouts, modifications, suppressions).
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

function connexion_monde($config) {
    $nom = isset($config['monde_db_name']) ? (string)$config['monde_db_name'] : 'mondeLV';
    if (!preg_match('/^[A-Za-z0-9_]+$/', $nom)) throw new RuntimeException('Nom de base monde invalide.');
    $dsn = sprintf('mysql:host=%s;port=%d;dbname=%s;charset=utf8mb4',
        (string)$config['db_host'], (int)$config['db_port'], $nom);
    return new PDO($dsn, (string)$config['db_user'], (string)$config['db_password'], array(
        PDO::ATTR_ERRMODE => PDO::ERRMODE_EXCEPTION,
        PDO::ATTR_DEFAULT_FETCH_MODE => PDO::FETCH_ASSOC,
        PDO::ATTR_EMULATE_PREPARES => false,
    ));
}

// Incremente le compteur global et renvoie le nouveau numero de revision.
function prochaine_revision($pdo) {
    $pdo->exec('UPDATE monde_compteur SET revision = LAST_INSERT_ID(revision + 1) WHERE id = 1');
    return (int)$pdo->query('SELECT LAST_INSERT_ID()')->fetchColumn();
}

// Renvoie ce qui a change depuis la revision "depuis". depuis = 0 : tout le monde.
function lire_monde($pdo, $depuis) {
    $depuis = max(0, (int)$depuis);
    $revision = (int)$pdo->query('SELECT revision FROM monde_compteur WHERE id = 1')->fetchColumn();
    $complet = ($depuis === 0);
    if ($complet) {
        $requete = $pdo->prepare('SELECT id, genre, cle, type, position_x, position_z, rotation_y, taille, donnees, supprime'
            . ' FROM monde_objet WHERE supprime = 0 ORDER BY id');
        $requete->execute();
    } else {
        $requete = $pdo->prepare('SELECT id, genre, cle, type, position_x, position_z, rotation_y, taille, donnees, supprime'
            . ' FROM monde_objet WHERE revision > :depuis ORDER BY id');
        $requete->execute(array(':depuis' => $depuis));
    }
    $elements = array(); $supprimes = array(); $maisons = array(); $maisonsSupprimees = array();
    foreach ($requete->fetchAll() as $ligne) {
        $supprime = (int)$ligne['supprime'] === 1;
        if ($ligne['genre'] === 'maison') {
            if ($supprime) $maisonsSupprimees[] = (string)$ligne['cle'];
            else $maisons[] = array(
                'id' => (string)$ligne['cle'], 'nom' => '', 'x' => (float)$ligne['position_x'],
                'z' => (float)$ligne['position_z'], 'rotation' => (float)$ligne['rotation_y'],
                'donnees' => (string)$ligne['donnees'],
            );
            continue;
        }
        if ($supprime) { $supprimes[] = (int)$ligne['id']; continue; }
        $elements[] = array(
            'id' => (int)$ligne['id'], 'type' => (string)$ligne['type'],
            'x' => (float)$ligne['position_x'], 'z' => (float)$ligne['position_z'],
            'rotation' => (float)$ligne['rotation_y'], 'taille' => (float)$ligne['taille'],
        );
    }
    return array(
        'revision' => $revision, 'complet' => $complet,
        'elements' => $elements, 'supprimes' => $supprimes,
        'maisons' => $maisons, 'maisons_supprimees' => $maisonsSupprimees,
    );
}

// Ecritures du monde (admin seulement, controle fait dans api.php).
// Renvoie le tableau de reponse ; lance InvalidArgumentException si la saisie est fausse.
function traiter_monde($pdo, $action, $donnees, $membre) {
    $auteur = (int)$membre['id'];
    if ($action === 'ajouter_element_monde') {
        $element = valider_element_monde($donnees);
        $rev = prochaine_revision($pdo);
        $requete = $pdo->prepare('INSERT INTO monde_objet (genre, type, position_x, position_z, rotation_y, taille, auteur_id, revision)'
            . " VALUES ('element', :type, :x, :z, :rotation, :taille, :auteur, :rev)");
        $requete->execute(array(':type' => $element['type'], ':x' => $element['x'], ':z' => $element['z'],
            ':rotation' => $element['rotation'], ':taille' => $element['taille'], ':auteur' => $auteur, ':rev' => $rev));
        return array('message' => 'Element ajoute.', 'extra' => array('id' => (int)$pdo->lastInsertId(), 'revision' => $rev));
    }
    if ($action === 'modifier_element_monde') {
        $id = isset($donnees['id']) ? (int)$donnees['id'] : 0;
        if ($id <= 0) throw new InvalidArgumentException('Identifiant d element invalide.');
        $element = valider_element_monde($donnees);
        $rev = prochaine_revision($pdo);
        $requete = $pdo->prepare("UPDATE monde_objet SET type = :type, position_x = :x, position_z = :z,"
            . " rotation_y = :rotation, taille = :taille, auteur_id = :auteur, revision = :rev"
            . " WHERE id = :id AND genre = 'element' AND supprime = 0");
        $requete->execute(array(':type' => $element['type'], ':x' => $element['x'], ':z' => $element['z'],
            ':rotation' => $element['rotation'], ':taille' => $element['taille'], ':auteur' => $auteur,
            ':rev' => $rev, ':id' => $id));
        return array('message' => 'Element modifie.', 'extra' => array('revision' => $rev));
    }
    if ($action === 'supprimer_element_monde') {
        $id = isset($donnees['id']) ? (int)$donnees['id'] : 0;
        if ($id <= 0) throw new InvalidArgumentException('Identifiant d element invalide.');
        $rev = prochaine_revision($pdo);
        $requete = $pdo->prepare("UPDATE monde_objet SET supprime = 1, revision = :rev WHERE id = :id AND genre = 'element'");
        $requete->execute(array(':rev' => $rev, ':id' => $id));
        return array('message' => 'Element supprime.', 'extra' => array('revision' => $rev));
    }
    if ($action === 'vider_monde') {
        $rev = prochaine_revision($pdo);
        $requete = $pdo->prepare("UPDATE monde_objet SET supprime = 1, revision = :rev WHERE genre = 'element' AND supprime = 0");
        $requete->execute(array(':rev' => $rev));
        return array('message' => 'Elements du monde supprimes.', 'extra' => array('revision' => $rev));
    }
    if ($action === 'enregistrer_maison') {
        $maison = valider_maison_monde($donnees);
        $rev = prochaine_revision($pdo);
        $requete = $pdo->prepare("INSERT INTO monde_objet (genre, cle, type, position_x, position_z, rotation_y, donnees, auteur_id, revision)"
            . " VALUES ('maison', :cle, 'MAISON', :x, :z, :rotation, :donnees, :auteur, :rev)"
            . " ON DUPLICATE KEY UPDATE position_x = VALUES(position_x), position_z = VALUES(position_z),"
            . " rotation_y = VALUES(rotation_y), donnees = VALUES(donnees), auteur_id = VALUES(auteur_id),"
            . " supprime = 0, revision = VALUES(revision)");
        $requete->execute(array(':cle' => $maison['id'], ':x' => $maison['x'], ':z' => $maison['z'],
            ':rotation' => $maison['rotation'], ':donnees' => $maison['donnees'], ':auteur' => $auteur, ':rev' => $rev));
        return array('message' => 'Maison enregistree.', 'extra' => array('revision' => $rev));
    }
    throw new InvalidArgumentException('Action monde inconnue.');
}
