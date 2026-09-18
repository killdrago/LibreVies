using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Jeu LibreVies autonome pour Unity.
/// Le monde est construit en primitives afin que la build Windows ne dépende
/// d'aucun asset ou plugin externe. Le launcher n'a besoin que de l'exécutable
/// produit par Unity dans game/LibreViesGame.exe.
/// </summary>
public sealed class LibreViesGame : MonoBehaviour
{
    private const float WorldSize = 90f;
    private const float TownRadius = 32f;
    private const float VillageRadius = 26f;
    private const float PlayerSpeed = 5f;
    private const float RunSpeed = 9f;
    private const int MaxHp = 100;
    private const int HammerDamage = 25;
    // Valeurs reprises de la reference de jeu : hauteur logique par defaut
    // d'un obstacle, hauteur de la cloture du village (franchissable en
    // sautant par le heros, jamais par les monstres), rayon du heros.
    private const float HauteurCollision = 2f;
    private const float HauteurCloture = 1f;
    private const float RayonJoueur = 0.45f;
    // Saut : 8 m/s avec une gravite de 20 -> 1,60 m de hauteur maximale,
    // exactement de quoi sauter la cloture de 1 m.
    private const float Gravite = 20f;
    private const float ForceSaut = 8f;

    // Trace de la route (memes points que le ruban visible) : sert aux
    // collisions de la cloture (on passe par les portails) et au pave.
    private static readonly Vector2[] RoutePoints =
    {
        new Vector2(0, 30), new Vector2(3, 18), new Vector2(-2, 6), new Vector2(1, -8),
        new Vector2(4, -20), new Vector2(-1, -34), new Vector2(1, -48), new Vector2(0, -67)
    };

    private readonly List<Obstacle> obstacles = new List<Obstacle>();
    private readonly List<EnemyState> enemies = new List<EnemyState>();
    private readonly List<PickupState> pickups = new List<PickupState>();
    private readonly List<GameObject> clouds = new List<GameObject>();
    private readonly List<Material> materials = new List<Material>();

    private Transform player;
    private Transform cameraPivot;
    private Camera gameCamera;
    private float cameraDistance = 6.5f;
    private float cameraPitch = 18f;
    private float cameraYaw;
    // Sensibilite de la souris, reglable comme dans les options du jeu.
    // lisse GetAxis("Mouse X") : on utilise GetAxisRaw pour une reponse
    // immediate, sinon la camera parait "longue a la detente".
    private float cameraSensitivity = 3f;
    private Shader cachedShader;
    private bool firstPerson;
    private bool cameraDragging;
    private Vector3 playerVelocity;
    private bool playerGrounded = true;
    private float walkClock;
    private float attackCooldown;
    private float attackAnimation;
    private float regenClock;
    private float playerProtection;
    private float speedBoost;
    private int hp = MaxHp;
    private int coins;
    private int rocks;
    private int xp;
    private int level = 1;
    private int ratsKilled;
    private int spidersKilled;
    private int selectedSlot;
    private readonly int[] potions = { 5, 5 };
    private bool dead;
    private bool inventoryOpen;
    private bool optionsOpen;
    private bool questOpen = true;
    private string infoMessage = "";
    private float infoTimer;
    private float brightness = 0.3f;
    private float contrast = 1f;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private GUIStyle boxStyle;

    private sealed class Obstacle
    {
        public bool Cercle;     // true = cercle (Rayon), false = boite (Largeur/Profondeur)
        public float X;
        public float Z;
        public float Rayon;
        public float Largeur;
        public float Profondeur;
        public float Portee;    // elagage rapide : rayon + 1, ou max(w, d) / 2 + 1
        public float Hauteur;   // hauteur AU-DESSUS du terrain
        public bool Actif = true;
    }

    private sealed class EnemyState
    {
        public GameObject Root;
        public bool Spider;
        public int Hp = MaxHp;
        public bool Alive = true;
        public float AttackCooldown;
        public float RespawnAt;
        public Vector3 Home;
        public Transform[] Legs;
        public Obstacle Corps;      // pour que le heros ne traverse pas la bete
        public Vector3 Direction;   // direction de deplacement (errance / poursuite)
        public float WanderTimer;
        public float Speed = 1.6f;
    }

    private sealed class PickupState
    {
        public GameObject Root;
        public bool Coin;
        public bool Active = true;
        public Vector3 Home;
        public float RespawnAt;
    }

    private void Awake()
    {
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
        Screen.SetResolution(1280, 720, false);
        gameCamera = Camera.main;
        if (gameCamera == null)
        {
            var cameraObject = new GameObject("Main Camera");
            gameCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
        gameCamera.clearFlags = CameraClearFlags.Skybox;
        // Champ de vision 55 : le cadrage de la reference de jeu.
        gameCamera.fieldOfView = 55f;
        LoadOptions();
        CreateMaterials();
        CreateEnvironment();
        CreateTerrain();
        CreateRoad();
        CreateTown();
        CreateCastle();
        CreateFence();
        CreateTreesAndProps();
        CreateClouds();
        CreatePlayer();
        CreateEnemies();
        CreatePickups();
        ShowInfo("LibreVies — monde Unity prêt");
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        UpdateClouds(dt);
        UpdatePickups(dt);
        UpdateEnemies(dt);
        UpdatePlayer(dt);
        UpdateCamera();
        UpdateEffects(dt);
        UpdateHudState(dt);
    }

    private void CreateMaterials()
    {
        materials.Clear();
        MakeMaterial("Terrain", new Color(0.23f, 0.42f, 0.20f));
        MakeMaterial("Dirt", new Color(0.45f, 0.30f, 0.17f));
        MakeMaterial("Stone", new Color(0.42f, 0.45f, 0.48f));
        MakeMaterial("Wood", new Color(0.35f, 0.17f, 0.07f));
        MakeMaterial("Wall", new Color(0.65f, 0.55f, 0.38f));
        MakeMaterial("Roof", new Color(0.38f, 0.08f, 0.05f));
        MakeMaterial("Leaf", new Color(0.08f, 0.31f, 0.10f));
        MakeMaterial("Player", new Color(0.16f, 0.35f, 0.70f));
        MakeMaterial("Skin", new Color(0.86f, 0.59f, 0.40f));
        MakeMaterial("Enemy", new Color(0.28f, 0.18f, 0.12f));
        MakeMaterial("Spider", new Color(0.12f, 0.08f, 0.07f));
        MakeMaterial("Gold", new Color(1f, 0.65f, 0.08f), true);
        MakeMaterial("Water", new Color(0.08f, 0.45f, 0.85f), true);
        MakeMaterial("Red", new Color(0.80f, 0.06f, 0.04f), true);
        MakeMaterial("White", Color.white);
    }

    private Material MakeMaterial(string name, Color color, bool emission = false)
    {
        // Les materiaux sont crees en code : aucun n'est reference par un asset,
        // donc Unity peut retirer les shaders integres de la build. C'est ce qui
        // affichait tout le monde en MAGENTA (Shader.Find renvoie null).
        // ResoudreShader() passe par un shader du projet place dans Resources,
        // qui est toujours embarque : plus de monde rose.
        Shader shader = ResoudreShader();
        if (shader == null)
        {
            Debug.LogError("LibreVies : aucun shader disponible pour " + name);
            materials.Add(null);
            return null;
        }
        var material = new Material(shader) { name = name };
        if (material.HasProperty("_Color")) material.color = color;
        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.7f);
        }
        materials.Add(material);
        return material;
    }

    private Shader ResoudreShader()
    {
        if (cachedShader != null) return cachedShader;

        // 1) Eclairage complet : disponible dans l'editeur et dans la plupart
        //    des builds, tant qu'Unity ne l'a pas retire.
        cachedShader = Shader.Find("Standard");

        // 2) Le shader du projet (Assets/Resources/LVShaders/LVColor) : les
        //    assets de Resources sont TOUJOURS inclus dans la build.
        if (cachedShader == null) cachedShader = Resources.Load<Shader>("LVShaders/LVColor");

        // 3) Derniers recours integres (sans eclairage, mais colores).
        if (cachedShader == null) cachedShader = Shader.Find("Unlit/Color");
        if (cachedShader == null) cachedShader = Shader.Find("Sprites/Default");
        if (cachedShader == null) cachedShader = Shader.Find("UI/Default");

        if (cachedShader != null) Debug.Log("LibreVies : shader utilisé = " + cachedShader.name);
        return cachedShader;
    }

    private Material Mat(string name)
    {
        for (int i = 0; i < materials.Count; i++)
            if (materials[i] != null && materials[i].name == name) return materials[i];
        for (int i = 0; i < materials.Count; i++)
            if (materials[i] != null) return materials[i];
        return null;
    }

    private void CreateEnvironment()
    {
        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.42f, 0.53f, 0.74f);
        RenderSettings.ambientEquatorColor = new Color(0.58f, 0.56f, 0.45f);
        RenderSettings.ambientGroundColor = new Color(0.17f, 0.15f, 0.11f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.68f, 0.77f, 0.88f);
        RenderSettings.fogDensity = 0.0022f;
        CreateSky();
        var sunObject = new GameObject("Soleil");
        var sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.3f;                 // energie du soleil de la reference
        sun.color = new Color(1f, 0.90f, 0.74f);
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(52f, -32f, 0f);
    }

    private void CreateSky()
    {
        // Ciel bleu vif avec un horizon chaud, comme Creer_environnement() de
        // de la reference de jeu. Repli en cascade : aucun de ces shaders ne peut
        // laisser le ciel noir, et le dernier laisse simplement le ciel Unity.
        Shader procedural = Shader.Find("Skybox/Procedural");
        if (procedural != null)
        {
            var material = new Material(procedural) { name = "Ciel_LibreVies" };
            if (material.HasProperty("_SkyTint")) material.SetColor("_SkyTint", new Color(0.30f, 0.46f, 0.78f));
            if (material.HasProperty("_GroundColor")) material.SetColor("_GroundColor", new Color(0.45f, 0.50f, 0.26f));
            if (material.HasProperty("_AtmosphereThickness")) material.SetFloat("_AtmosphereThickness", 0.85f);
            if (material.HasProperty("_Exposure")) material.SetFloat("_Exposure", 1.35f);
            if (material.HasProperty("_SunSize")) material.SetFloat("_SunSize", 0.04f);
            RenderSettings.skybox = material;
            return;
        }
        Shader secours = Resources.Load<Shader>("LVShaders/LVSky");
        if (secours != null)
        {
            var material = new Material(secours) { name = "Ciel_LibreVies" };
            if (material.HasProperty("_SkyColor")) material.SetColor("_SkyColor", new Color(0.30f, 0.46f, 0.78f));
            if (material.HasProperty("_HorizonColor")) material.SetColor("_HorizonColor", new Color(0.98f, 0.74f, 0.48f));
            if (material.HasProperty("_GroundColor")) material.SetColor("_GroundColor", new Color(0.45f, 0.50f, 0.26f));
            RenderSettings.skybox = material;
        }
    }

    private float TerrainHeight(float x, float z)
    {
        float distance = new Vector2(x, z).magnitude;
        float townBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TownRadius, TownRadius + 26f, distance));
        float hills = Mathf.Sin(x * 0.09f) * Mathf.Cos(z * 0.07f) * 2.2f;
        hills += Mathf.Sin(x * 0.21f + 1.7f) * Mathf.Cos(z * 0.17f + 0.6f) * 0.9f;
        hills += Mathf.Sin((x + z) * 0.05f) * 1.4f;
        float castleDistance = new Vector2(x, z + 78f).magnitude;
        float plateau = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(52f, 20f, castleDistance));
        return Mathf.Lerp(hills * townBlend, 11f, plateau);
    }

    // ------------------------------------------------------------------
    // COLLISIONS
    // Le deplacement ecrit directement dans transform.position : les
    // colliders Unity ne servent a rien ici (aucun Rigidbody). On utilise donc
    // le meme systeme que la reference de jeu : une liste d'obstacles
    // (cercles ou boites) et une resolution mathematique du deplacement.
    // Consequences voulues : on ne traverse plus les murs, on saute par-dessus
    // la cloture, on peut atterrir sur les caisses, et les monstres restent
    // enfermes hors du village.
    // ------------------------------------------------------------------
    private void ColCercle(float x, float z, float r, float h = HauteurCollision)
    {
        obstacles.Add(new Obstacle
        {
            Cercle = true, X = x, Z = z, Rayon = r, Portee = r + 1f, Hauteur = h
        });
    }

    private void ColBoite(float x, float z, float w, float d, float h = HauteurCollision)
    {
        obstacles.Add(new Obstacle
        {
            X = x, Z = z, Largeur = w, Profondeur = d,
            Portee = Mathf.Max(w, d) * 0.5f + 1f, Hauteur = h
        });
    }

    private bool DansVillage(float x, float z)
    {
        return new Vector2(x, z).magnitude < VillageRadius;
    }

    private float DistRoute(Vector2 p)
    {
        float best = 100000f;
        for (int i = 0; i < RoutePoints.Length - 1; i++)
        {
            Vector2 a = RoutePoints[i];
            Vector2 b = RoutePoints[i + 1];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
            best = Mathf.Min(best, (p - (a + ab * t)).magnitude);
        }
        return best;
    }

    // La cloture du village separe l'interieur de l'exterieur : un monstre ne
    // peut pas frapper le heros a travers (sauf au niveau des portails).
    private bool ClotureEntre(Vector3 a, Vector3 b)
    {
        if (DansVillage(a.x, a.z) == DansVillage(b.x, b.z)) return false;
        var milieu = new Vector2((a.x + b.x) * 0.5f, (a.z + b.z) * 0.5f);
        return DistRoute(milieu) > 3.4f;
    }

    // 'ignorer' sert aux monstres : leur propre corps est un obstacle (le heros
    // ne les traverse pas), ils ne doivent donc pas se repousser eux-memes.
    private Vector2 ResoudreCollisions(float px, float pz, float rayon, float pieds = 0f, Obstacle ignorer = null)
    {
        var p = new Vector2(px, pz);
        for (int passe = 0; passe < 2; passe++)
        {
            for (int i = 0; i < obstacles.Count; i++)
            {
                Obstacle c = obstacles[i];
                if (!c.Actif || c == ignorer) continue;
                if (Mathf.Abs(p.x - c.X) > c.Portee + rayon && Mathf.Abs(p.y - c.Z) > c.Portee + rayon) continue;
                // Ce qui est plus bas que le saut peut etre franchi (et on peut
                // atterrir dessus, voir HauteurSupport). Epsilon 0,05 : debout
                // sur un objet, celui-ci ne pousse plus.
                if (pieds > 0f && pieds > TerrainHeight(c.X, c.Z) + c.Hauteur - 0.05f) continue;
                if (c.Cercle)
                {
                    float rr = c.Rayon + rayon;
                    float dx = p.x - c.X;
                    float dz = p.y - c.Z;
                    float d2 = dx * dx + dz * dz;
                    if (d2 < rr * rr)
                    {
                        float d = Mathf.Sqrt(d2);
                        if (d < 0.0001f) p = new Vector2(c.X + rr, c.Z);
                        else p = new Vector2(c.X + dx / d * rr, c.Z + dz / d * rr);
                    }
                }
                else
                {
                    float hw = c.Largeur * 0.5f + rayon;
                    float hd = c.Profondeur * 0.5f + rayon;
                    float dx = p.x - c.X;
                    float dz = p.y - c.Z;
                    if (Mathf.Abs(dx) < hw && Mathf.Abs(dz) < hd)
                    {
                        float ox = hw - Mathf.Abs(dx);
                        float oz = hd - Mathf.Abs(dz);
                        if (ox < oz) p.x = c.X + (dx >= 0f ? 1f : -1f) * hw;
                        else p.y = c.Z + (dz >= 0f ? 1f : -1f) * hd;
                    }
                }
            }
            // Cloture du village : bloquante SAUF aux portails (la ou passe la
            // route). Le heros peut sauter par-dessus ; les monstres (pieds = 0,
            // ils ne sautent pas) ne la franchissent jamais.
            float dl = p.magnitude;
            if (pieds <= TerrainHeight(p.x, p.y) + HauteurCloture - 0.05f
                && Mathf.Abs(dl - VillageRadius) < 0.4f + rayon
                && DistRoute(p) > 3.4f)
            {
                if (dl < 0.001f) p = new Vector2(VillageRadius + 0.4f + rayon, 0f);
                else if (dl >= VillageRadius) p = p / dl * (VillageRadius + 0.4f + rayon);
                else p = p / dl * (VillageRadius - 0.4f - rayon);
            }
        }
        return p;
    }

    // Atterrissage sur les objets : quand le heros retombe au-dessus d'un
    // objet dont le sommet est sous ses pieds, il se pose DESSUS (perche).
    private float HauteurSupport(float x, float z, float pieds)
    {
        float sol = TerrainHeight(x, z);
        // La route est pavee (sommet ~ +0,10) : les pieds ne s'enfoncent plus.
        float dr = DistRoute(new Vector2(x, z));
        if (dr < 2.9f) sol += 0.10f * Mathf.Clamp01((2.9f - dr) / 0.6f);
        for (int i = 0; i < obstacles.Count; i++)
        {
            Obstacle c = obstacles[i];
            if (!c.Actif) continue;
            if (Mathf.Abs(x - c.X) > 4f && Mathf.Abs(z - c.Z) > 4f) continue;
            bool dedans;
            if (c.Cercle) dedans = new Vector2(x - c.X, z - c.Z).magnitude < c.Rayon + 0.1f;
            else dedans = Mathf.Abs(x - c.X) < c.Largeur * 0.5f + 0.05f
                && Mathf.Abs(z - c.Z) < c.Profondeur * 0.5f + 0.05f;
            if (!dedans) continue;
            float sommet = TerrainHeight(c.X, c.Z) + c.Hauteur;
            if (sommet > sol && pieds >= sommet - 0.25f) sol = sommet;
        }
        return sol;
    }

    private void CreateTerrain()
    {
        const int cells = 36;
        const float step = WorldSize * 2f / cells;
        var mesh = new Mesh { name = "LibreViesTerrain" };
        var vertices = new Vector3[(cells + 1) * (cells + 1)];
        var triangles = new int[cells * cells * 6];
        for (int z = 0; z <= cells; z++)
        {
            for (int x = 0; x <= cells; x++)
            {
                float wx = -WorldSize + x * step;
                float wz = -WorldSize + z * step;
                vertices[z * (cells + 1) + x] = new Vector3(wx, TerrainHeight(wx, wz), wz);
            }
        }
        int index = 0;
        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                int a = z * (cells + 1) + x;
                int b = a + 1;
                int c = a + cells + 1;
                int d = c + 1;
                triangles[index++] = a; triangles[index++] = c; triangles[index++] = b;
                triangles[index++] = b; triangles[index++] = c; triangles[index++] = d;
            }
        }
        mesh.vertices = vertices;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        var terrain = new GameObject("Terrain_Unity");
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        var terrainRenderer = terrain.AddComponent<MeshRenderer>();
        var terrainMaterial = Mat("Terrain");
        if (terrainMaterial != null) terrainRenderer.sharedMaterial = terrainMaterial;
        terrain.AddComponent<MeshCollider>().sharedMesh = mesh;
    }

    private GameObject Primitive(PrimitiveType type, Vector3 position, Vector3 scale, string material,
        Transform parent = null, string objectName = "Primitive", bool collider = false)
    {
        var obj = GameObject.CreatePrimitive(type);
        obj.name = objectName;
        obj.transform.SetParent(parent, false);
        obj.transform.localPosition = position;
        obj.transform.localScale = scale;
        var renderer = obj.GetComponent<Renderer>();
        var sharedMaterial = Mat(material);
        if (sharedMaterial != null) renderer.sharedMaterial = sharedMaterial;
        if (!collider)
        {
            var currentCollider = obj.GetComponent<Collider>();
            if (currentCollider != null) Destroy(currentCollider);
        }
        return obj;
    }

    private GameObject Box(Vector3 position, Vector3 scale, string material, Transform parent = null,
        string name = "Box", bool collider = false, Quaternion rotation = default(Quaternion))
    {
        var obj = Primitive(PrimitiveType.Cube, position, scale, material, parent, name, collider);
        obj.transform.localRotation = rotation;
        return obj;
    }

    private void CreateRoad()
    {
        // Memes points que RoutePoints (voir constantes) : la trace sert aux
        // collisions (portails de la cloture) et au pave sous les pieds.
        for (int i = 0; i < RoutePoints.Length - 1; i++)
        {
            Vector3 a = new Vector3(RoutePoints[i].x, 0f, RoutePoints[i].y);
            Vector3 b = new Vector3(RoutePoints[i + 1].x, 0f, RoutePoints[i + 1].y);
            Vector3 direction = b - a;
            Vector3 center = (a + b) * 0.5f;
            center.y = TerrainHeight(center.x, center.z) + 0.04f;
            Box(center, new Vector3(5f, 0.10f, direction.magnitude), "Dirt", null,
                "Route", false, Quaternion.LookRotation(direction));
        }
    }

    private void CreateTown()
    {
        CreateBuilding(new Vector3(-14, 0, 10), new Vector3(8, 4, 7), "Maison_Ouest");
        CreateBuilding(new Vector3(15, 0, 8), new Vector3(8, 5, 8), "Maison_Est");
        CreateBuilding(new Vector3(-13, 0, -11), new Vector3(7, 3.5f, 7), "Atelier");
        CreateBuilding(new Vector3(14, 0, -12), new Vector3(9, 4, 7), "Auberge");
        CreateBuilding(new Vector3(-22, 0, -1), new Vector3(6, 3, 6), "Entrepot");
        CreateBuilding(new Vector3(23, 0, -2), new Vector3(6, 3, 6), "Forge");
        // Bâtiments supplémentaires du village de départ.
        CreateBuilding(new Vector3(-3, 0, 21), new Vector3(7, 4, 6), "Mairie");
        CreateBuilding(new Vector3(-19, 0, 13), new Vector3(5, 3.5f, 5), "Maison_Nord");
        CreateBuilding(new Vector3(-3, 0, -20), new Vector3(7, 4, 6), "Maison_Sud");
        CreateFountain(new Vector3(9, 0, 15));
        CreateGuard(new Vector3(-12, 0, 26));
        CreateGuard(new Vector3(12, 0, 26));
    }

    private void CreateBuilding(Vector3 position, Vector3 size, string name)
    {
        var root = new GameObject(name).transform;
        root.position = new Vector3(position.x, TerrainHeight(position.x, position.z), position.z);
        Box(Vector3.up * (size.y * 0.5f), size, "Wall", root, "Murs", true);
        Box(new Vector3(0, size.y + 0.15f, 0), new Vector3(size.x + 0.8f, 0.35f, size.z + 0.8f), "Roof", root, "Toit");
        // On ne traverse plus les maisons (0,5 m de marge comme la reference).
        ColBoite(position.x, position.z, size.x + 0.5f, size.z + 0.5f, size.y);
        Box(new Vector3(0, 1.0f, -size.z * 0.51f), new Vector3(1.2f, 2f, 0.12f), "Wood", root, "Porte");
        for (int side = -1; side <= 1; side += 2)
        {
            Box(new Vector3(side * size.x * 0.27f, 1.8f, -size.z * 0.515f), new Vector3(1.0f, 0.75f, 0.10f), "Water", root, "Fenetre");
        }
    }

    private void CreateFountain(Vector3 position)
    {
        var root = new GameObject("Fontaine").transform;
        root.position = new Vector3(position.x, TerrainHeight(position.x, position.z), position.z);
        Primitive(PrimitiveType.Cylinder, Vector3.zero, new Vector3(3.4f, 0.18f, 3.4f), "Stone", root, "Bassin");
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 1.1f, 0), new Vector3(0.32f, 1.1f, 0.32f), "Stone", root, "Colonne");
        Primitive(PrimitiveType.Sphere, new Vector3(0, 2.3f, 0), new Vector3(0.5f, 0.5f, 0.5f), "Water", root, "Orbe");
        ColCercle(position.x, position.z, 1.7f, 0.36f);   // bassin
        ColCercle(position.x, position.z, 0.32f, 2.2f);   // colonne
    }

    private void CreateCastle()
    {
        var root = new GameObject("Chateau").transform;
        Vector3 center = new Vector3(0, TerrainHeight(0, -67), -67);
        root.position = center;
        Box(new Vector3(0, 2.5f, 0), new Vector3(21, 5, 13), "Stone", root, "Donjon", true);
        for (int x = -9; x <= 9; x += 6)
        {
            Box(new Vector3(x, 5.7f, -6), new Vector3(1.2f, 1.4f, 1.2f), "Stone", root, "Creneau");
            Box(new Vector3(x, 5.7f, 6), new Vector3(1.2f, 1.4f, 1.2f), "Stone", root, "Creneau");
        }
        foreach (float x in new[] { -10f, 10f })
        foreach (float z in new[] { -6f, 6f })
        {
            Primitive(PrimitiveType.Cylinder, new Vector3(x, 3.8f, z), new Vector3(2f, 3.8f, 2f), "Stone", root, "Tour", false);
            Primitive(PrimitiveType.Cylinder, new Vector3(x, 8.0f, z), new Vector3(2.3f, 0.5f, 2.3f), "Roof", root, "Toit_Tour");
        }
        Box(new Vector3(0, 1.2f, 6.7f), new Vector3(3, 2.4f, 0.3f), "Wood", root, "Porte");
        // Memes obstacles que la reference : donjon, tours, porte.
        ColBoite(0, -67, 21, 13, 5f);
        ColCercle(-10, -73, 1.0f, 7.6f);
        ColCercle(10, -73, 1.0f, 7.6f);
        ColCercle(-10, -61, 1.0f, 7.6f);
        ColCercle(10, -61, 1.0f, 7.6f);
        ColBoite(0, -60.3f, 3, 0.6f, 2.4f);
    }

    private void CreateFence()
    {
        const int posts = 96;                 // 96 poteaux, comme la reference
        // 1) Ou la route traverse l'enceinte, il y a un PORTAIL (deux trous
        //    dans cet anneau : au nord et au sud). On les repere d'abord, pour
        //    que le dessin et les collisions soient d'accord.
        var portails = new List<Vector2>();   // (angle de debut, angle de fin)
        const int pas = 720;
        bool dansTrou = false;
        float debut = 0f;
        for (int i = 0; i <= pas; i++)
        {
            float a = i * Mathf.PI * 2f / pas;
            bool trou = DistRoute(new Vector2(Mathf.Cos(a) * VillageRadius, Mathf.Sin(a) * VillageRadius)) < 3.0f;
            if (trou && !dansTrou) { dansTrou = true; debut = a; }
            else if (!trou && dansTrou)
            {
                dansTrou = false;
                portails.Add(new Vector2(debut, a));
            }
        }

        // 2) Poteaux + traverses sur toute la longueur, portails exclus.
        for (int i = 0; i < posts; i++)
        {
            float angle = i * Mathf.PI * 2f / posts;
            float x = Mathf.Cos(angle) * VillageRadius;
            float z = Mathf.Sin(angle) * VillageRadius;
            if (DistRoute(new Vector2(x, z)) < 3.0f) continue;
            float y = TerrainHeight(x, z);
            Primitive(PrimitiveType.Cylinder, new Vector3(x, y + 0.65f, z), new Vector3(0.14f, 0.65f, 0.14f), "Wood", null, "Cloture");
        }
        for (int i = 0; i < posts; i++)
        {
            float angle = (i + 0.5f) * Mathf.PI * 2f / posts;
            float x = Mathf.Cos(angle) * VillageRadius;
            float z = Mathf.Sin(angle) * VillageRadius;
            if (DistRoute(new Vector2(x, z)) < 3.2f) continue;
            float y = TerrainHeight(x, z) + 0.85f;
            var rail = Box(new Vector3(x, y, z), new Vector3(0.12f, 0.14f, 2.8f), "Wood", null, "Traverse");
            rail.transform.rotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
        }

        // 3) Les portails : grands poteaux, linteau et panneau LIBREVIES.
        for (int g = 0; g < portails.Count; g++)
        {
            float a0 = portails[g].x;
            float a1 = portails[g].y;
            foreach (float a in new[] { a0, a1 })
            {
                float gx = Mathf.Cos(a) * VillageRadius;
                float gz = Mathf.Sin(a) * VillageRadius;
                float gy = TerrainHeight(gx, gz);
                Primitive(PrimitiveType.Cylinder, new Vector3(gx, gy + 2.6f, gz), new Vector3(0.30f, 2.6f, 0.30f), "Wood", null, "Poteau_Portail");
                Box(new Vector3(gx, gy + 5.26f, gz), new Vector3(0.40f, 0.14f, 0.40f), "Wood", null, "Chapeau_Portail");
            }
            float am = (a0 + a1) * 0.5f;
            float mx = Mathf.Cos(am) * VillageRadius;
            float mz = Mathf.Sin(am) * VillageRadius;
            float my = TerrainHeight(mx, mz);
            // Longueur du linteau = corde entre les deux poteaux.
            float longueur = new Vector2(Mathf.Cos(a1) - Mathf.Cos(a0), Mathf.Sin(a1) - Mathf.Sin(a0)).magnitude * VillageRadius + 0.2f;
            // L'axe du linteau suit la corde (donc la route qui passe dessous).
            var corde = new Vector2(Mathf.Cos(a1) - Mathf.Cos(a0), Mathf.Sin(a1) - Mathf.Sin(a0));
            var rotation = Quaternion.LookRotation(new Vector3(corde.x, 0f, corde.y).normalized);
            Box(new Vector3(mx, my + 5.2f, mz), new Vector3(longueur, 0.18f, 0.16f), "Wood", null, "Linteau", false, rotation);
            // Panneau en bois portant le nom du village (deux planches).
            Box(new Vector3(mx, my + 4.35f, mz), new Vector3(2.6f, 0.7f, 0.10f), "Wood", null, "Panneau_Fond", false, rotation);
            Box(new Vector3(mx, my + 4.35f, mz), new Vector3(2.4f, 0.55f, 0.12f), "Wood", null, "Panneau_Bois", false, rotation);
            AjouterTextePanneau(new Vector3(mx, my + 4.35f, mz), rotation);
        }

        // La cloture n'est pas enregistree poteau par poteau : elle est geree
        // comme un anneau (voir ResoudreCollisions). Bloquante SAUF dans les
        // portails, et franchissable en sautant : le comportement de la
        // reference, ou le village reste interdit aux monstres.
    }

    // Nom du village sur le panneau des portails. Le rendu de texte 2D
    // (TextMesh) est optionnel : si aucune police n'est disponible, on garde
    // simplement le panneau en bois, sans erreur.
    private void AjouterTextePanneau(Vector3 position, Quaternion rotation)
    {
        Font police = null;
        try { police = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch (System.Exception) { police = null; }
        if (police == null)
        {
            try { police = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch (System.Exception) { police = null; }
        }
        if (police == null) return;
        var objet = new GameObject("Texte_Portail");
        objet.transform.position = position;
        objet.transform.rotation = rotation;
        var texte = objet.AddComponent<TextMesh>();
        texte.text = "LIBREVIES";
        texte.font = police;
        texte.characterSize = 0.42f;   // des lettres d environ 40 cm, ajustees au panneau
        texte.fontSize = 64;
        texte.anchor = TextAnchor.MiddleCenter;
        texte.color = new Color(0.20f, 0.12f, 0.05f);
        var rendu = objet.GetComponent<MeshRenderer>();
        if (rendu != null) rendu.sharedMaterial = police.material;
        // Le texte regarde vers l'exterieur, comme le panneau.
        objet.transform.Rotate(0f, 180f, 0f, Space.Self);
    }

    private void CreateGuard(Vector3 position)
    {
        var root = new GameObject("Garde").transform;
        root.position = new Vector3(position.x, TerrainHeight(position.x, position.z), position.z);
        Primitive(PrimitiveType.Capsule, new Vector3(0, 1.1f, 0), new Vector3(0.5f, 1.1f, 0.5f), "Player", root, "Corps");
        Box(new Vector3(0.55f, 1.5f, 0), new Vector3(0.10f, 2.2f, 0.10f), "Stone", root, "Hallebarde", false, Quaternion.Euler(0, 0, -8));
        ColCercle(position.x, position.z, 0.4f, 1.8f);
    }

    private void CreateTreesAndProps()
    {
        var random = new System.Random(4217);
        for (int i = 0; i < 75; i++)
        {
            float x = (float)(random.NextDouble() * 170 - 85);
            float z = (float)(random.NextDouble() * 170 - 85);
            if (new Vector2(x, z).magnitude < VillageRadius + 4 || Mathf.Abs(z + 67) < 22) continue;
            CreateTree(x, z, (float)(random.NextDouble() * 1.0 + 0.75));
        }
        for (int i = 0; i < 22; i++)
        {
            float x = (float)(random.NextDouble() * 54 - 27);
            float z = (float)(random.NextDouble() * 54 - 27);
            Box(new Vector3(x, TerrainHeight(x, z) + 0.5f, z), new Vector3(1, 1, 1), i % 2 == 0 ? "Wood" : "Stone", null, "Caisse");
            // 1 m de haut : on saute dessus (saut de 1,60 m) et on peut s'y percher.
            ColBoite(x, z, 1f, 1f, 1f);
        }
    }

    private void CreateTree(float x, float z, float size)
    {
        float y = TerrainHeight(x, z);
        var root = new GameObject("Sapin").transform;
        root.position = new Vector3(x, y, z);
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 1.0f * size, 0), new Vector3(0.24f * size, 1.0f * size, 0.24f * size), "Wood", root, "Tronc");
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 2.0f * size, 0), new Vector3(1.1f * size, 1.2f * size, 1.1f * size), "Leaf", root, "Feuillage");
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 3.25f * size, 0), new Vector3(0.75f * size, 1.15f * size, 0.75f * size), "Leaf", root, "Feuillage_Haut");
        ColCercle(x, z, 0.4f * size, 2.2f);   // tronc

    }

    private void CreateClouds()
    {
        for (int i = 0; i < 7; i++)
        {
            var cloud = new GameObject("Nuage");
            cloud.transform.position = new Vector3(-80 + i * 28, 24 + (i % 3) * 4, -30 + i * 13);
            Box(Vector3.zero, new Vector3(10, 1.4f, 3), "White", cloud.transform, "Nuage_A");
            Box(new Vector3(3, 0.7f, 0), new Vector3(5, 1.4f, 2.2f), "White", cloud.transform, "Nuage_B");
            clouds.Add(cloud);
        }
    }

    private void CreatePlayer()
    {
        player = new GameObject("Joueur").transform;
        // Point de départ choisi pour que la caméra voie le village.
        player.position = new Vector3(0, TerrainHeight(0, 6) + 0.05f, 6);
        cameraPivot = new GameObject("CameraPivot").transform;
        cameraPivot.SetParent(player, false);
        var body = new GameObject("Heros_LowPoly").transform;
        body.SetParent(player, false);
        Primitive(PrimitiveType.Capsule, new Vector3(0, 1.15f, 0), new Vector3(0.55f, 1.15f, 0.55f), "Player", body, "Tunique");
        Primitive(PrimitiveType.Cube, new Vector3(0, 2.25f, 0), new Vector3(0.62f, 0.62f, 0.62f), "Skin", body, "Tete");
        Box(new Vector3(-0.26f, 0.35f, 0), new Vector3(0.25f, 0.7f, 0.3f), "Stone", body, "JambeG");
        Box(new Vector3(0.26f, 0.35f, 0), new Vector3(0.25f, 0.7f, 0.3f), "Stone", body, "JambeD");
        Box(new Vector3(0.75f, 1.35f, 0), new Vector3(0.22f, 0.85f, 0.22f), "Skin", body, "BrasD");
        Box(new Vector3(-0.75f, 1.35f, 0), new Vector3(0.22f, 0.85f, 0.22f), "Skin", body, "BrasG");
        Box(new Vector3(1.0f, 1.1f, 0), new Vector3(0.18f, 0.75f, 0.18f), "Wood", body, "Marteau");
        var collider = player.gameObject.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0, 1.15f, 0);
        collider.height = 2.3f;
        collider.radius = 0.48f;
    }

    private void CreateEnemies()
    {
        var random = new System.Random(8342);
        for (int i = 0; i < 12; i++)
        {
            float angle = (float)random.NextDouble() * Mathf.PI * 2f;
            float radius = VillageRadius + 8f + (float)random.NextDouble() * 32f;
            CreateEnemy(new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius), i % 3 == 0);
        }
    }

    private void CreateEnemy(Vector3 position, bool spider)
    {
        var enemyObject = new GameObject(spider ? "Araignee" : "Rat");
        position.y = TerrainHeight(position.x, position.z);
        enemyObject.transform.position = position;
        EnemyState state = new EnemyState
        {
            Root = enemyObject,
            Spider = spider,
            Home = position,
            Legs = new Transform[spider ? 8 : 0],
            Speed = spider ? 2.0f : 1.6f
        };
        // Le corps de la bete est un obstacle : on ne la traverse pas. Il suit
        // ses deplacements (voir UpdateEnemies) et il est desactive a sa mort.
        state.Corps = new Obstacle
        {
            Cercle = true, X = position.x, Z = position.z,
            Rayon = spider ? 0.5f : 0.45f, Portee = 1.5f,
            Hauteur = spider ? 0.7f : 0.9f
        };
        obstacles.Add(state.Corps);
        string material = spider ? "Spider" : "Enemy";
        Primitive(PrimitiveType.Sphere, new Vector3(0, spider ? 0.45f : 0.55f, 0), spider ? new Vector3(0.85f, 0.35f, 0.85f) : new Vector3(0.65f, 0.45f, 1.0f), material, enemyObject.transform, "Corps");
        Primitive(PrimitiveType.Sphere, new Vector3(0, spider ? 0.62f : 0.72f, -0.48f), new Vector3(0.24f, 0.24f, 0.24f), "Red", enemyObject.transform, "Yeux");
        if (spider)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                var leg = Box(new Vector3(Mathf.Cos(angle) * 0.75f, 0.35f, Mathf.Sin(angle) * 0.75f), new Vector3(0.10f, 0.10f, 1.1f), "Spider", enemyObject.transform, "Patte");
                leg.transform.rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 25f * Mathf.Sin(angle));
                state.Legs[i] = leg.transform;
            }
        }
        enemies.Add(state);
    }

    private void CreatePickups()
    {
        CreatePickup(new Vector3(7, 0, 4), false);
        CreatePickup(new Vector3(-8, 0, 2), false);
        CreatePickup(new Vector3(4, 0, -5), true);
        CreatePickup(new Vector3(-5, 0, -16), true);
        CreatePickup(new Vector3(18, 0, -20), true);
    }

    private void CreatePickup(Vector3 position, bool coin)
    {
        position.y = TerrainHeight(position.x, position.z) + 0.45f;
        var root = Primitive(coin ? PrimitiveType.Cylinder : PrimitiveType.Sphere, position,
            coin ? new Vector3(0.30f, 0.08f, 0.30f) : new Vector3(0.35f, 0.35f, 0.35f),
            coin ? "Gold" : "Stone", null, coin ? "Piece" : "Caillou");
        pickups.Add(new PickupState { Root = root, Coin = coin, Home = position });
    }

    private void UpdateClouds(float dt)
    {
        foreach (var cloud in clouds)
        {
            cloud.transform.position += Vector3.right * (dt * 0.6f);
            if (cloud.transform.position.x > WorldSize + 40f)
                cloud.transform.position = new Vector3(-WorldSize - 40f, cloud.transform.position.y, cloud.transform.position.z);
        }
    }

    private void UpdatePlayer(float dt)
    {
        if (dead)
        {
            if (Input.GetKeyDown(KeyCode.R)) Respawn();
            return;
        }
        if (attackCooldown > 0) attackCooldown -= dt;
        if (attackAnimation > 0) attackAnimation -= dt;
        if (playerProtection > 0) playerProtection -= dt;
        if (speedBoost > 0) speedBoost -= dt;
        regenClock += dt;
        if (regenClock > 3f)
        {
            regenClock = 0;
            if (hp < MaxHp) hp++;
        }

        Vector3 input = Vector3.zero;
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.Z) || Input.GetKey(KeyCode.UpArrow)) input.z += 1;
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow)) input.z -= 1;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.Q) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input.x += 1;
        if (input.sqrMagnitude > 1) input.Normalize();
        Vector3 forward = gameCamera.transform.forward; forward.y = 0; forward.Normalize();
        Vector3 right = gameCamera.transform.right; right.y = 0; right.Normalize();
        Vector3 direction = forward * input.z + right * input.x;
        float speed = Input.GetKey(KeyCode.LeftShift) ? RunSpeed : PlayerSpeed;
        if (speedBoost > 0) speed += 3f;
        if (direction.sqrMagnitude > 0.01f)
        {
            direction.Normalize();
            // On ne traverse plus les murs : le deplacement est resolu contre
            // les obstacles (maisons, tours, arbres, caisses, monstres).
            Vector2 resolu = ResoudreCollisions(
                player.position.x + direction.x * speed * dt,
                player.position.z + direction.z * speed * dt,
                RayonJoueur, player.position.y);
            player.position = new Vector3(resolu.x, player.position.y, resolu.y);
            player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(direction), dt * 12f);
            walkClock += dt * (Input.GetKey(KeyCode.LeftShift) ? 14f : 9f);
        }
        else walkClock = 0;

        if (Input.GetKeyDown(KeyCode.Space) && playerGrounded)
        {
            playerVelocity.y = ForceSaut;
            playerGrounded = false;
        }
        playerVelocity.y -= Gravite * dt;
        player.position += Vector3.up * playerVelocity.y * dt;
        // Le sol n'est pas que le terrain : route pavee et dessus des caisses.
        float ground = HauteurSupport(player.position.x, player.position.z, player.position.y);
        if (player.position.y <= ground)
        {
            player.position = new Vector3(player.position.x, ground, player.position.z);
            playerVelocity.y = 0;
            playerGrounded = true;
        }
        player.position = new Vector3(Mathf.Clamp(player.position.x, -WorldSize + 2, WorldSize - 2), player.position.y,
            Mathf.Clamp(player.position.z, -WorldSize + 2, WorldSize - 2));

        if (Input.GetMouseButtonDown(0)) Attack();
        if (Input.GetKeyDown(KeyCode.E)) CollectNearby();
        if (Input.GetKeyDown(KeyCode.I)) inventoryOpen = !inventoryOpen;
        if (Input.GetKeyDown(KeyCode.O)) optionsOpen = !optionsOpen;
        if (Input.GetKeyDown(KeyCode.Q)) questOpen = !questOpen;
        if (Input.GetKeyDown(KeyCode.V)) firstPerson = !firstPerson;
        if (Input.GetKeyDown(KeyCode.R) && dead) Respawn();
        for (int i = 0; i < 5; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) UseSlot(i);

        if (Input.GetMouseButtonDown(1)) cameraDragging = true;
        if (Input.GetMouseButtonUp(1)) cameraDragging = false;
        if (cameraDragging && Input.GetMouseButton(1))
        {
            // GetAxisRaw : pas de lissage, la caméra suit la souris tout de
            // suite. Sensibilité réglable avec [ et ] (panneau Options).
            cameraYaw += Input.GetAxisRaw("Mouse X") * cameraSensitivity;
            cameraPitch = Mathf.Clamp(
                cameraPitch + Input.GetAxisRaw("Mouse Y") * cameraSensitivity, 5f, 70f);
        }
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            cameraSensitivity = Mathf.Clamp(cameraSensitivity - 0.5f, 0.5f, 10f);
        if (Input.GetKeyDown(KeyCode.RightBracket))
            cameraSensitivity = Mathf.Clamp(cameraSensitivity + 0.5f, 0.5f, 10f);
        cameraDistance = Mathf.Clamp(cameraDistance - Input.mouseScrollDelta.y * 0.5f, 3f, 18f);
    }

    private void UpdateCamera()
    {
        if (player == null) return;
        if (firstPerson)
        {
            // Vue 1re personne : on regarde dans l'axe de la caméra (pitch
            // inclus), comme la visee libre de la reference de jeu.
            gameCamera.transform.position = player.position + Vector3.up * 1.55f;
            gameCamera.transform.rotation = Quaternion.Euler(-cameraPitch, cameraYaw, 0f);
            return;
        }
        Quaternion orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
        Vector3 target = player.position + Vector3.up * 1.1f;
        // Vector3.back : la caméra se place DERRIERE et AU-DESSUS du héros.
        // Avec Vector3.forward elle passait SOUS le terrain (c'était le bug
        // "caméra sous le sol" : pitch +18° envoyait la caméra vers le bas).
        Vector3 position = target + orbit * (Vector3.back * cameraDistance);
        // Filet de sécurité identique à la référence : jamais sous le terrain.
        float sol = TerrainHeight(position.x, position.z) + 0.6f;
        if (position.y < sol) position.y = sol;
        gameCamera.transform.position = position;
        gameCamera.transform.LookAt(target);
    }

    private void UpdateEnemies(float dt)
    {
        foreach (EnemyState enemy in enemies)
        {
            if (!enemy.Alive)
            {
                // Une bete morte ne bloque plus le passage.
                if (enemy.Corps != null) enemy.Corps.Actif = false;
                if (Time.time >= enemy.RespawnAt)
                {
                    enemy.Alive = true; enemy.Hp = MaxHp; enemy.Root.SetActive(true);
                    enemy.Root.transform.position = enemy.Home;
                    if (enemy.Corps != null)
                    {
                        enemy.Corps.Actif = true;
                        enemy.Corps.X = enemy.Home.x; enemy.Corps.Z = enemy.Home.z;
                    }
                }
                continue;
            }
            if (enemy.AttackCooldown > 0) enemy.AttackCooldown -= dt;
            Vector3 position = enemy.Root.transform.position;
            float distance = Vector3.Distance(position, player.position);
            enemy.WanderTimer -= dt;

            // Poursuite quand le heros est proche, errance sinon : les betes ne
            // restent plus plantees a ne rien faire (comme la reference).
            if (distance < 10f && !dead)
            {
                Vector3 vers = player.position - position; vers.y = 0f;
                if (vers.sqrMagnitude > 0.01f) enemy.Direction = vers.normalized;
            }
            else if (enemy.WanderTimer <= 0f)
            {
                enemy.WanderTimer = UnityEngine.Random.Range(1.5f, 4f);
                if (UnityEngine.Random.value < 0.35f) enemy.Direction = Vector3.zero;
                else
                {
                    float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
                    enemy.Direction = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                }
            }

            float speed = enemy.Speed * (distance < 10f ? 1.4f : 0.6f);
            Vector3 deplacement = enemy.Direction;
            // A portee de coup, la bete s'arrete : elle n'entre plus dans les
            // jambes du heros.
            if (distance < 1.5f) deplacement = Vector3.zero;

            // Securite : une bete egaree dans le village est remise dehors.
            if (DansVillage(position.x, position.z))
            {
                Vector2 dehors = new Vector2(position.x, position.z);
                if (dehors.magnitude < 0.001f) dehors = new Vector2(1f, 0f);
                dehors = dehors.normalized * (VillageRadius + 0.5f);
                position.x = dehors.x; position.z = dehors.y;
            }

            if (deplacement.sqrMagnitude > 0.01f)
            {
                Vector3 suivant = position + deplacement * speed * dt;
                if (DansVillage(suivant.x, suivant.z))
                {
                    // Village protege : la bete longe la cloture sans entrer.
                    enemy.WanderTimer = Mathf.Min(enemy.WanderTimer, 0.4f);
                }
                else if (Mathf.Abs(suivant.x) < WorldSize - 3f && Mathf.Abs(suivant.z) < WorldSize - 3f)
                {
                    // Les monstres ne traversent rien non plus (murs, arbres).
                    Vector2 resolu = ResoudreCollisions(suivant.x, suivant.z, 0.35f, 0f, enemy.Corps);
                    position.x = resolu.x; position.z = resolu.y;
                    Vector3 regard = new Vector3(deplacement.x, 0f, deplacement.z);
                    if (regard.sqrMagnitude > 0.01f)
                        enemy.Root.transform.rotation = Quaternion.LookRotation(regard);
                }
            }

            position.y = TerrainHeight(position.x, position.z);
            enemy.Root.transform.position = position;
            if (enemy.Corps != null) { enemy.Corps.X = position.x; enemy.Corps.Z = position.z; }

            // Attaque : a portee et pas a travers la cloture du village.
            if (distance < 1.8f && enemy.AttackCooldown <= 0 && !dead
                && !ClotureEntre(position, player.position))
            {
                enemy.AttackCooldown = 0.8f;
                DamagePlayer(8);
            }

            if (enemy.Spider && enemy.Legs != null)
                for (int i = 0; i < enemy.Legs.Length; i++) enemy.Legs[i].localRotation *= Quaternion.Euler(0, dt * 50f, 0);
        }
    }

    private void UpdatePickups(float dt)
    {
        foreach (PickupState pickup in pickups)
        {
            if (!pickup.Active)
            {
                if (Time.time >= pickup.RespawnAt)
                {
                    pickup.Active = true; pickup.Root.SetActive(true); pickup.Root.transform.position = pickup.Home;
                }
                continue;
            }
            pickup.Root.transform.Rotate(Vector3.up, dt * (pickup.Coin ? 145f : 30f), Space.World);
        }
    }

    private void Attack()
    {
        if (attackCooldown > 0 || dead) return;
        attackCooldown = 0.5f;          // meme cadence que la reference
        attackAnimation = 0.3f;
        Vector3 forward = player.forward; forward.y = 0;
        forward = forward.normalized;
        int touches = 0;
        // Portee corps a corps de 2,4 m, et INTERDIT de taper a travers la
        // cloture du village (comme la reference).
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState enemy = enemies[i];
            if (!enemy.Alive) continue;
            Vector3 delta = enemy.Root.transform.position - player.position; delta.y = 0;
            float distance = delta.magnitude;
            if (distance >= 2.4f) continue;
            if (distance > 0.01f && Vector3.Dot(forward, delta.normalized) < 0.1f) continue;
            if (ClotureEntre(player.position, enemy.Root.transform.position)) continue;
            enemy.Hp -= HammerDamage;
            touches++;
            if (enemy.Hp <= 0)
            {
                enemy.Alive = false; enemy.RespawnAt = Time.time + 10f; enemy.Root.SetActive(false);
                if (enemy.Spider) spidersKilled++; else ratsKilled++;
                GainXp(enemy.Spider ? 35 : 20);
                coins += enemy.Spider ? 4 : 2;
            }
        }
        if (touches == 0) { ShowInfo("Aucune cible à portée"); return; }
        ShowInfo(touches > 1 ? "-25 x" + touches : "-25");
    }

    private void DamagePlayer(int amount)
    {
        if (playerProtection > 0 || dead) return;
        hp = Mathf.Max(0, hp - amount);
        playerProtection = 0.45f;
        if (hp <= 0) Die();
    }

    private void Die()
    {
        dead = true;
        ShowInfo("Vous êtes mort — appuyez sur R pour renaître");
    }

    private void Respawn()
    {
        dead = false; hp = MaxHp; player.position = new Vector3(0, TerrainHeight(0, 6), 6); ShowInfo("Vous êtes revenu à la vie");
    }

    private void CollectNearby()
    {
        // Portees et reapparitions de la reference : caillou 3,0 m / 20 s,
        // piece 3,5 m / 60 s. On ramasse tout ce qui est a portee d'un coup.
        int pris = 0;
        foreach (PickupState pickup in pickups)
        {
            if (!pickup.Active) continue;
            float portee = pickup.Coin ? 3.5f : 3.0f;
            if (Vector3.Distance(player.position, pickup.Root.transform.position) > portee) continue;
            pickup.Active = false;
            pickup.RespawnAt = Time.time + (pickup.Coin ? 60f : 20f);
            pickup.Root.SetActive(false);
            if (pickup.Coin) { coins++; ShowInfo("+1 Pièce d'or"); }
            else { rocks++; ShowInfo("+1 Caillou"); }
            pris++;
        }
        if (pris == 0) ShowInfo("Rien à ramasser ici");
    }

    private void GainXp(int amount)
    {
        xp += amount;
        int needed = level * 100;
        if (xp >= needed) { xp -= needed; level++; ShowInfo("Niveau supérieur !"); }
    }

    private void UseSlot(int slot)
    {
        selectedSlot = slot;
        if (slot == 1 && potions[0] > 0) { potions[0]--; hp = Mathf.Min(MaxHp, hp + 35); ShowInfo("Potion de soin"); }
        else if (slot == 2 && potions[1] > 0) { potions[1]--; speedBoost = 12f; ShowInfo("Potion de vitesse"); }
    }

    private void ShowInfo(string text)
    {
        infoMessage = text; infoTimer = 3f;
    }

    private void UpdateHudState(float dt)
    {
        if (infoTimer > 0) infoTimer -= dt;
    }

    private void OnGUI()
    {
        EnsureStyles();
        GUI.Label(new Rect(20, 18, 300, 30), "LIBREVIES  •  UNITY", titleStyle);
        GUI.Box(new Rect(20, 55, 270, 48), "", boxStyle);
        GUI.Label(new Rect(32, 62, 220, 22), "PV  " + hp + " / " + MaxHp, labelStyle);
        GUI.color = Color.red; GUI.DrawTexture(new Rect(32, 86, 230 * hp / (float)MaxHp, 8), Texture2D.whiteTexture); GUI.color = Color.white;
        GUI.Label(new Rect(32, 108, 260, 24), "Niveau " + level + "    XP " + xp + " / " + (level * 100), smallStyle);
        GUI.Label(new Rect(Screen.width - 250, 20, 230, 26), "Or : " + coins + "     Cailloux : " + rocks, labelStyle);
        GUI.Label(new Rect(Screen.width - 290, 55, 270, 24), "Rats " + ratsKilled + "/10   Araignées " + spidersKilled + "/5", smallStyle);
        GUI.Label(new Rect(20, Screen.height - 52, 560, 30), "ZQSD / WASD déplacer   •   Maj courir   •   Espace sauter   •   Clic attaquer   •   E ramasser", smallStyle);
        GUI.Label(new Rect(20, Screen.height - 27, 560, 24), "V caméra   I inventaire   O options   Q quêtes   1-5 objets   R renaître", smallStyle);
        for (int i = 0; i < 5; i++)
        {
            Rect slot = new Rect(Screen.width * 0.5f - 135 + i * 55, Screen.height - 78, 48, 48);
            GUI.color = i == selectedSlot ? Color.yellow : Color.white;
            GUI.Box(slot, (i + 1).ToString(), boxStyle); GUI.color = Color.white;
        }
        if (questOpen)
        {
            GUI.Box(new Rect(Screen.width - 275, 100, 255, 120), "QUÊTE\nPROBLÈME DE RATS\n\nRats : " + ratsKilled + " / 10\nAraignées : " + spidersKilled + " / 5", boxStyle);
        }
        if (inventoryOpen)
        {
            GUI.Box(new Rect(Screen.width / 2 - 160, Screen.height / 2 - 100, 320, 200), "INVENTAIRE\n\nPotions de soin : " + potions[0] + "\nPotions de vitesse : " + potions[1] + "\nOr : " + coins + "\nCailloux : " + rocks, boxStyle);
        }
        if (optionsOpen)
        {
            GUI.Box(new Rect(Screen.width / 2 - 200, Screen.height / 2 - 130, 400, 260),
                "OPTIONS\n\nLuminosité : " + Mathf.RoundToInt(brightness * 100)
                + "\nContraste : " + Mathf.RoundToInt(contrast * 100)
                + "\nSensibilité souris : " + cameraSensitivity.ToString("0.0")
                + "\n(régler avec [ et ])\n\nFermer : O", boxStyle);
        }
        if (dead) GUI.Box(new Rect(Screen.width / 2 - 180, Screen.height / 2 - 55, 360, 110), "VOUS ÊTES MORT\n\nAppuyez sur R pour renaître", boxStyle);
        if (infoTimer > 0) GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height - 150, 300, 35), infoMessage, titleStyle);
    }

    private void EnsureStyles()
    {
        if (labelStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.78f, 0.1f) } };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.82f, 0.87f, 0.92f) } };
        boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
    }

    private void LoadOptions()
    {
        brightness = PlayerPrefs.GetFloat("brightness", 0.3f);
        contrast = PlayerPrefs.GetFloat("contrast", 1f);
        cameraSensitivity = PlayerPrefs.GetFloat("cameraSensitivity", 3f);
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.SetFloat("brightness", brightness);
        PlayerPrefs.SetFloat("contrast", contrast);
        PlayerPrefs.SetFloat("cameraSensitivity", cameraSensitivity);
        PlayerPrefs.Save();
    }

    private void CreateEffectsPlaceholder() { }
    private void UpdateEffects(float dt) { }
}
