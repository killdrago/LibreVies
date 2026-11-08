-- OPTIONNEL, a executer une seule fois APRES mondeLV.sql, seulement si vous voulez
-- reprendre les elements deja poses dans la base librevies (monde_element, monde_maison).
-- Ne pas le relancer : les lignes seraient dupliquees.

USE mondeLV;

INSERT INTO monde_objet (id, genre, type, position_x, position_z, rotation_y, taille, revision)
SELECT id, 'element', type, position_x, position_z, rotation_y, taille, 1
FROM librevies.monde_element;

INSERT INTO monde_objet (genre, cle, type, position_x, position_z, rotation_y, donnees, revision)
SELECT 'maison', id, 'MAISON', position_x, position_z, rotation_y, donnees, 1
FROM librevies.monde_maison;

UPDATE monde_compteur SET revision = 1 WHERE id = 1;
