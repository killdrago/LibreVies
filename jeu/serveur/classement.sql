-- Un classement par compte : le meme id que membre.id.
-- Aucune remise a zero des scores d'un joueur deja present.
USE librevies;

CREATE TABLE IF NOT EXISTS classement (
  id INT UNSIGNED NOT NULL,
  experience BIGINT UNSIGNED NOT NULL DEFAULT 0,
  chasse BIGINT UNSIGNED NOT NULL DEFAULT 0,
  territoire BIGINT UNSIGNED NOT NULL DEFAULT 0,
  PRIMARY KEY (id),
  CONSTRAINT fk_classement_membre
    FOREIGN KEY (id) REFERENCES membre (id)
    ON UPDATE CASCADE
    ON DELETE CASCADE
) ENGINE=InnoDB
  DEFAULT CHARSET=utf8mb4
  COLLATE=utf8mb4_unicode_ci;

-- Completer les comptes crees avant l'ajout de classement, sans modifier
-- les lignes existantes ni leurs points d'experience/chasse/territoire.
INSERT IGNORE INTO classement (id, experience, chasse, territoire)
SELECT m.id, 0, 0, 0 FROM membre m
LEFT JOIN classement c ON c.id = m.id
WHERE c.id IS NULL;
