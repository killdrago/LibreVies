USE librevies;

-- A executer une seule fois sur une base membre deja existante.
-- 0 = non valide, 1 = valide.
ALTER TABLE membre
  ADD COLUMN valider TINYINT(1) NOT NULL DEFAULT 0 AFTER email;
