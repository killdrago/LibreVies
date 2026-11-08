-- LibreVies : monde partage en temps reel (lecture par tout joueur connecte,
-- ecriture reservee aux administrateurs via api.php).
-- A executer une fois dans la base librevies (phpMyAdmin ou ligne de commande).
USE librevies;

-- 1) Maisons : une ligne par maison, identifiant lisible choisi par l'administrateur
--    (ex. maison_forgeron). Les reglages de la maison (revetements, ouvertures,
--    pancarte...) sont stockes en JSON dans la colonne donnees.
CREATE TABLE IF NOT EXISTS monde_maison (
  id VARCHAR(100) NOT NULL,
  nom VARCHAR(60) NULL DEFAULT NULL,
  position_x DECIMAL(10,3) NOT NULL DEFAULT 0,
  position_z DECIMAL(10,3) NOT NULL DEFAULT 0,
  rotation_y DECIMAL(10,3) NOT NULL DEFAULT 0,
  donnees TEXT NULL,
  auteur_id INT NULL DEFAULT NULL,
  modifie_le TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

-- 2) Tout le reste du monde hors maisons : eau, herbe, cailloux, arbres, ponts.
--    Une ligne par element ; id numerique attribue par la base.
CREATE TABLE IF NOT EXISTS monde_element (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  type VARCHAR(20) NOT NULL,
  position_x DECIMAL(10,3) NOT NULL,
  position_z DECIMAL(10,3) NOT NULL,
  rotation_y DECIMAL(10,3) NOT NULL DEFAULT 0,
  taille DECIMAL(10,3) NOT NULL DEFAULT 1,
  donnees TEXT NULL,
  auteur_id INT NULL DEFAULT NULL,
  cree_le TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP,
  modifie_le TIMESTAMP NOT NULL DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  KEY idx_monde_element_type (type)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;
