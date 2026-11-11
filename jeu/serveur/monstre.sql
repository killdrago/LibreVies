-- Reglage des monstres : une ligne par lieu (nombre de monstres a faire apparaitre).
-- Base librevies (meme base que membre et npc). Lecture : tout joueur connecte.
-- Ecriture : administrateurs (droit = 1) seulement, via l'API (action save_monstres).
USE librevies;

CREATE TABLE IF NOT EXISTS monstre (
  lieu VARCHAR(60) NOT NULL,
  nombre INT UNSIGNED NOT NULL DEFAULT 18,
  PRIMARY KEY (lieu)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO monstre (lieu, nombre) VALUES ('village depart', 18);
