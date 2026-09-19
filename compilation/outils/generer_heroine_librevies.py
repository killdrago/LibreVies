#!/usr/bin/env python3
"""Génère le modèle humanoïde original de LibreVies.

Le résultat est un unique maillage OBJ importable par Unity, sans primitives
Unity créées à l'exécution. La géométrie et les couleurs sont originales et
placées sous CC0 avec le projet.
"""
from math import cos, pi, sin
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "unity/Assets/Resources/Characters"
OUT.mkdir(parents=True, exist_ok=True)
OBJ = OUT / "LibreViesHeroine.obj"
MTL = OUT / "LibreViesHeroine.mtl"

materials = {
    "Skin": (0.73, 0.39, 0.25),
    "SkinLight": (0.88, 0.57, 0.39),
    "Jacket": (0.28, 0.34, 0.39),
    "JacketLight": (0.40, 0.46, 0.51),
    "Jeans": (0.12, 0.19, 0.30),
    "Shoes": (0.63, 0.29, 0.10),
    "Sole": (0.12, 0.09, 0.07),
    "Hair_Red": (0.58, 0.055, 0.025),
    "Hair_Red_Light": (0.84, 0.15, 0.045),
    "Eyes": (0.025, 0.045, 0.06),
    "Belt": (0.06, 0.045, 0.035),
}

vertices = []
faces = []
face_mats = []
face_objects = []
active_object = "LibreViesHeroine"


def add_face(indices, material):
    faces.append(indices)
    face_mats.append(material)
    face_objects.append(active_object)


def add_ring_surface(name, material, rings, segments=16, phase=0.0):
    """Ajoute une surface fermée par anneaux, en un seul maillage OBJ."""
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
            # Face orientée vers l'extérieur.
            add_face((a, c, b), material)
            add_face((a, d, c), material)
    # Fermetures planes pour éviter les trous dans les vêtements.
    bottom = len(vertices) + 1
    x, y, z, rx, rz = rings[0]
    vertices.append((x, y, z))
    top = len(vertices) + 1
    x2, y2, z2, rx2, rz2 = rings[-1]
    vertices.append((x2, y2, z2))
    for i in range(segments):
        add_face((bottom, starts[0] + (i + 1) % segments, starts[0] + i), material)
        add_face((top, starts[-1] + i, starts[-1] + (i + 1) % segments), material)


def add_sphere(name, material, center, scale, segments=16, rings=8):
    global active_object
    active_object = name
    cx, cy, cz = center
    sx, sy, sz = scale
    starts = []
    # Les deux petits anneaux polaires évitent une face dégénérée.
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


# Chaussures : semelles, talons et empeignes en volume.
add_ring_surface("Shoe_L", "Sole", [(-0.18, 0.055, 0.075, .17, .30), (-0.18, .13, .075, .16, .27)], 16)
add_ring_surface("Shoe_R", "Sole", [(0.18, 0.055, 0.075, .17, .30), (0.18, .13, .075, .16, .27)], 16)
add_ring_surface("Boot_L", "Shoes", [(-0.18, .11, .075, .14, .25), (-0.18, .25, .055, .13, .21), (-0.18, .34, .045, .12, .18)], 16)
add_ring_surface("Boot_R", "Shoes", [(0.18, .11, .075, .14, .25), (0.18, .25, .055, .13, .21), (0.18, .34, .045, .12, .18)], 16)

# Jean : deux jambes légèrement fuselées, puis la ceinture.
add_ring_surface("Jeans_L", "Jeans", [(-.18, .28, 0, .13, .13), (-.18, .55, 0, .135, .135), (-.18, .92, 0, .15, .15), (-.18, 1.12, 0, .19, .17)], 16)
add_ring_surface("Jeans_R", "Jeans", [(.18, .28, 0, .13, .13), (.18, .55, 0, .135, .135), (.18, .92, 0, .15, .15), (.18, 1.12, 0, .19, .17)], 16)
add_ring_surface("Belt", "Belt", [(0, 1.05, 0, .35, .19), (0, 1.13, 0, .37, .20)], 20)

# Veste : volume trapézoïdal avec épaules marquées.
add_ring_surface("Jacket", "Jacket", [(0, 1.08, 0, .34, .18), (0, 1.30, 0, .36, .19), (0, 1.58, 0, .43, .21), (0, 1.70, 0, .34, .18)], 20, pi / 20)
# Col roulé et col ouvert contrasté.
add_ring_surface("Neck", "SkinLight", [(0, 1.62, 0, .105, .105), (0, 1.78, 0, .11, .11)], 14)
add_ring_surface("Collar", "JacketLight", [(0, 1.58, .005, .18, .12), (0, 1.70, .005, .15, .10)], 14)

# Bras inclinés, séparés mais contenus dans le même fichier mesh.
for side in (-1, 1):
    x = side
    add_ring_surface("Sleeve", "JacketLight", [(.40 * x, 1.58, 0, .13, .13), (.48 * x, 1.40, .02, .12, .12), (.57 * x, 1.20, .04, .105, .105)], 14, pi / 14)
    add_ring_surface("Cuff", "Jacket", [(.57 * x, 1.18, .04, .11, .11), (.59 * x, 1.12, .045, .105, .105)], 14)
    add_sphere("Hand", "SkinLight", (.62 * x, 1.05, .05), (.105, .13, .10), 14, 6)

# Tête, oreilles et yeux : proportions humaines plutôt que tête cartoon.
add_sphere("Head", "SkinLight", (0, 1.98, .01), (.235, .29, .205), 20, 10)
add_sphere("Ear_L", "Skin", (-.225, 2.00, .005), (.045, .075, .035), 12, 5)
add_sphere("Ear_R", "Skin", (.225, 2.00, .005), (.045, .075, .035), 12, 5)
add_sphere("Eye_L", "Eyes", (-.085, 2.035, .188), (.028, .035, .018), 12, 5)
add_sphere("Eye_R", "Eyes", (.085, 2.035, .188), (.028, .035, .018), 12, 5)
add_sphere("Nose", "SkinLight", (0, 1.975, .205), (.035, .055, .045), 12, 5)

# Chevelure rousse : calotte et mèches longues, toujours dans ce maillage.
add_ring_surface("HairCap", "Hair_Red", [(0, 2.07, -.005, .25, .21), (0, 2.18, -.005, .29, .22), (0, 2.29, -.005, .22, .17), (0, 2.36, -.005, .055, .045)], 20)
add_ring_surface("HairBack", "Hair_Red", [(0, 1.72, -.13, .28, .10), (0, 1.94, -.16, .31, .105), (0, 2.17, -.12, .30, .10)], 20)
for side in (-1, 1):
    add_ring_surface("HairLock", "Hair_Red_Light", [(side * .22, 2.16, .01, .085, .075), (side * .28, 1.92, .025, .085, .075), (side * .25, 1.67, .035, .065, .06), (side * .20, 1.48, .05, .035, .035)], 12, pi / 12)

with MTL.open("w", encoding="utf-8") as f:
    f.write("# Matériaux originaux de LibreVies — CC0\n")
    for name, (r, g, b) in materials.items():
        f.write(f"newmtl {name}\nKd {r:.4f} {g:.4f} {b:.4f}\nKa 0.05 0.05 0.05\nKs 0.18 0.18 0.18\nNs 32.0\nd 1.0\n\n")

with OBJ.open("w", encoding="utf-8", newline="\n") as f:
    f.write("# LibreViesHeroine — maillage humanoïde original, CC0\n")
    f.write("# Généré par compilation/outils/generer_heroine_librevies.py\n")
    f.write("mtllib LibreViesHeroine.mtl\n")
    for x, y, z in vertices:
        f.write(f"v {x:.6f} {y:.6f} {z:.6f}\n")
    current = None
    current_object = None
    for face, material, object_name in zip(faces, face_mats, face_objects):
        if object_name != current_object:
            f.write(f"o {object_name}\n")
            current_object = object_name
            current = None
        if material != current:
            f.write(f"usemtl {material}\n")
            current = material
        f.write("f " + " ".join(str(i) for i in face) + "\n")

print(f"Généré : {OBJ} ({len(vertices)} sommets, {len(faces)} faces)")
