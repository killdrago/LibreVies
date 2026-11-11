-- Reglage du nombre de monstres par zone (une ligne par lieu).
-- Base librevies (meme base que membre et npc). Lecture : tout joueur connecte.
-- Ecriture : administrateurs (droit = 1) seulement, via l'API (action save_monstres).
USE librevies;

CREATE TABLE IF NOT EXISTS qttmonstrezone (
  id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  nombre INT UNSIGNED NOT NULL DEFAULT 18,
  lieu VARCHAR(60) NOT NULL,
  PRIMARY KEY (id),
  UNIQUE KEY uk_qttmonstrezone_lieu (lieu)
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

INSERT IGNORE INTO qttmonstrezone (lieu, nombre) VALUES ('village depart', 18);
