import struct, json, os, math, numpy as np

def cube_v(cx,cy,cz,sx,sy,sz):
    hx,hy,hz=sx/2,sy/2,sz/2
    return [[cx-hx,cy-hy,cz-hz],[cx+hx,cy-hy,cz-hz],[cx+hx,cy+hy,cz-hz],[cx-hx,cy+hy,cz-hz],
            [cx-hx,cy-hy,cz+hz],[cx+hx,cy-hy,cz+hz],[cx+hx,cy+hy,cz+hz],[cx-hx,cy+hy,cz+hz]]
CI=[0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,2,6,7,2,7,3,0,3,7,0,7,4,1,5,6,1,6,2]

def write_glb(fp, parts, mats):
    """
    parts = [{"name": str, "cubes": [(x,y,z,sx,sy,sz), ...], "mat": int, "pivot": (px,py,pz)}]
    Chaque part = un node séparé avec son propre mesh et son pivot
    """
    bd = bytearray()
    bvs = []; acs = []; msh = []; nodes = []

    for part in parts:
        all_verts = []
        all_indices = []
        for (cx, cy, cz, sx, sy, sz) in part['cubes']:
            offset = len(all_verts)
            all_verts.extend(cube_v(cx, cy, cz, sx, sy, sz))
            all_indices.extend([idx + offset for idx in CI])

        if not all_verts:
            continue

        va = np.array(all_verts, dtype=np.float32)
        ia = np.array(all_indices, dtype=np.uint32)

        vb = va.tobytes(); vo = len(bd); bd.extend(vb)
        ib = ia.tobytes(); io = len(bd); bd.extend(ib)

        bv_v = len(bvs); bvs.append({'buffer': 0, 'byteOffset': vo, 'byteLength': len(vb), 'target': 34962})
        bv_i = len(bvs); bvs.append({'buffer': 0, 'byteOffset': io, 'byteLength': len(ib), 'target': 34963})

        av = len(acs)
        acs.append({'bufferView': bv_v, 'byteOffset': 0, 'componentType': 5126,
                    'count': len(all_verts), 'type': 'VEC3',
                    'max': va.max(0).tolist(), 'min': va.min(0).tolist()})
        ai = len(acs)
        acs.append({'bufferView': bv_i, 'byteOffset': 0, 'componentType': 5125,
                    'count': len(all_indices), 'type': 'SCALAR',
                    'max': [int(np.max(ia))], 'min': [int(np.min(ia))]})

        mi = len(msh)
        msh.append({'name': part['name'], 'primitives': [{'attributes': {'POSITION': av}, 'indices': ai, 'material': part['mat']}]})

        # Node avec pivot (position du centre de rotation)
        px, py, pz = part.get('pivot', (0, 0, 0))
        nodes.append({'name': part['name'], 'mesh': mi, 'translation': [px, py, pz]})

    # Node racine
    ri = len(nodes)
    nodes.append({'name': 'Personnage', 'children': list(range(len(nodes)))})

    while len(bd) % 4: bd += b'\x00'

    jd = {
        'asset': {'version': '2.0'},
        'scene': 0,
        'scenes': [{'name': 'Scene', 'nodes': [ri]}],
        'nodes': nodes,
        'meshes': msh,
        'materials': [{'pbrMetallicRoughness': {'baseColorFactor': m['c'], 'metallicFactor': 0, 'roughnessFactor': 1.0}, 'doubleSided': True, 'name': m['n']} for m in mats],
        'accessors': acs, 'bufferViews': bvs,
        'buffers': [{'byteLength': len(bd)}]
    }

    jb = json.dumps(jd, separators=(',', ':')).encode()
    while len(jb) % 4: jb += b' '
    bb = bytes(bd)
    while len(bb) % 4: bb += b'\x00'

    with open(fp, 'wb') as f:
        f.write(b'glTF'); f.write(struct.pack('<I', 2))
        f.write(struct.pack('<I', 12 + 8 + len(jb) + 8 + len(bb)))
        f.write(struct.pack('<I', len(jb))); f.write(b'JSON'); f.write(jb)
        f.write(struct.pack('<I', len(bb))); f.write(b'BIN\x00'); f.write(bb)

    print(f"  {os.path.basename(fp):30s} {len(msh):2d} parties  {os.path.getsize(fp):6d}o")


# ============================================================
# PERSONNAGE — Parties séparées pour animation
# ============================================================

def gen_perso():
    s = 0.05
    cubes = []  # (x, y, z, sx, sy, sz)

    def B(x, y, z, sx=1, sy=1, sz=1):
        cubes.append((x, y, z, sx * s, sy * s, sz * s))

    # ============== PIED GAUCHE ==============
    pied_g_cubes = []
    _c = []
    for x in range(-1, 2):
        for z in range(-2, 3):
            _c.append((x * s, 0.05, z * s, s, s, s))
    pied_g_cubes = _c[:]

    # ============== PIED DROIT ==============
    pied_d_cubes = []
    _c = []
    for x in range(-1, 2):
        for z in range(-2, 3):
            _c.append((x * s, 0.05, z * s, s, s, s))
    pied_d_cubes = _c[:]

    # ============== JAMBE GAUCHE (pivot en haut) ==============
    jambe_g_cubes = []
    for y_off in range(6):
        for x in range(-1, 2):
            for z in range(-1, 2):
                jambe_g_cubes.append((x * s, y_off * s, z * s, s, s, s))

    # ============== JAMBE DROITE (pivot en haut) ==============
    jambe_d_cubes = []
    for y_off in range(6):
        for x in range(-1, 2):
            for z in range(-1, 2):
                jambe_d_cubes.append((x * s, y_off * s, z * s, s, s, s))

    # ============== TORSO ==============
    torso_cubes = []
    torso_layers = [(0,7,5),(1,7,5),(2,7,5),(3,6,5),(4,6,4),(5,6,4)]
    for y_off, w, d in torso_layers:
        for x in range(int(-w/2), int(w/2)+1):
            for z in range(int(-d/2), int(d/2)+1):
                torso_cubes.append((x*s, y_off*s, z*s, s, s, s))

    # ============== COU ==============
    cou_cubes = []
    for x in range(-1, 2):
        for z in range(-1, 2):
            cou_cubes.append((x*s, 0, z*s, s, s, s))

    # ============== TÊTE ==============
    tete_cubes = []
    head_layers = [
        (0, [(-2,0),(-1,0),(0,0),(1,0),(2,0),(-2,-1),(2,-1),(-2,1),(2,1),(-1,-1),(0,-1),(1,-1),(-1,1),(0,1),(1,1)]),
        (1, [(-2,-1),(-2,0),(-2,1),(2,-1),(2,0),(2,1),(-1,-2),(0,-2),(1,-2),(-1,2),(0,2),(1,2),(-1,-1),(0,-1),(1,-1),(-1,0),(0,0),(1,0),(-1,1),(0,1),(1,1)]),
        (2, [(-2,0),(-1,0),(0,0),(1,0),(2,0),(-2,-1),(2,-1),(-2,1),(2,1),(-1,-2),(0,-2),(1,-2),(-1,2),(0,2),(1,2),(-1,-1),(0,-1),(1,-1),(-1,1),(0,1),(1,1)]),
        (3, [(0,0),(-1,0),(1,0),(-2,0),(2,0),(-1,-1),(0,-1),(1,-1),(-1,1),(0,1),(1,1)]),
    ]
    for y_off, positions in head_layers:
        for (x, z) in positions:
            tete_cubes.append((x*s, y_off*s, z*s, s, s, s))

    # Yeux (sur la tête, couche 2)
    tete_cubes.append((-1*s, 2*s, -2*s, s, s, s))  # Oeil G (mat 7)
    tete_cubes.append((1*s, 2*s, -2*s, s, s, s))   # Oeil D (mat 7)
    # Bouche (couche 0)
    tete_cubes.append((0, 0, -1*s, s, s, s))        # Bouche (mat 8)
    # Nez (couche 1)
    tete_cubes.append((0, 1*s, -2*s - s*0.3, s, s, s))  # Nez (mat 0)

    # ============== CHEVEUX ==============
    cheveux_cubes = []
    for (x, z) in [(-2,0),(-1,0),(0,0),(1,0),(2,0),(-2,-1),(2,-1),(-2,1),(2,1),
                   (-1,-2),(0,-2),(1,-2),(-1,2),(0,2),(1,2),(-1,-1),(0,-1),(1,-1),(-1,1),(0,1),(1,1)]:
        cheveux_cubes.append((x*s, 0, z*s, s, s, s))
    cheveux_cubes.append((0, -s, -2.5*s, s*1.5, s*0.8, s))

    # ============== BRAS GAUCHE (pivot en haut) ==============
    bras_g_cubes = []
    for y_off in range(2):
        bras_g_cubes.append((0, -y_off*s, 0, s, s, s))
    for y_off in range(4):
        bras_g_cubes.append((0, -2*s - y_off*s, 0, s, s, s))
    bras_g_cubes.append((0, -6*s, 0, s*1.1, s*1.1, s*1.1))
    for y_off in range(3):
        bras_g_cubes.append((0, -7*s - y_off*s, 0, s, s, s))
    bras_g_cubes.append((0, -10*s, 0, s*1.2, s*1.2, s*1.2))

    # ============== BRAS DROIT + MARTEAU (pivot en haut) ==============
    bras_d_cubes = []
    for y_off in range(2):
        bras_d_cubes.append((0, -y_off*s, 0, s, s, s))
    for y_off in range(4):
        bras_d_cubes.append((0, -2*s - y_off*s, 0, s, s, s))
    bras_d_cubes.append((0, -6*s, 0, s*1.1, s*1.1, s*1.1))
    for y_off in range(3):
        bras_d_cubes.append((0, -7*s - y_off*s, 0, s, s, s))
    bras_d_cubes.append((0, -10*s, 0, s*1.2, s*1.2, s*1.2))
    # Marteau (mat 6 bois, mat 5 fer)
    for y_off in range(3):
        bras_d_cubes.append((0, -11*s - y_off*s, -s*0.5, s*0.5, s, s*0.5))
    bras_d_cubes.append((0, -14*s, -s*0.5, s*2, s*1, s*1))

    # ============== ASSEMBLAGE ==============
    # Pivots = point de rotation de chaque partie
    # Position absolue dans le monde

    parts = [
        {'name': 'PiedG',    'cubes': pied_g_cubes,   'mat': 4, 'pivot': (-2*s, 0.05, 0)},
        {'name': 'PiedD',    'cubes': pied_d_cubes,   'mat': 4, 'pivot': (2*s, 0.05, 0)},
        {'name': 'JambeG',   'cubes': jambe_g_cubes,  'mat': 3, 'pivot': (-2*s, 0.05 + 6*s, 0)},
        {'name': 'JambeD',   'cubes': jambe_d_cubes,  'mat': 3, 'pivot': (2*s, 0.05 + 6*s, 0)},
        {'name': 'Torso',    'cubes': torso_cubes,     'mat': 2, 'pivot': (0, 0.05 + 6*s + s, 0)},
        {'name': 'Cou',      'cubes': cou_cubes,       'mat': 0, 'pivot': (0, 0.05 + 6*s + 7*s, 0)},
        {'name': 'Tete',     'cubes': tete_cubes,      'mat': 0, 'pivot': (0, 0.05 + 6*s + 8*s, 0)},
        {'name': 'Cheveux',  'cubes': cheveux_cubes,   'mat': 1, 'pivot': (0, 0.05 + 6*s + 12*s, 0)},
        {'name': 'BrasG',    'cubes': bras_g_cubes,    'mat': 2, 'pivot': (-4*s, 0.05 + 6*s + 5*s, 0)},
        {'name': 'BrasD',    'cubes': bras_d_cubes,    'mat': 2, 'pivot': (4*s, 0.05 + 6*s + 5*s, 0)},
    ]

    M = [
        {'n': 'Peau',      'c': [0.94, 0.78, 0.63, 1]},
        {'n': 'Cheveux',   'c': [0.25, 0.15, 0.06, 1]},
        {'n': 'TShirt',    'c': [0.12, 0.39, 0.78, 1]},
        {'n': 'Jean',      'c': [0.20, 0.14, 0.08, 1]},
        {'n': 'Chaussure', 'c': [0.90, 0.90, 0.90, 1]},
        {'n': 'Fer',       'c': [0.59, 0.59, 0.59, 1]},
        {'n': 'Bois',      'c': [0.47, 0.29, 0.16, 1]},
        {'n': 'Oeil',      'c': [0.08, 0.08, 0.08, 1]},
        {'n': 'Bouche',    'c': [0.60, 0.30, 0.25, 1]},
    ]

    # Note: les yeux, bouche, nez sont dans le mesh "Tete" mais utilisent le mat de base (0)
    # Pour les matériaux spéciaux, il faudrait des meshes séparés
    # Pour l'instant, tout utilise le mat de la partie

    write_glb('perso_voxel.glb', parts, M)


if __name__ == '__main__':
    print('Generation personnage avec parties separees...')
    gen_perso()
    print('Termine !')
