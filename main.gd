# ============================================================
# LIBREVIE — Godot 4.7 — Script principal
# Rendu low-poly "PrimitiveMesh" (BoxMesh / SphereMesh /
# CylinderMesh / PrismMesh / CapsuleMesh) fidèle à la capture
# "image/1er image develloppement.png".
# ============================================================

extends Node3D

# CONFIG
const WORLD := 90.0          # demi-taille du terrain
const TOWN_R := 32.0         # rayon de la zone plate (ville)
const SPD := 5.0
const RUN := 9.0
const PV_MAX := 100
const DEGATS := 25           # dégâts du marteau (comme le "25" du screen)
const BUILD := "0.3.0-b43"    # témoin de build : titre de fenêtre + message d'accueil
const VILLAGE_R := 26.0      # village protégé : clôture + zone interdite aux monstres
const HAUT_COLLISION := 2.0  # hauteur logique PAR DÉFAUT d'un collider
const HAUT_CLOTURE := 1.0    # hauteur clôture village : sautable par le héros (saut 1,6 m), jamais par les monstres
const ACTIONS_REGLABLES := ["move_forward", "move_back", "move_left", "move_right", "jump", "sprint", "attack", "pickup", "camera_view", "inventaire", "options_menu", "quest_panel"]
const LIBELLES_TOUCHES := {
	"move_forward": "Avancer", "move_back": "Reculer",
	"move_left": "Gauche", "move_right": "Droite",
	"jump": "Sauter", "sprint": "Courir",
	"attack": "Attaquer", "pickup": "Ramasser",
	"camera_view": "Vue 1ère/3ème pers.", "inventaire": "Inventaire",
	"options_menu": "Menu options",
	"quest_panel": "Panneau de quêtes",
}

# VARIABLES JOUEUR
var player_pv := PV_MAX
var player_argent := 0.0
var player_cailloux := 0
var player_dead := false
var player_protected := false
var player_prot_timer := 0.0
var player_attack_cd := 0.0
var player_attack_anim := 0.0
var player_vel_y := 0.0
var player_on_ground := true
var player_walk_time := 0.0
var player_boost := 0.0

var xp := 0
var level := 1
var xp_need := 100
var kills_rats := 0
var kills_araignees := 0
var potions := [5, 5]
var slot_sel := 0

# CAMERA
var cam_dist := 6.5
var cam_angle_x := 18.0
var cam_angle_y := 0.0
var cam_sens := 0.3
var cam_drag := false
var cam_mode := "third"
var cam_invert_y := false
var config_path := "user://librevie_config.cfg"

var player_node: Node3D
var camera: Camera3D

# Références animation du héros
var jambe_gauche: Node3D
var jambe_droite: Node3D
var bras_gauche: Node3D
var bras_droit: Node3D

# HUD
var hp_bar: ProgressBar
var hp_label: Label
var mort_label: Label
var info_label: Label
var inv_panel: PanelContainer
var inv_open := false
var options_panel: PanelContainer
var invert_check: CheckBox
var slider_lum: HSlider
var slider_con: HSlider
var lbl_val_lum: Label
var lbl_val_con: Label
var minimap: Control
var hotbar: Control
var quest_panel: PanelContainer
var quest_toggle: Button
var quest_close: Button
var quest_reduit := false
var quest_ferme := false
var pill_or: Control
var pill_cailloux: Control
var quete_titre: Label
var quete_l1: Label
var quete_l2: Label

var enemies := []
var cailloux_items := []
var argent_items := []
var batiments := []
var _parent_capture: Node = null      # racine courante des pièces de bâtiment
var _fade_cache := {}                 # matériaux translucides (camouflage)
var camo_objs := []                   # objets camouflables (bâtiments + arbres)
var colliders: Array[Dictionary] = []   # collisions statiques (bâtiments, props, clôture, rochers)
var portes: Array[Dictionary] = []      # portails du village + leurs gardes
var monde_env: Environment              # réglages luminosité / contraste / saturation
var chemin_lisse: Array[Vector2] = []   # courbe lissée de la route (Catmull-Rom)
var opt_lum := 30                       # 1..100 (défaut demandé par le dev)
var capture_action := ""                # action en cours de reconfiguration (Options > Contrôles)
var touches_boutons := {}               # action -> Button
var pnj_items := []                       # b25 : PNJ animés (métiers)
var _ecran_souris := -1                   # b38 : écran où est la souris au démarrage
var _ecran_pos := Vector2i.ZERO           # b38 : position fenêtre calculée
var _ecran_frames := 0                    # b38 : ré-applique ~0.75 s
var panneau_graph: VBoxContainer
var panneau_ctrl: VBoxContainer
var tab_graph: Button
var tab_ctrl: Button
var opt_con := 100                      # 1..100 (défaut demandé par le dev)
var opt_res_idx := 2                    # index dans RESOLUTIONS (défaut 1280×720)
var opt_res: OptionButton
const RESOLUTIONS := [
	Vector2i(1024, 768), Vector2i(1152, 864), Vector2i(1280, 720), Vector2i(1280, 768),
	Vector2i(1280, 800), Vector2i(1280, 854), Vector2i(1280, 960), Vector2i(1280, 1024),
	Vector2i(1366, 768), Vector2i(1400, 1050), Vector2i(1440, 900), Vector2i(1440, 960),
	Vector2i(1600, 900), Vector2i(1600, 1024), Vector2i(1600, 1200), Vector2i(1680, 1050),
]
var nuages := []
var floaters := []
var sparks := []

# Caches de ressources (mesh + matériaux partagés)
var _mesh_cache := {}
var _mat_cache := {}

# ============================================================
# READY
# ============================================================
func _ready():
	# b38 : le jeu démarre sur l'écran où est la souris du joueur (= l'écran
	# du launcher, où il vient de cliquer sur JOUER).
	_detecter_ecran_souris()
	camera = $Camera3D
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
	DisplayServer.window_set_title("LibreVie %s" % BUILD)

	ajouter_actions_perso()
	lisser_chemin()
	load_config()

	creer_environnement()
	creer_terrain()
	creer_route()
	creer_ville()
	creer_chateau()
	creer_fontaine(9.0, 15.0)     # espace vide au SUD du village, bien visible
	creer_arbres()
	creer_props()
	creer_cloture_village()
	creer_nuages()
	creer_joueur()
	creer_ennemis()
	creer_objets()
	creer_hud()

	# Regen timer
	var timer = Timer.new()
	timer.wait_time = 3.0
	timer.autostart = true
	timer.timeout.connect(_on_regen)
	add_child(timer)


func _on_regen():
	if not player_dead and player_pv < PV_MAX:
		player_pv = mini(PV_MAX, player_pv + 1)

# ============================================================
# PROCESS
# ============================================================
func _process(delta: float):
	# b38 : fenêtre replacée sur l'écran de la souris pendant le démarrage
	if _ecran_frames > 0:
		_ecran_frames -= 1
		_placer_sur_ecran_souris()

	# Nuages qui dérivent
	for n in nuages:
		n.position.x += delta * 0.6
		if n.position.x > WORLD + 40:
			n.position.x = -WORLD - 40

	# b25 : PNJ qui travaillent (animations par métier)
	update_pnj(delta)

	# Pièces qui tournent
	for a in argent_items:
		if not a.gone:
			a.node.rotation.y += delta * 2.5

	# Floaters (dégâts)
	for i in range(floaters.size() - 1, -1, -1):
		var f = floaters[i]
		f.t += delta
		f.node.position.y += delta * 1.6
		var m: float = 1.0 - float(f.t)
		if m <= 0.0:
			f.node.queue_free()
			floaters.remove_at(i)
		else:
			f.node.modulate.a = m

	# Étincelles d'impact
	for i in range(sparks.size() - 1, -1, -1):
		var s = sparks[i]
		s.t += delta
		var k: float = float(s.t) / 0.25
		if k >= 1.0:
			s.node.queue_free()
			sparks.remove_at(i)
		else:
			s.node.scale = Vector3.ONE * (0.6 + k * 1.2)
			s.node.visible = (int(s.t * 30) % 2 == 0)

	# --- IA ENNEMIS ---
	update_ennemis(delta)
	update_gardes(delta)

	if player_dead:
		return

	# --- MOUVEMENT ---
	var move := Vector3.ZERO
	var speed: float = (RUN + (3.0 if player_boost > 0 else 0.0)) if Input.is_action_pressed("sprint") else SPD
	if player_boost > 0:
		player_boost -= delta

	if Input.is_action_pressed("move_forward"): move.z -= 1
	if Input.is_action_pressed("move_back"): move.z += 1
	if Input.is_action_pressed("move_left"): move.x -= 1
	if Input.is_action_pressed("move_right"): move.x += 1
	# Pendant qu'on reconfigure une touche (Options), le héros ne bouge plus
	if capture_action != "":
		move = Vector3.ZERO

	var is_moving: bool = move.length() > 0

	if is_moving:
		move = move.normalized()
		var cam_basis: Basis = camera.global_transform.basis
		var forward: Vector3 = -cam_basis.z
		forward.y = 0
		forward = forward.normalized()
		var right: Vector3 = cam_basis.x
		right.y = 0
		right = right.normalized()

		var dir: Vector3 = (forward * -move.z + right * move.x).normalized()
		var new_pos: Vector3 = player_node.global_position + dir * speed * delta

		if abs(new_pos.x) < WORLD - 2 and abs(new_pos.z) < WORLD - 2:
			# Collisions universelles : bâtiments, clôture, props, rochers, arbres...
			var res := resoudre_collisions(new_pos.x, new_pos.z, 0.45, player_node.global_position.y)
			new_pos.x = res.x
			new_pos.z = res.y
			# Les monstres sont solides eux aussi (on ne passe plus au travers)
			for e in enemies:
				if not e.alive:
					continue
				var en: Node3D = e.node
				# Un monstre, c'est ~1 m de haut : en pleine montée de saut on passe au-dessus
				if player_node.global_position.y - hauteur_terrain(en.global_position.x, en.global_position.z) > 1.0:
					continue
				var dx: float = new_pos.x - en.global_position.x
				var dz: float = new_pos.z - en.global_position.z
				var rr := 0.95
				var d2 := dx * dx + dz * dz
				if d2 < rr * rr:
					var d := sqrt(d2)
					if d < 0.0001:
						new_pos.x = en.global_position.x + rr
					else:
						new_pos.x = en.global_position.x + dx / d * rr
						new_pos.z = en.global_position.z + dz / d * rr
			player_node.global_position = new_pos

		# Orientation
		var look: Vector3 = player_node.global_position + dir
		player_node.look_at(look, Vector3.UP)

	# --- GRAVITE / SOL (avec atterrissage sur les objets : on peut se percher) ---
	var sol := hauteur_support(player_node.global_position.x, player_node.global_position.z, player_node.global_position.y)
	player_vel_y -= 20 * delta
	player_node.global_position.y += player_vel_y * delta
	if player_node.global_position.y <= sol and player_vel_y <= 0.0:
		player_node.global_position.y = sol
		player_vel_y = 0
		player_on_ground = true
	elif player_node.global_position.y > sol + 0.02:
		player_on_ground = false

	# --- ANIMATION MARCHE ---
	if is_moving:
		# b28 : la cadence des bras/jambes SUIT la vitesse (sprint = plus rapide)
		var sprinte := Input.is_action_pressed("sprint")
		player_walk_time += delta * (14.0 if sprinte else 9.0)
		var swing := sin(player_walk_time) * (0.78 if sprinte else 0.55)
		if jambe_gauche: jambe_gauche.rotation.x = swing
		if jambe_droite: jambe_droite.rotation.x = -swing
		if bras_gauche: bras_gauche.rotation.x = -swing * 0.7
		if bras_droit and player_attack_anim <= 0:
			bras_droit.rotation.x = swing * 0.5
	else:
		player_walk_time = 0
		if jambe_gauche: jambe_gauche.rotation.x = 0
		if jambe_droite: jambe_droite.rotation.x = 0
		if bras_gauche: bras_gauche.rotation.x = 0
		if bras_droit and player_attack_anim <= 0:
			bras_droit.rotation.x = 0

	# --- ANIMATION ATTAQUE ---
	if player_attack_anim > 0:
		player_attack_anim -= delta
		var k := 1.0 - clampf(player_attack_anim / 0.3, 0.0, 1.0)
		var ke := k * k * (3.0 - 2.0 * k)
		if bras_droit:
			# b14 (demande dev) : le bras passe DEVANT (levé) puis redescend
			# à la position verticale — plus de départ depuis l'arrière.
			bras_droit.rotation.x = lerpf(2.6, 0.0, ke)

	# --- COOLDOWNS ---
	if player_attack_cd > 0: player_attack_cd -= delta
	if player_protected:
		player_prot_timer -= delta
		if player_prot_timer <= 0: player_protected = false

	# --- CAMERA ---
	update_camera()
	maj_camouflage()

	# --- HUD ---
	update_hud()

# ============================================================
# INPUT
# ============================================================
func _input(event):
	# Capture d'une touche pour le réglage des contrôles (Options > Contrôles)
	if capture_action != "":
		if event is InputEventKey and event.pressed and not event.echo:
			var kc: int = event.physical_keycode
			if kc == 0:
				kc = event.keycode
			if kc != KEY_ESCAPE:
				set_action_key(capture_action, kc)
				save_config()
			capture_action = ""
			rafraichir_boutons_touches()
			get_viewport().set_input_as_handled()
			return
		if event is InputEventMouseButton and event.pressed:
			var bi: int = event.button_index
			if bi == MOUSE_BUTTON_LEFT or bi == MOUSE_BUTTON_RIGHT or bi == MOUSE_BUTTON_MIDDLE:
				set_action_mouse(capture_action, bi)
				capture_action = ""
				rafraichir_boutons_touches()
				save_config()
				get_viewport().set_input_as_handled()
				return
		get_viewport().set_input_as_handled()
		return

	if event.is_action_pressed("camera_view"):
		cam_mode = "first" if cam_mode == "third" else "third"
	if event.is_action_pressed("options_menu"):
		_toggle_options()
	if event.is_action_pressed("inventaire"):
		_toggle_inventory()
	if event.is_action_pressed("quest_panel"):
		_toggle_quest_visible()

	if event is InputEventMouseButton:
		if event.button_index == MOUSE_BUTTON_RIGHT:
			cam_drag = event.pressed
		if event.button_index == MOUSE_BUTTON_WHEEL_UP:
			cam_dist = max(3.0, cam_dist - 0.5)
		if event.button_index == MOUSE_BUTTON_WHEEL_DOWN:
			cam_dist = min(18.0, cam_dist + 0.5)

	if event is InputEventMouseMotion and cam_drag:
		var do_invert := false
		if is_instance_valid(invert_check):
			do_invert = invert_check.button_pressed
		if do_invert:
			cam_angle_y -= event.relative.x * cam_sens
			cam_angle_x += event.relative.y * cam_sens
		else:
			cam_angle_y += event.relative.x * cam_sens
			cam_angle_x -= event.relative.y * cam_sens
		cam_angle_x = clamp(cam_angle_x, 5.0, 70.0)

	if event.is_action_pressed("jump") and player_on_ground and not player_dead:
		player_vel_y = 8
		player_on_ground = false

	if event.is_action_pressed("attack"):
		attaquer()

	if event.is_action_pressed("pickup"):
		ramasser()

	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_R and player_dead:
			renaitre()
		if event.keycode == KEY_ESCAPE:
			get_tree().quit()
		if event.keycode >= KEY_1 and event.keycode <= KEY_5:
			utiliser_slot(int(event.keycode) - KEY_1)

# ============================================================
# CAMERA
# ============================================================
func update_camera():
	if not player_node:
		return
	var rx := deg_to_rad(cam_angle_x)
	var ry := deg_to_rad(cam_angle_y)
	var tx := player_node.global_position.x
	var ty := player_node.global_position.y + 1.1
	var tz := player_node.global_position.z

	if cam_mode == "third":
		var cx := tx + sin(ry) * cos(rx) * cam_dist
		var cy := ty + sin(rx) * cam_dist
		var cz := tz + cos(ry) * cos(rx) * cam_dist
		var hsol := hauteur_terrain(cx, cz) + 0.6
		if cy < hsol:
			cy = hsol
		camera.global_position = Vector3(cx, cy, cz)
		camera.look_at(Vector3(tx, ty, tz), Vector3.UP)
	else:
		camera.global_position = Vector3(tx, player_node.global_position.y + 1.55, tz)
		var lx := tx + sin(ry) * cos(rx) * 10
		var ly := player_node.global_position.y + 1.55 + sin(rx) * 10
		var lz := tz + cos(ry) * cos(rx) * 10
		camera.look_at(Vector3(lx, ly, lz), Vector3.UP)

# ============================================================
# TERRAIN (collines low-poly facettées)
# ============================================================
func hauteur_terrain(x: float, z: float) -> float:
	var d := Vector2(x, z).length()
	var t := clampf((d - TOWN_R) / 26.0, 0.0, 1.0)
	t = t * t * (3.0 - 2.0 * t)
	var h := 0.0
	h += sin(x * 0.09) * cos(z * 0.07) * 2.2
	h += sin(x * 0.21 + 1.7) * cos(z * 0.17 + 0.6) * 0.9
	h += sin((x + z) * 0.05) * 1.4
	# Colline du château au nord : PLATEAU plat (le château repose entièrement
	# dessus) + pente LARGE et étalée autour (b14 : 32 m de jupe au lieu de 12,
	# profil de colline douce au lieu du « gros tas de terre »).
	var dc := Vector2(x, z + 78.0).length()
	var plat := clampf((52.0 - dc) / 32.0, 0.0, 1.0)
	plat = plat * plat * (3.0 - 2.0 * plat)
	return lerpf(h * t, 11.0, plat)

var CHEMIN: Array[Vector2] = [
	Vector2(0, 30), Vector2(3, 18), Vector2(-2, 6), Vector2(1, -8),
	Vector2(4, -20), Vector2(-1, -34), Vector2(1, -48), Vector2(0.5, -56), Vector2(0, -62), Vector2(0, -67.8),  # b29 : fin DROITE jusqu'à la porte
]

func dist_chemin(p: Vector2) -> float:
	var pts: Array[Vector2] = chemin_lisse if chemin_lisse.size() > 2 else CHEMIN
	var best := 1e9
	for i in range(pts.size() - 1):
		var a := pts[i]
		var b := pts[i + 1]
		var ab := b - a
		var t := clampf((p - a).dot(ab) / maxf(ab.length_squared(), 0.001), 0.0, 1.0)
		best = minf(best, (p - (a + ab * t)).length())
	return best

# Courbe lissée de la route (Catmull-Rom) : fini les carrés, place aux courbes
func lisser_chemin():
	chemin_lisse.clear()
	var pts := CHEMIN
	var n := pts.size()
	if n < 2:
		return
	for i in range(n - 1):
		var p0: Vector2 = pts[maxi(i - 1, 0)]
		var p1: Vector2 = pts[i]
		var p2: Vector2 = pts[i + 1]
		var p3: Vector2 = pts[mini(i + 2, n - 1)]
		var seg := p1.distance_to(p2)
		var steps := maxi(int(seg * 2.0), 4)
		for k in range(steps):
			var t := float(k) / float(steps)
			var t2 := t * t
			var t3 := t2 * t
			var x := 0.5 * (2.0 * p1.x + (-p0.x + p2.x) * t + (2.0 * p0.x - 5.0 * p1.x + 4.0 * p2.x - p3.x) * t2 + (-p0.x + 3.0 * p1.x - 3.0 * p2.x + p3.x) * t3)
			var y := 0.5 * (2.0 * p1.y + (-p0.y + p2.y) * t + (2.0 * p0.y - 5.0 * p1.y + 4.0 * p2.y - p3.y) * t2 + (-p0.y + 3.0 * p1.y - 3.0 * p2.y + p3.y) * t3)
			chemin_lisse.append(Vector2(x, y))
	chemin_lisse.append(pts[n - 1])

# Route en ruban courbe + pavés (remplace les carrés de couleur du terrain)
func creer_route():
	var n := chemin_lisse.size()
	if n < 2:
		return
	var rng := RandomNumberGenerator.new()
	rng.seed = 4242
	var tangs: Array[Vector2] = []
	var perps: Array[Vector2] = []
	for i in range(n):
		var tg := Vector2(1, 0)
		if i == 0:
			tg = chemin_lisse[1] - chemin_lisse[0]
		elif i == n - 1:
			tg = chemin_lisse[n - 1] - chemin_lisse[n - 2]
		else:
			tg = chemin_lisse[i + 1] - chemin_lisse[i - 1]
		tg = tg.normalized()
		tangs.append(tg)
		perps.append(Vector2(-tg.y, tg.x))
	# --- Ruban : bordures sombres + voie claire ---
	var offs: Array[float] = [-2.85, -2.4, 2.4, 2.85]
	var tris := PackedVector3Array()
	var cols := PackedColorArray()
	for i in range(n - 1):
		for k in range(3):
			var base := Color(0.46, 0.36, 0.26) if (k == 0 or k == 2) else Color(0.66, 0.55, 0.40)
			var ca := base.lightened(rng.randf_range(-0.045, 0.045))
			var cb := base.lightened(rng.randf_range(-0.045, 0.045))
			var a0 := point_route(i, offs[k], perps[i])
			var a1 := point_route(i, offs[k + 1], perps[i])
			var b0 := point_route(i + 1, offs[k], perps[i + 1])
			var b1 := point_route(i + 1, offs[k + 1], perps[i + 1])
			tris.push_back(a0); cols.push_back(ca)
			tris.push_back(b0); cols.push_back(cb)
			tris.push_back(a1); cols.push_back(ca)
			tris.push_back(b0); cols.push_back(cb)
			tris.push_back(b1); cols.push_back(cb)
			tris.push_back(a1); cols.push_back(ca)
	var mi := MeshInstance3D.new()
	mi.mesh = mesh_tris(tris, cols)
	var mat_route := StandardMaterial3D.new()
	mat_route.albedo_color = Color(1, 1, 1)
	mat_route.roughness = 0.95
	mat_route.vertex_color_use_as_albedo = true
	mi.material_override = mat_route
	add_child(mi)
	# --- Pavés (petites pierres plates, 3 nuances) ---
	var pave := make_tube(0.34, 0.30, 0.08, 6)
	var teintes := [Color(0.70, 0.60, 0.46), Color(0.60, 0.50, 0.38), Color(0.76, 0.66, 0.52)]
	for ni in range(3):
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = pave
		mm.instance_count = 340
		var mmi := MultiMeshInstance3D.new()
		mmi.multimesh = mm
		mmi.material_override = mat_std(teintes[ni])
		add_child(mmi)
		for k in range(340):
			var idx := rng.randi_range(0, n - 1)
			var p: Vector2 = chemin_lisse[idx]
			var perp: Vector2 = perps[idx]
			var tang: Vector2 = tangs[idx]
			var off := rng.randf_range(-2.05, 2.05)
			var jit := rng.randf_range(-0.4, 0.4)
			var x := p.x + perp.x * off + tang.x * jit
			var z := p.y + perp.y * off + tang.y * jit
			var s := rng.randf_range(0.7, 1.3)
			# b14 : pavés POSÉS à plat sur la pente (alignés sur la normale du
			# terrain) : plus de pavés qui flottent / s'enfoncent dans la montée.
			var h0 := hauteur_terrain(x, z)
			var gx := hauteur_terrain(x + 0.5, z) - hauteur_terrain(x - 0.5, z)
			var gz := hauteur_terrain(x, z + 0.5) - hauteur_terrain(x, z - 0.5)
			var nrm := Vector3(-gx, 1.0, -gz).normalized()
			var xv := (Vector3.RIGHT - nrm * Vector3.RIGHT.dot(nrm)).normalized()
			var zv := xv.cross(nrm)
			var bas := Basis(xv, nrm, zv).rotated(nrm, rng.randf_range(0, TAU)).scaled(Vector3(s, rng.randf_range(0.5, 1.0), s))
			var tr := Transform3D(bas, Vector3(x, h0 + 0.06, z))
			mm.set_instance_transform(k, tr)

func point_route(i: int, off: float, perp: Vector2) -> Vector3:
	var p: Vector2 = chemin_lisse[i]
	var x := p.x + perp.x * off
	var z := p.y + perp.y * off
	return Vector3(x, hauteur_terrain(x, z) + 0.09, z)

# Le village de départ est PROTÉGÉ : clôture visuelle + zone interdite aux monstres
func dans_village(x: float, z: float) -> bool:
	return Vector2(x, z).length() < VILLAGE_R

# Réglages visuels (Options) : 1..100 -> 0.51..1.5
func appliquer_reglages_visuels():
	if monde_env == null:
		return
	monde_env.adjustment_brightness = 0.5 + float(opt_lum) / 100.0
	monde_env.adjustment_contrast = 0.5 + float(opt_con) / 100.0

# ============================================================
# COLLISIONS UNIVERSELLES (b9) : plus rien ne se traverse
# ============================================================
# b17 : une maison entre la caméra et le joueur devient TRANSLUCIDE
# (test segment caméra->joueur contre l'emprise XZ du bâtiment).
func mat_fade(m_orig: StandardMaterial3D) -> StandardMaterial3D:
	var id := m_orig.get_instance_id()
	if _fade_cache.has(id):
		return _fade_cache[id]
	var m: StandardMaterial3D = m_orig.duplicate()
	m.transparency = BaseMaterial3D.TRANSPARENCY_ALPHA
	var c := m.albedo_color
	m.albedo_color = Color(c.r, c.g, c.b, 0.28)
	if m.emission_enabled:
		m.emission_energy_multiplier = 0.4
	_fade_cache[id] = m
	return m

func maj_camouflage():
	if player_node == null or camera == null:
		return
	var cp := Vector2(camera.global_position.x, camera.global_position.z)
	var pp := Vector2(player_node.global_position.x, player_node.global_position.z)
	# b18 : TOUT objet (bâtiment OU arbre) entre la caméra et le joueur
	# devient translucide — emprises élargies, 14 échantillons sur le segment.
	for co in camo_objs:
		var r: Rect2 = Rect2(Vector2(co.x - co.rx, co.z - co.rz), Vector2(co.rx * 2.0, co.rz * 2.0))
		var want := false
		# b20 : si la caméra EST dans la zone élargie (dos au bâtiment) et le
		# joueur dehors, le mur/toit est entre les deux => fantôme aussi.
		# Seul cas sans fondu : caméra ET joueur inside (on longe le mur).
		if not (r.has_point(cp) and r.has_point(pp)):
			for k in range(1, 40):
				if r.has_point(cp.lerp(pp, float(k) / 40.0)):
					want = true
					break
		if want == co.fade:
			continue
		co.fade = want
		for mi in co.root.get_children():
			if want:
				mi.material_override = mat_fade(mi.get_meta("mat", mi.material_override))
			else:
				mi.material_override = mi.get_meta("mat", mi.material_override)

func appliquer_resolution():
	var r: Vector2i = RESOLUTIONS[clampi(opt_res_idx, 0, RESOLUTIONS.size() - 1)]
	DisplayServer.window_set_size(Vector2i(r.x, r.y))
	var sc := DisplayServer.window_get_current_screen()
	var ssize := DisplayServer.screen_get_size(sc)
	var px := maxi(int((ssize.x - r.x) / 2.0), 0)
	var py := maxi(int((ssize.y - r.y) / 2.0), 0)
	DisplayServer.window_set_position(Vector2i(px, py))

func col_cercle(x: float, z: float, r: float, h: float = HAUT_COLLISION):
	colliders.append({"t": "c", "x": x, "z": z, "r": r, "g": r + 1.0, "h": h})

func col_boite(x: float, z: float, w: float, d: float, h: float = HAUT_COLLISION):
	colliders.append({"t": "b", "x": x, "z": z, "w": w, "d": d, "g": maxf(w, d) * 0.5 + 1.0, "h": h})

func resoudre_collisions(px: float, pz: float, rayon: float, pieds: float = 0.0) -> Vector2:
	var p := Vector2(px, pz)
	for _passe in range(2):
		for c in colliders:
			var cx: float = c.x
			var cz: float = c.z
			var g: float = c.g
			if absf(p.x - cx) > g + rayon and absf(p.y - cz) > g + rayon:
				continue
			# Hauteur RÉELLE des objets : ce qui est plus bas que le saut (1,6 m)
			# peut être franchi en sautant (et on peut atterrir dessus, voir
			# hauteur_support). Epsilon 0,05 : debout SUR un objet, il ne pousse plus.
			if pieds > 0.0 and pieds > hauteur_terrain(cx, cz) + float(c.h) - 0.05:
				continue
			if c.t == "c":
				var rr: float = float(c.r) + rayon
				var dx: float = p.x - cx
				var dz: float = p.y - cz
				var d2 := dx * dx + dz * dz
				if d2 < rr * rr:
					var d := sqrt(d2)
					if d < 0.0001:
						p = Vector2(cx + rr, cz)
					else:
						p = Vector2(cx + dx / d * rr, cz + dz / d * rr)
			else:
				var hw: float = float(c.w) * 0.5 + rayon
				var hd: float = float(c.d) * 0.5 + rayon
				var dx2: float = p.x - cx
				var dz2: float = p.y - cz
				if absf(dx2) < hw and absf(dz2) < hd:
					var ox: float = hw - absf(dx2)
					var oz: float = hd - absf(dz2)
					if ox < oz:
						p.x = cx + (1.0 if dx2 >= 0.0 else -1.0) * hw
					else:
						p.y = cz + (1.0 if dz2 >= 0.0 else -1.0) * hd
		# Clôture du village (1 m de haut) : bloquante SAUF aux portails (route).
		# Le héros peut sauter par-dessus (saut 1,6 m) ; les monstres (pieds = 0)
		# ne la franchissent JAMAIS — le village reste protégé.
		var dl := p.length()
		if pieds <= hauteur_terrain(p.x, p.y) + HAUT_CLOTURE - 0.05 and absf(dl - VILLAGE_R) < 0.4 + rayon and dist_chemin(p) > 3.4:
			if dl < 0.001:
				p = Vector2(VILLAGE_R + 0.4 + rayon, 0)
			elif dl >= VILLAGE_R:
				p = p / dl * (VILLAGE_R + 0.4 + rayon)
			else:
				p = p / dl * (VILLAGE_R - 0.4 - rayon)
	return p

# Atterrissage sur les objets : quand le héros retombe (vel_y <= 0) au-dessus
# d'un objet dont le sommet est sous ses pieds, il se pose DESSUS (perché).
func hauteur_support(x: float, z: float, pieds: float) -> float:
	var sol := hauteur_terrain(x, z)
	# b24 : la route est pavée (sommet ~+0.10) : les pieds ne s'enfoncent plus
	var dr := dist_chemin(Vector2(x, z))
	if dr < 2.9:
		sol += 0.10 * clampf((2.9 - dr) / 0.6, 0.0, 1.0)
	for c in colliders:
		var cx: float = c.x
		var cz: float = c.z
		if absf(x - cx) > 4.0 and absf(z - cz) > 4.0:
			continue
		var dedans := false
		if c.t == "c":
			dedans = Vector2(x - cx, z - cz).length() < float(c.r) + 0.1
		else:
			dedans = absf(x - cx) < float(c.w) * 0.5 + 0.05 and absf(z - cz) < float(c.d) * 0.5 + 0.05
		if not dedans:
			continue
		var top := hauteur_terrain(cx, cz) + float(c.h)
		if top > sol and pieds >= top - 0.25:
			sol = top
	return sol

func creer_terrain():
	var N := 88
	var pas := (WORLD * 2.0) / float(N)
	var tris := PackedVector3Array()
	var cols := PackedColorArray()
	var rng := RandomNumberGenerator.new()
	rng.seed = 42
	for i in range(N):
		for j in range(N):
			var x0 := -WORLD + i * pas
			var z0 := -WORLD + j * pas
			var x1 := x0 + pas
			var z1 := z0 + pas
			var v00 := Vector3(x0, hauteur_terrain(x0, z0), z0)
			var v10 := Vector3(x1, hauteur_terrain(x1, z0), z0)
			var v01 := Vector3(x0, hauteur_terrain(x0, z1), z1)
			var v11 := Vector3(x1, hauteur_terrain(x1, z1), z1)
			# Couleur par face
			var cx := (x0 + x1) * 0.5
			var cz := (z0 + z1) * 0.5
			var cy := hauteur_terrain(cx, cz)
			var col := Color(0.40, 0.56, 0.22)
			col = col.lightened(clampf(cy * 0.03, 0.0, 0.18))
			var v := rng.randf_range(-0.045, 0.045)
			col = Color(col.r + v, col.g + v * 0.8, col.b + v * 0.5)
			tris.push_back(v00); cols.push_back(col)
			tris.push_back(v10); cols.push_back(col)
			tris.push_back(v11); cols.push_back(col)
			tris.push_back(v00); cols.push_back(col)
			tris.push_back(v11); cols.push_back(col)
			tris.push_back(v01); cols.push_back(col)
	var m := mesh_tris(tris, cols)
	var mi := MeshInstance3D.new()
	mi.mesh = m
	var mat := mat_std(Color(1, 1, 1))
	mat.vertex_color_use_as_albedo = true
	mi.material_override = mat
	add_child(mi)

	# Herbe en touffes (MultiMesh, 3 nuances)
	var touffe := make_touffe()
	var nuances := [Color(0.28, 0.55, 0.16), Color(0.36, 0.65, 0.22), Color(0.45, 0.72, 0.26)]
	var rng2 := RandomNumberGenerator.new()
	rng2.seed = 7
	for ni in range(3):
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = touffe
		mm.instance_count = 140
		var mmi := MultiMeshInstance3D.new()
		mmi.multimesh = mm
		mmi.material_override = mat_std(nuances[ni])
		add_child(mmi)
		for k in range(140):
			var x := rng2.randf_range(-WORLD + 4, WORLD - 4)
			var z := rng2.randf_range(-WORLD + 4, WORLD - 4)
			if Vector2(x, z).length() < TOWN_R * 0.55:
				x += TOWN_R
			x = clampf(x, -WORLD + 4, WORLD - 4)
			z = clampf(z, -WORLD + 4, WORLD - 4)
			# b30 : rien sur la route (touffes)
			var garde := 0
			while dist_chemin(Vector2(x, z)) < 3.2 and garde < 8:
				x = rng2.randf_range(-WORLD + 4, WORLD - 4)
				z = rng2.randf_range(-WORLD + 4, WORLD - 4)
				garde += 1
			var y := hauteur_terrain(x, z)
			var tr := Transform3D()
			tr = tr.rotated(Vector3.UP, rng2.randf_range(0, TAU))
			tr = tr.scaled(Vector3.ONE * rng2.randf_range(0.8, 1.6))
			tr.origin = Vector3(x, y - 0.02, z)
			mm.set_instance_transform(k, tr)

	# b24 : fleurs SUPPRIMÉES (c'étaient les « petites boules jaunes » au sol)

	# Rochers (MultiMesh facetté)
	var roche := make_roche()
	var rng4 := RandomNumberGenerator.new()
	rng4.seed = 99
	for ni in range(2):
		var colr := Color(0.55, 0.55, 0.56) if ni == 0 else Color(0.44, 0.44, 0.46)
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = roche
		mm.instance_count = 45
		var mmi := MultiMeshInstance3D.new()
		mmi.multimesh = mm
		mmi.material_override = mat_std(colr)
		add_child(mmi)
		for k in range(45):
			var x := rng4.randf_range(-WORLD + 5, WORLD - 5)
			var z := rng4.randf_range(-WORLD + 5, WORLD - 5)
			if Vector2(x, z).length() < TOWN_R * 0.6:
				x += TOWN_R
			x = clampf(x, -WORLD + 5, WORLD - 5)
			z = clampf(z, -WORLD + 5, WORLD - 5)
			# Pas de rocher planté sur la clôture du village
			var dr := Vector2(x, z)
			if dr.length() > 0.001 and absf(dr.length() - VILLAGE_R) < 2.2:
				dr = dr.normalized() * (VILLAGE_R + 2.6)
				x = dr.x
				z = dr.y
			# b30 : rien sur la route (rochers)
			var garde := 0
			while dist_chemin(Vector2(x, z)) < 4.0 and garde < 8:
				x = rng4.randf_range(-WORLD + 5, WORLD - 5)
				z = rng4.randf_range(-WORLD + 5, WORLD - 5)
				garde += 1
			var s := rng4.randf_range(0.4, 1.5)
			var tr := Transform3D()
			tr = tr.rotated(Vector3.UP, rng4.randf_range(0, TAU))
			tr = tr.scaled(Vector3(s * rng4.randf_range(0.8, 1.3), s * 0.75, s))
			tr.origin = Vector3(x, hauteur_terrain(x, z) + s * 0.18, z)
			mm.set_instance_transform(k, tr)
			# Collision, sauf si le rocher est posé sur la route
			if dist_chemin(Vector2(x, z)) > 2.8:
				col_cercle(x, z, 0.42 * s, 0.45 * s + 0.2)

# Maille facettée à partir de triangles (normales plates)
func mesh_tris(tris: PackedVector3Array, cols: PackedColorArray) -> ArrayMesh:
	var st := SurfaceTool.new()
	st.begin(Mesh.PRIMITIVE_TRIANGLES)
	for i in range(tris.size()):
		if cols.size() == tris.size():
			st.set_color(cols[i])
		st.add_vertex(tris[i])
	st.generate_normals()
	return st.commit()

# Touffe d'herbe : 3 pointes
func make_touffe() -> ArrayMesh:
	var tris := PackedVector3Array()
	var offs: Array[Vector3] = [Vector3(0, 0, 0), Vector3(0.09, 0, 0.05), Vector3(-0.07, 0, 0.08)]
	var hts: Array[float] = [0.42, 0.32, 0.28]
	for k in range(3):
		var o := offs[k]
		var h := hts[k]
		var r := 0.055
		var apex := o + Vector3(randf_range(-0.04, 0.04), h, randf_range(-0.04, 0.04))
		var base := [
			o + Vector3(-r, 0, -r), o + Vector3(r, 0, -r),
			o + Vector3(r, 0, r), o + Vector3(-r, 0, r),
		]
		for b in range(4):
			tris.push_back(base[b])
			tris.push_back(base[(b + 1) % 4])
			tris.push_back(apex)
	return mesh_tris(tris, PackedColorArray())

# b43 : anneau horizontal de n points (ellipse) autour de c
func anneau(c: Vector3, rx: float, rz: float, n: int) -> Array:
	var r := []
	for i in range(n):
		var a := float(i) / float(n) * TAU
		r.push_back(c + Vector3(cos(a) * rx, 0, sin(a) * rz))
	return r

# b43 : anneau VERTICAL (perpendiculaire à Z) : corps allongés horizontaux
func anneau_z(c: Vector3, rx: float, ry: float, n: int) -> Array:
	var r := []
	for i in range(n):
		var a := float(i) / float(n) * TAU
		r.push_back(c + Vector3(cos(a) * rx, sin(a) * ry, 0))
	return r

# b43 : LOFT : relie des anneaux consécutifs = forme organique arrondie
# (le vrai maillage d'un modèle 3D, pas un assemblage de cubes)
func make_loft(rings: Array, cap_deb := true, cap_fin := true) -> ArrayMesh:
	var tris := PackedVector3Array()
	for k in range(rings.size() - 1):
		var a: Array = rings[k]
		var b: Array = rings[k + 1]
		var n: int = a.size()
		for i in range(n):
			var j := (i + 1) % n
			tris.push_back(a[i]); tris.push_back(b[i]); tris.push_back(b[j])
			tris.push_back(a[i]); tris.push_back(b[j]); tris.push_back(a[j])
	if cap_deb and rings.size() > 0:
		var a0: Array = rings[0]
		for i in range(1, a0.size() - 1):
			tris.push_back(a0[0]); tris.push_back(a0[i + 1]); tris.push_back(a0[i])
	if cap_fin and rings.size() > 0:
		var a1: Array = rings[rings.size() - 1]
		for i in range(1, a1.size() - 1):
			tris.push_back(a1[0]); tris.push_back(a1[i]); tris.push_back(a1[i + 1])
	return mesh_tris(tris, PackedColorArray())

# b43 : MEMBRE FUSELE entre 2 points (bras, jambe, patte, queue) :
# section ronde qui rétrécit => forme arrondie vivante, pas un cylindre droit
func make_membre(p0: Vector3, p1: Vector3, r0: float, r1: float, n := 7) -> ArrayMesh:
	var d := p1 - p0
	var L := d.length()
	if L < 0.001:
		p1 = p0 + Vector3(0, 0.02, 0)
		d = p1 - p0
		L = d.length()
	var up := d / L
	var ax: Vector3
	if absf(up.y) < 0.9:
		ax = up.cross(Vector3(0, 1, 0)).normalized()
	else:
		ax = up.cross(Vector3(1, 0, 0)).normalized()
	var az := up.cross(ax).normalized()
	var rings := []
	for t in [0.0, 0.35, 0.7, 1.0]:
		var c := p0.lerp(p1, t)
		var r: float = lerpf(r0, r1, t)
		var ring := []
		for i in range(n):
			var a := float(i) / float(n) * TAU
			ring.push_back(c + ax * (cos(a) * r) + az * (sin(a) * r))
		rings.push_back(ring)
	return make_loft(rings)

# b43 : rectangle à coins coupés (8 pts) pour boîtes chanfreinées
func _ring_rect(c: Vector3, hx: float, hz: float, b: float) -> Array:
	return [
		c + Vector3(-hx + b, 0, -hz), c + Vector3(hx - b, 0, -hz),
		c + Vector3(hx, 0, -hz + b), c + Vector3(hx, 0, hz - b),
		c + Vector3(hx - b, 0, hz), c + Vector3(-hx + b, 0, hz),
		c + Vector3(-hx, 0, hz - b), c + Vector3(-hx, 0, -hz + b),
	]

# b43 : BOÎTE TRAVAILLÉE : octogonale, chanfreins haut/bas (plus de cube sec)
func make_bboite(size: Vector3, bev := 0.06) -> ArrayMesh:
	var hx := size.x / 2.0
	var hy := size.y / 2.0
	var hz := size.z / 2.0
	var b := minf(bev, minf(hx, minf(hy, hz)) * 0.45)
	var rings := [
		_ring_rect(Vector3(0, -hy, 0), hx - b, hz - b, b),
		_ring_rect(Vector3(0, -hy + b, 0), hx, hz, b),
		_ring_rect(Vector3(0, hy - b, 0), hx, hz, b),
		_ring_rect(Vector3(0, hy, 0), hx - b, hz - b, b),
	]
	return make_loft(rings)

# b43 : instance de maillage organique (cache + double face)
func _org(key: String, m: ArrayMesh, pos: Vector3, col: Color, parent: Node = null, rot := Vector3.ZERO, scale := Vector3.ONE) -> MeshInstance3D:
	if not _mesh_cache.has(key):
		_mesh_cache[key] = m
	var mi := MeshInstance3D.new()
	mi.mesh = _mesh_cache[key]
	var mat := mat_std(col)
	mat.cull_mode = BaseMaterial3D.CULL_DISABLED
	mi.material_override = mat
	mi.set_meta("mat", mat)
	mi.position = pos
	mi.rotation = rot
	mi.scale = scale
	(parent if parent else (_parent_capture if _parent_capture else self)).add_child(mi)
	return mi

# b40 : piquet de palissade : planche + pointe pyramidale
func make_piquet() -> ArrayMesh:
	var tris := PackedVector3Array()
	var w := 0.115
	var d := 0.05
	var y1 := 1.20
	var apex := 1.52
	var b: Array[Vector3] = [Vector3(-w, 0, -d), Vector3(w, 0, -d), Vector3(w, 0, d), Vector3(-w, 0, d)]
	var m: Array[Vector3] = [Vector3(-w, y1, -d), Vector3(w, y1, -d), Vector3(w, y1, d), Vector3(-w, y1, d)]
	for k in range(4):
		tris.push_back(b[k]); tris.push_back(b[(k + 1) % 4]); tris.push_back(m[(k + 1) % 4])
		tris.push_back(b[k]); tris.push_back(m[(k + 1) % 4]); tris.push_back(m[k])
	for k in range(4):
		tris.push_back(m[k]); tris.push_back(m[(k + 1) % 4]); tris.push_back(Vector3(0, apex, 0))
	return mesh_tris(tris, PackedColorArray())

# Rocher low-poly : hexaèdre jitteré
func make_roche() -> ArrayMesh:
	var rng := RandomNumberGenerator.new()
	rng.seed = 5
	var c := []
	for i in range(8):
		var v := Vector3(-1 if (i & 1) == 0 else 1, -1 if (i & 2) == 0 else 1, -1 if (i & 4) == 0 else 1) * 0.5
		v += Vector3(rng.randf_range(-0.16, 0.16), rng.randf_range(-0.16, 0.16), rng.randf_range(-0.16, 0.16))
		c.push_back(v)
	var faces := [[0, 1, 3, 2], [4, 6, 7, 5], [0, 4, 5, 1], [2, 3, 7, 6], [0, 2, 6, 4], [1, 5, 7, 3]]
	var tris := PackedVector3Array()
	for f in faces:
		tris.push_back(c[f[0]]); tris.push_back(c[f[1]]); tris.push_back(c[f[2]])
		tris.push_back(c[f[0]]); tris.push_back(c[f[2]]); tris.push_back(c[f[3]])
	return mesh_tris(tris, PackedColorArray())

# ============================================================
# ENVIRONNEMENT (ciel bleu vif + soleil chaud)
# ============================================================
func creer_environnement():
	var env := Environment.new()
	env.background_mode = Environment.BG_SKY
	var sky := Sky.new()
	var sm := ProceduralSkyMaterial.new()
	sm.sky_top_color = Color(0.34, 0.45, 0.75)
	sm.sky_horizon_color = Color(0.98, 0.74, 0.48)
	sm.ground_bottom_color = Color(0.45, 0.50, 0.26)
	sm.ground_horizon_color = Color(0.98, 0.74, 0.48)
	sky.sky_material = sm
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 1.0
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	# Couleurs globales adoucies (saturation) + réglages luminosité/contraste
	monde_env = env
	env.adjustment_enabled = true
	env.adjustment_saturation = 1.0
	appliquer_reglages_visuels()
	get_viewport().world_3d.environment = env

	# Soleil (le DirectionalLight3D de main.tscn)
	var sun: DirectionalLight3D = $DirectionalLight3D
	sun.rotation = Vector3(deg_to_rad(-40), deg_to_rad(-32), 0)
	sun.light_color = Color(1.0, 0.85, 0.64)
	sun.light_energy = 1.25
	sun.shadow_enabled = true

# ============================================================
# OUTILS MESH / MATERIAUX (PrimitiveMesh partagés)
# ============================================================
func mat_std(col: Color, unlit := false, emissive := false) -> StandardMaterial3D:
	var key := "%s|%d|%d" % [col.to_html(), 1 if unlit else 0, 1 if emissive else 0]
	if _mat_cache.has(key):
		return _mat_cache[key]
	var m := StandardMaterial3D.new()
	m.albedo_color = col
	m.roughness = 0.9
	m.metallic = 0.0
	if unlit:
		# Godot 4.7 : l'enum ShadingMode n'a que PIXEL/VERTEX, pas de mode
		# unlit natif => albedo noir + emission plate pleine couleur.
		m.albedo_color = Color(0, 0, 0)
		m.emission_enabled = true
		m.emission = col
		m.emission_energy_multiplier = 1.0
	if emissive:
		m.emission_enabled = true
		m.emission = col
		m.emission_energy_multiplier = 2.0
	_mat_cache[key] = m
	return m

func _box(pos: Vector3, size: Vector3, col: Color, parent: Node = null, rot := Vector3.ZERO) -> MeshInstance3D:
	var key := "box%s" % size
	if not _mesh_cache.has(key):
		var b := BoxMesh.new()
		b.size = size
		_mesh_cache[key] = b
	var mi := MeshInstance3D.new()
	mi.mesh = _mesh_cache[key]
	mi.material_override = mat_std(col)
	mi.set_meta("mat", mi.material_override)
	mi.position = pos
	mi.rotation = rot
	(parent if parent else (_parent_capture if _parent_capture else self)).add_child(mi)
	return mi

# b21 : tube en ArrayMesh (SurfaceTool) — le CylinderMesh natif ne s'affiche
# PAS chez le dev (Godot 4.7.2) : manches, troncs, fontaine, pièces étaient
# invisibles. Même pipeline que le terrain (mesh_tris) => affichage garanti.
func make_tube(r_bot: float, r_top: float, h: float, seg: int) -> ArrayMesh:
	var tris := PackedVector3Array()
	var n := maxi(seg, 3)
	var y0 := -h / 2.0
	var y1 := h / 2.0
	var cb := Vector3(0, y0, 0)
	var ct := Vector3(0, y1, 0)
	for i in range(n):
		var a0 := float(i) / float(n) * TAU
		var a1 := float(i + 1) / float(n) * TAU
		var b0 := Vector3(cos(a0) * r_bot, y0, sin(a0) * r_bot)
		var b1 := Vector3(cos(a1) * r_bot, y0, sin(a1) * r_bot)
		var t0 := Vector3(cos(a0) * r_top, y1, sin(a0) * r_top)
		var t1 := Vector3(cos(a1) * r_top, y1, sin(a1) * r_top)
		tris.push_back(b0); tris.push_back(t1); tris.push_back(t0)
		tris.push_back(b0); tris.push_back(b1); tris.push_back(t1)
		tris.push_back(ct); tris.push_back(t0); tris.push_back(t1)
		tris.push_back(cb); tris.push_back(b1); tris.push_back(b0)
	return mesh_tris(tris, PackedColorArray())

func _cyl(pos: Vector3, r_bot: float, r_top: float, h: float, col: Color, parent: Node = null, seg := 10, rot := Vector3.ZERO) -> MeshInstance3D:
	var key := "cyl%.2f_%.2f_%.2f_%d" % [r_bot, r_top, h, seg]
	if not _mesh_cache.has(key):
		_mesh_cache[key] = make_tube(r_bot, r_top, h, seg)
	var mi := MeshInstance3D.new()
	mi.mesh = _mesh_cache[key]
	mi.material_override = mat_std(col)
	mi.set_meta("mat", mi.material_override)
	mi.position = pos
	mi.rotation = rot
	(parent if parent else self).add_child(mi)
	return mi

func _cone(pos: Vector3, r: float, h: float, col: Color, parent: Node = null, seg := 7) -> MeshInstance3D:
	return _cyl(pos, r, 0.0, h, col, parent, seg)

func _sph(pos: Vector3, r: float, col: Color, parent: Node = null, emissive := false, lowpoly := true) -> MeshInstance3D:
	var key := "sph%.2f_%d" % [r, 1 if lowpoly else 0]
	if not _mesh_cache.has(key):
		var s := SphereMesh.new()
		s.radius = r
		s.height = r * 2.0
		if lowpoly:
			s.radial_segments = 8
			s.rings = 5
		else:
			s.radial_segments = 16
			s.rings = 8
		_mesh_cache[key] = s
	var mi := MeshInstance3D.new()
	mi.mesh = _mesh_cache[key]
	mi.material_override = mat_std(col, false, emissive)
	mi.set_meta("mat", mi.material_override)
	mi.position = pos
	(parent if parent else self).add_child(mi)
	return mi

func _facette(pos: Vector3, col: Color, parent: Node = null, scale := Vector3.ONE) -> MeshInstance3D:
	var key := "roche"
	if not _mesh_cache.has(key):
		_mesh_cache[key] = make_roche()
	var mi := MeshInstance3D.new()
	mi.mesh = _mesh_cache[key]
	mi.material_override = mat_std(col)
	mi.set_meta("mat", mi.material_override)
	mi.position = pos
	mi.scale = scale
	(parent if parent else self).add_child(mi)
	return mi

func _caps(pos: Vector3, r: float, h: float, col: Color, parent: Node = null) -> MeshInstance3D:
	var key := "cap%.2f_%.2f" % [r, h]
	if not _mesh_cache.has(key):
		var c := CapsuleMesh.new()
		c.radius = r
		c.height = h
		c.radial_segments = 8
		c.rings = 3
		_mesh_cache[key] = c
	var mi := MeshInstance3D.new()
	mi.mesh = _mesh_cache[key]
	mi.material_override = mat_std(col)
	mi.set_meta("mat", mi.material_override)
	mi.position = pos
	(parent if parent else self).add_child(mi)
	return mi

func _prism(pos: Vector3, size: Vector3, col: Color, parent: Node = null, rot := Vector3.ZERO) -> MeshInstance3D:
	var key := "pri%s" % size
	if not _mesh_cache.has(key):
		var p := PrismMesh.new()
		p.size = size
		_mesh_cache[key] = p
	var mi := MeshInstance3D.new()
	mi.mesh = _mesh_cache[key]
	mi.material_override = mat_std(col)
	mi.set_meta("mat", mi.material_override)
	mi.position = pos
	mi.rotation = rot
	(parent if parent else (_parent_capture if _parent_capture else self)).add_child(mi)
	return mi

# ============================================================
# VILLE
# ============================================================
func creer_ville():
	var PIERRE := Color(0.50, 0.48, 0.45)
	var data = [
		{"x":-10,"z":-5,"w":5,"h":4,"d":4,"c":Color(0.88,0.80,0.64),"n":"Supermarche","roof":Color(0.74,0.30,0.11)},
		{"x":-10,"z":3,"w":4,"h":3.5,"d":3.5,"c":Color(0.86,0.76,0.58),"n":"Armurerie","roof":Color(0.66,0.26,0.10)},
		{"x":10,"z":-6,"w":4,"h":3.5,"d":3.5,"c":Color(0.90,0.84,0.70),"n":"Vetements","roof":Color(0.78,0.36,0.14)},
		{"x":10,"z":3,"w":4,"h":4,"d":4,"c":Color(0.87,0.78,0.60),"n":"Auberge","roof":Color(0.70,0.27,0.10)},
		{"x":-9,"z":-20,"w":8,"h":6,"d":6,"c":Color(0.90,0.87,0.78),"n":"Mairie","roof":Color(0.28,0.42,0.60)},
		{"x":-20,"z":-12,"w":4,"h":3.5,"d":3.5,"c":Color(0.88,0.80,0.64),"n":"Maison","roof":Color(0.72,0.29,0.11)},
		{"x":-20,"z":-4,"w":3.5,"h":3,"d":3.5,"c":Color(0.85,0.74,0.56),"n":"Maison","roof":Color(0.64,0.25,0.09)},
		{"x":20,"z":8,"w":4,"h":3.5,"d":3.5,"c":Color(0.88,0.80,0.64),"n":"Maison","roof":Color(0.72,0.29,0.11)},
		{"x":20,"z":-4,"w":3.5,"h":3,"d":3.5,"c":Color(0.85,0.74,0.56),"n":"Maison","roof":Color(0.64,0.25,0.09)},
	]
	for b in data:
		var y := hauteur_terrain(b.x, b.z)
		var broot := Node3D.new()
		add_child(broot)
		_parent_capture = broot
		# Murs
		_box(Vector3(b.x, y + b.h / 2.0, b.z), Vector3(b.w, b.h, b.d), b.c)
		# Colombages façade (thème de base : image de référence)
		var BOIS := Color(0.36, 0.22, 0.11)
		_box(Vector3(b.x, y + b.h * 0.52, b.z + b.d / 2.0 + 0.04), Vector3(b.w * 0.98, 0.12, 0.10), BOIS)
		_box(Vector3(b.x - b.w / 2.0 + 0.08, y + b.h * 0.5, b.z + b.d / 2.0 + 0.04), Vector3(0.14, b.h * 0.98, 0.10), BOIS)
		_box(Vector3(b.x + b.w / 2.0 - 0.08, y + b.h * 0.5, b.z + b.d / 2.0 + 0.04), Vector3(0.14, b.h * 0.98, 0.10), BOIS)
		# b15 : murs « texturés » : soubassement pierre + panneaux de crépi
		# (avant/bas plus clairs, arrière ombré) — finis les gros cubes unis.
		_box(Vector3(b.x, y + b.h * 0.14, b.z), Vector3(b.w + 0.10, b.h * 0.28, b.d + 0.10), PIERRE.lightened(0.14))
		_box(Vector3(b.x, y + b.h * 0.62, b.z + b.d / 2.0 + 0.03), Vector3(b.w * 0.42, b.h * 0.30, 0.06), b.c.lightened(0.07))
		_box(Vector3(b.x, y + b.h * 0.62, b.z - b.d / 2.0 - 0.03), Vector3(b.w * 0.42, b.h * 0.30, 0.06), b.c.darkened(0.06))
		# Fondations pierre
		_box(Vector3(b.x, y + 0.1, b.z), Vector3(b.w + 0.25, 0.25, b.d + 0.25), PIERRE)
		# Toit en prisme (pignon) + débords
		var rh: float = b.d * 0.55   # b40 : pente PLUS raide (image de réf.)
		_prism(Vector3(b.x, y + b.h + rh / 2.0 - 0.05, b.z), Vector3(b.d + 0.35, rh, b.w + 0.35), b.roof, null, Vector3(0, deg_to_rad(90), 0))
		# b15 : TUILES visibles : 4 rangs en gradins inclinés par pan + faîtage
		var NR := 6
		var ang := atan2(rh, (b.d + 0.35) / 2.0)
		for side in [-1.0, 1.0]:
			for ti in range(NR):
				var tm := (float(ti) + 0.5) / float(NR)
				var zm: float = side * tm * (b.d + 0.35) / 2.0 * 0.94
				var ym: float = y + b.h + rh * (1.0 - tm) + 0.02
				var tc: Color = b.roof.lightened(0.07) if ti % 2 == 0 else b.roof.darkened(0.10)
				# b43 : tuiles AJUSTÉES au pan (ne débordent plus du toit)
				var NW := 8
				var lw: float = (b.w + 0.35) / float(NW)
				for j in range(NW):
					var xm: float = -((b.w + 0.35) / 2.0) + (float(j) + 0.5) * lw
					if ti % 2 == 1:
						xm += lw * 0.5
						if xm > (b.w + 0.35) / 2.0:
							xm -= (b.w + 0.35)
					var tc2: Color = tc.lightened(0.06) if (ti + j) % 2 == 0 else tc.darkened(0.08)
					_box(Vector3(b.x + xm, ym, b.z + zm), Vector3(lw * 0.92, 0.08, (b.d + 0.35) / 2.0 / float(NR) * 1.15), tc2, null, Vector3(side * ang, 0, 0))
		_box(Vector3(b.x, y + b.h + rh + 0.02, b.z), Vector3(b.w + 0.45, 0.12, 0.26), b.roof.darkened(0.15))
		# Porte + linteau
		_box(Vector3(b.x, y + b.h * 0.28, b.z + b.d / 2.0 + 0.06), Vector3(b.w * 0.22, b.h * 0.52, 0.14), Color(0.25, 0.14, 0.06))
		# b40 : porte CINTRÉE (demi-cylindre au sommet, comme l'image)
		_cyl(Vector3(b.x, y + b.h * 0.54, b.z + b.d / 2.0 + 0.06), b.w * 0.11, b.w * 0.11, 0.14, Color(0.25, 0.14, 0.06), broot, 8, Vector3(deg_to_rad(90), 0, 0))
		_box(Vector3(b.x, y + b.h * 0.56, b.z + b.d / 2.0 + 0.06), Vector3(b.w * 0.28, 0.08, 0.16), Color(0.38, 0.22, 0.09))
		# b23 : belle pancarte en bois au-dessus de la porte, avec le nom du lieu
		var NOMS := {"Supermarche": "SUPERMARCHÉ", "Armurerie": "ARMURERIE", "Vetements": "VÊTEMENTS", "Auberge": "AUBERGE", "Mairie": "MAIRIE", "Maison": "MAISON"}
		var txt: String = NOMS.get(b.n, b.n)
		_box(Vector3(b.x, y + b.h * 0.78, b.z + b.d / 2.0 + 0.09), Vector3(b.w * 0.65, 0.46, 0.07), Color(0.30, 0.19, 0.09))
		_box(Vector3(b.x, y + b.h * 0.78, b.z + b.d / 2.0 + 0.11), Vector3(b.w * 0.60, 0.34, 0.06), Color(0.52, 0.36, 0.19))
		var lbl := Label3D.new()
		lbl.text = txt
		lbl.font_size = 64
		lbl.pixel_size = 0.0035
		lbl.modulate = Color(0.16, 0.10, 0.04)
		lbl.position = Vector3(b.x, y + b.h * 0.78, b.z + b.d / 2.0 + 0.15)
		broot.add_child(lbl)
		# Fenêtres façade (cadre + vitre + croix)
		for fx in [-0.28, 0.28]:
			_box(Vector3(b.x + b.w * fx, y + b.h * 0.60, b.z + b.d / 2.0 + 0.06), Vector3(b.w * 0.18, b.h * 0.20, 0.12), Color(0.32, 0.21, 0.10))
			var vitre := _box(Vector3(b.x + b.w * fx, y + b.h * 0.60, b.z + b.d / 2.0 + 0.08), Vector3(b.w * 0.13, b.h * 0.14, 0.08), Color(1.0, 0.72, 0.30))
			vitre.material_override = mat_std(Color(1.0, 0.72, 0.30), false, true)
			vitre.set_meta("mat", vitre.material_override)
			_box(Vector3(b.x + b.w * fx, y + b.h * 0.60, b.z + b.d / 2.0 + 0.10), Vector3(b.w * 0.13, 0.03, 0.03), Color(0.32, 0.21, 0.10))
			_box(Vector3(b.x + b.w * fx, y + b.h * 0.60, b.z + b.d / 2.0 + 0.10), Vector3(0.03, b.h * 0.14, 0.03), Color(0.32, 0.21, 0.10))
			_box(Vector3(b.x + b.w * fx, y + b.h * 0.60 - b.h * 0.10 - 0.05, b.z + b.d / 2.0 + 0.10), Vector3(b.w * 0.20, 0.07, 0.16), PIERRE.lightened(0.22))
			# b40 : volets verts de part et d'autre (détail de l'image)
			_box(Vector3(b.x + b.w * fx - b.w * 0.115, y + b.h * 0.60, b.z + b.d / 2.0 + 0.09), Vector3(b.w * 0.05, b.h * 0.17, 0.05), Color(0.20, 0.38, 0.34), broot)
			_box(Vector3(b.x + b.w * fx + b.w * 0.115, y + b.h * 0.60, b.z + b.d / 2.0 + 0.09), Vector3(b.w * 0.05, b.h * 0.17, 0.05), Color(0.20, 0.38, 0.34), broot)
		# Fenêtres côtés
		for fz in [-0.28, 0.28]:
			var v2 := _box(Vector3(b.x + b.w / 2.0 + 0.06, y + b.h * 0.60, b.z + b.d * fz), Vector3(0.12, b.h * 0.16, b.d * 0.13), Color(1.0, 0.72, 0.30))
			v2.material_override = mat_std(Color(1.0, 0.72, 0.30), false, true)
			v2.set_meta("mat", v2.material_override)
			var v3 := _box(Vector3(b.x - b.w / 2.0 - 0.06, y + b.h * 0.60, b.z + b.d * fz), Vector3(0.12, b.h * 0.16, b.d * 0.13), Color(1.0, 0.72, 0.30))
			v3.material_override = mat_std(Color(1.0, 0.72, 0.30), false, true)
			v3.set_meta("mat", v3.material_override)
		# Cheminée des maisons
		if b.n == "Maison":
			_box(Vector3(b.x + b.w * 0.3, y + b.h + rh * 0.5, b.z + b.d * 0.3), Vector3(0.4, 0.9, 0.4), Color(0.62, 0.32, 0.16))
			_box(Vector3(b.x + b.w * 0.3, y + b.h + rh * 0.5 + 0.5, b.z + b.d * 0.3), Vector3(0.52, 0.1, 0.52), Color(0.52, 0.26, 0.12))
		# b40 : tour d'angle RONDE + toit conique orange (comme l'image)
		if b.n in ["Supermarche", "Auberge", "Armurerie", "Vetements"]:
			var tx: float = b.x + b.w * 0.5 - 0.55
			var tz: float = b.z + b.d * 0.5 - 0.55
			var th: float = b.h + 1.1
			_cyl(Vector3(tx, y + th * 0.5, tz), 0.72, 0.66, th, b.c.lightened(0.06), broot, 10)
			_cone(Vector3(tx, y + th + 0.75, tz), 0.95, 1.5, Color(0.86, 0.42, 0.10), broot, 10)
			_box(Vector3(tx, y + th * 0.62, tz + 0.68), Vector3(0.22, 0.30, 0.06), Color(0.32, 0.21, 0.10), broot)
			_sph(Vector3(tx, y + th + 1.52, tz), 0.09, Color(0.85, 0.70, 0.30), broot)
		# Escalier de la Mairie
		if b.n == "Mairie":
			for step in range(3):
				_box(Vector3(b.x, y + 0.26 - step * 0.1, b.z + b.d / 2.0 + 0.6 + step * 0.35),
					Vector3(b.w * 0.6, 0.12, 0.35), Color(0.60, 0.58, 0.55))
		_parent_capture = null
		batiments.append({"x": b.x, "z": b.z, "w": b.w + 0.5, "d": b.d + 0.5, "root": broot, "fade": false})
		# b18 : emprise ÉLARGIE aux débords de toit (+0.8) pour le camouflage
		camo_objs.append({"root": broot, "x": b.x, "z": b.z, "rx": (b.w + 0.5) / 2.0 + 0.8, "rz": (b.d + 0.5) / 2.0 + 0.8, "fade": false})
		col_boite(b.x, b.z, b.w + 0.5, b.d + 0.5, float(b.h))

	# PNJ (villageois low-poly)
	var pnj = [
		{"x":-8.25,"z":-2.1,"c":Color(0.80,0.20,0.16),"n":"Vendeur"},
		{"x":-8.6,"z":5.65,"c":Color(0.16,0.68,0.36),"n":"Forgeron"},
		{"x":-6.0,"z":-15.0,"c":Color(0.50,0.24,0.62),"n":"Maire"},
		{"x":11.4,"z":-3.35,"c":Color(0.82,0.42,0.08),"n":"Marchand"},
	]
	for d in pnj:
		var y := hauteur_terrain(d.x, d.z)
		var root := Node3D.new()
		root.position = Vector3(d.x, y, d.z)
		add_child(root)
		col_cercle(d.x, d.z, 0.4, 1.8)
		# b30 : PNJ construits COMME le joueur (tête boîte + yeux, torse boîte,
		# bottes, épaules boules) + coiffe et habits propres à chaque métier
		var PEAU := Color(0.93, 0.76, 0.58)
		var jambe_g := Node3D.new(); jambe_g.position = Vector3(-0.11, 0.60, 0); root.add_child(jambe_g)
		var jambe_d := Node3D.new(); jambe_d.position = Vector3(0.11, 0.60, 0); root.add_child(jambe_d)
		for j in [jambe_g, jambe_d]:
			_org("jambe_pnj", make_membre(Vector3(0, -0.02, 0), Vector3(0, -0.48, 0), 0.10, 0.07), Vector3.ZERO, d.c.darkened(0.30), j)
			_box(Vector3(0, -0.53, -0.03), Vector3(0.16, 0.15, 0.24), Color(0.30, 0.22, 0.12), j)
		_org("torse_pnj", make_bboite(Vector3(0.42, 0.50, 0.27)), Vector3(0, 0.95, 0), d.c, root)
		_box(Vector3(0, 0.74, 0), Vector3(0.44, 0.10, 0.29), Color(0.35, 0.24, 0.13), root)
		# b31 : bassin : les jambes touchent le torse (plus de trou)
		_box(Vector3(0, 0.66, 0), Vector3(0.36, 0.20, 0.24), d.c.darkened(0.15), root)
		_sph(Vector3(-0.30, 1.14, 0), 0.10, d.c, root)
		_sph(Vector3(0.30, 1.14, 0), 0.10, d.c, root)
		var bras_g := Node3D.new(); bras_g.position = Vector3(-0.29, 1.12, 0); root.add_child(bras_g)
		var bras_d := Node3D.new(); bras_d.position = Vector3(0.29, 1.12, 0); root.add_child(bras_d)
		for b in [bras_g, bras_d]:
			_org("bras_pnj", make_membre(Vector3(0, -0.02, 0), Vector3(0, -0.34, 0), 0.08, 0.055), Vector3.ZERO, d.c, b)
			# b43 : main avec doigts
			_box(Vector3(0, -0.40, 0), Vector3(0.10, 0.10, 0.12), PEAU, b)
			for fg in range(4):
				_box(Vector3(-0.036 + fg * 0.024, -0.48, -0.01), Vector3(0.02, 0.07, 0.02), PEAU, b)
		var tete := Node3D.new(); tete.position = Vector3(0, 1.38, 0); root.add_child(tete)
		_org("tete_pnj", make_bboite(Vector3(0.28, 0.28, 0.26), 0.05), Vector3.ZERO, PEAU, tete)
		_box(Vector3(-0.07, 0.03, 0.135), Vector3(0.05, 0.05, 0.02), Color(0.10, 0.10, 0.12), tete)
		_box(Vector3(0.07, 0.03, 0.135), Vector3(0.05, 0.05, 0.02), Color(0.10, 0.10, 0.12), tete)
		if d.n == "Forgeron":
			_box(Vector3(0, 0.16, 0), Vector3(0.30, 0.10, 0.28), Color(0.55, 0.15, 0.12), tete)
			_box(Vector3(0, -0.11, 0.11), Vector3(0.18, 0.10, 0.06), Color(0.30, 0.20, 0.14), tete)
			_box(Vector3(0, 0.85, 0.15), Vector3(0.30, 0.55, 0.06), Color(0.45, 0.30, 0.16), root)
			_box(Vector3(0, -0.44, 0.06), Vector3(0.05, 0.34, 0.05), Color(0.45, 0.30, 0.14), bras_d)
			_box(Vector3(0, -0.60, 0.06), Vector3(0.14, 0.12, 0.10), Color(0.35, 0.35, 0.38), bras_d)
			_box(Vector3(0.24, 0.25, 0.65), Vector3(0.30, 0.50, 0.30), Color(0.40, 0.27, 0.13), root)
			_box(Vector3(0.24, 0.72, 0.65), Vector3(0.55, 0.20, 0.35), Color(0.25, 0.25, 0.28), root)
			_box(Vector3(0.24, 0.86, 0.65), Vector3(0.20, 0.08, 0.24), Color(0.35, 0.35, 0.38), root)
		elif d.n == "Vendeur":
			_box(Vector3(0, 0.17, 0), Vector3(0.32, 0.08, 0.30), Color(0.20, 0.30, 0.55), tete)
			_box(Vector3(0, 0.13, 0.17), Vector3(0.22, 0.04, 0.10), Color(0.20, 0.30, 0.55), tete)
			_box(Vector3(0, 0.92, 0.145), Vector3(0.22, 0.42, 0.04), d.c.lightened(0.30), root)
			_box(Vector3(0, 0.45, 0.8), Vector3(1.3, 0.10, 0.7), Color(0.52, 0.36, 0.19), root)
			_box(Vector3(-0.45, 0.25, 0.8), Vector3(0.10, 0.40, 0.10), Color(0.40, 0.27, 0.13), root)
			_box(Vector3(0.45, 0.25, 0.8), Vector3(0.10, 0.40, 0.10), Color(0.40, 0.27, 0.13), root)
			var fruits := [Color(0.85, 0.2, 0.15), Color(0.95, 0.7, 0.1), Color(0.3, 0.6, 0.2), Color(0.6, 0.3, 0.6)]
			for q in range(4):
				_sph(Vector3(-0.4 + q * 0.27, 0.56, 0.8), 0.09, fruits[q], root)
		elif d.n == "Maire":
			_box(Vector3(0, 0.16, 0), Vector3(0.36, 0.04, 0.34), Color(0.15, 0.15, 0.18), tete)
			_box(Vector3(0, 0.30, 0), Vector3(0.24, 0.24, 0.24), Color(0.15, 0.15, 0.18), tete)
			_box(Vector3(-0.15, 0.06, 0), Vector3(0.04, 0.14, 0.20), Color(0.85, 0.85, 0.85), tete)
			_box(Vector3(0.15, 0.06, 0), Vector3(0.04, 0.14, 0.20), Color(0.85, 0.85, 0.85), tete)
			_box(Vector3(0, 0.95, 0.15), Vector3(0.10, 0.50, 0.05), Color(0.85, 0.75, 0.55), root, Vector3(0, 0, deg_to_rad(35)))
			_box(Vector3(0, -0.42, 0.10), Vector3(0.16, 0.22, 0.04), Color(0.92, 0.88, 0.75), bras_g)
		elif d.n == "Marchand":
			_box(Vector3(0, 0.18, -0.02), Vector3(0.30, 0.12, 0.30), Color(0.40, 0.30, 0.20), tete)
			_box(Vector3(0, 0.10, -0.16), Vector3(0.28, 0.16, 0.06), Color(0.40, 0.30, 0.20), tete)
			_box(Vector3(0.16, 0.70, 0.14), Vector3(0.12, 0.14, 0.10), Color(0.42, 0.28, 0.15), root)
			_sph(Vector3(0, 0.80, -0.24), 0.20, Color(0.55, 0.42, 0.25), root)
			_box(Vector3(0.55, 0.22, 0.3), Vector3(0.45, 0.44, 0.45), Color(0.52, 0.36, 0.19), root)
		if d.n == "Vendeur":
			col_boite(d.x, d.z + 0.8, 1.4, 0.8, 0.95)
		elif d.n == "Forgeron":
			col_boite(d.x + 0.24, d.z + 0.65, 0.6, 0.4, 0.95)
		elif d.n == "Marchand":
			col_boite(d.x + 0.55, d.z + 0.3, 0.5, 0.5, 0.5)
		pnj_items.append({"n": d.n, "root": root, "bras_g": bras_g, "bras_d": bras_d, "jambe_g": jambe_g, "jambe_d": jambe_d, "y": y, "t": randf() * 10.0})
		# b32 : étiquettes RETIRÉES (elles traversaient les têtes) ; seul le
		# Maire garde la sienne, remontée au-dessus du haut-de-forme
		if d.n == "Maire":
			var label := Label3D.new()
			label.text = d.n
			label.position = Vector3(0, 1.95, 0)
			label.font_size = 22
			label.pixel_size = 0.005
			label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
			label.modulate = Color(1, 1, 0.55)
			label.outline_size = 8
			label.outline_modulate = Color(0.1, 0.1, 0.1, 0.9)
			root.add_child(label)

# ============================================================
# CHÂTEAU (fond, sur la colline)
# ============================================================
# b25 : parement de briques décalées sur une face de mur (1 maillage coloré).
# udir = droite écran de la face, ndir = normale sortante, v = vertical.
func mur_briques(p0: Vector3, udir: Vector3, ndir: Vector3, length: float, htop: float, base: Color, seedk: int, parent: Node):
	var vdir := Vector3.UP
	var tris := PackedVector3Array()
	var cols := PackedColorArray()
	var rng := RandomNumberGenerator.new()
	rng.seed = seedk
	var bw := 1.6
	var br := 0.55
	var rows := int(htop / br)
	for r in range(rows):
		var dec := 0.0 if r % 2 == 0 else bw * 0.5
		var v0 := 0.18 + r * br
		var v1 := v0 + br - 0.12
		var u := -length / 2.0 + dec
		while u < length / 2.0 - 0.2:
			var u1 := minf(u + bw - 0.14, length / 2.0)
			var k := rng.randi() % 4
			var col := base
			if k == 1:
				col = base.lightened(0.12)
			elif k == 2:
				col = base.darkened(0.10)
			elif k == 3:
				col = base.darkened(0.22)
			var a := p0 + udir * u + vdir * v0 + ndir * 0.07
			var b := p0 + udir * u1 + vdir * v0 + ndir * 0.07
			var c := p0 + udir * u1 + vdir * v1 + ndir * 0.07
			var d := p0 + udir * u + vdir * v1 + ndir * 0.07
			tris.push_back(a); tris.push_back(b); tris.push_back(c)
			tris.push_back(a); tris.push_back(c); tris.push_back(d)
			for q in range(6):
				cols.push_back(col)
			u += bw
	var mi := MeshInstance3D.new()
	mi.mesh = mesh_tris(tris, cols)
	var mat := StandardMaterial3D.new()
	mat.vertex_color_use_as_albedo = true
	mat.roughness = 0.9
	mi.material_override = mat
	parent.add_child(mi)

func creer_chateau():
	var cx := 0.0
	var cz := -78.0
	var y := hauteur_terrain(cx, cz)
	# b26 : palette de la photo de référence : briques terracotta,
	# joints/encadrements crème, toits rouges
	var PIERRE := Color(0.66, 0.65, 0.62)   # joints / mortier gris clair
	var BRIQUE := Color(0.52, 0.52, 0.55)   # briques de pierre grise
	var PIERRE_F := Color(0.70, 0.66, 0.58)
	var TOIT := Color(0.78, 0.26, 0.14)
	var root := Node3D.new()
	root.position = Vector3(cx, y, cz)
	add_child(root)

	# b25 : plateforme SUPPRIMÉE (le grand trait horizontal en bas) :
	# l'enceinte descend jusqu'au sol
	# Enceinte
	_box(Vector3(0, 3.0, -9), Vector3(32, 6, 1.6), PIERRE, root)
	_box(Vector3(-16, 3.0, 0), Vector3(1.6, 6, 20), PIERRE, root)
	_box(Vector3(16, 3.0, 0), Vector3(1.6, 6, 20), PIERRE, root)
	_box(Vector3(-8, 3.0, 9), Vector3(16, 6, 1.6), PIERRE, root)
	_box(Vector3(8, 3.0, 9), Vector3(16, 6, 1.6), PIERRE, root)
	# Créneaux
	for i in range(-15, 16, 3):
		_box(Vector3(i, 6.4, -9), Vector3(1.2, 0.9, 1.8), BRIQUE, root)
		_box(Vector3(i, 6.4, 9), Vector3(1.2, 0.9, 1.8), BRIQUE, root)
	for i in range(-8, 9, 3):
		_box(Vector3(-16, 6.4, i), Vector3(1.8, 0.9, 1.2), BRIQUE, root)
		_box(Vector3(16, 6.4, i), Vector3(1.8, 0.9, 1.2), BRIQUE, root)
	# Porte
	_box(Vector3(-1.6, 1.9, 9), Vector3(0.8, 3.4, 2.0), Color(0.30, 0.18, 0.08), root)
	_box(Vector3(1.6, 1.9, 9), Vector3(0.8, 3.4, 2.0), Color(0.30, 0.18, 0.08), root)
	_box(Vector3(0, 3.75, 9), Vector3(4.0, 0.5, 2.0), Color(0.30, 0.18, 0.08), root)
	# b26 : encadrement crème de la porte (comme la photo)
	_box(Vector3(-2.35, 1.8, 9), Vector3(0.7, 3.6, 2.1), PIERRE.darkened(0.12), root)
	_box(Vector3(2.35, 1.8, 9), Vector3(0.7, 3.6, 2.1), PIERRE.darkened(0.12), root)
	_box(Vector3(0, 4.25, 9), Vector3(5.4, 0.8, 2.1), PIERRE.darkened(0.12), root)
	# b26 : meurtrières sombres sur les murs sud (détail photo)
	for mx in [-11.0, -5.0, 5.0, 11.0]:
		_box(Vector3(mx, 3.4, 9.90), Vector3(0.30, 1.0, 0.12), Color(0.15, 0.12, 0.10), root)
	# b23 : pancarte du château au-dessus de la porte sud
	_box(Vector3(0, 5.75, 10.05), Vector3(3.2, 0.7, 0.12), Color(0.30, 0.19, 0.09), root)
	_box(Vector3(0, 5.75, 10.12), Vector3(3.0, 0.55, 0.08), Color(0.52, 0.36, 0.19), root)
	var lblc := Label3D.new()
	lblc.text = "CHÂTEAU"
	lblc.font_size = 64
	lblc.pixel_size = 0.0075
	lblc.modulate = Color(0.16, 0.10, 0.04)
	lblc.position = Vector3(0, 5.75, 10.17)
	root.add_child(lblc)
	# b25 : murs en BRIQUES APPAREILLÉES (rangées décalées, façon photo) :
	# un seul maillage coloré par face (léger), joints = mur derrière
	mur_briques(Vector3(0, 0, -9.8), Vector3(-1, 0, 0), Vector3(0, 0, -1), 32.0, 5.9, BRIQUE, 11, root)
	mur_briques(Vector3(8, 0, 9.8), Vector3(1, 0, 0), Vector3(0, 0, 1), 16.0, 5.9, BRIQUE, 12, root)
	mur_briques(Vector3(-8, 0, 9.8), Vector3(1, 0, 0), Vector3(0, 0, 1), 16.0, 5.9, BRIQUE, 13, root)
	mur_briques(Vector3(-16.8, 0, 0), Vector3(0, 0, 1), Vector3(-1, 0, 0), 20.0, 5.9, BRIQUE, 14, root)
	mur_briques(Vector3(16.8, 0, 0), Vector3(0, 0, -1), Vector3(1, 0, 0), 20.0, 5.9, BRIQUE, 15, root)
	mur_briques(Vector3(0, 0, -0.5), Vector3(1, 0, 0), Vector3(0, 0, 1), 9.0, 10.8, BRIQUE, 16, root)
	# b27 : faces INTÉRIEURES (cour) appareillées elles aussi
	mur_briques(Vector3(0, 0, -8.2), Vector3(1, 0, 0), Vector3(0, 0, 1), 32.0, 5.9, BRIQUE, 21, root)
	mur_briques(Vector3(8, 0, 8.2), Vector3(-1, 0, 0), Vector3(0, 0, -1), 16.0, 5.9, BRIQUE, 22, root)
	mur_briques(Vector3(-8, 0, 8.2), Vector3(-1, 0, 0), Vector3(0, 0, -1), 16.0, 5.9, BRIQUE, 23, root)
	mur_briques(Vector3(-15.2, 0, 0), Vector3(0, 0, -1), Vector3(1, 0, 0), 20.0, 5.9, BRIQUE, 24, root)
	mur_briques(Vector3(15.2, 0, 0), Vector3(0, 0, 1), Vector3(-1, 0, 0), 20.0, 5.9, BRIQUE, 25, root)
	mur_briques(Vector3(-4.5, 0, -4), Vector3(0, 0, 1), Vector3(-1, 0, 0), 7.0, 10.8, BRIQUE, 26, root)
	mur_briques(Vector3(4.5, 0, -4), Vector3(0, 0, -1), Vector3(1, 0, 0), 7.0, 10.8, BRIQUE, 27, root)
	mur_briques(Vector3(0, 0, -7.5), Vector3(-1, 0, 0), Vector3(0, 0, -1), 9.0, 10.8, BRIQUE, 28, root)

	# Tours d'angle + donjon
	var tours: Array[Vector3] = [Vector3(-16, 0, -9), Vector3(16, 0, -9), Vector3(-16, 0, 9), Vector3(16, 0, 9), Vector3(-6, 0, -4), Vector3(6, 0, -4)]
	for k in range(tours.size()):
		var t := tours[k]
		var hh := 9.0 if k < 4 else 12.0
		var rr := 2.2 if k < 4 else 2.8
		_cyl(Vector3(t.x, (hh + 1.0) / 2.0, t.z), rr, rr * 0.9, hh + 1.0, BRIQUE, root, 8)
		_cone(Vector3(t.x, hh + 1.0 + 2.2, t.z), rr + 0.5, 4.4, TOIT, root, 8)
		# Drapeau
		_cyl(Vector3(t.x, hh + 5.2, t.z), 0.06, 0.06, 1.8, Color(0.35, 0.22, 0.10), root, 6)
		_box(Vector3(t.x + 0.5, hh + 5.8, t.z), Vector3(1.0, 0.6, 0.06), Color(0.85, 0.15, 0.15), root)
	# Donjon central
	_box(Vector3(0, 5.5, -4), Vector3(9, 11, 7), BRIQUE, root)
	_prism(Vector3(0, 12.5, -4), Vector3(8, 3.0, 10), TOIT, root, Vector3(0, deg_to_rad(90), 0))
	# Fenêtres du donjon
	for fx in [-2.5, 0.0, 2.5]:
		_box(Vector3(fx, 7.5, -0.4), Vector3(0.8, 1.6, 0.3), Color(0.25, 0.30, 0.42), root)
	# Collisions du château (coordonnées monde) — porte sud laissée passable
	col_boite(0, -87, 32, 1.6, 6.0)
	col_boite(-16, -78, 1.6, 20, 6.0)
	col_boite(16, -78, 1.6, 20, 6.0)
	col_boite(-9.1, -69, 13.8, 1.6, 6.0)
	col_boite(9.1, -69, 13.8, 1.6, 6.0)
	col_cercle(-16, -87, 2.3, 10.0)
	col_cercle(16, -87, 2.3, 10.0)
	col_cercle(-16, -69, 2.3, 10.0)
	col_cercle(16, -69, 2.3, 10.0)
	col_cercle(-6, -82, 2.9, 13.0)
	col_cercle(6, -82, 2.9, 13.0)
	col_boite(0, -82, 9, 7, 11.0)
	col_boite(0, -69, 4.4, 1.6, 5.0)   # b15 : porte sud BLOQUÉE, on ne peut plus entrer

# ============================================================
# FONTAINE / ARBRES / PROPS / NUAGE
# ============================================================
func creer_fontaine(x: float, z: float):
	var y := hauteur_terrain(x, z)
	col_cercle(x, z, 2.6, 0.95)
	# b31 : LES DEUX SEULS colliders du haut, comme demandé : le CYLINDRE
	# central (colonne + pinacle) et le PLATEAU du haut (vasque + eau)
	col_cercle(x, z, 0.6, 4.0)
	col_cercle(x, z, 1.2, 3.2)
	# b14 : thème de la fontaine de l'image de référence — pierre beige claire,
	# eau turquoise, filets d'eau blancs.
	var PIERRE_F := Color(0.82, 0.76, 0.62)
	# Degré extérieur (assise de blocs) + bassin principal + eau
	_cyl(Vector3(x, y + 0.12, z), 2.95, 2.75, 0.24, PIERRE_F.darkened(0.08), self, 12)
	_cyl(Vector3(x, y + 0.45, z), 2.5, 2.3, 0.9, PIERRE_F, self, 12)
	_cyl(Vector3(x, y + 0.85, z), 2.1, 2.1, 0.25, Color(0.13, 0.62, 0.66), self, 12)
	# Colonne centrale
	_cyl(Vector3(x, y + 1.7, z), 0.32, 0.26, 2.2, PIERRE_F, self, 8)
	# Vasque haute + orbe d'eau + filets d'eau retombant dans le bassin
	_cyl(Vector3(x, y + 2.72, z), 0.95, 0.5, 0.24, PIERRE_F, self, 10)
	_cyl(Vector3(x, y + 2.85, z), 0.82, 0.82, 0.06, Color(0.18, 0.70, 0.72), self, 10)
	# b15 : plus de BOULE VOLANTE : pinacle de pierre pointu (comme l'image)
	_cyl(Vector3(x, y + 3.0, z), 0.16, 0.12, 0.3, PIERRE_F, self, 8)
	_cone(Vector3(x, y + 3.32, z), 0.14, 0.34, PIERRE_F, self, 8)
	for k in range(4):
		var a := float(k) * TAU / 4.0 + 0.4
		_cyl(Vector3(x + cos(a) * 0.88, y + 1.82, z + sin(a) * 0.88), 0.05, 0.075, 1.85, Color(0.85, 0.95, 1.0), self, 5)

func creer_arbres():
	var pins := [
		# b22 : 5 sapins MAXI en ville (espacés, hors route et bâtiments) ;
		# ceux qui étaient SUR la route ([0,15], [0,-15], [0,-50], [5,29]) retirés.
		[-14, -14], [14, -9], [-17, 7], [14, 11], [-9, 13],
		[-26, 18], [26, 18], [-26, -20], [26, -20], [-34, 0], [34, 0], [-12, 26],
		[12, 26], [-40, 30], [40, 30], [-45, -35], [45, -35], [-55, 10], [55, 10],
		[-60, -50], [60, -50], [-70, 40], [70, 40], [-20, -45], [20, -45],
	]
	for p in pins:
		creer_pin(p[0], p[1])
	# Quelques arbres ronds
	for p in [[-14, 7], [14, 7], [-24, -8], [24, 8], [-30, 30], [30, -30]]:
		creer_arbre_rond(p[0], p[1])

func creer_pin(x: float, z: float):
	var y := hauteur_terrain(x, z)
	col_cercle(x, z, 0.4, 2.2)   # tronc du pin (collider d'origine restauré)
	var s := randf_range(0.8, 1.5)
	var root := Node3D.new()
	root.position = Vector3(x, y, z)
	root.scale = Vector3.ONE * s
	add_child(root)
	camo_objs.append({"root": root, "x": x, "z": z, "rx": 1.5 * s, "rz": 1.5 * s, "fade": false})
	# b15 : TRONC bien visible (épais, haut) puis feuillage au-dessus
	_cyl(Vector3(0, 1.3, 0), 0.34, 0.24, 2.6, Color(0.50, 0.34, 0.17), root, 8)
	_cone(Vector3(0, 0.25, 0), 0.55, 0.5, Color(0.50, 0.34, 0.17), root, 8)
	for k in range(4):
		var a := float(k) * TAU / 4.0
		_box(Vector3(cos(a) * 0.30, 1.3, sin(a) * 0.30), Vector3(0.10, 2.4, 0.10), Color(0.33, 0.21, 0.10), root, Vector3(0, -a, 0))
	_cone(Vector3(0, 3.3, 0), 1.35, 2.4, Color(0.13, 0.42, 0.16), root, 7)
	_cone(Vector3(0, 4.4, 0), 1.05, 2.1, Color(0.16, 0.50, 0.19), root, 7)
	_cone(Vector3(0, 5.4, 0), 0.72, 1.8, Color(0.20, 0.58, 0.22), root, 7)

func creer_arbre_rond(x: float, z: float):
	var y := hauteur_terrain(x, z)
	col_cercle(x, z, 0.45, 2.2)
	var root := Node3D.new()
	root.position = Vector3(x, y, z)
	add_child(root)
	camo_objs.append({"root": root, "x": x, "z": z, "rx": 1.9, "rz": 1.9, "fade": false})
	# b15 : tronc épais + feuillage remonté (ne trempe plus dans les maisons)
	_cyl(Vector3(0, 1.8, 0), 0.36, 0.26, 3.6, Color(0.50, 0.34, 0.17), root, 8)
	_cone(Vector3(0, 0.3, 0), 0.6, 0.6, Color(0.50, 0.34, 0.17), root, 8)
	for k in range(4):
		var a := float(k) * TAU / 4.0 + 0.4
		_box(Vector3(cos(a) * 0.32, 1.8, sin(a) * 0.32), Vector3(0.11, 3.2, 0.11), Color(0.33, 0.21, 0.10), root, Vector3(0, -a, 0))
	_facette(Vector3(0, 4.2, 0), Color(0.22, 0.58, 0.20), root, Vector3(2.4, 2.0, 2.4))
	_facette(Vector3(0.7, 3.6, 0.4), Color(0.18, 0.50, 0.17), root, Vector3(1.4, 1.2, 1.4))

func creer_props():
	# Lampadaires alignés le long de la route du village (plus au milieu de la route !)
	poser_lampadaires_route()
	# Barils + caisses près de l'auberge et du supermarché
	for p in [[8.2, 5.6], [8.7, 6.2], [-7.0, -1.4], [-7.7, -1.9], [12.4, 1.0]]:
		var y := hauteur_terrain(p[0], p[1])
		_cyl(Vector3(p[0], y + 0.45, p[1]), 0.34, 0.30, 0.9, Color(0.55, 0.36, 0.18), self, 10)
		_cyl(Vector3(p[0], y + 0.62, p[1]), 0.36, 0.36, 0.1, Color(0.30, 0.28, 0.28), self, 10)
		col_cercle(p[0], p[1], 0.45, 0.95)
	for p in [[-14.2, -6.0], [12.6, 4.6], [-12.4, 4.8]]:
		var y := hauteur_terrain(p[0], p[1])
		_box(Vector3(p[0], y + 0.35, p[1]), Vector3(0.7, 0.7, 0.7), Color(0.62, 0.45, 0.24))
		_box(Vector3(p[0], y + 0.36, p[1]), Vector3(0.72, 0.12, 0.72), Color(0.48, 0.34, 0.17))
		col_boite(p[0], p[1], 0.8, 0.8, 0.75)
	# Clôtures le long des routes
	for i in range(-6, 7):
		if abs(i) < 2: continue
		var y := hauteur_terrain(i * 2.4, 4.6)
		_box(Vector3(i * 2.4, y + 0.45, 4.6), Vector3(0.1, 0.9, 0.1), Color(0.52, 0.36, 0.18))
		_box(Vector3(i * 2.4, y + 0.7, 4.6), Vector3(2.4, 0.09, 0.07), Color(0.58, 0.41, 0.21))
	col_boite(-9.6, 4.6, 12.0, 0.25, 0.85)
	col_boite(9.6, 4.6, 12.0, 0.25, 0.85)
	# Panneaux / bannières (comme le screen)
	creer_banniere(-5.5, -3.0, Color(0.16, 0.30, 0.62))
	creer_banniere(6.0, 8.0, Color(0.55, 0.16, 0.16))
	creer_panneau(-4.0, 8.5)
	creer_panneau(5.0, -8.5)

# ============================================================
# LAMPADAIRES LE LONG DE LA ROUTE DU VILLAGE
# ============================================================
func creer_lampadaire(x: float, z: float):
	var y := hauteur_terrain(x, z)
	var root := Node3D.new()
	root.position = Vector3(x, y, z)
	add_child(root)
	col_cercle(x, z, 0.25, 3.0)
	_box(Vector3(0, 0.05, 0), Vector3(0.5, 0.12, 0.5), Color(0.20, 0.20, 0.22), root)
	_box(Vector3(0, 1.5, 0), Vector3(0.14, 3.0, 0.14), Color(0.16, 0.16, 0.18), root)
	_box(Vector3(0, 3.05, 0), Vector3(0.5, 0.1, 0.1), Color(0.16, 0.16, 0.18), root)
	var lan := _box(Vector3(0.22, 2.75, 0), Vector3(0.26, 0.4, 0.26), Color(1.0, 0.80, 0.30), root)
	lan.material_override = mat_std(Color(1.0, 0.80, 0.30), false, true)
	_cone(Vector3(0.22, 3.05, 0), 0.2, 0.25, Color(0.16, 0.16, 0.18), root, 4)

func poser_lampadaires_route():
	var dist_accum := 0.0
	var prochain := 6.0
	var cote := 1.0
	var n := chemin_lisse.size()
	for i in range(n - 1):
		var p: Vector2 = chemin_lisse[i]
		dist_accum += p.distance_to(chemin_lisse[i + 1])
		if p.length() > 24.0:
			continue    # hors du village : pas de lampadaire
		if dist_accum < prochain:
			continue
		prochain = dist_accum + 13.0
		cote = -cote    # en alternance d'un côté puis de l'autre
		var tang := (chemin_lisse[i + 1] - p).normalized()
		var lx := p.x + (-tang.y) * 3.4 * cote
		var lz := p.y + tang.x * 3.4 * cote
		var pose := true
		for b in batiments:
			if absf(lx - float(b.x)) < float(b.w) * 0.5 + 1.0 and absf(lz - float(b.z)) < float(b.d) * 0.5 + 1.0:
				pose = false
				break
		if pose:
			creer_lampadaire(lx, lz)

# ============================================================
# CLÔTURE DU VILLAGE — enceinte complète + portails sur la route
# (zone interdite aux monstres : voir dans_village / update_ennemis)
# ============================================================
func creer_cloture_village():
	var BOIS := Color(0.52, 0.36, 0.18)
	var BOIS_CLAIR := Color(0.58, 0.41, 0.21)
	var n := 96
	var step := TAU / float(n)
	var dernier_angle := 1e9
	var en_trou := false
	var a0_trou := 0.0
	var trous := []
	for i in range(n + 1):
		var a := float(i) * step
		var x := cos(a) * VILLAGE_R
		var z := sin(a) * VILLAGE_R
		# Ouverture uniquement là où la route traverse l'enceinte
		if dist_chemin(Vector2(x, z)) < 3.0:
			if not en_trou:
				en_trou = true
				a0_trou = dernier_angle
			dernier_angle = 1e9
			continue
		if en_trou:
			en_trou = false
			if a0_trou < 1e8:
				trous.append({"a0": a0_trou, "a1": a})
		dernier_angle = a
	# b40 : PALISSADE SERRÉE À POINTES (comme l'image) : ~640 piquets en UN
	# seul MultiMesh (1 draw call), trous laissés là où passe la route.
	if not _mesh_cache.has("piquet"):
		_mesh_cache["piquet"] = make_piquet()
	var mm := MultiMesh.new()
	mm.transform_format = MultiMesh.TRANSFORM_3D
	mm.mesh = _mesh_cache["piquet"]
	var pts := []
	var np := 820
	for i in range(np):
		var a := float(i) / float(np) * TAU
		var px := cos(a) * VILLAGE_R
		var pz := sin(a) * VILLAGE_R
		if dist_chemin(Vector2(px, pz)) < 3.4:
			continue
		pts.append([px, pz, hauteur_terrain(px, pz), a])
	mm.instance_count = pts.size()
	var rngp := RandomNumberGenerator.new()
	rngp.seed = 4242
	for i in range(pts.size()):
		var p = pts[i]
		var sc: float = rngp.randf_range(0.90, 1.14)
		var yaw: float = -p[3] + rngp.randf_range(-0.05, 0.05)
		var bas := Basis(Vector3(0, 1, 0), yaw).scaled(Vector3(1, sc, 1))
		mm.set_instance_transform(i, Transform3D(bas, Vector3(p[0], p[2] - 0.05, p[1])))
	var mmi := MultiMeshInstance3D.new()
	mmi.multimesh = mm
	var matp := mat_std(BOIS)
	matp.cull_mode = BaseMaterial3D.CULL_DISABLED
	mmi.material_override = matp
	mmi.set_meta("mat", matp)
	add_child(mmi)

	# Portails : grands poteaux + linteau + lanterne au-dessus de la route
	for trou in trous:
		var a0: float = trou.a0
		var a1: float = trou.a1
		for ga in [a0, a1]:
			var ag: float = ga
			var gx := cos(ag) * VILLAGE_R
			var gz := sin(ag) * VILLAGE_R
			var gy := hauteur_terrain(gx, gz)
			# b33 : porte DEUX FOIS plus haute
			_box(Vector3(gx, gy + 2.6, gz), Vector3(0.30, 5.2, 0.30), BOIS.darkened(0.1))
			_box(Vector3(gx, gy + 5.26, gz), Vector3(0.40, 0.14, 0.40), BOIS_CLAIR)
		var d01 := Vector2(cos(a1) - cos(a0), sin(a1) - sin(a0))
		var am := (a0 + a1) * 0.5
		var mx2 := cos(am) * VILLAGE_R
		var mz2 := sin(am) * VILLAGE_R
		var my2 := hauteur_terrain(mx2, mz2)
		var ry2 := atan2(-d01.y, d01.x)
		var long2 := d01.length() * VILLAGE_R + 0.2
		_box(Vector3(mx2, my2 + 5.2, mz2), Vector3(long2, 0.18, 0.16), BOIS_CLAIR, null, Vector3(0, ry2, 0))
		var lan := _box(Vector3(mx2, my2 + 4.86, mz2), Vector3(0.24, 0.34, 0.24), Color(1.0, 0.80, 0.30))
		lan.material_override = mat_std(Color(1.0, 0.80, 0.30), false, true)
		_cone(Vector3(mx2, my2 + 5.10, mz2), 0.19, 0.22, Color(0.16, 0.16, 0.18), null, 4)
		# b33 : pancarte AU MILIEU de la porte, nom de la ville : LIBREVIES
		_box(Vector3(mx2, my2 + 4.35, mz2), Vector3(2.6, 0.7, 0.10), Color(0.30, 0.19, 0.09), null, Vector3(0, ry2, 0))
		_box(Vector3(mx2, my2 + 4.35, mz2), Vector3(2.4, 0.55, 0.12), Color(0.52, 0.36, 0.19), null, Vector3(0, ry2, 0))
		var radg := Vector2(mx2, mz2).normalized()
		var yawg := atan2(radg.x, radg.y)
		for cote in [1.0, -1.0]:
			var lv := Label3D.new()
			lv.text = "LIBREVIES"
			lv.font_size = 64
			lv.pixel_size = 0.0045
			lv.modulate = Color(0.16, 0.10, 0.04)
			lv.rotation.y = yawg if cote > 0.0 else yawg + PI
			lv.position = Vector3(mx2 + radg.x * 0.07 * cote, my2 + 4.35, mz2 + radg.y * 0.07 * cote)
			add_child(lv)
		# Garde posté à côté du portail (côté village)
		var gx2 := cos(a0) * VILLAGE_R * 0.90
		var gz2 := sin(a0) * VILLAGE_R * 0.90
		var garde := creer_garde(gx2, gz2)
		portes.append({"x": mx2, "z": mz2, "garde": garde, "cd": 0.0, "lunge": 0.0, "tx": 0.0, "tz": 0.0, "bx": garde.global_position.x, "bz": garde.global_position.z, "pt": 0.0, "po": 0.0, "face": 0.0})

# ============================================================
# GARDE DU VILLAGE (low-poly, hallebarde) — protège les portails
# ============================================================
func creer_garde(x: float, z: float) -> Node3D:
	var y := hauteur_terrain(x, z)
	var root := Node3D.new()
	root.position = Vector3(x, y, z)
	add_child(root)
	var ACIER := Color(0.45, 0.50, 0.58)
	var TUNIQUE := Color(0.20, 0.32, 0.55)
	# b31 : lifting du garde — même gabarit que le joueur, en ARMURE complète
	var PEAU := Color(0.93, 0.76, 0.58)
	var jambe_g := Node3D.new(); jambe_g.name = "jg"; jambe_g.position = Vector3(-0.12, 0.62, 0); root.add_child(jambe_g)
	var jambe_d := Node3D.new(); jambe_d.name = "jd"; jambe_d.position = Vector3(0.12, 0.62, 0); root.add_child(jambe_d)
	for j in [jambe_g, jambe_d]:
		_org("jambe_gd", make_membre(Vector3(0, -0.02, 0), Vector3(0, -0.50, 0), 0.105, 0.075), Vector3.ZERO, ACIER.darkened(0.30), j)   # jambards
		_box(Vector3(0, -0.55, -0.03), Vector3(0.17, 0.16, 0.26), ACIER.darkened(0.45), j)  # solerets
	_box(Vector3(0, 0.70, 0), Vector3(0.38, 0.20, 0.26), ACIER.darkened(0.20), root)  # bassin
	_org("torse_gd", make_bboite(Vector3(0.44, 0.52, 0.28)), Vector3(0, 0.98, 0), ACIER, root)  # cuirasse
	_prism(Vector3(0, 0.64, 0), Vector3(0.48, 0.26, 0.34), TUNIQUE, root)   # b40 : jupe tunique
	_box(Vector3(0, 0.80, 0), Vector3(0.46, 0.10, 0.30), Color(0.30, 0.22, 0.12), root)  # ceinturon
	_box(Vector3(0, 1.02, 0.15), Vector3(0.30, 0.30, 0.05), ACIER.lightened(0.25), root)  # plastron
	_box(Vector3(0, 1.0, 0), Vector3(0.46, 0.09, 0.30), Color(0.75, 0.15, 0.15), root, Vector3(0, 0, deg_to_rad(35)))  # baudrier
	_sph(Vector3(-0.33, 1.18, 0), 0.13, ACIER.lightened(0.10), root)        # spallières
	_sph(Vector3(0.33, 1.18, 0), 0.13, ACIER.lightened(0.10), root)
	var bras_g := Node3D.new(); bras_g.position = Vector3(-0.31, 1.16, 0); root.add_child(bras_g)
	var bras_d := Node3D.new(); bras_d.position = Vector3(0.31, 1.16, 0); root.add_child(bras_d)
	for b in [bras_g, bras_d]:
		_org("bras_gd", make_membre(Vector3(0, -0.02, 0), Vector3(0, -0.36, 0), 0.085, 0.06), Vector3.ZERO, ACIER.darkened(0.10), b)   # brassards
		# b43 : main gantée avec doigts
		_box(Vector3(0, -0.42, 0), Vector3(0.11, 0.11, 0.13), PEAU, b)
		for fg in range(4):
			_box(Vector3(-0.039 + fg * 0.026, -0.51, -0.01), Vector3(0.021, 0.08, 0.021), PEAU.darkened(0.10), b)
	var tete := Node3D.new()
	tete.position = Vector3(0, 1.42, 0)
	root.add_child(tete)
	_org("tete_gd", make_bboite(Vector3(0.30, 0.30, 0.28), 0.05), Vector3.ZERO, PEAU, tete)  # visage
	_box(Vector3(-0.07, 0.03, 0.145), Vector3(0.05, 0.05, 0.02), Color(0.10, 0.10, 0.12), tete)
	_box(Vector3(0.07, 0.03, 0.145), Vector3(0.05, 0.05, 0.02), Color(0.10, 0.10, 0.12), tete)
	_box(Vector3(0, 0.17, 0), Vector3(0.34, 0.16, 0.32), ACIER, tete)       # casque
	_box(Vector3(0, 0.07, 0.16), Vector3(0.26, 0.05, 0.06), ACIER.darkened(0.25), tete)  # visière
	_box(Vector3(0, 0.05, 0.155), Vector3(0.045, 0.17, 0.05), ACIER, tete)  # b40 : nasal
	_box(Vector3(0, 0.32, 0), Vector3(0.06, 0.18, 0.32), Color(0.75, 0.15, 0.15), tete)  # cimier rouge
	# b15 : hallebarde ENTIÈREMENT visible : manche clair épais tenu de biais,
	# fer + croc latéral + talon (avant : fin manche sombre = on ne voyait que le haut).
	var halle := Node3D.new()
	halle.position = Vector3(0.34, 1.25, 0.05)
	halle.rotation.z = deg_to_rad(0)   # b19 : verticale, prolongement du bras
	root.add_child(halle)
	_cyl(Vector3(0, 0, 0), 0.04, 0.035, 2.5, Color(0.86, 0.70, 0.44), halle, 8)
	_cyl(Vector3(0, 0.55, 0), 0.055, 0.055, 0.05, Color(0.22, 0.22, 0.26), halle, 8)
	# b40 : LANCE comme l'image : barre transversale + fer en feuille (losange)
	_box(Vector3(0, 1.02, 0), Vector3(0.22, 0.05, 0.06), Color(0.70, 0.72, 0.78), halle)
	_cyl(Vector3(0, 1.10, 0), 0.0, 0.085, 0.18, Color(0.84, 0.86, 0.90), halle, 6)
	_cone(Vector3(0, 1.34, 0), 0.085, 0.36, Color(0.84, 0.86, 0.90), halle, 6)
	_box(Vector3(0, -1.20, 0), Vector3(0.10, 0.14, 0.10), Color(0.45, 0.50, 0.58), halle)
	# b34 : étiquette « Garde » RETIRÉE (au-dessus du casque)
	col_cercle(x, z, 0.4, 1.8)
	return root

func creer_banniere(x: float, z: float, col: Color):
	var y := hauteur_terrain(x, z)
	var root := Node3D.new()
	root.position = Vector3(x, y, z)
	add_child(root)
	col_cercle(x, z, 0.22, 3.2)
	_box(Vector3(0, 1.6, 0), Vector3(0.16, 3.2, 0.16), Color(0.40, 0.27, 0.13), root)
	_box(Vector3(0, 3.15, 0), Vector3(1.5, 0.14, 0.14), Color(0.40, 0.27, 0.13), root)
	_box(Vector3(0, 2.35, 0.02), Vector3(1.1, 1.5, 0.06), col, root)
	# Emblème : épées croisées
	_box(Vector3(0, 2.4, 0.08), Vector3(0.7, 0.12, 0.05), Color(0.95, 0.85, 0.35), root, Vector3(0, 0, deg_to_rad(45)))
	_box(Vector3(0, 2.4, 0.08), Vector3(0.7, 0.12, 0.05), Color(0.95, 0.85, 0.35), root, Vector3(0, 0, deg_to_rad(-45)))

func creer_panneau(x: float, z: float):
	var y := hauteur_terrain(x, z)
	var root := Node3D.new()
	root.position = Vector3(x, y, z)
	root.rotation.y = randf_range(0, TAU)
	add_child(root)
	col_cercle(x, z, 0.18, 2.0)
	_box(Vector3(0, 0.9, 0), Vector3(0.12, 1.8, 0.12), Color(0.42, 0.28, 0.14), root)
	_box(Vector3(0, 1.7, 0), Vector3(1.3, 0.5, 0.08), Color(0.62, 0.45, 0.24), root)
	_box(Vector3(0.2, 1.35, 0), Vector3(1.0, 0.4, 0.08), Color(0.55, 0.39, 0.20), root, Vector3(0, 0, deg_to_rad(20)))

func creer_nuages():
	var rng := RandomNumberGenerator.new()
	rng.seed = 21
	for i in range(10):
		var root := Node3D.new()
		root.position = Vector3(rng.randf_range(-WORLD, WORLD), rng.randf_range(24, 34), rng.randf_range(-WORLD, -20))
		add_child(root)
		nuages.push_back(root)
		var n := rng.randi_range(3, 5)
		for k in range(n):
			var w := rng.randf_range(2.5, 6.0)
			var mi := _box(Vector3(rng.randf_range(-3, 3), rng.randf_range(-0.5, 0.8), rng.randf_range(-1.5, 1.5)),
				Vector3(w, rng.randf_range(1.0, 1.8), rng.randf_range(1.6, 2.6)),
				Color(1, 1, 1), root)
			mi.material_override = mat_std(Color(1, 1, 1), true)
			mi.cast_shadow = GeometryInstance3D.SHADOW_CASTING_SETTING_OFF

# ============================================================
# HÉROS (low-poly, primitives) — remplace perso_voxel.glb
# ============================================================
func creer_joueur():
	player_node = Node3D.new()
	player_node.name = "Joueur"
	player_node.position = Vector3(0, 0, 6)
	add_child(player_node)

	var PEAU := Color(0.93, 0.76, 0.58)
	var TUNIQUE := Color(0.16, 0.35, 0.72)
	var TUNIQUE_F := Color(0.12, 0.27, 0.58)
	var CUIR := Color(0.42, 0.28, 0.15)
	var CHEVEUX := Color(0.35, 0.22, 0.11)
	var BOTTE := Color(0.33, 0.22, 0.12)

	# Jambes (pivots)
	jambe_gauche = Node3D.new(); jambe_gauche.position = Vector3(-0.12, 0.62, 0); player_node.add_child(jambe_gauche)
	jambe_droite = Node3D.new(); jambe_droite.position = Vector3(0.12, 0.62, 0); player_node.add_child(jambe_droite)
	var PANT := Color(0.36, 0.26, 0.16)   # b42 : pantalon brun (image)
	for j in [jambe_gauche, jambe_droite]:
		_org("jambe_p", make_membre(Vector3(0, -0.02, 0), Vector3(0, -0.50, 0), 0.105, 0.075), Vector3.ZERO, PANT, j)
		_box(Vector3(0, -0.44, -0.01), Vector3(0.19, 0.10, 0.28), BOTTE.lightened(0.18), j)  # revers de botte
		_box(Vector3(0, -0.55, -0.03), Vector3(0.17, 0.16, 0.26), BOTTE, j)
		_box(Vector3(0, -0.62, -0.04), Vector3(0.18, 0.05, 0.28), BOTTE.darkened(0.30), j)  # semelle

	# Torse + tunique
	_org("torse_p", make_bboite(Vector3(0.44, 0.52, 0.28)), Vector3(0, 0.98, 0), TUNIQUE, player_node)
	_prism(Vector3(0, 0.66, 0), Vector3(0.52, 0.26, 0.36), TUNIQUE, player_node)
	_box(Vector3(0, 0.80, 0), Vector3(0.46, 0.10, 0.30), CUIR, player_node)
	_box(Vector3(0, 0.80, 0.16), Vector3(0.09, 0.09, 0.03), Color(0.85, 0.70, 0.25), player_node)  # b42 : boucle dorée
	_box(Vector3(0, 1.25, 0), Vector3(0.30, 0.09, 0.24), TUNIQUE_F, player_node)  # b42 : col
	_box(Vector3(0, 0.55, 0), Vector3(0.50, 0.07, 0.35), TUNIQUE_F, player_node)  # b42 : ourlet de tunique
	# Baudrier
	_box(Vector3(0, 1.0, 0), Vector3(0.46, 0.09, 0.30), CUIR, player_node, Vector3(0, 0, deg_to_rad(35)))
	# Épaules
	# b22 : boules d'épaule qui EFFLEURENT le torse (x 0.33) => bras écartés
	_sph(Vector3(-0.33, 1.18, 0), 0.11, TUNIQUE, player_node)
	_sph(Vector3(0.33, 1.18, 0), 0.11, TUNIQUE, player_node)

	# Bras (pivots)
	bras_gauche = Node3D.new(); bras_gauche.position = Vector3(-0.31, 1.16, 0); player_node.add_child(bras_gauche)
	bras_droit = Node3D.new(); bras_droit.position = Vector3(0.31, 1.16, 0); player_node.add_child(bras_droit)
	for b in [bras_gauche, bras_droit]:
		_org("bras_p", make_membre(Vector3(0, -0.02, 0), Vector3(0, -0.36, 0), 0.085, 0.06), Vector3.ZERO, TUNIQUE, b)
		_box(Vector3(0, -0.335, 0), Vector3(0.115, 0.07, 0.115), TUNIQUE_F, b)  # b42 : manchette
		# b43 : MAIN avec DOIGTS : paume + 4 doigts + pouce devant
		_box(Vector3(0, -0.42, 0), Vector3(0.11, 0.11, 0.13), CUIR, b)
		for fg in range(4):
			_box(Vector3(-0.039 + fg * 0.026, -0.51, -0.01), Vector3(0.021, 0.08, 0.021), CUIR.lightened(0.10), b)
		_box(Vector3(0, -0.46, -0.09), Vector3(0.022, 0.06, 0.022), CUIR.lightened(0.10), b)

	# Tête + cheveux piquants
	var tete := Node3D.new()
	tete.position = Vector3(0, 1.42, 0)
	player_node.add_child(tete)
	_org("tete_p", make_bboite(Vector3(0.30, 0.30, 0.28), 0.05), Vector3.ZERO, PEAU, tete)
	_box(Vector3(0, 0.16, 0.02), Vector3(0.32, 0.14, 0.30), CHEVEUX, tete)
	_box(Vector3(0, 0.10, -0.14), Vector3(0.30, 0.12, 0.06), CHEVEUX, tete)
	_prism(Vector3(-0.09, 0.28, 0.04), Vector3(0.12, 0.18, 0.12), CHEVEUX, tete, Vector3(0, 0, deg_to_rad(15)))
	_prism(Vector3(0.07, 0.30, -0.02), Vector3(0.12, 0.20, 0.12), CHEVEUX, tete, Vector3(0, 0, deg_to_rad(-12)))
	_prism(Vector3(0.0, 0.27, 0.10), Vector3(0.10, 0.16, 0.10), CHEVEUX, tete, Vector3(0, 0, deg_to_rad(5)))
	# Yeux
	_box(Vector3(-0.07, 0.02, -0.145), Vector3(0.045, 0.05, 0.02), Color(0.12, 0.12, 0.12), tete)
	_box(Vector3(0.07, 0.02, -0.145), Vector3(0.045, 0.05, 0.02), Color(0.12, 0.12, 0.12), tete)
	# b42 : visage fin : sourcils, nez, bouche, oreilles
	_box(Vector3(-0.07, 0.075, -0.145), Vector3(0.06, 0.02, 0.02), CHEVEUX, tete)
	_box(Vector3(0.07, 0.075, -0.145), Vector3(0.06, 0.02, 0.02), CHEVEUX, tete)
	_box(Vector3(0, -0.02, -0.15), Vector3(0.04, 0.06, 0.03), PEAU.lightened(0.05), tete)
	_box(Vector3(0, -0.09, -0.145), Vector3(0.07, 0.025, 0.02), Color(0.55, 0.30, 0.25), tete)
	_box(Vector3(-0.155, 0.0, 0.0), Vector3(0.03, 0.07, 0.05), PEAU, tete)
	_box(Vector3(0.155, 0.0, 0.0), Vector3(0.03, 0.07, 0.05), PEAU, tete)
	# b42 : frange devant + mèches sur les côtés (cheveux en couches)
	_box(Vector3(0, 0.115, -0.145), Vector3(0.28, 0.07, 0.05), CHEVEUX, tete)
	_box(Vector3(-0.15, 0.05, 0.02), Vector3(0.04, 0.16, 0.22), CHEVEUX, tete)
	_box(Vector3(0.15, 0.05, 0.02), Vector3(0.04, 0.16, 0.22), CHEVEUX, tete)

	# Marteau (main droite)
	var marteau := Node3D.new()
	marteau.position = Vector3(0, -0.44, 0)
	bras_droit.add_child(marteau)
	# b19 : manche = PROLONGEMENT DU BRAS : pris dans le poing, tête au bout
	# en bas, sommet du manche dans la main. Bras droit légèrement écarté du
	# corps (rotation.z -10°) pour que le manche se découpe sur le décor.
	# b20 : manche DANS le poing, bois très clair + bagues sombres de contraste
	# b22 : manche commence JUSTE SOUS la main (tient dans le poing) ;
	# tête DIVISÉE PAR 2 et remontée à la cheville (~0.19 m du sol).
	_cyl(Vector3(0, -0.25, 0), 0.032, 0.028, 0.40, Color(0.86, 0.70, 0.44), marteau, 8)
	_cyl(Vector3(0, -0.08, 0), 0.045, 0.045, 0.05, Color(0.22, 0.22, 0.26), marteau, 8)
	_box(Vector3(0, -0.55, 0), Vector3(0.20, 0.20, 0.26), Color(0.55, 0.55, 0.58), marteau)
	_box(Vector3(0, -0.55, 0), Vector3(0.22, 0.09, 0.28), Color(0.35, 0.24, 0.12), marteau)

	# Lumière douce autour du joueur
	var light := OmniLight3D.new()
	light.position = Vector3(0, 3, 0)
	light.light_color = Color(1, 0.95, 0.85)
	light.light_energy = 0.5
	light.omni_range = 12
	player_node.add_child(light)

# ============================================================
# ENNEMIS (rats / souris / araignées)
# ============================================================
func creer_ennemis():
	var zones = [
		{"cx": 25, "cz": 20, "n": 3, "t": "souris"},
		{"cx": -25, "cz": -20, "n": 3, "t": "souris"},
		{"cx": 30, "cz": -25, "n": 3, "t": "rat"},
		{"cx": -30, "cz": 25, "n": 3, "t": "rat"},
		{"cx": 15, "cz": 35, "n": 3, "t": "araignee"},
		{"cx": -18, "cz": -38, "n": 3, "t": "araignee"},
	]
	for zone in zones:
		for i in range(zone.n):
			var x: float = zone.cx + randf_range(-8, 8)
			var z: float = zone.cz + randf_range(-8, 8)
			# Naissance interdite dans le village protégé
			if dans_village(x, z):
				var pousse := Vector2(x, z)
				if pousse.length() < 0.001:
					pousse = Vector2(1, 0)
				pousse = pousse.normalized() * (VILLAGE_R + 7.0)
				x = pousse.x
				z = pousse.y
			var root := Node3D.new()
			root.position = Vector3(x, hauteur_terrain(x, z), z)
			add_child(root)
			var pv := 25
			if zone.t == "rat": pv = 50
			if zone.t == "araignee": pv = 75
			if zone.t == "araignee":
				_construire_araignee(root)
			else:
				_construire_rat(root, zone.t == "rat")
			# Barre de vie au-dessus
			var barre := Node3D.new()
			barre.position = Vector3(0, 1.1 if zone.t == "araignee" else 0.75, 0)
			barre.visible = false
			root.add_child(barre)
			var bg := _box(Vector3(0, 0, 0), Vector3(0.9, 0.10, 0.03), Color(0.15, 0.05, 0.05), barre)
			var fill := _box(Vector3(0, 0, -0.02), Vector3(0.86, 0.07, 0.03), Color(0.90, 0.12, 0.12), barre)
			enemies.append({
				"node": root, "barre": barre, "fill": fill, "bg": bg,
				"pv": pv, "max_pv": pv, "alive": true, "sx": x, "sz": z,
				"name": zone.t, "spd": randf_range(1.2, 2.0),
				"dir": Vector3.ZERO, "t_wander": 0.0, "cd": 0.0,
			})

func _construire_rat(root: Node3D, gros: bool):
	var s := 1.25 if gros else 0.9
	var CORPS := Color(0.42, 0.38, 0.36) if gros else Color(0.47, 0.44, 0.42)
	root.scale = Vector3.ONE * s
	# b43 : CORPS LOFTÉ = vrai maillage organique (anneaux qui gonflent puis
	# rétrécissent le long du dos), comme le rat de l'image de référence
	var rings := []
	var prof := [
		[Vector3(0, 0.28, 0.52), 0.09, 0.09],
		[Vector3(0, 0.33, 0.30), 0.24, 0.25],
		[Vector3(0, 0.38, 0.00), 0.33, 0.35],
		[Vector3(0, 0.37, -0.28), 0.29, 0.30],
		[Vector3(0, 0.33, -0.50), 0.19, 0.20],
		[Vector3(0, 0.29, -0.64), 0.11, 0.11],
	]
	for pr in prof:
		rings.push_back(anneau_z(pr[0], pr[1], pr[2], 8))
	_org("rat_corps_%d" % (1 if gros else 0), make_loft(rings), Vector3.ZERO, CORPS, root)
	# Gueule ouverte : mandibule + dents + museau rose
	_box(Vector3(0, 0.18, -0.72), Vector3(0.17, 0.05, 0.22), CORPS.darkened(0.12), root, Vector3(deg_to_rad(-20), 0, 0))
	for dx in [-0.05, 0.05]:
		_cone(Vector3(dx, 0.26, -0.80), 0.020, 0.08, Color(0.95, 0.93, 0.85), root, 4)
	_cone(Vector3(0, 0.30, -0.84), 0.055, 0.12, Color(0.80, 0.50, 0.52), root, 5)
	# Oreilles rondes aplaties + intérieur rose
	for ex in [-0.15, 0.15]:
		_sph(Vector3(ex, 0.55, -0.40), 0.11, CORPS.lightened(0.12), root)
		_sph(Vector3(ex, 0.55, -0.44), 0.06, Color(0.85, 0.55, 0.60), root)
	# Yeux rouges
	_sph(Vector3(-0.10, 0.40, -0.58), 0.035, Color(1, 0.05, 0.05), root, true)
	_sph(Vector3(0.10, 0.40, -0.58), 0.035, Color(1, 0.05, 0.05), root, true)
	# Pattes fuselées (membre organique, pas un cube)
	for px in [-0.20, 0.20]:
		for pz in [-0.28, 0.30]:
			_org("rat_patte", make_membre(Vector3(px, 0.24, pz), Vector3(px * 1.25, 0.02, pz), 0.07, 0.05), Vector3.ZERO, CORPS.darkened(0.20), root)
	# Queue rose en S : loft courbé qui s'effile
	var qr := []
	var qprof := [
		[Vector3(0, 0.30, 0.50), 0.040],
		[Vector3(0, 0.38, 0.75), 0.032],
		[Vector3(0, 0.55, 0.92), 0.024],
		[Vector3(0, 0.75, 0.98), 0.016],
		[Vector3(0, 0.92, 0.92), 0.010],
	]
	for qp in qprof:
		qr.push_back(anneau(qp[0], qp[1], qp[1], 6))
	_org("rat_queue", make_loft(qr), Vector3.ZERO, Color(0.85, 0.55, 0.60), root)

func _construire_araignee(root: Node3D):
	var CORPS := Color(0.23, 0.14, 0.10)
	# b43 : ABDOMEN lofté en oeuf facetté + thorax plus petit devant
	var ab := []
	for pr in [
		[Vector3(0, 0.55, 0.62), 0.10, 0.10],
		[Vector3(0, 0.60, 0.40), 0.26, 0.28],
		[Vector3(0, 0.62, 0.15), 0.33, 0.34],
		[Vector3(0, 0.58, -0.05), 0.24, 0.25],
		[Vector3(0, 0.54, -0.18), 0.13, 0.13],
	]:
		ab.push_back(anneau_z(pr[0], pr[1], pr[2], 8))
	_org("arai_abdomen", make_loft(ab), Vector3.ZERO, CORPS, root)
	var th := []
	for pr in [
		[Vector3(0, 0.52, -0.16), 0.12, 0.12],
		[Vector3(0, 0.54, -0.30), 0.20, 0.20],
		[Vector3(0, 0.52, -0.44), 0.15, 0.15],
		[Vector3(0, 0.50, -0.52), 0.08, 0.08],
	]:
		th.push_back(anneau_z(pr[0], pr[1], pr[2], 8))
	_org("arai_thorax", make_loft(th), Vector3.ZERO, CORPS.lightened(0.08), root)
	# Crocs
	for dx in [-0.07, 0.07]:
		_cone(Vector3(dx, 0.42, -0.56), 0.035, 0.14, Color(0.10, 0.06, 0.05), root, 4)
	# Yeux rouges
	for ex in [-0.10, -0.03, 0.04, 0.11]:
		_sph(Vector3(ex, 0.58, -0.48), 0.03, Color(1, 0.05, 0.05), root, true)
	# 8 PATTES organiques : hanche->genou levé, genou->cheville, cheville->sol
	for cote in [-1, 1]:
		for k in range(4):
			var a := deg_to_rad(-50 + k * 33)
			var hx: float = cos(a) * 0.30 * cote
			var hz: float = sin(a) * 0.30 - 0.25
			var genou := Vector3(hx * 2.2, 0.95, hz * 1.5)
			var chev := Vector3(hx * 3.4, 0.45, hz * 2.1)
			var pied := Vector3(hx * 4.0, 0.02, hz * 2.5)
			var hanche := Vector3(hx, 0.55, hz)
			_org("arai_p1", make_membre(hanche, genou, 0.065, 0.05), Vector3.ZERO, CORPS.darkened(0.05), root)
			_org("arai_p2", make_membre(genou, chev, 0.05, 0.038), Vector3.ZERO, CORPS.darkened(0.12), root)
			_org("arai_p3", make_membre(chev, pied, 0.038, 0.015), Vector3.ZERO, CORPS.darkened(0.20), root)

func update_ennemis(delta: float):
	for e in enemies:
		if not e.alive:
			continue
		var node: Node3D = e.node
		var pp := player_node.global_position
		var dist := node.global_position.distance_to(pp)
		e.cd -= delta
		e.t_wander -= delta
		var cible := Vector3.ZERO
		if dist < 10.0 and not player_dead:
			cible = (pp - node.global_position); cible.y = 0; cible = cible.normalized()
			e.dir = cible
		elif e.t_wander <= 0:
			e.t_wander = randf_range(1.5, 4.0)
			if randf() < 0.35:
				e.dir = Vector3.ZERO
			else:
				var a := randf_range(0, TAU)
				e.dir = Vector3(cos(a), 0, sin(a))
		var spd: float = e.spd * (1.4 if dist < 10.0 else 0.6)
		var ndir: Vector3 = e.dir
		# b15 : les monstres s'arrêtent À PORTÉE de coup : plus de monstre
		# qui rentre dans les jambes du joueur pour taper.
		if dist < 1.5:
			ndir = Vector3.ZERO
		# Sécurité : un monstre égaré dans le village est remis dehors illico
		if dans_village(node.global_position.x, node.global_position.z):
			var dehors := Vector2(node.global_position.x, node.global_position.z)
			if dehors.length() < 0.001:
				dehors = Vector2(1, 0)
			dehors = dehors.normalized() * (VILLAGE_R + 0.5)
			node.global_position.x = dehors.x
			node.global_position.z = dehors.y
		if ndir.length() > 0.1:
			var np := node.global_position + ndir * spd * delta
			if dans_village(np.x, np.z):
				# Village protégé : la bébête longe la clôture sans jamais entrer
				e.t_wander = minf(float(e.t_wander), 0.4)
			elif abs(np.x) < WORLD - 3 and abs(np.z) < WORLD - 3:
				# Les monstres ne traversent plus rien non plus
				var res := resoudre_collisions(np.x, np.z, 0.35)
				node.global_position.x = res.x
				node.global_position.z = res.y
			var lk := node.global_position + ndir
			node.look_at(Vector3(lk.x, node.global_position.y, lk.z), Vector3.UP)
		node.global_position.y = hauteur_terrain(node.global_position.x, node.global_position.z)
		# Barre de vie orientée caméra + remplissage
		if is_instance_valid(e.barre):
			if e.pv < e.max_pv:
				e.barre.visible = true
				e.barre.look_at(camera.global_position, Vector3.UP)
				var r := clampf(float(e.pv) / float(e.max_pv), 0.0, 1.0)
				e.fill.scale.x = maxf(r, 0.001)
				e.fill.position.x = -(1.0 - r) * 0.43
			else:
				e.barre.visible = false
		# Attaque au contact
		if dist < 1.6 and e.cd <= 0 and not player_dead and not player_protected and not cloture_entre(node.global_position, pp):
			e.cd = 1.2
			player_pv -= 6
			spawn_floater(pp + Vector3(0, 1.8, 0), "-6", Color(1, 0.3, 0.2))
			if player_pv <= 0:
				mourir()

# ============================================================
# GARDES DES PORTAILS : toute bébête à moins de 2 m de la porte
# se fait attaquer (25 dégâts / 0,8 s) jusqu'à mort.
# ============================================================
func _detecter_ecran_souris():
	# b38 : demande du dev — détecter l'écran où est la souris du joueur et
	# démarrer le jeu sur CET écran (b39 : au MILIEU de cet écran). Fait côté jeu car Godot est DPI-aware :
	# mouse_get_position() renvoie des coordonnées PHYSIQUES globales,
	# cohérentes avec screen_get_position/size même si chaque écran a un
	# scaling différent (le forçage côté launcher échouait à cause de ça).
	var mp := DisplayServer.mouse_get_position()
	for i in range(DisplayServer.get_screen_count()):
		var sp := DisplayServer.screen_get_position(i)
		var ss := DisplayServer.screen_get_size(i)
		if mp.x >= sp.x and mp.x < sp.x + ss.x and mp.y >= sp.y and mp.y < sp.y + ss.y:
			_ecran_souris = i
			break
	if _ecran_souris < 0:
		return
	var sp2 := DisplayServer.screen_get_position(_ecran_souris)
	var ss2 := DisplayServer.screen_get_size(_ecran_souris)
	var win := DisplayServer.window_get_size()
	# b39 : fenêtre AU MILIEU de l'écran de la souris (demande du dev)
	var x := sp2.x
	var y := sp2.y
	if ss2.x >= win.x:
		x = sp2.x + int((ss2.x - win.x) * 0.5)
	if ss2.y >= win.y:
		y = sp2.y + int((ss2.y - win.y) * 0.5)
	_ecran_pos = Vector2i(x, y)
	_ecran_frames = 45   # ré-applique ~0.75 s contre les resets de fenêtre
	_placer_sur_ecran_souris()

func _placer_sur_ecran_souris():
	if _ecran_souris < 0:
		return
	DisplayServer.window_set_current_screen(_ecran_souris)
	DisplayServer.window_set_position(_ecran_pos)

func update_gardes(delta: float):
	# b33 : le garde ne se déplace PLUS en patrouille (effet crabe) : il ne
	# bouge QUE pour aller attaquer une cible DANS SA ZONE (autour de SON
	# portail) puis revient à son poste. Regard = cible / extérieur.
	for p in portes:
		var gd: Node3D = p.garde
		if not is_instance_valid(gd):
			continue
		p.cd = float(p.cd) - delta
		var gpos := Vector2(gd.global_position.x, gd.global_position.z)
		var base := Vector2(float(p.bx), float(p.bz))
		var gate := Vector2(float(p.x), float(p.z))
		var cible = null
		for e in enemies:
			if not e.alive:
				continue
			var en: Node3D = e.node
			var dq := Vector2(en.global_position.x - gate.x, en.global_position.z - gate.y)
			if dq.length() < 2.5:
				cible = e
				break
		var walking := false
		var look := Vector2.ZERO
		if cible != null:
			var tp := Vector2(cible.node.global_position.x, cible.node.global_position.z)
			look = tp - gpos
			var dl := look.length()
			if dl > 1.5:
				var mv := look.normalized() * minf(2.6 * delta, dl - 1.4)
				gd.global_position.x += mv.x
				gd.global_position.z += mv.y
				walking = true
			if dl < 2.0 and float(p.cd) <= 0.0:
				p.cd = 0.8
				# b34 : le garde est OVERPOWER : ONE SHOT sur tout monstre
				cible.pv = 0
				spawn_floater(cible.node.global_position + Vector3(0, 1.3, 0), "-999", Color(1, 0.85, 0.3))
				spawn_spark(cible.node.global_position + Vector3(0, 0.6, 0))
				if int(cible.pv) <= 0:
					cible.alive = false
					cible.node.visible = false
					show_info("Un garde du village a repoussé %s !" % cible.name)
					get_tree().create_timer(10.0).timeout.connect(_respawn_enemy.bind(cible))
		else:
			var db := base - gpos
			if db.length() > 0.15:
				var mv := db.normalized() * minf(2.0 * delta, db.length())
				gd.global_position.x += mv.x
				gd.global_position.z += mv.y
				walking = true
				look = db
			elif gate.length() > 0.01:
				look = gate.normalized()
			else:
				look = Vector2(0, 1)
		gd.global_position.y = hauteur_terrain(gd.global_position.x, gd.global_position.z)
		if look.length() > 0.01:
			gd.rotation.y = atan2(look.x, look.y)
		p.pt = float(p.pt) + delta
		var sw: float = sin(float(p.pt) * 8.0) * 0.45 if walking else 0.0
		var jg := gd.get_node_or_null("jg")
		var jd := gd.get_node_or_null("jd")
		if jg:
			jg.rotation.x = sw
		if jd:
			jd.rotation.x = -sw

func mourir():
	player_pv = 0
	player_dead = true
	# b28 : le perso se COUCHE par terre et le message reste affiché
	player_node.rotation.z = deg_to_rad(90)
	player_node.global_position.y += 0.2
	if is_instance_valid(mort_label):
		mort_label.visible = true

func renaitre():
	player_dead = false
	player_pv = PV_MAX
	player_node.rotation.z = 0.0
	if is_instance_valid(mort_label):
		mort_label.visible = false
	player_node.global_position = Vector3(0, 0, 6)
	player_protected = true
	player_prot_timer = 5.0
	show_info("Renaissance ! Protection 5 s")

# ============================================================
# OBJETS RAMASSABLES
# ============================================================
func creer_objets():
	for i in range(15):
		var x := randf_range(-50, 50)
		var z := randf_range(-50, 50)
		# b29 : AUCUN caillou/pièce dans le village (ressamplé hors enceinte)
		while Vector2(x, z).length() < VILLAGE_R + 2.0 or dist_chemin(Vector2(x, z)) < 3.0:
			x = randf_range(-50, 50)
			z = randf_range(-50, 50)
		var mi := _facette(Vector3(x, hauteur_terrain(x, z) + 0.14, z), Color(0.56, 0.54, 0.50), self, Vector3(0.28, 0.22, 0.28))
		cailloux_items.append({"node": mi, "gone": false})
	for i in range(3):  # b23 : pièces plus RARES
		var x := randf_range(-40, 40)
		var z := randf_range(-40, 40)
		# b29 : jamais dans le village
		while Vector2(x, z).length() < VILLAGE_R + 2.0 or dist_chemin(Vector2(x, z)) < 3.0:
			x = randf_range(-40, 40)
			z = randf_range(-40, 40)
		var mi := _cyl(Vector3(x, hauteur_terrain(x, z) + 0.12, z), 0.18, 0.18, 0.05, Color(1, 0.84, 0.1), self, 12)
		mi.material_override = mat_std(Color(1, 0.84, 0.1), false, true)
		argent_items.append({"node": mi, "gone": false})

# ============================================================
# EFFETS (dégâts flottants + impact)
# ============================================================
func spawn_floater(pos: Vector3, texte: String, col: Color):
	var l := Label3D.new()
	l.text = texte
	l.font_size = 64
	l.pixel_size = 0.006
	l.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	l.modulate = col
	l.outline_size = 10
	l.outline_modulate = Color(0.08, 0.08, 0.08, 0.95)
	l.position = pos
	add_child(l)
	floaters.append({"node": l, "t": 0.0})

func spawn_spark(pos: Vector3):
	var root := Node3D.new()
	root.position = pos
	add_child(root)
	var m := mat_std(Color(1.0, 0.85, 0.15), false, true)
	for i in range(3):
		var b := BoxMesh.new()
		b.size = Vector3(0.9, 0.10, 0.10)
		var mi := MeshInstance3D.new()
		mi.mesh = b
		mi.material_override = m
		mi.rotation = Vector3(0, deg_to_rad(i * 60.0), deg_to_rad(20))
		root.add_child(mi)
	sparks.append({"node": root, "t": 0.0})

# ============================================================
# ACTIONS
# ============================================================
# b21 : une clôture de village se trouve-t-elle entre a et b ?
# (appartenance au village différente ET milieu loin d'un portail ouvert)
func cloture_entre(a: Vector3, b: Vector3) -> bool:
	var va := dans_village(a.x, a.z)
	var vb := dans_village(b.x, b.z)
	if va == vb:
		return false
	var m := Vector2((a.x + b.x) / 2.0, (a.z + b.z) / 2.0)
	return dist_chemin(m) > 3.4

func attaquer():
	if player_dead or player_attack_cd > 0:
		return
	player_attack_cd = 0.5
	player_attack_anim = 0.3
	var vaincus := []
	for e in enemies:
		if not e.alive: continue
		var dist = player_node.global_position.distance_to(e.node.global_position)
		# b21 : portée corps-à-corps + INTERDIT de taper à travers la clôture
		if dist < 2.4 and not cloture_entre(player_node.global_position, e.node.global_position):
			e.pv -= DEGATS
			spawn_floater(e.node.global_position + Vector3(0, 1.3, 0), str(DEGATS), Color(1, 1, 1))
			spawn_spark(e.node.global_position + Vector3(0, 0.6, 0))
			if e.pv <= 0:
				e.alive = false
				e.node.visible = false
				player_argent += 0.002
				gagner_xp(e.name)
				show_info("%s vaincu ! +0.002e" % e.name)
				vaincus.append(e)
	for e in vaincus:
		get_tree().create_timer(10.0).timeout.connect(_respawn_enemy.bind(e))

func _respawn_enemy(e: Dictionary):
	if not is_instance_valid(e.node):
		return
	e.pv = e.max_pv
	e.alive = true
	e.node.visible = true
	e.node.position.x = e.sx + randf_range(-3, 3)
	e.node.position.z = e.sz + randf_range(-3, 3)
	# Respawn jamais dans le village protégé
	if dans_village(e.node.position.x, e.node.position.z):
		var pousse := Vector2(e.node.position.x, e.node.position.z)
		if pousse.length() < 0.001:
			pousse = Vector2(1, 0)
		pousse = pousse.normalized() * (VILLAGE_R + 4.0)
		e.node.position.x = pousse.x
		e.node.position.z = pousse.y
	e.node.position.y = hauteur_terrain(e.node.position.x, e.node.position.z)

func gagner_xp(t: String):
	var v := 10
	if t == "rat": v = 15
	if t == "araignee": v = 25
	if t in ["rat", "souris"]:
		kills_rats += 1
	else:
		kills_araignees += 1
	xp += v
	while xp >= xp_need:
		xp -= xp_need
		level += 1
		xp_need = int(xp_need * 1.5)
		player_pv = mini(PV_MAX, player_pv + 25)
		show_info("Niveau %d atteint !" % level)

func utiliser_slot(i: int):
	slot_sel = i
	if i == 1 and potions[0] > 0 and player_pv < PV_MAX:
		potions[0] -= 1
		player_pv = mini(PV_MAX, player_pv + 25)
		show_info("Potion rouge : +25 PV")
	elif i == 2 and potions[1] > 0:
		potions[1] -= 1
		player_boost = 6.0
		show_info("Potion bleue : vitesse + 6 s")
	elif i == 3:
		show_info("Quête : rats %d/10 — araignées %d/5" % [kills_rats, kills_araignees])

func ramasser():
	if player_dead: return
	for c in cailloux_items:
		if c.gone: continue
		if player_node.global_position.distance_to(c.node.global_position) < 3.0:
			c.gone = true
			c.node.visible = false
			player_cailloux += 1
			show_info("+1 Caillou")
			get_tree().create_timer(20.0).timeout.connect(_respawn_caillou.bind(c))
	for a in argent_items:
		if a.gone: continue
		if player_node.global_position.distance_to(a.node.global_position) < 3.5:
			a.gone = true
			a.node.visible = false
			player_argent += 0.001
			show_info("+0.001e")
			get_tree().create_timer(60.0).timeout.connect(_respawn_argent.bind(a))

func _respawn_caillou(c: Dictionary):
	if not is_instance_valid(c.node):
		return
	var x := randf_range(-50, 50)
	var z := randf_range(-50, 50)
	c.node.position = Vector3(x, hauteur_terrain(x, z) + 0.14, z)
	c.gone = false
	c.node.visible = true

func _respawn_argent(a: Dictionary):
	if not is_instance_valid(a.node):
		return
	var x := randf_range(-40, 40)
	var z := randf_range(-40, 40)
	a.node.position = Vector3(x, hauteur_terrain(x, z) + 0.10, z)
	a.gone = false
	a.node.visible = true

# ============================================================
# HUD (style capture d'écran)
# ============================================================
class Portrait extends Control:
	var tex: Texture2D = null
	func _draw():
		var c := size * 0.5
		var r := minf(size.x, size.y) * 0.5
		draw_circle(c, r, Color(0.13, 0.10, 0.09))
		if tex:
			draw_texture_rect(tex, Rect2(Vector2.ZERO, size), false)
		else:
			# Visage du héros dessiné (aucun fichier image requis)
			draw_circle(c + Vector2(0, r * 0.10), r * 0.62, Color(0.93, 0.76, 0.58))
			draw_colored_polygon(PackedVector2Array([
				c + Vector2(-r * 0.62, r * 0.05), c + Vector2(-r * 0.45, -r * 0.55),
				c + Vector2(-r * 0.15, -r * 0.30), c + Vector2(0.0, -r * 0.72),
				c + Vector2(r * 0.20, -r * 0.32), c + Vector2(r * 0.50, -r * 0.52),
				c + Vector2(r * 0.62, r * 0.05), c + Vector2(r * 0.30, -r * 0.10),
				c + Vector2(-r * 0.30, -r * 0.10)]), Color(0.35, 0.22, 0.11))
			draw_circle(c + Vector2(-r * 0.22, r * 0.05), r * 0.09, Color(0.10, 0.10, 0.12))
			draw_circle(c + Vector2(r * 0.22, r * 0.05), r * 0.09, Color(0.10, 0.10, 0.12))
			draw_arc(c + Vector2(0, r * 0.26), r * 0.22, deg_to_rad(20), deg_to_rad(160), 12, Color(0.45, 0.25, 0.18), 2.0)
		draw_arc(c, r - 1.5, 0, TAU, 40, Color(0.85, 0.72, 0.35), 3.0)
		draw_arc(c, r - 0.5, 0, TAU, 40, Color(0.10, 0.08, 0.07), 2.0)

class Pill extends Control:
	var parent = null
	var kind := "or"
	func _process(_d): queue_redraw()
	func _draw():
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.10, 0.09, 0.08, 0.88)
		sb.set_corner_radius_all(14)
		sb.border_width_bottom = 2; sb.border_width_top = 2
		sb.border_width_left = 2; sb.border_width_right = 2
		sb.border_color = Color(0.05, 0.04, 0.04)
		draw_style_box(sb, Rect2(Vector2.ZERO, size))
		var txt := ""
		if kind == "or":
			txt = str(int(parent.player_argent * 1000.0))
			draw_circle(Vector2(20, size.y * 0.5), 9, Color(1.0, 0.78, 0.10))
			draw_circle(Vector2(20, size.y * 0.5), 5, Color(1.0, 0.88, 0.35))
		else:
			txt = str(parent.player_cailloux)
			draw_colored_polygon(PackedVector2Array([
				Vector2(20, size.y * 0.5 - 9), Vector2(28, size.y * 0.5 - 2),
				Vector2(25, size.y * 0.5 + 8), Vector2(15, size.y * 0.5 + 8),
				Vector2(12, size.y * 0.5 - 2)]), Color(0.62, 0.60, 0.57))
		draw_string(ThemeDB.fallback_font, Vector2(36, size.y * 0.5 + 7), txt,
			HORIZONTAL_ALIGNMENT_LEFT, size.x - 42, 20, Color(1, 1, 1))

class MiniMap extends Control:
	var parent = null
	func _process(_d): queue_redraw()
	func _draw():
		if parent == null or parent.player_node == null: return
		var c := size * 0.5
		var r := minf(size.x, size.y) * 0.5
		draw_circle(c, r, Color(0.36, 0.55, 0.25))
		draw_circle(c, r * 0.55, Color(0.40, 0.60, 0.28))
		var sc := (r - 8.0) / 85.0
		# Château
		draw_rect(Rect2(c + Vector2(-4.0, -78.0 * sc - 3.0), Vector2(8, 6)), Color(0.75, 0.72, 0.65))
		# Routes
		draw_line(c + Vector2(0, -30 * sc), c + Vector2(0, 30 * sc), Color(0.55, 0.52, 0.48), 2.0)
		draw_line(c + Vector2(-30 * sc, 0), c + Vector2(30 * sc, 0), Color(0.55, 0.52, 0.48), 2.0)
		# Ennemis
		for e in parent.enemies:
			if not e.alive: continue
			var p := Vector2(e.node.global_position.x, e.node.global_position.z) * sc
			if p.length() > r - 6: continue
			draw_circle(c + p, 3.0, Color(0.90, 0.12, 0.12))
		# Objets
		for it in parent.cailloux_items:
			if it.gone: continue
			var p := Vector2(it.node.position.x, it.node.position.z) * sc
			if p.length() > r - 6: continue
			draw_circle(c + p, 2.0, Color(0.75, 0.75, 0.75))
		for it in parent.argent_items:
			if it.gone: continue
			var p := Vector2(it.node.position.x, it.node.position.z) * sc
			if p.length() > r - 6: continue
			draw_circle(c + p, 2.5, Color(1.0, 0.82, 0.15))
		# Joueur (flèche)
		var fw: Vector3 = -parent.player_node.global_transform.basis.z
		var d := Vector2(fw.x, fw.z).normalized()
		var pp := Vector2(parent.player_node.global_position.x, parent.player_node.global_position.z) * sc
		var per := Vector2(-d.y, d.x)
		draw_colored_polygon(PackedVector2Array([
			c + pp + d * 7, c + pp - d * 4 + per * 4, c + pp - d * 2, c + pp - d * 4 - per * 4]),
			Color(1, 1, 1))
		# Bordure
		draw_arc(c, r - 1.0, 0, TAU, 48, Color(0.12, 0.10, 0.08), 4.0)
		draw_arc(c, r - 3.5, 0, TAU, 48, Color(0.75, 0.62, 0.30), 1.5)

class Hotbar extends Control:
	var parent = null
	const S := 56.0
	const GAP := 8.0
	func _process(_d): queue_redraw()
	func _draw():
		for i in range(5):
			var rect := Rect2(Vector2(i * (S + GAP), 0), Vector2(S, S))
			var sb := StyleBoxFlat.new()
			sb.bg_color = Color(0.09, 0.08, 0.08, 0.85)
			sb.set_corner_radius_all(8)
			if i == parent.slot_sel:
				sb.border_color = Color(1.0, 0.82, 0.15)
				sb.border_width_bottom = 3; sb.border_width_top = 3
				sb.border_width_left = 3; sb.border_width_right = 3
			else:
				sb.border_color = Color(0.25, 0.22, 0.20)
				sb.border_width_bottom = 2; sb.border_width_top = 2
				sb.border_width_left = 2; sb.border_width_right = 2
			draw_style_box(sb, rect)
			draw_string(ThemeDB.fallback_font, rect.position + Vector2(6, 16), str(i + 1),
				HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(0.85, 0.85, 0.85))
			var ic := rect.position + Vector2(S * 0.5, S * 0.55)
			match i:
				0: _ic_marteau(ic)
				1: _ic_potion(ic, Color(0.88, 0.15, 0.15))
				2: _ic_potion(ic, Color(0.20, 0.45, 0.90))
				3: _ic_rouleau(ic)
			if i == 1 or i == 2:
				draw_string(ThemeDB.fallback_font, rect.position + Vector2(S - 16, S - 6),
					str(parent.potions[i - 1]), HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color(1, 1, 1))
	func _ic_marteau(c: Vector2):
		draw_set_transform(c, deg_to_rad(-40), Vector2.ONE)
		draw_rect(Rect2(Vector2(-2.5, -14), Vector2(5, 24)), Color(0.55, 0.38, 0.18))
		draw_rect(Rect2(Vector2(-9, -20), Vector2(18, 11)), Color(0.62, 0.62, 0.66))
		draw_rect(Rect2(Vector2(-9, -16), Vector2(18, 3)), Color(0.38, 0.26, 0.13))
		draw_set_transform(Vector2.ZERO, 0, Vector2.ONE)
	func _ic_potion(c: Vector2, col: Color):
		draw_circle(c + Vector2(0, 5), 10, col)
		draw_rect(Rect2(c + Vector2(-3.5, -12), Vector2(7, 10)), col)
		draw_rect(Rect2(c + Vector2(-4.5, -15), Vector2(9, 4)), Color(0.55, 0.40, 0.22))
		draw_circle(c + Vector2(-3, 3), 3, col.lightened(0.4))
	func _ic_rouleau(c: Vector2):
		draw_rect(Rect2(c + Vector2(-10, -11), Vector2(20, 22)), Color(0.88, 0.82, 0.66))
		draw_circle(c + Vector2(-10, -11), 4, Color(0.75, 0.68, 0.52))
		draw_circle(c + Vector2(10, 11), 4, Color(0.75, 0.68, 0.52))
		for k in range(3):
			draw_line(c + Vector2(-6, -5 + k * 5), c + Vector2(6, -5 + k * 5), Color(0.45, 0.40, 0.32), 1.5)

class Losange extends Control:
	func _draw():
		var c := size * 0.5
		var r := minf(size.x, size.y) * 0.5
		draw_colored_polygon(PackedVector2Array([
			c + Vector2(0, -r), c + Vector2(r, 0), c + Vector2(0, r), c + Vector2(-r, 0)]),
			Color(0.95, 0.75, 0.10))

func creer_hud():
	var canvas := CanvasLayer.new()
	add_child(canvas)

	# ===== Portrait + barre de vie (haut gauche) =====
	var port := Portrait.new()
	# b25 : portrait en BAS à gauche (les barres remontent en haut à gauche)
	port.anchor_left = 0.0
	port.anchor_top = 1.0
	port.anchor_right = 0.0
	port.anchor_bottom = 1.0
	port.offset_left = 14
	port.offset_top = -76
	port.offset_right = 76
	port.offset_bottom = -14
	var img := load("res://pp_lv_3.png") if ResourceLoader.exists("res://pp_lv_3.png") else null
	port.tex = img
	# Découpe circulaire du portrait
	var sh := Shader.new()
	sh.code = "shader_type canvas_item;\nvoid fragment() {\n\tvec2 d = UV - vec2(0.5);\n\tCOLOR = texture(TEXTURE, UV);\n\tif (dot(d, d) > 0.23) { COLOR.a = 0.0; }\n}\n"
	var shm := ShaderMaterial.new()
	shm.shader = sh
	port.material = shm
	canvas.add_child(port)

	hp_bar = ProgressBar.new()
	hp_bar.position = Vector2(14, 14)
	hp_bar.size = Vector2(210, 26)
	hp_bar.max_value = PV_MAX
	hp_bar.value = PV_MAX
	hp_bar.show_percentage = false
	var bg := StyleBoxFlat.new()
	bg.bg_color = Color(0.25, 0.06, 0.06)
	bg.set_corner_radius_all(13)
	bg.border_width_bottom = 2; bg.border_width_top = 2
	bg.border_width_left = 2; bg.border_width_right = 2
	bg.border_color = Color(0.10, 0.03, 0.03)
	hp_bar.add_theme_stylebox_override("background", bg)
	var fill := StyleBoxFlat.new()
	fill.bg_color = Color(0.85, 0.13, 0.16)
	fill.set_corner_radius_all(13)
	hp_bar.add_theme_stylebox_override("fill", fill)
	canvas.add_child(hp_bar)
	hp_label = Label.new()
	hp_label.text = "100 HP"
	hp_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	hp_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	hp_label.add_theme_font_size_override("font_size", 15)
	hp_label.add_theme_color_override("font_color", Color(1, 1, 1))
	hp_label.add_theme_color_override("font_outline_color", Color(0.15, 0.02, 0.02))
	hp_label.add_theme_constant_override("outline_size", 4)
	hp_label.set_anchors_preset(Control.PRESET_FULL_RECT)
	hp_bar.add_child(hp_label)

	# ===== Quêtes (gauche) — réductible par le petit bouton en haut à droite =====
	quest_panel = PanelContainer.new()
	quest_panel.position = Vector2(14, 118)
	quest_panel.size = Vector2(280, 96)
	var qs := StyleBoxFlat.new()
	qs.bg_color = Color(0.10, 0.09, 0.08, 0.90)
	qs.set_corner_radius_all(6)
	qs.border_width_bottom = 2; qs.border_width_top = 2
	qs.border_width_left = 2; qs.border_width_right = 2
	qs.border_color = Color(0.05, 0.04, 0.04)
	quest_panel.add_theme_stylebox_override("panel", qs)
	canvas.add_child(quest_panel)
	var qv := VBoxContainer.new()
	qv.add_theme_constant_override("separation", 3)
	quest_panel.add_child(qv)
	var qh := HBoxContainer.new()
	qh.add_theme_constant_override("separation", 8)
	qv.add_child(qh)
	var los := Losange.new()
	los.custom_minimum_size = Vector2(16, 16)
	qh.add_child(los)
	quete_titre = Label.new()
	quete_titre.text = "PROBLÈME DE RATS"
	quete_titre.add_theme_font_size_override("font_size", 16)
	quete_titre.add_theme_color_override("font_color", Color(1, 1, 1))
	qh.add_child(quete_titre)
	var qspacer := Control.new()
	qspacer.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	qh.add_child(qspacer)
	quest_toggle = Button.new()
	quest_toggle.text = "—"
	quest_toggle.custom_minimum_size = Vector2(26, 22)
	quest_toggle.pressed.connect(_toggle_quete)
	qh.add_child(quest_toggle)
	# b24 : croix pour SUPPRIMER (fermer) l'encart de quête
	quest_close = Button.new()
	quest_close.text = "✕"
	quest_close.custom_minimum_size = Vector2(26, 22)
	quest_close.pressed.connect(_fermer_quete)
	qh.add_child(quest_close)
	quete_l1 = Label.new()
	quete_l1.text = "Vaincre 10 rats (0/10)"
	quete_l1.add_theme_font_size_override("font_size", 14)
	quete_l1.add_theme_color_override("font_color", Color(0.88, 0.88, 0.88))
	qv.add_child(quete_l1)
	quete_l2 = Label.new()
	quete_l2.text = "Vaincre 5 araignées (0/5)"
	quete_l2.add_theme_font_size_override("font_size", 14)
	quete_l2.add_theme_color_override("font_color", Color(0.88, 0.88, 0.88))
	qv.add_child(quete_l2)

	# ===== Or + cailloux : sous la barre de vie, en haut à gauche (b14) =====
	pill_or = Pill.new()
	pill_or.kind = "or"
	pill_or.parent = self
	pill_or.position = Vector2(14, 46)
	pill_or.size = Vector2(110, 30)
	canvas.add_child(pill_or)
	pill_cailloux = Pill.new()
	pill_cailloux.kind = "cailloux"
	pill_cailloux.parent = self
	pill_cailloux.position = Vector2(14, 80)
	pill_cailloux.size = Vector2(92, 30)
	canvas.add_child(pill_cailloux)

	# b28 : message de mort PERSISTANT, centré (reste jusqu'à R)
	mort_label = Label.new()
	mort_label.text = "💀  Vous êtes mort — appuie sur R pour renaître"
	mort_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	mort_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
	mort_label.set_anchors_preset(Control.PRESET_FULL_RECT)
	mort_label.add_theme_font_size_override("font_size", 30)
	mort_label.add_theme_color_override("font_color", Color(1, 0.35, 0.3))
	mort_label.add_theme_color_override("font_outline_color", Color(0.05, 0.0, 0.0))
	mort_label.add_theme_constant_override("outline_size", 6)
	mort_label.visible = false
	canvas.add_child(mort_label)

	# ===== Mini-carte =====
	minimap = MiniMap.new()
	minimap.parent = self
	minimap.size = Vector2(148, 148)
	canvas.add_child(minimap)
	minimap.anchor_left = 1.0; minimap.anchor_right = 1.0
	minimap.anchor_top = 0.0; minimap.anchor_bottom = 0.0
	minimap.offset_left = -162.0; minimap.offset_top = 14.0
	minimap.offset_right = -14.0; minimap.offset_bottom = 162.0

	# ===== Hotbar =====
	hotbar = Hotbar.new()
	hotbar.parent = self
	hotbar.size = Vector2(5 * 56 + 4 * 8, 56)
	canvas.add_child(hotbar)
	hotbar.anchor_left = 0.5; hotbar.anchor_right = 0.5
	hotbar.anchor_top = 1.0; hotbar.anchor_bottom = 1.0
	hotbar.offset_left = -156.0; hotbar.offset_top = -68.0
	hotbar.offset_right = 156.0; hotbar.offset_bottom = -12.0

	# ===== Info centre =====
	info_label = Label.new()
	info_label.text = ""
	info_label.anchor_left = 0.5; info_label.anchor_right = 0.5
	info_label.anchor_top = 0.5; info_label.anchor_bottom = 0.5
	info_label.offset_left = -280.0; info_label.offset_top = -40.0
	info_label.offset_right = 280.0; info_label.offset_bottom = 40.0
	info_label.add_theme_font_size_override("font_size", 26)
	info_label.add_theme_color_override("font_color", Color(1, 1, 0.4))
	info_label.add_theme_color_override("font_outline_color", Color(0.1, 0.1, 0.1))
	info_label.add_theme_constant_override("outline_size", 6)
	info_label.visible = false
	canvas.add_child(info_label)

	# ===== Bouton options =====
	var opt_btn := Button.new()
	opt_btn.text = "Options"
	opt_btn.size = Vector2(86, 30)
	opt_btn.anchor_left = 1.0; opt_btn.anchor_right = 1.0
	opt_btn.anchor_top = 1.0; opt_btn.anchor_bottom = 1.0
	opt_btn.offset_left = -100.0; opt_btn.offset_top = -44.0
	opt_btn.offset_right = -14.0; opt_btn.offset_bottom = -14.0
	opt_btn.pressed.connect(_toggle_options)
	canvas.add_child(opt_btn)

	# ===== Panel options (onglets Graphique / Contrôles) =====
	options_panel = PanelContainer.new()
	options_panel.size = Vector2(620, 470)
	options_panel.anchor_left = 0.5; options_panel.anchor_right = 0.5
	options_panel.anchor_top = 0.5; options_panel.anchor_bottom = 0.5
	options_panel.offset_left = -310.0; options_panel.offset_top = -235.0
	options_panel.offset_right = 310.0; options_panel.offset_bottom = 235.0
	options_panel.visible = false
	var opt_style := StyleBoxFlat.new()
	opt_style.bg_color = Color(0.1, 0.1, 0.1, 0.94)
	opt_style.set_corner_radius_all(10)
	opt_style.border_width_bottom = 2; opt_style.border_width_top = 2
	opt_style.border_width_left = 2; opt_style.border_width_right = 2
	opt_style.border_color = Color(1, 0.86, 0.2)
	options_panel.add_theme_stylebox_override("panel", opt_style)
	canvas.add_child(options_panel)
	var opt_root := VBoxContainer.new()
	opt_root.position = Vector2(16, 12)
	opt_root.size = Vector2(588, 446)
	opt_root.add_theme_constant_override("separation", 8)
	options_panel.add_child(opt_root)
	var opt_title := Label.new()
	opt_title.text = "OPTIONS"
	opt_title.add_theme_font_size_override("font_size", 22)
	opt_title.add_theme_color_override("font_color", Color(1, 0.86, 0.2))
	opt_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	opt_root.add_child(opt_title)
	var opt_hbox := HBoxContainer.new()
	opt_hbox.add_theme_constant_override("separation", 14)
	opt_root.add_child(opt_hbox)
	# --- Onglets (colonne de gauche) ---
	var tabs_vbox := VBoxContainer.new()
	tabs_vbox.custom_minimum_size = Vector2(150, 0)
	tabs_vbox.add_theme_constant_override("separation", 6)
	opt_hbox.add_child(tabs_vbox)
	tab_graph = Button.new()
	tab_graph.text = "Graphique"
	tab_graph.custom_minimum_size = Vector2(150, 40)
	tabs_vbox.add_child(tab_graph)
	tab_ctrl = Button.new()
	tab_ctrl.text = "Contrôles"
	tab_ctrl.custom_minimum_size = Vector2(150, 40)
	tabs_vbox.add_child(tab_ctrl)
	tab_graph.pressed.connect(func(): panneau_graph.visible = true; panneau_ctrl.visible = false; styliser_onglets())
	tab_ctrl.pressed.connect(func(): panneau_graph.visible = false; panneau_ctrl.visible = true; styliser_onglets())
	# --- Onglet GRAPHIQUE : luminosité + contraste ---
	panneau_graph = VBoxContainer.new()
	panneau_graph.custom_minimum_size = Vector2(404, 360)
	panneau_graph.add_theme_constant_override("separation", 12)
	opt_hbox.add_child(panneau_graph)
	var g_title := Label.new()
	g_title.text = "RÉGLAGES VISUELS"
	g_title.add_theme_font_size_override("font_size", 16)
	g_title.add_theme_color_override("font_color", Color(0.85, 0.85, 0.85))
	panneau_graph.add_child(g_title)
	var row_lum := HBoxContainer.new()
	panneau_graph.add_child(row_lum)
	var lbl_l := Label.new()
	lbl_l.text = "Luminosité"
	lbl_l.add_theme_font_size_override("font_size", 15)
	lbl_l.add_theme_color_override("font_color", Color(0.9, 0.9, 0.9))
	lbl_l.custom_minimum_size = Vector2(110, 0)
	row_lum.add_child(lbl_l)
	slider_lum = HSlider.new()
	slider_lum.min_value = 1
	slider_lum.max_value = 100
	slider_lum.step = 1
	slider_lum.value = opt_lum
	slider_lum.custom_minimum_size = Vector2(200, 22)
	row_lum.add_child(slider_lum)
	lbl_val_lum = Label.new()
	lbl_val_lum.text = str(opt_lum)
	lbl_val_lum.add_theme_font_size_override("font_size", 15)
	lbl_val_lum.add_theme_color_override("font_color", Color(1, 0.86, 0.2))
	lbl_val_lum.custom_minimum_size = Vector2(42, 0)
	row_lum.add_child(lbl_val_lum)
	slider_lum.value_changed.connect(func(v: float): opt_lum = int(v); lbl_val_lum.text = str(opt_lum); appliquer_reglages_visuels(); save_config())
	var row_con := HBoxContainer.new()
	panneau_graph.add_child(row_con)
	var lbl_c := Label.new()
	lbl_c.text = "Contraste"
	lbl_c.add_theme_font_size_override("font_size", 15)
	lbl_c.add_theme_color_override("font_color", Color(0.9, 0.9, 0.9))
	lbl_c.custom_minimum_size = Vector2(110, 0)
	row_con.add_child(lbl_c)
	slider_con = HSlider.new()
	slider_con.min_value = 1
	slider_con.max_value = 100
	slider_con.step = 1
	slider_con.value = opt_con
	slider_con.custom_minimum_size = Vector2(200, 22)
	row_con.add_child(slider_con)
	lbl_val_con = Label.new()
	lbl_val_con.text = str(opt_con)
	lbl_val_con.add_theme_font_size_override("font_size", 15)
	lbl_val_con.add_theme_color_override("font_color", Color(1, 0.86, 0.2))
	lbl_val_con.custom_minimum_size = Vector2(42, 0)
	row_con.add_child(lbl_val_con)
	slider_con.value_changed.connect(func(v: float): opt_con = int(v); lbl_val_con.text = str(opt_con); appliquer_reglages_visuels(); save_config())
	var row_res := HBoxContainer.new()
	panneau_graph.add_child(row_res)
	var lbl_r := Label.new()
	lbl_r.text = "Résolution"
	lbl_r.add_theme_font_size_override("font_size", 15)
	lbl_r.add_theme_color_override("font_color", Color(0.9, 0.9, 0.9))
	lbl_r.custom_minimum_size = Vector2(110, 0)
	row_res.add_child(lbl_r)
	opt_res = OptionButton.new()
	opt_res.custom_minimum_size = Vector2(220, 26)
	for r in RESOLUTIONS:
		opt_res.add_item("%d×%d" % [r.x, r.y])
	opt_res.selected = opt_res_idx
	opt_res.item_selected.connect(func(idx: int): opt_res_idx = idx; appliquer_resolution(); save_config())
	row_res.add_child(opt_res)
	appliquer_resolution()
	# --- Onglet CONTRÔLES : axe Y inversé + toutes les touches ---
	panneau_ctrl = VBoxContainer.new()
	panneau_ctrl.custom_minimum_size = Vector2(404, 360)
	panneau_ctrl.add_theme_constant_override("separation", 3)
	panneau_ctrl.visible = false
	opt_hbox.add_child(panneau_ctrl)
	invert_check = CheckBox.new()
	invert_check.text = "Inverser axe Y de la caméra (clic droit)"
	invert_check.add_theme_font_size_override("font_size", 15)
	invert_check.add_theme_color_override("font_color", Color(0.9, 0.9, 0.9))
	invert_check.button_pressed = cam_invert_y
	invert_check.toggled.connect(func(pressed: bool): cam_invert_y = pressed; save_config())
	panneau_ctrl.add_child(invert_check)
	panneau_ctrl.add_child(HSeparator.new())
	var c_title := Label.new()
	c_title.text = "Touches — clique sur un cadre puis appuie sur la touche voulue"
	c_title.add_theme_font_size_override("font_size", 13)
	c_title.add_theme_color_override("font_color", Color(0.75, 0.75, 0.75))
	panneau_ctrl.add_child(c_title)
	var lbl_kb := Label.new()
	lbl_kb.text = "Clavier détecté : " + ("AZERTY" if est_clavier_azerty() else "QWERTY")
	lbl_kb.add_theme_font_size_override("font_size", 13)
	lbl_kb.add_theme_color_override("font_color", Color(1, 0.86, 0.2))
	panneau_ctrl.add_child(lbl_kb)
	# b15 : l'aide des touches n'est plus en bas du HUD, elle est ICI
	var aide := Label.new()
	aide.text = ("[%s/Flèches] Bouger   [MAJ] Courir   [ESPACE] Saut   [Clic] Attaque\n[E] Ramasser   [ClicD] Caméra   [V] Vue   [1-5] Objets\n[I] Inventaire   [O] Options   [%s] Quêtes   [ÉCHAP] Quitter" % ["ZQSD" if est_clavier_azerty() else "WASD", texte_touche("quest_panel")])
	aide.add_theme_font_size_override("font_size", 12)
	aide.add_theme_color_override("font_color", Color(0.72, 0.72, 0.72))
	panneau_ctrl.add_child(aide)
	panneau_ctrl.add_child(HSeparator.new())
	for act in ACTIONS_REGLABLES:
		var row := HBoxContainer.new()
		panneau_ctrl.add_child(row)
		var lbl := Label.new()
		lbl.text = str(LIBELLES_TOUCHES[act])
		lbl.add_theme_font_size_override("font_size", 14)
		lbl.add_theme_color_override("font_color", Color(0.9, 0.9, 0.9))
		lbl.custom_minimum_size = Vector2(185, 0)
		row.add_child(lbl)
		var btn := Button.new()
		btn.custom_minimum_size = Vector2(130, 25)
		btn.text = texte_touche(act)
		row.add_child(btn)
		touches_boutons[act] = btn
		btn.pressed.connect(func(): capture_action = act; rafraichir_boutons_touches())
	# --- Fermer ---
	var close_btn := Button.new()
	close_btn.text = "Fermer"
	close_btn.custom_minimum_size = Vector2(120, 32)
	close_btn.pressed.connect(_toggle_options)
	opt_root.add_child(close_btn)
	styliser_onglets()

	# ===== Panel inventaire =====
	inv_panel = PanelContainer.new()
	inv_panel.position = Vector2(400, 150)
	inv_panel.size = Vector2(480, 400)
	inv_panel.visible = false
	var inv_style := StyleBoxFlat.new()
	inv_style.bg_color = Color(0.08, 0.08, 0.08, 0.95)
	inv_style.set_corner_radius_all(10)
	inv_style.border_width_bottom = 2; inv_style.border_width_top = 2
	inv_style.border_width_left = 2; inv_style.border_width_right = 2
	inv_style.border_color = Color(0.5, 0.8, 1.0)
	inv_panel.add_theme_stylebox_override("panel", inv_style)
	canvas.add_child(inv_panel)
	var inv_vbox := VBoxContainer.new()
	inv_vbox.position = Vector2(20, 20)
	inv_vbox.size = Vector2(440, 360)
	inv_panel.add_child(inv_vbox)
	var inv_title := Label.new()
	inv_title.text = "INVENTAIRE"
	inv_title.add_theme_font_size_override("font_size", 24)
	inv_title.add_theme_color_override("font_color", Color(0.5, 0.8, 1.0))
	inv_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	inv_vbox.add_child(inv_title)
	var inv_spacer := Control.new()
	inv_spacer.custom_minimum_size = Vector2(0, 15)
	inv_vbox.add_child(inv_spacer)
	var grid := GridContainer.new()
	grid.columns = 4
	grid.add_theme_constant_override("h_separation", 8)
	grid.add_theme_constant_override("v_separation", 8)
	inv_vbox.add_child(grid)
	for slot_i in range(20):
		var slot := PanelContainer.new()
		slot.custom_minimum_size = Vector2(90, 70)
		var slot_style := StyleBoxFlat.new()
		slot_style.bg_color = Color(0.15, 0.15, 0.15, 0.8)
		slot_style.set_corner_radius_all(6)
		slot_style.border_width_bottom = 1; slot_style.border_width_top = 1
		slot_style.border_width_left = 1; slot_style.border_width_right = 1
		slot_style.border_color = Color(0.3, 0.3, 0.3)
		slot.add_theme_stylebox_override("panel", slot_style)
		grid.add_child(slot)
		var slot_label := Label.new()
		slot_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		slot_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		slot_label.add_theme_font_size_override("font_size", 12)
		slot_label.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
		if slot_i == 0:
			slot_label.text = "Marteau"
			slot_label.add_theme_color_override("font_color", Color(0.8, 0.8, 0.8))
		if slot_i == 1:
			slot_label.text = "Potion rouge x%d" % potions[0]
		if slot_i == 2:
			slot_label.text = "Potion bleue x%d" % potions[1]
		slot.add_child(slot_label)
	var inv_close := Button.new()
	inv_close.text = "Fermer [I]"
	inv_close.size = Vector2(100, 30)
	inv_close.pressed.connect(_toggle_inventory)
	inv_vbox.add_child(inv_close)

func update_hud():
	hp_bar.value = player_pv
	hp_label.text = "%d HP" % player_pv
	quete_l1.text = ("✓ " if kills_rats >= 10 else "") + "Vaincre 10 rats (%d/10)" % mini(kills_rats, 10)
	quete_l2.text = ("✓ " if kills_araignees >= 5 else "") + "Vaincre 5 araignées (%d/5)" % mini(kills_araignees, 5)
	quete_l1.add_theme_color_override("font_color", Color(0.5, 0.9, 0.4) if kills_rats >= 10 else Color(0.88, 0.88, 0.88))
	quete_l2.add_theme_color_override("font_color", Color(0.5, 0.9, 0.4) if kills_araignees >= 5 else Color(0.88, 0.88, 0.88))

func show_info(text: String):
	info_label.text = text
	info_label.visible = true
	await get_tree().create_timer(2.0).timeout
	if info_label:
		info_label.visible = false

func _toggle_options():
	if options_panel:
		options_panel.visible = not options_panel.visible

# Réduire / déplier le panneau de quête
func update_pnj(delta: float):
	for p in pnj_items:
		p.t += delta
		var t: float = p.t
		if p.n == "Forgeron":
			# frappe l'enclume : le bras monte puis claque
			var cyc := fmod(t * 1.4, 1.0)
			var ang: float = -2.4 + 1.17 * (cyc / 0.7) if cyc < 0.7 else -1.23 - 1.17 * ((cyc - 0.7) / 0.3)
			p.bras_d.rotation.x = ang
			p.bras_g.rotation.x = -0.5
		elif p.n == "Vendeur":
			p.bras_d.rotation.x = -1.2 + sin(t * 2.2) * 0.25
			p.bras_g.rotation.x = -0.9 + sin(t * 2.2 + 1.0) * 0.2
		elif p.n == "Maire":
			p.root.rotation.y = sin(t * 0.5) * 0.35
			p.bras_d.rotation.x = -0.45
			p.bras_g.rotation.x = -0.6
		elif p.n == "Marchand":
			p.root.position.y = p.y + absf(sin(t * 1.8)) * 0.06
			p.bras_d.rotation.x = sin(t * 3.0) * 0.3 - 0.3
			p.bras_g.rotation.x = -sin(t * 3.0) * 0.3 - 0.3
		p.jambe_g.rotation.x = sin(t * 2.0) * 0.08
		p.jambe_d.rotation.x = -sin(t * 2.0) * 0.08

func _fermer_quete():
	quest_ferme = true
	if is_instance_valid(quest_panel):
		quest_panel.visible = false

func _toggle_quest_visible():
	quest_ferme = not quest_ferme
	if is_instance_valid(quest_panel):
		quest_panel.visible = not quest_ferme

func _toggle_quete():
	quest_reduit = not quest_reduit
	if is_instance_valid(quete_l1):
		quete_l1.visible = not quest_reduit
	if is_instance_valid(quete_l2):
		quete_l2.visible = not quest_reduit
	if is_instance_valid(quest_panel):
		quest_panel.size = Vector2(280, 42) if quest_reduit else Vector2(280, 96)
	if is_instance_valid(quest_toggle):
		quest_toggle.text = "+" if quest_reduit else "—"

# Onglets du menu Options : bien visibles, d'une AUTRE couleur que le contenu
# (actif = or, inactif = bleu nuit) pour ne pas les confondre avec les cadres
# de touches à droite.
func styliser_onglets():
	if not (is_instance_valid(tab_graph) and is_instance_valid(tab_ctrl)):
		return
	onglet_style(tab_graph, panneau_graph != null and panneau_graph.visible)
	onglet_style(tab_ctrl, panneau_ctrl != null and panneau_ctrl.visible)

func onglet_style(btn: Button, actif: bool):
	var sb := StyleBoxFlat.new()
	sb.bg_color = Color(0.92, 0.72, 0.14) if actif else Color(0.13, 0.22, 0.38)
	sb.set_corner_radius_all(6)
	sb.border_width_bottom = 2; sb.border_width_top = 2
	sb.border_width_left = 2; sb.border_width_right = 2
	sb.border_color = Color(1, 0.86, 0.2) if actif else Color(0.40, 0.58, 0.85)
	btn.add_theme_stylebox_override("normal", sb)
	btn.add_theme_stylebox_override("hover", sb)
	btn.add_theme_stylebox_override("pressed", sb)
	btn.add_theme_stylebox_override("focus", StyleBoxEmpty.new())
	btn.add_theme_color_override("font_color", Color(0.10, 0.08, 0.02) if actif else Color(0.78, 0.87, 1.0))
	btn.add_theme_font_size_override("font_size", 15)

func _toggle_inventory():
	if inv_panel:
		inv_panel.visible = not inv_panel.visible
		inv_open = inv_panel.visible

# ============================================================
# TOUCHES CONFIGURABLES (Options > Contrôles)
# ============================================================
func ajouter_actions_perso():
	var defs := {"camera_view": KEY_V, "inventaire": KEY_I, "options_menu": KEY_O}
	for a in defs:
		var act := str(a)
		if not InputMap.has_action(act):
			InputMap.add_action(act)
			var ev := InputEventKey.new()
			ev.physical_keycode = int(defs[a])
			InputMap.action_add_event(act, ev)

func set_action_key(action: String, keycode: int):
	InputMap.action_erase_events(action)
	var ev := InputEventKey.new()
	ev.physical_keycode = keycode
	InputMap.action_add_event(action, ev)

func set_action_mouse(action: String, button: int):
	InputMap.action_erase_events(action)
	var ev := InputEventMouseButton.new()
	ev.button_index = button
	InputMap.action_add_event(action, ev)

# Détection du type de clavier (AZERTY francophone vs QWERTY) via la langue
# de la disposition courante : les touches sont affichées TELLES QU'IMPRIMÉES
# sur le clavier du joueur (ZQSD en AZERTY, WASD en QWERTY).
func est_clavier_azerty() -> bool:
	var id := DisplayServer.keyboard_get_current_layout()
	var lang := DisplayServer.keyboard_get_layout_language(id).to_lower()
	return lang in ["fr", "be"]

func texte_touche(action: String) -> String:
	var evs := InputMap.action_get_events(action)
	var prefs := ["Z", "Q", "S", "D"] if est_clavier_azerty() else ["W", "A", "S", "D"]
	var repli := ""
	for ev in evs:
		if ev is InputEventKey:
			var kc: int = ev.physical_keycode
			if kc == 0:
				kc = ev.keycode
			if kc == 0:
				continue
			var loc: int = DisplayServer.keyboard_get_keycode_from_physical(kc)
			if loc == 0:
				loc = kc
			var nom := OS.get_keycode_string(loc)
			if nom in prefs:
				return nom
			if repli == "" and nom.length() <= 7:
				repli = nom
		if ev is InputEventMouseButton:
			if ev.button_index == MOUSE_BUTTON_LEFT:
				return "Clic gauche"
			if ev.button_index == MOUSE_BUTTON_RIGHT:
				return "Clic droit"
			if ev.button_index == MOUSE_BUTTON_MIDDLE:
				return "Clic milieu"
	if repli != "":
		return repli
	return "—"

func rafraichir_boutons_touches():
	for a in touches_boutons:
		var btn: Button = touches_boutons[a]
		if capture_action == str(a):
			btn.text = "Appuie..."
		else:
			btn.text = texte_touche(str(a))

func save_config():
	var cfg := ConfigFile.new()
	cfg.set_value("options", "cam_invert_y", cam_invert_y)
	cfg.set_value("options", "luminosite", opt_lum)
	cfg.set_value("options", "contraste", opt_con)
	cfg.set_value("options", "resolution", opt_res_idx)
	for a in ACTIONS_REGLABLES:
		var evs := InputMap.action_get_events(a)
		for ev in evs:
			if ev is InputEventKey:
				var kc: int = ev.physical_keycode
				if kc == 0:
					kc = ev.keycode
				if kc != 0:
					cfg.set_value("touches", a, kc)
				break
			if ev is InputEventMouseButton:
				cfg.set_value("touches", a, -int(ev.button_index))
				break
	cfg.save(config_path)

func load_config():
	var cfg := ConfigFile.new()
	if cfg.load(config_path) == OK:
		cam_invert_y = cfg.get_value("options", "cam_invert_y", false)
		if is_instance_valid(invert_check):
			invert_check.button_pressed = cam_invert_y
		opt_lum = int(cfg.get_value("options", "luminosite", 30))
		opt_con = int(cfg.get_value("options", "contraste", 100))
		opt_res_idx = clampi(int(cfg.get_value("options", "resolution", 2)), 0, RESOLUTIONS.size() - 1)
		if is_instance_valid(slider_lum):
			slider_lum.value = opt_lum
			lbl_val_lum.text = str(opt_lum)
		if is_instance_valid(slider_con):
			slider_con.value = opt_con
			lbl_val_con.text = str(opt_con)
		for a in ACTIONS_REGLABLES:
			if cfg.has_section_key("touches", a):
				var v: int = int(cfg.get_value("touches", a, 0))
				if v > 0:
					set_action_key(a, v)
				elif v < 0:
					set_action_mouse(a, -v)
