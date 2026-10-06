USE librevies;

-- A utiliser si la table membre a été créée avec INT pour pseudo,
-- motdepasse ou email, comme dans la première capture.
-- Ce script convient à une table encore vide.
ALTER TABLE membre
  MODIFY id INT UNSIGNED NOT NULL AUTO_INCREMENT,
  MODIFY pseudo VARCHAR(50) NOT NULL,
  MODIFY motdepasse VARCHAR(255) NOT NULL,
  MODIFY email VARCHAR(254) NOT NULL;

ALTER TABLE membre
  ADD PRIMARY KEY (id),
  ADD UNIQUE KEY uq_membre_pseudo (pseudo),
  ADD UNIQUE KEY uq_membre_email (email);
