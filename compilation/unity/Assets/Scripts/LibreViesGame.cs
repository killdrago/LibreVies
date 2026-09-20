using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
#if UNITY_STANDALONE_WIN
using System.Runtime.InteropServices;
#endif
using UnityEngine;

/// <summary>
/// Jeu LibreVies autonome pour Unity.
/// Le monde combine une base procédurale autonome et des modèles de nature
/// CC0 importés dans Resources ; les primitives restent le repli si un asset
/// manque. Le launcher n'a besoin que de l'exécutable
/// produit par Unity dans game/LibreViesGame.exe.
/// </summary>
public sealed class LibreViesGame : MonoBehaviour
{
    private const string VersionJeu = "0.5.71";
    private const float WorldSize = 125f;
    // Le village occupe maintenant un rayon de 40 m : assez large pour
    // respirer, sans revenir a la taille excessive de la MAJ 27.
    private const float TownRadius = 40f;
    private const float VillageRadius = 40f;
    private const float LargeurOuverturePortail = 7f;
    private const float RayonPoteauPortail = 0.15f;
    private const float DemiOuverturePortail = LargeurOuverturePortail * 0.5f + RayonPoteauPortail;
    private const float PlayerSpeed = 5f;
    private const float RunSpeed = 11f;
    private const float SwimSpeed = 3.6f;
    // Les monstres ne nagent pas : une marge de sécurité autour de la nappe
    // leur laisse le temps de tourner avant que leur corps ne touche l'eau.
    private const float MargeEauMonstres = 0.85f;
    // L'eau est une nappe qui suit le relief naturel : la surface reste au
    // niveau du sol naturel et son lit est creuse de cette profondeur.
    private const float WaterDepth = 3.20f;
    private const float WaterSurfaceOffset = 0.03f;
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
    private string cheminJournalRuntime;
    private bool mondePret;
    private string etapeChargement = "Preparation...";
    private float avancement;
    private int objetsCrees;
    private int imagesAffichees;
    private float prochainBattement = 5f;
    private GUIStyle loadingTitleStyle;
    private GUIStyle loadingTextStyle;

    // Trace de la route (memes points que le ruban visible) : sert aux
    // collisions de la cloture (on passe par les portails) et au pave. Les
    // deux extremites traversent l'enceinte au nord et au sud.
    private static readonly Vector2[] RoutePoints =
    {
        new Vector2(0, 44), new Vector2(4, 28), new Vector2(-2, 6),
        new Vector2(1, -8), new Vector2(1, -44)
    };

    // Le fleuve traverse le monde du nord-ouest vers le sud-est et reste hors
    // du village.
    private static readonly Vector2[] RivierePrincipale =
    {
        // Au nord du village, le cours reste droit et ne coupe jamais son
        // entree. Il ne descend qu'une fois suffisamment a l'est.
        new Vector2(-118, 72), new Vector2(-96, 64), new Vector2(-65, 61),
        new Vector2(-30, 60), new Vector2(8, 60), new Vector2(43, 60),
        new Vector2(64, 45), new Vector2(66, 10), new Vector2(55, -30),
        new Vector2(82, -68), new Vector2(118, -108)
    };
    private readonly List<Obstacle> obstacles = new List<Obstacle>();
    // Rectangles des batiments : le decor (arbres, lampadaires, caisses...)
    // ne doit jamais etre pose dans un mur.
    private readonly List<Batiment> batiments = new List<Batiment>();
    // Portails du village (position du portail) et gardes qui les surveillent.
    private readonly List<Vector2> portails = new List<Vector2>();
    private readonly List<GardeState> gardes = new List<GardeState>();
    private readonly List<PnjState> pnjs = new List<PnjState>();
    private readonly List<FacadeTextState> textesFacades = new List<FacadeTextState>();
    // Degats flottants et etincelles d'impact (listes d'effets temporaires).
    private readonly List<EffetTexte> floaters = new List<EffetTexte>();
    private readonly List<EffetEtincelle> etincelles = new List<EffetEtincelle>();
    private Font policeDefaut;
    private bool policeCherchee;
    private readonly List<EnemyState> enemies = new List<EnemyState>();
    private readonly List<PickupState> pickups = new List<PickupState>();
    private readonly List<GameObject> clouds = new List<GameObject>();
    private readonly List<Material> materials = new List<Material>();
    // Assets CC0 convertis en OBJ pour rester importables sans plugin Unity.
    // Le cache évite de relire Resources à chaque instance du décor.
    private readonly Dictionary<string, GameObject> naturePrefabs = new Dictionary<string, GameObject>();
    private int natureInstances;
    private readonly Dictionary<Renderer, Material[]> materiauxOriginaux = new Dictionary<Renderer, Material[]>();

    private Transform player;
    private Transform cameraPivot;
    private Transform heroBody;
    private Transform heroineModel;
    private Transform brasAttaque;
    private Transform brasHeroineGauche;
    private Transform brasHeroineDroit;
    private Transform coudeHeroineGauche;
    private Transform coudeHeroineDroit;
    private Transform jambeHeroineGauche;
    private Transform jambeHeroineDroite;
    private Transform genouHeroineGauche;
    private Transform genouHeroineDroit;
    private bool heroineRigPret;
    private CapsuleCollider joueurCollider;
    private string heroineAnimationActuelle = "Idle";
    private Camera gameCamera;
    private float cameraDistance = 6.5f;
    private float cameraPitch = 18f;
    private float firstPersonPitch;
    private float cameraYaw;
    // Sensibilite de la souris, reglable comme dans les options du jeu.
    // lisse GetAxis("Mouse X") : on utilise GetAxisRaw pour une reponse
    // immediate, sinon la camera parait "longue a la detente".
    private float cameraSensitivity = 3f;
    private Shader cachedShader;
    private Shader routeShader;
    private bool routePbrActif;
    private bool firstPerson;
    private bool cameraDragging;
    private Vector3 playerVelocity;
    private bool playerGrounded = true;
    private float walkClock;
    private float attackCooldown;
    private float attackAnimation;
    private float regenClock;
    private float playerProtection;
    private float invincibility;
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
    private bool notationOpen;
    private bool conversationOpen;
    private PnjState conversationPnj;
    private bool conversationFocusRequested;
    private string conversationInput = "";
    private readonly List<string> conversationMessages = new List<string>();
    private string infoMessage = "";
    private float infoTimer;
    private float brightness = 0.3f;
    private float contrast = 1f;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle smallStyle;
    private GUIStyle boxStyle;
    private GUIStyle tabStyle;
    private GUIStyle tabActifStyle;
    private GUIStyle buttonStyle;
    private GUIStyle miniCarteTextStyle;
    private GUIStyle miniCarteButtonStyle;
    private GUIStyle barreValeurStyle;
    private GUIStyle conversationTitleStyle;
    private GUIStyle conversationTextStyle;
    private Texture2D miniCarteTexture;
    private float miniCarteZoom = 1f;
    private float miniCarteOrientation;

    // Options persistantes : le fichier est lisible et portable avec la partie
    // jeu. PlayerPrefs reste accepte pour reprendre les anciennes installations.
    private bool clavierAzerty;
    private bool inverserAxeX;
    private bool inverserAxeY;
    private bool menuResolutions;
    private int ongletOptions;
    private string toucheEnCours = "";
    private int resolutionIndex;
    private float endurance = 100f;

    private int toucheAvant = (int)KeyCode.Z;
    private int toucheArriere = (int)KeyCode.S;
    private int toucheGauche = (int)KeyCode.Q;
    private int toucheDroite = (int)KeyCode.D;
    private int toucheSaut = (int)KeyCode.Space;
    private int toucheCourir = (int)KeyCode.LeftShift;
    private int toucheRamasser = (int)KeyCode.E;
    private int toucheAttaque = (int)KeyCode.Mouse0;
    private int toucheInventaire = (int)KeyCode.I;
    private int toucheOptions = (int)KeyCode.O;
    private int toucheQuete = (int)KeyCode.Q;
    private int toucheCamera = (int)KeyCode.V;
    private int toucheRenaître = (int)KeyCode.R;
    private bool playerInWater;
    private bool playerUnderwater;
    private bool playerWasUnderwater;
    private float underwaterDamageClock;

    private readonly Vector2Int[] resolutions =
    {
        new Vector2Int(1024, 768), new Vector2Int(1152, 864), new Vector2Int(1280, 720),
        new Vector2Int(1280, 768), new Vector2Int(1280, 800), new Vector2Int(1280, 854),
        new Vector2Int(1280, 960), new Vector2Int(1280, 1024), new Vector2Int(1366, 768),
        new Vector2Int(1400, 1050), new Vector2Int(1440, 900), new Vector2Int(1440, 960),
        new Vector2Int(1600, 900), new Vector2Int(1600, 1024), new Vector2Int(1600, 1200),
        new Vector2Int(1680, 1050)
    };

    [Serializable]
    private sealed class Configuration
    {
        public int version = 1;
        public float brightness = 0.30f;
        public float contrast = 1f;
        public float cameraSensitivity = 3f;
        public bool invertX;
        public bool invertY;
        public bool azerty;
        public int resolution = 2;
        public int avant = (int)KeyCode.Z;
        public int arriere = (int)KeyCode.S;
        public int gauche = (int)KeyCode.Q;
        public int droite = (int)KeyCode.D;
        public int saut = (int)KeyCode.Space;
        public int courir = (int)KeyCode.LeftShift;
        public int ramasser = (int)KeyCode.E;
        public int attaque = (int)KeyCode.Mouse0;
        public int inventaire = (int)KeyCode.I;
        public int options = (int)KeyCode.O;
        public int quete = (int)KeyCode.Q;
        public int camera = (int)KeyCode.V;
        public int renaitre = (int)KeyCode.R;
    }

    private string CheminConfiguration
    {
        get { return Path.Combine(Application.persistentDataPath, "librevies_config.json"); }
    }

    private sealed class EffetTexte
    {
        public GameObject Root;
        public float Age;
        public Vector3 Origine;     // depart du texte : sert a l'arc
    }

    private sealed class EffetEtincelle
    {
        public GameObject Root;
        public float Age;
    }

    private sealed class GardeState
    {
        public GameObject Root;
        public Transform Model;
        public Transform JambeG;
        public Transform JambeD;
        public Transform GenouG;
        public Transform GenouD;
        public Transform BrasG;
        public Transform BrasD;
        public Transform CoudeG;
        public Transform CoudeD;
        public Transform Hallebarde;
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

    private sealed class PnjState
    {
        public GameObject Root;
        public Transform Model;
        public Transform Corps;
        public Transform JambeG;
        public Transform JambeD;
        public Transform GenouG;
        public Transform GenouD;
        public Transform BrasG;
        public Transform BrasD;
        public Transform CoudeG;
        public Transform CoudeD;
        public Transform Main;
        public Transform Feuille;
        public Transform Marteau;
        public Transform Etal;
        public float Phase;
        public string Metier;
    }

    private sealed class FacadeTextState
    {
        public GameObject Root;
        public Vector3 Position;
        public Vector3 DirectionFacade;
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
        public int PvMax = 50;          // souris 25, rat 50, araignee 75
        public bool Souris;
        public Transform Barre;         // barre de vie (orientee camera)
        public Transform Remplissage;   // partie rouge de la barre
    }

    private sealed class PickupState
    {
        public GameObject Root;
        public bool Coin;
        public bool Active = true;
        public Vector3 Home;
        public float RespawnAt;
    }

    private void InitialiserJournalRuntime()
    {
        try
        {
            string dossierJeu = Path.GetDirectoryName(Application.dataPath);
            string dossierLogs = Path.Combine(dossierJeu ?? Application.dataPath, "logs");
            Directory.CreateDirectory(dossierLogs);
            cheminJournalRuntime = Path.Combine(dossierLogs, "LibreViesRuntime.log");
            File.WriteAllText(cheminJournalRuntime,
                "LibreVies runtime v" + VersionJeu + " - " + DateTime.Now.ToString("s")
                + Environment.NewLine);
            Application.logMessageReceived += EcrireLogRuntime;
        }
        catch (Exception erreur)
        {
            cheminJournalRuntime = null;
            Debug.LogWarning("[LV] journal runtime indisponible : " + erreur.Message);
        }
    }

    private void EcrireLogRuntime(string condition, string stackTrace, LogType type)
    {
        if (string.IsNullOrEmpty(cheminJournalRuntime)) return;
        try
        {
            string ligne = DateTime.Now.ToString("s") + " [" + type + "] " + condition;
            if (!string.IsNullOrEmpty(stackTrace)) ligne += Environment.NewLine + stackTrace;
            File.AppendAllText(cheminJournalRuntime, ligne + Environment.NewLine);
        }
        catch (Exception) { }
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

#if UNITY_STANDALONE_WIN
    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern System.IntPtr GetActiveWindow();

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool SetWindowText(System.IntPtr hWnd, string texte);
#endif

    private void AfficherVersionDansTitre()
    {
#if UNITY_STANDALONE_WIN
        System.IntPtr fenetre = GetActiveWindow();
        if (fenetre != System.IntPtr.Zero)
            SetWindowText(fenetre, "LibreVies - v" + VersionJeu);
#endif
    }

    private void Awake()
    {
        InitialiserJournalRuntime();
        chrono.Start();
        AfficherVersionDansTitre();
        Journal("journal runtime : " + (cheminJournalRuntime ?? "indisponible"));
        Journal("demarrage LibreVies v" + VersionJeu);
        JournalMachine();
        Application.targetFrameRate = 60;
        Application.runInBackground = true;
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

    private void OnDestroy()
    {
        Application.logMessageReceived -= EcrireLogRuntime;
    }

    // Le monde se construit une etape par image : l'ecran de chargement reste
    // vivant et chaque etape laisse une trace dans le journal. Une etape qui
    // echoue (exception) est signalee mais n'empeche plus le jeu de demarrer.
    private System.Collections.IEnumerator ConstruireMonde()
    {
        string[] noms =
        {
            "materiaux", "environnement", "terrain", "route", "village",
            "riviere", "cloture et portails", "arbres", "herbe", "rochers",
            "decor (barils, caisses)", "lampadaires", "nuages", "heros",
            "monstres", "gardes", "objets a ramasser"
        };
        System.Action[] travaux =
        {
            CreateMaterials, CreateEnvironment, CreateTerrain, CreateRoad, CreateTown,
            CreateWaterways, CreateFence, CreateTreesAndProps, CreateGrassTufts, CreateRocks,
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
        Journal("demarrage termine : " + objetsCrees + " objets, " + obstacles.Count
                + " obstacles, " + enemies.Count + " monstres, " + gardes.Count + " gardes");
        Renderer[] renderers = FindObjectsByType<Renderer>(FindObjectsSortMode.None);
        Journal("controle rendu : " + renderers.Length + " renderer(s), shader "
                + (cachedShader == null ? "AUCUN" : cachedShader.name));
        ControlerCouvertureShader(renderers);
        avancement = 1f;
        etapeChargement = "";
        mondePret = true;
    }

    private void ControlerCouvertureShader(Renderer[] renderers)
    {
        int geometries = 0;
        int stables = 0;
        int autres = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer rendu = renderers[i];
            if (rendu == null || rendu.GetComponent<TextMesh>() != null) continue;
            geometries++;
            Material[] mats = rendu.sharedMaterials;
            bool stable = mats != null && mats.Length > 0;
            if (stable)
            {
                for (int m = 0; m < mats.Length; m++)
                {
                    if (mats[m] == null || mats[m].shader == null
                        || mats[m].shader.name != "LibreVies/StablePBR")
                    {
                        stable = false;
                        break;
                    }
                }
            }
            if (stable) stables++; else autres++;
        }
        Journal("couverture shader monde : " + stables + "/" + geometries
                + " renderer(s) StablePBR, " + autres + " autre(s)"
                + " (textes 3D exclus)");
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
        UpdatePnj(dt);
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
        MakeMaterial("Carton", new Color(0.62f, 0.35f, 0.12f));
        MakeMaterial("Wall", new Color(0.70f, 0.61f, 0.47f));
        MakeMaterial("Roof", new Color(0.42f, 0.12f, 0.09f));
        MakeMaterial("RoofBlue", new Color(0.16f, 0.30f, 0.58f));
        MakeMaterial("RoofRed", new Color(0.55f, 0.13f, 0.10f));
        MakeMaterial("Leaf", new Color(0.08f, 0.31f, 0.10f));
        MakeMaterial("Player", new Color(0.12f, 0.28f, 0.52f));
        MakeMaterial("Skin", new Color(0.86f, 0.59f, 0.40f));
        MakeMaterial("Hair_Rouge", new Color(0.72f, 0.055f, 0.025f));
        MakeMaterial("Yeux_Heros", new Color(0.025f, 0.035f, 0.06f));
        MakeMaterial("Ceinture_Heros", new Color(0.22f, 0.10f, 0.045f));
        MakeMaterial("Enemy", new Color(0.28f, 0.18f, 0.12f));
        MakeMaterial("Spider", new Color(0.12f, 0.08f, 0.07f));
        MakeMaterial("Gold", new Color(1f, 0.65f, 0.08f), true);
        MakeMaterial("Water", new Color(0.08f, 0.45f, 0.85f), true);
        MakeMaterial("Glass", new Color(0.28f, 0.58f, 0.82f), true);
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
        MakeMaterial("Cimier", new Color(0.75f, 0.15f, 0.15f));
        // Textures dediees aux objets qui etaient encore trop plats : feuillage,
        // pierre, metal, drapeaux et eau partagent toujours StablePBR.
        MakeMaterial("Eau_Riviere", new Color(0.16f, 0.56f, 0.72f), true);
        MakeMaterial("Drapeau", Color.white);
        MakeMaterial("Pierre_Mur", new Color(0.46f, 0.48f, 0.50f));
        MakeMaterial("Feuillage", new Color(0.20f, 0.56f, 0.19f));
        // Barres de vie des monstres : fond sombre + partie rouge (reference).
        MakeMaterial("Vie_Fond", new Color(0.15f, 0.05f, 0.05f));
        MakeMaterial("Vie_Rouge", new Color(0.90f, 0.12f, 0.12f));
        // Le shader stable est maintenant commun a toutes les surfaces du
        // monde. Route_PBR garde son nom historique pour le diagnostic, mais
        // utilise exactement le meme shader que les murs, arbres et PNJ.
        MakeMaterial("Route_PBR", new Color(0.42f, 0.45f, 0.48f), false, true);
        MakeMaterial("Route_Terre", new Color(0.46f, 0.30f, 0.16f));
        MakeMaterial("Route_Bord", new Color(0.36f, 0.34f, 0.31f));
        MakeMaterial("Pave_Route", new Color(0.58f, 0.53f, 0.43f));
    }

    private Texture2D TextureRealiste(string nom)
    {
        string chemin = null;
        if (nom == "Wall") chemin = "LVTextures/LV_PlasterWall";
        else if (nom == "Roof" || nom == "RoofBlue" || nom == "RoofRed") chemin = "LVTextures/LV_TerracottaRoof";
        else if (nom == "Wood" || nom == "Bois_Clair") chemin = "LVTextures/LV_Wood";
        else if (nom == "Terrain" || nom == "Dirt" || nom.StartsWith("Touffe")) chemin = "LVTextures/LV_Ground";
        else if (nom == "Route_PBR" || nom == "Route_Bord" || nom == "Pave_Route") chemin = "LVTextures/LV_Stone";
        else if (nom == "Route_Terre") chemin = "LVTextures/LV_Ground";
        else if (nom == "Stone" || nom.StartsWith("Roche") || nom == "Pierre_Mur") chemin = "LVTextures/LV_Stone";
        else if (nom == "Metal" || nom == "Lanterne") chemin = "LVTextures/LV_Metal";
        else if (nom == "Leaf" || nom.StartsWith("Sapin") || nom == "Feuillage") chemin = "LVTextures/LV_Leaf";
        else if (nom == "Banniere_Bleue" || nom == "Banniere_Rouge" || nom == "Drapeau" || nom == "Player") chemin = "LVTextures/LV_Cloth";
        else if (nom == "Water" || nom == "Eau_Riviere" || nom == "Glass") chemin = "LVTextures/LV_Water";
        return chemin == null ? null : Resources.Load<Texture2D>(chemin);
    }

    private Material MakeMaterial(string name, Color color, bool emission = false, bool pbr = false)
    {
        // Toutes les geometries du monde utilisent StablePBR. Le parametre pbr
        // reste conserve pour identifier Route_PBR dans le journal, sans
        // retirer les autres familles du rendu texture.
        Shader shader = pbr ? ResoudreShaderRoute() : ResoudreShader();
        if (shader == null)
        {
            Debug.LogError("[LV_SHADER] aucun shader disponible pour " + name);
            materials.Add(null);
            return null;
        }
        var material = new Material(shader) { name = name };
        if (material.HasProperty("_Color")) material.color = color;
        Texture2D texture = TextureRealiste(name);
        if (texture != null && material.HasProperty("_MainTex")) material.SetTexture("_MainTex", texture);
        if (pbr)
        {
            if (texture == null)
                Debug.LogError("[LV_SHADER_ROUTE] texture absente pour Route_PBR : LVTextures/LV_ConcretePaving");
            else
                Debug.Log("[LV_SHADER_ROUTE] Route_PBR construit avec shader=" + shader.name
                    + " texture=LVTextures/LV_ConcretePaving pbr=" + routePbrActif);
        }
        if (material.HasProperty("_Tiling"))
        {
            float echelle = name == "Terrain" || name == "Dirt" || name == "Route_Terre" ? 0.08f
                : (name.Contains("Roof") ? 0.55f : (name == "Wall" ? 0.34f
                : (name == "Water" || name == "Eau_Riviere" ? 0.16f
                : (name == "Leaf" || name.StartsWith("Sapin") || name == "Feuillage" ? 0.22f
                : (name == "Metal" || name == "Lanterne" ? 0.30f
                : (name.Contains("Banniere") || name == "Drapeau" ? 0.42f : 0.28f))))));
            material.SetFloat("_Tiling", echelle);
        }
        if (material.HasProperty("_BumpStrength"))
        {
            float relief = name == "Terrain" || name == "Dirt" || name == "Route_Terre" ? 0.20f
                : (name.Contains("Roof") ? 0.16f : 0.11f);
            material.SetFloat("_BumpStrength", relief);
        }
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", name == "Metal" ? 0.82f : (name == "Gold" ? 0.72f : 0f));
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", name == "Metal" || name == "Gold" ? 0.76f
                : (name == "Water" || name == "Eau_Riviere" || name == "Glass" ? 0.90f : 0.32f));
        if (material.HasProperty("_DetailScale"))
            material.SetFloat("_DetailScale", name == "Terrain" ? 0.16f : (name.Contains("Roof") ? 1.8f : 0.75f));
        if (material.HasProperty("_DetailStrength"))
            material.SetFloat("_DetailStrength", name == "Terrain" ? 0.12f : (name.Contains("Roof") ? 0.10f : 0.055f));
        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.7f);
        }
        materials.Add(material);
        return material;
    }

    private Shader ChargerShader(string chemin)
    {
        Shader shader = Resources.Load<Shader>(chemin);
        if (shader == null)
        {
            Debug.LogError("[LV_SHADER] Resources.Load a echoue : " + chemin
                + " | GPU=" + SystemInfo.graphicsDeviceName
                + " | API=" + SystemInfo.graphicsDeviceType);
            return null;
        }
        if (!shader.isSupported)
        {
            Debug.LogError("[LV_SHADER] shader non supporte : " + shader.name
                + " (" + chemin + ") | GPU=" + SystemInfo.graphicsDeviceName
                + " | API=" + SystemInfo.graphicsDeviceType);
            return null;
        }
        Debug.Log("[LV_SHADER] shader charge : " + shader.name + " (" + chemin + ")");
        return shader;
    }

    private Shader ResoudreShader()
    {
        if (cachedShader != null) return cachedShader;

        // Le test route 0.5.41 est concluant : ce chemin vertex/fragment
        // stable devient maintenant le shader PBR de tout le monde. Il evite
        // le surface shader Standard qui noircissait la route sur AMD.
        cachedShader = ChargerShader("LVShaders/LVRouteStable");
        if (cachedShader == null) cachedShader = ChargerShader("LVShaders/LVColor");
        if (cachedShader == null) cachedShader = Shader.Find("Standard");
        if (cachedShader == null) cachedShader = Shader.Find("Unlit/Color");
        if (cachedShader == null) cachedShader = Shader.Find("Sprites/Default");
        if (cachedShader == null) cachedShader = Shader.Find("UI/Default");

        if (cachedShader != null) Debug.Log("[LV_SHADER] shader general applique = " + cachedShader.name);
        else Debug.LogError("LibreVies : aucun shader general disponible");
        return cachedShader;
    }

    private Shader ResoudreShaderRoute()
    {
        if (routeShader != null) return routeShader;
        // Route_PBR partage exactement le shader general : le meme rendu
        // stable est donc applique aux murs, toits, sol, vegetation, PNJ,
        // monstres, balustrades et route.
        routeShader = ResoudreShader();
        routePbrActif = routeShader != null && routeShader.name == "LibreVies/StablePBR";
        if (routeShader != null)
            Debug.Log("[LV_SHADER_ROUTE] meme shader global applique a la route : " + routeShader.name);
        else
            Debug.LogError("[LV_SHADER_ROUTE] aucun shader global disponible");
        return routeShader;
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
        RenderSettings.defaultReflectionMode = UnityEngine.Rendering.DefaultReflectionMode.Skybox;
        RenderSettings.reflectionIntensity = 0.7f;
        QualitySettings.shadowDistance = 100f;
        QualitySettings.shadowCascades = 4;
        QualitySettings.shadowResolution = ShadowResolution.High;
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

    private float TerrainSurfaceHeight(float x, float z)
    {
        float distance = new Vector2(x, z).magnitude;
        float townBlend = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(TownRadius, TownRadius + 26f, distance));
        float hills = Mathf.Sin(x * 0.09f) * Mathf.Cos(z * 0.07f) * 2.2f;
        hills += Mathf.Sin(x * 0.21f + 1.7f) * Mathf.Cos(z * 0.17f + 0.6f) * 0.9f;
        hills += Mathf.Sin((x + z) * 0.05f) * 1.4f;
        float hauteur = hills * townBlend;

        return hauteur;
    }

    private float HauteurSurfaceEau(float x, float z)
    {
        // La surface est calculee sur le relief naturel, jamais sur le terrain
        // deja creuse. Elle suit donc chaque montee et chaque descente.
        return TerrainSurfaceHeight(x, z) + WaterSurfaceOffset;
    }

    private float TerrainHeight(float x, float z)
    {
        float hauteur = TerrainSurfaceHeight(x, z);
        float profondeur = 0f;

        // Le lit est creuse sous le relief naturel, au lieu d'etre force a une
        // altitude mondiale fixe. La nappe d'eau pourra ainsi suivre ce relief.
        if (!DansVillage(x, z))
        {
            float distanceEau = DistancePolyligne(new Vector2(x, z), RivierePrincipale);
            // Le creux revient a zero exactement sur la berge : le terrain
            // rejoint ainsi la surface de l'eau sans marche ni espace.
            float creuxRiviere = 1f - Mathf.SmoothStep(4.5f, 7.0f, distanceEau);
            profondeur = Mathf.Max(profondeur, WaterDepth * creuxRiviere);
        }

        return hauteur - profondeur;
    }

    private float DistancePolyligne(Vector2 point, Vector2[] ligne)
    {
        float meilleur = 100000f;
        for (int i = 0; i < ligne.Length - 1; i++)
        {
            Vector2 a = ligne[i];
            Vector2 b = ligne[i + 1];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
            meilleur = Mathf.Min(meilleur, Vector2.Distance(point, a + ab * t));
        }
        return meilleur;
    }

    private bool EstDansEau(Vector2 point)
    {
        // Le fleuve reste hors du village ; il n'y a plus de douves ni de
        // pont-levis a traiter.
        if (DansVillage(point.x, point.y)) return false;
        return DistancePolyligne(point, RivierePrincipale) <= 5.3f;
    }

    // Les monstres terrestres n'ont pas de nage : l'eau est une zone interdite
    // un peu plus large que le ruban visible. La marge permet de choisir une
    // direction tangentielle avant que la collision ne les bloque sur la berge.
    private bool ZoneEauInterditeMonstre(Vector2 point)
    {
        if (DansVillage(point.x, point.y)) return false;
        return DistancePolyligne(point, RivierePrincipale) <= 5.3f + MargeEauMonstres;
    }

    private Vector2 PointRiviereProche(Vector2 point, out Vector2 tangente)
    {
        float meilleureDistance = 100000f;
        Vector2 meilleurPoint = RivierePrincipale[0];
        tangente = Vector2.right;
        for (int i = 0; i < RivierePrincipale.Length - 1; i++)
        {
            Vector2 a = RivierePrincipale[i];
            Vector2 b = RivierePrincipale[i + 1];
            Vector2 ab = b - a;
            float longueur = Mathf.Max(ab.magnitude, 0.001f);
            Vector2 direction = ab / longueur;
            float t = Mathf.Clamp01(Vector2.Dot(point - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
            Vector2 projection = a + ab * t;
            float distance = (point - projection).sqrMagnitude;
            if (distance < meilleureDistance)
            {
                meilleureDistance = distance;
                meilleurPoint = projection;
                tangente = direction;
            }
        }
        return meilleurPoint;
    }

    private Vector2 SortirZoneEauMonstre(Vector2 point)
    {
        Vector2 tangente;
        Vector2 bord = PointRiviereProche(point, out tangente);
        Vector2 normale = point - bord;
        if (normale.sqrMagnitude < 0.0001f)
            normale = new Vector2(-tangente.y, tangente.x);
        return bord + normale.normalized * (5.3f + MargeEauMonstres + 0.20f);
    }

    private Vector3 DirectionSansEau(Vector2 position, Vector3 direction)
    {
        if (direction.sqrMagnitude < 0.0001f) return direction;
        direction.y = 0f;
        direction.Normalize();
        if (!ZoneEauInterditeMonstre(position + new Vector2(direction.x, direction.z) * 0.9f)
            && !ZoneEauInterditeMonstre(position + new Vector2(direction.x, direction.z) * 1.7f))
            return direction;

        // Teste d'abord de petits virages : le monstre longe ainsi la berge
        // dans le sens le plus proche de sa trajectoire au lieu de rester face
        // à l'eau. Les grands angles servent de dernier dégagement.
        float[] angles = { 30f, -30f, 55f, -55f, 80f, -80f, 110f, -110f, 145f, -145f, 180f };
        Vector3 meilleur = Vector3.zero;
        float meilleurAlignement = -2f;
        for (int i = 0; i < angles.Length; i++)
        {
            Vector3 candidate = Quaternion.Euler(0f, angles[i], 0f) * direction;
            Vector2 direction2 = new Vector2(candidate.x, candidate.z);
            if (ZoneEauInterditeMonstre(position + direction2 * 0.55f)
                || ZoneEauInterditeMonstre(position + direction2 * 1.25f)) continue;
            float alignement = Vector3.Dot(direction, candidate);
            if (alignement > meilleurAlignement)
            {
                meilleurAlignement = alignement;
                meilleur = candidate;
            }
        }
        return meilleur.sqrMagnitude > 0.0001f ? meilleur.normalized : -direction;
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

    private Vector2 TangenteRouteProche(Vector2 p)
    {
        Vector2 tangente = Vector2.up;
        float distanceMin = 100000f;
        for (int i = 0; i < RoutePoints.Length - 1; i++)
        {
            Vector2 a = RoutePoints[i];
            Vector2 b = RoutePoints[i + 1];
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(ab.sqrMagnitude, 0.001f));
            Vector2 projete = a + ab * t;
            float distance = (p - projete).sqrMagnitude;
            if (distance < distanceMin)
            {
                distanceMin = distance;
                tangente = ab.normalized;
            }
        }
        return tangente.sqrMagnitude > 0.001f ? tangente : Vector2.up;
    }

    // La cloture du village separe l'interieur de l'exterieur : un monstre ne
    // peut pas frapper le heros a travers (sauf au niveau des portails).
    private bool ClotureEntre(Vector3 a, Vector3 b)
    {
        if (DansVillage(a.x, a.z) == DansVillage(b.x, b.z)) return false;
        var milieu = new Vector2((a.x + b.x) * 0.5f, (a.z + b.z) * 0.5f);
        return DistRoute(milieu) > DemiOuverturePortail;
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
                && DistRoute(p) > DemiOuverturePortail)
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
        // La route est entièrement pavée : le support suit aussi ses bords
        // pour éviter que les pieds ne s'enfoncent dans le raccord extérieur.
        float dr = DistRoute(new Vector2(x, z));
        if (dr < 3.55f)
        {
            float bordRoute = Mathf.InverseLerp(3.55f, 2.75f, dr);
            sol += Mathf.Lerp(0.025f, 0.15f, bordRoute);
        }
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
        // Une grille plus fine evite qu'une cellule complete de terrain
        // traverse le fleuve et laisse du gazon au milieu de l'eau.
        const int cells = 96;
        const float step = WorldSize * 2f / cells;
        var mesh = new Mesh { name = "LibreViesTerrain" };
        var vertices = new Vector3[(cells + 1) * (cells + 1)];
        var trianglesSol = new List<int>();
        var trianglesEau = new List<int>();
        for (int z = 0; z <= cells; z++)
        {
            for (int x = 0; x <= cells; x++)
            {
                float wx = -WorldSize + x * step;
                float wz = -WorldSize + z * step;
                vertices[z * (cells + 1) + x] = new Vector3(wx, TerrainHeight(wx, wz), wz);
            }
        }
        for (int z = 0; z < cells; z++)
        {
            for (int x = 0; x < cells; x++)
            {
                int a = z * (cells + 1) + x;
                int b = a + 1;
                int c = a + cells + 1;
                int d = c + 1;
                float centreX = -WorldSize + (x + 0.5f) * step;
                float centreZ = -WorldSize + (z + 0.5f) * step;
                bool zoneEau = !DansVillage(centreX, centreZ)
                    && DistancePolyligne(new Vector2(centreX, centreZ), RivierePrincipale) <= 5.8f;
                List<int> destination = zoneEau ? trianglesEau : trianglesSol;
                destination.Add(a); destination.Add(c); destination.Add(b);
                destination.Add(b); destination.Add(c); destination.Add(d);
            }
        }
        mesh.vertices = vertices;
        mesh.subMeshCount = 2;
        mesh.SetTriangles(trianglesSol, 0);
        mesh.SetTriangles(trianglesEau, 1);
        mesh.RecalculateNormals();
        var terrain = new GameObject("Terrain_Unity");
        terrain.AddComponent<MeshFilter>().sharedMesh = mesh;
        var terrainRenderer = terrain.AddComponent<MeshRenderer>();
        terrainRenderer.sharedMaterials = new[] { Mat("Terrain"), Mat("Eau_Riviere") };
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

    // MAILLAGE PROCEDURAL DE SECOURS
    // L'herbe en touffes et les rochers de la reference sont des maillages
    // faits a la main (MultiMesh cote reference, un seul maillage ici). Les
    // sapins de secours ont besoin de cones : Unity n'a que des primitives
    // simples, donc on fabrique les triangles nous-memes.
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

        // Boite de mur avec angles legerement chanfreines : la facade n'est
        // plus un cube mathematiquement parfait, tout en gardant exactement
        // la meme emprise de collision en dehors du maillage.
        public void BoiteBiseautee(float largeur, float profondeur, float hauteur, float chanfrein)
        {
            float x = largeur * 0.5f;
            float z = profondeur * 0.5f;
            float c = Mathf.Min(chanfrein, Mathf.Min(x, z) * 0.45f);
            Vector2[] contour =
            {
                new Vector2(-x + c, -z), new Vector2(-x, -z + c),
                new Vector2(-x, z - c), new Vector2(-x + c, z),
                new Vector2(x - c, z), new Vector2(x, z - c),
                new Vector2(x, -z + c), new Vector2(x - c, -z)
            };
            var bas = new Vector3[contour.Length];
            var haut = new Vector3[contour.Length];
            for (int i = 0; i < contour.Length; i++)
            {
                bas[i] = new Vector3(contour[i].x, 0f, contour[i].y);
                haut[i] = new Vector3(contour[i].x, hauteur, contour[i].y);
            }
            for (int i = 0; i < contour.Length; i++)
            {
                int suivant = (i + 1) % contour.Length;
                Quad(bas[i], bas[suivant], haut[suivant], haut[i]);
            }
            for (int i = 1; i < contour.Length - 1; i++)
            {
                Triangle(bas[0], bas[i + 1], bas[i]);
                Triangle(haut[0], haut[i], haut[i + 1]);
            }
        }

        public void ToitTriangle(float largeur, float profondeur, float hauteur)
        {
            float x = largeur * 0.5f;
            float z = profondeur * 0.5f;
            Vector3 avantG = new Vector3(-x, 0f, -z);
            Vector3 avantD = new Vector3(x, 0f, -z);
            Vector3 arriereG = new Vector3(-x, 0f, z);
            Vector3 arriereD = new Vector3(x, 0f, z);
            Vector3 faItageAvant = new Vector3(0f, hauteur, -z);
            Vector3 faItageArriere = new Vector3(0f, hauteur, z);
            Triangle(avantD, avantG, faItageAvant);
            Triangle(arriereG, arriereD, faItageArriere);
            Quad(avantG, faItageAvant, faItageArriere, arriereG);
            Quad(avantD, arriereD, faItageArriere, faItageAvant);
            Quad(avantG, arriereG, arriereD, avantD);
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
    // ni dans la fontaine. Sert a poser le decor sans qu'un arbre pousse dans
    // un mur, dans le fleuve ou au milieu de la route.
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
        if (EstDansEau(new Vector2(x, z))) return false;
        if (new Vector2(x - 9f, z + 15f).magnitude < 3.2f + marge) return false;  // fontaine
        return true;
    }

    private Vector2 CourbeRoute(float t)
    {
        float position = Mathf.Clamp01(t) * (RoutePoints.Length - 1);
        int i = Mathf.Min(RoutePoints.Length - 2, Mathf.FloorToInt(position));
        float local = position - i;
        Vector2 p0 = RoutePoints[Mathf.Max(0, i - 1)];
        Vector2 p1 = RoutePoints[i];
        Vector2 p2 = RoutePoints[i + 1];
        Vector2 p3 = RoutePoints[Mathf.Min(RoutePoints.Length - 1, i + 2)];
        return 0.5f * ((2f * p1) + (-p0 + p2) * local
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * local * local
            + (-p0 + 3f * p1 - 3f * p2 + p3) * local * local * local);
    }

    private void CreateRoad()
    {
        // Route entièrement pavée : le soubassement ne sert qu'à boucher les
        // raccords avec le terrain, et toute la surface visible reçoit la
        // pierre. Les dalles sont assez rapprochées pour que le chemin ne
        // redevienne jamais une bande de terre entre deux pavés.
        var fond = new Maillage();
        var bord = new Maillage();
        var pave = new Maillage();
        var dalles = new Maillage();
        const int echantillons = 120;
        Vector2[] centres = new Vector2[echantillons + 1];
        Vector2[] normales = new Vector2[echantillons + 1];
        for (int i = 0; i <= echantillons; i++)
        {
            float t = i / (float)echantillons;
            centres[i] = CourbeRoute(t);
            float avant = Mathf.Max(0f, t - 0.002f);
            float apres = Mathf.Min(1f, t + 0.002f);
            Vector2 direction = (CourbeRoute(apres) - CourbeRoute(avant)).normalized;
            normales[i] = new Vector2(-direction.y, direction.x);
        }
        for (int i = 0; i < echantillons; i++)
        {
            AjouterBandeRoute(fond, centres, normales, i, 3.62f, 0.025f);
            AjouterBandeRoute(bord, centres, normales, i, 3.52f, 0.075f);
            AjouterBandeRoute(pave, centres, normales, i, 3.34f, 0.13f);
            if (i % 2 == 0)
            {
                // Quatre rangées par travée donnent un pavage continu, avec
                // des joints visibles mais sans trous d'herbe ou de terre.
                AjouterDalleRoute(dalles, centres, normales, i, -2.48f, 0.82f, 0.70f);
                AjouterDalleRoute(dalles, centres, normales, i, -0.83f, 0.82f, 0.70f);
                AjouterDalleRoute(dalles, centres, normales, i, 0.83f, 0.82f, 0.70f);
                AjouterDalleRoute(dalles, centres, normales, i, 2.48f, 0.82f, 0.70f);
            }
        }
        // Même la bordure et le fond restent minéraux : aucune bande brune ne
        // doit réapparaître au milieu de la route si la caméra s'abaisse.
        ObjetMaillage("Route_Soubassement", fond.VersMesh("Route_Soubassement"), "Pave_Route");
        ObjetMaillage("Route_Sinueuse", bord.VersMesh("Route_Sinueuse"), "Route_Bord");
        ObjetMaillage("Paves_Route", pave.VersMesh("Paves_Route"), "Pave_Route");
        ObjetMaillage("Dalles_Route", dalles.VersMesh("Dalles_Route"), "Pave_Route");
    }

    private void AjouterDalleRoute(Maillage maillage, Vector2[] centres, Vector2[] normales,
        int index, float decalage, float demiLongueur, float demiLargeur)
    {
        Vector2 centre = centres[index] + normales[index] * decalage;
        Vector2 tangent = index < centres.Length - 1
            ? (centres[index + 1] - centres[index]).normalized
            : (centres[index] - centres[index - 1]).normalized;
        Vector2 normale = normales[index];
        Vector2 avant = centre + tangent * demiLongueur;
        Vector2 arriere = centre - tangent * demiLongueur;
        Vector2 gauche = avant + normale * demiLargeur;
        Vector2 droite = avant - normale * demiLargeur;
        Vector2 gaucheArriere = arriere + normale * demiLargeur;
        Vector2 droiteArriere = arriere - normale * demiLargeur;
        float hauteur = 0.15f;
        maillage.Quad(new Vector3(gauche.x, TerrainHeight(gauche.x, gauche.y) + hauteur, gauche.y),
            new Vector3(gaucheArriere.x, TerrainHeight(gaucheArriere.x, gaucheArriere.y) + hauteur, gaucheArriere.y),
            new Vector3(droiteArriere.x, TerrainHeight(droiteArriere.x, droiteArriere.y) + hauteur, droiteArriere.y),
            new Vector3(droite.x, TerrainHeight(droite.x, droite.y) + hauteur, droite.y));
    }

    private void AjouterBandeRoute(Maillage maillage, Vector2[] centres, Vector2[] normales,
        int index, float demiLargeur, float hauteur)
    {
        Vector2 gauche = centres[index] + normales[index] * demiLargeur;
        Vector2 droite = centres[index] - normales[index] * demiLargeur;
        Vector2 gauche2 = centres[index + 1] + normales[index + 1] * demiLargeur;
        Vector2 droite2 = centres[index + 1] - normales[index + 1] * demiLargeur;
        maillage.Quad(new Vector3(gauche.x, TerrainHeight(gauche.x, gauche.y) + hauteur, gauche.y),
            new Vector3(gauche2.x, TerrainHeight(gauche2.x, gauche2.y) + hauteur, gauche2.y),
            new Vector3(droite2.x, TerrainHeight(droite2.x, droite2.y) + hauteur, droite2.y),
            new Vector3(droite.x, TerrainHeight(droite.x, droite.y) + hauteur, droite.y));
    }

    private void CreateTown()
    {
        // Les maisons sont etalees dans le nouveau rayon de 40 m.
        CreateBuilding(new Vector3(-18, 0, 13), new Vector3(8, 4, 7), "Maison_Ouest");
        CreateBuilding(new Vector3(19, 0, 10), new Vector3(8, 5, 8), "Maison_Est");
        CreateBuilding(new Vector3(-17, 0, -14), new Vector3(7, 3.5f, 7), "Atelier");
        CreateBuilding(new Vector3(18, 0, -15), new Vector3(9, 4, 7), "Auberge");
        CreateBuilding(new Vector3(-28, 0, -1), new Vector3(6, 3.5f, 6), "Entrepot");
        CreateBuilding(new Vector3(29, 0, -3), new Vector3(6, 3.5f, 6), "Forge");
        // La mairie est proche du centre, mais decalee de la route.
        CreateBuilding(new Vector3(-8, 0, 14), new Vector3(7, 4, 6), "Mairie");
        // Cette maison devient le salon d'esthétique : le nom est porté par
        // sa pancarte et le médecin attend juste devant sa façade.
        CreateBuilding(new Vector3(-27, 0, 20), new Vector3(6, 3.5f, 5), "Esthetique");
        // Maison_Sud est remise sur le terrain plat du village, loin de la
        // colline du chateau : son socle ne s'enfonce plus dans la pente.
        // La maison devant l'auberge revient a son axe d'origine puis est
        // decalee legerement vers la droite, a l'oppose du dernier essai.
        CreateBuilding(new Vector3(9.5f, 0, -6), new Vector3(7, 4, 6), "Maison_Sud");
        // Fontaine sur le terrain libre directement devant la mairie.
        CreateFountain(new Vector3(-8, 0, 24));
        // Les PNJ sont a moins d'une largeur de porte de leur batiment.
        CreerPnj(new Vector3(25.2f, 0, 0.8f), "Forgeron");
        // Le vendeur inutile devant une maison a ete retire. Le marchand reste
        // a droite de l'entrepot, derriere son etal.
        CreerPnj(new Vector3(-25.3f, 0, 2.5f), "Marchand");
        CreerPnj(new Vector3(-4.8f, 0, 17.5f), "Maire");
        CreerPnj(new Vector3(-27f, 0, 23.35f), "Medecin");
        // Les gardes ne sont pas poses ici : ils sont crees par CreateGuards(),
        // juste devant les portails du village (voir CreateFence).
    }

    private void CreateBuilding(Vector3 position, Vector3 size, string name)
    {
        var root = new GameObject(name).transform;
        root.position = new Vector3(position.x, TerrainHeight(position.x, position.z), position.z);
        var murMaillage = new Maillage();
        murMaillage.BoiteBiseautee(size.x, size.z, size.y, 0.22f);
        var murs = ObjetMaillage("Murs", murMaillage.VersMesh("Murs_" + name), "Wall");
        var murCollider = murs.AddComponent<MeshCollider>();
        murCollider.sharedMesh = murs.GetComponent<MeshFilter>().sharedMesh;
        murs.transform.SetParent(root, false);
        // Socle, chaînages d'angle et poutres de rive donnent une silhouette
        // bâtie plus crédible sans modifier l'emprise de collision du bâtiment.
        Box(new Vector3(0f, 0.14f, 0f), new Vector3(size.x + 0.30f, 0.28f, size.z + 0.30f),
            "Stone", root, "Soubassement");
        for (int sx = -1; sx <= 1; sx += 2)
        {
            for (int sz = -1; sz <= 1; sz += 2)
            {
                Box(new Vector3(sx * (size.x * 0.5f - 0.12f), size.y * 0.5f,
                    sz * (size.z * 0.5f - 0.12f)), new Vector3(0.24f, size.y - 0.30f, 0.24f),
                    "Bois_Clair", root, "Chaine_Angle");
            }
        }
        string materiauToit = name == "Mairie" ? "RoofBlue" : (name == "Forge" ? "RoofRed" : "Roof");
        CreerToitTriangle(root, size, materiauToit, "Toit_" + name);
        Box(new Vector3(0f, size.y - 0.10f, (size.z + 0.72f) * 0.5f),
            new Vector3(size.x + 0.95f, 0.18f, 0.20f), "Wood", root, "Rive_Avant");
        Box(new Vector3(0f, size.y - 0.10f, -(size.z + 0.72f) * 0.5f),
            new Vector3(size.x + 0.95f, 0.18f, 0.20f), "Wood", root, "Rive_Arriere");
        if (name == "Forge" || name == "Auberge" || name == "Mairie")
        {
            Box(new Vector3(size.x * 0.24f, size.y + 2.05f, 0f), new Vector3(0.48f, 1.05f, 0.48f),
                "Stone", root, "Cheminee");
        }
        // On ne traverse plus les maisons (0,5 m de marge comme la reference).
        ColBoite(position.x, position.z, size.x + 0.5f, size.z + 0.5f, size.y);
        batiments.Add(new Batiment
        {
            X = position.x, Z = position.z, Largeur = size.x, Profondeur = size.z
        });
        // Facade et ouvertures orientees vers le sud : dans ce monde, le sud
        // est le cote +z. Les panneaux suivent exactement cette facade.
        Box(new Vector3(0, 1.0f, size.z * 0.51f), new Vector3(1.2f, 2f, 0.12f), "Wood", root, "Porte");
        for (int side = -1; side <= 1; side += 2)
        {
            Box(new Vector3(side * size.x * 0.27f, 1.8f, size.z * 0.515f), new Vector3(1.0f, 0.75f, 0.10f), "Glass", root, "Fenetre");
        }
        CreerAfficheMaison(root, size, name);
    }

    private string LibelleMaison(string nom)
    {
        if (nom == "Forge") return "FORGE";
        if (nom == "Atelier") return "ATELIER";
        if (nom == "Auberge") return "AUBERGE";
        if (nom == "Entrepot") return "ENTREPOT";
        if (nom == "Mairie") return "MAIRIE";
        if (nom == "Esthetique") return "ESTHÉTIQUE";
        if (nom == "Maison_Nord") return "MAISON";
        if (nom == "Maison_Sud") return "MAISON";
        return "MAISON";
    }

    private void CreerAfficheMaison(Transform parent, Vector3 taille, string nom)
    {
        float z = taille.z * 0.525f;
        float largeur;
        if (nom == "Auberge") largeur = 5.20f;
        else if (nom == "Atelier") largeur = 4.80f;
        else if (nom == "Entrepot") largeur = 5.00f;
        else if (nom == "Forge") largeur = 4.00f;
        else if (nom == "Mairie") largeur = 4.40f;
        else if (nom == "Esthetique") largeur = 5.80f;
        else largeur = 4.20f;
        float tailleTexte = nom == "Auberge" ? 0.15f
            : (nom == "Esthetique" ? 0.11f : 0.14f);
        Box(new Vector3(0f, 2.62f, z), new Vector3(largeur, 0.68f, 0.08f), "Bois_Clair", parent, "Affiche_Maison");
        Vector3 position = parent.TransformPoint(new Vector3(0f, 2.62f, z + 0.06f));
        GameObject texte = CreerTexte3D(LibelleMaison(nom), position, new Color(0.16f, 0.09f, 0.04f), tailleTexte);
        if (texte != null)
        {
            // Le TextMesh devait etre retourne pour que le nom soit lisible
            // depuis la facade sud (+z), pas en miroir.
            texte.transform.rotation = parent.rotation * Quaternion.Euler(0f, 180f, 0f);
            textesFacades.Add(new FacadeTextState
            {
                Root = texte,
                Position = position,
                DirectionFacade = parent.TransformDirection(Vector3.forward).normalized
            });
        }
    }

    private void CreerToitTriangle(Transform parent, Vector3 taille, string materiau, string nom)
    {
        var maillage = new Maillage();
        maillage.ToitTriangle(taille.x + 0.8f, taille.z + 0.8f, 2.2f);
        var toit = ObjetMaillage(nom, maillage.VersMesh(nom), materiau);
        // Un collider est necessaire au rayon camera : il pourra rendre le toit
        // translucide comme les autres murs quand il passe devant le joueur.
        var collisionToit = toit.AddComponent<MeshCollider>();
        collisionToit.sharedMesh = toit.GetComponent<MeshFilter>().sharedMesh;
        toit.transform.SetParent(parent, false);
        // La base du toit recouvre legerement le haut du mur : aucun jour
        // lumineux entre la maison et sa toiture.
        toit.transform.localPosition = new Vector3(0f, taille.y - 0.02f, 0f);
    }

    private void CreateFountain(Vector3 position)
    {
        var root = new GameObject("Fontaine").transform;
        root.position = new Vector3(position.x, TerrainHeight(position.x, position.z), position.z);
        Primitive(PrimitiveType.Cylinder, Vector3.zero, new Vector3(3.4f, 0.18f, 3.4f), "Stone", root, "Bassin");
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 1.1f, 0), new Vector3(0.32f, 1.1f, 0.32f), "Stone", root, "Colonne");
        // Eau plate au sommet : l'ancienne boule bleue flottante est supprimee.
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 2.05f, 0), new Vector3(0.72f, 0.035f, 0.72f), "Water", root, "Eau");
        Primitive(PrimitiveType.Cylinder, new Vector3(0, 2.17f, 0), new Vector3(0.12f, 0.12f, 0.12f), "Water", root, "Jet");
        ColCercle(position.x, position.z, 1.7f, 0.36f);   // bassin
        ColCercle(position.x, position.z, 0.32f, 2.2f);   // colonne
    }

    private void CreateWaterways()
    {
        // Il ne reste qu'un seul fleuve qui traverse la carte ; aucune
        // derivation ni autre surface d'eau n'est creee.
        ObjetMaillage("Riviere_Principale",
            CreerRubanEau(RivierePrincipale, 5.20f, "Riviere_Principale"), "Eau_Riviere");
    }

    private Vector2[] EchantillonnerLigne(Vector2[] points)
    {
        var resultats = new List<Vector2>();
        const int subdivisions = 8;
        for (int i = 0; i < points.Length - 1; i++)
        {
            for (int j = 0; j < subdivisions; j++)
                resultats.Add(Vector2.Lerp(points[i], points[i + 1], j / (float)subdivisions));
        }
        resultats.Add(points[points.Length - 1]);
        return resultats.ToArray();
    }

    private Mesh CreerRubanEau(Vector2[] points, float demiLargeur, string nom)
    {
        points = EchantillonnerLigne(points);
        var ruban = new Maillage();
        Vector2[] normales = new Vector2[points.Length];
        for (int i = 0; i < points.Length; i++)
        {
            Vector2 avant = points[Mathf.Max(0, i - 1)];
            Vector2 apres = points[Mathf.Min(points.Length - 1, i + 1)];
            Vector2 direction = (apres - avant).normalized;
            normales[i] = new Vector2(-direction.y, direction.x);
        }
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 gauche = points[i] + normales[i] * demiLargeur;
            Vector2 droite = points[i] - normales[i] * demiLargeur;
            Vector2 gauche2 = points[i + 1] + normales[i + 1] * demiLargeur;
            Vector2 droite2 = points[i + 1] - normales[i + 1] * demiLargeur;
            // Une hauteur unique par section evite que les deux bords du
            // ruban se croisent quand le terrain monte d'un cote. Elle est
            // prise sur l'axe du chenal : l'eau reste posee au niveau du sol
            // naturel, sans etre remontee artificiellement sur une berge.
            float hauteur1 = HauteurSurfaceEau(points[i].x, points[i].y);
            float hauteur2 = HauteurSurfaceEau(points[i + 1].x, points[i + 1].y);
            ruban.Quad(new Vector3(gauche.x, hauteur1, gauche.y),
                new Vector3(gauche2.x, hauteur2, gauche2.y),
                new Vector3(droite2.x, hauteur2, droite2.y),
                new Vector3(droite.x, hauteur1, droite.y));
        }
        return ruban.VersMesh(nom);
    }

    private void CreateFence()
    {
        const int posts = 96;                 // 96 poteaux, comme la reference
        // 1) Ou la route traverse l'enceinte, il y a un PORTAIL (deux trous
        //    dans cet anneau : au nord et au sud). On les repere d'abord, pour
        //    que le dessin et les collisions soient d'accord.
        //
        //    ATTENTION (incident 19, gel au demarrage) : cette liste contient
        //    les INTERVALLES D'ANGLE (debut, fin). Elle ne doit surtout pas
        //    s'appeler 'portails' : ce nom est celui du CHAMP qui garde la
        //    position des portails pour les gardes. En le masquant, la boucle
        //    de construction ajoutait dans la liste qu'elle parcourait :
        //    boucle infinie, et aucun garde a l'arrivee.
        var intervalles = new List<Vector2>();   // (angle de debut, angle de fin)
        const int pas = 720;
        bool dansTrou = false;
        float debut = 0f;
        for (int i = 0; i <= pas; i++)
        {
            float a = i * Mathf.PI * 2f / pas;
            bool trou = DistRoute(new Vector2(Mathf.Cos(a) * VillageRadius, Mathf.Sin(a) * VillageRadius)) < DemiOuverturePortail;
            if (trou && !dansTrou) { dansTrou = true; debut = a; }
            else if (!trou && dansTrou)
            {
                dansTrou = false;
                intervalles.Add(new Vector2(debut, a));
            }
        }

        // 2) Poteaux + traverses sur toute la longueur, portails exclus.
        for (int i = 0; i < posts; i++)
        {
            float angle = i * Mathf.PI * 2f / posts;
            float x = Mathf.Cos(angle) * VillageRadius;
            float z = Mathf.Sin(angle) * VillageRadius;
            if (DistRoute(new Vector2(x, z)) < DemiOuverturePortail) continue;
            float y = TerrainHeight(x, z);
            Primitive(PrimitiveType.Cylinder, new Vector3(x, y + 0.65f, z), new Vector3(0.14f, 0.65f, 0.14f), "Wood", null, "Cloture");
        }
        for (int i = 0; i < posts; i++)
        {
            float angle = (i + 0.5f) * Mathf.PI * 2f / posts;
            float x = Mathf.Cos(angle) * VillageRadius;
            float z = Mathf.Sin(angle) * VillageRadius;
            if (DistRoute(new Vector2(x, z)) < DemiOuverturePortail) continue;
            float y = TerrainHeight(x, z) + 0.85f;
            var rail = Box(new Vector3(x, y, z), new Vector3(0.12f, 0.14f, 2.8f), "Wood", null, "Traverse");
            rail.transform.rotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
        }

        // 3) Les portails : grands poteaux, linteau et panneau LIBREVIES.
        for (int g = 0; g < intervalles.Count; g++)
        {
            float a0 = intervalles[g].x;
            float a1 = intervalles[g].y;
            float am = (a0 + a1) * 0.5f;
            Vector2 passage = new Vector2(Mathf.Cos(am) * VillageRadius, Mathf.Sin(am) * VillageRadius);
            // Le portique est recentré exactement sur la sortie : aucun
            // décalage latéral ne doit repousser la sortie nord (ou sud) vers
            // la gauche. Les faces internes des deux poteaux laissent 5 m.
            Vector2 tangente = TangenteRouteProche(passage);
            Vector2 travers = new Vector2(-tangente.y, tangente.x).normalized;
            const float largeurLibre = LargeurOuverturePortail;
            const float demiPoteau = RayonPoteauPortail; // cylindre scale .30 => rayon .15
            float demiEspacementCentres = largeurLibre * 0.5f + demiPoteau;
            Vector2 pilierGauche = passage - travers * demiEspacementCentres;
            Vector2 pilierDroit = passage + travers * demiEspacementCentres;
            Vector2[] piliers = { pilierGauche, pilierDroit };
            for (int p = 0; p < piliers.Length; p++)
            {
                Vector2 pilier = piliers[p];
                float gy = TerrainHeight(pilier.x, pilier.y);
                Primitive(PrimitiveType.Cylinder, new Vector3(pilier.x, gy + 2.6f, pilier.y),
                    new Vector3(0.30f, 2.6f, 0.30f), "Wood", null, "Poteau_Portail");
                Box(new Vector3(pilier.x, gy + 5.26f, pilier.y), new Vector3(0.40f, 0.14f, 0.40f),
                    "Wood", null, "Chapeau_Portail");
            }
            float my = TerrainHeight(passage.x, passage.y);
            // La largeur extérieure du linteau et de la pancarte inclut les
            // deux poteaux : ils se rejoignent proprement sans surplomb.
            float largeurExterieure = largeurLibre + demiPoteau * 4f;
            float largeurPanneau = largeurExterieure - 0.06f;
            portails.Add(passage);
            Quaternion rotation = Quaternion.LookRotation(new Vector3(travers.x, 0f, travers.y).normalized)
                * Quaternion.Euler(0f, 90f, 0f);
            Box(new Vector3(passage.x, my + 4.78f, passage.y),
                new Vector3(largeurExterieure, 1.05f, 0.30f), "Wood", null,
                "Fermeture_Superieure", false, rotation);
            Box(new Vector3(passage.x, my + 5.35f, passage.y),
                new Vector3(largeurExterieure + 0.08f, 0.22f, 0.38f), "Bois_Clair", null,
                "Linteau", false, rotation);
            Box(new Vector3(passage.x, my + 4.45f, passage.y),
                new Vector3(largeurPanneau, 0.76f, 0.34f), "Bois_Clair", null,
                "Panneau_Fond", true, rotation);
            Box(new Vector3(passage.x, my + 4.45f, passage.y),
                new Vector3(largeurPanneau - 0.06f, 0.60f, 0.30f), "Wood", null,
                "Panneau_Bois", false, rotation);
            AjouterTextePanneau(new Vector3(passage.x, my + 4.45f, passage.y), rotation);
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
        // Deux inscriptions dos a dos : l'une est lisible depuis l'exterieur,
        // l'autre depuis l'interieur. Elles sont legerement remontees et
        // decalees de la planche pour ne jamais depasser par dessous.
        Vector3 normale = rotation * Vector3.forward;
        float hauteur = position.y + 0.14f;
        Vector3 faceExterieure = new Vector3(position.x, hauteur, position.z) + normale * 0.22f;
        Vector3 faceInterieure = new Vector3(position.x, hauteur, position.z) - normale * 0.22f;
        GameObject exterieur = CreerTexte3D("LIBREVIES", faceExterieure, Color.white, 0.12f);
        GameObject interieur = CreerTexte3D("LIBREVIES", faceInterieure, Color.white, 0.12f);
        if (exterieur != null)
        {
            exterieur.name = "Texte_Portail_Exterieur";
            exterieur.transform.rotation = rotation * Quaternion.Euler(0f, 180f, 0f);
            textesFacades.Add(new FacadeTextState
            {
                Root = exterieur, Position = faceExterieure, DirectionFacade = normale.normalized
            });
        }
        if (interieur != null)
        {
            interieur.name = "Texte_Portail_Interieur";
            interieur.transform.rotation = rotation;
            textesFacades.Add(new FacadeTextState
            {
                Root = interieur, Position = faceInterieure, DirectionFacade = (-normale).normalized
            });
        }
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
            Vector2 tangente = TangenteRouteProche(portail);
            Vector2 coteRoute = new Vector2(-tangente.y, tangente.x).normalized;
            // Le garde reste à l'intérieur de la clôture, au-delà de la
            // bordure de la route : le passage et les dalles restent libres.
            Vector2 poste = portail.normalized * (VillageRadius * 0.95f) + coteRoute * 4.2f;
            CreerGarde(poste, portail);
        }
    }

    private void TeinterGarde(Transform model, Color couleur)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materiaux = renderers[i].materials;
            bool modifie = false;
            for (int j = 0; j < materiaux.Length; j++)
            {
                if (materiaux[j] == null || materiaux[j].name.IndexOf("GuardCloth", StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                materiaux[j].color = couleur;
                modifie = true;
            }
            if (modifie) renderers[i].materials = materiaux;
        }
    }

    private void TeinterPnj(Transform model, Color couleur)
    {
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materiaux = renderers[i].materials;
            bool modifie = false;
            for (int j = 0; j < materiaux.Length; j++)
            {
                if (materiaux[j] == null) continue;
                string nom = materiaux[j].name;
                if (nom.IndexOf("Jacket", StringComparison.OrdinalIgnoreCase) < 0) continue;
                materiaux[j].color = couleur;
                modifie = true;
            }
            if (modifie) renderers[i].materials = materiaux;
        }
    }

    private void CreerGarde(Vector2 poste, Vector2 portail)
    {
        const string dossier = "Characters/LibreViesGuardParts";
        float y = TerrainHeight(poste.x, poste.y);
        var root = new GameObject("Garde").transform;
        root.position = new Vector3(poste.x, y, poste.y);
        var state = new GardeState { Root = root.gameObject, Poste = poste, Portail = portail };
        GameObject model = new GameObject("Garde_Humanoide_Original_CC0");
        model.transform.SetParent(root, false);
        state.Model = model.transform;
        state.Model.localPosition = Vector3.zero;
        state.Model.localRotation = Quaternion.identity;
        state.Model.localScale = Vector3.one;

        string[] partiesFixes =
        {
            "Guard_Belt", "Guard_Torso", "Guard_ChestPlate", "Guard_Shoulder_L",
            "Guard_Shoulder_R", "Guard_Head", "Guard_Helmet", "Guard_Visor", "Guard_Crest"
        };
        for (int i = 0; i < partiesFixes.Length; i++)
            ChargerPartieImportee(state.Model, dossier, partiesFixes[i]);

        // Les gardes sont maintenant des maillages humanoïdes importés, pas des
        // cubes/capsules Godot ou Unity. Les segments inférieurs sont attachés
        // aux genoux et les avant-bras aux coudes.
        state.JambeG = CreerPivotImporte(state.Model, dossier,
            new Vector3(-0.18f, 1.12f, 0f), "Pivot_Garde_Cuisse_G", "Guard_LegUpper_L");
        state.JambeD = CreerPivotImporte(state.Model, dossier,
            new Vector3(0.18f, 1.12f, 0f), "Pivot_Garde_Cuisse_D", "Guard_LegUpper_R");
        state.GenouG = CreerPivotImporte(state.Model, dossier,
            new Vector3(-0.18f, 0.66f, 0f), "Pivot_Garde_Genou_G",
            "Guard_LegLower_L", "Guard_Boot_L");
        state.GenouD = CreerPivotImporte(state.Model, dossier,
            new Vector3(0.18f, 0.66f, 0f), "Pivot_Garde_Genou_D",
            "Guard_LegLower_R", "Guard_Boot_R");
        state.GenouG.SetParent(state.JambeG, true);
        state.GenouD.SetParent(state.JambeD, true);
        state.BrasG = CreerPivotImporte(state.Model, dossier,
            new Vector3(-0.40f, 1.58f, 0f), "Pivot_Garde_Bras_G", "Guard_ArmUpper_L");
        state.BrasD = CreerPivotImporte(state.Model, dossier,
            new Vector3(0.40f, 1.58f, 0f), "Pivot_Garde_Bras_D", "Guard_ArmUpper_R");
        state.CoudeG = CreerPivotImporte(state.Model, dossier,
            new Vector3(-0.48f, 1.40f, 0.01f), "Pivot_Garde_Coude_G",
            "Guard_ArmLower_L", "Guard_Glove_L");
        state.CoudeD = CreerPivotImporte(state.Model, dossier,
            new Vector3(0.48f, 1.40f, 0.01f), "Pivot_Garde_Coude_D",
            "Guard_ArmLower_R", "Guard_Glove_R");
        state.CoudeG.SetParent(state.BrasG, true);
        state.CoudeD.SetParent(state.BrasD, true);
        // Le pivot est au niveau de la main droite : la hampe tourne avec
        // la main et ne traverse plus l'épaule quand le garde se déplace.
        state.Hallebarde = CreerPivotImporte(state.Model, dossier,
            new Vector3(0.59f, 1.10f, 0.05f), "Pivot_Garde_Hallebarde",
            "Guard_HalberdShaft", "Guard_HalberdBlade", "Guard_HalberdHook", "Guard_HalberdTip");
        AppliquerShaderPersonnage(state.Model);
        TeinterGarde(state.Model, gardes.Count % 2 == 0
            ? new Color(0.18f, 0.34f, 0.78f)
            : new Color(0.72f, 0.16f, 0.12f));

        state.Corps = new Obstacle
        {
            Cercle = true, X = poste.x, Z = poste.y, Rayon = 0.48f,
            Portee = 1.5f, Hauteur = 2.2f
        };
        obstacles.Add(state.Corps);
        gardes.Add(state);
        Debug.Log("[LV] garde humanoïde OBJ séparé chargé et articulé");
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
            float cycle = marche ? Mathf.Sin(garde.Phase * 8f) : 0f;
            float balancement = marche ? cycle * 34f : 0f;
            float flexionGenouG = marche ? Mathf.Max(0f, -cycle) * 44f : 0f;
            float flexionGenouD = marche ? Mathf.Max(0f, cycle) * 44f : 0f;
            float bras = marche ? cycle * 28f : Mathf.Sin(garde.Phase * 2.2f) * 4f;
            float flexionCoudeG = marche ? Mathf.Max(0f, cycle) * 18f : 0f;
            float flexionCoudeD = marche ? Mathf.Max(0f, -cycle) * 18f : 0f;
            if (garde.JambeG != null) garde.JambeG.localRotation = Quaternion.Euler(balancement, 0f, 0f);
            if (garde.JambeD != null) garde.JambeD.localRotation = Quaternion.Euler(-balancement, 0f, 0f);
            if (garde.GenouG != null) garde.GenouG.localRotation = Quaternion.Euler(flexionGenouG, 0f, 0f);
            if (garde.GenouD != null) garde.GenouD.localRotation = Quaternion.Euler(flexionGenouD, 0f, 0f);
            if (garde.BrasG != null) garde.BrasG.localRotation = Quaternion.Euler(-bras, 0f, 0f);
            if (garde.BrasD != null) garde.BrasD.localRotation = Quaternion.Euler(bras, 0f, 0f);
            if (garde.CoudeG != null) garde.CoudeG.localRotation = Quaternion.Euler(flexionCoudeG, 0f, 0f);
            if (garde.CoudeD != null) garde.CoudeD.localRotation = Quaternion.Euler(flexionCoudeD, 0f, 0f);
            if (garde.Model != null)
            {
                garde.Model.localRotation = Quaternion.identity;
                garde.Model.localPosition = new Vector3(0f, marche ? Mathf.Abs(cycle) * 0.018f : 0f, 0f);
            }
            if (garde.Hallebarde != null)
                garde.Hallebarde.localRotation = Quaternion.Euler(0f, 0f, marche ? -bras * 0.35f : bras);
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

    private void CreerEnclumeForgeron(Transform parent)
    {
        var racine = new GameObject("Enclume_Forgeron").transform;
        racine.SetParent(parent, false);
        // Le socle commence au niveau du terrain ; le plateau est à hauteur
        // des coups du marteau, au lieu de flotter comme l'ancien cube.
        racine.localPosition = new Vector3(0.80f, 0f, 0.45f);

        var socle = new Maillage();
        socle.BoiteBiseautee(0.62f, 0.48f, 0.42f, 0.08f);
        GameObject socleObjet = ObjetMaillage("Enclume_Socle", socle.VersMesh("Enclume_Socle"), "Metal");
        socleObjet.transform.SetParent(racine, false);

        var col = new Maillage();
        col.BoiteBiseautee(0.36f, 0.32f, 0.30f, 0.06f);
        GameObject colObjet = ObjetMaillage("Enclume_Col", col.VersMesh("Enclume_Col"), "Metal");
        colObjet.transform.SetParent(racine, false);
        colObjet.transform.localPosition = new Vector3(0f, 0.42f, 0f);

        var plateau = new Maillage();
        plateau.BoiteBiseautee(0.92f, 0.42f, 0.14f, 0.07f);
        GameObject plateauObjet = ObjetMaillage("Enclume_Plateau", plateau.VersMesh("Enclume_Plateau"), "Metal");
        plateauObjet.transform.SetParent(racine, false);
        plateauObjet.transform.localPosition = new Vector3(0f, 0.72f, 0f);

        // Corne conique orientée vers l'avant de l'établi.
        var corne = new Maillage();
        Vector3 baseG = new Vector3(-0.18f, 0.76f, 0.14f);
        Vector3 baseD = new Vector3(0.18f, 0.76f, 0.14f);
        Vector3 hautD = new Vector3(0.18f, 0.85f, 0.14f);
        Vector3 hautG = new Vector3(-0.18f, 0.85f, 0.14f);
        Vector3 pointe = new Vector3(0f, 0.81f, 0.64f);
        corne.Triangle(baseG, baseD, pointe);
        corne.Triangle(baseD, hautD, pointe);
        corne.Triangle(hautD, hautG, pointe);
        corne.Triangle(hautG, baseG, pointe);
        corne.Triangle(baseG, hautG, hautD);
        corne.Triangle(baseG, hautD, baseD);
        GameObject corneObjet = ObjetMaillage("Enclume_Corne", corne.VersMesh("Enclume_Corne"), "Metal");
        corneObjet.transform.SetParent(racine, false);
    }

    private void CreerPnj(Vector3 position, string metier)
    {
        float y = TerrainHeight(position.x, position.z);
        var root = new GameObject("PNJ_" + metier).transform;
        root.position = new Vector3(position.x, y, position.z);
        var pnj = new PnjState { Root = root.gameObject, Metier = metier };

        // Les habitants utilisent le même humanoïde OBJ indépendant que
        // l'héroïne : plus de capsule, cube ou tête carrée issue du prototype
        // Godot devant les maisons. Les pivots restent séparés pour que leurs
        // bras et leurs jambes puissent bouger réellement.
        const string dossier = "Characters/LibreViesHeroineParts";
        GameObject objetModele = new GameObject("PNJ_Humanoide_Importe");
        pnj.Model = objetModele.transform;
        pnj.Model.SetParent(root, false);
        pnj.Corps = pnj.Model;
        pnj.Model.localScale = Vector3.one * 1.02f;
        string[] partiesFixes =
        {
            "Belt", "Jacket", "Neck", "Collar", "Head", "Ear_L", "Ear_R",
            "EyeWhite_L", "EyeWhite_R", "Eye_L", "Eye_R", "Brow_L", "Brow_R",
            "Nose", "Mouth", "JacketZip",
            "JacketButton_1_31", "JacketButton_1_45", "JacketButton_1_59",
            "HairCap", "HairBack", "HairLock_L", "HairLock_R"
        };
        for (int i = 0; i < partiesFixes.Length; i++)
            ChargerPartieImportee(pnj.Model, dossier, partiesFixes[i]);
        pnj.JambeG = CreerPivotImporte(pnj.Model, dossier,
            new Vector3(-0.18f, 1.12f, 0f), "Pivot_PNJ_Cuisse_G", "JeansUpper_L");
        pnj.JambeD = CreerPivotImporte(pnj.Model, dossier,
            new Vector3(0.18f, 1.12f, 0f), "Pivot_PNJ_Cuisse_D", "JeansUpper_R");
        pnj.GenouG = CreerPivotImporte(pnj.Model, dossier,
            new Vector3(-0.18f, 0.66f, 0f), "Pivot_PNJ_Genou_G",
            "JeansLower_L", "Boot_L", "Shoe_L");
        pnj.GenouD = CreerPivotImporte(pnj.Model, dossier,
            new Vector3(0.18f, 0.66f, 0f), "Pivot_PNJ_Genou_D",
            "JeansLower_R", "Boot_R", "Shoe_R");
        pnj.GenouG.SetParent(pnj.JambeG, true);
        pnj.GenouD.SetParent(pnj.JambeD, true);
        pnj.BrasG = CreerPivotImporte(pnj.Model, dossier,
            new Vector3(-0.40f, 1.58f, 0f), "Pivot_PNJ_Bras_G", "SleeveUpper_L");
        pnj.BrasD = CreerPivotImporte(pnj.Model, dossier,
            new Vector3(0.40f, 1.58f, 0f), "Pivot_PNJ_Bras_D", "SleeveUpper_R");
        pnj.CoudeG = CreerPivotImporte(pnj.Model, dossier,
            new Vector3(-0.48f, 1.40f, 0.02f), "Pivot_PNJ_Coude_G",
            "SleeveLower_L", "Cuff_L", "Hand_L");
        pnj.CoudeD = CreerPivotImporte(pnj.Model, dossier,
            new Vector3(0.48f, 1.40f, 0.02f), "Pivot_PNJ_Coude_D",
            "SleeveLower_R", "Cuff_R", "Hand_R");
        pnj.CoudeG.SetParent(pnj.BrasG, true);
        pnj.CoudeD.SetParent(pnj.BrasD, true);
        pnj.Main = new GameObject("Point_Main_PNJ").transform;
        pnj.Main.SetParent(pnj.CoudeD, false);
        pnj.Main.localPosition = new Vector3(0.14f, -0.35f, 0.04f);
        AppliquerShaderPersonnage(pnj.Model);
        TeinterPnj(pnj.Model, metier == "Maire"
            ? new Color(0.12f, 0.25f, 0.58f)
            : (metier == "Forgeron" ? new Color(0.24f, 0.27f, 0.31f)
            : (metier == "Medecin" ? new Color(0.88f, 0.90f, 0.94f)
            : new Color(0.36f, 0.25f, 0.20f))));

        if (metier == "Medecin")
        {
            // Blouse claire et croix rouge : les accessoires sont des détails
            // de tenue, le corps reste le vrai maillage humanoïde importé.
            Box(new Vector3(0f, 1.45f, 0.235f), new Vector3(0.055f, 0.25f, 0.018f),
                "Red", pnj.Model, "Croix_Medecin_Verticale");
            Box(new Vector3(0f, 1.45f, 0.235f), new Vector3(0.16f, 0.055f, 0.018f),
                "Red", pnj.Model, "Croix_Medecin_Horizontale");
        }
        else if (metier == "Forgeron")
        {
            // Enclume sur pied, avec plateau et pointe : ce n'est plus un
            // simple cube flottant. Le marteau est parenté au point de la main.
            CreerEnclumeForgeron(root);
            pnj.Marteau = new GameObject("Marteau_Forgeron").transform;
            pnj.Marteau.SetParent(pnj.Main, false);
            pnj.Marteau.localPosition = Vector3.zero;
            Box(new Vector3(0f, -0.15f, 0f), new Vector3(0.09f, 0.42f, 0.09f),
                "Bois_Clair", pnj.Marteau, "Manche_Marteau");
            Box(new Vector3(0f, -0.39f, 0f), new Vector3(0.40f, 0.20f, 0.18f),
                "Metal", pnj.Marteau, "Tete_Marteau");
        }
        else if (metier == "Maire")
        {
            // La feuille est tenue devant le maire et bouge legerement comme
            // une presentation au public.
            pnj.Feuille = new GameObject("Feuille_Maire").transform;
            pnj.Feuille.SetParent(pnj.Main, false);
            pnj.Feuille.localPosition = new Vector3(-0.12f, -0.02f, 0.10f);
            Box(Vector3.zero, new Vector3(0.48f, 0.62f, 0.035f), "White", pnj.Feuille, "Feuille");
            Box(new Vector3(0f, 0.18f, -0.025f), new Vector3(0.30f, 0.025f, 0.012f), "Dirt", pnj.Feuille, "Ligne_Feuille");
        }
        else if (metier == "Vendeur" || metier == "Marchand")
        {
            // Le marchand devant l'entrepôt n'a plus de table : les cartons
            // reposent directement au sol et restent des accessoires du décor.
            pnj.Etal = new GameObject("Cartons_" + metier).transform;
            pnj.Etal.SetParent(root, false);
            pnj.Etal.localPosition = new Vector3(0f, 0f, 0.72f);
            Box(new Vector3(-0.28f, 0.24f, 0f), new Vector3(0.52f, 0.48f, 0.52f),
                "Carton", pnj.Etal, "Carton_Bas_Gauche");
            Box(new Vector3(0.25f, 0.20f, 0.08f), new Vector3(0.44f, 0.40f, 0.46f),
                "Carton", pnj.Etal, "Carton_Bas_Droit");
            Box(new Vector3(-0.02f, 0.67f, 0.02f), new Vector3(0.48f, 0.34f, 0.46f),
                "Bois_Clair", pnj.Etal, "Carton_Haut");
        }
        ColCercle(position.x, position.z, 0.42f, 2.2f);
        pnjs.Add(pnj);
    }

    private void UpdatePnj(float dt)
    {
        for (int i = 0; i < pnjs.Count; i++)
        {
            PnjState pnj = pnjs[i];
            pnj.Phase += dt;
            float balancement;
            float hauteur;
            if (pnj.Metier == "Forgeron")
            {
                balancement = Mathf.Sin(pnj.Phase * 1.4f) * 0.55f;
                hauteur = 0f;
            }
            else if (pnj.Metier == "Medecin")
            {
                balancement = Mathf.Sin(pnj.Phase * 0.8f) * 0.045f;
                hauteur = 0f;
            }
            else if (pnj.Metier == "Vendeur" || pnj.Metier == "Marchand")
            {
                balancement = Mathf.Sin(pnj.Phase * 1.4f) * 0.08f;
                hauteur = 0f;
            }
            else if (pnj.Metier == "Maire")
            {
                balancement = Mathf.Sin(pnj.Phase * 0.5f) * 0.20f;
                hauteur = 0f;
            }
            else
            {
                balancement = Mathf.Abs(Mathf.Sin(pnj.Phase * 1.8f)) * 0.12f;
                hauteur = 0f;
            }
            if (pnj.Corps != null) pnj.Corps.localPosition = new Vector3(0f, hauteur, 0f);
            if (pnj.BrasG != null) pnj.BrasG.localRotation = Quaternion.Euler(balancement * Mathf.Rad2Deg, 0f, 0f);
            if (pnj.BrasD != null) pnj.BrasD.localRotation = Quaternion.Euler(-balancement * Mathf.Rad2Deg, 0f, 0f);
            if (pnj.CoudeG != null) pnj.CoudeG.localRotation = Quaternion.Euler(Mathf.Max(0f, balancement) * 16f, 0f, 0f);
            if (pnj.CoudeD != null) pnj.CoudeD.localRotation = Quaternion.Euler(Mathf.Max(0f, -balancement) * 16f, 0f, 0f);
            if (pnj.JambeG != null) pnj.JambeG.localRotation = Quaternion.Euler(Mathf.Sin(pnj.Phase * 0.8f) * 3f, 0f, 0f);
            if (pnj.JambeD != null) pnj.JambeD.localRotation = Quaternion.Euler(-Mathf.Sin(pnj.Phase * 0.8f) * 3f, 0f, 0f);
            if (pnj.GenouG != null) pnj.GenouG.localRotation = Quaternion.Euler(Mathf.Max(0f, -Mathf.Sin(pnj.Phase * 0.8f)) * 6f, 0f, 0f);
            if (pnj.GenouD != null) pnj.GenouD.localRotation = Quaternion.Euler(Mathf.Max(0f, Mathf.Sin(pnj.Phase * 0.8f)) * 6f, 0f, 0f);
            if (pnj.Marteau != null && pnj.Main != null)
            {
                // Le marteau suit la main. Son angle est volontairement
                // inverse à celui du bras : quand le bras recule, le marteau
                // avance vers l'enclume, et inversement.
                pnj.Marteau.position = pnj.Main.position;
                pnj.Marteau.rotation = pnj.Root.transform.rotation
                    * Quaternion.Euler(-18f - balancement * Mathf.Rad2Deg * 1.25f, 0f, 0f);
            }
            if (pnj.Feuille != null)
            {
                pnj.Feuille.localRotation = Quaternion.Euler(4f + Mathf.Sin(pnj.Phase * 0.8f) * 7f,
                    Mathf.Sin(pnj.Phase * 0.6f) * 5f, Mathf.Sin(pnj.Phase * 0.9f) * 4f);
            }
            if (pnj.Etal != null)
                pnj.Etal.localRotation = Quaternion.identity;
        }
    }

    private void CreateTreesAndProps()
    {
        // Sapins disperses (meme esprit que la reference, avec ses regles :
        // jamais sur la route, jamais dans un batiment, jamais dans le village).
        var random = new System.Random(4217);
        int plantes = 0;
        for (int i = 0; i < 320 && plantes < 120; i++)
        {
            float x = (float)(random.NextDouble() * 236 - 118);
            float z = (float)(random.NextDouble() * 236 - 118);
            if (new Vector2(x, z).magnitude < VillageRadius + 4) continue;
            if (!EmplacementLibre(x, z, 1.5f)) continue;
            string asset = (plantes % 3 == 0) ? "CommonTree_1" : "Pine_1";
            float taille = (float)(random.NextDouble() * 0.24 + 0.92);
            if (!CreateNatureInstance(asset, x, z, taille, true))
                CreatePine(x, z, (float)(random.NextDouble() * 0.7 + 0.8));
            plantes++;
        }
        // La nature CC0 ajoute plusieurs couches de détail : buissons, fougères,
        // fleurs, plantes et rochers. Les primitives restent le repli si une
        // installation Unity ne retrouve pas les fichiers Resources.
        CreateImportedNatureDetails();
        // Les anciens arbres ronds (boule sur cylindre, vestiges de l'ancien
        // prototype Godot) ne sont plus créés. La végétation visible provient
        // uniquement des arbres importés CC0 et des sapins procéduraux de secours.
    }

    private GameObject NaturePrefab(string asset)
    {
        GameObject prefab;
        if (naturePrefabs.TryGetValue(asset, out prefab)) return prefab;
        prefab = Resources.Load<GameObject>("WorldAssets/" + asset + "/" + asset);
        naturePrefabs[asset] = prefab;
        if (prefab == null)
            Debug.LogWarning("[LV] asset nature introuvable : Resources/WorldAssets/" + asset);
        return prefab;
    }

    private bool CreateNatureInstance(string asset, float x, float z, float scale, bool collision)
    {
        GameObject prefab = NaturePrefab(asset);
        if (prefab == null) return false;
        float baseY = TerrainHeight(x, z) + (asset.StartsWith("CommonTree") || asset.StartsWith("Pine") ? 0.24f : 0.08f);
        GameObject instance = Instantiate(prefab, new Vector3(x, baseY, z),
            Quaternion.Euler(0f, UnityEngine.Random.Range(0f, 360f), 0f));
        instance.name = "Asset_CC0_" + asset;
        instance.transform.localScale = Vector3.one * scale;
        natureInstances++;
        objetsCrees++;

        // Les collisions de déplacement du jeu restent mathématiques, mais le
        // modèle possède aussi un collider Unity exploitable par les outils et
        // les futures interactions physiques.
        if (collision)
        {
            if (asset.StartsWith("CommonTree") || asset.StartsWith("Pine"))
            {
                CapsuleCollider tronc = instance.AddComponent<CapsuleCollider>();
                tronc.center = new Vector3(0f, 2.5f, 0f);
                tronc.radius = 0.65f;
                tronc.height = 5.2f;
                ColCercle(x, z, 0.72f * scale, 6.8f * scale);
            }
            else if (asset.StartsWith("Rock"))
            {
                BoxCollider rocher = instance.AddComponent<BoxCollider>();
                rocher.center = new Vector3(0f, 0.75f, 0f);
                rocher.size = new Vector3(2.2f, 1.6f, 2.2f);
                ColCercle(x, z, 1.0f * scale, 2.0f * scale);
            }
        }
        return true;
    }

    private void CreateImportedNatureDetails()
    {
        var random = new System.Random(5817);
        string[] assets = { "Bush_Common", "Fern_1", "Flower_3_Group", "Plant_1_Big", "Grass_Common_Tall", "Rock_Medium_1" };
        int[] limites = { 18, 22, 18, 16, 24, 18 };
        for (int type = 0; type < assets.Length; type++)
        {
            int poses = 0;
            for (int essai = 0; essai < limites[type] * 8 && poses < limites[type]; essai++)
            {
                float x = (float)(random.NextDouble() * 226f - 113f);
                float z = (float)(random.NextDouble() * 226f - 113f);
                if (new Vector2(x, z).magnitude < VillageRadius + 3.5f) continue;
                if (!EmplacementLibre(x, z, type == 5 ? 1.4f : 0.65f)) continue;
                float scale = type == 5
                    ? (float)(random.NextDouble() * 0.55f + 0.75f)
                    : (float)(random.NextDouble() * 0.45f + 0.82f);
                if (CreateNatureInstance(assets[type], x, z, scale, type == 5)) poses++;
            }
        }
        Journal("nature CC0 : " + natureInstances + " instances importees");
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
        // Le premier cone commence a 2,55 m : il recouvre le sommet du tronc
        // (2,60 m) au lieu de laisser un espace visible.
        CreateCone(root, new Vector3(0f, 2.55f, 0f), 1.35f, 2.4f, "Sapin_Bas", "Feuillage_Bas");
        CreateCone(root, new Vector3(0f, 3.75f, 0f), 1.05f, 2.1f, "Sapin_Milieu", "Feuillage_Milieu");
        CreateCone(root, new Vector3(0f, 4.80f, 0f), 0.72f, 1.8f, "Sapin_Haut", "Feuillage_Haut");
        ColCercle(x, z, 0.4f * taille, 2.2f);   // tronc
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
                while ((DistRoute(new Vector2(x, z)) < 3.2f
                    || EstDansEau(new Vector2(x, z))) && garde < 16)
                {
                    x = (float)(rng.NextDouble() * (WorldSize * 2 - 8) - (WorldSize - 4));
                    z = (float)(rng.NextDouble() * (WorldSize * 2 - 8) - (WorldSize - 4));
                    x = Mathf.Clamp(x, -WorldSize + 4, WorldSize - 4);
                    z = Mathf.Clamp(z, -WorldSize + 4, WorldSize - 4);
                    garde++;
                }
                if (EstDansEau(new Vector2(x, z))) continue;
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
            if (p.magnitude > VillageRadius - 2f) continue; // hors du village
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

    private Transform ChargerPartieImportee(Transform parent, string dossier, string nom)
    {
        GameObject prefab = Resources.Load<GameObject>(dossier + "/" + nom);
        if (prefab == null)
        {
            Debug.LogError("[LV] partie OBJ absente : " + dossier + "/" + nom);
            return null;
        }
        GameObject instance = Instantiate(prefab, parent);
        instance.name = nom;
        instance.transform.localPosition = Vector3.zero;
        instance.transform.localRotation = Quaternion.identity;
        instance.transform.localScale = Vector3.one;
        return instance.transform;
    }

    private void AppliquerShaderPersonnage(Transform model)
    {
        Shader shader = ChargerShader("LVShaders/LVCharacterOpaque");
        if (shader == null || model == null) return;
        Renderer[] renderers = model.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Material[] materiaux = renderers[i].materials;
            for (int j = 0; j < materiaux.Length; j++)
            {
                if (materiaux[j] == null) continue;
                Color couleur = materiaux[j].HasProperty("_Color")
                    ? materiaux[j].color : Color.white;
                Texture texture = materiaux[j].HasProperty("_MainTex")
                    ? materiaux[j].GetTexture("_MainTex") : null;
                materiaux[j].shader = shader;
                if (materiaux[j].HasProperty("_Color")) materiaux[j].color = couleur;
                if (texture != null && materiaux[j].HasProperty("_MainTex"))
                    materiaux[j].SetTexture("_MainTex", texture);
            }
            renderers[i].materials = materiaux;
        }
    }

    private Transform CreerPivotImporte(Transform model, string dossier, Vector3 position,
        string nom, params string[] parties)
    {
        var pivot = new GameObject(nom).transform;
        pivot.SetParent(model, false);
        pivot.localPosition = position;
        pivot.localRotation = Quaternion.identity;
        for (int i = 0; i < parties.Length; i++)
        {
            Transform partie = ChargerPartieImportee(model, dossier, parties[i]);
            if (partie != null)
            {
                // Chaque partie provient maintenant d'un OBJ indépendant :
                // Unity ne peut plus la fusionner avec le torse ou un autre
                // membre dans le même Mesh.
                partie.SetParent(pivot, true);
            }
        }
        return pivot;
    }

    private bool CreateHumanHeroine(Transform body)
    {
        const string dossier = "Characters/LibreViesHeroineParts";
        GameObject model = new GameObject("Heroine_Humaine_Originale_CC0");
        model.transform.SetParent(body, false);
        heroineModel = model.transform;
        heroineModel.localPosition = Vector3.zero;
        heroineModel.localRotation = Quaternion.identity;
        heroineModel.localScale = Vector3.one * 1.05f;

        // Les éléments fixes sont eux aussi des OBJ importés indépendants :
        // aucune primitive Unity n'est utilisée pour reconstituer le modèle.
        string[] partiesFixes =
        {
            "Belt", "Jacket", "Neck", "Collar", "Head", "Ear_L", "Ear_R",
            "EyeWhite_L", "EyeWhite_R", "Eye_L", "Eye_R", "Brow_L", "Brow_R",
            "Nose", "Mouth", "JacketZip",
            "JacketButton_1_31", "JacketButton_1_45", "JacketButton_1_59",
            "HairCap", "HairBack", "HairLock_L", "HairLock_R"
        };
        int piecesChargees = 0;
        for (int i = 0; i < partiesFixes.Length; i++)
            if (ChargerPartieImportee(heroineModel, dossier, partiesFixes[i]) != null) piecesChargees++;

        // Les pivots ne peuvent plus être ignorés par l'importeur : chaque
        // segment visible est un fichier OBJ distinct dans Resources.
        brasHeroineGauche = CreerPivotImporte(heroineModel, dossier,
            new Vector3(-0.40f, 1.58f, 0f), "Pivot_Bras_Gauche", "SleeveUpper_L");
        brasHeroineDroit = CreerPivotImporte(heroineModel, dossier,
            new Vector3(0.40f, 1.58f, 0f), "Pivot_Bras_Droit", "SleeveUpper_R");
        coudeHeroineGauche = CreerPivotImporte(heroineModel, dossier,
            new Vector3(-0.48f, 1.40f, 0.02f), "Pivot_Coude_Gauche",
            "SleeveLower_L", "Cuff_L", "Hand_L");
        coudeHeroineDroit = CreerPivotImporte(heroineModel, dossier,
            new Vector3(0.48f, 1.40f, 0.02f), "Pivot_Coude_Droit",
            "SleeveLower_R", "Cuff_R", "Hand_R");
        coudeHeroineGauche.SetParent(brasHeroineGauche, true);
        coudeHeroineDroit.SetParent(brasHeroineDroit, true);
        jambeHeroineGauche = CreerPivotImporte(heroineModel, dossier,
            new Vector3(-0.18f, 1.12f, 0f), "Pivot_Cuisse_Gauche", "JeansUpper_L");
        jambeHeroineDroite = CreerPivotImporte(heroineModel, dossier,
            new Vector3(0.18f, 1.12f, 0f), "Pivot_Cuisse_Droite", "JeansUpper_R");
        genouHeroineGauche = CreerPivotImporte(heroineModel, dossier,
            new Vector3(-0.18f, 0.66f, 0f), "Pivot_Genou_Gauche",
            "JeansLower_L", "Boot_L", "Shoe_L");
        genouHeroineDroit = CreerPivotImporte(heroineModel, dossier,
            new Vector3(0.18f, 0.66f, 0f), "Pivot_Genou_Droit",
            "JeansLower_R", "Boot_R", "Shoe_R");
        genouHeroineGauche.SetParent(jambeHeroineGauche, true);
        genouHeroineDroit.SetParent(jambeHeroineDroite, true);
        brasAttaque = brasHeroineDroit;
        AppliquerShaderPersonnage(heroineModel);
        heroineRigPret = piecesChargees > 0
            && brasHeroineGauche.childCount > 0 && brasHeroineDroit.childCount > 0
            && coudeHeroineGauche.childCount > 0 && coudeHeroineDroit.childCount > 0
            && jambeHeroineGauche.childCount > 0 && jambeHeroineDroite.childCount > 0
            && genouHeroineGauche.childCount > 0 && genouHeroineDroit.childCount > 0;
        ConfigurerAnimationsHeroine(model);
        Debug.Log("[LV] héroïne CC0 assemblée avec " + piecesChargees
            + " OBJ fixes ; animation par OBJ séparés=" + heroineRigPret);
        return heroineRigPret;
    }

    private void ConfigurerAnimationsHeroine(GameObject model)
    {
        // LibreViesHeroine.obj est volontairement sans squelette. Les groupes
        // OBJ sont donc animés par AnimerHeroine, tandis qu'un futur FBX/glTF
        // pourra remplacer ce chemin par un Animator sans modifier le gameplay.
        JouerAnimationHeroine("Idle", true);
    }

    private void JouerAnimationHeroine(string recherche, bool boucle)
    {
        heroineAnimationActuelle = recherche;
    }

    private void AnimerHeroine(bool enMouvement, bool enCourse)
    {
        if (heroineModel == null) return;
        // Ne jamais incliner le personnage : la rotation de joueur ne contient
        // que le lacet horizontal, et l'animation ne touche qu'aux pivots.
        heroineModel.localRotation = Quaternion.identity;
        if (!heroineRigPret)
        {
            heroineModel.localPosition = Vector3.zero;
            return;
        }

        float cycle = enMouvement ? Mathf.Sin(walkClock) : 0f;
        float balancementJambe = (enCourse ? 38f : 30f) * cycle;
        float balancementBras = (enCourse ? 32f : 24f) * cycle;
        float amplitudeGenou = enCourse ? 48f : 36f;
        float flexionGenouGauche = enMouvement
            ? amplitudeGenou * Mathf.Max(0f, -cycle)
            : 0f;
        float flexionGenouDroit = enMouvement
            ? amplitudeGenou * Mathf.Max(0f, cycle)
            : 0f;
        float flexionCoudeGauche = enMouvement
            ? (enCourse ? 22f : 14f) * Mathf.Max(0f, cycle)
            : 0f;
        float flexionCoudeDroit = enMouvement
            ? (enCourse ? 22f : 14f) * Mathf.Max(0f, -cycle)
            : 0f;
        if (attackAnimation > 0f)
        {
            float phase = 1f - attackAnimation / 0.30f;
            brasHeroineDroit.localRotation = Quaternion.Euler(
                Mathf.Lerp(-105f, 56f, phase), Mathf.Lerp(-18f, 8f, phase), 0f);
            coudeHeroineDroit.localRotation = Quaternion.Euler(Mathf.Lerp(-20f, -68f, phase), 0f, 0f);
            brasHeroineGauche.localRotation = Quaternion.Euler(-8f, 0f, 0f);
            coudeHeroineGauche.localRotation = Quaternion.Euler(-8f, 0f, 0f);
        }
        else
        {
            brasHeroineGauche.localRotation = Quaternion.Euler(-balancementBras, 0f, 0f);
            brasHeroineDroit.localRotation = Quaternion.Euler(balancementBras, 0f, 0f);
            coudeHeroineGauche.localRotation = Quaternion.Euler(flexionCoudeGauche, 0f, 0f);
            coudeHeroineDroit.localRotation = Quaternion.Euler(flexionCoudeDroit, 0f, 0f);
        }
        jambeHeroineGauche.localRotation = Quaternion.Euler(balancementJambe, 0f, 0f);
        jambeHeroineDroite.localRotation = Quaternion.Euler(-balancementJambe, 0f, 0f);
        genouHeroineGauche.localRotation = Quaternion.Euler(flexionGenouGauche, 0f, 0f);
        genouHeroineDroit.localRotation = Quaternion.Euler(flexionGenouDroit, 0f, 0f);
        // La marche ne doit pas faire sautiller le personnage : ses pieds
        // restent calés sur le sol et seul le balancement des articulations
        // anime la foulée.
        heroineModel.localPosition = Vector3.zero;
    }

    private void CreatePlayer()
    {
        player = new GameObject("Joueur").transform;
        // Point de départ choisi pour que la caméra voie le village.
        player.position = new Vector3(0, TerrainHeight(0, 6) + 0.05f, 6);
        cameraPivot = new GameObject("CameraPivot").transform;
        cameraPivot.SetParent(player, false);
        heroBody = new GameObject("Heros_LowPoly").transform;
        heroBody.SetParent(player, false);
        var body = heroBody;
        // Le maillage humanoïde original CC0 est obligatoire : aucun cube,
        // sphère ou assemblage procédural ne doit remplacer le personnage.
        if (!CreateHumanHeroine(body))
        {
            Debug.LogError("[LV] Modèle humanoïde introuvable : LibreViesHeroine.obj doit être importé par Unity.");
            brasAttaque = body;
        }
        joueurCollider = player.gameObject.AddComponent<CapsuleCollider>();
        joueurCollider.center = new Vector3(0, 1.15f, 0);
        joueurCollider.height = 2.3f;
        joueurCollider.radius = 0.48f;
    }

    private void CreateEnemies()
    {
        // 6 zones de 3 betes, exactement comme la reference : souris (25 PV),
        // rats (50 PV), araignees (75 PV). Une bete qui naitrait dans le
        // village protege est repoussee juste dehors.
        string[] types = { "souris", "souris", "rat", "rat", "araignee", "araignee" };
        float[] centresX = { 55f, -55f, 70f, -70f, 35f, -45f };
        float[] centresZ = { 45f, -40f, -55f, 55f, 80f, -82f };
        for (int zone = 0; zone < types.Length; zone++)
        {
            for (int i = 0; i < 3; i++)
            {
                float x = centresX[zone] + UnityEngine.Random.Range(-8f, 8f);
                float z = centresZ[zone] + UnityEngine.Random.Range(-8f, 8f);
                if (DansVillage(x, z))
                {
                    Vector2 pousse = new Vector2(x, z);
                    if (pousse.magnitude < 0.001f) pousse = new Vector2(1f, 0f);
                    pousse = pousse.normalized * (VillageRadius + 7f);
                    x = pousse.x;
                    z = pousse.y;
                }
                CreateEnemy(new Vector3(x, 0f, z), types[zone]);
            }
        }
        Debug.Log("[LV] monstres créés : " + enemies.Count + " (6 zones de 3, quantité conservée)");
    }

    private void CreateEnemy(Vector3 position, string type)
    {
        bool spider = type == "araignee";
        bool souris = type == "souris";
        var enemyObject = new GameObject(spider ? "Araignee" : (souris ? "Souris" : "Rat"));
        Vector2 depart = new Vector2(position.x, position.z);
        if (ZoneEauInterditeMonstre(depart))
        {
            depart = SortirZoneEauMonstre(depart);
            position.x = depart.x;
            position.z = depart.y;
        }
        position.y = TerrainHeight(position.x, position.z);
        enemyObject.transform.position = position;
        EnemyState state = new EnemyState
        {
            Root = enemyObject,
            Spider = spider,
            Souris = souris,
            PvMax = spider ? 75 : (souris ? 25 : 50),
            Home = position,
            Legs = new Transform[spider ? 8 : 0],
            // Vitesse tiree au hasard d'un monstre a l'autre (1,2 a 2,0 m/s),
            // comme la reference : certaines betes sont plus vives.
            Speed = UnityEngine.Random.Range(1.2f, 2.0f)
        };
        state.Hp = state.PvMax;
        // Le corps de la bete est un obstacle : on ne la traverse pas. Il suit
        // ses deplacements (voir UpdateEnemies) et il est desactive a sa mort.
        state.Corps = new Obstacle
        {
            Cercle = true, X = position.x, Z = position.z,
            Rayon = spider ? 0.5f : (souris ? 0.30f : 0.45f), Portee = 1.5f,
            Hauteur = spider ? 0.7f : 0.9f
        };
        obstacles.Add(state.Corps);
        // La silhouette est dans un sous-objet : une souris est une petite bete.
        var silhouette = new GameObject("Silhouette").transform;
        silhouette.SetParent(enemyObject.transform, false);
        silhouette.localScale = Vector3.one * (souris ? 0.7f : 1f);
        string material = spider ? "Spider" : "Enemy";
        Primitive(PrimitiveType.Sphere, new Vector3(0, spider ? 0.45f : 0.55f, 0), spider ? new Vector3(0.85f, 0.35f, 0.85f) : new Vector3(0.65f, 0.45f, 1.0f), material, silhouette, "Corps");
        Primitive(PrimitiveType.Sphere, new Vector3(0, spider ? 0.62f : 0.72f, -0.48f), new Vector3(0.24f, 0.24f, 0.24f), "Red", silhouette, "Yeux");
        if (spider)
        {
            for (int i = 0; i < 8; i++)
            {
                float angle = i * Mathf.PI * 2f / 8f;
                var leg = Box(new Vector3(Mathf.Cos(angle) * 0.75f, 0.35f, Mathf.Sin(angle) * 0.75f), new Vector3(0.10f, 0.10f, 1.1f), "Spider", silhouette, "Patte");
                leg.transform.rotation = Quaternion.Euler(0, angle * Mathf.Rad2Deg, 25f * Mathf.Sin(angle));
                state.Legs[i] = leg.transform;
            }
        }

        // Barre de vie au-dessus de la bete (comme la reference) : fond sombre
        // et partie rouge proportionnelle aux PV. Elle reste cachee tant que la
        // bete est intacte, et se tourne vers la camera.
        var barre = new GameObject("BarreVie").transform;
        barre.SetParent(enemyObject.transform, false);
        barre.localPosition = new Vector3(0f, spider ? 1.1f : 0.75f, 0f);
        barre.gameObject.SetActive(false);
        Box(new Vector3(0f, 0f, 0f), new Vector3(0.9f, 0.10f, 0.03f), "Vie_Fond", barre, "BarreVie_Fond");
        var remplissage = Box(new Vector3(0f, 0f, 0.02f), new Vector3(0.86f, 0.07f, 0.03f), "Vie_Rouge", barre, "BarreVie_Remplissage");
        state.Barre = barre;
        state.Remplissage = remplissage.transform;

        enemies.Add(state);
    }

    private void CreatePickups()
    {
        // 15 cailloux et 3 pieces, comme la reference : JAMAIS dans le village
        // (il reste un refuge) et jamais sur la route. Quand on les ramasse, ils
        // ne reviennent pas au meme endroit mais ailleurs, au hasard : c'est ce
        // qui fait "apparaitre des choses" dans le monde au fil du temps.
        for (int i = 0; i < 22; i++) CreatePickup(PositionRessource(85f, 3.0f), false);
        for (int i = 0; i < 5; i++) CreatePickup(PositionRessource(70f, 3.0f), true);
    }

    // Position aleatoire hors du village et hors de la route : on retire tant
    // que ce n'est pas le cas (comme le while de la reference), avec un nombre
    // d'essais borne pour ne jamais pouvoir bloquer le jeu.
    private Vector3 PositionRessource(float portee, float margeRoute)
    {
        for (int essai = 0; essai < 60; essai++)
        {
            float x = UnityEngine.Random.Range(-portee, portee);
            float z = UnityEngine.Random.Range(-portee, portee);
            if (new Vector2(x, z).magnitude < VillageRadius + 2f) continue;
            if (DistRoute(new Vector2(x, z)) < margeRoute) continue;
            if (EstDansEau(new Vector2(x, z))) continue;
            return new Vector3(x, 0f, z);
        }
        // Repli : juste a l'exterieur de l'enceinte, jamais devant un portail.
        float angle = UnityEngine.Random.Range(0f, Mathf.PI * 2f);
        return new Vector3(Mathf.Cos(angle) * (VillageRadius + 4f), 0f, Mathf.Sin(angle) * (VillageRadius + 4f));
    }

    private void CreatePickup(Vector3 position, bool coin)
    {
        // Tailles de la reference : caillou 0,28 x 0,22 x 0,28 pose a 0,14 m,
        // piece de 0,36 m de diametre et 0,05 m d'epaisseur posee a 0,12 m.
        var root = Primitive(coin ? PrimitiveType.Cylinder : PrimitiveType.Sphere, Vector3.zero,
            coin ? new Vector3(0.36f, 0.025f, 0.36f) : new Vector3(0.30f, 0.24f, 0.30f),
            coin ? "Gold" : "Stone", null, coin ? "Piece" : "Caillou");
        var pickup = new PickupState { Root = root, Coin = coin, Home = position };
        pickups.Add(pickup);
        PoserPickup(pickup, position);
    }

    // Pose un caillou (ou une piece) a l'endroit voulu, en le posant bien sur
    // le terrain (les pieces sont plates, les cailloux plus epais).
    private void PoserPickup(PickupState pickup, Vector3 position)
    {
        position.y = TerrainHeight(position.x, position.z) + (pickup.Coin ? 0.12f : 0.14f);
        pickup.Root.transform.position = position;
        pickup.Home = position;
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

    private bool Touche(int code)
    {
        return Input.GetKey((KeyCode)code);
    }

    private bool ToucheDown(int code)
    {
        return Input.GetKeyDown((KeyCode)code);
    }

    private void UpdatePlayer(float dt)
    {
        if (dead)
        {
            if (ToucheDown(toucheRenaître)) Respawn();
            return;
        }
        // La saisie de texte ne doit ni faire marcher l'héroïne ni déclencher
        // une attaque lorsque la fenêtre de conversation est ouverte.
        if (conversationOpen) return;
        if (attackCooldown > 0) attackCooldown -= dt;
        if (attackAnimation > 0) attackAnimation -= dt;
        // Le joueur ne reçoit qu'un lacet horizontal. Le corps reste donc
        // toujours droit, même lorsque la camera regarde vers le bas.
        if (heroBody != null) heroBody.localRotation = Quaternion.identity;
        if (playerProtection > 0) playerProtection -= dt;
        if (invincibility > 0) invincibility -= dt;
        if (speedBoost > 0) speedBoost -= dt;
        playerInWater = player != null && EstDansEau(new Vector2(player.position.x, player.position.z));
        // L'immersion est mesurée sur la hauteur de la tete. L'endurance, elle,
        // descend dès que les pieds sont dans l'eau, afin de ne pas dépendre
        // d'un seul point de détection lorsque le joueur entre par la berge.
        float hauteurTete = player.position.y + 2.35f;
        float hauteurEau = HauteurSurfaceEau(player.position.x, player.position.z);
        // Hysteresis de 15 cm : le voile ne clignote pas lorsque la tete
        // touche exactement la surface pendant une nage ou un mouvement.
        if (!playerInWater)
            playerUnderwater = false;
        else if (playerWasUnderwater)
            playerUnderwater = hauteurTete < hauteurEau + 0.15f;
        else
            playerUnderwater = hauteurTete < hauteurEau - 0.15f;
        if (playerUnderwater && !playerWasUnderwater)
            ShowInfo("Sous l'eau : remontez avec ESPACE");
        playerWasUnderwater = playerUnderwater;
        if (playerInWater)
        {
            // L'endurance se vide dans l'eau. Une fois vide et sous la surface,
            // chaque seconde retire exactement 1 PV jusqu'a la sortie.
            regenClock = 0f;
            endurance = Mathf.MoveTowards(endurance, 0f, dt * 11f);
            if (playerUnderwater && endurance <= 0.01f)
            {
                underwaterDamageClock += dt;
                while (underwaterDamageClock >= 1f && !dead)
                {
                    underwaterDamageClock -= 1f;
                    DamagePlayer(1);
                }
            }
        }
        else
        {
            underwaterDamageClock = 0f;
            regenClock += dt;
            if (regenClock > 3f)
            {
                regenClock = 0;
                if (hp < MaxHp) hp++;
            }
        }

        Vector3 input = Vector3.zero;
        if (Touche(toucheAvant) || Input.GetKey(KeyCode.UpArrow)) input.z += 1;
        if (Touche(toucheArriere) || Input.GetKey(KeyCode.DownArrow)) input.z -= 1;
        if (Touche(toucheGauche) || Input.GetKey(KeyCode.LeftArrow)) input.x -= 1;
        if (Touche(toucheDroite) || Input.GetKey(KeyCode.RightArrow)) input.x += 1;
        if (input.sqrMagnitude > 1) input.Normalize();
        Vector3 forward = gameCamera.transform.forward; forward.y = 0; forward.Normalize();
        Vector3 right = gameCamera.transform.right; right.y = 0; right.Normalize();
        Vector3 direction = forward * input.z + right * input.x;
        bool courseDemandee = Touche(toucheCourir) && direction.sqrMagnitude > 0.01f;
        bool courseActive = courseDemandee && endurance > 0.5f && !playerInWater;
        float speed = playerInWater ? SwimSpeed : (courseActive ? RunSpeed : PlayerSpeed);
        if (!playerInWater)
            endurance = Mathf.MoveTowards(endurance, courseActive ? 0f : 100f,
                dt * (courseActive ? 22f : 16f));
        if (speedBoost > 0) speed += playerInWater ? 0.8f : 3f;
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
            walkClock += dt * (Touche(toucheCourir) ? 14f : 9f);
        }
        else walkClock = 0;
        bool heroineEnMouvement = !playerInWater && direction.sqrMagnitude > 0.01f;
        if (heroineEnMouvement)
            JouerAnimationHeroine(courseActive ? "Running" : "Walking", true);
        else if (!playerInWater)
            JouerAnimationHeroine("Idle", true);
        AnimerHeroine(heroineEnMouvement, courseActive);

        if (playerInWater)
        {
            // Dans l'eau, ESPACE monte et CTRL/C descend. Le heros reste dans
            // le volume navigable au-dessus du lit, sans saut balistique.
            playerGrounded = false;
            bool monter = Touche(toucheSaut) || Input.GetKey(KeyCode.UpArrow);
            bool descendre = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.C) || Input.GetKey(KeyCode.DownArrow);
            float vitesseVerticale = monter ? SwimSpeed : (descendre ? -SwimSpeed : 0f);
            playerVelocity.y = Mathf.MoveTowards(playerVelocity.y, vitesseVerticale, dt * 12f);
            player.position += Vector3.up * playerVelocity.y * dt;
            float lit = TerrainHeight(player.position.x, player.position.z) + 0.08f;
            // L'eau n'enfonce plus automatiquement le personnage : dans une
            // zone peu profonde il peut rester debout, avec la tete au-dessus
            // de la surface. Il ne descend que s'il demande a plonger.
            float plafond = HauteurSurfaceEau(player.position.x, player.position.z) + 0.35f;
            if (player.position.y < lit) player.position = new Vector3(player.position.x, lit, player.position.z);
            if (player.position.y > plafond) player.position = new Vector3(player.position.x, plafond, player.position.z);
        }
        else
        {
            if (ToucheDown(toucheSaut) && playerGrounded)
            {
                playerVelocity.y = ForceSaut;
                playerGrounded = false;
            }
            playerVelocity.y -= Gravite * dt;
            player.position += Vector3.up * playerVelocity.y * dt;
            // Le sol n'est pas que le terrain : route pavee, pont et dessus des caisses.
            float ground = HauteurSupport(player.position.x, player.position.z, player.position.y);
            if (player.position.y <= ground)
            {
                player.position = new Vector3(player.position.x, ground, player.position.z);
                playerVelocity.y = 0;
                playerGrounded = true;
            }
        }
        player.position = new Vector3(Mathf.Clamp(player.position.x, -WorldSize + 2, WorldSize - 2), player.position.y,
            Mathf.Clamp(player.position.z, -WorldSize + 2, WorldSize - 2));

        bool clicGauche = Input.GetMouseButtonDown(0);
        bool clicNpc = clicGauche && TryOuvrirConversation();
        bool clicMonstre = clicGauche && !clicNpc && TryAttaquerMonstreClique();
        bool attaqueClavier = toucheAttaque != (int)KeyCode.Mouse0 && ToucheDown(toucheAttaque);
        // Un clic gauche sur le sol ne déclenche plus l'animation d'attaque.
        // Le clic doit viser un monstre ; une touche d'attaque remappée reste
        // disponible séparément.
        if (!conversationOpen && (clicMonstre || attaqueClavier)) Attack();
        if (ToucheDown(toucheRamasser)) CollectNearby();
        if (ToucheDown(toucheInventaire)) inventoryOpen = !inventoryOpen;
        if (ToucheDown(toucheOptions)) optionsOpen = !optionsOpen;
        if (ToucheDown(toucheQuete)) questOpen = !questOpen;
        if (ToucheDown(toucheCamera))
        {
            firstPerson = !firstPerson;
            firstPersonPitch = 0f;
        }
        if (ToucheDown(toucheRenaître) && dead) Respawn();
        for (int i = 0; i < 5; i++) if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) UseSlot(i);

        if (Input.GetMouseButtonDown(1)) cameraDragging = true;
        if (Input.GetMouseButtonUp(1)) cameraDragging = false;
        if (cameraDragging && Input.GetMouseButton(1))
        {
            // GetAxisRaw : pas de lissage, la caméra suit la souris tout de
            // suite. Sensibilité réglable avec [ et ] (panneau Options).
            cameraYaw += Input.GetAxisRaw("Mouse X") * cameraSensitivity * (inverserAxeX ? -1f : 1f);
            float mouvementVertical = Input.GetAxisRaw("Mouse Y") * cameraSensitivity
                * (inverserAxeY ? -1f : 1f);
            if (firstPerson)
            {
                // En premiere personne, la souris peut maintenant regarder
                // franchement vers le bas comme vers le haut.
                firstPersonPitch = Mathf.Clamp(firstPersonPitch - mouvementVertical, -82f, 82f);
            }
            else
            {
                cameraPitch = Mathf.Clamp(cameraPitch + mouvementVertical, 5f, 70f);
            }
        }
        if (Input.GetKeyDown(KeyCode.LeftBracket))
            cameraSensitivity = Mathf.Clamp(cameraSensitivity - 0.5f, 0.5f, 10f);
        if (Input.GetKeyDown(KeyCode.RightBracket))
            cameraSensitivity = Mathf.Clamp(cameraSensitivity + 0.5f, 0.5f, 10f);
        if (Input.GetKeyDown(KeyCode.Equals) || Input.GetKeyDown(KeyCode.KeypadPlus))
            miniCarteZoom = Mathf.Clamp(miniCarteZoom + 0.25f, 0.75f, 3f);
        if (Input.GetKeyDown(KeyCode.Minus) || Input.GetKeyDown(KeyCode.KeypadMinus))
            miniCarteZoom = Mathf.Clamp(miniCarteZoom - 0.25f, 0.75f, 3f);
        cameraDistance = Mathf.Clamp(cameraDistance - Input.mouseScrollDelta.y * 0.5f, 3f, 18f);
    }

    private void UpdateCamera()
    {
        if (player == null) return;
        RestaurerTransparences();
        if (firstPerson)
        {
            // En premiere personne, aucun morceau du heros ne doit etre rendu :
            // ni bras sur les cotes, ni interieur de la tete en regardant haut.
            RendreJoueurVisible(false);
            gameCamera.transform.position = player.position + Vector3.up * 1.80f;
            gameCamera.transform.rotation = Quaternion.Euler(firstPersonPitch, cameraYaw, 0f);
            MettreAJourVisibiliteAffiches(gameCamera.transform.position);
            return;
        }
        RendreJoueurVisible(true);
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
        MettreAJourVisibiliteAffiches(position);
        RendreObstaclesTranslucides(target, position);
    }

    private void RendreJoueurVisible(bool visible)
    {
        if (player == null) return;
        Renderer[] rendus = player.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < rendus.Length; i++) rendus[i].enabled = visible;
    }

    private void MettreAJourVisibiliteAffiches(Vector3 cameraPosition)
    {
        for (int i = textesFacades.Count - 1; i >= 0; i--)
        {
            FacadeTextState affiche = textesFacades[i];
            if (affiche.Root == null)
            {
                textesFacades.RemoveAt(i);
                continue;
            }
            Renderer rendu = affiche.Root.GetComponent<Renderer>();
            if (rendu == null) continue;
            // Une affiche de facade n'existe visuellement que du cote de sa
            // propre facade : le cube opaque de la maison ne laisse plus son
            // envers apparaitre quand on regarde depuis l'arriere.
            bool visible = Vector3.Dot(cameraPosition - affiche.Position, affiche.DirectionFacade) > 0.02f;
            if (visible)
            {
                Vector3 versAffiche = affiche.Position - cameraPosition;
                float distance = versAffiche.magnitude;
                if (distance > 0.05f && Physics.Raycast(cameraPosition, versAffiche.normalized,
                    out RaycastHit obstruction, distance - 0.02f, ~0, QueryTriggerInteraction.Ignore))
                    visible = false;
            }
            rendu.enabled = visible;
        }
    }

    private Material MateriauFade(Material origine, float alpha)
    {
        Shader shader = Resources.Load<Shader>("LVShaders/LVFade");
        if (shader == null) return origine;
        string nomOrigine = origine != null ? origine.name : "default";
        var fade = new Material(shader) { name = "mat_fade_" + nomOrigine };
        // La transparence doit rester neutre : un toit rouge ou bleu ne doit
        // pas teinter tout l'ecran quand il passe devant le heros.
        Color couleur = Color.white;
        fade.SetColor("_Color", couleur);
        fade.SetFloat("_Alpha", alpha);
        return fade;
    }

    private void RendreTranslucide(Renderer rendu, float alpha)
    {
        if (rendu == null || materiauxOriginaux.ContainsKey(rendu)) return;
        Material[] originaux = rendu.sharedMaterials;
        if (originaux == null || originaux.Length == 0) return;
        var fades = new Material[originaux.Length];
        for (int i = 0; i < originaux.Length; i++) fades[i] = MateriauFade(originaux[i], alpha);
        materiauxOriginaux.Add(rendu, originaux);
        rendu.sharedMaterials = fades;
    }

    private void RendreObstaclesTranslucides(Vector3 cible, Vector3 cameraPosition)
    {
        Vector3 direction = cameraPosition - cible;
        float distance = direction.magnitude;
        if (distance < 0.1f) return;
        RaycastHit[] touches = Physics.RaycastAll(cible, direction.normalized, distance, ~0, QueryTriggerInteraction.Ignore);
        for (int i = 0; i < touches.Length; i++)
        {
            Transform touche = touches[i].collider.transform;
            if (touche == player || touche.IsChildOf(player)) continue;
            // Le sol, la route et la fontaine ne doivent jamais recevoir le
            // materiau fade : seuls les murs et toits des maisons sont vises.
            if (touche.name != "Murs" && !touche.name.StartsWith("Toit_")) continue;
            Renderer rendu = touche.GetComponent<Renderer>();
            if (rendu == null) rendu = touche.GetComponentInParent<Renderer>();
            if (rendu != null) RendreTranslucide(rendu, 0.30f);
        }
    }

    private void RestaurerTransparences()
    {
        foreach (KeyValuePair<Renderer, Material[]> entree in materiauxOriginaux)
        {
            if (entree.Key != null) renduRestaurer(entree.Key, entree.Value);
        }
        materiauxOriginaux.Clear();
    }

    private void renduRestaurer(Renderer rendu, Material[] originaux)
    {
        Material[] actuels = rendu.sharedMaterials;
        rendu.sharedMaterials = originaux;
        if (actuels == null) return;
        for (int i = 0; i < actuels.Length; i++)
        {
            if (actuels[i] != null && actuels[i].name.StartsWith("mat_fade_")) Destroy(actuels[i]);
        }
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
                    enemy.Alive = true;
                    enemy.Hp = enemy.PvMax;
                    enemy.Root.SetActive(true);
                    // Reapparition decalee de 3 m au hasard autour du point de
                    // depart, et jamais dans le village (reference).
                    Vector2 retour = new Vector2(enemy.Home.x, enemy.Home.z)
                        + new Vector2(UnityEngine.Random.Range(-3f, 3f), UnityEngine.Random.Range(-3f, 3f));
                    if (retour.magnitude < VillageRadius + 1f)
                    {
                        if (retour.magnitude < 0.001f) retour = new Vector2(1f, 0f);
                        retour = retour.normalized * (VillageRadius + 1f);
                    }
                    if (ZoneEauInterditeMonstre(retour)) retour = SortirZoneEauMonstre(retour);
                    Vector3 reapparition = new Vector3(retour.x, TerrainHeight(retour.x, retour.y), retour.y);
                    enemy.Root.transform.position = reapparition;
                    enemy.Home = reapparition;
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

            // Securite : une bete sortie de sa zone autorisee est remise sur
            // la berge, puis repart dans une direction terrestre.
            if (ZoneEauInterditeMonstre(new Vector2(position.x, position.z)))
            {
                Vector2 dehors = SortirZoneEauMonstre(new Vector2(position.x, position.z));
                position.x = dehors.x;
                position.z = dehors.y;
                deplacement = DirectionSansEau(new Vector2(position.x, position.z), deplacement);
                enemy.Direction = deplacement;
            }
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
                // Si la prochaine foulée entre dans l'eau, le monstre tourne
                // immédiatement vers la berge. Il ne reste donc pas planté
                // face au fleuve à répéter la même collision.
                deplacement = DirectionSansEau(new Vector2(position.x, position.z), deplacement);
                enemy.Direction = deplacement;
                Vector3 suivant = position + deplacement * speed * dt;
                if (DansVillage(suivant.x, suivant.z))
                {
                    // Village protege : la bete longe la cloture sans entrer.
                    enemy.WanderTimer = 0f;
                    enemy.Direction = Quaternion.Euler(0f, 90f, 0f) * deplacement;
                }
                else if (Mathf.Abs(suivant.x) < WorldSize - 3f && Mathf.Abs(suivant.z) < WorldSize - 3f)
                {
                    // Les monstres ne traversent rien non plus (murs, arbres).
                    Vector2 resolu = ResoudreCollisions(suivant.x, suivant.z, 0.35f, 0f, enemy.Corps);
                    if (ZoneEauInterditeMonstre(resolu))
                    {
                        // Une collision avec un obstacle ne doit jamais pousser
                        // une bete dans l'eau : elle garde sa position et
                        // choisira un autre virage à l'image suivante.
                        enemy.Direction = DirectionSansEau(new Vector2(position.x, position.z), -deplacement);
                    }
                    else
                    {
                        position.x = resolu.x; position.z = resolu.y;
                        Vector3 regard = new Vector3(deplacement.x, 0f, deplacement.z);
                        if (regard.sqrMagnitude > 0.01f)
                            enemy.Root.transform.rotation = Quaternion.LookRotation(regard);
                    }
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

            // Barre de vie : visible des que la bete est blessee, tournee vers
            // la camera, remplie proportionnellement (tailles et formules de la
            // reference : 0,86 m de remplissage qui se retrecit vers la gauche).
            // On voit enfin combien de coups il reste a donner.
            if (enemy.Barre != null && enemy.Remplissage != null)
            {
                if (enemy.Alive && enemy.Hp < enemy.PvMax)
                {
                    enemy.Barre.gameObject.SetActive(true);
                    enemy.Barre.LookAt(gameCamera.transform.position, Vector3.up);
                    float proportion = Mathf.Clamp01(enemy.Hp / (float)enemy.PvMax);
                    enemy.Remplissage.localScale = new Vector3(0.86f * Mathf.Max(proportion, 0.001f), 0.07f, 0.03f);
                    enemy.Remplissage.localPosition = new Vector3(-(1f - proportion) * 0.43f, 0f, 0.02f);
                }
                else
                {
                    enemy.Barre.gameObject.SetActive(false);
                }
            }
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
                    // Il revient AILLEURS (jamais deux fois au meme endroit) :
                    // cailloux dans +/-85 m, pieces dans +/-70 m, hors du
                    // village, de la route et de la riviere.
                    PoserPickup(pickup, PositionRessource(pickup.Coin ? 70f : 85f, 3.0f));
                    pickup.Active = true;
                    pickup.Root.SetActive(true);
                }
                continue;
            }
            // Seules les pieces tournent (2,5 rad/s dans la reference).
            if (pickup.Coin) pickup.Root.transform.Rotate(Vector3.up, dt * 143f, Space.World);
        }
    }

    private void Attack()
    {
        if (attackCooldown > 0 || dead) return;
        attackCooldown = 0.5f;          // meme cadence que la reference
        attackAnimation = 0.3f;
        JouerAnimationHeroine("1H_Melee_Attack", false);
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
        if (playerProtection > 0 || invincibility > 0 || dead) return;
        hp = Mathf.Max(0, hp - amount);
        playerProtection = 0.45f;
        // Le nombre de degats s'affiche au-dessus du heros (en rouge).
        SpawnFloater(player.position + Vector3.up * 1.8f, "-" + amount, new Color(1f, 0.30f, 0.20f));
        if (hp <= 0) Die();
    }

    private void Die()
    {
        dead = true;
        playerVelocity = Vector3.zero;
        playerGrounded = true;
        if (joueurCollider != null) joueurCollider.enabled = false;
        // Le corps se couche sur le cote au sol au lieu de rester debout.
        if (heroBody != null)
        {
            heroBody.localPosition = new Vector3(0f, 0.45f, 0f);
            heroBody.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }
        ShowInfo("Vous êtes mort — appuyez sur " + NomTouche(toucheRenaître) + " pour renaître");
    }

    private void Respawn()
    {
        dead = false;
        hp = MaxHp;
        endurance = 100f;
        underwaterDamageClock = 0f;
        playerInWater = false;
        playerUnderwater = false;
        playerWasUnderwater = false;
        invincibility = 30f;
        playerProtection = 0.45f;
        player.position = new Vector3(0, TerrainHeight(0, 6), 6);
        player.rotation = Quaternion.identity;
        if (joueurCollider != null) joueurCollider.enabled = true;
        if (heroBody != null)
        {
            heroBody.localPosition = Vector3.zero;
            heroBody.localRotation = Quaternion.identity;
        }
        ShowInfo("Vous êtes revenu à la vie — invincibilité 30 s");
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

    private bool TryAttaquerMonstreClique()
    {
        if (gameCamera == null) return false;
        Vector2 souris = Input.mousePosition;
        souris.y = Screen.height - souris.y;
        float meilleureProfondeur = 100000f;
        EnemyState cible = null;
        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyState ennemi = enemies[i];
            if (ennemi == null || !ennemi.Alive || ennemi.Root == null || !ennemi.Root.activeInHierarchy)
                continue;
            Vector3 origine = ennemi.Root.transform.position;
            float largeur = ennemi.Spider ? 0.95f : 0.70f;
            Vector3[] points =
            {
                origine + Vector3.up * 0.05f,
                origine + Vector3.up * (ennemi.Spider ? 0.95f : 1.20f),
                origine + Vector3.left * largeur + Vector3.up * 0.55f,
                origine + Vector3.right * largeur + Vector3.up * 0.55f
            };
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float profondeur = 100000f;
            bool visible = true;
            for (int point = 0; point < points.Length; point++)
            {
                Vector3 ecran = gameCamera.WorldToScreenPoint(points[point]);
                if (ecran.z <= 0f) { visible = false; break; }
                float y = Screen.height - ecran.y;
                minX = Mathf.Min(minX, ecran.x);
                maxX = Mathf.Max(maxX, ecran.x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                profondeur = Mathf.Min(profondeur, ecran.z);
            }
            if (!visible) continue;
            Rect zone = Rect.MinMaxRect(minX - 16f, minY - 16f, maxX + 16f, maxY + 16f);
            if (zone.Contains(souris) && profondeur < meilleureProfondeur)
            {
                meilleureProfondeur = profondeur;
                cible = ennemi;
            }
        }
        return cible != null;
    }

    private string NomAffichePnj(PnjState pnj)
    {
        if (pnj == null || string.IsNullOrEmpty(pnj.Metier)) return "Habitant";
        if (pnj.Metier == "Medecin") return "Médecin";
        if (pnj.Metier == "Forgeron") return "Forgeron";
        if (pnj.Metier == "Marchand") return "Marchand";
        if (pnj.Metier == "Maire") return "Maire";
        return pnj.Metier;
    }

    private bool TryOuvrirConversation()
    {
        if (conversationOpen || gameCamera == null || player == null) return false;
        Vector2 souris = Input.mousePosition;
        souris.y = Screen.height - souris.y;
        float meilleureProfondeur = 100000f;
        PnjState cible = null;
        for (int i = 0; i < pnjs.Count; i++)
        {
            PnjState pnj = pnjs[i];
            if (pnj == null || pnj.Root == null || !pnj.Root.activeInHierarchy) continue;
            Vector3 origine = pnj.Root.transform.position;
            Vector3[] points =
            {
                origine + Vector3.up * 0.02f,
                origine + Vector3.up * 2.35f,
                origine + Vector3.left * 0.72f + Vector3.up * 1.45f,
                origine + Vector3.right * 0.72f + Vector3.up * 1.45f
            };
            float minX = float.MaxValue;
            float maxX = float.MinValue;
            float minY = float.MaxValue;
            float maxY = float.MinValue;
            float profondeur = 100000f;
            bool visible = true;
            for (int point = 0; point < points.Length; point++)
            {
                Vector3 ecran = gameCamera.WorldToScreenPoint(points[point]);
                if (ecran.z <= 0f) { visible = false; break; }
                float y = Screen.height - ecran.y;
                minX = Mathf.Min(minX, ecran.x);
                maxX = Mathf.Max(maxX, ecran.x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                profondeur = Mathf.Min(profondeur, ecran.z);
            }
            if (!visible) continue;
            Rect zone = Rect.MinMaxRect(minX - 18f, minY - 18f, maxX + 18f, maxY + 18f);
            if (zone.Contains(souris) && profondeur < meilleureProfondeur)
            {
                meilleureProfondeur = profondeur;
                cible = pnj;
            }
        }
        if (cible == null) return false;
        conversationPnj = cible;
        conversationOpen = true;
        conversationFocusRequested = true;
        conversationInput = "";
        conversationMessages.Clear();
        conversationMessages.Add(NomAffichePnj(cible) + " : Bonjour");
        return true;
    }

    private void EnvoyerMessageConversation()
    {
        string message = (conversationInput ?? "").Trim();
        if (message.Length == 0) return;
        conversationMessages.Add("Vous : " + message);
        conversationMessages.Add(NomAffichePnj(conversationPnj)
            + " : Je vous écoute. Que puis-je faire pour vous ?");
        conversationInput = "";
    }

    private void FermerConversation()
    {
        conversationOpen = false;
        conversationPnj = null;
        conversationFocusRequested = false;
        conversationInput = "";
        conversationMessages.Clear();
        GUI.FocusControl("");
    }

    private void DessinerConversation()
    {
        float largeur = Mathf.Min(700f, Screen.width - 40f);
        float hauteur = Mathf.Min(340f, Screen.height - 80f);
        Rect cadre = new Rect((Screen.width - largeur) * 0.5f,
            (Screen.height - hauteur) * 0.5f, largeur, hauteur);
        GUI.Box(cadre, "", boxStyle);
        GUI.Label(new Rect(cadre.x + 22f, cadre.y + 16f, largeur - 80f, 30f),
            "CONVERSATION — " + NomAffichePnj(conversationPnj), conversationTitleStyle);
        if (GUI.Button(new Rect(cadre.x + largeur - 48f, cadre.y + 14f, 28f, 26f), "X", buttonStyle))
        {
            FermerConversation();
            return;
        }
        Rect texte = new Rect(cadre.x + 24f, cadre.y + 58f, largeur - 48f, hauteur - 132f);
        GUI.Box(texte, "", boxStyle);
        string contenu = string.Join("\n\n", conversationMessages.ToArray());
        GUI.Label(new Rect(texte.x + 14f, texte.y + 12f, texte.width - 28f, texte.height - 20f),
            contenu, conversationTextStyle);
        GUI.SetNextControlName("ConversationInput");
        conversationInput = GUI.TextField(new Rect(cadre.x + 24f, cadre.y + hauteur - 58f,
            largeur - 142f, 32f), conversationInput);
        if (conversationFocusRequested)
        {
            GUI.FocusControl("ConversationInput");
            conversationFocusRequested = false;
        }
        if (GUI.Button(new Rect(cadre.x + largeur - 108f, cadre.y + hauteur - 58f, 84f, 32f),
            "ENVOYER", buttonStyle))
            EnvoyerMessageConversation();
        if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Return
            && GUI.GetNameOfFocusedControl() == "ConversationInput")
        {
            EnvoyerMessageConversation();
            Event.current.Use();
        }
    }

    private void OnGUI()
    {
        EnsureStyles();
        if (!mondePret)
        {
            DessinerChargement();
            return;
        }

        DessinerCorrectionCouleur();
        if (playerUnderwater)
        {
            GUI.color = new Color(0.04f, 0.30f, 0.46f, 0.24f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(new Rect(Screen.width * 0.5f - 100f, 92f, 200f, 28f),
                endurance > 0.01f ? "SOUS L'EAU" : "REMONTÉE URGENTE", titleStyle);
        }
        // Les barres restent le premier encadre en haut a gauche. La fenetre
        // de quete vient juste SOUS ce cadre, avec une croix pour la fermer.
        GUI.Box(new Rect(20, 18, 270, 124), "", boxStyle);
        float hudY = 29f;
        DessinerBarre(new Rect(32, hudY, 230, 14), hp / (float)MaxHp, "PV");
        DessinerBarre(new Rect(32, hudY + 28f, 230, 14), endurance / 100f, "ENDURANCE");
        DessinerBarre(new Rect(32, hudY + 56f, 230, 14), (xp % (level * 100)) / (float)(level * 100), "EXPERIENCE");
        GUI.Label(new Rect(32, hudY + 82f, 250, 22), "Or : " + coins + "     Cailloux : " + rocks, smallStyle);
        // L'ancienne ligne Energie est retiree : la vie est lue au centre de
        // sa barre et l'endurance reste la barre de course.
        if (questOpen)
        {
            GUI.Box(new Rect(20, 150, 270, 118), "QUÊTE\nPROBLÈME DE RATS\n\nRats : " + ratsKilled + " / 10\nAraignées : " + spidersKilled + " / 5", boxStyle);
            if (GUI.Button(new Rect(262, 154, 24, 24), "X", buttonStyle))
                questOpen = false;
        }
        else if (GUI.Button(new Rect(20, 150, 112, 26), "QUÊTE  +", buttonStyle))
        {
            questOpen = true;
        }
        DessinerMiniCarte();
        for (int i = 0; i < 5; i++)
        {
            Rect slot = new Rect(Screen.width * 0.5f - 135 + i * 55, Screen.height - 68, 48, 48);
            GUI.color = i == selectedSlot ? Color.yellow : Color.white;
            GUI.Box(slot, (i + 1).ToString(), boxStyle); GUI.color = Color.white;
        }

        if (inventoryOpen)
        {
            GUI.Box(new Rect(Screen.width / 2 - 160, Screen.height / 2 - 100, 320, 200), "INVENTAIRE\n\nPotions de soin : " + potions[0] + "\nPotions de vitesse : " + potions[1] + "\nOr : " + coins + "\nCailloux : " + rocks, boxStyle);
        }
        if (optionsOpen) DessinerOptions();
        if (dead) GUI.Box(new Rect(Screen.width / 2 - 180, Screen.height / 2 - 55, 360, 110), "VOUS ÊTES MORT\n\nAppuyez sur " + NomTouche(toucheRenaître) + " pour renaître", boxStyle);
        if (infoTimer > 0) GUI.Label(new Rect(Screen.width / 2 - 150, Screen.height - 128, 300, 35), infoMessage, titleStyle);
        if (GUI.Button(new Rect(Screen.width - 178f, Screen.height - 42f, 164f, 28f),
            "NOTER L'ÉDITION", buttonStyle))
            notationOpen = !notationOpen;
        if (notationOpen) DessinerNotationEdition();
        if (conversationOpen) DessinerConversation();
    }

    private void DessinerNotationEdition()
    {
        Rect cadre = new Rect(Screen.width - 270f, Screen.height - 178f, 256f, 126f);
        GUI.Box(cadre, "NOTER L'ÉDITION", boxStyle);
        GUI.Label(new Rect(cadre.x + 12f, cadre.y + 32f, 232f, 24f),
            "Votre note :", smallStyle);
        for (int i = 0; i < 5; i++)
        {
            if (GUI.Button(new Rect(cadre.x + 12f + i * 46f, cadre.y + 68f, 38f, 30f),
                (i + 1).ToString(), buttonStyle))
            {
                notationOpen = false;
                ShowInfo("Note enregistrée : " + (i + 1) + "/5");
            }
        }
    }

    private void DessinerCorrectionCouleur()
    {
        // Le vrai reglage luminosite/contraste est applique par OnRenderImage.
        // Ce petit voile garantit aussi un rendu doux lorsque le shader de
        // post-traitement est indisponible dans une vieille build.
        if (contrast < 0.92f)
        {
            GUI.color = new Color(0.55f, 0.58f, 0.65f, (1f - contrast) * 0.18f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
        }
        GUI.color = Color.white;
    }

    private void DessinerBarre(Rect rect, float valeur, string texte)
    {
        GUI.color = new Color(0.06f, 0.08f, 0.12f, 0.90f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = texte == "PV" ? new Color(0.90f, 0.16f, 0.12f) : (texte == "ENDURANCE" ? new Color(0.20f, 0.78f, 0.28f) : new Color(0.92f, 0.68f, 0.12f));
        GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(valeur), rect.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        string affichage = texte == "PV" ? hp + "/" + MaxHp : texte;
        GUIStyle style = texte == "PV" ? barreValeurStyle : smallStyle;
        GUI.Label(new Rect(rect.x + 5, rect.y - 2, rect.width - 10, rect.height + 5), affichage, style);
    }

    private void DessinerMiniCarte()
    {
        // La mini-carte garde sa taille d'origine. C'est le monde jouable qui
        // est agrandi, pas l'interface.
        const float taille = 154f;
        Rect carte = new Rect(Screen.width - 190f, 18f, taille, taille);
        if (miniCarteTexture == null || Event.current.type == EventType.Repaint)
            MettreAJourMiniCarte();
        GUI.color = Color.white;
        if (miniCarteTexture != null)
        {
            // La carte tourne avec l'orientation de la camera.
            Vector2 centreEcran = new Vector2(carte.x + taille * 0.5f, carte.y + taille * 0.5f);
            GUIUtility.RotateAroundPivot(-miniCarteOrientation, centreEcran);
            GUI.DrawTexture(carte, miniCarteTexture, ScaleMode.StretchToFill, true);
            GUIUtility.RotateAroundPivot(miniCarteOrientation, centreEcran);
        }
        DessinerPointCardinal(carte, "N", new Vector2(0f, -1f));
        DessinerPointCardinal(carte, "S", new Vector2(0f, 1f));
        DessinerPointCardinal(carte, "W", new Vector2(-1f, 0f));
        DessinerPointCardinal(carte, "E", new Vector2(1f, 0f));
        // Les commandes sont sorties de la texture, sur sa droite : elles ne
        // recouvrent plus la carte et restent alignees sur son cote nord.
        float commandesX = carte.x + carte.width + 6f;
        if (GUI.Button(new Rect(commandesX, carte.y + 5f, 24f, 24f), "+", miniCarteButtonStyle))
            miniCarteZoom = Mathf.Clamp(miniCarteZoom + 0.25f, 0.75f, 3f);
        if (GUI.Button(new Rect(commandesX, carte.y + 32f, 24f, 24f), "-", miniCarteButtonStyle))
            miniCarteZoom = Mathf.Clamp(miniCarteZoom - 0.25f, 0.75f, 3f);
        GUI.Label(new Rect(commandesX - 3f, carte.y + 58f, 30f, 20f), "x" + miniCarteZoom.ToString("0.00"), miniCarteTextStyle);
        GUI.color = Color.white;
    }

    private void DessinerPointCardinal(Rect carte, string texte, Vector2 directionEcran)
    {
        float radians = -miniCarteOrientation * Mathf.Deg2Rad;
        float x = directionEcran.x * Mathf.Cos(radians) - directionEcran.y * Mathf.Sin(radians);
        float y = directionEcran.x * Mathf.Sin(radians) + directionEcran.y * Mathf.Cos(radians);
        float centreX = carte.x + carte.width * 0.5f;
        float centreY = carte.y + carte.height * 0.5f;
        GUI.Label(new Rect(centreX + x * 69f - 8f, centreY + y * 69f - 11f, 18f, 22f), texte, miniCarteTextStyle);
    }

    private void MettreAJourMiniCarte()
    {
        const int taille = 128;
        const int centre = 64;
        const int rayon = 60;
        if (miniCarteTexture == null)
        {
            miniCarteTexture = new Texture2D(taille, taille, TextureFormat.RGBA32, false);
            miniCarteTexture.name = "MiniCarteRonde";
            miniCarteTexture.filterMode = FilterMode.Bilinear;
            miniCarteTexture.wrapMode = TextureWrapMode.Clamp;
        }

        Color fond = new Color(0.10f, 0.20f, 0.12f, 1f);
        Color bord = new Color(0.72f, 0.58f, 0.30f, 1f);
        for (int y = 0; y < taille; y++)
        for (int x = 0; x < taille; x++)
        {
            float distance = Vector2.Distance(new Vector2(x, y), new Vector2(centre, centre));
            if (distance > rayon) miniCarteTexture.SetPixel(x, y, Color.clear);
            else if (distance > rayon - 2f) miniCarteTexture.SetPixel(x, y, bord);
            else miniCarteTexture.SetPixel(x, y, fond);
        }

        Vector2 centreMonde = player == null ? Vector2.zero : new Vector2(player.position.x, player.position.z);
        // La carte suit la camera, pas la rotation automatique du joueur.
        miniCarteOrientation = cameraYaw;
        float rayonMonde = WorldSize / miniCarteZoom;
        for (int i = 0; i < 120; i++)
        {
            float t0 = i / 120f;
            float t1 = (i + 1) / 120f;
            DessinerLigneMiniCarte(CourbeRoute(t0), CourbeRoute(t1), centreMonde, rayonMonde,
                new Color(0.72f, 0.58f, 0.30f, 1f), 2);
        }
        for (int i = 0; i < RivierePrincipale.Length - 1; i++)
            DessinerLigneMiniCarte(RivierePrincipale[i], RivierePrincipale[i + 1], centreMonde, rayonMonde,
                new Color(0.18f, 0.66f, 0.88f, 1f), 3);
        // Limite de l'enceinte du village.
        const int cerclePoints = 96;
        for (int i = 0; i < cerclePoints; i++)
        {
            float a0 = i * Mathf.PI * 2f / cerclePoints;
            float a1 = (i + 1) * Mathf.PI * 2f / cerclePoints;
            DessinerLigneMiniCarte(
                new Vector2(Mathf.Cos(a0) * VillageRadius, Mathf.Sin(a0) * VillageRadius),
                new Vector2(Mathf.Cos(a1) * VillageRadius, Mathf.Sin(a1) * VillageRadius),
                centreMonde, rayonMonde, new Color(0.48f, 0.70f, 0.35f, 1f), 1);
        }
        for (int i = 0; i < batiments.Count; i++)
        {
            Batiment batiment = batiments[i];
            Vector2 a = new Vector2(batiment.X - batiment.Largeur * 0.5f, batiment.Z - batiment.Profondeur * 0.5f);
            Vector2 b = new Vector2(batiment.X + batiment.Largeur * 0.5f, batiment.Z - batiment.Profondeur * 0.5f);
            Vector2 c = new Vector2(batiment.X + batiment.Largeur * 0.5f, batiment.Z + batiment.Profondeur * 0.5f);
            Vector2 d = new Vector2(batiment.X - batiment.Largeur * 0.5f, batiment.Z + batiment.Profondeur * 0.5f);
            Color couleurMaison = new Color(0.66f, 0.48f, 0.30f, 1f);
            DessinerLigneMiniCarte(a, b, centreMonde, rayonMonde, couleurMaison, 1);
            DessinerLigneMiniCarte(b, c, centreMonde, rayonMonde, couleurMaison, 1);
            DessinerLigneMiniCarte(c, d, centreMonde, rayonMonde, couleurMaison, 1);
            DessinerLigneMiniCarte(d, a, centreMonde, rayonMonde, couleurMaison, 1);
        }
        if (player != null)
            DessinerPointMiniCarte(centreMonde, centreMonde, rayonMonde, Color.white, 3);
        miniCarteTexture.Apply(false, false);
    }

    private void DessinerLigneMiniCarte(Vector2 debut, Vector2 fin, Vector2 centreMonde,
        float rayonMonde, Color couleur, int epaisseur)
    {
        int pas = Mathf.Max(1, Mathf.CeilToInt(Vector2.Distance(debut, fin) * 2f));
        for (int i = 0; i <= pas; i++)
        {
            float t = i / (float)pas;
            DessinerPointMiniCarte(Vector2.Lerp(debut, fin, t), centreMonde, rayonMonde,
                couleur, epaisseur);
        }
    }

    private void DessinerPointMiniCarte(Vector2 monde, Vector2 centreMonde, float rayonMonde,
        Color couleur, int epaisseur)
    {
        const int centre = 64;
        const int rayon = 60;
        int x = centre + Mathf.RoundToInt((monde.x - centreMonde.x) / rayonMonde * rayon);
        int y = centre + Mathf.RoundToInt((monde.y - centreMonde.y) / rayonMonde * rayon);
        for (int oy = -epaisseur; oy <= epaisseur; oy++)
        for (int ox = -epaisseur; ox <= epaisseur; ox++)
        {
            int px = x + ox;
            int py = y + oy;
            if (px < 0 || px >= 128 || py < 0 || py >= 128) continue;
            if (Vector2.Distance(new Vector2(px, py), new Vector2(centre, centre)) <= rayon - 1)
                miniCarteTexture.SetPixel(px, py, couleur);
        }
    }

    private string NomTouche(int code)
    {
        KeyCode touche = (KeyCode)code;
        if (touche == KeyCode.Mouse0) return "Clic gauche";
        return touche.ToString();
    }

    private void DemanderTouche(string id, string libelle, ref int valeur, float colonne, float y)
    {
        GUI.Label(new Rect(colonne, y, 102, 24), libelle, smallStyle);
        string texte = toucheEnCours == id ? "Appuyez..." : NomTouche(valeur);
        if (GUI.Button(new Rect(colonne + 104, y - 2, 94, 25), texte, buttonStyle))
            toucheEnCours = id;
        if (toucheEnCours == id && Event.current.type == EventType.KeyDown)
        {
            if (Event.current.keyCode == KeyCode.Escape) toucheEnCours = "";
            else if (Event.current.keyCode != KeyCode.None)
            {
                valeur = (int)Event.current.keyCode;
                toucheEnCours = "";
                SauverConfiguration();
                Event.current.Use();
            }
        }
    }

    private void DessinerOptions()
    {
        Rect cadre = new Rect(Screen.width / 2 - 310, Screen.height / 2 - 235, 620, 470);
        GUI.Box(cadre, "OPTIONS", boxStyle);
        GUI.color = ongletOptions == 0 ? new Color(0.18f, 0.40f, 0.62f) : new Color(0.12f, 0.16f, 0.25f);
        GUI.DrawTexture(new Rect(cadre.x + 18, cadre.y + 42, 145, 42), Texture2D.whiteTexture);
        GUI.color = ongletOptions == 1 ? new Color(0.42f, 0.25f, 0.58f) : new Color(0.12f, 0.16f, 0.25f);
        GUI.DrawTexture(new Rect(cadre.x + 18, cadre.y + 88, 145, 42), Texture2D.whiteTexture);
        GUI.color = Color.white;
        if (GUI.Button(new Rect(cadre.x + 18, cadre.y + 42, 145, 42), "GRAPHIQUE", tabActifStyle)) ongletOptions = 0;
        if (GUI.Button(new Rect(cadre.x + 18, cadre.y + 88, 145, 42), "CONTROLE", tabActifStyle)) ongletOptions = 1;
        GUI.BeginGroup(new Rect(cadre.x + 180, cadre.y + 42, 420, 400));
        if (ongletOptions == 0) DessinerGraphique(); else DessinerControle();
        GUI.EndGroup();
        if (GUI.Button(new Rect(cadre.x + 18, cadre.y + 410, 145, 35), "FERMER", buttonStyle))
        {
            optionsOpen = false;
            toucheEnCours = "";
            SauverConfiguration();
        }
    }

    private void DessinerGraphique()
    {
        GUI.Label(new Rect(0, 0, 380, 25), "REGLAGES VISUELS", labelStyle);
        GUI.Label(new Rect(0, 42, 130, 24), "Luminosite", smallStyle);
        brightness = GUI.HorizontalSlider(new Rect(135, 51, 220, 18), brightness, 0f, 1f);
        GUI.Label(new Rect(360, 42, 55, 24), Mathf.RoundToInt(brightness * 100) + "%", smallStyle);
        GUI.Label(new Rect(0, 78, 130, 24), "Contraste", smallStyle);
        contrast = GUI.HorizontalSlider(new Rect(135, 87, 220, 18), contrast, 0.5f, 1.5f);
        GUI.Label(new Rect(360, 78, 55, 24), Mathf.RoundToInt(contrast * 100) + "%", smallStyle);
        GUI.Label(new Rect(0, 114, 160, 24), "Resolution", smallStyle);
        if (GUI.Button(new Rect(135, 112, 220, 28), resolutions[resolutionIndex].x + " x " + resolutions[resolutionIndex].y, buttonStyle)) menuResolutions = !menuResolutions;
        if (menuResolutions)
        {
            int debut = Mathf.Max(0, resolutionIndex - 3);
            for (int i = debut; i < Mathf.Min(resolutions.Length, debut + 6); i++)
            {
                if (GUI.Button(new Rect(135, 142 + (i - debut) * 27, 220, 25), resolutions[i].x + " x " + resolutions[i].y, buttonStyle))
                {
                    resolutionIndex = i;
                    menuResolutions = false;
                    Screen.SetResolution(resolutions[i].x, resolutions[i].y, false);
                    SauverConfiguration();
                }
            }
        }
    }

    private void DessinerControle()
    {
        GUI.Label(new Rect(0, 0, 380, 25), "CLAVIER " + (clavierAzerty ? "AZERTY" : "QWERTY"), labelStyle);
        GUI.Label(new Rect(0, 30, 190, 24), "Inverser axe horizontal", smallStyle);
        inverserAxeX = GUI.Toggle(new Rect(205, 30, 24, 24), inverserAxeX, "");
        GUI.Label(new Rect(245, 30, 150, 24), inverserAxeX ? "Oui" : "Non", smallStyle);
        GUI.Label(new Rect(0, 57, 190, 24), "Inverser axe vertical", smallStyle);
        inverserAxeY = GUI.Toggle(new Rect(205, 57, 24, 24), inverserAxeY, "");
        GUI.Label(new Rect(245, 57, 150, 24), inverserAxeY ? "Oui" : "Non", smallStyle);
        GUI.Label(new Rect(0, 84, 125, 24), "Vitesse camera", smallStyle);
        cameraSensitivity = GUI.HorizontalSlider(new Rect(135, 92, 220, 18), cameraSensitivity, 0.5f, 10f);
        GUI.Label(new Rect(360, 84, 55, 24), cameraSensitivity.ToString("0.0"), smallStyle);
        GUI.Label(new Rect(0, 112, 390, 24), "Clique une touche, puis appuie sur la nouvelle touche.", smallStyle);
        DemanderTouche("avant", "Avancer", ref toucheAvant, 0, 140);
        DemanderTouche("arriere", "Reculer", ref toucheArriere, 0, 169);
        DemanderTouche("gauche", "Gauche", ref toucheGauche, 0, 198);
        DemanderTouche("droite", "Droite", ref toucheDroite, 0, 227);
        DemanderTouche("saut", "Sauter", ref toucheSaut, 0, 256);
        DemanderTouche("courir", "Courir", ref toucheCourir, 0, 285);
        DemanderTouche("ramasser", "Ramasser", ref toucheRamasser, 205, 140);
        DemanderTouche("attaque", "Attaquer", ref toucheAttaque, 205, 169);
        DemanderTouche("inventaire", "Inventaire", ref toucheInventaire, 205, 198);
        DemanderTouche("options", "Options", ref toucheOptions, 205, 227);
        DemanderTouche("quete", "Quete", ref toucheQuete, 205, 256);
        DemanderTouche("camera", "Camera", ref toucheCamera, 205, 285);
        DemanderTouche("renaitre", "Renaitre", ref toucheRenaître, 205, 314);
    }

    private void EnsureStyles()
    {
        if (labelStyle != null) return;
        titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 22, fontStyle = FontStyle.Bold, normal = { textColor = new Color(1f, 0.78f, 0.1f) } };
        labelStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, normal = { textColor = Color.white } };
        smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 12, normal = { textColor = new Color(0.82f, 0.87f, 0.92f) } };
        boxStyle = new GUIStyle(GUI.skin.box) { fontSize = 14, alignment = TextAnchor.MiddleCenter, normal = { textColor = Color.white } };
        buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 12, alignment = TextAnchor.MiddleCenter };
        miniCarteTextStyle = new GUIStyle(smallStyle);
        miniCarteTextStyle.normal.textColor = Color.white;
        miniCarteTextStyle.hover.textColor = Color.white;
        miniCarteTextStyle.alignment = TextAnchor.MiddleCenter;
        miniCarteButtonStyle = new GUIStyle(buttonStyle);
        miniCarteButtonStyle.normal.textColor = Color.white;
        miniCarteButtonStyle.hover.textColor = Color.white;
        miniCarteButtonStyle.active.textColor = Color.white;
        miniCarteButtonStyle.focused.textColor = Color.white;
        barreValeurStyle = new GUIStyle(smallStyle) { alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
        conversationTitleStyle = new GUIStyle(titleStyle)
        {
            fontSize = 18, alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(1f, 0.78f, 0.10f) }
        };
        conversationTextStyle = new GUIStyle(smallStyle)
        {
            fontSize = 15, alignment = TextAnchor.UpperLeft, wordWrap = true,
            normal = { textColor = Color.white }
        };
        tabStyle = new GUIStyle(buttonStyle) { normal = { textColor = Color.white } };
        tabActifStyle = new GUIStyle(buttonStyle) { fontStyle = FontStyle.Bold, normal = { textColor = Color.white } };
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

    private bool EstClavierAzerty()
    {
        string langue = Application.systemLanguage.ToString();
        string culture = CultureInfo.CurrentCulture.Name;
        return langue.IndexOf("French", StringComparison.OrdinalIgnoreCase) >= 0
            || culture.StartsWith("fr", StringComparison.OrdinalIgnoreCase);
    }

    private void AppliquerConfiguration(Configuration config)
    {
        brightness = Mathf.Clamp01(config.brightness);
        contrast = Mathf.Clamp(config.contrast, 0.5f, 1.5f);
        cameraSensitivity = Mathf.Clamp(config.cameraSensitivity, 0.5f, 10f);
        clavierAzerty = config.azerty;
        inverserAxeX = config.invertX;
        inverserAxeY = config.invertY;
        resolutionIndex = Mathf.Clamp(config.resolution, 0, resolutions.Length - 1);
        toucheAvant = config.avant; toucheArriere = config.arriere;
        toucheGauche = config.gauche; toucheDroite = config.droite;
        toucheSaut = config.saut; toucheCourir = config.courir;
        toucheRamasser = config.ramasser; toucheAttaque = config.attaque;
        toucheInventaire = config.inventaire; toucheOptions = config.options;
        toucheQuete = config.quete; toucheCamera = config.camera;
        toucheRenaître = config.renaitre;
    }

    private Configuration LireConfiguration()
    {
        try
        {
            if (File.Exists(CheminConfiguration))
            {
                Configuration config = JsonUtility.FromJson<Configuration>(File.ReadAllText(CheminConfiguration));
                if (config != null) return config;
            }
        }
        catch (Exception erreur) { Debug.LogWarning("LibreVies : configuration illisible : " + erreur.Message); }
        var neuve = new Configuration { azerty = EstClavierAzerty() };
        if (!neuve.azerty)
        {
            neuve.avant = (int)KeyCode.W;
            neuve.gauche = (int)KeyCode.A;
        }
        return neuve;
    }

    private void LoadOptions()
    {
        Configuration config = LireConfiguration();
        AppliquerConfiguration(config);
        // Compatibilite avec les builds qui avaient seulement PlayerPrefs.
        if (!File.Exists(CheminConfiguration))
        {
            brightness = PlayerPrefs.GetFloat("brightness", brightness);
            contrast = PlayerPrefs.GetFloat("contrast", contrast);
            cameraSensitivity = PlayerPrefs.GetFloat("cameraSensitivity", cameraSensitivity);
        }
        Vector2Int resolution = resolutions[resolutionIndex];
        Screen.SetResolution(resolution.x, resolution.y, false);
    }

    private void SauverConfiguration()
    {
        try
        {
            var config = new Configuration
            {
                brightness = brightness, contrast = contrast, cameraSensitivity = cameraSensitivity,
                invertX = inverserAxeX, invertY = inverserAxeY, azerty = clavierAzerty,
                resolution = resolutionIndex, avant = toucheAvant, arriere = toucheArriere,
                gauche = toucheGauche, droite = toucheDroite, saut = toucheSaut,
                courir = toucheCourir, ramasser = toucheRamasser, attaque = toucheAttaque,
                inventaire = toucheInventaire, options = toucheOptions, quete = toucheQuete,
                camera = toucheCamera, renaitre = toucheRenaître
            };
            string temporaire = CheminConfiguration + ".tmp";
            File.WriteAllText(temporaire, JsonUtility.ToJson(config, true));
            File.Copy(temporaire, CheminConfiguration, true);
            File.Delete(temporaire);
        }
        catch (Exception erreur) { Debug.LogWarning("LibreVies : sauvegarde configuration impossible : " + erreur.Message); }
        PlayerPrefs.SetFloat("brightness", brightness);
        PlayerPrefs.SetFloat("contrast", contrast);
        PlayerPrefs.SetFloat("cameraSensitivity", cameraSensitivity);
        PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        SauverConfiguration();
    }

    private Material screenAdjustMaterial;

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (screenAdjustMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("LVShaders/LVScreenAdjust");
            if (shader != null) screenAdjustMaterial = new Material(shader);
        }
        if (screenAdjustMaterial == null)
        {
            Graphics.Blit(source, destination);
            return;
        }
        screenAdjustMaterial.SetFloat("_Brightness", brightness - 0.30f);
        screenAdjustMaterial.SetFloat("_Contrast", contrast);
        Graphics.Blit(source, destination, screenAdjustMaterial);
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
        floaters.Add(new EffetTexte { Root = objet, Age = 0f, Origine = position });
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
            // Les degats MONTENT puis REDESCENDENT un peu (arc), au lieu de
            // monter en ligne droite : le coup se lit tout de suite, comme un
            // chiffre qui jaillit de la bete puis retombe. 1 s de vie.
            float t = floater.Age;
            floater.Root.transform.position = floater.Origine + Vector3.up * (1.9f * t - 1.7f * t * t);
            if (gameCamera != null)
            {
                // TextMesh est oriente vers sa face avant, sinon la camera
                // voit l'envers et lit 25 comme 52 ou -6 comme 6-.
                Vector3 versCamera = gameCamera.transform.position - floater.Root.transform.position;
                if (versCamera.sqrMagnitude > 0.001f)
                    // TextMesh a sa face lisible vers son axe -Z : on tourne
                    // donc son envers vers la camera pour eviter l'effet miroir.
                    floater.Root.transform.rotation = Quaternion.LookRotation(
                        versCamera.normalized, Vector3.up) * Quaternion.Euler(0f, 180f, 0f);
            }
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
            // 'progression' et non 'avancement' : ce dernier est le nom du
            // champ qui suit le chargement du monde (outil de controle).
            float progression = effet.Age / 0.25f;
            if (progression >= 1f)
            {
                Destroy(effet.Root);
                etincelles.RemoveAt(i);
                continue;
            }
            effet.Root.transform.localScale = Vector3.one * (0.6f + progression * 1.2f);
            effet.Root.SetActive(((int)(effet.Age * 30f)) % 2 == 0);
        }
    }
}
