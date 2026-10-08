// Recette de l'API sans serveur ni identifiants reels.
// npm install --prefix compilation/build/tests-php @php-wasm/cli@3.1.57
// node compilation/outils/test_api_personnage.mjs
// LIBREVIES_PHP_WASM_ROOT permet de choisir un autre dossier node_modules.
// PHP=7.4 ou PHP=8.3 selectionne le moteur (8.3 par defaut).
// SQLite remplace uniquement le DSN et INSERT IGNORE dans des copies en RAM :
// cette recette ne remplace pas une verification MySQL/Unity en conditions reelles.
import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '../..');
const dependencies = process.env.LIBREVIES_PHP_WASM_ROOT
    || join(root, 'compilation/build/tests-php/node_modules');
const require = createRequire(join(dependencies, '__test__.js'));
const { PHP } = await import(pathToFileURL(require.resolve('@php-wasm/universal')));
const { loadNodeRuntime } = await import(pathToFileURL(require.resolve('@php-wasm/node')));
const php = new PHP(await loadNodeRuntime(process.env.PHP || '8.3', {
    emscriptenOptions: { processId: 1 }
}));
// Unitaires du CSPRNG et du chemin Windows avec I/O simulees.
php.mkdir('/outils');
php.mkdir('/jeu');
php.mkdir('/jeu/serveur');
php.writeFile('/jeu/serveur/securite.php', readFileSync(join(root, 'jeu/serveur/securite.php'), 'utf8'));
php.writeFile('/outils/test_securite.php', readFileSync(join(root, 'compilation/outils/test_securite.php'), 'utf8')
    .replaceAll("__DIR__ . '/../../jeu/serveur/securite.php'", "'/jeu/serveur/securite.php'"));
const securiteResult = await php.run({ scriptPath: '/outils/test_securite.php' });
assert.equal(securiteResult.exitCode, 0, securiteResult.text + securiteResult.errors);
assert(!securiteResult.text.includes('Fatal error'), securiteResult.text);
console.log(securiteResult.text.trim());
const sliders = ['tete', 'yeux', 'nez', 'bouche', 'oreilles', 'seins', 'volume',
    'hanche', 'ventre', 'largeur_bras', 'longueur_bras', 'hauteur_jambe', 'pieds'];
const source = name => readFileSync(join(root, 'jeu/serveur', name), 'utf8');
php.mkdir('/tests');
php.mkdir('/tests/serveur');
php.mkdir('/tests/sessions');
php.writeFile('/tests/serveur/config.php', source('config.php.example'));
php.writeFile('/tests/serveur/securite.php', source('securite.php'));
const api = source('api.php');
assert(api.includes('new PDO($dsn,'), 'Le point de remplacement du DSN a change.');
const apiSQLite = api.replace('new PDO($dsn,', "new PDO('sqlite:/tests/test.sqlite',");
php.writeFile('/tests/serveur/api.php', apiSQLite);
php.writeFile('/tests/serveur/personnage.php', source('personnage.php')
    .replaceAll('INSERT IGNORE INTO', 'INSERT OR IGNORE INTO'));
const initialization = `<?php
$pdo = new PDO('sqlite:/tests/test.sqlite');
$pdo->exec('CREATE TABLE membre (id INTEGER PRIMARY KEY AUTOINCREMENT,
 pseudo TEXT COLLATE NOCASE NOT NULL UNIQUE, motdepasse TEXT NOT NULL,
 email TEXT COLLATE NOCASE NOT NULL UNIQUE, valider INTEGER NOT NULL DEFAULT 0)');
$pdo->exec('CREATE TABLE personnage (id INTEGER PRIMARY KEY REFERENCES membre(id),
 \`default\` INTEGER NOT NULL DEFAULT 1, sexe TEXT NULL,
 ${sliders.map(name => name + ' DECIMAL(10,4) NULL').join(', ')},
 teinte_peau TEXT NULL, coiffure TEXT NULL, chaussures TEXT NULL,
 chapeau TEXT NULL, tenue TEXT NULL, objets TEXT NULL)');
$pdo->exec('CREATE TABLE classement (id INTEGER PRIMARY KEY REFERENCES membre(id),
 experience INTEGER NOT NULL DEFAULT 0, chasse INTEGER NOT NULL DEFAULT 0,
 territoire INTEGER NOT NULL DEFAULT 0)');
echo 'OK';`;
assert.equal((await php.run({ code: initialization })).text, 'OK');

const request = async (action, data = {}, status = 200, json = false) => {
    const params = { action, ...data };
    const bootstrap = `<?php ini_set('session.save_path', '/tests/sessions'); require '/tests/serveur/api.php';`;
    php.writeFile('/tests/request.php', bootstrap);
    const response = await php.run({
        scriptPath: '/tests/request.php', method: 'POST', relativeUri: '/serveur/api.php',
        headers: { 'Content-Type': json ? 'application/json' : 'application/x-www-form-urlencoded' },
        body: new TextEncoder().encode(json ? JSON.stringify(params) : new URLSearchParams(params).toString())
    });
    assert.equal(response.httpStatusCode, status, response.text + '\n' + response.errors);
    assert.equal(response.exitCode, 0, response.errors);
    const body = response.json;
    assert.equal(body.ok, status === 200, response.text);
    return body;
};
const sql = async code => {
    const response = await php.run({ code: `<?php $pdo = new PDO('sqlite:/tests/test.sqlite');
      $pdo->setAttribute(PDO::ATTR_DEFAULT_FETCH_MODE, PDO::FETCH_ASSOC); ${code}` });
    assert.equal(response.exitCode, 0, response.errors);
    return response.text ? JSON.parse(response.text) : null;
};
const row = async id => {
    const result = await sql(`echo json_encode($pdo->query('SELECT * FROM personnage WHERE id=${id}')->fetch());`);
    result.id = Number(result.id); result.default = Number(result.default);
    return result;
};
const classement = async id => {
    const result = await sql(`echo json_encode($pdo->query('SELECT * FROM classement WHERE id=${id}')->fetch());`);
    for (const key of Object.keys(result)) result[key] = Number(result[key]);
    return result;
};
const password = 'motdepasse-de-recette-184!';
const a = await request('register', { email: 'alice@example.test', pseudo: 'Alice Test', password });
const b = await request('register', { email: 'bob@example.test', pseudo: 'Bob Test', password });
const idA = a.membre.id, idB = b.membre.id;
const initial = await row(idA);
assert.equal(initial.default, 1);
assert(Object.entries(initial).every(([key, value]) => key === 'id' || key === 'default' || value === null));
assert.equal(Number(await sql(`echo json_encode($pdo->query('SELECT valider FROM membre WHERE id=${idA}')->fetchColumn());`)), 0);
assert.deepEqual(await classement(idA), { id: idA, experience: 0, chasse: 0, territoire: 0 });
assert.deepEqual(await classement(idB), { id: idB, experience: 0, chasse: 0, territoire: 0 });
await request('register', { email: 'alice@example.test', pseudo: 'Alice Test', password }, 409);
console.log('OK inscription atomique membre/personnage/classement : meme id, default=1, reglages NULL, valider=0, trois scores a 0');

await request('login', { pseudo: 'Alice Test', password: 'incorrect' }, 401);
const login = await request('login', { pseudo: 'alice test', password });
assert.equal(login.membre.pseudo, 'Alice Test');
assert.equal(login.membre.valider, 0);
assert.deepEqual(login.personnage, { id: idA, default: 1 });
assert(/^[0-9a-f]{64}$/.test(login.session.token) && login.session.expires_at > Date.now() / 1000);
assert(!JSON.stringify(login).includes(password));
const token = login.session.token;
// Simuler PHP 5.6 sans random_bytes/OpenSSL, sans modifier PHP.ini du poste.
// Le generateur de l'OS doit prendre le relais et produire un nouveau jeton.
php.writeFile('/tests/serveur/securite.php', source('securite.php')
    .replace("function_exists('random_bytes')", 'false')
    .replace("function_exists('openssl_random_pseudo_bytes')", 'false'));
const loginSansOpenSSL = await request('login', { pseudo: 'Alice Test', password });
assert(/^[0-9a-f]{64}$/.test(loginSansOpenSSL.session.token));
assert.notEqual(loginSansOpenSSL.session.token, login.session.token);
assert.equal((await request('get_character', { session_token: loginSansOpenSSL.session.token })).membre.id, idA);
php.writeFile('/tests/serveur/securite.php', source('securite.php'));
console.log('OK connexion sans random_bytes ni OpenSSL : CSPRNG du systeme, jeton utilisable');

await request('get_character', { id: idA, pseudo: 'Alice Test' }, 401);
await request('save_character', { id: idA, pseudo: 'Alice Test' }, 401);
await request('get_character', { session_token: 'x'.repeat(40) }, 401);
console.log('OK connexion autorisee avec valider=0 ; mot de passe et sessions factices refuses');

const draft = { sexe: 'femme', teinte_peau: '6', coiffure: 'faydaen_hair_1',
    tenue: 'female_elegantsuit01', chapeau: 'fedora01', chaussures: 'shoes06' };
sliders.forEach((name, i) => { draft[name] = (i + 1) * 0.314159; });
let saved = await request('save_character', { session_token: token, id: idB, pseudo: 'Bob Test',
    personnage: JSON.stringify({ ...draft, id: idB, default: 1, objets: 'injection' }) });
assert.equal(saved.personnage.id, idA);
assert.equal(saved.personnage.default, 0);
sliders.forEach(name => assert.equal(saved.personnage[name], Math.round(draft[name] * 10000) / 10000));
for (const name of ['sexe', 'teinte_peau', 'coiffure', 'tenue', 'chapeau', 'chaussures'])
    assert.equal(saved.personnage[name], draft[name]);
assert.deepEqual(await row(idB), { ...initial, id: idB });
assert.deepEqual((await request('get_character', { session_token: token })).personnage, saved.personnage);
const reconnect = await request('login', { pseudo: 'Alice Test', password });
assert.notEqual(reconnect.session.token, token, 'Un nouveau jeton doit etre cree a chaque connexion.');
assert.deepEqual(reconnect.personnage, saved.personnage);
console.log('OK 13 sliders et choix sauvegardes/recharges ; id/pseudo usurpes sans effet sur Bob');

const beforeInvalid = await row(idA);
for (const [name, value] of [['tete', -1], ['yeux', 5.0001], ['nez', '1e309'], ['bouche', true],
    ['oreilles', null], ['sexe', 'autre'], ['teinte_peau', 6], ['coiffure', '../model'],
    ['tenue', 'male_worksuit01'], ['chapeau', 'inconnu'], ['chaussures', 'inconnu']]) {
    await request('save_character', { session_token: token,
        personnage: JSON.stringify({ ...draft, [name]: value }) }, 400);
    assert.deepEqual(await row(idA), beforeInvalid);
}
const missing = { ...draft }; delete missing.volume;
await request('save_character', { session_token: token, personnage: JSON.stringify(missing) }, 400);
assert.deepEqual(await row(idA), beforeInvalid);
console.log('OK reglages manquants, non numeriques, non finis, hors limites et ressources inconnues refuses sans ecriture');

await sql(`$pdo->exec("UPDATE personnage SET objets='objet-existant' WHERE id=${idA}");`);
saved = await request('save_character', { session_token: token,
    personnage: JSON.stringify({ ...draft, chapeau: '', chaussures: '', objets: null }) });
assert.equal(saved.personnage.chapeau, null);
assert.equal(saved.personnage.chaussures, null);
assert.equal(saved.personnage.objets, 'objet-existant');
const nullable = await row(idA);
assert.equal(nullable.chapeau, null); assert.equal(nullable.chaussures, null);
// Egalement verifier le transport JSON direct et un profil masculin.
const male = { ...draft, sexe: 'homme', tenue: 'male_worksuit01', chapeau: null, chaussures: null };
await request('save_character', { session_token: token, personnage: male }, 200, true);
console.log('OK Aucun reste NULL en SQL ; objets preserve ; profils feminin et masculin et deux transports');

const bobLogin = await request('login', { pseudo: 'Bob Test', password });
await sql(`$pdo->exec("CREATE TRIGGER refuser_sauvegarde BEFORE UPDATE ON personnage
  BEGIN SELECT RAISE(ABORT, 'indisponible'); END");`);
await request('save_character', { session_token: bobLogin.session.token,
    personnage: JSON.stringify(draft) }, 500);
assert.deepEqual(await row(idB), { ...initial, id: idB });
await sql("$pdo->exec('DROP TRIGGER refuser_sauvegarde');");
await sql(`$pdo->exec("CREATE TRIGGER refuser_inscription BEFORE INSERT ON personnage
  BEGIN SELECT RAISE(ABORT, 'indisponible'); END");`);
await request('register', { email: 'rollback@example.test', pseudo: 'Rollback Test', password }, 500);
assert.equal(Number(await sql("echo json_encode($pdo->query(\"SELECT count(*) FROM membre WHERE pseudo='Rollback Test'\")->fetchColumn());")), 0);
await sql("$pdo->exec('DROP TRIGGER refuser_inscription');");
console.log('OK panne SQL : ni default=0 premature ni inscription orpheline');

// Une erreur dans la troisieme insertion annule les deux premieres.
const nombreAvantPanne = await sql("echo json_encode(array('membre' => $pdo->query('SELECT count(*) FROM membre')->fetchColumn(), 'personnage' => $pdo->query('SELECT count(*) FROM personnage')->fetchColumn(), 'classement' => $pdo->query('SELECT count(*) FROM classement')->fetchColumn()));");
await sql(`$pdo->exec("CREATE TRIGGER refuser_classement BEFORE INSERT ON classement
  BEGIN SELECT RAISE(ABORT, 'indisponible'); END");`);
await request('register', { email: 'classement-panne@example.test', pseudo: 'Classement Panne', password }, 500);
assert.deepEqual(await sql("echo json_encode(array('membre' => $pdo->query('SELECT count(*) FROM membre')->fetchColumn(), 'personnage' => $pdo->query('SELECT count(*) FROM personnage')->fetchColumn(), 'classement' => $pdo->query('SELECT count(*) FROM classement')->fetchColumn()));"), nombreAvantPanne);
await sql("$pdo->exec('DROP TRIGGER refuser_classement');");
console.log('OK panne classement : membre et personnage annules aussi, aucun compte incomplet');

// La migration SQL ajoute seulement les lignes manquantes aux anciens comptes.
await sql(`$pdo->exec('UPDATE classement SET experience=125, chasse=7, territoire=3 WHERE id=${idA}');
  $pdo->exec('DELETE FROM classement WHERE id=${idB}');`);
const backfill = source('classement.sql').slice(source('classement.sql').indexOf('INSERT IGNORE INTO'))
    .replace('INSERT IGNORE INTO', 'INSERT OR IGNORE INTO');
await sql('$pdo->exec(' + JSON.stringify(backfill) + ');');
await sql('$pdo->exec(' + JSON.stringify(backfill) + ');');
assert.deepEqual(await classement(idA), { id: idA, experience: 125, chasse: 7, territoire: 3 });
assert.deepEqual(await classement(idB), { id: idB, experience: 0, chasse: 0, territoire: 0 });
console.log('OK ancien compte : classement manquant complete sans remise a zero des scores existants');


await sql(`$pdo->exec('DELETE FROM personnage WHERE id=${idB}');`);
const legacy = await request('get_character', { session_token: bobLogin.session.token });
assert.deepEqual(legacy.personnage, { id: idB, default: 1 });
assert.deepEqual(await row(idB), { ...initial, id: idB });
const expire = await php.run({ code: `<?php
ini_set('session.save_path', '/tests/sessions'); ini_set('session.use_cookies', '0');
session_name('LIBREVIES'); session_id('${token}'); session_start();
$_SESSION['expire_le'] = time() - 1; session_write_close();` });
assert.equal(expire.exitCode, 0, expire.errors);
await request('get_character', { session_token: token }, 401);
const logs = php.readFileAsText('/journal_api.log');
for (const secret of [password, token, loginSansOpenSSL.session.token, reconnect.session.token, bobLogin.session.token])
    assert(!logs.includes(secret), 'Secret present dans le journal de l API.');
console.log('OK ancien compte initialise sans ecraser un profil ; session expiree refusee ; aucun secret dans les logs');
php.exit();
console.log(`OK : recette API terminee (PHP ${process.env.PHP || '8.3'}, SQLite de test uniquement)`);
