-- A executer une seule fois si membre.droit n'existe pas encore.
-- Les comptes existants recoivent 0. Aucun droit existant n'est reinitialise.
USE librevies;

ALTER TABLE membre
  ADD COLUMN droit TINYINT UNSIGNED NOT NULL DEFAULT 0;
