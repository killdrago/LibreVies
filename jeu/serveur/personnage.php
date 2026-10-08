<?php
/**
 * Contrat du personnage : une colonne SQL par reglage, jamais de JSON en BDD.
 * Les valeurs internes des sliders existants vont de 0 a 5 (affichage 0..500).
 * volume conserve le slider "Jambes largeur" ; seins conserve "Seins volume".
 */

function colonnes_sliders_personnage() {
    return array(
        'tete', 'yeux', 'nez', 'bouche', 'oreilles', 'seins', 'volume',
        'hanche', 'ventre', 'largeur_bras', 'longueur_bras',
        'hauteur_jambe', 'pieds',
    );
}

function catalogue_personnage($sexe) {
    return array(
        'teinte_peau' => array('0', '1', '2', '3', '4', '5', '6'),
        'coiffure' => array('short_messy', 'straight_bangs', 'shaggy_green',
            'strawberry_cloud', 'faydaen_hair_1'),
        'chaussures' => array(null, 'shoes01', 'shoes02', 'shoes03',
            'shoes04', 'shoes05', 'shoes06'),
        'chapeau' => array(null, 'fedora01'),
        'tenue' => $sexe === 'femme'
            ? array('female_sportsuit01', 'female_casualsuit01',
                'female_casualsuit02', 'female_elegantsuit01')
            : array('male_casualsuit04', 'male_casualsuit05', 'male_casualsuit06',
                'male_elegantsuit01', 'male_worksuit01'),
    );
}

function valider_profil_personnage($donnees) {
    if (!is_array($donnees) || !isset($donnees['sexe'])
        || !in_array($donnees['sexe'], array('homme', 'femme'), true)) {
        throw new InvalidArgumentException('Sexe du personnage invalide.');
    }
    $profil = array('sexe' => $donnees['sexe']);
    foreach (colonnes_sliders_personnage() as $colonne) {
        if (!isset($donnees[$colonne])
            || !(is_string($donnees[$colonne]) || is_int($donnees[$colonne])
                || is_float($donnees[$colonne]))
            || !is_numeric($donnees[$colonne])) {
            throw new InvalidArgumentException('Reglage manquant ou invalide : ' . $colonne . '.');
        }
        $valeur = (float)$donnees[$colonne];
        if (!is_finite($valeur) || $valeur < 0 || $valeur > 5) {
            throw new InvalidArgumentException('Reglage hors limites (0 a 5) : ' . $colonne . '.');
        }
        // Meme precision que DECIMAL(10,4), utilisee aussi dans la reponse.
        $profil[$colonne] = round($valeur, 4);
    }
    foreach (catalogue_personnage($profil['sexe']) as $champ => $choix) {
        if (!array_key_exists($champ, $donnees)) {
            throw new InvalidArgumentException('Choix de personnage manquant : ' . $champ . '.');
        }
        $valeur = $donnees[$champ];
        // JsonUtility peut transporter une string NULL comme une string vide.
        // "Aucun" est toujours NULL en BDD, pour ces deux choix uniquement.
        if (($champ === 'chapeau' || $champ === 'chaussures') && $valeur === '') $valeur = null;
        if (!in_array($valeur, $choix, true)) {
            throw new InvalidArgumentException('Choix de personnage invalide : ' . $champ . '.');
        }
        // Identifiants stables des ressources, pas leur position dans le menu.
        $profil[$champ] = $valeur;
    }
    return $profil;
}

function personnage_public($ligne) {
    if ((int)$ligne['default'] === 1) {
        // Aucun reglage n'est utilise pour la base primitive actuelle.
        return array('id' => (int)$ligne['id'], 'default' => 1);
    }
    if ((int)$ligne['default'] !== 0) {
        throw new InvalidArgumentException('Etat du personnage invalide.');
    }
    $profil = valider_profil_personnage($ligne);
    return array_merge(array('id' => (int)$ligne['id'], 'default' => 0), $profil,
        array('objets' => isset($ligne['objets']) ? (string)$ligne['objets'] : null));
}

function lire_personnage($pdo, $id) {
    $requete = $pdo->prepare('SELECT * FROM personnage WHERE id = :id LIMIT 1');
    $requete->execute(array(':id' => $id));
    $ligne = $requete->fetch();
    if (!$ligne) {
        // Compatibilite avec les comptes crees avant l'ajout de personnage.
        // INSERT IGNORE ne remplace jamais un personnage deja personnalise.
        $initial = $pdo->prepare('INSERT IGNORE INTO personnage (id, `default`) VALUES (:id, 1)');
        $initial->execute(array(':id' => $id));
        $requete->execute(array(':id' => $id));
        $ligne = $requete->fetch();
    }
    if (!$ligne) {
        throw new RuntimeException('Personnage du compte introuvable.');
    }
    return personnage_public($ligne);
}

function enregistrer_personnage($pdo, $id, $profil) {
    lire_personnage($pdo, $id);
    $parametres = array(':id' => $id, ':sexe' => $profil['sexe']);
    $affectations = array('`default` = 0', 'sexe = :sexe');
    $colonnes = array_merge(colonnes_sliders_personnage(),
        array('teinte_peau', 'coiffure', 'chaussures', 'chapeau', 'tenue'));
    foreach ($colonnes as $colonne) {
        // Les noms de colonnes viennent uniquement de cette liste serveur.
        $affectations[] = $colonne . ' = :' . $colonne;
        $parametres[':' . $colonne] = $profil[$colonne];
    }
    $requete = $pdo->prepare('UPDATE personnage SET '
        . implode(', ', $affectations) . ' WHERE id = :id');
    $requete->execute($parametres);
    // objets est conserve : le panel actuel ne gere pas encore ce slot.
    return lire_personnage($pdo, $id);
}
