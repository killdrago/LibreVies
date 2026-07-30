"""
Viewer 3D — python viewer.py
Ouvre le viewer dans le navigateur par defaut
"""
import http.server
import threading
import webbrowser
import os
import sys
import time

PORT = 8765
DIR = os.path.dirname(os.path.abspath(__file__))

# HTML du viewer
HTML = '''<!DOCTYPE html>
<html>
<head>
<meta charset="UTF-8">
<title>LibreVieS Viewer</title>
<style>
*{margin:0;padding:0}
body{background:#1a1a2e;overflow:hidden;font-family:Arial}
#ui{position:fixed;top:10px;left:10px;z-index:10;color:#fff;background:rgba(0,0,0,.85);padding:15px;border-radius:10px}
#ui h3{color:#f1c40f;margin-bottom:8px}
.btn{background:#f1c40f;color:#000;border:none;padding:8px 16px;border-radius:5px;cursor:pointer;font-weight:bold;margin-top:8px}
.btn:hover{background:#e0b40e}
#st{position:fixed;bottom:10px;left:10px;right:10px;color:#fff;background:rgba(0,0,0,.85);padding:10px 15px;border-radius:8px;font-size:14px}
p{font-size:12px;color:#aaa;margin-top:5px}
#fi{display:none}
</style>
</head>
<body>
<div id="ui">
<h3>LibreVieS Viewer 3D</h3>
<button class="btn" onclick="fi.click()">Ouvrir .glb</button>
<button class="btn" onclick="loadUrl()" style="background:#3498db;color:#fff;margin-left:5px">Charger perso_voxel.glb</button>
<p>Souris: tourner | Molette: zoom | Clic droit: deplacer</p>
</div>
<div id="st">En attente...</div>
<input type="file" id="fi" accept=".glb">

<script src="https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js"></script>
<script src="https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/loaders/GLTFLoader.js"></script>
<script>
const S=new THREE.Scene();
S.background=new THREE.Color(0x1a1a2e);
S.add(new THREE.GridHelper(10,10,0x444466,0x333355));
S.add(new THREE.AxesHelper(2));
S.add(new THREE.AmbientLight(0xffffff,0.8));
const dl=new THREE.DirectionalLight(0xffffff,0.8);
dl.position.set(3,6,3);S.add(dl);
S.add(new THREE.PointLight(0xffffff,0.5,20));

const C=new THREE.PerspectiveCamera(50,innerWidth/innerHeight,0.001,100);
C.position.set(2,1.5,2);
const R=new THREE.WebGLRenderer({antialias:true});
R.setSize(innerWidth,innerHeight);
R.setPixelRatio(devicePixelRatio);
document.body.appendChild(R.domElement);

const ctrl=new THREE.OrbitControls(C,R.domElement);
ctrl.target.set(0,0.5,0);ctrl.enableDamping=true;ctrl.dampingFactor=0.05;ctrl.update();

function anim(){requestAnimationFrame(anim);ctrl.update();R.render(S,C)}anim();
addEventListener('resize',()=>{C.aspect=innerWidth/innerHeight;C.updateProjectionMatrix();R.setSize(innerWidth,innerHeight)});

let cur=null;
fi.addEventListener('change',e=>{if(e.target.files[0])load(e.target.files[0])});

function loadUrl(){
    load('./glb/perso_voxel.glb');
}

function load(f){
    const st=document.getElementById('st');
    st.textContent='Chargement...';st.style.color='#ff0';
    
    if(typeof f==='string'){
        // URL
        new THREE.GLTFLoader().load(f,
        function(g){setup(g,f.split('/').pop())},
        function(xhr){},
        function(err){st.textContent='ERREUR: '+err.message;st.style.color='#f00'});
    } else {
        // File
        const r=new FileReader();
        r.onload=function(e){
            new THREE.GLTFLoader().parse(e.target.result,'',
            function(g){setup(g,f.name)},
            function(err){st.textContent='ERREUR: '+(err.message||err);st.style.color='#f00'});
        };
        r.readAsArrayBuffer(f);
    }
}

function setup(g,name){
    if(cur)S.remove(cur);
    cur=g.scene;
    const box=new THREE.Box3().setFromObject(cur);
    const cen=box.getCenter(new THREE.Vector3());
    const sz=box.getSize(new THREE.Vector3());
    const mx=Math.max(sz.x,sz.y,sz.z);
    cur.position.set(-cen.x,-box.min.y,-cen.z);
    const d=Math.max(mx*2,1);
    C.position.set(d,d*0.7,d);
    ctrl.target.set(0,sz.y*0.4,0);ctrl.update();
    S.add(cur);
    let mc=0;cur.traverse(c=>{if(c.isMesh)mc++});
    const st=document.getElementById('st');
    st.innerHTML='<b>'+name+'</b> charge !<br>'+mc+' meshes | '+
        sz.x.toFixed(2)+'x'+sz.y.toFixed(2)+'x'+sz.z.toFixed(2);
    st.style.color='#0f0';
}
</script>
</body>
</html>'''

# Sauvegarder le HTML
html_path = os.path.join(DIR, '_viewer.html')
with open(html_path, 'w', encoding='utf-8') as f:
    f.write(HTML)

# Lancer le serveur
os.chdir(DIR)
handler = http.server.SimpleHTTPRequestHandler
httpd = http.server.HTTPServer(('127.0.0.1', PORT), handler)

def run_server():
    httpd.serve_forever()

t = threading.Thread(target=run_server, daemon=True)
t.start()

url = f'http://localhost:{PORT}/_viewer.html'
print(f'Viewer ouvert : {url}')
print(f'Dossier servi : {DIR}')
print(f'Fichiers .glb trouves :')
for f in os.listdir(DIR):
    if f.endswith('.glb'):
        print(f'  - {f}')
print()
print('Cliquez sur "Charger perso_voxel.glb" dans le navigateur')
print('Ou ouvrez un autre .glb avec le bouton')
print()
print('Appuyez sur Ctrl+C pour fermer')

webbrowser.open(url)

try:
    while True:
        time.sleep(1)
except KeyboardInterrupt:
    print('\nFermeture...')
    httpd.shutdown()
