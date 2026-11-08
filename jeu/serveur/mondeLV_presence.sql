-- Presence des joueurs : position envoyee par le jeu toutes les secondes.
-- A executer une fois dans phpMyAdmin (onglet SQL), apres mondeLV.sql.
USE mondeLV;

CREATE TABLE IF NOT EXISTS monde_presence (
  membre_id INT UNSIGNED NOT NULL,
  pseudo VARCHAR(40) NOT NULL DEFAULT '',
  position_x DECIMAL(10,3) NOT NULL DEFAULT 0,
  position_z DECIMAL(10,3) NOT NULL DEFAULT 0,
  rotation_y DECIMAL(10,3) NOT NULL DEFAULT 0,
  animation VARCHAR(20) NOT NULL DEFAULT 'Idle',
  mis_a_jour DATETIME NOT NULL,
  PRIMARY KEY (membre_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
