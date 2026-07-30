import struct, json, os, math, numpy as np

def cube_v(cx,cy,cz,sx,sy,sz):
    hx,hy,hz=sx/2,sy/2,sz/2
    return [[cx-hx,cy-hy,cz-hz],[cx+hx,cy-hy,cz-hz],[cx+hx,cy+hy,cz-hz],[cx-hx,cy+hy,cz-hz],
            [cx-hx,cy-hy,cz+hz],[cx+hx,cy-hy,cz+hz],[cx+hx,cy+hy,cz+hz],[cx-hx,cy+hy,cz+hz]]
CI=[0,1,2,0,2,3,4,6,5,4,7,6,0,4,5,0,5,1,2,6,7,2,7,3,0,3,7,0,7,4,1,5,6,1,6,2]

def sph_v(cx,cy,cz,r,s=8):
    v=[]
    for i in range(s+1):
        la=math.pi*i/s
        for j in range(s):
            lo=2*math.pi*j/s
            v.append([cx+r*math.sin(la)*math.cos(lo),cy+r*math.cos(la),cz+r*math.sin(la)*math.sin(lo)])
    ix=[]
    for i in range(s):
        for j in range(s):
            a=i*s+j;b=i*s+(j+1)%s;c=(i+1)*s+j;d=(i+1)*s+(j+1)%s
            ix+=[a,b,c,b,d,c]
    return v,ix

def cyl_v(cx,cy,cz,r,h,s=12):
    v=[]
    for i in range(s):
        a=2*math.pi*i/s;v.append([cx+r*math.cos(a),cy-h/2,cz+r*math.sin(a)])
    for i in range(s):
        a=2*math.pi*i/s;v.append([cx+r*math.cos(a),cy+h/2,cz+r*math.sin(a)])
    v+=[[cx,cy-h/2,cz],[cx,cy+h/2,cz]]
    ix=[]
    for i in range(s):
        j=(i+1)%s;ix+=[i,j,s+j,i,s+j,s+i]
    for i in range(s):
        j=(i+1)%s;ix+=[i,j,2*s]
    for i in range(s):
        j=(i+1)%s;ix+=[s+i,2*s+1,s+j]
    return v,ix

def cone_v(cx,cy,cz,r,h,s=12):
    v=[[cx,cy+h,cz]]
    for i in range(s):
        a=2*math.pi*i/s;v.append([cx+r*math.cos(a),cy,cz+r*math.sin(a)])
    ix=[]
    for i in range(s):
        j=(i+1)%s;ix+=[0,i+1,j+1]
    v.append([cx,cy,cz])
    return v,ix

def write_glb(fp,parts,mats):
    bd=bytearray();bvs=[];acs=[];msh=[];nds=[]
    for p in parts:
        va=np.array(p['v'],dtype=np.float32);ia=np.array(p['i'],dtype=np.uint16)
        vb=va.tobytes();vo=len(bd);bd.extend(vb)
        ib=ia.tobytes();io=len(bd);bd.extend(ib)
        bv_v=len(bvs);bvs.append({'buffer':0,'byteOffset':vo,'byteLength':len(vb),'target':34962})
        bv_i=len(bvs);bvs.append({'buffer':0,'byteOffset':io,'byteLength':len(ib),'target':34963})
        av=len(acs);acs.append({'bufferView':bv_v,'byteOffset':0,'componentType':5126,'count':len(p['v']),'type':'VEC3','max':va.max(0).tolist(),'min':va.min(0).tolist()})
        ai=len(acs);acs.append({'bufferView':bv_i,'byteOffset':0,'componentType':5123,'count':len(p['i']),'type':'SCALAR','max':[int(max(p['i']))],'min':[int(min(p['i']))]})
        mi=len(msh);msh.append({'name':p['n'],'primitives':[{'attributes':{'POSITION':av},'indices':ai,'material':p['m']}]})
        nds.append({'name':p['n'],'mesh':mi})
    ri=len(nds);nds.append({'name':'R','children':list(range(len(nds)))})
    while len(bd)%4:bd+=b'\x00'
    jd={'asset':{'version':'2.0'},'scene':0,'scenes':[{'name':'S','nodes':[ri]}],
        'nodes':nds,'meshes':msh,
        'materials':[{'pbrMetallicRoughness':{'baseColorFactor':m['c'],'metallicFactor':0,'roughnessFactor':0.8},'name':m['n']} for m in mats],
        'accessors':acs,'bufferViews':bvs,'buffers':[{'byteLength':len(bd)}]}
    jb=json.dumps(jd,separators=(',',':')).encode()
    while len(jb)%4:jb+=b' '
    bb=bytes(bd)
    while len(bb)%4:bb+=b'\x00'
    with open(fp,'wb') as f:
        f.write(b'glTF');f.write(struct.pack('<I',2));f.write(struct.pack('<I',12+8+len(jb)+8+len(bb)))
        f.write(struct.pack('<I',len(jb)));f.write(b'JSON');f.write(jb)
        f.write(struct.pack('<I',len(bb)));f.write(b'BIN\x00');f.write(bb)
    print(f"  {os.path.basename(fp):40s} {len(msh):2d} meshes  {os.path.getsize(fp):6d}o")

# ======== P ========
def P_cube(P,cx,cy,cz,sx,sy,sz,m,n):
    P.append({'v':cube_v(cx,cy,cz,sx,sy,sz),'i':CI,'m':m,'n':n})
def P_sph(P,cx,cy,cz,r,m,n,s=8):
    v,i=sph_v(cx,cy,cz,r,s);P.append({'v':v,'i':i,'m':m,'n':n})
def P_cyl(P,cx,cy,cz,r,h,m,n,s=12):
    v,i=cyl_v(cx,cy,cz,r,h,s);P.append({'v':v,'i':i,'m':m,'n':n})
def P_cone(P,cx,cy,cz,r,h,m,n,s=12):
    v,i=cone_v(cx,cy,cz,r,h,s);P.append({'v':v,'i':i,'m':m,'n':n})

# ===================== PERSONNAGE =====================
def gen_perso():
    P=[]
    # VENTRE (t-shirt bleu, plus court)
    P_cyl(P,0,0.65,0,0.28,0.45,2,'Ventre')
    # POITRINE (t-shirt bleu, plus large)
    P_cyl(P,0,0.95,0,0.30,0.30,2,'Poitrine')
    # COU
    P_cyl(P,0,1.12,0,0.10,0.10,0,'Cou')
    # TÊTE
    P_sph(P,0,1.25,0,0.24,0,'Tete')
    # CHEVEUX (pas un bonnet — plusieurs sphères pour volume)
    P_sph(P,0,1.38,-0.02,0.18,1,'Cheveux1')
    P_sph(P,0.08,1.35,0.05,0.12,1,'Cheveux2')
    P_sph(P,-0.08,1.35,0.05,0.12,1,'Cheveux3')
    P_sph(P,0,1.42,0.02,0.10,1,'Cheveux4')
    # Mèche devant
    P_cube(P,0,1.38,-0.18,0.12,0.06,0.08,1,'Meche')
    # YEUX
    P_sph(P,-0.09,1.27,-0.22,0.04,7,'OeilG')
    P_sph(P,0.09,1.27,-0.22,0.04,7,'OeilD')
    # SOURCILS
    P_cube(P,-0.09,1.33,-0.22,0.08,0.02,0.02,1,'SourcilG')
    P_cube(P,0.09,1.33,-0.22,0.08,0.02,0.02,1,'SourcilD')
    # BOUCHE
    P_cube(P,0,1.17,-0.22,0.1,0.025,0.02,8,'Bouche')
    # OREILLES
    P_sph(P,-0.24,1.25,0,0.05,0,'OreilleG')
    P_sph(P,0.24,1.25,0,0.05,0,'OreilleD')

    # HANCHE (séparation visible t-shirt / pantalon)
    P_cyl(P,0,0.42,0,0.26,0.08,3,'Hanche')

    # JAMBES (jean foncé)
    for s in [-1,1]:
        cote = "G" if s<0 else "D"
        P_cyl(P,s*0.12,0.22,0,0.10,0.35,3,f'Jambe{cote}')
        P_sph(P,s*0.12,0.02,0,0.06,3,f'Genou{cote}')
        P_cyl(P,s*0.12,-0.12,0,0.08,0.20,3,f'Tibia{cote}')
        P_cube(P,s*0.12,-0.24,-0.02,0.20,0.08,0.28,4,f'Pied{cote}')

    # BRAS (t-shirt bleu)
    for s in [-1,1]:
        cote = "G" if s<0 else "D"
        P_cyl(P,s*0.36,0.85,0,0.08,0.30,2,f'Epaule{cote}')
        P_sph(P,s*0.36,0.68,0,0.07,2,f'Coude{cote}')
        P_cyl(P,s*0.36,0.52,0,0.06,0.25,0,f'AvBras{cote}')
        P_sph(P,s*0.36,0.38,0,0.06,0,f'Main{cote}')

    # MARTEAU (bras droit)
    P_cube(P,0.36,0.22,-0.08,0.06,0.30,0.06,5,'Manche')
    P_cube(P,0.36,0.02,-0.08,0.22,0.14,0.14,6,'TeteMarteau')

    # CEINTURE
    P_cyl(P,0,0.42,0,0.29,0.04,3,'Ceinture')
    P_cube(P,0,0.42,0.28,0.10,0.05,0.03,6,'Boucle')

    M=[{'n':'Peau','c':[.94,.78,.63,1]},{'n':'Cheveux','c':[.25,.15,.06,1]},
       {'n':'TShirt','c':[.12,.39,.78,1]},{'n':'Jean','c':[.20,.14,.08,1]},
       {'n':'Chaussure','c':[.82,.82,.82,1]},{'n':'Bois','c':[.47,.29,.16,1]},
       {'n':'Fer','c':[.59,.59,.59,1]},{'n':'Oeil','c':[.08,.08,.08,1]},
       {'n':'Bouche','c':[.6,.3,.25,1]}]
    write_glb('perso.glb',P,M)

# ===================== BATIMENTS =====================
def gen_bat(nom,mc,tc,w,h,d):
    P=[]
    P_cube(P,0,0.12,0,w+0.3,0.24,d+0.3,0,'Fond')
    P_cube(P,0,h/2,0,w,h,d,0,'Murs')
    P_cone(P,0,h+0.05,0,max(w,d)*0.8,h*0.5,1,'Toit',4)
    P_cube(P,0,h+0.05,0,w+0.4,0.1,d+0.4,1,'Corniche')
    P_cube(P,0,h*0.25,d/2+0.06,w*0.3,h*0.48,0.14,2,'Porte')
    P_sph(P,0,h*0.25,d/2+0.14,0.04,5,'Poignee')
    P_cube(P,0,h*0.52,d/2+0.5,w*0.5,0.06,0.8,3,'Auvent')
    P_cube(P,-w*0.22,h*0.3,d/2+0.85,0.08,h*0.4,0.08,4,'PoteauG')
    P_cube(P,w*0.22,h*0.3,d/2+0.85,0.08,h*0.4,0.08,4,'PoteauD')
    for fx in [-0.3,0.3]:
        P_cube(P,w*fx,h*0.6,d/2+0.06,w*0.2,h*0.2,0.12,4,f'Cadre{fx}')
        P_cube(P,w*fx,h*0.6,d/2+0.07,w*0.15,h*0.15,0.08,6,f'Vitre{fx}')
        P_cube(P,w*fx,h*0.6,d/2+0.08,w*0.15,0.02,0.02,4,f'CrossH{fx}')
        P_cube(P,w*fx,h*0.6,d/2+0.08,0.02,h*0.15,0.02,4,f'CrossV{fx}')
    P_cube(P,w*0.25,h+0.6,d*0.25,0.35,0.7,0.35,4,'Cheminee')
    P_cube(P,w*0.25,h+1,d*0.25,0.45,0.08,0.45,4,'Chapeau')

    M=[{'n':'Mur','c':mc},{'n':'Toit','c':tc},{'n':'Porte','c':[.22,.12,.04,1]},
       {'n':'Auvent','c':[.55,.30,.12,1]},{'n':'Bois','c':[.40,.25,.10,1]},
       {'n':'Poignee','c':[.7,.7,.1,1]},{'n':'Vitre','c':[.50,.75,.95,1]}]
    write_glb(f'batiment_{nom}.glb',P,M)

def gen_bats():
    for n,mc,tc,w,h,d in [
        ('supermarche',[.85,.72,.55,1],[.70,.28,.12,1],6,5,5),
        ('armurerie',[.78,.63,.42,1],[.5,.5,.5,1],5,4,4),
        ('vetements',[.55,.75,.62,1],[.58,.38,.22,1],5,4,4),
        ('auberge',[.78,.62,.38,1],[.65,.25,.10,1],5,5,5),
        ('mairie',[.88,.84,.74,1],[.35,.50,.60,1],10,7,8),
        ('maison1',[.80,.68,.50,1],[.65,.25,.10,1],5,4,4),
        ('maison2',[.75,.62,.44,1],[.58,.32,.18,1],4,3.5,4),
    ]: gen_bat(n,mc,tc,w,h,d)

# ===================== ARBRE =====================
def gen_arbre():
    P=[]
    P_cyl(P,0,1,0,0.18,2.2,0,'Tronc')
    P_cyl(P,0.05,1.2,0.05,0.12,1.5,0,'Tronc2')
    P_cube(P,0.4,1.9,0,0.6,0.1,0.1,0,'Branche1')
    P_cube(P,-0.3,1.6,0.3,0.1,0.1,0.5,0,'Branche2')
    P_cube(P,-0.2,2.1,-0.2,0.4,0.08,0.08,0,'Branche3')
    P_sph(P,0,3,0,1.4,1,'F1')
    P_sph(P,0.6,2.5,0.4,1.0,1,'F2')
    P_sph(P,-0.5,3.3,-0.3,0.9,1,'F3')
    P_sph(P,0.3,3.5,0.2,0.7,2,'F4')
    P_sph(P,-0.4,2.7,0.5,0.8,2,'F5')
    M=[{'n':'Tronc','c':[.42,.27,.14,1]},{'n':'F1','c':[.20,.62,.20,1]},{'n':'F2','c':[.15,.55,.15,1]}]
    write_glb('arbre.glb',P,M)

# ===================== FONTAINE =====================
def gen_fontaine():
    P=[]
    P_cyl(P,0,0.5,0,2.8,1.0,0,'Base')
    P_cyl(P,0,0.6,0,3.0,0.15,0,'Rebord')
    P_cyl(P,0,0.85,0,2.3,0.3,1,'Eau')
    P_cyl(P,0,2.2,0,0.35,2.8,0,'Colonne')
    P_sph(P,0,3.8,0,0.5,0,'Chapiteau')
    for a in [0,90,180,270]:
        r=math.radians(a)
        P_cube(P,math.sin(r)*2.5,0.8,math.cos(r)*2.5,0.3,0.2,0.3,0,f'Garg{a}')
    for a in [45,135,225,315]:
        r=math.radians(a)
        P_cube(P,math.sin(r)*5,0.35,math.cos(r)*5,2.2,0.35,0.5,2,f'Banc{a}')
        P_cube(P,math.sin(r)*5,0.65,math.cos(r)*5-0.2,2.2,0.3,0.08,2,f'Dossier{a}')
    M=[{'n':'Pierre','c':[.58,.56,.54,1]},{'n':'Eau','c':[.18,.58,.88,1]},{'n':'Bois','c':[.50,.32,.18,1]}]
    write_glb('fontaine.glb',P,M)

# ===================== LAMPADAIRE =====================
def gen_lampadaire():
    P=[]
    P_cyl(P,0,2,0,0.1,4,0,'Poteau')
    P_cube(P,0.35,4.1,0,0.7,0.08,0.08,0,'Crochet')
    P_sph(P,0.35,3.9,0,0.28,1,'Lampe')
    P_cone(P,0.35,4.0,0,0.35,0.15,0,'AbatJour')
    P_cyl(P,0,0.05,0,0.2,0.1,0,'Base')
    M=[{'n':'Metal','c':[.18,.18,.18,1]},{'n':'Lumiere','c':[1,.90,.30,1]}]
    write_glb('lampadaire.glb',P,M)

# ===================== ENNEMIS =====================
def gen_ennemi(nom,col,s):
    P=[]
    P_cube(P,0,s*0.35,0,s,s*0.65,s*1.3,0,'Corps')
    P_sph(P,0,s*0.7,s*0.5,s*0.35,0,'Tete')
    P_sph(P,-s*0.15,s*0.75,s*0.75,0.04,1,'OeilG')
    P_sph(P,s*0.15,s*0.75,s*0.75,0.04,1,'OeilD')
    P_sph(P,-s*0.25,s*0.9,s*0.4,s*0.12,0,'OreilleG')
    P_sph(P,s*0.25,s*0.9,s*0.4,s*0.12,0,'OreilleD')
    for j in range(4):
        lx=(-1 if j%2==0 else 1)*s*0.4;lz=(1 if j<2 else -1)*s*0.45
        P_cyl(P,lx,s*0.12,lz,0.04,s*0.25,0,f'P{j}')
        P_sph(P,lx,0,lz,0.03,2,f'G{j}')
    P_cyl(P,0,s*0.4,-s*0.7,0.02,s*0.4,0,'Queue')
    M=[{'n':'Corps','c':[col[0],col[1],col[2],1]},{'n':'Yeux','c':[1,0,0,1]},{'n':'Griffes','c':[.9,.9,.8,1]}]
    write_glb(f'ennemi_{nom}.glb',P,M)

# ===================== PNJ =====================
def gen_pnj(nom,col):
    P=[]
    P_cyl(P,0,0.45,0,0.22,0.85,0,'Corps')
    P_sph(P,0,1.0,0,0.2,1,'Tete')
    P_cyl(P,0,1.2,0,0.25,0.1,0,'Chapeau')
    P_cone(P,0,1.25,0,0.2,0.2,0,'ChapeauH')
    P_sph(P,-0.07,1.02,0.17,0.03,2,'OeilG')
    P_sph(P,0.07,1.02,0.17,0.03,2,'OeilD')
    P_sph(P,0,0.98,0.2,0.03,1,'Nez')
    for s in [-1,1]:
        P_cyl(P,s*0.1,0.18,0,0.07,0.35,3,f'Jambe{"G" if s<0 else "D"}')
        P_cyl(P,s*0.28,0.5,0,0.06,0.45,0,f'Bras{"G" if s<0 else "D"}')
        P_sph(P,s*0.28,0.25,0,0.06,1,f'Main{"G" if s<0 else "D"}')
        P_cube(P,s*0.1,0.03,0.02,0.16,0.06,0.22,4,f'Chauss{"G" if s<0 else "D"}')
    M=[{'n':'Vet','c':col},{'n':'Peau','c':[.92,.78,.62,1]},{'n':'Oeil','c':[.08,.08,.08,1]},
       {'n':'Pant','c':[.22,.22,.22,1]},{'n':'Chauss','c':[.35,.25,.15,1]}]
    write_glb(f'pnj_{nom}.glb',P,M)

# ===================== MAIN =====================
if __name__=='__main__':
    print('='*60)
    print('  LIBREVIES — Generation des modeles 3D')
    print('='*60)
    print('\nPersonnage...');gen_perso()
    print('\nBatiments (7)...');gen_bats()
    print('\nArbre...');gen_arbre()
    print('\nFontaine...');gen_fontaine()
    print('\nLampadaire...');gen_lampadaire()
    print('\nEnnemis...')
    gen_ennemi('souris',[.65,.65,.60],0.3)
    gen_ennemi('rat',[.45,.42,.32],0.5)
    gen_ennemi('araignee',[.18,.16,.16],0.45)
    print('\nPNJ...')
    gen_pnj('vendeur',[.82,.20,.15,1])
    gen_pnj('forgeron',[.16,.70,.38,1])
    gen_pnj('maire',[.50,.25,.62,1])
    gen_pnj('marchand',[.82,.40,.08,1])
    print(f'\n{"="*60}')
    print(f'  {len([f for f in os.listdir(".") if f.endswith(".glb")])} fichiers .glb generes')
    print('='*60)
