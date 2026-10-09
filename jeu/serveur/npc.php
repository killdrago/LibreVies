<?php
// Profils de NPC : lecture joueur, modification uniquement apres controle droit=1 dans api.php.
function identifiants_npc_village() {
    return array('maire', 'forgeron', 'marchand', 'esthetique', 'garde_nord', 'garde_sud');
}

function lire_npcs_village($pdo) {
    $requete = $pdo->prepare('SELECT * FROM npc WHERE id IN '
        . "('maire', 'forgeron', 'marchand', 'esthetique', 'garde_nord', 'garde_sud')");
    $requete->execute();
    $profils = array();
    foreach ($requete->fetchAll() as $ligne) {
        $id = strtolower((string)$ligne['id']);
        try {
            $profil = valider_profil_personnage($ligne);
        } catch (InvalidArgumentException $erreur) {
            throw new InvalidArgumentException('Profil NPC ' . $id . ' incomplet/invalide : '
                . $erreur->getMessage() . ' Completez ce profil dans la table npc.');
        }
        $profils[$id] = array_merge(array('id' => $id), $profil,
            array('objets' => isset($ligne['objets']) ? (string)$ligne['objets'] : null));
    }
    $resultat = array();
    foreach (identifiants_npc_village() as $id) {
        if (!isset($profils[$id])) {
            throw new InvalidArgumentException('NPC manquant : ' . $id
                . '. Ajoutez son profil complet dans la table npc.');
        }
        $resultat[] = $profils[$id];
    }
    return $resultat;
}

// Les noms de colonnes sont une liste serveur, l'id est parametre.
// Ni id/default/objets/droit dans le profil ne peuvent elever des permissions.
function enregistrer_npc($pdo, $id, $profil) {
    $colonnes = array_merge(array('sexe'), colonnes_sliders_personnage(),
        array('teinte_peau', 'coiffure', 'chaussures', 'chapeau', 'tenue'));
    $parametres = array(':id' => $id);
    $affectations = array();
    foreach ($colonnes as $colonne) {
        $affectations[] = $colonne . ' = :' . $colonne;
        $parametres[':' . $colonne] = $profil[$colonne];
    }
    $requete = $pdo->prepare('UPDATE npc SET ' . implode(', ', $affectations) . ' WHERE id = :id');
    $requete->execute($parametres);
    // objets est preserve : les accessoires de metier sont independants de l'esthetique.
}
