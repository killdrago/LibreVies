#!/usr/bin/env python3
"""Génère le modèle de garde humanoïde original de LibreVies.

Le garde est un vrai maillage OBJ importé par Unity. Sa géométrie, son
armure et son arme sont originales et placées sous CC0 avec le projet ; aucun
primitive Unity n'est nécessaire pour construire le personnage en jeu.
"""
from math import cos, pi, sin
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "unity/Assets/Resources/Characters"
OUT.mkdir(parents=True, exist_ok=True)
OBJ = OUT / "LibreViesGuard.obj"
MTL = OUT / "LibreViesGuard.mtl"
LICENSE = OUT / "LibreViesGuard_LICENSE.txt"

materials = {
    "GuardSkin": (0.70, 0.43, 0.28),
    "GuardArmor": (0.20, 0.25, 0.31),
    "GuardArmorLight": (0.42, 0.48, 0.56),
    "GuardCloth": (0.16, 0.28, 0.55),
    "GuardLeather": (0.16, 0.09, 0.055),
    "GuardBoots": (0.09, 0.07, 0.055),
    "GuardMetal": (0.62, 0.66, 0.70),
    "GuardGold": (0.92, 0.67, 0.18),
    "GuardWood": (0.45, 0.25, 0.10),
    "GuardVisor": (0.06, 0.08, 0.10),
}

vertices = []
faces = []
face_mats = []
face_objects = []
active_object = "LibreViesGuard"


def add_face(indices, material):
    faces.append(indices)
    face_mats.append(material)
    face_objects.append(active_object)


def add_ring_surface(name, material, rings, segments=16, phase=0.0, caps=True):
    global active_object
    active_object = name
    starts = []
    for x, y, z, rx, rz in rings:
        starts.append(len(vertices) + 1)
        for i in range(segments):
            angle = phase + i * 2.0 * pi / segments
            vertices.append((x + cos(angle) * rx, y, z + sin(angle) * rz))
    for r in range(len(rings) - 1):
        for i in range(segments):
            a = starts[r] + i
            b = starts[r] + (i + 1) % segments
            c = starts[r + 1] + (i + 1) % segments
            d = starts[r + 1] + i
            add_face((a, c, b), material)
            add_face((a, d, c), material)
    if caps:
        bottom = len(vertices) + 1
        x, y, z, _, _ = rings[0]
        vertices.append((x, y, z))
        top = len(vertices) + 1
        x, y, z, _, _ = rings[-1]
        vertices.append((x, y, z))
        for i in range(segments):
            # Normales des bouchons vers l'extérieur : bas vers -Y, haut vers +Y.
            add_face((bottom, starts[0] + i, starts[0] + (i + 1) % segments), material)
            add_face((top, starts[-1] + (i + 1) % segments, starts[-1] + i), material)

def add_sphere(name, material, center, scale, segments=16, rings=8):
    global active_object
    active_object = name
    cx, cy, cz = center
    sx, sy, sz = scale
    starts = []
    for r in range(rings + 1):
        t = -pi / 2.0 + pi * r / rings
        y = cy + sin(t) * sy
        radius = cos(t)
        starts.append(len(vertices) + 1)
        for i in range(segments):
            a = i * 2.0 * pi / segments
            vertices.append((cx + cos(a) * sx * radius, y, cz + sin(a) * sz * radius))
    for r in range(rings):
        for i in range(segments):
            a = starts[r] + i
            b = starts[r] + (i + 1) % segments
            c = starts[r + 1] + (i + 1) % segments
            d = starts[r + 1] + i
            add_face((a, b, c), material)
            add_face((a, c, d), material)


def add_box(name, material, center, size):
    global active_object
    active_object = name
    cx, cy, cz = center
    sx, sy, sz = (value * 0.5 for value in size)
    corners = [
        (cx - sx, cy - sy, cz - sz), (cx + sx, cy - sy, cz - sz),
        (cx + sx, cy - sy, cz + sz), (cx - sx, cy - sy, cz + sz),
        (cx - sx, cy + sy, cz - sz), (cx + sx, cy + sy, cz - sz),
        (cx + sx, cy + sy, cz + sz), (cx - sx, cy + sy, cz + sz),
    ]
    first = len(vertices) + 1
    vertices.extend(corners)
    for quad in ((0, 3, 2, 1), (4, 5, 6, 7), (0, 1, 5, 4),
                 (3, 7, 6, 2), (1, 2, 6, 5), (0, 4, 7, 3)):
        a, b, c, d = (first + i for i in quad)
        add_face((a, b, c), material)
        add_face((a, c, d), material)


# Bottes et jambes articulées.
for side, suffix in ((-1, "L"), (1, "R")):
    x = 0.18 * side
    add_ring_surface("Guard_Boot_" + suffix, "GuardBoots",
                     [(x, .06, .05, .16, .25), (x, .30, .04, .14, .21)], 16, caps=False)
    add_ring_surface("Guard_LegLower_" + suffix, "GuardArmor",
                     [(x, .30, 0, .13, .13), (x, .76, 0, .145, .145)], 16, caps=False)
    add_ring_surface("Guard_LegUpper_" + suffix, "GuardCloth",
                     [(x, .56, 0, .145, .145), (x, 1.12, 0, .20, .17)], 16, caps=False)

# Bassin, tunique blindée et épaulières.
add_ring_surface("Guard_Belt", "GuardLeather",
                 [(0, 1.06, 0, .37, .20), (0, 1.16, 0, .39, .21)], 20)
add_ring_surface("Guard_Torso", "GuardArmor",
                 [(0, 1.10, 0, .35, .19), (0, 1.42, 0, .41, .22),
                  (0, 1.70, 0, .34, .18)], 20, pi / 20)
add_ring_surface("Guard_ChestPlate", "GuardArmorLight",
                 [(0, 1.27, .19, .22, .035), (0, 1.55, .19, .24, .035)], 12)

# Bras en segments séparés pour les animations de garde.
for side, suffix in ((-1, "L"), (1, "R")):
    x = side
    add_ring_surface("Guard_ArmUpper_" + suffix, "GuardArmorLight",
                     [(.40 * x, 1.58, 0, .13, .13), (.53 * x, 1.30, .01, .115, .115)], 14, caps=False)
    add_ring_surface("Guard_ArmLower_" + suffix, "GuardArmor",
                     [(.43 * x, 1.48, .01, .125, .125), (.57 * x, 1.18, .03, .10, .10)], 14, caps=False)
    add_sphere("Guard_Glove_" + suffix, "GuardLeather",
               (.59 * x, 1.10, .04), (.11, .13, .10), 14, 6)
    add_box("Guard_Shoulder_" + suffix, "GuardArmorLight",
            (.37 * x, 1.61, 0), (.24, .14, .30))

# Tête et casque.
add_sphere("Guard_Head", "GuardSkin", (0, 1.98, .01), (.235, .28, .205), 20, 10)
add_ring_surface("Guard_Helmet", "GuardArmor",
                 [(0, 2.08, -.01, .25, .21), (0, 2.25, -.01, .28, .22),
                  (0, 2.34, -.01, .08, .07)], 20)
add_box("Guard_Visor", "GuardVisor", (0, 2.00, .215), (.34, .08, .035))
add_box("Guard_Crest", "GuardGold", (0, 2.47, 0), (.07, .28, .24))

# Hallebarde complète, incluse dans le même maillage importé.
add_ring_surface("Guard_HalberdShaft", "GuardWood",
                 [(.42, .20, .08, .035, .035), (.42, 2.72, .08, .035, .035)], 10)
add_box("Guard_HalberdBlade", "GuardMetal", (.42, 2.78, .08), (.11, .48, .20))
add_box("Guard_HalberdHook", "GuardMetal", (.28, 2.58, .08), (.28, .10, .08))
add_ring_surface("Guard_HalberdTip", "GuardMetal",
                 [(.42, 2.97, .08, .09, .09), (.42, 3.16, .08, .015, .015)], 10)

# Harmonise les normales par groupe. Les anneaux qui descendent (bras,
# mèches) ne doivent pas être orientés vers l'intérieur, sinon leur face
# avant disparaît dès que le shader utilise Cull Back.
for object_name in dict.fromkeys(face_objects):
    numeros = [i for i, nom in enumerate(face_objects) if nom == object_name]
    volume = 0.0
    for numero in numeros:
        face = faces[numero]
        if len(face) < 3:
            continue
        a, b, c = (vertices[face[k] - 1] for k in range(3))
        volume += (a[0] * (b[1] * c[2] - b[2] * c[1])
                   - a[1] * (b[0] * c[2] - b[2] * c[0])
                   + a[2] * (b[0] * c[1] - b[1] * c[0])) / 6.0
    if volume < -1e-9:
        for numero in numeros:
            face = faces[numero]
            faces[numero] = (face[0], face[2], face[1]) + tuple(face[3:])

with MTL.open("w", encoding="utf-8") as f:
    f.write("# Matériaux originaux du garde LibreVies — CC0\n")
    for name, (r, g, b) in materials.items():
        f.write(f"newmtl {name}\nKd {r:.4f} {g:.4f} {b:.4f}\nKa 0.05 0.05 0.05\nKs 0.22 0.22 0.22\nNs 32.0\nd 1.0\n\n")

with OBJ.open("w", encoding="utf-8", newline="\n") as f:
    f.write("# LibreViesGuard — maillage humanoïde original blindé, CC0\n")
    f.write("# Généré par compilation/outils/generer_garde_librevies.py\n")
    f.write("mtllib LibreViesGuard.mtl\n")
    for x, y, z in vertices:
        f.write(f"v {x:.6f} {y:.6f} {z:.6f}\n")
    current_material = None
    current_object = None
    for face, material, object_name in zip(faces, face_mats, face_objects):
        if object_name != current_object:
            f.write(f"o {object_name}\n")
            current_object = object_name
            current_material = None
        if material != current_material:
            f.write(f"usemtl {material}\n")
            current_material = material
        f.write("f " + " ".join(str(i) for i in face) + "\n")

LICENSE.write_text(
    "LibreViesGuard.obj et LibreViesGuard.mtl sont des créations originales "
    "du projet LibreVies.\n"
    "Licence : CC0 1.0 Universal (domaine public), sans restriction de "
    "modification, usage commercial ou redistribution.\n"
    "Générateur : compilation/outils/generer_garde_librevies.py\n",
    encoding="utf-8",
)
print(f"Généré : {OBJ} ({len(vertices)} sommets, {len(faces)} faces)")

# Exporte aussi chaque groupe dans un OBJ indépendant. Unity conserve ainsi
# une vraie Transform par membre, même lorsque son importeur fusionne les
# groupes d'un OBJ multi-matériaux en un seul Mesh.
PARTS = OUT / "LibreViesGuardParts"
PARTS.mkdir(parents=True, exist_ok=True)
for ancien in PARTS.glob("*.obj"):
    ancien.unlink()
PARTS_MTL = PARTS / "LibreViesGuardParts.mtl"
with PARTS_MTL.open("w", encoding="utf-8") as f:
    f.write("# Matériaux originaux de LibreViesGuard — CC0\n")
    for name, (r, g, b) in materials.items():
        f.write(f"newmtl {name}\nKd {r:.4f} {g:.4f} {b:.4f}\nKa 0.05 0.05 0.05\nKs 0.18 0.18 0.18\nNs 32.0\nd 1.0\n\n")

objets = []
for object_name in face_objects:
    if object_name not in objets:
        objets.append(object_name)
for object_name in objets:
    numeros = [i for i, nom in enumerate(face_objects) if nom == object_name]
    remap = {}
    sommets_locaux = []
    faces_locales = []
    materiaux_locaux = []
    for numero in numeros:
        face = faces[numero]
        face_locale = []
        for indice in face:
            if indice not in remap:
                remap[indice] = len(sommets_locaux) + 1
                sommets_locaux.append(vertices[indice - 1])
            face_locale.append(remap[indice])
        faces_locales.append(face_locale)
        materiaux_locaux.append(face_mats[numero])
    sortie = PARTS / (object_name + ".obj")
    with sortie.open("w", encoding="utf-8", newline="\n") as f:
        f.write("# LibreViesGuard — groupe importé indépendant, CC0\n")
        f.write("# Généré par compilation/outils/generer_garde_librevies.py\n")
        f.write("mtllib LibreViesGuardParts.mtl\n")
        for x, y, z in sommets_locaux:
            f.write(f"v {x:.6f} {y:.6f} {z:.6f}\n")
        f.write("o " + object_name + "\n")
        dernier = None
        for face, material in zip(faces_locales, materiaux_locaux):
            if material != dernier:
                f.write("usemtl " + material + "\n")
                dernier = material
            f.write("f " + " ".join(str(i) for i in face) + "\n")
(PARTS / "LibreViesGuardParts_LICENSE.txt").write_text(
    "LibreViesGuardParts est une déclinaison technique du maillage original LibreViesGuard, sous CC0 1.0.\n"
    "Les groupes séparés de ce dossier sont générés à partir du maillage CC0 "
    "principal pour permettre l'animation par articulations dans Unity.\n",
    encoding="utf-8",
)
print(f"Groupes indépendants : {len(objets)} dans {PARTS}")
