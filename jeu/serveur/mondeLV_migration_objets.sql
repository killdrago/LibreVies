-- Migration de la base mondeLV : maisons -> monde_maison, tout le reste -> monde_element,
-- puis monde_objet est vide (table gardee pour plus tard).
--
-- A EXECUTER DANS PHPMYADMIN, base mondeLV, ETAPE PAR ETAPE :
--   Etape 1 : bloc 1 puis bloc 2 (copie). Ne rien supprimer.
--   Etape 2 : verifier les compteurs affiches par le bloc 3.
--   Etape 3 : seulement si les compteurs sont bons, executer le bloc 4 (vide monde_objet).
-- Ne lancer ce script QUE avec le monde.php mis a jour (il lit les nouvelles tables),
-- sinon le jeu affichera un monde vide.
-- Les anciennes tables monde_element et monde_maison sont renommees (sauvegarde),
-- jamais supprimees ici.

USE mondeLV;
SET NAMES utf8mb4;

-- ===== Bloc 1 : mise de cote des anciennes tables homonymes (a executer une fois) =====
RENAME TABLE monde_element TO monde_element_ancien, monde_maison TO monde_maison_ancien;

-- ===== Bloc 1b : nouvelles tables (meme structure que monde_objet) =====
CREATE TABLE IF NOT EXISTS monde_maison (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  genre VARCHAR(20) NOT NULL,
  cle VARCHAR(100) NULL DEFAULT NULL,
  type VARCHAR(30) NOT NULL DEFAULT '',
  position_x DECIMAL(10,3) NOT NULL DEFAULT 0,
  position_z DECIMAL(10,3) NOT NULL DEFAULT 0,
  rotation_y DECIMAL(10,3) NOT NULL DEFAULT 0,
  taille DECIMAL(10,3) NOT NULL DEFAULT 1,
  donnees TEXT NULL,
  supprime TINYINT(1) NOT NULL DEFAULT 0,
  revision BIGINT UNSIGNED NOT NULL DEFAULT 0,
  auteur_id INT NULL DEFAULT NULL,
  cree_le TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  modifie_le TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY idx_monde_maison_cle (cle),
  KEY idx_monde_maison_revision (revision),
  KEY idx_monde_maison_genre (genre)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE IF NOT EXISTS monde_element (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  genre VARCHAR(20) NOT NULL,
  cle VARCHAR(100) NULL DEFAULT NULL,
  type VARCHAR(30) NOT NULL DEFAULT '',
  position_x DECIMAL(10,3) NOT NULL DEFAULT 0,
  position_z DECIMAL(10,3) NOT NULL DEFAULT 0,
  rotation_y DECIMAL(10,3) NOT NULL DEFAULT 0,
  taille DECIMAL(10,3) NOT NULL DEFAULT 1,
  donnees TEXT NULL,
  supprime TINYINT(1) NOT NULL DEFAULT 0,
  revision BIGINT UNSIGNED NOT NULL DEFAULT 0,
  auteur_id INT NULL DEFAULT NULL,
  cree_le TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  modifie_le TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY idx_monde_element_cle (cle),
  KEY idx_monde_element_revision (revision),
  KEY idx_monde_element_genre (genre)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- ===== Bloc 2 : copie (les ids et revisions sont conserves) =====
INSERT INTO monde_maison (id, genre, cle, type, position_x, position_z, rotation_y, taille, donnees, supprime, revision, auteur_id, cree_le, modifie_le)
SELECT id, genre, cle, type, position_x, position_z, rotation_y, taille, donnees, supprime, revision, auteur_id, cree_le, modifie_le
FROM monde_objet WHERE genre = 'maison';

INSERT INTO monde_element (id, genre, cle, type, position_x, position_z, rotation_y, taille, donnees, supprime, revision, auteur_id, cree_le, modifie_le)
SELECT id, genre, cle, type, position_x, position_z, rotation_y, taille, donnees, supprime, revision, auteur_id, cree_le, modifie_le
FROM monde_objet WHERE genre <> 'maison';

-- ===== Bloc 3 : verification (les trois nombres doivent etre egaux) =====
SELECT
  (SELECT COUNT(*) FROM monde_objet) AS total_ancien,
  (SELECT COUNT(*) FROM monde_maison) AS maisons_copiees,
  (SELECT COUNT(*) FROM monde_element) AS elements_copies,
  (SELECT COUNT(*) FROM monde_maison) + (SELECT COUNT(*) FROM monde_element) AS total_nouveau;

-- ===== Bloc 4 : vider monde_objet (a lancer seulement si bloc 3 est correct) =====
DELETE FROM monde_objet;
