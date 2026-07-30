# ============================================================
# LIBREVIE — Godot 4 — Script principal
# ============================================================

extends Node3D

# CONFIG
const WORLD := 60
const SPD := 5.0
const RUN := 9.0
const PV_MAX := 100

# VARIABLES
var player_pv := PV_MAX
var player_argent := 0.0
var player_cailloux := 0
var player_dead := false
var player_protected := false
var player_prot_timer := 0.0
var player_attack_cd := 0.0
var player_vel_y := 0.0
var player_on_ground := true
var player_walk_time := 0.0

var cam_dist := 8.0
var cam_angle_x := 20.0
var cam_angle_y := 0.0
var cam_sens := 0.3
var cam_drag := false
var cam_mode := "third"
var cam_invert_y := false
var config_path := "user://librevie_config.cfg"

var player_node: Node3D
var camera: Camera3D

# Références animation
var jambe_gauche: Node3D
var jambe_droite: Node3D
var bras_gauche: Node3D
var bras_droit: Node3D

# HUD
var pv_label: Label
var argent_label: Label
var cailloux_label: Label
var info_label: Label
var vue_label: Label
var inv_panel: PanelContainer
var inv_open := false
var pv_bar: ProgressBar

var enemies := []
var cailloux_items := []
var argent_items := []
var batiments := []

# ============================================================
# READY
# ============================================================
func _ready():
	camera = $Camera3D
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)

	load_config()

	creer_sol()
	creer_routes()
	creer_ville()
	creer_fontaine()
	creer_arbres()
	creer_lampadaires()
	creer_joueur()
	creer_pnj()
	creer_ennemis()
	creer_objets()
	creer_hud()

	# Regen timer
	var timer = Timer.new()
	timer.wait_time = 3.0
	timer.autostart = true
	timer.timeout.connect(_on_regen)
	add_child(timer)

	show_info("Bienvenue dans LibreVie !")

func _on_regen():
	if not player_dead and player_pv < PV_MAX:
		player_pv = mini(PV_MAX, player_pv + 1)

# ============================================================
# PROCESS
# ============================================================
func _process(delta):
	if player_dead:
		return

	# --- MOUVEMENT ---
	var move := Vector3.ZERO
	var speed: float = RUN if Input.is_action_pressed("sprint") else SPD

	if Input.is_action_pressed("move_forward"): move.z -= 1
	if Input.is_action_pressed("move_back"): move.z += 1
	if Input.is_action_pressed("move_left"): move.x -= 1
	if Input.is_action_pressed("move_right"): move.x += 1

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

		# Collisions
		var blocked: bool = false
		for b in batiments:
			if abs(new_pos.x - b.x) < b.w / 2 + 0.5 and abs(new_pos.z - b.z) < b.d / 2 + 0.5:
				blocked = true
				break

		if not blocked and abs(new_pos.x) < WORLD and abs(new_pos.z) < WORLD:
			player_node.global_position = new_pos

		# Orientation
		var look: Vector3 = player_node.global_position + dir
		player_node.look_at(look, Vector3.UP)

	# --- GRAVITE ---
	player_vel_y -= 20 * delta
	player_node.global_position.y += player_vel_y * delta
	if player_node.global_position.y <= 0:
		player_node.global_position.y = 0
		player_vel_y = 0
		player_on_ground = true

	# --- ANIMATION MARCHE ---
	if is_moving:
		player_walk_time += delta * 8
		var swing := sin(player_walk_time) * 0.5
		if jambe_gauche:
			jambe_gauche.rotation.x = swing
		if jambe_droite:
			jambe_droite.rotation.x = -swing
		if bras_gauche:
			bras_gauche.rotation.x = -swing * 0.7
		if bras_droit:
			bras_droit.rotation.x = swing * 0.7
	else:
		player_walk_time = 0
		if jambe_gauche: jambe_gauche.rotation.x = 0
		if jambe_droite: jambe_droite.rotation.x = 0
		if bras_gauche: bras_gauche.rotation.x = 0
		if bras_droit: bras_droit.rotation.x = 0

	# --- COOLDOWNS ---
	if player_attack_cd > 0: player_attack_cd -= delta
	if player_protected:
		player_prot_timer -= delta
		if player_prot_timer <= 0: player_protected = false

	# --- CAMERA ---
	update_camera()

	# --- HUD ---
	update_hud()

# ============================================================
# INPUT
# ============================================================
func _input(event):
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

	if event.is_action_pressed("jump") and player_on_ground:
		player_vel_y = 8
		player_on_ground = false

	if event.is_action_pressed("attack"):
		attaquer()

	if event.is_action_pressed("pickup"):
		ramasser()

	if event is InputEventKey and event.pressed:
		if event.keycode == KEY_V:
			cam_mode = "first" if cam_mode == "third" else "third"
		if event.keycode == KEY_O:
			_toggle_options()
		if event.keycode == KEY_I:
			_toggle_inventory()
		if event.keycode == KEY_ESCAPE:
			get_tree().quit()

# ============================================================
# CAMERA
# ============================================================
func update_camera():
	if not player_node:
		return
	var rx := deg_to_rad(cam_angle_x)
	var ry := deg_to_rad(cam_angle_y)
	var tx := player_node.global_position.x
	var ty := player_node.global_position.y + 0.9
	var tz := player_node.global_position.z

	if cam_mode == "third":
		var cx := tx + sin(ry) * cos(rx) * cam_dist
		var cy := ty + sin(rx) * cam_dist
		var cz := tz + cos(ry) * cos(rx) * cam_dist
		camera.global_position = Vector3(cx, cy, cz)
		camera.look_at(Vector3(tx, ty, tz), Vector3.UP)
	else:
		camera.global_position = Vector3(tx, player_node.global_position.y + 1.5, tz)
		var lx := tx + sin(ry) * cos(rx) * 10
		var ly := player_node.global_position.y + 1.5 + sin(rx) * 10
		var lz := tz + cos(ry) * cos(rx) * 10
		camera.look_at(Vector3(lx, ly, lz), Vector3.UP)

# ============================================================
# CREATION DU MONDE
# ============================================================
func _make_box(pos: Vector3, size: Vector3, col: Color, parent: Node = self) -> CSGBox3D:
	var box = CSGBox3D.new()
	box.size = size
	box.position = pos
	var mat = StandardMaterial3D.new()
	mat.albedo_color = col
	box.material = mat
	parent.add_child(box)
	return box

func _make_cyl(pos: Vector3, radius: float, height: float, col: Color) -> CSGCylinder3D:
	var cyl = CSGCylinder3D.new()
	cyl.radius = radius
	cyl.height = height
	cyl.position = pos
	var mat = StandardMaterial3D.new()
	mat.albedo_color = col
	cyl.material = mat
	add_child(cyl)
	return cyl

func _make_sphere(pos: Vector3, radius: float, col: Color, emission: bool = false) -> CSGSphere3D:
	var sph = CSGSphere3D.new()
	sph.radius = radius
	sph.position = pos
	var mat = StandardMaterial3D.new()
	mat.albedo_color = col
	if emission:
		mat.emission_enabled = true
		mat.emission = col
		mat.emission_energy_multiplier = 1.0
	sph.material = mat
	add_child(sph)
	return sph

func creer_sol():
	_make_box(Vector3(0, -0.05, 0), Vector3(WORLD * 2, 0.1, WORLD * 2), Color(0.24, 0.55, 0.16))

func creer_routes():
	var routes = [
		[0, 0.02, 0, WORLD * 2, 0.04, 6],
		[0, 0.02, 0, 6, 0.04, WORLD * 2],
		[-20, 0.02, 10, 40, 0.04, 4],
		[20, 0.02, -10, 40, 0.04, 4],
	]
	for r in routes:
		_make_box(Vector3(r[0], r[1], r[2]), Vector3(r[3], r[4], r[5]), Color(0.51, 0.50, 0.49))

func creer_ville():
	var data = [
		{"x":-10,"z":-6,"w":5,"h":4,"d":4,"c":Color(0.82,0.69,0.51),"n":"Supermarche","roof":Color(0.65,0.25,0.10)},
		{"x":-10,"z":3,"w":4,"h":3.5,"d":3.5,"c":Color(0.76,0.61,0.39),"n":"Armurerie","roof":Color(0.45,0.45,0.45)},
		{"x":10,"z":-6,"w":4,"h":3.5,"d":3.5,"c":Color(0.51,0.73,0.59),"n":"Vetements","roof":Color(0.55,0.35,0.20)},
		{"x":10,"z":3,"w":4,"h":4,"d":4,"c":Color(0.75,0.59,0.35),"n":"Auberge","roof":Color(0.60,0.22,0.08)},
		{"x":0,"z":-15,"w":8,"h":6,"d":6,"c":Color(0.86,0.82,0.71),"n":"Mairie","roof":Color(0.30,0.45,0.55)},
		{"x":-20,"z":-12,"w":4,"h":3.5,"d":3.5,"c":Color(0.78,0.65,0.47),"n":"Maison","roof":Color(0.60,0.22,0.08)},
		{"x":-20,"z":-4,"w":3.5,"h":3,"d":3.5,"c":Color(0.73,0.59,0.41),"n":"Maison","roof":Color(0.55,0.30,0.15)},
		{"x":20,"z":8,"w":4,"h":3.5,"d":3.5,"c":Color(0.78,0.65,0.47),"n":"Maison","roof":Color(0.60,0.22,0.08)},
		{"x":20,"z":-4,"w":3.5,"h":3,"d":3.5,"c":Color(0.73,0.59,0.41),"n":"Maison","roof":Color(0.55,0.30,0.15)},
	]
	for b in data:
		# Murs principaux
		_make_box(Vector3(b.x, b.h/2.0, b.z), Vector3(b.w, b.h, b.d), b.c)
		# Fondations (pierre)
		_make_box(Vector3(b.x, 0.1, b.z), Vector3(b.w+0.2, 0.2, b.d+0.2), Color(0.50,0.48,0.45))
		# Toit (pente simulée avec 3 couches)
		_make_box(Vector3(b.x, b.h+0.05, b.z), Vector3(b.w+0.6, 0.15, b.d+0.6), b.roof)
		_make_box(Vector3(b.x, b.h+0.2, b.z), Vector3(b.w+0.3, 0.15, b.d+0.3), b.roof)
		_make_box(Vector3(b.x, b.h+0.35, b.z), Vector3(b.w*0.6, 0.12, b.d*0.6), b.roof)
		# Porte (marron foncé + cadre)
		_make_box(Vector3(b.x, b.h*0.25, b.z+b.d/2.0+0.06), Vector3(b.w*0.22, b.h*0.48, 0.14), Color(0.22,0.12,0.04))
		_make_box(Vector3(b.x, b.h*0.5, b.z+b.d/2.0+0.06), Vector3(b.w*0.26, 0.06, 0.15), Color(0.35,0.20,0.08))
		# Fenêtres avec cadre
		for fx in [-0.28, 0.28]:
			_make_box(Vector3(b.x+b.w*fx, b.h*0.58, b.z+b.d/2.0+0.06), Vector3(b.w*0.18, b.h*0.18, 0.12), Color(0.30,0.20,0.10))
			_make_box(Vector3(b.x+b.w*fx, b.h*0.58, b.z+b.d/2.0+0.07), Vector3(b.w*0.13, b.h*0.13, 0.08), Color(0.50,0.75,0.95))
			# Croix fenêtre
			_make_box(Vector3(b.x+b.w*fx, b.h*0.58, b.z+b.d/2.0+0.08), Vector3(b.w*0.13, 0.02, 0.02), Color(0.30,0.20,0.10))
			_make_box(Vector3(b.x+b.w*fx, b.h*0.58, b.z+b.d/2.0+0.08), Vector3(0.02, b.h*0.13, 0.02), Color(0.30,0.20,0.10))
		# Fenêtres côté
		for fz in [-0.28, 0.28]:
			_make_box(Vector3(b.x+b.w/2.0+0.06, b.h*0.58, b.z+b.d*fz), Vector3(0.12, b.h*0.15, b.d*0.12), Color(0.50,0.75,0.95))
			_make_box(Vector3(b.x-b.w/2.0-0.06, b.h*0.58, b.z+b.d*fz), Vector3(0.12, b.h*0.15, b.d*0.12), Color(0.50,0.75,0.95))
		# Auvent commerces
		if b.n in ["Supermarche","Armurerie","Vetements","Auberge"]:
			_make_box(Vector3(b.x, b.h*0.42, b.z+b.d/2.0+0.9), Vector3(b.w+0.4, 0.08, 1.8), Color(0.70,0.35,0.10))
			# Poteaux auvent
			_make_box(Vector3(b.x-b.w/2.0+0.1, b.h*0.22, b.z+b.d/2.0+1.7), Vector3(0.1, b.h*0.4, 0.1), Color(0.40,0.25,0.10))
			_make_box(Vector3(b.x+b.w/2.0-0.1, b.h*0.22, b.z+b.d/2.0+1.7), Vector3(0.1, b.h*0.4, 0.1), Color(0.40,0.25,0.10))
		# Cheminée pour maisons
		if b.n == "Maison":
			_make_box(Vector3(b.x+b.w*0.3, b.h+0.6, b.z+b.d*0.3), Vector3(0.4, 0.6, 0.4), Color(0.60,0.30,0.15))
			_make_box(Vector3(b.x+b.w*0.3, b.h+0.95, b.z+b.d*0.3), Vector3(0.5, 0.08, 0.5), Color(0.50,0.25,0.12))
		# Escalier Mairie
		if b.n == "Mairie":
			for step in range(3):
				_make_box(Vector3(b.x, 0.05+step*0.08, b.z+b.d/2.0+0.5+step*0.3),
					Vector3(b.w*0.6, 0.08, 0.3), Color(0.55,0.53,0.50))
		batiments.append({"x":b.x,"z":b.z,"w":b.w+0.5,"d":b.d+0.5})

func creer_fontaine():
	_make_cyl(Vector3(0,0.5,0), 2.5, 1.0, Color(0.55,0.54,0.53))
	_make_cyl(Vector3(0,0.8,0), 2.1, 0.4, Color(0.16,0.55,0.86))
	_make_cyl(Vector3(0,2,0), 0.3, 2.5, Color(0.61,0.60,0.59))

func creer_arbres():
	for p in [[-4,4],[4,4],[-4,-4],[4,-4],[-7,12],[7,12],[-7,-12],[7,-12],
			  [-12,8],[12,-8],[-15,6],[15,-6],[0,12],[0,-12],[12,0],[-12,0]]:
		# Tronc avec écorce
		_make_box(Vector3(p[0],1,p[1]), Vector3(0.3,2,0.3), Color(0.40,0.25,0.12))
		# Branches
		_make_box(Vector3(p[0]+0.3,1.8,p[1]), Vector3(0.5,0.08,0.08), Color(0.38,0.23,0.10))
		_make_box(Vector3(p[0]-0.2,1.5,p[1]+0.2), Vector3(0.08,0.08,0.4), Color(0.38,0.23,0.10))
		# Feuilles (3 sphères pour volume)
		_make_sphere(Vector3(p[0],2.8,p[1]), 1.3, Color(0.18,0.60,0.18))
		_make_sphere(Vector3(p[0]+0.5,2.4,p[1]+0.3), 0.9, Color(0.15,0.55,0.15))
		_make_sphere(Vector3(p[0]-0.3,3.2,p[1]-0.2), 0.8, Color(0.20,0.65,0.20))

func creer_lampadaires():
	for p in [[-3,6],[3,6],[-3,-6],[3,-6],[-10,0],[10,0],[0,10],[0,-10]]:
		_make_box(Vector3(p[0],1.75,p[1]), Vector3(0.15,3.5,0.15), Color(0.16,0.16,0.16))
		_make_sphere(Vector3(p[0],3.7,p[1]), 0.25, Color(1,0.86,0.24), true)
		var light = OmniLight3D.new()
		light.position = Vector3(p[0], 3.7, p[1])
		light.light_color = Color(1, 0.86, 0.59)
		light.light_energy = 0.5
		light.omni_range = 8.0
		add_child(light)

# ============================================================
# JOUEUR
func creer_joueur():
	var scene = load("res://glb/perso_voxel.glb")
	player_node = scene.instantiate()
	player_node.position = Vector3(0, 0, 0)
	add_child(player_node)

	# Lumière autour du joueur
	var light = OmniLight3D.new()
	light.position = Vector3(0, 3, 0)
	light.light_color = Color(1, 0.95, 0.85)
	light.light_energy = 0.8
	light.omni_range = 15
	player_node.add_child(light)

	# Trouver les parties du corps par nom
	jambe_gauche = player_node.find_child("JambeG", true, false)
	jambe_droite = player_node.find_child("JambeD", true, false)
	bras_gauche = player_node.find_child("BrasG", true, false)
	bras_droit = player_node.find_child("BrasD", true, false)
	if not jambe_gauche: jambe_gauche = Node3D.new(); player_node.add_child(jambe_gauche)
	if not jambe_droite: jambe_droite = Node3D.new(); player_node.add_child(jambe_droite)
	if not bras_gauche: bras_gauche = Node3D.new(); player_node.add_child(bras_gauche)
	if not bras_droit: bras_droit = Node3D.new(); player_node.add_child(bras_droit)
# ============================================================
func creer_pnj():
	var data = [
		{"x":-10,"z":-3,"c":Color(0.78,0.18,0.14),"n":"Vendeur"},
		{"x":10,"z":5,"c":Color(0.14,0.67,0.35),"n":"Forgeron"},
		{"x":0,"z":-12,"c":Color(0.47,0.22,0.59),"n":"Maire"},
		{"x":10,"z":-3,"c":Color(0.78,0.37,0.06),"n":"Marchand"},
	]
	for d in data:
		_make_box(Vector3(d.x, 0.4, d.z), Vector3(0.4, 0.8, 0.3), d.c)
		_make_box(Vector3(d.x, 0.95, d.z), Vector3(0.3, 0.3, 0.3), Color(0.90, 0.75, 0.59))
		_make_box(Vector3(d.x, 1.12, d.z), Vector3(0.32, 0.1, 0.32), d.c)
		for side in [-0.12, 0.12]:
			_make_box(Vector3(d.x+side, 0.62, d.z+0.15), Vector3(0.08, 0.06, 0.04), Color(0.08,0.08,0.08))
		for side in [-0.15, 0.15]:
			_make_box(Vector3(d.x+side, 0.2, d.z), Vector3(0.12, 0.4, 0.15), Color(0.20,0.20,0.20))

		var label = Label3D.new()
		label.text = d.n
		label.position = Vector3(d.x, 1.5, d.z)
		label.font_size = 18
		label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		add_child(label)

		var enseigne = Label3D.new()
		enseigne.text = d.n.to_upper()
		enseigne.position = Vector3(d.x, 3.0, d.z)
		enseigne.font_size = 24
		enseigne.billboard = BaseMaterial3D.BILLBOARD_ENABLED
		enseigne.modulate = Color(1, 1, 0.4)
		add_child(enseigne)

# ============================================================
# ENNEMIS
# ============================================================
func creer_ennemis():
	var zones = [
		{"cx":25,"cz":20,"n":4,"t":"souris","pv":5,"s":0.3},
		{"cx":-25,"cz":-20,"n":4,"t":"souris","pv":5,"s":0.3},
		{"cx":30,"cz":-25,"n":3,"t":"rat","pv":10,"s":0.5},
		{"cx":-30,"cz":25,"n":3,"t":"rat","pv":10,"s":0.5},
	]
	for zone in zones:
		for i in range(zone.n):
			var x = zone.cx + randf_range(-8, 8)
			var z = zone.cz + randf_range(-8, 8)
			var enemy = _make_box(Vector3(x, zone.s*0.4, z), Vector3(zone.s, zone.s*0.6, zone.s*1.2), Color(0.27,0.27,0.27))
			# Yeux rouges
			_make_box(Vector3(x-zone.s*0.2, zone.s*0.55, z+zone.s*0.4), Vector3(0.04, 0.03, 0.02), Color(1,0,0))
			_make_box(Vector3(x+zone.s*0.2, zone.s*0.55, z+zone.s*0.4), Vector3(0.04, 0.03, 0.02), Color(1,0,0))
			# Pattes
			for j in range(4):
				var lx = (-1 if j%2==0 else 1) * zone.s*0.4
				var lz = (1 if j<2 else -1) * zone.s*0.4
				_make_box(Vector3(x+lx, zone.s*0.1, z+lz), Vector3(0.06, zone.s*0.3, 0.06), Color(0.22,0.22,0.22))
			enemies.append({"node":enemy, "pv":zone.pv, "max_pv":zone.pv, "alive":true, "sx":x, "sz":z, "name":zone.t})

# ============================================================
# OBJETS
# ============================================================
func creer_objets():
	for i in range(15):
		var x = randf_range(-50, 50)
		var z = randf_range(-50, 50)
		var c = _make_sphere(Vector3(x, 0.12, z), 0.12, Color(0.55,0.53,0.49))
		cailloux_items.append({"node":c, "gone":false})
	for i in range(6):
		var x = randf_range(-40, 40)
		var z = randf_range(-40, 40)
		var a = _make_cyl(Vector3(x, 0.08, z), 0.1, 0.02, Color(1,0.84,0))
		argent_items.append({"node":a, "gone":false})

# ============================================================
# HUD
# ============================================================
# Options
var options_panel: PanelContainer
var invert_check: CheckBox

func creer_hud():
	var canvas = CanvasLayer.new()
	add_child(canvas)

	# === BOUTON OPTIONS ===
	var opt_btn = Button.new()
	opt_btn.text = "Options"
	opt_btn.position = Vector2(1180, 680)
	opt_btn.size = Vector2(80, 30)
	opt_btn.pressed.connect(_toggle_options)
	canvas.add_child(opt_btn)

	# === PANEL OPTIONS ===
	options_panel = PanelContainer.new()
	options_panel.position = Vector2(440, 250)
	options_panel.size = Vector2(400, 200)
	options_panel.visible = false
	var opt_style = StyleBoxFlat.new()
	opt_style.bg_color = Color(0.1, 0.1, 0.1, 0.9)
	opt_style.corner_radius_top_left = 10
	opt_style.corner_radius_top_right = 10
	opt_style.corner_radius_bottom_left = 10
	opt_style.corner_radius_bottom_right = 10
	opt_style.border_width_bottom = 2
	opt_style.border_width_top = 2
	opt_style.border_width_left = 2
	opt_style.border_width_right = 2
	opt_style.border_color = Color(1, 0.86, 0.2)
	options_panel.add_theme_stylebox_override("panel", opt_style)
	canvas.add_child(options_panel)

	var opt_vbox = VBoxContainer.new()
	opt_vbox.position = Vector2(20, 20)
	opt_vbox.size = Vector2(360, 160)
	options_panel.add_child(opt_vbox)

	var opt_title = Label.new()
	opt_title.text = "OPTIONS"
	opt_title.add_theme_font_size_override("font_size", 22)
	opt_title.add_theme_color_override("font_color", Color(1, 0.86, 0.2))
	opt_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	opt_vbox.add_child(opt_title)

	var spacer = Control.new()
	spacer.custom_minimum_size = Vector2(0, 15)
	opt_vbox.add_child(spacer)

	# Checkbox inverser souris
	invert_check = CheckBox.new()
	invert_check.text = "Inverser axe Y de la camera (clic droit)"
	invert_check.add_theme_font_size_override("font_size", 16)
	invert_check.add_theme_color_override("font_color", Color(0.9, 0.9, 0.9))
	invert_check.button_pressed = cam_invert_y
	invert_check.toggled.connect(func(pressed: bool): cam_invert_y = pressed; save_config())
	invert_check.button_pressed = cam_invert_y
	opt_vbox.add_child(invert_check)

	var spacer2 = Control.new()
	spacer2.custom_minimum_size = Vector2(0, 20)
	opt_vbox.add_child(spacer2)

	var close_btn = Button.new()
	close_btn.text = "Fermer"
	close_btn.size = Vector2(100, 30)
	close_btn.pressed.connect(_toggle_options)
	opt_vbox.add_child(close_btn)

	# === PANEL INVENTAIRE ===
	inv_panel = PanelContainer.new()
	inv_panel.position = Vector2(400, 150)
	inv_panel.size = Vector2(480, 400)
	inv_panel.visible = false
	var inv_style = StyleBoxFlat.new()
	inv_style.bg_color = Color(0.08, 0.08, 0.08, 0.95)
	inv_style.corner_radius_top_left = 10
	inv_style.corner_radius_top_right = 10
	inv_style.corner_radius_bottom_left = 10
	inv_style.corner_radius_bottom_right = 10
	inv_style.border_width_bottom = 2
	inv_style.border_width_top = 2
	inv_style.border_width_left = 2
	inv_style.border_width_right = 2
	inv_style.border_color = Color(0.5, 0.8, 1.0)
	inv_panel.add_theme_stylebox_override("panel", inv_style)
	canvas.add_child(inv_panel)

	var inv_vbox = VBoxContainer.new()
	inv_vbox.position = Vector2(20, 20)
	inv_vbox.size = Vector2(440, 360)
	inv_panel.add_child(inv_vbox)

	var inv_title = Label.new()
	inv_title.text = "INVENTAIRE"
	inv_title.add_theme_font_size_override("font_size", 24)
	inv_title.add_theme_color_override("font_color", Color(0.5, 0.8, 1.0))
	inv_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	inv_vbox.add_child(inv_title)

	var inv_spacer = Control.new()
	inv_spacer.custom_minimum_size = Vector2(0, 15)
	inv_vbox.add_child(inv_spacer)

	# Grille d'inventaire 4x5
	var grid = GridContainer.new()
	grid.columns = 4
	grid.add_theme_constant_override("h_separation", 8)
	grid.add_theme_constant_override("v_separation", 8)
	inv_vbox.add_child(grid)

	for slot_i in range(20):
		var slot = PanelContainer.new()
		slot.custom_minimum_size = Vector2(90, 70)
		var slot_style = StyleBoxFlat.new()
		slot_style.bg_color = Color(0.15, 0.15, 0.15, 0.8)
		slot_style.corner_radius_top_left = 6
		slot_style.corner_radius_top_right = 6
		slot_style.corner_radius_bottom_left = 6
		slot_style.corner_radius_bottom_right = 6
		slot_style.border_width_bottom = 1
		slot_style.border_width_top = 1
		slot_style.border_width_left = 1
		slot_style.border_width_right = 1
		slot_style.border_color = Color(0.3, 0.3, 0.3)
		slot.add_theme_stylebox_override("panel", slot_style)
		grid.add_child(slot)

		var slot_label = Label.new()
		slot_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
		slot_label.vertical_alignment = VERTICAL_ALIGNMENT_CENTER
		slot_label.add_theme_font_size_override("font_size", 12)
		slot_label.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
		if slot_i == 0:
			slot_label.text = "Marteau"
			slot_label.add_theme_color_override("font_color", Color(0.8, 0.8, 0.8))
		slot.add_child(slot_label)

	# Bouton fermer inventaire
	var inv_close = Button.new()
	inv_close.text = "Fermer [I]"
	inv_close.size = Vector2(100, 30)
	inv_close.pressed.connect(_toggle_inventory)
	inv_vbox.add_child(inv_close)

	# === HUD NORMAL ===

	# Panel gauche
	var panel = PanelContainer.new()
	panel.position = Vector2(15, 15)
	panel.size = Vector2(250, 120)
	var style = StyleBoxFlat.new()
	style.bg_color = Color(0, 0, 0, 0.5)
	style.corner_radius_top_left = 8
	style.corner_radius_top_right = 8
	style.corner_radius_bottom_left = 8
	style.corner_radius_bottom_right = 8
	panel.add_theme_stylebox_override("panel", style)
	canvas.add_child(panel)

	var vbox = VBoxContainer.new()
	vbox.position = Vector2(10, 10)
	panel.add_child(vbox)

	# Label PV
	var pv_title = Label.new()
	pv_title.text = "PV"
	pv_title.add_theme_font_size_override("font_size", 16)
	pv_title.add_theme_color_override("font_color", Color(1, 0.3, 0.3))
	vbox.add_child(pv_title)

	# Barre de vie
	pv_bar = ProgressBar.new()
	pv_bar.custom_minimum_size = Vector2(220, 20)
	pv_bar.max_value = PV_MAX
	pv_bar.value = PV_MAX
	pv_bar.show_percentage = false
	var bar_bg = StyleBoxFlat.new()
	bar_bg.bg_color = Color(0.2, 0.05, 0.05)
	bar_bg.corner_radius_top_left = 4
	bar_bg.corner_radius_top_right = 4
	bar_bg.corner_radius_bottom_left = 4
	bar_bg.corner_radius_bottom_right = 4
	pv_bar.add_theme_stylebox_override("background", bar_bg)
	var bar_fill = StyleBoxFlat.new()
	bar_fill.bg_color = Color(0.9, 0.15, 0.15)
	bar_fill.corner_radius_top_left = 4
	bar_fill.corner_radius_top_right = 4
	bar_fill.corner_radius_bottom_left = 4
	bar_fill.corner_radius_bottom_right = 4
	pv_bar.add_theme_stylebox_override("fill", bar_fill)
	vbox.add_child(pv_bar)

	pv_label = Label.new()
	pv_label.text = "100 / 100"
	pv_label.add_theme_font_size_override("font_size", 14)
	pv_label.add_theme_color_override("font_color", Color(0.8, 0.8, 0.8))
	vbox.add_child(pv_label)

	argent_label = Label.new()
	argent_label.text = "Argent: 0.000e"
	argent_label.add_theme_font_size_override("font_size", 18)
	argent_label.add_theme_color_override("font_color", Color(1, 0.86, 0.20))
	vbox.add_child(argent_label)

	cailloux_label = Label.new()
	cailloux_label.text = "Cailloux: 0"
	cailloux_label.add_theme_font_size_override("font_size", 16)
	cailloux_label.add_theme_color_override("font_color", Color(0.8, 0.8, 0.8))
	vbox.add_child(cailloux_label)

	vue_label = Label.new()
	vue_label.text = "[V] 3eme"
	vue_label.add_theme_font_size_override("font_size", 14)
	vue_label.add_theme_color_override("font_color", Color(0.7, 0.7, 0.7))
	vbox.add_child(vue_label)

	# Titre droite
	var titre = Label.new()
	titre.text = "LibreVie"
	titre.position = Vector2(1100, 15)
	titre.add_theme_font_size_override("font_size", 30)
	titre.add_theme_color_override("font_color", Color(1, 0.86, 0.20))
	canvas.add_child(titre)

	# Contrôles bas
	var ctrl = Label.new()
	ctrl.text = "[ZQSD/Fleches] Move  [SHIFT] Run  [SPACE] Jump  [Click] Attack  [E] Pickup  [RightClick] Camera  [V] View  [I] Inventory  [O] Options  [ESC] Quit"
	ctrl.position = Vector2(150, 685)
	ctrl.add_theme_font_size_override("font_size", 13)
	ctrl.add_theme_color_override("font_color", Color(0.6, 0.6, 0.6))
	canvas.add_child(ctrl)

	# Info centre
	info_label = Label.new()
	info_label.text = ""
	info_label.position = Vector2(480, 330)
	info_label.add_theme_font_size_override("font_size", 26)
	info_label.add_theme_color_override("font_color", Color(1, 1, 0))
	info_label.visible = false
	canvas.add_child(info_label)

func update_hud():
	pv_bar.value = player_pv
	pv_label.text = "%d / %d" % [player_pv, PV_MAX]
	argent_label.text = "Argent: %.3fe" % player_argent
	cailloux_label.text = "Cailloux: %d" % player_cailloux
	vue_label.text = "[V] %s" % ("3eme" if cam_mode == "third" else "1ere")

func show_info(text: String):
	info_label.text = text
	info_label.visible = true
	await get_tree().create_timer(2.0).timeout
	if info_label:
		info_label.visible = false

func _toggle_options():
	if options_panel:
		options_panel.visible = not options_panel.visible

func _toggle_inventory():
	if inv_panel:
		inv_panel.visible = not inv_panel.visible
		inv_open = inv_panel.visible

func save_config():
	var cfg = ConfigFile.new()
	cfg.set_value("options", "cam_invert_y", cam_invert_y)
	cfg.save(config_path)

func load_config():
	var cfg = ConfigFile.new()
	if cfg.load(config_path) == OK:
		cam_invert_y = cfg.get_value("options", "cam_invert_y", false)
		if is_instance_valid(invert_check):
			invert_check.button_pressed = cam_invert_y

# ============================================================
# ACTIONS
# ============================================================
func attaquer():
	if player_dead or player_attack_cd > 0:
		return
	player_attack_cd = 0.5
	for e in enemies:
		if not e.alive: continue
		var dist = player_node.global_position.distance_to(e.node.global_position)
		if dist < 3.5:
			e.pv -= 5
			if e.pv <= 0:
				e.alive = false
				e.node.visible = false
				player_argent += 0.002
				show_info("%s vaincu ! +0.002e" % e.name)
				await get_tree().create_timer(10.0).timeout
				e.pv = e.max_pv
				e.alive = true
				e.node.visible = true
				e.node.position.x = e.sx + randf_range(-3, 3)
				e.node.position.z = e.sz + randf_range(-3, 3)

func ramasser():
	if player_dead: return
	for c in cailloux_items:
		if c.gone: continue
		if player_node.global_position.distance_to(c.node.global_position) < 3.0:
			c.gone = true
			c.node.visible = false
			player_cailloux += 1
			show_info("+1 Caillou")
			await get_tree().create_timer(20.0).timeout
			c.node.position.x = randf_range(-50, 50)
			c.node.position.z = randf_range(-50, 50)
			c.gone = false
			c.node.visible = true
	for a in argent_items:
		if a.gone: continue
		if player_node.global_position.distance_to(a.node.global_position) < 3.0:
			a.gone = true
			a.node.visible = false
			player_argent += 0.001
			show_info("+0.001e")
			await get_tree().create_timer(60.0).timeout
			a.node.position.x = randf_range(-40, 40)
			a.node.position.z = randf_range(-40, 40)
			a.gone = false
			a.node.visible = true
