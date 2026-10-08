-- Personnage LibreVies : un personnage par membre.
-- Les reglages de sliders, la tenue et les objets sont stockes en JSON texte
-- pour rester compatibles avec MySQL 5.6. Les ressources 3D restent dans le jeu.

USE librevies;

CREATE TABLE IF NOT EXISTS personnage (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  membre_id INT UNSIGNED NOT NULL,
  sexe ENUM('homme', 'femme') NOT NULL,
  sliders TEXT NOT NULL,
  tenue TEXT NOT NULL,
  objets TEXT NOT NULL,
  cree_le DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  modifie_le DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (id),
  UNIQUE KEY uq_personnage_membre (membre_id),
  CONSTRAINT fk_personnage_membre
    FOREIGN KEY (membre_id) REFERENCES membre (id)
    ON UPDATE CASCADE
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
