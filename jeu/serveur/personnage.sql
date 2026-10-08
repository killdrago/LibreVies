-- Personnage LibreVies : un personnage par compte.
-- personnage.id reprend exactement membre.id.
-- Les reglages de sliders, la tenue et les objets sont stockes en JSON texte
-- pour rester compatibles avec MySQL 5.6. Les ressources 3D restent dans le jeu.

USE librevies;

CREATE TABLE IF NOT EXISTS personnage (
  id INT UNSIGNED NOT NULL,
  `default` TINYINT(1) NOT NULL DEFAULT 1,
  sexe ENUM('homme', 'femme') NOT NULL DEFAULT 'homme',
  sliders TEXT NOT NULL,
  teinte_peau VARCHAR(32) NOT NULL DEFAULT '',
  coiffure VARCHAR(100) NOT NULL DEFAULT '',
  chaussures VARCHAR(100) NOT NULL DEFAULT '',
  chapeau VARCHAR(100) NOT NULL DEFAULT '',
  tenue TEXT NOT NULL,
  objets TEXT NOT NULL,
  cree_le DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  modifie_le DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  CONSTRAINT fk_personnage_membre
    FOREIGN KEY (id) REFERENCES membre (id)
    ON UPDATE CASCADE
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
