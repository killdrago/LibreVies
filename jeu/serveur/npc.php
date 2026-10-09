<?php
// Profils de NPC lus exclusivement cote serveur, sans modification depuis le jeu.
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
                . $erreur->getMessage() . ' Executez jeu/serveur/remplir_npc.sql.');
        }
        $profils[$id] = array_merge(array('id' => $id), $profil,
            array('objets' => isset($ligne['objets']) ? (string)$ligne['objets'] : null));
    }
    $resultat = array();
    foreach (identifiants_npc_village() as $id) {
        if (!isset($profils[$id])) {
            throw new InvalidArgumentException('NPC manquant : ' . $id
                . '. Executez jeu/serveur/remplir_npc.sql.');
        }
        $resultat[] = $profils[$id];
    }
    return $resultat;
}
