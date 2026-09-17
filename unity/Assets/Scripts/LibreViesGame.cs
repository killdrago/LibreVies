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
        gameCamera.fieldOfView = 65f;
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
        // Runtime-generated materials are not referenced by an asset. Keep a
        // fallback chain so shader stripping cannot abort Awake in a player.
        Shader shader = Shader.Find("Standard");
        if (shader == null) shader = Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning("LibreVies : aucun shader intégré disponible pour " + name);
            materials.Add(null);
            return null;
        }
        var material = new Material(shader) { name = name };
        material.color = color;
        if (emission && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", color * 0.7f);
        }
        materials.Add(material);
        return material;
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
        RenderSettings.ambientSkyColor = new Color(0.40f, 0.52f, 0.72f);
        RenderSettings.ambientEquatorColor = new Color(0.55f, 0.55f, 0.46f);
        RenderSettings.ambientGroundColor = new Color(0.15f, 0.13f, 0.10f);
        RenderSettings.fog = true;
        RenderSettings.fogColor = new Color(0.56f, 0.69f, 0.80f);
        RenderSettings.fogDensity = 0.003f;
        var sunObject = new GameObject("Soleil");
        var sun = sunObject.AddComponent<Light>();
        sun.type = LightType.Directional;
        sun.intensity = 1.15f;
        sun.color = new Color(1f, 0.88f, 0.70f);
        sun.shadows = LightShadows.Soft;
        sunObject.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
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
        var points = new[]
        {
            new Vector3(0, 0, 30), new Vector3(3, 0, 18), new Vector3(-2, 0, 6),
            new Vector3(1, 0, -8), new Vector3(4, 0, -20), new Vector3(-1, 0, -34),
            new Vector3(1, 0, -48), new Vector3(0, 0, -67)
        };
        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector3 a = points[i];
            Vector3 b = points[i + 1];
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
        // Bâtiments supplémentaires du village de départ Godot.
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
    }

    private void CreateFence()
    {
        const int posts = 64;
        for (int i = 0; i < posts; i++)
        {
            float angle = i * Mathf.PI * 2f / posts;
            float x = Mathf.Cos(angle) * VillageRadius;
            float z = Mathf.Sin(angle) * VillageRadius;
            if (Mathf.Abs(x) < 4f && z > 22f) continue;
            float y = TerrainHeight(x, z);
            Primitive(PrimitiveType.Cylinder, new Vector3(x, y + 0.65f, z), new Vector3(0.14f, 0.65f, 0.14f), "Wood", null, "Cloture");
        }
        for (int i = 0; i < posts; i++)
        {
            float angle = (i + 0.5f) * Mathf.PI * 2f / posts;
            float x = Mathf.Cos(angle) * VillageRadius;
            float z = Mathf.Sin(angle) * VillageRadius;
            if (Mathf.Abs(x) < 4f && z > 22f) continue;
            float y = TerrainHeight(x, z) + 0.85f;
            var rail = Box(new Vector3(x, y, z), new Vector3(0.12f, 0.14f, 2.8f), "Wood", null, "Traverse");
            rail.transform.rotation = Quaternion.Euler(0, -angle * Mathf.Rad2Deg, 0);
        }
    }

    private void CreateGuard(Vector3 position)
    {
        var root = new GameObject("Garde").transform;
        root.position = new Vector3(position.x, TerrainHeight(position.x, position.z), position.z);
        Primitive(PrimitiveType.Capsule, new Vector3(0, 1.1f, 0), new Vector3(0.5f, 1.1f, 0.5f), "Player", root, "Corps");
        Box(new Vector3(0.55f, 1.5f, 0), new Vector3(0.10f, 2.2f, 0.10f), "Stone", root, "Hallebarde", false, Quaternion.Euler(0, 0, -8));
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
        // Même point de départ que la scène Godot : la caméra voit le village.
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
            Legs = new Transform[spider ? 8 : 0]
        };
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
            player.position += direction * speed * dt;
            player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(direction), dt * 12f);
            walkClock += dt * (Input.GetKey(KeyCode.LeftShift) ? 14f : 9f);
        }
        else walkClock = 0;

        if (Input.GetKeyDown(KeyCode.Space) && playerGrounded)
        {
            playerVelocity.y = 8f;
            playerGrounded = false;
        }
        playerVelocity.y -= 20f * dt;
        player.position += Vector3.up * playerVelocity.y * dt;
        float ground = TerrainHeight(player.position.x, player.position.z);
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
            cameraYaw += Input.GetAxis("Mouse X") * 0.3f;
            cameraPitch = Mathf.Clamp(cameraPitch - Input.GetAxis("Mouse Y") * 0.3f, 5f, 70f);
        }
        cameraDistance = Mathf.Clamp(cameraDistance - Input.mouseScrollDelta.y * 0.5f, 3f, 18f);
    }

    private void UpdateCamera()
    {
        if (player == null) return;
        if (firstPerson)
        {
            gameCamera.transform.position = player.position + Vector3.up * 1.55f;
            gameCamera.transform.rotation = player.rotation;
            return;
        }
        Quaternion orbit = Quaternion.Euler(cameraPitch, cameraYaw, 0);
        Vector3 target = player.position + Vector3.up * 1.1f;
        // Caméra placée au nord comme dans la scène Godot d'origine.
        gameCamera.transform.position = target + orbit * (Vector3.forward * cameraDistance);
        gameCamera.transform.LookAt(target);
    }

    private void UpdateEnemies(float dt)
    {
        foreach (EnemyState enemy in enemies)
        {
            if (!enemy.Alive)
            {
                if (Time.time >= enemy.RespawnAt)
                {
                    enemy.Alive = true; enemy.Hp = MaxHp; enemy.Root.SetActive(true);
                    enemy.Root.transform.position = enemy.Home;
                }
                continue;
            }
            if (enemy.AttackCooldown > 0) enemy.AttackCooldown -= dt;
            Vector3 position = enemy.Root.transform.position;
            float distance = Vector3.Distance(position, player.position);
            bool outsideVillage = new Vector2(position.x, position.z).magnitude > VillageRadius + 1f;
            if (!outsideVillage)
            {
                Vector2 radial = new Vector2(position.x, position.z).normalized * (VillageRadius + 2f);
                position.x = radial.x; position.z = radial.y;
            }
            if (distance < 13f && outsideVillage)
            {
                Vector3 direction = player.position - position; direction.y = 0;
                if (direction.sqrMagnitude > 0.1f)
                {
                    direction.Normalize();
                    position += direction * dt * (enemy.Spider ? 2.0f : 1.6f);
                    enemy.Root.transform.rotation = Quaternion.LookRotation(direction);
                }
                if (distance < 1.8f && enemy.AttackCooldown <= 0)
                {
                    enemy.AttackCooldown = 0.8f;
                    DamagePlayer(8);
                }
            }
            enemy.Root.transform.position = new Vector3(position.x, TerrainHeight(position.x, position.z), position.z);
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
        attackCooldown = 0.55f;
        attackAnimation = 0.3f;
        EnemyState best = null;
        float bestDistance = 3.1f;
        Vector3 forward = player.forward; forward.y = 0;
        foreach (EnemyState enemy in enemies)
        {
            if (!enemy.Alive) continue;
            Vector3 delta = enemy.Root.transform.position - player.position; delta.y = 0;
            float distance = delta.magnitude;
            if (distance < bestDistance && Vector3.Dot(forward.normalized, delta.normalized) > 0.1f)
            {
                best = enemy; bestDistance = distance;
            }
        }
        if (best == null) { ShowInfo("Aucune cible à portée"); return; }
        best.Hp -= HammerDamage;
        ShowInfo("-25");
        if (best.Hp <= 0)
        {
            best.Alive = false; best.RespawnAt = Time.time + 10f; best.Root.SetActive(false);
            if (best.Spider) spidersKilled++; else ratsKilled++;
            GainXp(best.Spider ? 35 : 20);
            coins += best.Spider ? 4 : 2;
        }
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
        foreach (PickupState pickup in pickups)
        {
            if (!pickup.Active || Vector3.Distance(player.position, pickup.Root.transform.position) > 2.2f) continue;
            pickup.Active = false; pickup.RespawnAt = Time.time + 8f; pickup.Root.SetActive(false);
            if (pickup.Coin) { coins++; ShowInfo("Pièce d'or ramassée"); }
            else { rocks++; ShowInfo("Caillou ramassé"); }
            return;
        }
        ShowInfo("Rien à ramasser ici");
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
            GUI.Box(new Rect(Screen.width / 2 - 180, Screen.height / 2 - 120, 360, 240), "OPTIONS\n\nLuminosité : " + Mathf.RoundToInt(brightness * 100) + "\nContraste : " + Mathf.RoundToInt(contrast * 100) + "\n\nFermer : O", boxStyle);
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
    }

    private void OnApplicationQuit()
    {
        PlayerPrefs.SetFloat("brightness", brightness);
        PlayerPrefs.SetFloat("contrast", contrast);
        PlayerPrefs.Save();
    }

    private void CreateEffectsPlaceholder() { }
    private void UpdateEffects(float dt) { }
}
