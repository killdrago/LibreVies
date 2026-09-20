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
    "EyeWhite": (0.92, 0.92, 0.88),
    "Eyes": (0.025, 0.045, 0.06),
    "Brow": (0.16, 0.055, 0.035),
    "Mouth": (0.24, 0.035, 0.045),
    "JacketTrim": (0.12, 0.14, 0.17),
    "JeansLight": (0.20, 0.30, 0.46),
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


def add_ring_surface(name, material, rings, segments=16, phase=0.0, caps=True):
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

# Jean : cuisses et bas de jambes séparés, avec un genou visible
# quand les pivots d'animation font marcher ou courir l'héroïne.
for side, suffix in ((-1, "L"), (1, "R")):
    add_ring_surface("JeansUpper_" + suffix, "Jeans", [
        (.18 * side, .56, 0, .145, .145), (.18 * side, .92, 0, .15, .15),
        (.18 * side, 1.12, 0, .19, .17)], 22, caps=False)
    add_ring_surface("JeansLower_" + suffix, "Jeans", [
        (.18 * side, .28, 0, .13, .13), (.18 * side, .50, 0, .135, .135),
        (.18 * side, .76, 0, .145, .145)], 24, caps=False)
add_ring_surface("Belt", "Belt", [(0, 1.05, 0, .35, .19), (0, 1.13, 0, .37, .20)], 28)

# Veste : volume trapézoïdal avec épaules marquées.
add_ring_surface("Jacket", "Jacket", [(0, 1.08, 0, .34, .18), (0, 1.30, 0, .36, .19), (0, 1.58, 0, .43, .21), (0, 1.70, 0, .34, .18)], 28, pi / 28)
# Col roulé et col ouvert contrasté.
add_ring_surface("Neck", "SkinLight", [(0, 1.62, 0, .105, .105), (0, 1.78, 0, .11, .11)], 14)
add_ring_surface("Collar", "JacketLight", [(0, 1.58, .005, .18, .12), (0, 1.70, .005, .15, .10)], 14)

# Bras en deux segments : l'épaule et le coude sont des groupes OBJ
# distincts afin que le balancement soit réellement visible en jeu.
for side in (-1, 1):
    x = side
    add_ring_surface("SleeveUpper_" + ("L" if side < 0 else "R"), "JacketLight", [
        (.40 * x, 1.58, 0, .13, .13), (.45 * x, 1.46, .01, .125, .125),
        (.53 * x, 1.30, .02, .12, .12)], 14, pi / 14, caps=False)
    add_ring_surface("SleeveLower_" + ("L" if side < 0 else "R"), "JacketLight", [
        (.43 * x, 1.48, .02, .13, .13), (.54 * x, 1.29, .03, .11, .11),
        (.57 * x, 1.20, .04, .105, .105)], 14, pi / 14, caps=False)
    add_ring_surface("Cuff_" + ("L" if side < 0 else "R"), "Jacket", [
        (.57 * x, 1.18, .04, .11, .11), (.59 * x, 1.12, .045, .105, .105)], 14, caps=False)
    add_sphere("Hand_" + ("L" if side < 0 else "R"), "SkinLight",
               (.62 * x, 1.05, .05), (.105, .13, .10), 20, 8)

# Tête, oreilles et yeux : proportions humaines plutôt que tête cartoon.
add_sphere("Head", "SkinLight", (0, 1.98, .01), (.235, .29, .205), 32, 16)
add_sphere("Ear_L", "Skin", (-.225, 2.00, .005), (.045, .075, .035), 16, 7)
add_sphere("Ear_R", "Skin", (.225, 2.00, .005), (.045, .075, .035), 16, 7)
add_sphere("Eye_L", "Eyes", (-.085, 2.035, .188), (.028, .035, .018), 16, 7)
add_sphere("Eye_R", "Eyes", (.085, 2.035, .188), (.028, .035, .018), 16, 7)
add_sphere("Nose", "SkinLight", (0, 1.975, .205), (.035, .055, .045), 16, 7)
# Bouche visible sur la face avant du visage. Elle est un petit volume OBJ
# indépendant, afin de rester opaque avec le shader personnage et de suivre la
# tête sans recourir à une primitive Unity au runtime.
add_sphere("Mouth", "Mouth", (0, 1.895, .204), (.055, .018, .014), 20, 7)
# Regard et sourcils : de petits volumes superposes donnent un visage plus
# humain, tout en restant des pieces importees et animables avec la tete.
for side in (-1, 1):
    suffix = "L" if side < 0 else "R"
    add_sphere("EyeWhite_" + suffix, "EyeWhite",
               (side * .085, 2.035, .183), (.035, .042, .012), 16, 7)
    add_sphere("Brow_" + suffix, "Brow",
               (side * .085, 2.095, .194), (.050, .014, .010), 16, 5)

# Details de veste et de jean : fermeture, poches et boutons. Ils suivent le
# torse fixe comme sur une tenue civile, au lieu d'un simple tube colore.
add_box("JacketZip", "JacketTrim", (0, 1.43, .216), (.025, .48, .026))
for side in (-1, 1):
    suffix = "L" if side < 0 else "R"
    add_box("JacketLapel_" + suffix, "JacketLight",
            (side * .145, 1.53, .205), (.085, .34, .030))
    add_box("JacketPocket_" + suffix, "JacketLight",
            (side * .205, 1.25, .208), (.22, .115, .030))
    add_box("JeansPocket_" + suffix, "JeansLight",
            (side * .235, .88, .145), (.16, .12, .026))
for y in (1.31, 1.45, 1.59):
    add_sphere("JacketButton_" + str(y).replace(".", "_"), "JacketTrim",
               (0, y, .222), (.020, .020, .012), 12, 5)

# Chevelure rousse : calotte et mèches longues, toujours dans ce maillage.
add_ring_surface("HairCap", "Hair_Red", [(0, 2.07, -.005, .25, .21), (0, 2.18, -.005, .29, .22), (0, 2.29, -.005, .22, .17), (0, 2.36, -.005, .055, .045)], 20)
add_ring_surface("HairBack", "Hair_Red", [(0, 1.72, -.13, .28, .10), (0, 1.94, -.16, .31, .105), (0, 2.17, -.12, .30, .10)], 20)
for side in (-1, 1):
    add_ring_surface("HairLock_" + ("L" if side < 0 else "R"), "Hair_Red_Light", [(side * .22, 2.16, .01, .085, .075), (side * .28, 1.92, .025, .085, .075), (side * .25, 1.67, .035, .065, .06), (side * .20, 1.48, .05, .035, .035)], 12, pi / 12)

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

# Exporte aussi chaque groupe dans un OBJ indépendant. Unity conserve ainsi
# une vraie Transform par membre, même lorsque son importeur fusionne les
# groupes d'un OBJ multi-matériaux en un seul Mesh.
PARTS = OUT / "LibreViesHeroineParts"
PARTS.mkdir(parents=True, exist_ok=True)
for ancien in PARTS.glob("*.obj"):
    ancien.unlink()
PARTS_MTL = PARTS / "LibreViesHeroineParts.mtl"
with PARTS_MTL.open("w", encoding="utf-8") as f:
    f.write("# Matériaux originaux de LibreViesHeroine — CC0\n")
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
        f.write("# LibreViesHeroine — groupe importé indépendant, CC0\n")
        f.write("# Généré par compilation/outils/generer_heroine_librevies.py\n")
        f.write("mtllib LibreViesHeroineParts.mtl\n")
        for x, y, z in sommets_locaux:
            f.write(f"v {x:.6f} {y:.6f} {z:.6f}\n")
        f.write("o " + object_name + "\n")
        dernier = None
        for face, material in zip(faces_locales, materiaux_locaux):
            if material != dernier:
                f.write("usemtl " + material + "\n")
                dernier = material
            f.write("f " + " ".join(str(i) for i in face) + "\n")
(PARTS / "LibreViesHeroineParts_LICENSE.txt").write_text(
    "LibreViesHeroineParts est une déclinaison technique du maillage original LibreViesHeroine, sous CC0 1.0.\n"
    "Les groupes séparés de ce dossier sont générés à partir du maillage CC0 "
    "principal pour permettre l'animation par articulations dans Unity.\n",
    encoding="utf-8",
)
print(f"Groupes indépendants : {len(objets)} dans {PARTS}")
