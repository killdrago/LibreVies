-- Executer apres npc.sql. Les profils sont tires UNE fois en BDD,
-- puis restent identiques a chaque connexion.
-- Relancer cette requete rerandomise les 6 avatars (objets deja presents preserves).
USE librevies;

INSERT INTO npc (
  id, sexe, tete, yeux, nez, bouche, oreilles, seins, volume, hanche,
  ventre, largeur_bras, longueur_bras, hauteur_jambe, pieds,
  teinte_peau, coiffure, chaussures, chapeau, tenue, objets
)
SELECT
  n.id, n.sexe,
  ROUND(0.6 + RAND() * 2.8, 4), ROUND(0.6 + RAND() * 2.8, 4),
  ROUND(0.6 + RAND() * 2.8, 4), ROUND(0.6 + RAND() * 2.8, 4),
  ROUND(0.6 + RAND() * 2.8, 4),
  ROUND(CASE WHEN n.sexe = 'femme' THEN 0.6 + RAND() * 2.8 ELSE RAND() * 1.0 END, 4),
  ROUND(0.6 + RAND() * 2.8, 4), ROUND(0.6 + RAND() * 2.8, 4),
  ROUND(0.6 + RAND() * 2.8, 4), ROUND(0.6 + RAND() * 2.8, 4),
  ROUND(0.6 + RAND() * 2.8, 4), ROUND(0.6 + RAND() * 2.8, 4),
  ROUND(0.6 + RAND() * 2.8, 4),
  CAST(FLOOR(RAND() * 7) AS CHAR),
  ELT(1 + FLOOR(RAND() * 5), 'short_messy', 'straight_bangs',
      'shaggy_green', 'strawberry_cloud', 'faydaen_hair_1'),
  ELT(1 + FLOOR(RAND() * 6), 'shoes01', 'shoes02', 'shoes03', 'shoes04', 'shoes05', 'shoes06'),
  CASE WHEN RAND() < 0.30 THEN 'fedora01' ELSE NULL END,
  CASE WHEN n.sexe = 'femme' THEN
    ELT(1 + FLOOR(RAND() * 4), 'female_sportsuit01', 'female_casualsuit01',
        'female_casualsuit02', 'female_elegantsuit01')
  ELSE
    ELT(1 + FLOOR(RAND() * 5), 'male_casualsuit04', 'male_casualsuit05',
        'male_casualsuit06', 'male_elegantsuit01', 'male_worksuit01')
  END,
  NULL
FROM (
  SELECT 'maire' AS id, CASE WHEN RAND() < 0.5 THEN 'homme' ELSE 'femme' END AS sexe
  UNION ALL SELECT 'forgeron', CASE WHEN RAND() < 0.5 THEN 'homme' ELSE 'femme' END
  UNION ALL SELECT 'marchand', CASE WHEN RAND() < 0.5 THEN 'homme' ELSE 'femme' END
  UNION ALL SELECT 'esthetique', CASE WHEN RAND() < 0.5 THEN 'homme' ELSE 'femme' END
  UNION ALL SELECT 'garde_nord', CASE WHEN RAND() < 0.5 THEN 'homme' ELSE 'femme' END
  UNION ALL SELECT 'garde_sud', CASE WHEN RAND() < 0.5 THEN 'homme' ELSE 'femme' END
) AS n
WHERE 1 = 1
ON DUPLICATE KEY UPDATE
  sexe = VALUES(sexe), tete = VALUES(tete), yeux = VALUES(yeux), nez = VALUES(nez),
  bouche = VALUES(bouche), oreilles = VALUES(oreilles), seins = VALUES(seins),
  volume = VALUES(volume), hanche = VALUES(hanche), ventre = VALUES(ventre),
  largeur_bras = VALUES(largeur_bras), longueur_bras = VALUES(longueur_bras),
  hauteur_jambe = VALUES(hauteur_jambe), pieds = VALUES(pieds),
  teinte_peau = VALUES(teinte_peau), coiffure = VALUES(coiffure),
  chaussures = VALUES(chaussures), chapeau = VALUES(chapeau), tenue = VALUES(tenue);
