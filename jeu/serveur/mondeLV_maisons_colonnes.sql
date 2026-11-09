-- Maisons : nom, territoire et une colonne par partie (papier, mur, porte, pancarte, fenetre, toit, sol).
-- A executer une seule fois dans phpMyAdmin (onglet SQL), base mondeLV.
-- Syntaxe ADD COLUMN IF NOT EXISTS : MariaDB (XAMPP) et MySQL 8.0.29+ l'acceptent.
-- Les colonnes sont remplies au prochain enregistrement de chaque maison.

USE mondeLV;

-- 1. Nouvelles colonnes. Les maisons existantes recoivent 'sans nom' et 'Depart'.
ALTER TABLE monde_maison
  ADD COLUMN IF NOT EXISTS nom VARCHAR(60) NOT NULL DEFAULT 'sans nom' AFTER cle,
  ADD COLUMN IF NOT EXISTS territoire VARCHAR(40) NOT NULL DEFAULT 'Depart' AFTER nom,
  ADD COLUMN IF NOT EXISTS papier VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS mur VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS porte VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS pancarte VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS fenetre VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS toit VARCHAR(255) NULL DEFAULT NULL,
  ADD COLUMN IF NOT EXISTS sol VARCHAR(255) NULL DEFAULT NULL;

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
