-- Personnage LibreVies : un personnage par compte.
-- personnage.id reprend exactement membre.id.
-- Chaque reglage de slider possede sa propre colonne.

USE librevies;

CREATE TABLE IF NOT EXISTS personnage (
  id INT UNSIGNED NOT NULL,
  `default` TINYINT(1) NOT NULL DEFAULT 1,
  sexe ENUM('homme', 'femme') NOT NULL DEFAULT 'homme',

  tete DECIMAL(10,4) NOT NULL DEFAULT 0,
  yeux DECIMAL(10,4) NOT NULL DEFAULT 0,
  nez DECIMAL(10,4) NOT NULL DEFAULT 0,
  bouche DECIMAL(10,4) NOT NULL DEFAULT 0,
  oreilles DECIMAL(10,4) NOT NULL DEFAULT 0,
  seins DECIMAL(10,4) NOT NULL DEFAULT 0,
  volume DECIMAL(10,4) NOT NULL DEFAULT 0,
  hanche DECIMAL(10,4) NOT NULL DEFAULT 0,
  ventre DECIMAL(10,4) NOT NULL DEFAULT 0,
  largeur_bras DECIMAL(10,4) NOT NULL DEFAULT 0,
  longueur_bras DECIMAL(10,4) NOT NULL DEFAULT 0,
  hauteur_jambe DECIMAL(10,4) NOT NULL DEFAULT 0,
  pieds DECIMAL(10,4) NOT NULL DEFAULT 0,

  teinte_peau VARCHAR(32) NOT NULL DEFAULT '',
  coiffure VARCHAR(100) NOT NULL DEFAULT '',
  chaussures VARCHAR(100) NOT NULL DEFAULT '',
  chapeau VARCHAR(100) NOT NULL DEFAULT '',
  tenue VARCHAR(100) NOT NULL DEFAULT '',
  objets TEXT NOT NULL,

  PRIMARY KEY (id),
  CONSTRAINT fk_personnage_membre
    FOREIGN KEY (id) REFERENCES membre (id)
    ON UPDATE CASCADE
    ON DELETE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;
