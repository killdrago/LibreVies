-- Mise a jour d'une ancienne table personnage, sans la supprimer.
-- A importer une seule fois si elle precede le schema nullable actuel.
-- Aucune modification des comptes ou des profils personnalises (default=0).
-- Sauvegarder la base avant une migration de schema.

USE librevies;

ALTER TABLE personnage
  MODIFY `default` TINYINT(1) NOT NULL DEFAULT 1,
  MODIFY sexe ENUM('homme', 'femme') NULL DEFAULT NULL,
  MODIFY tete DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY yeux DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY nez DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY bouche DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY oreilles DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY seins DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY volume DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY hanche DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY ventre DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY largeur_bras DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY longueur_bras DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY hauteur_jambe DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY pieds DECIMAL(10,4) NULL DEFAULT NULL,
  MODIFY teinte_peau VARCHAR(32) NULL DEFAULT NULL,
  MODIFY coiffure VARCHAR(100) NULL DEFAULT NULL,
  MODIFY chaussures VARCHAR(100) NULL DEFAULT NULL,
  MODIFY chapeau VARCHAR(100) NULL DEFAULT NULL,
  MODIFY tenue VARCHAR(100) NULL DEFAULT NULL,
  MODIFY objets TEXT NULL;

-- Les anciens reglages de la base primitive ne sont pas des choix du joueur.
-- Ne jamais remettre a NULL les reglages d'un profil personnalise.
UPDATE personnage SET
  sexe = NULL, tete = NULL, yeux = NULL, nez = NULL,
  bouche = NULL, oreilles = NULL, seins = NULL, volume = NULL,
  hanche = NULL, ventre = NULL, largeur_bras = NULL, longueur_bras = NULL,
  hauteur_jambe = NULL, pieds = NULL, teinte_peau = NULL, coiffure = NULL,
  chaussures = NULL, chapeau = NULL, tenue = NULL, objets = NULL
WHERE `default` = 1;

-- Initialiser uniquement les anciens comptes sans personnage.
INSERT IGNORE INTO personnage (id, `default`)
SELECT m.id, 1 FROM membre m
LEFT JOIN personnage p ON p.id = m.id
WHERE p.id IS NULL;
