-- Classement et statistiques du joueur LibreVies.
-- Chaque ligne correspond a un membre de la table membre.
-- Executer ce fichier une seule fois sur la base librevies.

USE librevies;

CREATE TABLE IF NOT EXISTS classement (
  id INT UNSIGNED NOT NULL,
  experience BIGINT UNSIGNED NOT NULL DEFAULT 0,
  chasse INT UNSIGNED NOT NULL DEFAULT 0,
  territoire INT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  CONSTRAINT fk_classement_membre
    FOREIGN KEY (id) REFERENCES membre (id)
    ON UPDATE CASCADE
    ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
