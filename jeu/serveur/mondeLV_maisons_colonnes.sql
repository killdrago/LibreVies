-- Maisons : nom, territoire et une colonne par partie (papier, mur, porte, pancarte, fenetre, toit, sol).
-- A executer une seule fois dans phpMyAdmin (onglet SQL), base mondeLV.
-- Syntaxe MySQL : pas de IF NOT EXISTS. Si une colonne existe deja, MySQL repond "Duplicate column" :
-- dans ce cas, supprimer de la requete la ligne de la colonne concernee et relancer.

USE mondeLV;

-- 1. Nouvelles colonnes. Les maisons existantes recoivent 'sans nom' et 'Depart'.
ALTER TABLE monde_maison
  ADD COLUMN nom VARCHAR(60) NOT NULL DEFAULT 'sans nom' AFTER cle,
  ADD COLUMN territoire VARCHAR(40) NOT NULL DEFAULT 'Depart' AFTER nom,
  ADD COLUMN papier VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN mur VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN porte VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN pancarte VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN fenetre VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN toit VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN sol VARCHAR(255) NULL DEFAULT NULL;

-- 2. Compteurs avant de deplacer les maisons restees dans monde_element.
SELECT 'monde_element (maisons)' AS table_, COUNT(*) AS nombre FROM monde_element WHERE type = 'MAISON'
UNION ALL SELECT 'monde_maison', COUNT(*) FROM monde_maison;

-- 3. Copie des maisons de monde_element vers monde_maison (les maisons deja presentes sont gardees).
INSERT IGNORE INTO monde_maison (genre, cle, nom, territoire, type, position_x, position_z, rotation_y, taille,
    donnees, supprime, revision, auteur_id, cree_le, modifie_le)
SELECT 'maison', cle, 'sans nom', 'Depart', type, position_x, position_z, rotation_y, taille,
    donnees, supprime, revision, auteur_id, cree_le, modifie_le
FROM monde_element WHERE type = 'MAISON';

-- 4. Suppression des maisons de monde_element : a lancer seulement si les compteurs ci-dessous sont bons.
DELETE FROM monde_element WHERE type = 'MAISON';

-- 5. Compteurs apres migration.
SELECT 'monde_maison (apres)' AS table_, COUNT(*) AS nombre FROM monde_maison
UNION ALL SELECT 'monde_element (maisons restantes)', COUNT(*) FROM monde_element WHERE type = 'MAISON';
