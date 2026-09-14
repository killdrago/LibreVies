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
const BUILD := "0.3.0-b10"    # témoin de build : titre de fenêtre + message d'accueil
const VILLAGE_R := 26.0      # village protégé : clôture + zone interdite aux monstres

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
var barre_xp: Control
var pill_or: Control
var pill_cailloux: Control
var quete_titre: Label
var quete_l1: Label
var quete_l2: Label

var enemies := []
var cailloux_items := []
var argent_items := []
var batiments := []
var colliders: Array[Dictionary] = []   # collisions statiques (bâtiments, props, clôture, rochers)
var portes: Array[Dictionary] = []      # portails du village + leurs gardes
var monde_env: Environment              # réglages luminosité / contraste / saturation
var opt_lum := 50                       # 1..100
var opt_con := 50                       # 1..100
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
	camera = $Camera3D
	Input.set_mouse_mode(Input.MOUSE_MODE_VISIBLE)
	DisplayServer.window_set_title("LibreVie %s" % BUILD)

	load_config()

	creer_environnement()
	creer_terrain()
	creer_ville()
	creer_chateau()
	creer_fontaine()
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
	# Nuages qui dérivent
	for n in nuages:
		n.position.x += delta * 0.6
		if n.position.x > WORLD + 40:
			n.position.x = -WORLD - 40

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
			var res := resoudre_collisions(new_pos.x, new_pos.z, 0.45)
			new_pos.x = res.x
			new_pos.z = res.y
			# Les monstres sont solides eux aussi (on ne passe plus au travers)
			for e in enemies:
				if not e.alive:
					continue
				var en: Node3D = e.node
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

	# --- GRAVITE / SOL ---
	var sol := hauteur_terrain(player_node.global_position.x, player_node.global_position.z)
	player_vel_y -= 20 * delta
	player_node.global_position.y += player_vel_y * delta
	if player_node.global_position.y <= sol:
		player_node.global_position.y = sol
		player_vel_y = 0
		player_on_ground = true

	# --- ANIMATION MARCHE ---
	if is_moving:
		player_walk_time += delta * 9
		var swing := sin(player_walk_time) * 0.55
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
		if bras_droit:
			bras_droit.rotation.x = lerpf(-2.5, 0.7, k)

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

	if event.is_action_pressed("jump") and player_on_ground and not player_dead:
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
	# Colline du château au nord
	var dc := Vector2(x, z + 78.0).length()
	h += maxf(0.0, 10.0 - dc * 0.22)
	return h * t

var CHEMIN: Array[Vector2] = [
	Vector2(0, 30), Vector2(3, 18), Vector2(-2, 6), Vector2(1, -8),
	Vector2(4, -20), Vector2(-1, -34), Vector2(1, -48), Vector2(3, -60), Vector2(0, -72),
]

func dist_chemin(p: Vector2) -> float:
	var best := 1e9
	for i in range(CHEMIN.size() - 1):
		var a := CHEMIN[i]
		var b := CHEMIN[i + 1]
		var ab := b - a
		var t := clampf((p - a).dot(ab) / maxf(ab.length_squared(), 0.001), 0.0, 1.0)
		best = minf(best, (p - (a + ab * t)).length())
	return best

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
func col_cercle(x: float, z: float, r: float):
	colliders.append({"t": "c", "x": x, "z": z, "r": r, "g": r + 1.0})

func col_boite(x: float, z: float, w: float, d: float):
	colliders.append({"t": "b", "x": x, "z": z, "w": w, "d": d, "g": maxf(w, d) * 0.5 + 1.0})

func resoudre_collisions(px: float, pz: float, rayon: float) -> Vector2:
	var p := Vector2(px, pz)
	for _passe in range(2):
		for c in colliders:
			var cx: float = c.x
			var cz: float = c.z
			var g: float = c.g
			if absf(p.x - cx) > g + rayon and absf(p.y - cz) > g + rayon:
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
		# Clôture du village : anneau bloquant SAUF aux portails (route)
		var dl := p.length()
		if absf(dl - VILLAGE_R) < 0.4 + rayon and dist_chemin(p) > 3.4:
			if dl < 0.001:
				p = Vector2(VILLAGE_R + 0.4 + rayon, 0)
			elif dl >= VILLAGE_R:
				p = p / dl * (VILLAGE_R + 0.4 + rayon)
			else:
				p = p / dl * (VILLAGE_R - 0.4 - rayon)
	return p

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
			var col := Color(0.33, 0.60, 0.20)
			col = col.lightened(clampf(cy * 0.03, 0.0, 0.18))
			var v := rng.randf_range(-0.045, 0.045)
			col = Color(col.r + v, col.g + v * 0.8, col.b + v * 0.5)
			if dist_chemin(Vector2(cx, cz)) < 2.4:
				col = Color(0.62, 0.50, 0.33).lightened(rng.randf_range(-0.04, 0.04))
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
			var y := hauteur_terrain(x, z)
			var tr := Transform3D()
			tr = tr.rotated(Vector3.UP, rng2.randf_range(0, TAU))
			tr = tr.scaled(Vector3.ONE * rng2.randf_range(0.8, 1.6))
			tr.origin = Vector3(x, y - 0.02, z)
			mm.set_instance_transform(k, tr)

	# Fleurs (MultiMesh)
	var fleur := SphereMesh.new()
	fleur.radius = 0.09
	fleur.height = 0.18
	fleur.radial_segments = 6
	fleur.rings = 3
	var rng3 := RandomNumberGenerator.new()
	rng3.seed = 12
	for ni in range(2):
		var colf := Color(1.0, 0.85, 0.2) if ni == 0 else Color(0.95, 0.95, 0.9)
		var mm := MultiMesh.new()
		mm.transform_format = MultiMesh.TRANSFORM_3D
		mm.mesh = fleur
		mm.instance_count = 70
		var mmi := MultiMeshInstance3D.new()
		mmi.multimesh = mm
		mmi.material_override = mat_std(colf)
		add_child(mmi)
		for k in range(70):
			var x := rng3.randf_range(-WORLD + 6, WORLD - 6)
			var z := rng3.randf_range(-WORLD + 6, WORLD - 6)
			if Vector2(x, z).length() < TOWN_R * 0.5:
				x += TOWN_R * 0.8
			x = clampf(x, -WORLD + 6, WORLD - 6)
			z = clampf(z, -WORLD + 6, WORLD - 6)
			var tr := Transform3D()
			tr.origin = Vector3(x, hauteur_terrain(x, z) + 0.10, z)
			tr = tr.scaled(Vector3.ONE * rng3.randf_range(0.7, 1.3))
			mm.set_instance_transform(k, tr)

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
			var s := rng4.randf_range(0.4, 1.5)
			var tr := Transform3D()
			tr = tr.rotated(Vector3.UP, rng4.randf_range(0, TAU))
			tr = tr.scaled(Vector3(s * rng4.randf_range(0.8, 1.3), s * 0.75, s))
			tr.origin = Vector3(x, hauteur_terrain(x, z) + s * 0.18, z)
			mm.set_instance_transform(k, tr)
			# Collision, sauf si le rocher est posé sur la route
			if dist_chemin(Vector2(x, z)) > 2.8:
				col_cercle(x, z, 0.42 * s)

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
	sm.sky_top_color = Color(0.22, 0.51, 0.90)
	sm.sky_horizon_color = Color(0.62, 0.83, 0.97)
	sm.ground_bottom_color = Color(0.35, 0.55, 0.25)
	sm.ground_horizon_color = Color(0.62, 0.83, 0.97)
	sky.sky_material = sm
	env.sky = sky
	env.ambient_light_source = Environment.AMBIENT_SOURCE_SKY
	env.ambient_light_energy = 1.0
	env.tonemap_mode = Environment.TONE_MAPPER_FILMIC
	# Couleurs globales adoucies (saturation) + réglages luminosité/contraste
	monde_env = env
	env.adjustment_enabled = true
	env.adjustment_saturation = 0.85
	appliquer_reglages_visuels()
	get_viewport().world_3d.environment = env

	# Soleil (le DirectionalLight3D de main.tscn)
	var sun: DirectionalLight3D = $DirectionalLight3D
	sun.rotation = Vector3(deg_to_rad(-52), deg_to_rad(-32), 0)
	sun.light_color = Color(1.0, 0.96, 0.88)
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
	mi.position = pos
	mi.rotation = rot
	(parent if parent else self).add_child(mi)
	return mi

func _cyl(pos: Vector3, r_bot: float, r_top: float, h: float, col: Color, parent: Node = null, seg := 10, rot := Vector3.ZERO) -> MeshInstance3D:
	var key := "cyl%.2f_%.2f_%.2f_%d" % [r_bot, r_top, h, seg]
	if not _mesh_cache.has(key):
		var c := CylinderMesh.new()
		c.radius_bottom = r_bot
		c.radius_top = r_top
		c.height = h
		c.radial_segments = seg
		c.rings = 1
		_mesh_cache[key] = c
	var mi := MeshInstance3D.new()
	mi.mesh = _mesh_cache[key]
	mi.material_override = mat_std(col)
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
	mi.position = pos
	mi.rotation = rot
	(parent if parent else self).add_child(mi)
	return mi

# ============================================================
# VILLE
# ============================================================
func creer_ville():
	var PIERRE := Color(0.50, 0.48, 0.45)
	var data = [
		{"x":-10,"z":-6,"w":5,"h":4,"d":4,"c":Color(0.85,0.74,0.56),"n":"Supermarche","roof":Color(0.72,0.28,0.12)},
		{"x":-10,"z":3,"w":4,"h":3.5,"d":3.5,"c":Color(0.80,0.66,0.45),"n":"Armurerie","roof":Color(0.45,0.45,0.48)},
		{"x":10,"z":-6,"w":4,"h":3.5,"d":3.5,"c":Color(0.60,0.78,0.66),"n":"Vetements","roof":Color(0.62,0.38,0.20)},
		{"x":10,"z":3,"w":4,"h":4,"d":4,"c":Color(0.82,0.66,0.42),"n":"Auberge","roof":Color(0.68,0.24,0.10)},
		{"x":0,"z":-15,"w":8,"h":6,"d":6,"c":Color(0.88,0.85,0.76),"n":"Mairie","roof":Color(0.32,0.47,0.58)},
		{"x":-20,"z":-12,"w":4,"h":3.5,"d":3.5,"c":Color(0.83,0.70,0.52),"n":"Maison","roof":Color(0.68,0.24,0.10)},
		{"x":-20,"z":-4,"w":3.5,"h":3,"d":3.5,"c":Color(0.78,0.64,0.46),"n":"Maison","roof":Color(0.60,0.32,0.16)},
		{"x":20,"z":8,"w":4,"h":3.5,"d":3.5,"c":Color(0.83,0.70,0.52),"n":"Maison","roof":Color(0.68,0.24,0.10)},
		{"x":20,"z":-4,"w":3.5,"h":3,"d":3.5,"c":Color(0.78,0.64,0.46),"n":"Maison","roof":Color(0.60,0.32,0.16)},
	]
	for b in data:
		var y := hauteur_terrain(b.x, b.z)
		# Murs
		_box(Vector3(b.x, y + b.h / 2.0, b.z), Vector3(b.w, b.h, b.d), b.c)
		# Fondations pierre
		_box(Vector3(b.x, y + 0.1, b.z), Vector3(b.w + 0.25, 0.25, b.d + 0.25), PIERRE)
		# Toit en prisme (pignon) + débords
		var rh: float = b.d * 0.42
		_prism(Vector3(b.x, y + b.h + rh / 2.0 - 0.05, b.z), Vector3(b.d + 0.5, rh, b.w + 0.5), b.roof, null, Vector3(0, deg_to_rad(90), 0))
		# Porte + linteau
		_box(Vector3(b.x, y + b.h * 0.28, b.z + b.d / 2.0 + 0.06), Vector3(b.w * 0.22, b.h * 0.52, 0.14), Color(0.25, 0.14, 0.06))
		_box(Vector3(b.x, y + b.h * 0.56, b.z + b.d / 2.0 + 0.06), Vector3(b.w * 0.28, 0.08, 0.16), Color(0.38, 0.22, 0.09))
		# Fenêtres façade (cadre + vitre + croix)
		for fx in [-0.28, 0.28]:
			_box(Vector3(b.x + b.w * fx, y + b.h * 0.60, b.z + b.d / 2.0 + 0.06), Vector3(b.w * 0.18, b.h * 0.20, 0.12), Color(0.32, 0.21, 0.10))
			_box(Vector3(b.x + b.w * fx, y + b.h * 0.60, b.z + b.d / 2.0 + 0.08), Vector3(b.w * 0.13, b.h * 0.14, 0.08), Color(0.55, 0.80, 0.95))
			_box(Vector3(b.x + b.w * fx, y + b.h * 0.60, b.z + b.d / 2.0 + 0.10), Vector3(b.w * 0.13, 0.03, 0.03), Color(0.32, 0.21, 0.10))
			_box(Vector3(b.x + b.w * fx, y + b.h * 0.60, b.z + b.d / 2.0 + 0.10), Vector3(0.03, b.h * 0.14, 0.03), Color(0.32, 0.21, 0.10))
		# Fenêtres côtés
		for fz in [-0.28, 0.28]:
			_box(Vector3(b.x + b.w / 2.0 + 0.06, y + b.h * 0.60, b.z + b.d * fz), Vector3(0.12, b.h * 0.16, b.d * 0.13), Color(0.55, 0.80, 0.95))
			_box(Vector3(b.x - b.w / 2.0 - 0.06, y + b.h * 0.60, b.z + b.d * fz), Vector3(0.12, b.h * 0.16, b.d * 0.13), Color(0.55, 0.80, 0.95))
		# Auvent des commerces
		if b.n in ["Supermarche", "Armurerie", "Vetements", "Auberge"]:
			_box(Vector3(b.x, y + b.h * 0.44, b.z + b.d / 2.0 + 0.9), Vector3(b.w + 0.4, 0.1, 1.8), Color(0.75, 0.38, 0.12))
			_box(Vector3(b.x - b.w / 2.0 + 0.1, y + b.h * 0.22, b.z + b.d / 2.0 + 1.7), Vector3(0.12, b.h * 0.42, 0.12), Color(0.42, 0.26, 0.10))
			_box(Vector3(b.x + b.w / 2.0 - 0.1, y + b.h * 0.22, b.z + b.d / 2.0 + 1.7), Vector3(0.12, b.h * 0.42, 0.12), Color(0.42, 0.26, 0.10))
		# Cheminée des maisons
		if b.n == "Maison":
			_box(Vector3(b.x + b.w * 0.3, y + b.h + rh * 0.5, b.z + b.d * 0.3), Vector3(0.4, 0.9, 0.4), Color(0.62, 0.32, 0.16))
			_box(Vector3(b.x + b.w * 0.3, y + b.h + rh * 0.5 + 0.5, b.z + b.d * 0.3), Vector3(0.52, 0.1, 0.52), Color(0.52, 0.26, 0.12))
		# Escalier de la Mairie
		if b.n == "Mairie":
			for step in range(3):
				_box(Vector3(b.x, y + 0.06 + step * 0.1, b.z + b.d / 2.0 + 0.6 + step * 0.35),
					Vector3(b.w * 0.6, 0.12, 0.35), Color(0.60, 0.58, 0.55))
		batiments.append({"x": b.x, "z": b.z, "w": b.w + 0.5, "d": b.d + 0.5})
		col_boite(b.x, b.z, b.w + 0.5, b.d + 0.5)

	# PNJ (villageois low-poly)
	var pnj = [
		{"x":-10,"z":-2.5,"c":Color(0.80,0.20,0.16),"n":"Vendeur"},
		{"x":10,"z":5.5,"c":Color(0.16,0.68,0.36),"n":"Forgeron"},
		{"x":0,"z":-11,"c":Color(0.50,0.24,0.62),"n":"Maire"},
		{"x":10.5,"z":-2.5,"c":Color(0.82,0.42,0.08),"n":"Marchand"},
	]
	for d in pnj:
		var y := hauteur_terrain(d.x, d.z)
		var root := Node3D.new()
		root.position = Vector3(d.x, y, d.z)
		add_child(root)
		col_cercle(d.x, d.z, 0.4)
		_caps(Vector3(0, 0.55, 0), 0.19, 0.62, d.c, root)              # corps
		_prism(Vector3(0, 0.28, 0), Vector3(0.44, 0.30, 0.44), d.c.darkened(0.15), root)  # jupe
		_sph(Vector3(0, 1.02, 0), 0.17, Color(0.93, 0.78, 0.62), root) # tête
		_sph(Vector3(0, 1.12, 0), 0.16, d.c.darkened(0.3), root)       # chapeau/cheveux
		_caps(Vector3(-0.24, 0.62, 0), 0.07, 0.42, d.c.darkened(0.1), root)
		_caps(Vector3(0.24, 0.62, 0), 0.07, 0.42, d.c.darkened(0.1), root)
		var label := Label3D.new()
		label.text = d.n
		label.position = Vector3(0, 1.55, 0)
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
func creer_chateau():
	var cx := 0.0
	var cz := -78.0
	var y := hauteur_terrain(cx, cz)
	var PIERRE := Color(0.80, 0.76, 0.68)
	var PIERRE_F := Color(0.70, 0.66, 0.58)
	var TOIT := Color(0.78, 0.26, 0.14)
	var root := Node3D.new()
	root.position = Vector3(cx, y, cz)
	add_child(root)

	# Plateforme
	_box(Vector3(0, 0.5, 0), Vector3(34, 2.0, 20), PIERRE_F, root)
	# Enceinte
	_box(Vector3(0, 3.5, -9), Vector3(32, 5, 1.6), PIERRE, root)
	_box(Vector3(-16, 3.5, 0), Vector3(1.6, 5, 20), PIERRE, root)
	_box(Vector3(16, 3.5, 0), Vector3(1.6, 5, 20), PIERRE, root)
	_box(Vector3(-8, 3.5, 9), Vector3(16, 5, 1.6), PIERRE, root)
	_box(Vector3(8, 3.5, 9), Vector3(16, 5, 1.6), PIERRE, root)
	# Créneaux
	for i in range(-15, 16, 3):
		_box(Vector3(i, 6.4, -9), Vector3(1.2, 0.9, 1.8), PIERRE, root)
		_box(Vector3(i, 6.4, 9), Vector3(1.2, 0.9, 1.8), PIERRE, root)
	for i in range(-8, 9, 3):
		_box(Vector3(-16, 6.4, i), Vector3(1.8, 0.9, 1.2), PIERRE, root)
		_box(Vector3(16, 6.4, i), Vector3(1.8, 0.9, 1.2), PIERRE, root)
	# Porte
	_box(Vector3(-1.6, 2.6, 9), Vector3(0.8, 3.4, 2.0), Color(0.30, 0.18, 0.08), root)
	_box(Vector3(1.6, 2.6, 9), Vector3(0.8, 3.4, 2.0), Color(0.30, 0.18, 0.08), root)
	_box(Vector3(0, 4.6, 9), Vector3(4.0, 0.9, 2.0), Color(0.30, 0.18, 0.08), root)
	# Tours d'angle + donjon
	var tours: Array[Vector3] = [Vector3(-16, 0, -9), Vector3(16, 0, -9), Vector3(-16, 0, 9), Vector3(16, 0, 9), Vector3(-6, 0, -4), Vector3(6, 0, -4)]
	for k in range(tours.size()):
		var t := tours[k]
		var hh := 9.0 if k < 4 else 12.0
		var rr := 2.2 if k < 4 else 2.8
		_cyl(Vector3(t.x, hh / 2.0 + 1.0, t.z), rr, rr * 0.9, hh, PIERRE, root, 8)
		_cone(Vector3(t.x, hh + 1.0 + 2.2, t.z), rr + 0.5, 4.4, TOIT, root, 8)
		# Drapeau
		_cyl(Vector3(t.x, hh + 5.2, t.z), 0.06, 0.06, 1.8, Color(0.35, 0.22, 0.10), root, 6)
		_box(Vector3(t.x + 0.5, hh + 5.8, t.z), Vector3(1.0, 0.6, 0.06), Color(0.85, 0.15, 0.15), root)
	# Donjon central
	_box(Vector3(0, 6.5, -4), Vector3(9, 9, 7), PIERRE, root)
	_prism(Vector3(0, 12.5, -4), Vector3(8, 3.0, 10), TOIT, root, Vector3(0, deg_to_rad(90), 0))
	# Fenêtres du donjon
	for fx in [-2.5, 0.0, 2.5]:
		_box(Vector3(fx, 7.5, -0.4), Vector3(0.8, 1.6, 0.3), Color(0.25, 0.30, 0.42), root)
	# Collisions du château (coordonnées monde) — porte sud laissée passable
	col_boite(0, -87, 32, 1.6)
	col_boite(-16, -78, 1.6, 20)
	col_boite(16, -78, 1.6, 20)
	col_boite(-9.1, -69, 13.8, 1.6)
	col_boite(9.1, -69, 13.8, 1.6)
	col_cercle(-16, -87, 2.3)
	col_cercle(16, -87, 2.3)
	col_cercle(-16, -69, 2.3)
	col_cercle(16, -69, 2.3)
	col_cercle(-6, -82, 2.9)
	col_cercle(6, -82, 2.9)
	col_boite(0, -82, 9, 7)

# ============================================================
# FONTAINE / ARBRES / PROPS / NUAGE
# ============================================================
func creer_fontaine():
	var y := hauteur_terrain(0, 0)
	col_cercle(0, 0, 2.6)
	_cyl(Vector3(0, y + 0.45, 0), 2.5, 2.3, 0.9, Color(0.62, 0.61, 0.60), self, 12)
	_cyl(Vector3(0, y + 0.85, 0), 2.1, 2.1, 0.25, Color(0.18, 0.58, 0.88), self, 12)
	_cyl(Vector3(0, y + 1.7, 0), 0.32, 0.26, 2.2, Color(0.66, 0.65, 0.64), self, 8)
	_sph(Vector3(0, y + 2.9, 0), 0.42, Color(0.66, 0.65, 0.64), self)
	_cyl(Vector3(0, y + 1.05, 0), 0.10, 0.16, 0.9, Color(0.55, 0.80, 0.95), self, 6)

func creer_arbres():
	var pins := [
		[-6, 6], [6, 6], [-6, -6], [6, -6], [-9, 13], [9, 13], [-9, -13], [9, -13],
		[-14, 9], [14, -9], [-17, 7], [17, -7], [0, 15], [0, -15], [15, 0], [-15, 0],
		[-26, 18], [26, 18], [-26, -20], [26, -20], [-34, 0], [34, 0], [5, 29], [-12, 26],
		[12, 26], [-40, 30], [40, 30], [-45, -35], [45, -35], [-55, 10], [55, 10],
		[-60, -50], [60, -50], [-70, 40], [70, 40], [0, -50], [-20, -45], [20, -45],
	]
	for p in pins:
		creer_pin(p[0], p[1])
	# Quelques arbres ronds
	for p in [[-12, 2], [12, 2], [-24, -8], [24, 8], [-30, 30], [30, -30]]:
		creer_arbre_rond(p[0], p[1])

func creer_pin(x: float, z: float):
	var y := hauteur_terrain(x, z)
	col_cercle(x, z, 0.4)
	var s := randf_range(0.8, 1.5)
	var root := Node3D.new()
	root.position = Vector3(x, y, z)
	root.scale = Vector3.ONE * s
	add_child(root)
	_cyl(Vector3(0, 0.9, 0), 0.16, 0.12, 1.8, Color(0.42, 0.28, 0.14), root, 6)
	_cone(Vector3(0, 2.4, 0), 1.35, 2.4, Color(0.13, 0.42, 0.16), root, 7)
	_cone(Vector3(0, 3.5, 0), 1.05, 2.1, Color(0.16, 0.50, 0.19), root, 7)
	_cone(Vector3(0, 4.5, 0), 0.72, 1.8, Color(0.20, 0.58, 0.22), root, 7)

func creer_arbre_rond(x: float, z: float):
	var y := hauteur_terrain(x, z)
	col_cercle(x, z, 0.45)
	var root := Node3D.new()
	root.position = Vector3(x, y, z)
	add_child(root)
	_cyl(Vector3(0, 1.1, 0), 0.18, 0.14, 2.2, Color(0.42, 0.28, 0.14), root, 6)
	_facette(Vector3(0, 2.9, 0), Color(0.22, 0.58, 0.20), root, Vector3(2.4, 2.0, 2.4))
	_facette(Vector3(0.6, 2.4, 0.4), Color(0.18, 0.50, 0.17), root, Vector3(1.4, 1.2, 1.4))

func creer_props():
	# Lampadaires / lanternes
	for p in [[-3, 6], [3, 6], [-3, -6], [3, -6], [-10, 0], [10, 0], [0, 10], [0, -10]]:
		var y := hauteur_terrain(p[0], p[1])
		var root := Node3D.new()
		root.position = Vector3(p[0], y, p[1])
		add_child(root)
		col_cercle(p[0], p[1], 0.25)
		_box(Vector3(0, 0.05, 0), Vector3(0.5, 0.12, 0.5), Color(0.20, 0.20, 0.22), root)
		_box(Vector3(0, 1.5, 0), Vector3(0.14, 3.0, 0.14), Color(0.16, 0.16, 0.18), root)
		_box(Vector3(0, 3.05, 0), Vector3(0.5, 0.1, 0.1), Color(0.16, 0.16, 0.18), root)
		var lan := _box(Vector3(0.22, 2.75, 0), Vector3(0.26, 0.4, 0.26), Color(1.0, 0.80, 0.30), root)
		lan.material_override = mat_std(Color(1.0, 0.80, 0.30), false, true)
		_cone(Vector3(0.22, 3.05, 0), 0.2, 0.25, Color(0.16, 0.16, 0.18), root, 4)
	# Barils + caisses près de l'auberge et du supermarché
	for p in [[8.2, 5.6], [8.7, 6.2], [-8.0, -3.6], [-8.6, -4.1], [12.4, 1.0]]:
		var y := hauteur_terrain(p[0], p[1])
		_cyl(Vector3(p[0], y + 0.45, p[1]), 0.34, 0.30, 0.9, Color(0.55, 0.36, 0.18), self, 10)
		_cyl(Vector3(p[0], y + 0.62, p[1]), 0.36, 0.36, 0.1, Color(0.30, 0.28, 0.28), self, 10)
		col_cercle(p[0], p[1], 0.45)
	for p in [[-12.6, -4.4], [12.6, 4.6], [-12.4, 4.8]]:
		var y := hauteur_terrain(p[0], p[1])
		_box(Vector3(p[0], y + 0.35, p[1]), Vector3(0.7, 0.7, 0.7), Color(0.62, 0.45, 0.24))
		_box(Vector3(p[0], y + 0.36, p[1]), Vector3(0.72, 0.12, 0.72), Color(0.48, 0.34, 0.17))
		col_boite(p[0], p[1], 0.8, 0.8)
	# Clôtures le long des routes
	for i in range(-6, 7):
		if abs(i) < 2: continue
		var y := hauteur_terrain(i * 2.4, 4.6)
		_box(Vector3(i * 2.4, y + 0.45, 4.6), Vector3(0.1, 0.9, 0.1), Color(0.52, 0.36, 0.18))
		_box(Vector3(i * 2.4, y + 0.7, 4.6), Vector3(2.4, 0.09, 0.07), Color(0.58, 0.41, 0.21))
	col_boite(-9.6, 4.6, 12.0, 0.25)
	col_boite(9.6, 4.6, 12.0, 0.25)
	# Panneaux / bannières (comme le screen)
	creer_banniere(-5.5, -3.0, Color(0.16, 0.30, 0.62))
	creer_banniere(6.0, 8.0, Color(0.55, 0.16, 0.16))
	creer_panneau(-4.0, 8.5)
	creer_panneau(5.0, -8.5)

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
		var y := hauteur_terrain(x, z)
		# Poteau
		_box(Vector3(x, y + 0.45, z), Vector3(0.12, 0.9, 0.12), BOIS)
		# Traverses entre deux poteaux consécutifs
		if absf(a - dernier_angle - step) < 0.001:
			var ap := a - step
			var mx := (cos(ap) + cos(a)) * 0.5 * VILLAGE_R
			var mz := (sin(ap) + sin(a)) * 0.5 * VILLAGE_R
			var my := hauteur_terrain(mx, mz)
			var ry := atan2(-(sin(a) - sin(ap)), cos(a) - cos(ap))
			var long := step * VILLAGE_R + 0.14
			_box(Vector3(mx, my + 0.72, mz), Vector3(long, 0.09, 0.07), BOIS_CLAIR, null, Vector3(0, ry, 0))
			_box(Vector3(mx, my + 0.38, mz), Vector3(long, 0.09, 0.07), BOIS_CLAIR, null, Vector3(0, ry, 0))
		dernier_angle = a
	# Portails : grands poteaux + linteau + lanterne au-dessus de la route
	for trou in trous:
		var a0: float = trou.a0
		var a1: float = trou.a1
		for ga in [a0, a1]:
			var ag: float = ga
			var gx := cos(ag) * VILLAGE_R
			var gz := sin(ag) * VILLAGE_R
			var gy := hauteur_terrain(gx, gz)
			_box(Vector3(gx, gy + 1.3, gz), Vector3(0.26, 2.6, 0.26), BOIS.darkened(0.1))
			_box(Vector3(gx, gy + 2.66, gz), Vector3(0.36, 0.12, 0.36), BOIS_CLAIR)
		var d01 := Vector2(cos(a1) - cos(a0), sin(a1) - sin(a0))
		var am := (a0 + a1) * 0.5
		var mx2 := cos(am) * VILLAGE_R
		var mz2 := sin(am) * VILLAGE_R
		var my2 := hauteur_terrain(mx2, mz2)
		var ry2 := atan2(-d01.y, d01.x)
		var long2 := d01.length() * VILLAGE_R + 0.2
		_box(Vector3(mx2, my2 + 2.6, mz2), Vector3(long2, 0.16, 0.14), BOIS_CLAIR, null, Vector3(0, ry2, 0))
		var lan := _box(Vector3(mx2, my2 + 2.36, mz2), Vector3(0.24, 0.34, 0.24), Color(1.0, 0.80, 0.30))
		lan.material_override = mat_std(Color(1.0, 0.80, 0.30), false, true)
		_cone(Vector3(mx2, my2 + 2.60, mz2), 0.19, 0.22, Color(0.16, 0.16, 0.18), null, 4)
		# Garde posté à côté du portail (côté village)
		var gx2 := cos(a0) * VILLAGE_R * 0.90
		var gz2 := sin(a0) * VILLAGE_R * 0.90
		var garde := creer_garde(gx2, gz2)
		portes.append({"x": mx2, "z": mz2, "garde": garde, "cd": 0.0})

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
	_caps(Vector3(0, 0.62, 0), 0.21, 0.7, TUNIQUE, root)                            # corps
	_prism(Vector3(0, 0.30, 0), Vector3(0.48, 0.32, 0.48), ACIER.darkened(0.2), root)  # jupe d'armure
	_sph(Vector3(0, 1.12, 0), 0.17, Color(0.93, 0.78, 0.62), root)                  # tête
	_cone(Vector3(0, 1.28, 0), 0.20, 0.26, ACIER, root, 8)                          # casque
	_box(Vector3(0, 1.16, 0.02), Vector3(0.36, 0.06, 0.38), ACIER, root)            # bord du casque
	_caps(Vector3(-0.27, 0.68, 0), 0.075, 0.46, TUNIQUE.darkened(0.15), root)       # bras
	_caps(Vector3(0.27, 0.68, 0), 0.075, 0.46, TUNIQUE.darkened(0.15), root)
	_cyl(Vector3(0.38, 1.0, 0), 0.035, 0.03, 2.2, Color(0.42, 0.28, 0.14), root, 6) # hallebarde
	_box(Vector3(0.38, 2.0, 0), Vector3(0.08, 0.42, 0.16), Color(0.75, 0.77, 0.80), root)
	_cone(Vector3(0.38, 2.3, 0), 0.07, 0.24, Color(0.80, 0.82, 0.85), root, 6)
	var label := Label3D.new()
	label.text = "Garde"
	label.position = Vector3(0, 1.75, 0)
	label.font_size = 20
	label.pixel_size = 0.005
	label.billboard = BaseMaterial3D.BILLBOARD_ENABLED
	label.modulate = Color(0.7, 0.85, 1, 1)
	label.outline_size = 8
	label.outline_modulate = Color(0.1, 0.1, 0.1, 0.9)
	root.add_child(label)
	col_cercle(x, z, 0.4)
	return root

func creer_banniere(x: float, z: float, col: Color):
	var y := hauteur_terrain(x, z)
	var root := Node3D.new()
	root.position = Vector3(x, y, z)
	add_child(root)
	col_cercle(x, z, 0.22)
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
	col_cercle(x, z, 0.18)
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
	for j in [jambe_gauche, jambe_droite]:
		_caps(Vector3(0, -0.26, 0), 0.095, 0.52, TUNIQUE_F, j)
		_box(Vector3(0, -0.55, -0.03), Vector3(0.17, 0.16, 0.26), BOTTE, j)

	# Torse + tunique
	_box(Vector3(0, 0.98, 0), Vector3(0.44, 0.52, 0.28), TUNIQUE, player_node)
	_prism(Vector3(0, 0.66, 0), Vector3(0.52, 0.26, 0.36), TUNIQUE, player_node)
	_box(Vector3(0, 0.80, 0), Vector3(0.46, 0.10, 0.30), CUIR, player_node)
	# Baudrier
	_box(Vector3(0, 1.0, 0), Vector3(0.46, 0.09, 0.30), CUIR, player_node, Vector3(0, 0, deg_to_rad(35)))
	# Épaules
	_sph(Vector3(-0.27, 1.18, 0), 0.11, TUNIQUE, player_node)
	_sph(Vector3(0.27, 1.18, 0), 0.11, TUNIQUE, player_node)

	# Bras (pivots)
	bras_gauche = Node3D.new(); bras_gauche.position = Vector3(-0.27, 1.16, 0); player_node.add_child(bras_gauche)
	bras_droit = Node3D.new(); bras_droit.position = Vector3(0.27, 1.16, 0); player_node.add_child(bras_droit)
	for b in [bras_gauche, bras_droit]:
		_caps(Vector3(0, -0.20, 0), 0.075, 0.40, TUNIQUE, b)
		_sph(Vector3(0, -0.42, 0), 0.075, PEAU, b)

	# Tête + cheveux piquants
	var tete := Node3D.new()
	tete.position = Vector3(0, 1.42, 0)
	player_node.add_child(tete)
	_box(Vector3(0, 0, 0), Vector3(0.30, 0.30, 0.28), PEAU, tete)
	_box(Vector3(0, 0.16, 0.02), Vector3(0.32, 0.14, 0.30), CHEVEUX, tete)
	_box(Vector3(0, 0.10, -0.14), Vector3(0.30, 0.12, 0.06), CHEVEUX, tete)
	_prism(Vector3(-0.09, 0.28, 0.04), Vector3(0.12, 0.18, 0.12), CHEVEUX, tete, Vector3(0, 0, deg_to_rad(15)))
	_prism(Vector3(0.07, 0.30, -0.02), Vector3(0.12, 0.20, 0.12), CHEVEUX, tete, Vector3(0, 0, deg_to_rad(-12)))
	_prism(Vector3(0.0, 0.27, 0.10), Vector3(0.10, 0.16, 0.10), CHEVEUX, tete, Vector3(0, 0, deg_to_rad(5)))
	# Yeux
	_box(Vector3(-0.07, 0.02, -0.145), Vector3(0.045, 0.05, 0.02), Color(0.12, 0.12, 0.12), tete)
	_box(Vector3(0.07, 0.02, -0.145), Vector3(0.045, 0.05, 0.02), Color(0.12, 0.12, 0.12), tete)

	# Marteau (main droite)
	var marteau := Node3D.new()
	marteau.position = Vector3(0, -0.44, 0)
	bras_droit.add_child(marteau)
	_cyl(Vector3(0, -0.15, 0), 0.035, 0.035, 0.75, Color(0.48, 0.32, 0.16), marteau, 6)
	_box(Vector3(0, -0.5, 0), Vector3(0.20, 0.24, 0.34), Color(0.55, 0.55, 0.58), marteau)
	_box(Vector3(0, -0.5, 0), Vector3(0.24, 0.10, 0.36), Color(0.35, 0.24, 0.12), marteau)

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
	var CORPS := Color(0.32, 0.26, 0.24) if gros else Color(0.36, 0.33, 0.32)
	root.scale = Vector3.ONE * s
	_facette(Vector3(0, 0.30, 0.05), CORPS, root, Vector3(0.62, 0.44, 0.80))
	_facette(Vector3(0, 0.34, -0.42), CORPS.lightened(0.08), root, Vector3(0.34, 0.28, 0.36))
	# Museau
	_cone(Vector3(0, 0.30, -0.66), 0.10, 0.22, Color(0.85, 0.55, 0.55), root, 5)
	# Oreilles
	_sph(Vector3(-0.14, 0.52, -0.34), 0.10, Color(0.55, 0.40, 0.40), root)
	_sph(Vector3(0.14, 0.52, -0.34), 0.10, Color(0.55, 0.40, 0.40), root)
	# Yeux rouges
	_sph(Vector3(-0.10, 0.38, -0.55), 0.035, Color(1, 0.05, 0.05), root, true)
	_sph(Vector3(0.10, 0.38, -0.55), 0.035, Color(1, 0.05, 0.05), root, true)
	# Pattes
	for px in [-0.2, 0.2]:
		for pz in [-0.25, 0.3]:
			_box(Vector3(px, 0.10, pz), Vector3(0.08, 0.20, 0.08), CORPS.darkened(0.2), root)
	# Queue
	_cyl(Vector3(0, 0.26, 0.62), 0.030, 0.012, 0.7, Color(0.85, 0.55, 0.60), root, 5, Vector3(deg_to_rad(100), 0, 0))

func _construire_araignee(root: Node3D):
	var CORPS := Color(0.20, 0.12, 0.10)
	_facette(Vector3(0, 0.55, 0.25), CORPS, root, Vector3(0.62, 0.52, 0.70))
	_facette(Vector3(0, 0.50, -0.25), CORPS.lightened(0.1), root, Vector3(0.36, 0.32, 0.36))
	# Yeux rouges
	for ex in [-0.10, -0.03, 0.04, 0.11]:
		_sph(Vector3(ex, 0.55, -0.42), 0.03, Color(1, 0.05, 0.05), root, true)
	# 8 pattes (2 segments)
	for cote in [-1, 1]:
		for k in range(4):
			var a := deg_to_rad(-50 + k * 33)
			var hx: float = cos(a) * 0.35 * cote
			var hz := sin(a) * 0.35 - 0.1
			var patte := Node3D.new()
			patte.position = Vector3(hx * 0.6, 0.5, hz)
			patte.rotation.y = -atan2(hz, hx * cote) * cote
			root.add_child(patte)
			_box(Vector3(0.28 * cote, 0.16, 0), Vector3(0.56, 0.05, 0.05), CORPS.darkened(0.1), patte, Vector3(0, 0, deg_to_rad(-30) * cote))
			_box(Vector3(0.62 * cote, -0.12, 0), Vector3(0.5, 0.04, 0.04), CORPS.darkened(0.2), patte, Vector3(0, 0, deg_to_rad(40) * cote))

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
		if dist < 1.1 and e.cd <= 0 and not player_dead and not player_protected:
			e.cd = 1.2
			player_pv -= 6
			spawn_floater(pp + Vector3(0, 1.8, 0), "-6", Color(1, 0.3, 0.2))
			if player_pv <= 0:
				mourir()

# ============================================================
# GARDES DES PORTAILS : toute bébête à moins de 2 m de la porte
# se fait attaquer (25 dégâts / 0,8 s) jusqu'à mort.
# ============================================================
func update_gardes(delta: float):
	for p in portes:
		p.cd = float(p.cd) - delta
		if float(p.cd) > 0.0:
			continue
		var gx: float = p.x
		var gz: float = p.z
		var cible = null
		for e in enemies:
			if not e.alive:
				continue
			var en: Node3D = e.node
			var dq := Vector2(en.global_position.x - gx, en.global_position.z - gz)
			if dq.length() < 2.0:
				cible = e
				break
		if cible == null:
			continue
		p.cd = 0.8
		var monstre: Node3D = cible.node
		if is_instance_valid(p.garde):
			var gd: Node3D = p.garde
			if Vector2(monstre.global_position.x - gd.global_position.x, monstre.global_position.z - gd.global_position.z).length() > 0.05:
				gd.look_at(Vector3(monstre.global_position.x, gd.global_position.y, monstre.global_position.z), Vector3.UP)
		cible.pv = int(cible.pv) - 25
		spawn_floater(monstre.global_position + Vector3(0, 1.3, 0), "-25", Color(1, 0.85, 0.3))
		spawn_spark(monstre.global_position + Vector3(0, 0.6, 0))
		if int(cible.pv) <= 0:
			cible.alive = false
			monstre.visible = false
			show_info("Un garde du village a repoussé %s !" % cible.name)
			get_tree().create_timer(10.0).timeout.connect(_respawn_enemy.bind(cible))

func mourir():
	player_pv = 0
	player_dead = true
	show_info("Vous êtes mort... [R] pour renaître")

func renaitre():
	player_dead = false
	player_pv = PV_MAX
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
		var mi := _facette(Vector3(x, hauteur_terrain(x, z) + 0.14, z), Color(0.56, 0.54, 0.50), self, Vector3(0.28, 0.22, 0.28))
		cailloux_items.append({"node": mi, "gone": false})
	for i in range(6):
		var x := randf_range(-40, 40)
		var z := randf_range(-40, 40)
		var mi := _cyl(Vector3(x, hauteur_terrain(x, z) + 0.10, z), 0.14, 0.14, 0.035, Color(1, 0.84, 0.1), self, 12)
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
func attaquer():
	if player_dead or player_attack_cd > 0:
		return
	player_attack_cd = 0.5
	player_attack_anim = 0.3
	var vaincus := []
	for e in enemies:
		if not e.alive: continue
		var dist = player_node.global_position.distance_to(e.node.global_position)
		if dist < 3.5:
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
		if player_node.global_position.distance_to(a.node.global_position) < 3.0:
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

class BarreXP extends Control:
	var parent = null
	func _process(_d): queue_redraw()
	func _draw():
		var sb := StyleBoxFlat.new()
		sb.bg_color = Color(0.09, 0.08, 0.08, 0.88)
		sb.set_corner_radius_all(4)
		draw_style_box(sb, Rect2(Vector2.ZERO, Vector2(52, 22)))
		draw_string(ThemeDB.fallback_font, Vector2(6, 16), "LVL %d" % parent.level,
			HORIZONTAL_ALIGNMENT_LEFT, -1, 14, Color(1, 1, 1))
		var rect := Rect2(Vector2(58, 0), Vector2(size.x - 58, 22))
		var sb2 := StyleBoxFlat.new()
		sb2.bg_color = Color(0.10, 0.12, 0.20, 0.9)
		sb2.set_corner_radius_all(4)
		draw_style_box(sb2, rect)
		var r := clampf(float(parent.xp) / float(parent.xp_need), 0.0, 1.0)
		if r > 0.01:
			var sb3 := StyleBoxFlat.new()
			sb3.bg_color = Color(0.20, 0.55, 0.95)
			sb3.set_corner_radius_all(4)
			draw_style_box(sb3, Rect2(rect.position + Vector2(2, 2), Vector2((rect.size.x - 4) * r, rect.size.y - 4)))
		draw_string(ThemeDB.fallback_font, Vector2(rect.position.x + rect.size.x * 0.5 - 30, 16),
			"%d / %d" % [parent.xp, parent.xp_need], HORIZONTAL_ALIGNMENT_LEFT, -1, 13, Color(1, 1, 1))

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
	port.position = Vector2(14, 12)
	port.size = Vector2(62, 62)
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
	hp_bar.position = Vector2(84, 26)
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

	# ===== Quêtes (gauche) =====
	var quest := PanelContainer.new()
	quest.position = Vector2(14, 92)
	quest.size = Vector2(280, 96)
	var qs := StyleBoxFlat.new()
	qs.bg_color = Color(0.10, 0.09, 0.08, 0.90)
	qs.set_corner_radius_all(6)
	qs.border_width_bottom = 2; qs.border_width_top = 2
	qs.border_width_left = 2; qs.border_width_right = 2
	qs.border_color = Color(0.05, 0.04, 0.04)
	quest.add_theme_stylebox_override("panel", qs)
	canvas.add_child(quest)
	var qv := VBoxContainer.new()
	qv.add_theme_constant_override("separation", 3)
	quest.add_child(qv)
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

	# ===== Or + cailloux (haut droite) =====
	pill_or = Pill.new()
	pill_or.kind = "or"
	pill_or.parent = self
	pill_or.position = Vector2(1280 - 250 - 14, 20)
	pill_or.size = Vector2(110, 34)
	canvas.add_child(pill_or)
	pill_cailloux = Pill.new()
	pill_cailloux.kind = "cailloux"
	pill_cailloux.parent = self
	pill_cailloux.position = Vector2(1280 - 250 - 14 - 100, 20)
	pill_cailloux.size = Vector2(92, 34)
	canvas.add_child(pill_cailloux)

	# ===== Mini-carte =====
	minimap = MiniMap.new()
	minimap.parent = self
	minimap.position = Vector2(1280 - 148 - 14, 62)
	minimap.size = Vector2(148, 148)
	canvas.add_child(minimap)

	# ===== Hotbar =====
	hotbar = Hotbar.new()
	hotbar.parent = self
	hotbar.position = Vector2(640 - (5 * 56 + 4 * 8) / 2.0, 720 - 72)
	hotbar.size = Vector2(5 * 56 + 4 * 8, 56)
	canvas.add_child(hotbar)

	# ===== Barre d'XP =====
	barre_xp = BarreXP.new()
	barre_xp.parent = self
	barre_xp.position = Vector2(14, 720 - 36)
	barre_xp.size = Vector2(260, 22)
	canvas.add_child(barre_xp)

	# ===== Contrôles (bas) =====
	var ctrl := Label.new()
	ctrl.text = "[ZQSD/Flèches] Bouger  [MAJ] Courir  [ESPACE] Saut  [Clic] Attaque  [E] Ramasser  [ClicD] Caméra  [V] Vue  [1-5] Objets  [I] Inventaire  [O] Options  [ÉCHAP] Quitter"
	ctrl.position = Vector2(300, 720 - 18)
	ctrl.add_theme_font_size_override("font_size", 12)
	ctrl.add_theme_color_override("font_color", Color(0.75, 0.75, 0.75))
	ctrl.add_theme_color_override("font_outline_color", Color(0, 0, 0, 0.8))
	ctrl.add_theme_constant_override("outline_size", 3)
	canvas.add_child(ctrl)

	# ===== Info centre =====
	info_label = Label.new()
	info_label.text = ""
	info_label.position = Vector2(440, 300)
	info_label.add_theme_font_size_override("font_size", 26)
	info_label.add_theme_color_override("font_color", Color(1, 1, 0.4))
	info_label.add_theme_color_override("font_outline_color", Color(0.1, 0.1, 0.1))
	info_label.add_theme_constant_override("outline_size", 6)
	info_label.visible = false
	canvas.add_child(info_label)

	# ===== Bouton options =====
	var opt_btn := Button.new()
	opt_btn.text = "Options"
	opt_btn.position = Vector2(1180, 680)
	opt_btn.size = Vector2(86, 30)
	opt_btn.pressed.connect(_toggle_options)
	canvas.add_child(opt_btn)

	# ===== Panel options =====
	options_panel = PanelContainer.new()
	options_panel.position = Vector2(440, 250)
	options_panel.size = Vector2(400, 310)
	options_panel.visible = false
	var opt_style := StyleBoxFlat.new()
	opt_style.bg_color = Color(0.1, 0.1, 0.1, 0.92)
	opt_style.set_corner_radius_all(10)
	opt_style.border_width_bottom = 2; opt_style.border_width_top = 2
	opt_style.border_width_left = 2; opt_style.border_width_right = 2
	opt_style.border_color = Color(1, 0.86, 0.2)
	options_panel.add_theme_stylebox_override("panel", opt_style)
	canvas.add_child(options_panel)
	var opt_vbox := VBoxContainer.new()
	opt_vbox.position = Vector2(20, 20)
	opt_vbox.size = Vector2(360, 270)
	options_panel.add_child(opt_vbox)
	var opt_title := Label.new()
	opt_title.text = "OPTIONS"
	opt_title.add_theme_font_size_override("font_size", 22)
	opt_title.add_theme_color_override("font_color", Color(1, 0.86, 0.2))
	opt_title.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	opt_vbox.add_child(opt_title)
	var spacer := Control.new()
	spacer.custom_minimum_size = Vector2(0, 15)
	opt_vbox.add_child(spacer)
	invert_check = CheckBox.new()
	invert_check.text = "Inverser axe Y de la camera (clic droit)"
	invert_check.add_theme_font_size_override("font_size", 16)
	invert_check.add_theme_color_override("font_color", Color(0.9, 0.9, 0.9))
	invert_check.button_pressed = cam_invert_y
	invert_check.toggled.connect(func(pressed: bool): cam_invert_y = pressed; save_config())
	opt_vbox.add_child(invert_check)
	var spacer_visu := Control.new()
	spacer_visu.custom_minimum_size = Vector2(0, 14)
	opt_vbox.add_child(spacer_visu)
	# --- Luminosité (1..100) ---
	var row_lum := HBoxContainer.new()
	opt_vbox.add_child(row_lum)
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
	slider_lum.custom_minimum_size = Vector2(170, 22)
	row_lum.add_child(slider_lum)
	lbl_val_lum = Label.new()
	lbl_val_lum.text = str(opt_lum)
	lbl_val_lum.add_theme_font_size_override("font_size", 15)
	lbl_val_lum.add_theme_color_override("font_color", Color(1, 0.86, 0.2))
	lbl_val_lum.custom_minimum_size = Vector2(42, 0)
	row_lum.add_child(lbl_val_lum)
	slider_lum.value_changed.connect(func(v: float): opt_lum = int(v); lbl_val_lum.text = str(opt_lum); appliquer_reglages_visuels(); save_config())
	# --- Contraste (1..100) ---
	var row_con := HBoxContainer.new()
	opt_vbox.add_child(row_con)
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
	slider_con.custom_minimum_size = Vector2(170, 22)
	row_con.add_child(slider_con)
	lbl_val_con = Label.new()
	lbl_val_con.text = str(opt_con)
	lbl_val_con.add_theme_font_size_override("font_size", 15)
	lbl_val_con.add_theme_color_override("font_color", Color(1, 0.86, 0.2))
	lbl_val_con.custom_minimum_size = Vector2(42, 0)
	row_con.add_child(lbl_val_con)
	slider_con.value_changed.connect(func(v: float): opt_con = int(v); lbl_val_con.text = str(opt_con); appliquer_reglages_visuels(); save_config())
	var spacer2 := Control.new()
	spacer2.custom_minimum_size = Vector2(0, 20)
	opt_vbox.add_child(spacer2)
	var close_btn := Button.new()
	close_btn.text = "Fermer"
	close_btn.size = Vector2(100, 30)
	close_btn.pressed.connect(_toggle_options)
	opt_vbox.add_child(close_btn)

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

func _toggle_inventory():
	if inv_panel:
		inv_panel.visible = not inv_panel.visible
		inv_open = inv_panel.visible

func save_config():
	var cfg := ConfigFile.new()
	cfg.set_value("options", "cam_invert_y", cam_invert_y)
	cfg.set_value("options", "luminosite", opt_lum)
	cfg.set_value("options", "contraste", opt_con)
	cfg.save(config_path)

func load_config():
	var cfg := ConfigFile.new()
	if cfg.load(config_path) == OK:
		cam_invert_y = cfg.get_value("options", "cam_invert_y", false)
		if is_instance_valid(invert_check):
			invert_check.button_pressed = cam_invert_y
		opt_lum = int(cfg.get_value("options", "luminosite", 50))
		opt_con = int(cfg.get_value("options", "contraste", 50))
		if is_instance_valid(slider_lum):
			slider_lum.value = opt_lum
			lbl_val_lum.text = str(opt_lum)
		if is_instance_valid(slider_con):
			slider_con.value = opt_con
			lbl_val_con.text = str(opt_con)
