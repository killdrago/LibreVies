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

    // ------------------------------------------------------------------
    // DEMARRAGE : journal et ecran de chargement
    // Le monde est construit entierement par code. Sans ces quelques lignes,
    // un demarrage lent ressemble exactement a un plantage (ecran noir) et un
    // vrai probleme ne laisse aucune trace. Desormais le journal (Player.log)
    // dit ou le demarrage en est, et l'ecran affiche l'etape en cours : le
    // dernier "debut" sans "fin" dans le journal designe le coupable exact.
    // ------------------------------------------------------------------
    private readonly System.Diagnostics.Stopwatch chrono = new System.Diagnostics.Stopwatch();
    private bool mondePret;
    private string etapeChargement = "Preparation...";
    private float avancement;
    private int objetsCrees;
    private int imagesAffichees;
    private float prochainBattement = 5f;
    private GUIStyle loadingTitleStyle;
    private GUIStyle loadingTextStyle;

    // Trace de la route (memes points que le ruban visible) : sert aux
    // collisions de la cloture (on passe par les portails) et au pave.
    private static readonly Vector2[] RoutePoints =
    {
        new Vector2(0, 30), new Vector2(3, 18), new Vector2(-2, 6), new Vector2(1, -8),
        new Vector2(4, -20), new Vector2(-1, -34), new Vector2(1, -48), new Vector2(0, -67)
    };

    private readonly List<Obstacle> obstacles = new List<Obstacle>();
    // Rectangles des batiments : le decor (arbres, lampadaires, caisses...)
    // ne doit jamais etre pose dans un mur.
    private readonly List<Batiment> batiments = new List<Batiment>();
    // Portails du village (position du portail) et gardes qui les surveillent.
    private readonly List<Vector2> portails = new List<Vector2>();
    private readonly List<GardeState> gardes = new List<GardeState>();
    // Degats flottants et etincelles d'impact (listes d'effets temporaires).
    private readonly List<EffetTexte> floaters = new List<EffetTexte>();
    private readonly List<EffetEtincelle> etincelles = new List<EffetEtincelle>();
    private Font policeDefaut;
    private bool policeCherchee;
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

    private sealed class EffetTexte
    {
        public GameObject Root;
        public float Age;
    }

    private sealed class EffetEtincelle
    {
        public GameObject Root;
        public float Age;
    }

    private sealed class GardeState
    {
        public GameObject Root;
        public Transform JambeG;
        public Transform JambeD;
        public Obstacle Corps;
        public Vector2 Poste;      // la ou il revient quand tout est calme
        public Vector2 Portail;    // la porte qu'il surveille
        public float Recharge;
        public float Phase;
    }

    private sealed class Batiment
    {
        public float X;
        public float Z;
        public float Largeur;
        public float Profondeur;
    }

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

    private void Journal(string message)
    {
        Debug.Log("[LV] " + chrono.Elapsed.TotalSeconds.ToString("0.00")
                  + " s  " + message);
    }

    private void JournalMachine()
    {
        // Ces lignes evitent de deviner la machine du joueur en cas de probleme.
        Journal("LibreVies " + Application.version + " / Unity " + Application.unityVersion);
        Journal("systeme " + SystemInfo.operatingSystem + " | " + SystemInfo.processorCount
                + " coeurs | RAM " + SystemInfo.systemMemorySize + " Mo");
        Journal("carte " + SystemInfo.graphicsDeviceName + " | API " + SystemInfo.graphicsDeviceType
                + " | VRAM " + SystemInfo.graphicsMemorySize + " Mo");
        Journal("ecran " + Screen.currentResolution.width + "x" + Screen.currentResolution.height
                + " | fenetre " + Screen.width + "x" + Screen.height
                + " | vsync " + QualitySettings.vSyncCount);
    }

    private void Awake()
    {
        chrono.Start();
        Journal("demarrage");
        JournalMachine();
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
        obstacles.Clear();
        batiments.Clear();
        portails.Clear();
        gardes.Clear();
        floaters.Clear();
        etincelles.Clear();
        Journal("tout est pret : construction du monde");
        StartCoroutine(ConstruireMonde());
    }

    // Le monde se construit une etape par image : l'ecran de chargement reste
    // vivant et chaque etape laisse une trace dans le journal. Une etape qui
    // echoue (exception) est signalee mais n'empeche plus le jeu de demarrer.
    private System.Collections.IEnumerator ConstruireMonde()
    {
        string[] noms =
        {
            "materiaux", "environnement", "terrain", "route", "village", "chateau",
            "cloture et portails", "arbres", "herbe", "rochers",
            "decor (barils, caisses)", "lampadaires", "nuages", "heros",
            "monstres", "gardes", "objets a ramasser"
        };
        System.Action[] travaux =
        {
            CreateMaterials, CreateEnvironment, CreateTerrain, CreateRoad, CreateTown,
            CreateCastle, CreateFence, CreateTreesAndProps, CreateGrassTufts, CreateRocks,
            CreateProps, CreateLampposts, CreateClouds, CreatePlayer, CreateEnemies,
            CreateGuards, CreatePickups
        };
        for (int i = 0; i < noms.Length; i++)
        {
            etapeChargement = noms[i];
            avancement = i / (float)noms.Length;
            Journal("debut : " + noms[i]);
            try
            {
                travaux[i]();
            }
            catch (System.Exception erreur)
            {
                Debug.LogError("[LV] ECHEC de l'etape " + noms[i] + " : " + erreur.Message);
                Debug.LogException(erreur);
            }
            Journal("fin   : " + noms[i] + "  (" + objetsCrees + " objets)");
            yield return null;
        }
        ShowInfo("LibreVies — monde Unity prêt");
        Journal("demarrage termine : " + objetsCrees + " objets, " + obstacles.Count
                + " obstacles, " + enemies.Count + " monstres, " + gardes.Count + " gardes");
        avancement = 1f;
        etapeChargement = "";
        mondePret = true;
    }

    private void Update()
    {
        imagesAffichees++;
        if (!mondePret)
        {
            if (imagesAffichees == 1)
                Journal("premiere image affichee : le rendu fonctionne");
            // Battement de coeur : s'il s'arrete net, c'est la que ca bloque.
            if (Time.realtimeSinceStartup >= prochainBattement)
            {
                prochainBattement = Time.realtimeSinceStartup + 5f;
                Journal("construction en cours : " + etapeChargement);
            }
            return;
        }
        if (Time.realtimeSinceStartup >= prochainBattement)
        {
            prochainBattement = Time.realtimeSinceStartup + 60f;
            Journal("jeu en cours : image " + imagesAffichees + ", "
                    + Mathf.RoundToInt(1f / Mathf.Max(Time.deltaTime, 0.0001f)) + " i/s");
        }
        float dt = Mathf.Min(Time.deltaTime, 0.05f);
        UpdateClouds(dt);
        UpdatePickups(dt);
        UpdateEnemies(dt);
        UpdateGuards(dt);
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
        // --- Decor (portage de la reference) ---
        MakeMaterial("Bois_Clair", new Color(0.58f, 0.41f, 0.21f));
        MakeMaterial("Metal", new Color(0.30f, 0.28f, 0.28f));
        MakeMaterial("Lanterne", new Color(1f, 0.80f, 0.30f), true);
        MakeMaterial("Sapin_Bas", new Color(0.13f, 0.42f, 0.16f));
        MakeMaterial("Sapin_Milieu", new Color(0.16f, 0.50f, 0.19f));
        MakeMaterial("Sapin_Haut", new Color(0.20f, 0.58f, 0.22f));
        MakeMaterial("Touffe1", new Color(0.28f, 0.55f, 0.16f));
        MakeMaterial("Touffe2", new Color(0.36f, 0.65f, 0.22f));
        MakeMaterial("Touffe3", new Color(0.45f, 0.72f, 0.26f));
        MakeMaterial("Roche1", new Color(0.55f, 0.55f, 0.56f));
        MakeMaterial("Roche2", new Color(0.44f, 0.44f, 0.46f));
        MakeMaterial("Banniere_Bleue", new Color(0.16f, 0.30f, 0.62f));
        MakeMaterial("Banniere_Rouge", new Color(0.55f, 0.16f, 0.16f));
        MakeMaterial("Embleme", new Color(0.95f, 0.85f, 0.35f));
        MakeMaterial("Arbre_Rond", new Color(0.22f, 0.58f, 0.20f));
        MakeMaterial("Cimier", new Color(0.75f, 0.15f, 0.15f));
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

    // Point de la route le plus proche d'une position : sert a poser les
    // lampadaires a distance fixe du bord de la route.
    private Vector2 PointRouteProche(Vector2 p)
    {
        Vector2 meilleur = RoutePoints[0];
        float distanceMin = 100000f;
        for (int i = 0; i < RoutePoints.Length - 1; i++)
        {
            Vector2 a = RoutePoints[i];
            Vector2 b = RoutePoints[i + 1];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
            Vector2 projete = a + ab * t;
            float d = (p - projete).magnitude;
            if (d < distanceMin)
            {
                distanceMin = d;
                meilleur = projete;
            }
        }
        return meilleur;
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
        objetsCrees++;
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

    // ------------------------------------------------------------------
    // MAILLAGE PROCEDURAL
    // L'herbe en touffes et les rochers de la reference sont des maillages
    // faits a la main (MultiMesh cote reference, un seul maillage ici). Les
    // sapins ont besoin de cones : Unity n'a que des primitives simples, donc
    // on fabrique les triangles nous-memes. Aucun asset externe.
    // ------------------------------------------------------------------
    private sealed class Maillage
    {
        private readonly List<Vector3> sommets = new List<Vector3>();
        private readonly List<int> triangles = new List<int>();

        public void Triangle(Vector3 a, Vector3 b, Vector3 c)
        {
            int premier = sommets.Count;
            sommets.Add(a);
            sommets.Add(b);
            sommets.Add(c);
            triangles.Add(premier);
            triangles.Add(premier + 1);
            triangles.Add(premier + 2);
        }

        public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Triangle(a, b, c);
            Triangle(a, c, d);
        }

        // Cone (ou pyramide) : base circulaire + sommet, avec le dessous ferme.
        public void Cone(Vector3 baseCentre, float rayon, float hauteur, int segments)
        {
            Vector3 sommet = baseCentre + Vector3.up * hauteur;
            for (int i = 0; i < segments; i++)
            {
                float a0 = i * Mathf.PI * 2f / segments;
                float a1 = (i + 1) * Mathf.PI * 2f / segments;
                Vector3 p0 = baseCentre + new Vector3(Mathf.Cos(a0) * rayon, 0f, Mathf.Sin(a0) * rayon);
                Vector3 p1 = baseCentre + new Vector3(Mathf.Cos(a1) * rayon, 0f, Mathf.Sin(a1) * rayon);
                Triangle(p0, p1, sommet);
                Triangle(baseCentre, p1, p0);
            }
        }

        // Touffe d'herbe : 3 lames fines (comme make_touffe de la reference).
        public void Touffe(Vector3 pied, float echelle, float rotation, System.Random rng)
        {
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            float[] decalagesX = { 0f, 0.09f, -0.07f };
            float[] decalagesZ = { 0f, 0.05f, 0.08f };
            float[] hauteurs = { 0.42f, 0.32f, 0.28f };
            for (int k = 0; k < 3; k++)
            {
                float dx = decalagesX[k];
                float dz = decalagesZ[k];
                float ox = (dx * cos - dz * sin) * echelle;
                float oz = (dx * sin + dz * cos) * echelle;
                float angle = rotation + k;
                float pointeX = (float)(rng.NextDouble() * 0.08 - 0.04);
                float pointeZ = (float)(rng.NextDouble() * 0.08 - 0.04);
                Vector3 centre = pied + new Vector3(ox, 0f, oz);
                Vector3 pointe = centre + new Vector3(pointeX * echelle, hauteurs[k] * echelle, pointeZ * echelle);
                float r = 0.055f * echelle;
                Vector3 c0 = centre + new Vector3(Mathf.Cos(angle) * r, 0f, Mathf.Sin(angle) * r);
                Vector3 c1 = centre + new Vector3(-Mathf.Cos(angle) * r, 0f, -Mathf.Sin(angle) * r);
                Vector3 c2 = centre + new Vector3(-Mathf.Sin(angle) * r, 0f, Mathf.Cos(angle) * r);
                Triangle(c0, c1, pointe);
                Triangle(c1, c2, pointe);
                Triangle(c2, c0, pointe);
            }
        }

        // Rocher : cube de 8 sommets deplaces au hasard (hexaedre facette).
        public static Vector3[] SommetsRocher(System.Random rng)
        {
            var coins = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                var v = new Vector3(
                    (i & 1) == 0 ? -0.5f : 0.5f,
                    (i & 2) == 0 ? -0.5f : 0.5f,
                    (i & 4) == 0 ? -0.5f : 0.5f);
                v += new Vector3(
                    (float)(rng.NextDouble() * 0.32 - 0.16),
                    (float)(rng.NextDouble() * 0.32 - 0.16),
                    (float)(rng.NextDouble() * 0.32 - 0.16));
                coins[i] = v;
            }
            return coins;
        }

        public void Rocher(Vector3 pied, Vector3 echelle, float rotation, Vector3[] coins)
        {
            var tournes = new Vector3[8];
            float cos = Mathf.Cos(rotation);
            float sin = Mathf.Sin(rotation);
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = coins[i];
                float x = (c.x * cos - c.z * sin) * echelle.x;
                float z = (c.x * sin + c.z * cos) * echelle.z;
                tournes[i] = pied + new Vector3(x, c.y * echelle.y, z);
            }
            // 6 faces (2 triangles chacune)
            Quad(tournes[0], tournes[1], tournes[3], tournes[2]);
            Quad(tournes[4], tournes[6], tournes[7], tournes[5]);
            Quad(tournes[0], tournes[4], tournes[5], tournes[1]);
            Quad(tournes[2], tournes[3], tournes[7], tournes[6]);
            Quad(tournes[0], tournes[2], tournes[6], tournes[4]);
            Quad(tournes[1], tournes[5], tournes[7], tournes[3]);
        }

        public Mesh VersMesh(string nom)
        {
            var mesh = new Mesh { name = nom };
            if (sommets.Count > 65000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(sommets);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }

    private GameObject ObjetMaillage(string nom, Mesh mesh, string materiau)
    {
        objetsCrees++;
        var objet = new GameObject(nom);
        objet.AddComponent<MeshFilter>().sharedMesh = mesh;
        var rendu = objet.AddComponent<MeshRenderer>();
        var materiauTrouve = Mat(materiau);
        if (materiauTrouve != null) rendu.sharedMaterial = materiauTrouve;
        return objet;
    }

    // Un emplacement est libre s'il n'est ni dans un batiment, ni sur la route,
    // ni dans le chateau, ni dans la fontaine. Sert a poser le decor sans
    // qu'un arbre pousse dans un mur ou au milieu de la route.
    private bool EmplacementLibre(float x, float z, float marge = 1f)
    {
        for (int i = 0; i < batiments.Count; i++)
        {
            Batiment b = batiments[i];
            if (Mathf.Abs(x - b.X) < b.Largeur * 0.5f + marge
                && Mathf.Abs(z - b.Z) < b.Profondeur * 0.5f + marge)
                return false;
        }
        if (DistRoute(new Vector2(x, z)) < 3.0f + marge) return false;
        if (Mathf.Abs(z + 67f) < 20f && Mathf.Abs(x) < 26f) return false;   // chateau
        if (new Vector2(x - 9f, z - 15f).magnitude < 3.2f + marge) return false;  // fontaine
        return true;
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
        // Les gardes ne sont pas poses ici : ils sont crees par CreateGuards(),
        // juste devant les portails du village (voir CreateFence).
    }

    private void CreateBuilding(Vector3 position, Vector3 size, string name)
    {
        var root = new GameObject(name).transform;
        root.position = new Vector3(position.x, TerrainHeight(position.x, position.z), position.z);
        Box(Vector3.up * (size.y * 0.5f), size, "Wall", root, "Murs", true);
        Box(new Vector3(0, size.y + 0.15f, 0), new Vector3(size.x + 0.8f, 0.35f, size.z + 0.8f), "Roof", root, "Toit");
        // On ne traverse plus les maisons (0,5 m de marge comme la reference).
        ColBoite(position.x, position.z, size.x + 0.5f, size.z + 0.5f, size.y);
        batiments.Add(new Batiment
        {
            X = position.x, Z = position.z, Largeur = size.x, Profondeur = size.z
        });
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
            // Memorise : les gardes se postent a l'interieur de chaque portail.
            portails.Add(new Vector2(mx, mz));
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
        GameObject objet = CreerTexte3D("LIBREVIES", position, new Color(0.20f, 0.12f, 0.05f), 0.42f);
        if (objet == null) return;
        objet.name = "Texte_Portail";
        objet.transform.rotation = rotation;
        // Le texte regarde vers l'exterieur, comme le panneau.
        objet.transform.Rotate(0f, 180f, 0f, Space.Self);
    }

    // Un garde par portail, poste a l'interieur (comme la reference : 0,90 du
    // rayon du village). Il ne patrouille pas : il reste a son poste, regarde
    // vers l'exterieur, et va frapper tout monstre qui approche de SA porte.
    private void CreateGuards()
    {
        for (int i = 0; i < portails.Count; i++)
        {
            Vector2 portail = portails[i];
            float distance = portail.magnitude;
            if (distance < 0.001f) continue;
            Vector2 poste = portail.normalized * (VillageRadius * 0.90f);
            CreerGarde(poste, portail);
        }
    }

    private void CreerGarde(Vector2 poste, Vector2 portail)
    {
        float y = TerrainHeight(poste.x, poste.y);
        var root = new GameObject("Garde").transform;
        root.position = new Vector3(poste.x, y, poste.y);
        var state = new GardeState { Root = root.gameObject, Poste = poste, Portail = portail };

        // Jambes : chacune est un pivot anime (comme jg / jd de la reference).
        state.JambeG = CreerJambe(root, -0.12f);
        state.JambeD = CreerJambe(root, 0.12f);

        Box(new Vector3(0, 0.70f, 0), new Vector3(0.38f, 0.20f, 0.26f), "Stone", root, "Bassin");
        Box(new Vector3(0, 0.98f, 0), new Vector3(0.44f, 0.52f, 0.28f), "Stone", root, "Cuirasse");
        Box(new Vector3(0, 0.80f, 0), new Vector3(0.46f, 0.10f, 0.30f), "Dirt", root, "Ceinturon");
        Box(new Vector3(0, 1.02f, 0.15f), new Vector3(0.30f, 0.30f, 0.05f), "White", root, "Plastron");
        Box(new Vector3(0, 1.0f, 0), new Vector3(0.46f, 0.09f, 0.30f), "Cimier", root, "Baudrier", false, Quaternion.Euler(0f, 0f, 35f));
        Primitive(PrimitiveType.Sphere, new Vector3(-0.33f, 1.18f, 0), new Vector3(0.26f, 0.26f, 0.26f), "Stone", root, "SpalliereG");
        Primitive(PrimitiveType.Sphere, new Vector3(0.33f, 1.18f, 0), new Vector3(0.26f, 0.26f, 0.26f), "Stone", root, "SpalliereD");
        Primitive(PrimitiveType.Capsule, new Vector3(-0.31f, 0.96f, 0), new Vector3(0.15f, 0.20f, 0.15f), "Stone", root, "BrasG");
        Primitive(PrimitiveType.Capsule, new Vector3(0.31f, 0.96f, 0), new Vector3(0.15f, 0.20f, 0.15f), "Stone", root, "BrasD");
        Primitive(PrimitiveType.Sphere, new Vector3(-0.31f, 0.74f, 0), new Vector3(0.15f, 0.15f, 0.15f), "Skin", root, "MainG");
        Primitive(PrimitiveType.Sphere, new Vector3(0.31f, 0.74f, 0), new Vector3(0.15f, 0.15f, 0.15f), "Skin", root, "MainD");
        Box(new Vector3(0, 1.42f, 0), new Vector3(0.30f, 0.30f, 0.28f), "Skin", root, "Visage");
        Box(new Vector3(-0.07f, 1.45f, 0.145f), new Vector3(0.05f, 0.05f, 0.02f), "Metal", root, "OeilG");
        Box(new Vector3(0.07f, 1.45f, 0.145f), new Vector3(0.05f, 0.05f, 0.02f), "Metal", root, "OeilD");
        Box(new Vector3(0, 1.59f, 0), new Vector3(0.34f, 0.16f, 0.32f), "Stone", root, "Casque");
        Box(new Vector3(0, 1.49f, 0.16f), new Vector3(0.26f, 0.05f, 0.06f), "Metal", root, "Visiere");
        Box(new Vector3(0, 1.74f, 0), new Vector3(0.06f, 0.18f, 0.32f), "Cimier", root, "Cimier");

        // Hallebarde verticale, bien visible (manche clair, fer, croc, talon).
        var hallebarde = new GameObject("Hallebarde").transform;
        hallebarde.SetParent(root, false);
        hallebarde.localPosition = new Vector3(0.34f, 1.25f, 0.05f);
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 0, 0), new Vector3(0.08f, 1.25f, 0.08f), "Bois_Clair", hallebarde, "Manche");
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 0.55f, 0), new Vector3(0.11f, 0.025f, 0.11f), "Metal", hallebarde, "Bague");
        Box(new Vector3(0, 1.10f, 0), new Vector3(0.12f, 0.50f, 0.20f), "White", hallebarde, "Fer");
        CreateCone(hallebarde, new Vector3(0, 1.50f, 0), 0.10f, 0.32f, "White", "Pointe_Hallebarde");
        Box(new Vector3(-0.16f, 0.90f, 0), new Vector3(0.22f, 0.32f, 0.07f), "White", hallebarde, "Croc");
        Box(new Vector3(0, -1.20f, 0), new Vector3(0.10f, 0.14f, 0.10f), "Stone", hallebarde, "Talon");

        state.Corps = new Obstacle
        {
            Cercle = true, X = poste.x, Z = poste.y, Rayon = 0.4f, Portee = 1.4f, Hauteur = 1.8f
        };
        obstacles.Add(state.Corps);
        gardes.Add(state);
    }

    private Transform CreerJambe(Transform parent, float decalageX)
    {
        var jambe = new GameObject(decalageX < 0f ? "JambeG" : "JambeD").transform;
        jambe.SetParent(parent, false);
        jambe.localPosition = new Vector3(decalageX, 0.62f, 0f);
        Primitive(PrimitiveType.Capsule, new Vector3(0, -0.26f, 0), new Vector3(0.19f, 0.26f, 0.19f), "Stone", jambe, "Jambard");
        Box(new Vector3(0, -0.55f, -0.03f), new Vector3(0.17f, 0.16f, 0.26f), "Metal", jambe, "Soleret");
        return jambe;
    }

    // Les gardes : poste fixe, regard vers l'exterieur, et coup mortel sur tout
    // monstre qui s'approche a moins de 2,5 m de LEUR portail (la reference est
    // volontairement radicale : un garde tue un monstre en un seul coup).
    private void UpdateGuards(float dt)
    {
        for (int i = 0; i < gardes.Count; i++)
        {
            GardeState garde = gardes[i];
            garde.Recharge -= dt;
            garde.Phase += dt;
            var position = new Vector2(garde.Root.transform.position.x, garde.Root.transform.position.z);

            EnemyState cible = null;
            for (int e = 0; e < enemies.Count; e++)
            {
                EnemyState ennemi = enemies[e];
                if (!ennemi.Alive) continue;
                Vector3 ep = ennemi.Root.transform.position;
                if (new Vector2(ep.x - garde.Portail.x, ep.z - garde.Portail.y).magnitude < 2.5f)
                {
                    cible = ennemi;
                    break;
                }
            }

            bool marche = false;
            Vector2 regard = Vector2.zero;
            if (cible != null)
            {
                var ciblePosition = new Vector2(cible.Root.transform.position.x, cible.Root.transform.position.z);
                regard = ciblePosition - position;
                float distance = regard.magnitude;
                if (distance > 1.5f && distance > 0.001f)
                {
                    Vector2 pas = regard.normalized * Mathf.Min(2.6f * dt, distance - 1.4f);
                    position += pas;
                    marche = true;
                }
                if (distance < 2.0f && garde.Recharge <= 0f)
                {
                    garde.Recharge = 0.8f;
                    // Coup de grace : "-999" et etincelles, comme la reference.
                    cible.Hp = 0;
                    SpawnFloater(cible.Root.transform.position + Vector3.up * 1.3f, "-999",
                        new Color(1f, 0.85f, 0.30f));
                    SpawnSpark(cible.Root.transform.position + Vector3.up * 0.6f);
                    if (cible.Hp <= 0)
                    {
                        TuerEnnemiParGarde(cible);
                        ShowInfo("Un garde du village a repousse " + cible.Root.name + " !");
                    }
                }
            }
            else
            {
                Vector2 retour = garde.Poste - position;
                if (retour.magnitude > 0.15f)
                {
                    Vector2 pas = retour.normalized * Mathf.Min(2.0f * dt, retour.magnitude);
                    position += pas;
                    marche = true;
                    regard = retour;
                }
                else if (garde.Portail.magnitude > 0.01f)
                {
                    regard = garde.Portail.normalized;
                }
                else regard = Vector2.up;
            }

            float sol = TerrainHeight(position.x, position.y);
            garde.Root.transform.position = new Vector3(position.x, sol, position.y);
            if (garde.Corps != null)
            {
                garde.Corps.X = position.x;
                garde.Corps.Z = position.y;
            }
            if (regard.sqrMagnitude > 0.0001f)
                garde.Root.transform.rotation = Quaternion.LookRotation(new Vector3(regard.x, 0f, regard.y));
            float balancement = marche ? Mathf.Sin(garde.Phase * 8f) * 0.45f : 0f;
            if (garde.JambeG != null) garde.JambeG.localRotation = Quaternion.Euler(balancement * Mathf.Rad2Deg, 0f, 0f);
            if (garde.JambeD != null) garde.JambeD.localRotation = Quaternion.Euler(-balancement * Mathf.Rad2Deg, 0f, 0f);
        }
    }

    private void TuerEnnemiParGarde(EnemyState ennemi)
    {
        ennemi.Alive = false;
        ennemi.RespawnAt = Time.time + 10f;
        ennemi.Root.SetActive(false);
        // Remarque : la reference ne compte pas ce monstre pour la quete et ne
        // donne pas d'experience au heros (c'est le garde qui l'a tue).
    }

    private void CreateTreesAndProps()
    {
        // Sapins disperses (meme esprit que la reference, avec ses regles :
        // jamais sur la route, jamais dans un batiment, jamais dans le village).
        var random = new System.Random(4217);
        int plantes = 0;
        for (int i = 0; i < 220 && plantes < 80; i++)
        {
            float x = (float)(random.NextDouble() * 170 - 85);
            float z = (float)(random.NextDouble() * 170 - 85);
            if (new Vector2(x, z).magnitude < VillageRadius + 4) continue;
            if (!EmplacementLibre(x, z, 1.5f)) continue;
            CreatePine(x, z, (float)(random.NextDouble() * 0.7 + 0.8));
            plantes++;
        }
        // Quelques arbres ronds aux positions de la reference.
        foreach (float[] p in new[]
        {
            new[] { -14f, 7f }, new[] { 14f, 7f }, new[] { -24f, -8f },
            new[] { 24f, 8f }, new[] { -30f, 30f }, new[] { 30f, -30f }
        })
        {
            if (EmplacementLibre(p[0], p[1], 1.5f)) CreateRoundTree(p[0], p[1]);
        }
    }

    // Sapin de la reference : tronc epais bien visible, 4 renforts et 3 cones
    // de feuillage superposes (verts de plus en plus clairs vers le haut).
    private void CreatePine(float x, float z, float taille)
    {
        float y = TerrainHeight(x, z);
        var root = new GameObject("Sapin").transform;
        root.position = new Vector3(x, y, z);
        root.localScale = Vector3.one * taille;
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 1.3f, 0), new Vector3(0.68f, 1.3f, 0.68f), "Wood", root, "Tronc");
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 0.25f, 0), new Vector3(1.1f, 0.25f, 1.1f), "Wood", root, "Souche");
        for (int k = 0; k < 4; k++)
        {
            float a = k * Mathf.PI * 0.5f;
            Box(new Vector3(Mathf.Cos(a) * 0.30f, 1.3f, Mathf.Sin(a) * 0.30f), new Vector3(0.10f, 2.4f, 0.10f),
                "Wood", root, "Renfort", false, Quaternion.Euler(0f, -a * Mathf.Rad2Deg, 0f));
        }
        CreateCone(root, new Vector3(0f, 3.3f, 0f), 1.35f, 2.4f, "Sapin_Bas", "Feuillage_Bas");
        CreateCone(root, new Vector3(0f, 4.4f, 0f), 1.05f, 2.1f, "Sapin_Milieu", "Feuillage_Milieu");
        CreateCone(root, new Vector3(0f, 5.4f, 0f), 0.72f, 1.8f, "Sapin_Haut", "Feuillage_Haut");
        ColCercle(x, z, 0.4f * taille, 2.2f);   // tronc
    }

    // Arbre rond : tronc + deux masses de feuillage facettees.
    private void CreateRoundTree(float x, float z)
    {
        float y = TerrainHeight(x, z);
        var root = new GameObject("Arbre_Rond").transform;
        root.position = new Vector3(x, y, z);
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 1.8f, 0), new Vector3(0.72f, 1.8f, 0.72f), "Wood", root, "Tronc");
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 0.3f, 0), new Vector3(1.2f, 0.3f, 1.2f), "Wood", root, "Souche");
        Primitive(PrimitiveType.Sphere, new Vector3(0, 4.2f, 0), new Vector3(2.4f, 2.0f, 2.4f), "Arbre_Rond", root, "Feuillage");
        Primitive(PrimitiveType.Sphere, new Vector3(0.7f, 3.6f, 0.4f), new Vector3(1.4f, 1.2f, 1.4f), "Leaf", root, "Feuillage_Bas");
        ColCercle(x, z, 0.45f, 2.2f);
    }

    private void CreateCone(Transform parent, Vector3 position, float rayon, float hauteur, string materiau, string nom)
    {
        var maillage = new Maillage();
        maillage.Cone(Vector3.zero, rayon, hauteur, 7);
        var objet = ObjetMaillage(nom, maillage.VersMesh(nom), materiau);
        objet.transform.SetParent(parent, false);
        objet.transform.localPosition = position;
    }

    // Herbe en touffes : 3 nuances x 140 touffes, en UN seul maillage par
    // nuance (comme le MultiMesh de la reference) : aucun ralentissement.
    private void CreateGrassTufts()
    {
        string[] nuances = { "Touffe1", "Touffe2", "Touffe3" };
        var rng = new System.Random(7);
        for (int teinte = 0; teinte < nuances.Length; teinte++)
        {
            var maillage = new Maillage();
            for (int k = 0; k < 140; k++)
            {
                float x = (float)(rng.NextDouble() * (WorldSize * 2 - 8) - (WorldSize - 4));
                float z = (float)(rng.NextDouble() * (WorldSize * 2 - 8) - (WorldSize - 4));
                if (new Vector2(x, z).magnitude < TownRadius * 0.55f) x += TownRadius;
                x = Mathf.Clamp(x, -WorldSize + 4, WorldSize - 4);
                z = Mathf.Clamp(z, -WorldSize + 4, WorldSize - 4);
                int garde = 0;
                while (DistRoute(new Vector2(x, z)) < 3.2f && garde < 8)
                {
                    x = (float)(rng.NextDouble() * (WorldSize * 2 - 8) - (WorldSize - 4));
                    z = (float)(rng.NextDouble() * (WorldSize * 2 - 8) - (WorldSize - 4));
                    garde++;
                }
                maillage.Touffe(new Vector3(x, TerrainHeight(x, z) - 0.02f, z),
                    (float)(rng.NextDouble() * 0.8 + 0.8),
                    (float)(rng.NextDouble() * Mathf.PI * 2f), rng);
            }
            ObjetMaillage("Herbe_" + (teinte + 1), maillage.VersMesh("Touffes"),
                nuances[teinte]);
        }
    }

    // Rochers facettes : 2 nuances x 45, poses comme dans la reference (jamais
    // sur la route, jamais sur la cloture du village) et bloquants.
    private void CreateRocks()
    {
        string[] nuances = { "Roche1", "Roche2" };
        var rng = new System.Random(99);
        var rngForme = new System.Random(5);
        Vector3[] forme = Maillage.SommetsRocher(rngForme);
        for (int teinte = 0; teinte < nuances.Length; teinte++)
        {
            var maillage = new Maillage();
            for (int k = 0; k < 45; k++)
            {
                float x = (float)(rng.NextDouble() * (WorldSize * 2 - 10) - (WorldSize - 5));
                float z = (float)(rng.NextDouble() * (WorldSize * 2 - 10) - (WorldSize - 5));
                if (new Vector2(x, z).magnitude < TownRadius * 0.6f) x += TownRadius;
                x = Mathf.Clamp(x, -WorldSize + 5, WorldSize - 5);
                z = Mathf.Clamp(z, -WorldSize + 5, WorldSize - 5);
                var radial = new Vector2(x, z);
                if (radial.magnitude > 0.001f && Mathf.Abs(radial.magnitude - VillageRadius) < 2.2f)
                {
                    radial = radial.normalized * (VillageRadius + 2.6f);
                    x = radial.x;
                    z = radial.y;
                }
                int garde = 0;
                while (DistRoute(new Vector2(x, z)) < 4.0f && garde < 8)
                {
                    x = (float)(rng.NextDouble() * (WorldSize * 2 - 10) - (WorldSize - 5));
                    z = (float)(rng.NextDouble() * (WorldSize * 2 - 10) - (WorldSize - 5));
                    garde++;
                }
                float taille = (float)(rng.NextDouble() * 1.1 + 0.4);
                var echelle = new Vector3(taille * (float)(rng.NextDouble() * 0.5 + 0.8), taille * 0.75f, taille);
                maillage.Rocher(new Vector3(x, TerrainHeight(x, z) + taille * 0.18f, z), echelle,
                    (float)(rng.NextDouble() * Mathf.PI * 2f), forme);
                if (DistRoute(new Vector2(x, z)) > 2.8f)
                    ColCercle(x, z, 0.42f * taille, 0.45f * taille + 0.2f);
            }
            ObjetMaillage("Rochers_" + (teinte + 1), maillage.VersMesh("Rochers"), nuances[teinte]);
        }
    }

    // ------------------------------------------------------------------
    // DECOR DU VILLAGE (barils, caisses, cloutures, bannieres, panneaux)
    // Positions reprises de la reference. Chaque emplacement est verifie
    // (EmplacementLibre) : si un batiment ou la route est la, l'objet est
    // simplement ignore plutot que plante dans un mur.
    // ------------------------------------------------------------------
    private void CreateProps()
    {
        // Barils pres de l'auberge et de la forge.
        float[][] barils =
        {
            new[] { 8.2f, 5.6f }, new[] { 8.7f, 6.2f }, new[] { -7.0f, -1.4f },
            new[] { -7.7f, -1.9f }, new[] { 12.4f, 1.0f }
        };
        foreach (float[] p in barils)
        {
            if (!EmplacementLibre(p[0], p[1], 0.6f)) continue;
            float y = TerrainHeight(p[0], p[1]);
            Primitive(PrimitiveType.Cylinder, new Vector3(p[0], y + 0.45f, p[1]),
                new Vector3(0.68f, 0.45f, 0.68f), "Wood", null, "Baril");
            Primitive(PrimitiveType.Cylinder, new Vector3(p[0], y + 0.62f, p[1]),
                new Vector3(0.72f, 0.05f, 0.72f), "Metal", null, "Couvercle_Baril");
            ColCercle(p[0], p[1], 0.45f, 0.95f);
        }

        // Caisses : positions de la reference + deux de plus, libres.
        float[][] caisses =
        {
            new[] { -14.2f, -6.0f }, new[] { 12.6f, 4.6f }, new[] { -12.4f, 4.8f },
            new[] { 10.6f, -2.2f }, new[] { -10.5f, -4.5f }
        };
        foreach (float[] p in caisses)
        {
            if (!EmplacementLibre(p[0], p[1], 0.6f)) continue;
            CreateCrate(p[0], p[1], 0.7f);
        }

        // Bannieres (bleue et rouge) et panneaux indicateurs.
        CreateBanner(-5.5f, -3.0f, "Banniere_Bleue");
        CreateBanner(6.0f, 8.0f, "Banniere_Rouge");
        CreateSignpost(-5.2f, 9.8f, 0.6f);
        CreateSignpost(5.0f, -8.5f, -1.1f);

        CreateRoadFence();
    }

    private void CreateCrate(float x, float z, float taille)
    {
        float y = TerrainHeight(x, z);
        Box(new Vector3(x, y + taille * 0.5f, z), new Vector3(taille, taille, taille), "Wood", null, "Caisse");
        Box(new Vector3(x, y + taille * 0.62f, z), new Vector3(taille * 1.03f, taille * 0.17f, taille * 1.03f),
            "Bois_Clair", null, "Couvercle_Caisse");
        // Moins d'un metre de haut : on saute dessus (saut de 1,60 m).
        ColBoite(x, z, taille + 0.1f, taille + 0.1f, taille * 0.75f);
    }

    private void CreateRoadFence()
    {
        // Petite clouture de bois le long de la route dans le village :
        // positions de la reference (aucun poteau devant le passage : les
        // indices -1, 0 et 1 sont sautes).
        var libre = new List<bool>();
        var positions = new List<float>();
        for (int i = -6; i <= 6; i++)
        {
            float x = i * 2.4f;
            bool place = Mathf.Abs(i) >= 2 && EmplacementLibre(x, 4.6f, 0.2f);
            libre.Add(place);
            positions.Add(x);
            if (!place) continue;
            float y = TerrainHeight(x, 4.6f);
            Box(new Vector3(x, y + 0.45f, 4.6f), new Vector3(0.1f, 0.9f, 0.1f), "Wood", null, "Poteau_Cloture");
            Box(new Vector3(x, y + 0.7f, 4.6f), new Vector3(2.4f, 0.09f, 0.07f), "Bois_Clair", null, "Traverse_Cloture");
        }
        // Collisions : un bloc par suite de poteaux consecutifs.
        int debut = -1;
        for (int k = 0; k <= libre.Count; k++)
        {
            bool place = k < libre.Count && libre[k];
            if (place && debut < 0) debut = k;
            if (!place && debut >= 0)
            {
                float premier = positions[debut];
                float dernier = positions[k - 1];
                ColBoite((premier + dernier) * 0.5f, 4.6f, dernier - premier + 1.2f, 0.25f, 0.85f);
                debut = -1;
            }
        }
    }

    private void CreateBanner(float x, float z, string tissu)
    {
        if (!EmplacementLibre(x, z, 0.6f)) return;
        float y = TerrainHeight(x, z);
        var root = new GameObject("Banniere").transform;
        root.position = new Vector3(x, y, z);
        Box(new Vector3(0, 1.6f, 0), new Vector3(0.16f, 3.2f, 0.16f), "Dirt", root, "Mat");
        Box(new Vector3(0, 3.15f, 0), new Vector3(1.5f, 0.14f, 0.14f), "Dirt", root, "Traverse");
        Box(new Vector3(0, 2.35f, 0.02f), new Vector3(1.1f, 1.5f, 0.06f), tissu, root, "Tissu");
        Box(new Vector3(0, 2.4f, 0.08f), new Vector3(0.7f, 0.12f, 0.05f), "Embleme", root, "Epee1", false, Quaternion.Euler(0f, 0f, 45f));
        Box(new Vector3(0, 2.4f, 0.08f), new Vector3(0.7f, 0.12f, 0.05f), "Embleme", root, "Epee2", false, Quaternion.Euler(0f, 0f, -45f));
        ColCercle(x, z, 0.22f, 3.2f);
    }

    private void CreateSignpost(float x, float z, float rotation)
    {
        if (!EmplacementLibre(x, z, 0.6f)) return;
        float y = TerrainHeight(x, z);
        var root = new GameObject("Panneau").transform;
        root.position = new Vector3(x, y, z);
        root.rotation = Quaternion.Euler(0f, rotation * Mathf.Rad2Deg, 0f);
        Box(new Vector3(0, 0.9f, 0), new Vector3(0.12f, 1.8f, 0.12f), "Wood", root, "Pied");
        Box(new Vector3(0, 1.7f, 0), new Vector3(1.3f, 0.5f, 0.08f), "Bois_Clair", root, "Panneau");
        Box(new Vector3(0.2f, 1.35f, 0), new Vector3(1.0f, 0.4f, 0.08f), "Dirt", root, "Planche", false, Quaternion.Euler(0f, 0f, 20f));
        ColCercle(x, z, 0.18f, 2.0f);
    }

    private void CreateLampposts()
    {
        // Lampadaires le long de la route, en alternance d'un cote puis de
        // l'autre, tous les 13 m, uniquement dans le village : exactement la
        // regle de la reference (poser_lampadaires_route).
        float parcouru = 0f;
        float prochain = 6f;
        float cote = 1f;
        for (int i = 0; i < RoutePoints.Length - 1; i++)
        {
            Vector2 p = RoutePoints[i];
            Vector2 suivant = RoutePoints[i + 1];
            parcouru += Vector2.Distance(p, suivant);
            if (p.magnitude > 24f) continue;              // hors du village
            if (parcouru < prochain) continue;
            // 13 m dans la reference ; 9 m ici car notre trace de route a peu
            // de sommets : cela donne 3 lampadaires bien repartis dans le
            // village (sinon un seul).
            prochain = parcouru + 9f;
            cote = -cote;                                 // un coup a gauche, un coup a droite
            var normale = new Vector2(-(suivant - p).normalized.y, (suivant - p).normalized.x);
            bool pose = false;
            var position = Vector2.zero;
            // On essaie d'abord le cote voulu, puis l'autre : assez de
            // lampadaires pour eclairer le village sans en planter dans un mur.
            for (int essai = 0; essai < 2 && !pose; essai++)
            {
                float facteur = cote * (essai == 0 ? 1f : -1f);
                var candidat = new Vector2(p.x + normale.x * 3.4f * facteur, p.y + normale.y * 3.4f * facteur);
                // On repart du point de route le plus proche : un sommet de la
                // ligne droite donnerait un ecart faux.
                Vector2 bord = PointRouteProche(candidat);
                Vector2 ecart = candidat - bord;
                Vector2 essaiPose = ecart.sqrMagnitude > 0.04f ? bord + ecart.normalized * 3.4f : candidat;
                if (!EmplacementLibre(essaiPose.x, essaiPose.y, 0.3f)) continue;
                position = essaiPose;
                pose = true;
            }
            if (!pose) continue;
            CreateLamppost(position.x, position.y, PointRouteProche(position));
        }
    }

    private void CreateLamppost(float x, float z, Vector2 pointRoute)
    {
        float y = TerrainHeight(x, z);
        var root = new GameObject("Lampadaire").transform;
        root.position = new Vector3(x, y, z);
        // Le bras doit regarder la route : on oriente le lampadaire vers elle.
        Vector3 versRoute = new Vector3(pointRoute.x - x, 0f, pointRoute.y - z);
        if (versRoute.sqrMagnitude > 0.01f)
            root.rotation = Quaternion.LookRotation(versRoute.normalized);
        Box(new Vector3(0, 0.05f, 0), new Vector3(0.5f, 0.12f, 0.5f), "Metal", root, "Socle");
        Box(new Vector3(0, 1.5f, 0), new Vector3(0.14f, 3.0f, 0.14f), "Metal", root, "Mat");
        Box(new Vector3(0, 3.05f, 0), new Vector3(0.5f, 0.1f, 0.1f), "Metal", root, "Bras");
        Box(new Vector3(0, 2.75f, 0.22f), new Vector3(0.26f, 0.4f, 0.26f), "Lanterne", root, "Lanterne");
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 3.05f, 0.22f), new Vector3(0.42f, 0.1f, 0.42f), "Metal", root, "Chapeau");
        ColCercle(x, z, 0.25f, 3.0f);
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
            // Cadence 1,2 s / portee 1,6 m / 6 degats : les valeurs de la
            // reference (c'etait 0,8 s / 1,8 m / 8 degats).
            if (distance < 1.6f && enemy.AttackCooldown <= 0 && !dead
                && !ClotureEntre(position, player.position))
            {
                enemy.AttackCooldown = 1.2f;
                DamagePlayer(6);
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
            // Le "-25" qui s'envole et les etincelles de l'impact.
            SpawnFloater(enemy.Root.transform.position + Vector3.up * 1.3f, "-" + HammerDamage, Color.white);
            SpawnSpark(enemy.Root.transform.position + Vector3.up * 0.6f);
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
        // Le nombre de degats s'affiche au-dessus du heros (en rouge).
        SpawnFloater(player.position + Vector3.up * 1.8f, "-" + amount, new Color(1f, 0.30f, 0.20f));
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

    // Ecran de chargement : plus d'ecran noir pendant la construction du
    // monde. Il affiche l'etape en cours et l'avancement, et reste visible
    // jusqu'a ce que le jeu soit jouable.
    private void DessinerChargement()
    {
        GUI.color = new Color(0.06f, 0.08f, 0.14f);
        GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        float haut = Screen.height * 0.38f;
        GUI.Label(new Rect(0, haut, Screen.width, 56), "LIBREVIES", loadingTitleStyle);
        GUI.Label(new Rect(0, haut + 62, Screen.width, 30), "Chargement du monde...", loadingTextStyle);
        GUI.Label(new Rect(0, haut + 96, Screen.width, 26), etapeChargement, loadingTextStyle);
        int largeur = Mathf.Max(200, Mathf.RoundToInt(Screen.width * 0.5f));
        int x = (Screen.width - largeur) / 2;
        int y = Mathf.RoundToInt(haut) + 136;
        GUI.color = new Color(0.18f, 0.20f, 0.30f);
        GUI.DrawTexture(new Rect(x, y, largeur, 14), Texture2D.whiteTexture);
        GUI.color = new Color(1f, 0.78f, 0.10f);
        GUI.DrawTexture(new Rect(x, y, largeur * Mathf.Clamp01(avancement), 14), Texture2D.whiteTexture);
        GUI.color = Color.white;
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (!mondePret)
        {
            DessinerChargement();
            return;
        }
        GUI.Label(new Rect(20, 18, 380, 30), "LIBREVIES  •  MMO OPEN WORLD", titleStyle);
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
        loadingTitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 46, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(1f, 0.78f, 0.10f) }
        };
        loadingTextStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 18, alignment = TextAnchor.MiddleCenter,
            normal = { textColor = new Color(0.85f, 0.89f, 0.95f) }
        };
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

    // ------------------------------------------------------------------
    // DEGATS FLOTTANTS ET ETINCELLES
    // Le petit "-25" qui s'envole a chaque coup et les etincelles jaunes de
    // l'impact : sans eux, on ne sait pas si on touche. Ecrits en clair dans le
    // jeu (le HUD OnGUI ne peut pas suivre un point du monde en 3D).
    // ------------------------------------------------------------------
    private Font PoliceParDefaut()
    {
        if (policeCherchee) return policeDefaut;
        policeCherchee = true;
        try { policeDefaut = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); }
        catch (System.Exception) { policeDefaut = null; }
        if (policeDefaut == null)
        {
            try { policeDefaut = Resources.GetBuiltinResource<Font>("Arial.ttf"); }
            catch (System.Exception) { policeDefaut = null; }
        }
        return policeDefaut;
    }

    private GameObject CreerTexte3D(string texte, Vector3 position, Color couleur, float taille)
    {
        Font police = PoliceParDefaut();
        if (police == null) return null;
        var objet = new GameObject("Texte3D");
        objet.transform.position = position;
        var composant = objet.AddComponent<TextMesh>();
        composant.text = texte;
        composant.font = police;
        composant.fontSize = 64;
        composant.characterSize = taille;
        composant.anchor = TextAnchor.MiddleCenter;
        composant.color = couleur;
        var rendu = objet.GetComponent<MeshRenderer>();
        if (rendu != null) rendu.sharedMaterial = police.material;
        return objet;
    }

    private void SpawnFloater(Vector3 position, string texte, Color couleur)
    {
        GameObject objet = CreerTexte3D(texte, position, couleur, 0.25f);
        if (objet == null) return;
        floaters.Add(new EffetTexte { Root = objet, Age = 0f });
    }

    private void SpawnSpark(Vector3 position)
    {
        var root = new GameObject("Etincelles");
        root.transform.position = position;
        for (int i = 0; i < 3; i++)
        {
            var branche = new GameObject("Etincelle");
            branche.transform.SetParent(root.transform, false);
            Box(Vector3.zero, new Vector3(0.9f, 0.10f, 0.10f), "Lanterne", branche.transform, "Branche");
            branche.transform.localRotation = Quaternion.Euler(20f, i * 60f, 0f);
        }
        etincelles.Add(new EffetEtincelle { Root = root, Age = 0f });
    }

    private void UpdateEffects(float dt)
    {
        // Textes de degats : montent de 1,6 m/s et s'effacent en 1 s.
        for (int i = floaters.Count - 1; i >= 0; i--)
        {
            EffetTexte floater = floaters[i];
            if (floater.Root == null)
            {
                floaters.RemoveAt(i);
                continue;
            }
            floater.Age += dt;
            floater.Root.transform.position += Vector3.up * dt * 1.6f;
            float opacite = 1f - floater.Age;
            if (opacite <= 0f)
            {
                Destroy(floater.Root);
                floaters.RemoveAt(i);
                continue;
            }
            var composant = floater.Root.GetComponent<TextMesh>();
            if (composant != null)
            {
                Color couleur = composant.color;
                couleur.a = opacite;
                composant.color = couleur;
            }
        }

        // Etincelles : grossissent et clignotent pendant 0,25 s.
        for (int i = etincelles.Count - 1; i >= 0; i--)
        {
            EffetEtincelle effet = etincelles[i];
            if (effet.Root == null)
            {
                etincelles.RemoveAt(i);
                continue;
            }
            effet.Age += dt;
            float avancement = effet.Age / 0.25f;
            if (avancement >= 1f)
            {
                Destroy(effet.Root);
                etincelles.RemoveAt(i);
                continue;
            }
            effet.Root.transform.localScale = Vector3.one * (0.6f + avancement * 1.2f);
            effet.Root.SetActive(((int)(effet.Age * 30f)) % 2 == 0);
        }
    }
}
