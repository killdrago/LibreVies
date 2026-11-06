-- LibreVies : ajout du nom des PNJ dans la table npc.
-- A executer UNE SEULE FOIS dans phpMyAdmin, sur la base librevies.
-- Ne supprime aucune donnee : ajoute la colonne nom puis renseigne les 6 PNJ.
USE librevies;

-- 1) Verification avant : si la colonne nom existe deja, ne pas lancer l'etape 2.
SHOW COLUMNS FROM npc LIKE 'nom';

-- 2) Ajout de la colonne (si elle existe deja, MySQL repond "Duplicate column" : rien n'est casse).
ALTER TABLE npc
  ADD COLUMN nom VARCHAR(60) NULL DEFAULT NULL AFTER id;

-- 3) Noms : hommes pour le maire, le forgeron, le marchand et les gardes ;
--    femme pour l'esthetique.
UPDATE npc SET nom = 'Henri Morel'      WHERE id = 'maire';
UPDATE npc SET nom = 'Bastien Roux'     WHERE id = 'forgeron';
UPDATE npc SET nom = 'Gaston Lambert'   WHERE id = 'marchand';
UPDATE npc SET nom = 'Amélie Rousseau'  WHERE id = 'esthetique';
UPDATE npc SET nom = 'Thomas Girard'    WHERE id = 'garde_nord';
UPDATE npc SET nom = 'Lucas Marchal'    WHERE id = 'garde_sud';

-- 4) Verification : le nom doit etre renseigne, et le sexe doit correspondre.
SELECT id, sexe, nom
FROM npc
WHERE id IN ('maire', 'forgeron', 'marchand', 'esthetique', 'garde_nord', 'garde_sud')
ORDER BY id;
