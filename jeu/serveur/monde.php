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

// Monstres : table monde_monstre (une ligne par monstre). Position fixee a la
// premiere declaration ; pv / mort partages entre tous les joueurs.
function valider_cle_monstre($donnees) {
    $cle = isset($donnees['cle']) ? trim((string)$donnees['cle']) : '';
    if (!preg_match('/^monstre_[0-9]{1,4}$/', $cle)) {
        throw new InvalidArgumentException('Identifiant de monstre invalide.');
    }
    return $cle;
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
        'x' => nombre_monde(isset($donnees['x']) ? $donnees['x'] : 0, -200, 200),
        'z' => nombre_monde(isset($donnees['z']) ? $donnees['z'] : 0, -200, 200),
        'rotation' => nombre_monde(isset($donnees['rotation']) ? $donnees['rotation'] : 0, -360, 360),
        'donnees' => $reglages,
    );
}

// Nom d'une maison : "sans nom" si vide. Renommer ne change que ce champ.
function valider_nom_maison($donnees) {
    $id = isset($donnees['id']) ? strtolower(trim((string)$donnees['id'])) : '';
    if (!preg_match('/^[a-z0-9_]{3,100}$/', $id)) {
        throw new InvalidArgumentException('Identifiant de maison invalide (lettres, chiffres, _).');
    }
    $nom = isset($donnees['nom']) ? trim((string)$donnees['nom']) : '';
    // Lettres sans accent, chiffres et espaces seulement.
    if (!preg_match('/^[A-Za-z0-9 ]{0,60}$/', $nom)) {
        throw new InvalidArgumentException('Nom invalide : lettres, chiffres et espaces seulement (60 caracteres max).');
    }
    if ($nom === '') $nom = 'sans nom';
    return array('id' => $id, 'nom' => $nom);
}

// Materiau ou texture choisi pour chaque partie de la maison (colonnes de monde_maison).
// Vide = suit le menu general : colonne NULL.
function surfaces_maison($reglages) {
    $r = json_decode((string)$reglages, true);
    if (!is_array($r)) $r = array();
    $choisir = function ($cles) use ($r) {
        foreach ($cles as $cle) {
            if (isset($r[$cle]) && is_string($r[$cle]) && trim($r[$cle]) !== '') {
                return mb_substr(trim($r[$cle]), 0, 255);
            }
        }
        return null;
    };
    return array(
        'papier' => $choisir(array('TexturePapier')),
        'mur' => $choisir(array('TextureMur', 'MateriauMur')),
        'porte' => $choisir(array('TexturePorte', 'MateriauPorte')),
        'pancarte' => $choisir(array('MateriauSupportPancarte')),
        'fenetre' => $choisir(array('MateriauFenetre')),
        'toit' => $choisir(array('TextureToit', 'MateriauToit')),
        'sol' => $choisir(array('TextureSol')),
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
    // monde_element : elements (arbres, cailloux, eau...) et 'edition'. monde_maison : maisons.
    $lignes = array();
    foreach (array('monde_element', 'monde_maison') as $table) {
        // Seule monde_maison a les colonnes nom et territoire.
        $colonnesNom = $table === 'monde_maison' ? 'nom, territoire' : "'' AS nom, '' AS territoire";
        if ($complet) {
            $requete = $pdo->prepare('SELECT id, genre, cle, type, position_x, position_z, rotation_y, taille, donnees, supprime,'
                . ' ' . $colonnesNom . ', TIMESTAMPDIFF(SECOND, modifie_le, NOW()) AS age'
                . ' FROM ' . $table . ' WHERE supprime = 0 ORDER BY id');
            $requete->execute();
        } else {
            $requete = $pdo->prepare('SELECT id, genre, cle, type, position_x, position_z, rotation_y, taille, donnees, supprime,'
                . ' ' . $colonnesNom . ', TIMESTAMPDIFF(SECOND, modifie_le, NOW()) AS age'
                . ' FROM ' . $table . ' WHERE revision > :depuis ORDER BY id');
            $requete->execute(array(':depuis' => $depuis));
        }
        $lignes = array_merge($lignes, $requete->fetchAll());
    }
    $elements = array(); $supprimes = array(); $maisons = array(); $maisonsSupprimees = array();
    foreach ($lignes as $ligne) {
        $supprime = (int)$ligne['supprime'] === 1;
        if ($ligne['genre'] === 'maison' || $ligne['genre'] === 'edition') {
            if ($supprime) $maisonsSupprimees[] = (string)$ligne['cle'];
            else $maisons[] = array(
                'id' => (string)$ligne['cle'], 'nom' => (string)$ligne['nom'], 'territoire' => (string)$ligne['territoire'],
                'x' => (float)$ligne['position_x'],
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
        'monstres' => lire_monstres($pdo, $complet ? 0 : $depuis),
    );
}

// ---- Monstres partages ----
// Regeneration (colonne monstre.regen = pts/minute) : lue par type de monstre.
// Les types correspondent aux identifiants de la table monstre (1 rat, 2 souris, 3 araignee).
// Si la colonne regen n'existe pas encore, il n'y a pas de regeneration (le monde reste lisible).
// La table monstre est dans la base principale (config db_name), pas dans mondeLV :
// api.php transmet sa connexion via base_regen_monstres().
function base_regen_monstres($pdo = null) {
    static $base = null;
    if ($pdo !== null) $base = $pdo;
    return $base;
}

function regen_par_type($pdo) {
    $regens = array('rat' => 0, 'souris' => 0, 'araignee' => 0);
    $source = base_regen_monstres();
    if ($source === null) $source = $pdo;
    try {
        $noms = array(1 => 'rat', 2 => 'souris', 3 => 'araignee');
        $requete = $source->prepare('SELECT id, regen FROM monstre WHERE id IN (1, 2, 3)');
        $requete->execute();
        foreach ($requete->fetchAll() as $ligne) {
            $id = (int)$ligne['id'];
            if (isset($noms[$id])) $regens[$noms[$id]] = max(0, (int)$ligne['regen']);
        }
    } catch (Exception $erreur) {
        // Colonne regen absente ou table absente : aucune regeneration, signalee dans le journal.
        if (function_exists('journal_api')) journal_api('REGEN monstres : lecture impossible : ' . $erreur->getMessage());
    }
    return $regens;
}

// PV actuels : pv enregistre + regeneration depuis la derniere variation de pv.
// Calcul paresseux : rien n'est ecrit en base, la regeneration se lit a chaque demande.
function pv_courant($ligne, $regens) {
    $pv = (int)$ligne['pv'];
    $pvMax = (int)$ligne['pv_max'];
    if ((int)$ligne['mort'] === 1 || $pv >= $pvMax) return $pv;
    if (!isset($ligne['pv_age']) || $ligne['pv_age'] === null) return $pv;
    $regen = isset($regens[$ligne['type']]) ? (int)$regens[$ligne['type']] : 0;
    if ($regen <= 0) return $pv;
    return min($pvMax, $pv + (int)floor($regen * (int)$ligne['pv_age'] / 60));
}

// Colonnes lues pour un monstre partage.
function colonnes_monstre() {
    return 'cle, type, position_x, position_z, rotation_y, proprietaire_id, pv, pv_max, mort,'
        . ' TIMESTAMPDIFF(SECOND, modifie_le, NOW()) AS age,'
        . ' TIMESTAMPDIFF(SECOND, pv_modifie_le, NOW()) AS pv_age';
}

function formater_monstre($ligne, $regens) {
    return array(
        'cle' => (string)$ligne['cle'], 'x' => (float)$ligne['position_x'], 'z' => (float)$ligne['position_z'],
        'pv' => pv_courant($ligne, $regens), 'pv_max' => (int)$ligne['pv_max'], 'mort' => (int)$ligne['mort'],
        'age' => (int)$ligne['age'], 'rot' => (float)$ligne['rotation_y'],
        'proprietaire' => (int)$ligne['proprietaire_id'],
    );
}

// depuis = 0 : tous les monstres ; sinon seulement ceux qui ont change.
function lire_monstres($pdo, $depuis) {
    $regens = regen_par_type($pdo);
    $sql = 'SELECT ' . colonnes_monstre() . ' FROM monde_monstre';
    // Les monstres blesses sont renvoyes a chaque demande : la regeneration ne change
    // pas la revision, sans cela le client ne verrait jamais les PV remonter.
    if ((int)$depuis > 0) {
        $requete = $pdo->prepare($sql . ' WHERE revision > :depuis OR (mort = 0 AND pv < pv_max) ORDER BY cle');
        $requete->execute(array(':depuis' => (int)$depuis));
    } else {
        $requete = $pdo->prepare($sql . ' ORDER BY cle');
        $requete->execute();
    }
    $liste = array();
    foreach ($requete->fetchAll() as $ligne) $liste[] = formater_monstre($ligne, $regens);
    return $liste;
}

function lire_monstres_par_cle($pdo, $cles) {
    if (count($cles) === 0) return array();
    $marques = implode(',', array_fill(0, count($cles), '?'));
    $regens = regen_par_type($pdo);
    $requete = $pdo->prepare('SELECT ' . colonnes_monstre() . ' FROM monde_monstre WHERE cle IN (' . $marques . ')');
    $requete->execute(array_values($cles));
    $liste = array();
    foreach ($requete->fetchAll() as $ligne) $liste[] = formater_monstre($ligne, $regens);
    return $liste;
}

// Declare les monstres du client. La premiere declaration fixe la position ;
// ensuite tous les joueurs recoivent la meme position (INSERT IGNORE).
// Remplace la liste des obstacles du monde (envoyee par le jeu au demarrage).
function traiter_declaration_obstacles($pdo, $donnees) {
    $brut = isset($donnees['obstacles']) ? $donnees['obstacles'] : '[]';
    $liste = is_string($brut) ? json_decode($brut, true) : $brut;
    if (!is_array($liste) || count($liste) > 20000) throw new InvalidArgumentException('Liste d\'obstacles invalide.');
    $pdo->beginTransaction();
    try {
        $pdo->exec('DELETE FROM monde_obstacle');
        $insertion = $pdo->prepare('INSERT INTO monde_obstacle (forme, x, z, rayon, largeur, profondeur)'
            . ' VALUES (:forme, :x, :z, :rayon, :largeur, :profondeur)');
        $nombre = 0;
        foreach ($liste as $o) {
            if (!is_array($o) || !isset($o['type'], $o['x'], $o['z'])) continue;
            $forme = $o['type'] === 'cercle' ? 'cercle' : 'boite';
            $x = (float)$o['x'];
            $z = (float)$o['z'];
            if (!is_finite($x) || !is_finite($z)) continue;
            $insertion->execute(array(
                ':forme' => $forme, ':x' => $x, ':z' => $z,
                ':rayon' => $forme === 'cercle' ? (float)(isset($o['rayon']) ? $o['rayon'] : 0) : 0,
                ':largeur' => $forme === 'boite' ? (float)(isset($o['largeur']) ? $o['largeur'] : 0) : 0,
                ':profondeur' => $forme === 'boite' ? (float)(isset($o['profondeur']) ? $o['profondeur'] : 0) : 0,
            ));
            $nombre++;
        }
        $pdo->commit();
    } catch (Exception $erreur) {
        $pdo->rollBack();
        throw $erreur;
    }
    return array('message' => 'Obstacles enregistres.', 'extra' => array('nombre' => $nombre));
}

function traiter_declaration_monstres($pdo, $donnees) {
    $brut = isset($donnees['monstres']) ? $donnees['monstres'] : '[]';
    $liste = is_string($brut) ? json_decode($brut, true) : $brut;
    if (!is_array($liste) || count($liste) > 2000) throw new InvalidArgumentException('Liste de monstres invalide.');
    $rev = prochaine_revision($pdo);
    $insertion = $pdo->prepare('INSERT IGNORE INTO monde_monstre'
        . ' (cle, type, position_x, position_z, pv, pv_max, mort, revision, pv_modifie_le)'
        . ' VALUES (:cle, :type, :x, :z, :pv, :pvmax, 0, :rev, NOW())');
    // Monstre deja en base avec un autre pv_max (table monstre modifiee) : on prend la
    // nouvelle valeur et le monstre repart a pleine vie (sauf s'il est mort).
    $miseAJourMax = $pdo->prepare('UPDATE monde_monstre SET pv_max = :pvmax1,'
        . ' pv = CASE WHEN mort = 0 THEN :pvmax2 ELSE pv END, pv_modifie_le = NOW(), revision = :rev'
        . ' WHERE cle = :cle AND pv_max <> :pvmax3');
    // « reinitialiser = 1 » (reconfiguration de la quantite par l'admin) : les positions et les
    // PV en base repartent des valeurs envoyees, au lieu de garder les anciennes positions.
    $reinitialiser = isset($donnees['reinitialiser']) && (string)$donnees['reinitialiser'] === '1';
    $reinit = $pdo->prepare('UPDATE monde_monstre SET position_x = :x, position_z = :z, rotation_y = 0,'
        . ' pv = pv_max, mort = 0, proprietaire_id = NULL, pv_modifie_le = NOW(), revision = :rev'
        . ' WHERE cle = :cle');
    $cles = array();
    foreach ($liste as $monstre) {
        if (!is_array($monstre)) throw new InvalidArgumentException('Monstre invalide.');
        $cle = valider_cle_monstre($monstre);
        $type = isset($monstre['type']) ? (string)$monstre['type'] : '';
        if (!in_array($type, array('souris', 'rat', 'araignee'), true)) {
            throw new InvalidArgumentException('Type de monstre inconnu.');
        }
        $x = nombre_monde(isset($monstre['x']) ? $monstre['x'] : 0, -130, 130);
        $z = nombre_monde(isset($monstre['z']) ? $monstre['z'] : 0, -130, 130);
        $pvMax = isset($monstre['pv_max']) ? (int)$monstre['pv_max'] : 0;
        if ($pvMax < 1 || $pvMax > 100000) throw new InvalidArgumentException('Points de vie invalides.');
        $insertion->execute(array(':cle' => $cle, ':type' => $type, ':x' => $x, ':z' => $z,
            ':pv' => $pvMax, ':pvmax' => $pvMax, ':rev' => $rev));
        $miseAJourMax->execute(array(':pvmax1' => $pvMax, ':pvmax2' => $pvMax, ':pvmax3' => $pvMax,
            ':rev' => $rev, ':cle' => $cle));
        if ($reinitialiser) {
            $reinit->execute(array(':x' => $x, ':z' => $z, ':rev' => $rev, ':cle' => $cle));
        }
        $cles[] = $cle;
    }
    return array('message' => 'Monstres declares.', 'extra' => array(
        'revision' => $rev, 'monstres' => lire_monstres_par_cle($pdo, $cles)));
}

// ---- Butin des monstres ----
// Tables (mondeLV) : monde_loot (catalogue : id, objet, quantite, pourcentage),
// monde_degats (degats par joueur et par monstre), monde_butin (butin en attente
// de recuperation par le joueur). La liste de lignes d'un monstre est dans
// monstre.loot (base principale) : texte « 1,2 » ou « 0 » pour rien.
function id_monstre_du_type($type) {
    $ids = array('rat' => 1, 'souris' => 2, 'araignee' => 3);
    return isset($ids[$type]) ? $ids[$type] : 0;
}

function loot_du_type($type) {
    $base = base_regen_monstres();
    $id = id_monstre_du_type($type);
    if ($base === null || $id === 0) return '0';
    try {
        $requete = $base->prepare('SELECT loot FROM monstre WHERE id = :id LIMIT 1');
        $requete->execute(array(':id' => $id));
        $loot = $requete->fetchColumn();
        return ($loot === false || $loot === null) ? '0' : trim((string)$loot);
    } catch (Exception $erreur) {
        if (function_exists('journal_api')) journal_api('BUTIN : lecture de monstre.loot impossible : ' . $erreur->getMessage());
        return '0';
    }
}

// Objets reconnus par le client : 'or' et 'cailloux'. Tolere casse, espaces et synonymes.
function objet_butin_normalise($brut) {
    $texte = strtolower(trim((string)$brut));
    $texte = str_replace(array('é', 'è', 'É'), 'e', $texte);
    if (in_array($texte, array('or', 'pieces', 'piece', 'pièces', 'pièce', 'gold', 'coin', 'coins'), true)) return 'or';
    if (in_array($texte, array('cailloux', 'caillou', 'pierre', 'pierres', 'rock', 'rocks'), true)) return 'cailloux';
    return '';
}

// Tirage independant pour chaque ligne du monstre : chaque ligne tiree peut donner son objet.
function tirer_butin($pdo, $type, $membreId) {
    $loot = loot_du_type($type);
    if ($loot === '' || $loot === '0') return;
    $ids = array();
    foreach (explode(',', $loot) as $morceau) {
        $numero = (int)trim($morceau);
        if ($numero > 0) $ids[] = $numero;
    }
    foreach ($ids as $numero) {
        try {
            $requete = $pdo->prepare('SELECT objet, quantite, pourcentage FROM monde_loot WHERE id = :id LIMIT 1');
            $requete->execute(array(':id' => $numero));
            $ligne = $requete->fetch();
            if (!$ligne) continue;
            // Nom de l'objet normalise (casse, espaces, synonymes) pour que le client le reconnaisse.
            $objet = objet_butin_normalise($ligne['objet']);
            $tirage = mt_rand(0, 9999999) / 100000; // 0 a 99,99999 %
            $gagne = ($objet !== '') && ($tirage < (float)$ligne['pourcentage']);
            if (function_exists('journal_api')) journal_api('BUTIN : ligne ' . $numero . ' objet=[' . (string)$ligne['objet']
                . '] -> [' . $objet . '] pourcentage=' . $ligne['pourcentage'] . ' tirage=' . round($tirage, 5)
                . ($gagne ? ' GAGNE' : ' perdu'));
            if ($gagne) {
                $ajout = $pdo->prepare('INSERT INTO monde_butin (membre_id, objet, quantite) VALUES (:membre, :objet, :quantite)');
                $ajout->execute(array(':membre' => $membreId, ':objet' => $objet,
                    ':quantite' => (int)$ligne['quantite']));
            }
        } catch (Exception $erreur) {
            if (function_exists('journal_api')) journal_api('BUTIN : ligne ' . $numero . ' impossible : ' . $erreur->getMessage());
        }
    }
}

function enregistrer_degats($pdo, $cle, $membreId, $degats) {
    try {
        $requete = $pdo->prepare('INSERT INTO monde_degats (cle, membre_id, degats) VALUES (:cle, :membre, :degats)'
            . ' ON DUPLICATE KEY UPDATE degats = degats + VALUES(degats)');
        $requete->execute(array(':cle' => $cle, ':membre' => $membreId, ':degats' => $degats));
    } catch (Exception $erreur) {
        if (function_exists('journal_api')) journal_api('BUTIN : degats non enregistres : ' . $erreur->getMessage());
    }
}

// A la mort : le joueur qui a fait le plus de degats sur le monstre recoit le butin.
function attribuer_butin_mort($pdo, $cle, $type) {
    try {
        $requete = $pdo->prepare('SELECT membre_id FROM monde_degats WHERE cle = :cle'
            . ' ORDER BY degats DESC, membre_id ASC LIMIT 1');
        $requete->execute(array(':cle' => $cle));
        $premier = $requete->fetchColumn();
        if ($premier !== false) tirer_butin($pdo, $type, (int)$premier);
        $suppression = $pdo->prepare('DELETE FROM monde_degats WHERE cle = :cle');
        $suppression->execute(array(':cle' => $cle));
    } catch (Exception $erreur) {
        if (function_exists('journal_api')) journal_api('BUTIN : attribution impossible : ' . $erreur->getMessage());
    }
}

// Le joueur recupere son butin en attente (supprime cote serveur une fois remis).
function reclamer_butin($pdo, $membreId) {
    $pdo->beginTransaction();
    try {
        $requete = $pdo->prepare('SELECT id, objet, quantite FROM monde_butin WHERE membre_id = :membre ORDER BY id FOR UPDATE');
        $requete->execute(array(':membre' => $membreId));
        $lignes = $requete->fetchAll();
        $ids = array();
        $butin = array();
        foreach ($lignes as $ligne) {
            $ids[] = (int)$ligne['id'];
            $butin[] = array('objet' => (string)$ligne['objet'], 'quantite' => (int)$ligne['quantite']);
        }
        if (count($ids) > 0) {
            $suppression = $pdo->prepare('DELETE FROM monde_butin WHERE membre_id = :membre AND id IN (' . implode(',', $ids) . ')');
            $suppression->execute(array(':membre' => $membreId));
        }
        $pdo->commit();
    } catch (Exception $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        throw $erreur;
    }
    return array('message' => 'Butin recu.', 'extra' => array('butin' => $butin));
}

// Un coup porte sur un monstre : les pv baissent pour tout le monde. Un monstre
// mort se releve 10 s apres sa mort (remise a pleine vie au premier coup apres ce delai).
function traiter_coup_monstre($pdo, $donnees, $auteur) {
    $cle = valider_cle_monstre($donnees);
    $degats = isset($donnees['degats']) ? (int)$donnees['degats'] : 0;
    if ($degats < 1 || $degats > 999) throw new InvalidArgumentException('Degats invalides.');
    // « tue = 1 » (coup du garde) : le monstre meurt, quels que soient ses PV.
    $tue = isset($donnees['tue']) && (string)$donnees['tue'] === '1';
    $pdo->beginTransaction();
    try {
        $regens = regen_par_type($pdo);
        $requete = $pdo->prepare('SELECT type, pv, pv_max, mort, TIMESTAMPDIFF(SECOND, modifie_le, NOW()) AS age,'
            . ' TIMESTAMPDIFF(SECOND, pv_modifie_le, NOW()) AS pv_age'
            . ' FROM monde_monstre WHERE cle = :cle FOR UPDATE');
        $requete->execute(array(':cle' => $cle));
        $ligne = $requete->fetch();
        if (!$ligne) throw new InvalidArgumentException('Monstre inconnu : declaration manquante.');
        $pvMax = (int)$ligne['pv_max'];
        $mort = (int)$ligne['mort'] === 1;
        if ($mort) {
            $pdo->commit();
            return array('message' => 'Monstre deja mort.', 'extra' => array());
        }
        $pvAvant = pv_courant($ligne, $regens);
        $pv = $tue ? 0 : max(0, $pvAvant - $degats);
        $mort = ($pv === 0);
        // Coup du garde (tue) : le garde a tue le monstre, donc ni degats comptes, ni butin.
        // Les degats deja comptes par les joueurs sont effaces.
        if ($tue) {
            if ($mort) {
                $effacement = $pdo->prepare('DELETE FROM monde_degats WHERE cle = :cle');
                $effacement->execute(array(':cle' => $cle));
            }
        } else {
            enregistrer_degats($pdo, $cle, $auteur, min($degats, $pvAvant));
            if ($mort) attribuer_butin_mort($pdo, $cle, (string)$ligne['type']);
        }
        $rev = prochaine_revision($pdo);
        $miseAJour = $pdo->prepare('UPDATE monde_monstre SET pv = :pv, mort = :mort, revision = :rev, auteur_id = :auteur,'
            . ' pv_modifie_le = NOW() WHERE cle = :cle');
        $miseAJour->execute(array(':pv' => $pv, ':mort' => $mort ? 1 : 0, ':rev' => $rev,
            ':auteur' => $auteur, ':cle' => $cle));
        $pdo->commit();
    } catch (Exception $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        throw $erreur;
    }
    return array('message' => 'Coup enregistre.', 'extra' => array('revision' => $rev,
        'monstre' => array('cle' => $cle, 'pv' => $pv, 'pv_max' => $pvMax, 'mort' => $mort ? 1 : 0)));
}

// Liste envoyee par le client : tableau JSON (ou deja decode).
function liste_monstres_recue($donnees) {
    $brut = isset($donnees['monstres']) ? $donnees['monstres'] : '[]';
    $liste = is_string($brut) ? json_decode($brut, true) : $brut;
    if (!is_array($liste) || count($liste) > 2000) throw new InvalidArgumentException('Liste de monstres invalide.');
    return $liste;
}

// Pilotage : un client pilote les monstres dont il a la charge (position calculee chez lui).
// Un monstre est pris si personne ne le pilote, si c'est deja nous, ou si le pilote
// precedent n'a pas envoye de mise a jour depuis 2 s. Renvoie les monstres pris.
function traiter_pilotage_monstres($pdo, $donnees, $auteur) {
    $liste = liste_monstres_recue($donnees);
    $rev = prochaine_revision($pdo);
    $pilotes = array();
    $pdo->beginTransaction();
    try {
        $lecture = $pdo->prepare('SELECT proprietaire_id, mort, TIMESTAMPDIFF(SECOND, modifie_le, NOW()) AS age'
            . ' FROM monde_monstre WHERE cle = :cle FOR UPDATE');
        $miseAJour = $pdo->prepare('UPDATE monde_monstre SET proprietaire_id = :auteur, position_x = :x,'
            . ' position_z = :z, rotation_y = :rot, revision = :rev WHERE cle = :cle');
        foreach ($liste as $monstre) {
            if (!is_array($monstre)) continue;
            $cle = valider_cle_monstre($monstre);
            $x = nombre_monde(isset($monstre['x']) ? $monstre['x'] : 0, -130, 130);
            $z = nombre_monde(isset($monstre['z']) ? $monstre['z'] : 0, -130, 130);
            $rot = nombre_monde(isset($monstre['rot']) ? $monstre['rot'] : 0, -360, 360);
            $lecture->execute(array(':cle' => $cle));
            $ligne = $lecture->fetch();
            if (!$ligne || (int)$ligne['mort'] === 1) continue;
            $proprietaire = $ligne['proprietaire_id'] === null ? 0 : (int)$ligne['proprietaire_id'];
            // -1 : monstre tenu par le serveur (jeu/serveur/mondeLV.py). Les jeux ne le pilotent plus.
            if ($proprietaire === -1) continue;
            if (!($proprietaire === 0 || $proprietaire === (int)$auteur || (int)$ligne['age'] >= 2)) continue;
            $miseAJour->execute(array(':auteur' => $auteur, ':x' => $x, ':z' => $z, ':rot' => $rot,
                ':rev' => $rev, ':cle' => $cle));
            $pilotes[] = $cle;
        }
        $pdo->commit();
    } catch (Exception $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        throw $erreur;
    }
    return array('message' => 'Pilotage enregistre.', 'extra' => array('revision' => $rev, 'pilotes' => $pilotes));
}

// Ressurection : un monstre mort depuis 10 s revient a pleine vie, au point donne
// par le client (une seule fois : le premier qui le demande).
function traiter_ressurection_monstres($pdo, $donnees) {
    $liste = liste_monstres_recue($donnees);
    $rev = prochaine_revision($pdo);
    $ressuscites = array();
    $pdo->beginTransaction();
    try {
        $lecture = $pdo->prepare('SELECT pv_max, mort, TIMESTAMPDIFF(SECOND, modifie_le, NOW()) AS age'
            . ' FROM monde_monstre WHERE cle = :cle FOR UPDATE');
        $miseAJour = $pdo->prepare('UPDATE monde_monstre SET mort = 0, pv = pv_max, position_x = :x,'
            . ' position_z = :z, proprietaire_id = NULL, revision = :rev WHERE cle = :cle');
        foreach ($liste as $monstre) {
            if (!is_array($monstre)) continue;
            $cle = valider_cle_monstre($monstre);
            $x = nombre_monde(isset($monstre['x']) ? $monstre['x'] : 0, -130, 130);
            $z = nombre_monde(isset($monstre['z']) ? $monstre['z'] : 0, -130, 130);
            $lecture->execute(array(':cle' => $cle));
            $ligne = $lecture->fetch();
            if (!$ligne || (int)$ligne['mort'] !== 1 || (int)$ligne['age'] < 10) continue;
            $miseAJour->execute(array(':x' => $x, ':z' => $z, ':rev' => $rev, ':cle' => $cle));
            $ressuscites[] = $cle;
        }
        $pdo->commit();
    } catch (Exception $erreur) {
        if ($pdo->inTransaction()) $pdo->rollBack();
        throw $erreur;
    }
    return array('message' => 'Ressurection traitee.', 'extra' => array(
        'revision' => $rev, 'monstres' => lire_monstres_par_cle($pdo, $ressuscites)));
}

// Ecritures du monde (admin seulement, controle fait dans api.php).
// Renvoie le tableau de reponse ; lance InvalidArgumentException si la saisie est fausse.
function traiter_monde($pdo, $action, $donnees, $membre) {
    $auteur = (int)$membre['id'];
    if ($action === 'ajouter_element_monde') {
        $element = valider_element_monde($donnees);
        $rev = prochaine_revision($pdo);
        $requete = $pdo->prepare('INSERT INTO monde_element (genre, type, position_x, position_z, rotation_y, taille, auteur_id, revision)'
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
        $requete = $pdo->prepare("UPDATE monde_element SET type = :type, position_x = :x, position_z = :z,"
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
        $requete = $pdo->prepare("UPDATE monde_element SET supprime = 1, revision = :rev WHERE id = :id AND genre = 'element'");
        $requete->execute(array(':rev' => $rev, ':id' => $id));
        return array('message' => 'Element supprime.', 'extra' => array('revision' => $rev));
    }
    if ($action === 'vider_monde') {
        $rev = prochaine_revision($pdo);
        $requete = $pdo->prepare("UPDATE monde_element SET supprime = 1, revision = :rev WHERE genre = 'element' AND supprime = 0");
        $requete->execute(array(':rev' => $rev));
        return array('message' => 'Elements du monde supprimes.', 'extra' => array('revision' => $rev));
    }
    if ($action === 'declarer_monstres') return traiter_declaration_monstres($pdo, $donnees);
    if ($action === 'declarer_obstacles') return traiter_declaration_obstacles($pdo, $donnees);
    if ($action === 'frapper_monstre') return traiter_coup_monstre($pdo, $donnees, $auteur);
    if ($action === 'reclamer_butin') return reclamer_butin($pdo, $auteur);
    if ($action === 'piloter_monstres') return traiter_pilotage_monstres($pdo, $donnees, $auteur);
    if ($action === 'ressusciter_monstres') return traiter_ressurection_monstres($pdo, $donnees);
    if ($action === 'enregistrer_maison') {
        $maison = valider_maison_monde($donnees);
        $rev = prochaine_revision($pdo);
        // 'edition' : objets de l'editeur (fichier edition/coordonee) ; 'maison' par defaut.
        $genre = (isset($donnees['genre']) && (string)$donnees['genre'] === 'edition') ? 'edition' : 'maison';
        // Une maison existante reste dans la table ou elle se trouve (evite les doublons) ;
        // une nouvelle maison va dans monde_maison, un nouvel objet 'edition' dans monde_element.
        $table = $genre === 'edition' ? 'monde_element' : 'monde_maison';
        foreach (array('monde_maison', 'monde_element') as $tableExistante) {
            $verification = $pdo->prepare('SELECT 1 FROM ' . $tableExistante . ' WHERE cle = :cle LIMIT 1');
            $verification->execute(array(':cle' => $maison['id']));
            if ($verification->fetchColumn() !== false) { $table = $tableExistante; break; }
        }
        if ($table === 'monde_maison') {
            // Le nom et le territoire ne sont pas modifies ici : seul renommer_maison le change.
            $surfaces = surfaces_maison($maison['donnees']);
            $requete = $pdo->prepare('INSERT INTO monde_maison (genre, cle, nom, territoire, type, position_x, position_z,'
                . ' rotation_y, donnees, papier, mur, porte, pancarte, fenetre, toit, sol, auteur_id, revision)'
                . " VALUES (:genre, :cle, 'sans nom', 'Depart', 'MAISON', :x, :z, :rotation, :donnees,"
                . ' :papier, :mur, :porte, :pancarte, :fenetre, :toit, :sol, :auteur, :rev)'
                . ' ON DUPLICATE KEY UPDATE position_x = VALUES(position_x), position_z = VALUES(position_z),'
                . ' rotation_y = VALUES(rotation_y), donnees = VALUES(donnees), papier = VALUES(papier),'
                . ' mur = VALUES(mur), porte = VALUES(porte), pancarte = VALUES(pancarte),'
                . ' fenetre = VALUES(fenetre), toit = VALUES(toit), sol = VALUES(sol),'
                . ' auteur_id = VALUES(auteur_id), supprime = 0, revision = VALUES(revision)');
            $requete->execute(array(':genre' => $genre, ':cle' => $maison['id'], ':x' => $maison['x'], ':z' => $maison['z'],
                ':rotation' => $maison['rotation'], ':donnees' => $maison['donnees'], ':papier' => $surfaces['papier'],
                ':mur' => $surfaces['mur'], ':porte' => $surfaces['porte'], ':pancarte' => $surfaces['pancarte'],
                ':fenetre' => $surfaces['fenetre'], ':toit' => $surfaces['toit'], ':sol' => $surfaces['sol'],
                ':auteur' => $auteur, ':rev' => $rev));
        } else {
            $requete = $pdo->prepare("INSERT INTO $table (genre, cle, type, position_x, position_z, rotation_y, donnees, auteur_id, revision)"
                . " VALUES (:genre, :cle, 'MAISON', :x, :z, :rotation, :donnees, :auteur, :rev)"
                . " ON DUPLICATE KEY UPDATE position_x = VALUES(position_x), position_z = VALUES(position_z),"
                . " rotation_y = VALUES(rotation_y), donnees = VALUES(donnees), auteur_id = VALUES(auteur_id),"
                . " supprime = 0, revision = VALUES(revision)");
            $requete->execute(array(':genre' => $genre, ':cle' => $maison['id'], ':x' => $maison['x'], ':z' => $maison['z'],
                ':rotation' => $maison['rotation'], ':donnees' => $maison['donnees'], ':auteur' => $auteur, ':rev' => $rev));
        }
        return array('message' => 'Maison enregistree.', 'extra' => array('revision' => $rev));
    }
    if ($action === 'renommer_maison') {
        $maison = valider_nom_maison($donnees);
        $verification = $pdo->prepare('SELECT 1 FROM monde_maison WHERE cle = :cle AND supprime = 0 LIMIT 1');
        $verification->execute(array(':cle' => $maison['id']));
        if ($verification->fetchColumn() === false) {
            throw new InvalidArgumentException("Maison introuvable dans la base : modifiez-la une fois pour l'enregistrer.");
        }
        $rev = prochaine_revision($pdo);
        $requete = $pdo->prepare('UPDATE monde_maison SET nom = :nom, revision = :rev WHERE cle = :cle');
        $requete->execute(array(':nom' => $maison['nom'], ':rev' => $rev, ':cle' => $maison['id']));
        return array('message' => 'Nom de la maison enregistre.', 'extra' => array('revision' => $rev));
    }
    throw new InvalidArgumentException('Action monde inconnue.');
}

// Presence des joueurs : enregistre la position du joueur connecte et renvoie
// les autres joueurs vus il y a moins de 10 secondes.
function mettre_a_jour_presence($pdo, $membre, $donnees) {
    $x = nombre_monde(isset($donnees['x']) ? $donnees['x'] : 0, -130, 130);
    $z = nombre_monde(isset($donnees['z']) ? $donnees['z'] : 0, -130, 130);
    $rotation = nombre_monde(isset($donnees['rotation']) ? $donnees['rotation'] : 0, -360, 360);
    $animation = isset($donnees['animation']) ? substr(preg_replace('/[^A-Za-z_]/', '', (string)$donnees['animation']), 0, 20) : 'Idle';
    if ($animation === '') $animation = 'Idle';
    $id = (int)$membre['id'];
    $pseudo = mb_substr((string)$membre['pseudo'], 0, 40);
    $requete = $pdo->prepare('INSERT INTO monde_presence (membre_id, pseudo, position_x, position_z, rotation_y, animation, mis_a_jour)'
        . ' VALUES (:id, :pseudo, :x, :z, :rotation, :animation, NOW())'
        . ' ON DUPLICATE KEY UPDATE pseudo = VALUES(pseudo), position_x = VALUES(position_x),'
        . ' position_z = VALUES(position_z), rotation_y = VALUES(rotation_y), animation = VALUES(animation), mis_a_jour = NOW()');
    $requete->execute(array(':id' => $id, ':pseudo' => $pseudo, ':x' => $x, ':z' => $z,
        ':rotation' => $rotation, ':animation' => $animation));
    $requete = $pdo->prepare('SELECT membre_id, pseudo, position_x, position_z, rotation_y, animation FROM monde_presence'
        . ' WHERE membre_id <> :id AND mis_a_jour > NOW() - INTERVAL 10 SECOND');
    $requete->execute(array(':id' => $id));
    $joueurs = array();
    foreach ($requete->fetchAll() as $ligne) {
        $joueurs[] = array(
            'membre_id' => (int)$ligne['membre_id'], 'pseudo' => (string)$ligne['pseudo'],
            'position_x' => (float)$ligne['position_x'], 'position_z' => (float)$ligne['position_z'],
            'rotation_y' => (float)$ligne['rotation_y'], 'animation' => (string)$ligne['animation'],
        );
    }
    return array('joueurs' => $joueurs);
}

