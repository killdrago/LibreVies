-- Personnage LibreVies : un personnage par compte.
-- personnage.id reprend exactement membre.id.
-- A la creation du compte, seul id et `default` sont renseignes.
-- Tous les autres champs restent NULL jusqu'a la creation du personnage.

USE librevies;

CREATE TABLE IF NOT EXISTS personnage (
  id INT UNSIGNED NOT NULL,
  `default` TINYINT(1) NOT NULL DEFAULT 1,
  sexe ENUM('homme', 'femme') NULL DEFAULT NULL,

  tete DECIMAL(10,4) NULL DEFAULT NULL,
  yeux DECIMAL(10,4) NULL DEFAULT NULL,
  nez DECIMAL(10,4) NULL DEFAULT NULL,
  bouche DECIMAL(10,4) NULL DEFAULT NULL,
  oreilles DECIMAL(10,4) NULL DEFAULT NULL,
  seins DECIMAL(10,4) NULL DEFAULT NULL,
  volume DECIMAL(10,4) NULL DEFAULT NULL,
  hanche DECIMAL(10,4) NULL DEFAULT NULL,
  ventre DECIMAL(10,4) NULL DEFAULT NULL,
  largeur_bras DECIMAL(10,4) NULL DEFAULT NULL,
  longueur_bras DECIMAL(10,4) NULL DEFAULT NULL,
  hauteur_jambe DECIMAL(10,4) NULL DEFAULT NULL,
  pieds DECIMAL(10,4) NULL DEFAULT NULL,

  teinte_peau VARCHAR(32) NULL DEFAULT NULL,
  coiffure VARCHAR(100) NULL DEFAULT NULL,
  chaussures VARCHAR(100) NULL DEFAULT NULL,
  chapeau VARCHAR(100) NULL DEFAULT NULL,
  tenue VARCHAR(100) NULL DEFAULT NULL,
  objets TEXT NULL,

  PRIMARY KEY (id),
  CONSTRAINT fk_personnage_membre
    FOREIGN KEY (id) REFERENCES membre (id)
    ON UPDATE CASCADE
    ON DELETE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;
