-- Base du monde partage LibreVies : tout ce qui se passe dans le monde.
-- A executer une seule fois dans phpMyAdmin (onglet SQL), sur n'importe quelle base.
-- Le jeu lit et ecrit cette base via l'API (monde.php) ; il n'y a pas d'acces direct.

CREATE DATABASE IF NOT EXISTS mondeLV
  CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE mondeLV;

-- Compteur global : chaque modification recoit le numero suivant.
CREATE TABLE IF NOT EXISTS monde_compteur (
  id TINYINT UNSIGNED NOT NULL,
  revision BIGINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO monde_compteur (id, revision) VALUES (1, 0);

-- Un objet du monde par ligne. genre = element (arbre, caillou, pont, eau, herbe...)
-- ou maison (cle = identifiant lisible, donnees = reglages en JSON).
-- supprime = 1 : l'objet est retire ; la ligne reste pour que les joueurs le voient disparaitre.
CREATE TABLE IF NOT EXISTS monde_objet (
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
  UNIQUE KEY idx_monde_objet_cle (cle),
  KEY idx_monde_objet_revision (revision),
  KEY idx_monde_objet_genre (genre)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
