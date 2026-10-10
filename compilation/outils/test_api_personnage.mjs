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
const moteur = await php.run({ code: '<?php echo PHP_VERSION;' });
assert.equal(moteur.exitCode, 0, moteur.errors);
console.log('Moteur de recette : PHP ' + moteur.text);
const sliders = ['tete', 'yeux', 'nez', 'bouche', 'oreilles', 'seins', 'volume',
    'hanche', 'ventre', 'largeur_bras', 'longueur_bras', 'hauteur_jambe', 'pieds'];
const source = name => readFileSync(join(root, 'jeu/serveur', name), 'utf8');
php.mkdir('/tests');
php.mkdir('/tests/serveur');
php.mkdir('/tests/sessions');
php.writeFile('/tests/serveur/config.php', source('config.php.example'));
php.writeFile('/tests/serveur/securite.php', source('securite.php'));
php.writeFile('/tests/serveur/npc.php', source('npc.php'));
const api = source('api.php');
assert(api.includes('new PDO($dsn,'), 'Le point de remplacement du DSN a change.');
const apiSQLite = api.replace('new PDO($dsn,', "new PDO('sqlite:/tests/test.sqlite',")
    .replaceAll(' FOR UPDATE', ''); // SQLite ne valide pas les verrous InnoDB natifs.
php.writeFile('/tests/serveur/api.php', apiSQLite);
php.writeFile('/tests/serveur/personnage.php', source('personnage.php')
    .replaceAll('INSERT IGNORE INTO', 'INSERT OR IGNORE INTO'));
const initialization = `<?php
$pdo = new PDO('sqlite:/tests/test.sqlite');
$pdo->exec("CREATE TABLE membre (id INTEGER PRIMARY KEY AUTOINCREMENT,
 pseudo TEXT COLLATE NOCASE NOT NULL UNIQUE, motdepasse TEXT NOT NULL,
 email TEXT COLLATE NOCASE NOT NULL UNIQUE, valider INTEGER NOT NULL DEFAULT 0,
 droit INTEGER NOT NULL DEFAULT 0, bani TEXT NOT NULL DEFAULT 'non' CHECK(bani IN ('non', 'oui')))");
$pdo->exec('CREATE TABLE personnage (id INTEGER PRIMARY KEY REFERENCES membre(id),
 \`default\` INTEGER NOT NULL DEFAULT 1, sexe TEXT NULL,
 ${sliders.map(name => name + ' DECIMAL(10,4) NULL').join(', ')},
 teinte_peau TEXT NULL, coiffure TEXT NULL, chaussures TEXT NULL,
 chapeau TEXT NULL, tenue TEXT NULL, objets TEXT NULL)');
$pdo->exec('CREATE TABLE npc (id TEXT PRIMARY KEY, sexe TEXT NULL,
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
    const bootstrap = `<?php error_reporting(E_ALL); ini_set('display_errors', '1'); ini_set('session.save_path', '/tests/sessions'); require '/tests/serveur/api.php';`;
    php.writeFile('/tests/request.php', bootstrap);
    const response = await php.run({
        scriptPath: '/tests/request.php', method: 'POST', relativeUri: '/serveur/api.php',
        headers: { 'Content-Type': json ? 'application/json' : 'application/x-www-form-urlencoded' },
        body: new TextEncoder().encode(json ? JSON.stringify(params) : new URLSearchParams(params).toString())
    });
    assert.equal(response.httpStatusCode, status, response.text + '\n' + response.errors);
    assert.equal(response.exitCode, 0, response.errors);
    assert(!/(Warning|Deprecated|Fatal error|Notice):/.test(response.text + response.errors), response.text + response.errors);
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
const a = await request('register', { email: 'alice@example.test', pseudo: 'Alice Test', password, droit: 255, bani: 'oui' });
const b = await request('register', { email: 'bob@example.test', pseudo: 'Bob Test', password });
const idA = a.membre.id, idB = b.membre.id;
assert.equal(await sql(`echo json_encode($pdo->query('SELECT bani FROM membre WHERE id=${idA}')->fetchColumn());`), 'non');
const initial = await row(idA);
assert.equal(initial.default, 1);
assert(Object.entries(initial).every(([key, value]) => key === 'id' || key === 'default' || value === null));
assert.equal(Number(await sql(`echo json_encode($pdo->query('SELECT valider FROM membre WHERE id=${idA}')->fetchColumn());`)), 0);
assert.equal(Number(await sql(`echo json_encode($pdo->query('SELECT droit FROM membre WHERE id=${idA}')->fetchColumn());`)), 0);
assert.equal(Number(await sql(`echo json_encode($pdo->query('SELECT droit FROM membre WHERE id=${idB}')->fetchColumn());`)), 0);
console.log('OK droit=0 impose par le serveur meme si le client envoie 255');
assert.deepEqual(await classement(idA), { id: idA, experience: 0, chasse: 0, territoire: 0 });
assert.deepEqual(await classement(idB), { id: idB, experience: 0, chasse: 0, territoire: 0 });
await request('register', { email: 'alice@example.test', pseudo: 'Alice Test', password }, 409);
console.log('OK inscription atomique membre/personnage/classement : meme id, default=1, reglages NULL, valider=0, trois scores a 0');

await request('login', { pseudo: 'Alice Test', password: 'incorrect' }, 401);
const login = await request('login', { pseudo: 'alice test', password });
assert.equal(login.membre.pseudo, 'Alice Test');
assert.equal(login.membre.valider, 0);
assert.equal(login.membre.bani, 'non');
assert.deepEqual(login.personnage, { id: idA, default: 1 });
assert(/^[0-9a-f]{64}$/.test(login.session.token) && login.session.expires_at > Date.now() / 1000);
assert(!JSON.stringify(login).includes(password));
const token = login.session.token;
// Les profils NPC ne se lisent qu'avec une vraie session ; aucun id client
// ne peut choisir ou modifier les lignes serveur.
await request('get_npcs', {}, 401);
await request('get_npcs', { session_token: login.session.token }, 409);
const npcColumns = ['sexe', ...sliders, 'teinte_peau', 'coiffure', 'chaussures', 'chapeau', 'tenue'];
const seedMySQL = readFileSync(join(root, 'compilation/outils/fixtures/npc_profiles_fixture.sql'), 'utf8');
const seedSqlite = seedMySQL.slice(seedMySQL.indexOf('INSERT INTO npc'), seedMySQL.indexOf('ON DUPLICATE KEY UPDATE'))
    + 'ON CONFLICT(id) DO UPDATE SET ' + npcColumns.map(name => name + '=excluded.' + name).join(', ') + ';';
const seedBootstrap = `$pdo->sqliteCreateFunction('RAND', function () { return mt_rand(0, 9999999) / 10000000; });
$pdo->sqliteCreateFunction('FLOOR', function ($x) { return (int)floor($x); });
$pdo->sqliteCreateFunction('ELT', function () { $a = func_get_args(); $i = (int)$a[0]; return isset($a[$i]) ? $a[$i] : null; });`;
await sql(seedBootstrap + '$pdo->exec(' + JSON.stringify(seedSqlite) + ');');
let npcs = await request('get_npcs', { session_token: login.session.token, id: 'autre', droit: 255 });
assert.deepEqual(npcs.npcs.map(npc => npc.id), ['maire', 'forgeron', 'marchand', 'esthetique', 'garde_nord', 'garde_sud']);
assert(npcs.npcs.every(npc => !('default' in npc) && npc.objets === null));
assert.deepEqual((await request('get_npcs', { session_token: login.session.token })).npcs, npcs.npcs);
await sql("$pdo->exec(\"UPDATE npc SET objets='accessoire-conserve' WHERE id='maire'\");");
await sql(seedBootstrap + '$pdo->exec(' + JSON.stringify(seedSqlite) + ');');
npcs = await request('get_npcs', { session_token: login.session.token });
assert.equal(npcs.npcs.find(npc => npc.id === 'maire').objets, 'accessoire-conserve');
await sql("$pdo->exec('UPDATE npc SET nez=NULL');");
await request('get_npcs', { session_token: login.session.token }, 409);
await sql(seedBootstrap + '$pdo->exec(' + JSON.stringify(seedSqlite) + ');');
console.log('OK 6 NPC aleatoires complets, profils stables, objets preserves ; absence/incomplet/session factice refuses');
const npcAvantAdmin = (await request('get_npcs', { session_token: login.session.token })).npcs;
const maireAvant = npcAvantAdmin.find(npc => npc.id === 'maire');
const draftNpc = { ...maireAvant, tete: 1.234567, objets: 'injection', droit: 1 };
assert.equal(login.membre.droit, 0);
await request('save_npc', { session_token: login.session.token, npc_id: 'maire', personnage: JSON.stringify(draftNpc), droit: 1 }, 403);
assert.deepEqual((await request('get_npcs', { session_token: login.session.token })).npcs, npcAvantAdmin);
await sql(`$pdo->exec('UPDATE membre SET droit=1 WHERE id=${idA}');`);
const admin = await request('login', { pseudo: 'Alice Test', password });
assert.equal(admin.membre.droit, 1);
const npcSaved = await request('save_npc', { session_token: admin.session.token, npc_id: 'maire', personnage: JSON.stringify(draftNpc) });
assert.equal(npcSaved.npcs.find(npc => npc.id === 'maire').tete, 1.2346);
assert.equal(npcSaved.npcs.find(npc => npc.id === 'maire').objets, maireAvant.objets);
assert.deepEqual(npcSaved.npcs.find(npc => npc.id === 'marchand'), npcAvantAdmin.find(npc => npc.id === 'marchand'));
await request('save_npc', { session_token: admin.session.token, npc_id: 'inconnu', personnage: JSON.stringify(draftNpc) }, 400);
await request('save_npc', { session_token: admin.session.token, npc_id: 'maire', personnage: JSON.stringify({ ...draftNpc, volume: -1 }) }, 400);
await sql(`$pdo->exec("CREATE TRIGGER refuser_npc BEFORE UPDATE ON npc BEGIN SELECT RAISE(ABORT, 'indisponible'); END");`);
await request('save_npc', { session_token: admin.session.token, npc_id: 'maire', personnage: JSON.stringify({ ...draftNpc, tete: 3 }) }, 500);
assert.deepEqual((await request('get_npcs', { session_token: admin.session.token })).npcs, npcSaved.npcs);
await sql("$pdo->exec('DROP TRIGGER refuser_npc');");
await sql(`$pdo->exec('UPDATE membre SET droit=2 WHERE id=${idA}');`);
await request('save_npc', { session_token: admin.session.token, npc_id: 'maire', personnage: JSON.stringify(draftNpc), droit: 1 }, 403);
await sql(`$pdo->exec('UPDATE membre SET droit=0 WHERE id=${idA}');`);
await request('save_npc', { session_token: admin.session.token, npc_id: 'maire', personnage: JSON.stringify(draftNpc), droit: 1 }, 403);
console.log('OK ADMIN droit=1 seulement : sauvegarde NPC, precision/objets, refus invalide, rollback et revocation live');


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


// Recherche/fiches/bannissement : seuls les administrateurs NON banis droit=1.
const adminCompte = await request('register', { email: 'admin-bani@example.test', pseudo: 'Admin Bani Test', password });
const idAdmin = adminCompte.membre.id;
await sql(`$pdo->exec('UPDATE membre SET droit=1 WHERE id=${idAdmin}');`);
const adminBani = await request('login', { pseudo: 'Admin Bani Test', password });
const sessionAdmin = adminBani.session.token;
for (const action of ['search_players', 'get_player', 'save_player_ban']) {
    await request(action, { search: 'Test', player_id: String(idB), bani: 'oui', droit: 1, id: idAdmin }, 401);
    await request(action, { session_token: bobLogin.session.token, search: 'Test', player_id: String(idA), bani: 'oui', droit: 1 }, 403);
}
let trouves = await request('search_players', { session_token: sessionAdmin, search: 'bOb' });
assert.equal(trouves.membre.id, idAdmin);
assert.deepEqual(trouves.players.map(m => m.id), [idB]);
assert.equal(trouves.players[0].bani, 'non');
for (const search of ['', '%', '_', "' OR 1=1 --"]) {
    assert.deepEqual((await request('search_players', { session_token: sessionAdmin, search })).players, []);
}
const fiche = await request('get_player', { session_token: sessionAdmin, player_id: String(idA) });
assert.equal(fiche.membre.id, idAdmin);
assert.equal(fiche.player.membre.id, idA);
assert.equal(fiche.player.membre.email, 'alice@example.test');
assert.equal(fiche.player.personnage.id, idA);
assert.deepEqual(fiche.player.classement, { experience: '125', chasse: '7', territoire: '3' });
assert(!JSON.stringify(fiche).includes('motdepasse') && !JSON.stringify(fiche).includes(password));
for (const player_id of ['0', '-1', '1 OR 1=1', '4294967296']) {
    await request('get_player', { session_token: sessionAdmin, player_id }, 400);
}
await request('get_player', { session_token: sessionAdmin, player_id: '424242' }, 404);
const cibleAvant = await sql(`echo json_encode($pdo->query('SELECT * FROM membre WHERE id=${idB}')->fetch());`);
const profilAvantBan = await row(idB), pointsAvantBan = await classement(idB);
let bani = await request('save_player_ban', { session_token: sessionAdmin, player_id: String(idB), bani: 'oui', droit: 1, motdepasse: 'attaque' });
assert.equal(bani.player.membre.bani, 'oui');
const cibleApres = await sql(`echo json_encode($pdo->query('SELECT * FROM membre WHERE id=${idB}')->fetch());`);
assert.deepEqual(cibleApres, { ...cibleAvant, bani: 'oui' });
assert.deepEqual(await row(idB), profilAvantBan);
assert.deepEqual(await classement(idB), pointsAvantBan);
const bloque = await request('login', { pseudo: 'Bob Test', password }, 403);
assert.equal(bloque.code, 'membre_bani');
assert.equal(bloque.message, "Joueur bani veuillez contacter l'administrateur");
assert(!('session' in bloque));
await request('login', { pseudo: 'Bob Test', password: 'incorrect' }, 401);
for (const action of ['get_character', 'save_character', 'get_npcs', 'save_npc', 'search_players']) {
    const refus = await request(action, { session_token: bobLogin.session.token, personnage: JSON.stringify(draft), search: 'Test', bani: 'non' }, 403);
    assert.equal(refus.code, 'membre_bani');
}
for (const invalide of ['yes', '', '1']) {
    await request('save_player_ban', { session_token: sessionAdmin, player_id: String(idB), bani: invalide }, 400);
}
await request('save_player_ban', { session_token: sessionAdmin, player_id: idB, bani: true }, 400, true);
await sql(`$pdo->exec("CREATE TRIGGER refuser_bani BEFORE UPDATE OF bani ON membre BEGIN SELECT RAISE(ABORT, 'indisponible'); END");`);
await request('save_player_ban', { session_token: sessionAdmin, player_id: String(idB), bani: 'non' }, 500);
assert.equal(await sql(`echo json_encode($pdo->query('SELECT bani FROM membre WHERE id=${idB}')->fetchColumn());`), 'oui');
await sql("$pdo->exec('DROP TRIGGER refuser_bani');");
bani = await request('save_player_ban', { session_token: sessionAdmin, player_id: idB, bani: 'non' }, 200, true);
assert.equal(bani.player.membre.bani, 'non');
assert.equal((await request('login', { pseudo: 'Bob Test', password })).membre.bani, 'non');
for (const droit of [2, 0]) {
    await sql(`$pdo->exec('UPDATE membre SET droit=${droit} WHERE id=${idAdmin}');`);
    await request('save_player_ban', { session_token: sessionAdmin, player_id: String(idB), bani: 'oui', droit: 1 }, 403);
}
await sql(`$pdo->exec('UPDATE membre SET droit=1, bani="oui" WHERE id=${idAdmin}');`);
await request('search_players', { session_token: sessionAdmin, search: 'Test' }, 403);
await sql(`$pdo->exec('UPDATE membre SET bani="non" WHERE id=${idAdmin}');`);
await sql(`for ($i=0; $i<35; $i++) $pdo->exec("INSERT INTO membre(pseudo,motdepasse,email) VALUES ('Limite ". $i ."', 'hash-inutilise', 'limite".$i."@example.test')");`);
assert.equal((await request('search_players', { session_token: sessionAdmin, search: 'Limite' })).players.length, 30);
console.log('OK admin joueurs : recherche litterale bornee, fiche compte/profil/classement sans hash ; bans/unbans persistants, seul bani modifie');
console.log('OK comptes banis : login et anciennes sessions refuses, message exact, pas de jeton ; droits falsifies/revoques et panne SQL refuses');



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
// Ancien schema : ne jamais supposer bani=non si la colonne est absente.
await sql("$pdo->exec('ALTER TABLE membre RENAME COLUMN bani TO bani_absente');");
const schemaAbsent = await request('health', {}, 500);
assert(schemaAbsent.message.includes('bani'));
await sql("$pdo->exec('ALTER TABLE membre RENAME COLUMN bani_absente TO bani');");
const getInterdit = await php.run({ scriptPath: '/tests/request.php', method: 'GET',
    relativeUri: '/serveur/api.php?action=get_player&player_id=1' });
assert.equal(getInterdit.httpStatusCode, 405);
console.log('OK colonne bani absente : refus explicite sans ALTER automatique ; API comptes/admin POST seulement');
const logs = php.readFileAsText('/journal_api.log');
for (const secret of [sessionAdmin, password, token, loginSansOpenSSL.session.token, reconnect.session.token, bobLogin.session.token])
    assert(!logs.includes(secret), 'Secret present dans le journal de l API.');
console.log('OK ancien compte initialise sans ecraser un profil ; session expiree refusee ; aucun secret dans les logs');
php.exit();
console.log(`OK : recette API terminee (PHP ${process.env.PHP || '8.3'}, SQLite de test uniquement)`);
