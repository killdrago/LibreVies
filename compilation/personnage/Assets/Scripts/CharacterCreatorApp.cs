using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

// LibreVies Character Creator
//
// The viewer deliberately reads the bundled MakeHuman CC0 mesh and targets at
// runtime.  It is not a MakeHuman plug-in: after the Windows build is made,
// the executable contains this mesh, the target data and the game skeleton.
// There is no Python, Blender, MakeHuman or MPFB dependency for the player.
public sealed class CharacterCreatorApp : MonoBehaviour
{
    private CharacterPreset preset;
    private HumanAvatar avatar;
    private Camera previewCamera;
    private Vector2 scroll;
    private string status = "Base humaine CC0 chargee";
    private bool showTechnical;
    private float cameraYaw;
    private bool dragging;
    private Vector2 lastMouse;

    private static readonly Color Background = new Color(0.035f, 0.047f, 0.08f);
    private static readonly Color Panel = new Color(0.075f, 0.09f, 0.145f);
    private static readonly Color Panel2 = new Color(0.105f, 0.125f, 0.195f);
    private static readonly Color Gold = new Color(0.94f, 0.69f, 0.19f);
    private static readonly Color Cyan = new Color(0.22f, 0.74f, 0.82f);

    private GUIStyle titleStyle;
    private GUIStyle subtitleStyle;
    private GUIStyle sectionStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle buttonStyle;
    private GUIStyle selectedButtonStyle;
    private GUIStyle helpStyle;

    private void Start()
    {
        Application.targetFrameRate = 60;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        preset = CharacterPreset.Default();
        BuildScene();
        avatar = new HumanAvatar();
        avatar.Build(preset);
        ConfigureStyles();
    }

    private void BuildScene()
    {
        previewCamera = Camera.main;
        if (previewCamera == null)
        {
            var cameraObject = new GameObject("Camera personnage");
            previewCamera = cameraObject.AddComponent<Camera>();
            cameraObject.tag = "MainCamera";
        }
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        previewCamera.backgroundColor = Background;
        previewCamera.fieldOfView = 30f;
        previewCamera.transform.position = new Vector3(0f, 1.22f, -6.5f);
        previewCamera.transform.LookAt(new Vector3(0f, 1.15f, 0f));

        var key = new GameObject("Lumiere principale");
        var light = key.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.88f, 0.75f);
        key.transform.rotation = Quaternion.Euler(30f, -28f, 0f);

        var fill = new GameObject("Lumiere de remplissage");
        var fillLight = fill.AddComponent<Light>();
        fillLight.type = LightType.Point;
        fillLight.range = 8f;
        fillLight.intensity = 1.4f;
        fillLight.color = new Color(0.45f, 0.66f, 1f);
        fill.transform.position = new Vector3(-2.3f, 2.2f, -2.5f);

        // The floor is only presentation geometry. The character itself is a
        // genuine continuous human mesh, never a collection of primitives.
        var floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = "Sol de presentation";
        floor.transform.position = new Vector3(0f, -0.015f, 0f);
        floor.transform.localScale = Vector3.one * 4f;
        var floorMaterial = NewMaterial(new Color(0.055f, 0.075f, 0.11f), 0.15f, 0.1f);
        floor.GetComponent<Renderer>().sharedMaterial = floorMaterial;
    }

    private void ConfigureStyles()
    {
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 26,
            fontStyle = FontStyle.Bold,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = Color.white }
        };
        subtitleStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            alignment = TextAnchor.MiddleLeft,
            normal = { textColor = new Color(0.62f, 0.72f, 0.83f) }
        };
        sectionStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Gold },
            padding = new RectOffset(0, 0, 10, 3)
        };
        labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 12,
            normal = { textColor = new Color(0.88f, 0.91f, 0.96f) }
        };
        valueStyle = new GUIStyle(labelStyle)
        {
            alignment = TextAnchor.MiddleRight,
            normal = { textColor = Cyan }
        };
        buttonStyle = new GUIStyle(GUI.skin.button)
        {
            fontSize = 12,
            normal = { textColor = Color.white, background = MakeTexture(1, 1, Panel2) },
            hover = { textColor = Color.white, background = MakeTexture(1, 1, new Color(0.15f, 0.2f, 0.3f)) },
            active = { textColor = Color.white, background = MakeTexture(1, 1, Gold) },
            padding = new RectOffset(8, 8, 7, 7)
        };
        selectedButtonStyle = new GUIStyle(buttonStyle)
        {
            normal = { textColor = Background, background = MakeTexture(1, 1, Gold) }
        };
        helpStyle = new GUIStyle(labelStyle)
        {
            wordWrap = true,
            fontSize = 11,
            normal = { textColor = new Color(0.63f, 0.7f, 0.8f) }
        };
    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0) && Input.mousePosition.x > 360f)
        {
            dragging = true;
            lastMouse = Input.mousePosition;
        }
        if (Input.GetMouseButtonUp(0)) dragging = false;
        if (dragging && avatar != null)
        {
            Vector2 now = Input.mousePosition;
            cameraYaw += (now.x - lastMouse.x) * 0.35f;
            lastMouse = now;
            avatar.SetYaw(cameraYaw);
        }
    }

    private void OnGUI()
    {
        if (titleStyle == null) return;
        DrawTopBar();
        DrawControls();
        DrawPreviewHint();
    }

    private void DrawTopBar()
    {
        GUI.color = Panel;
        GUI.DrawTexture(new Rect(0, 0, Screen.width, 74), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(24, 12, 520, 34), "LIBREVIES  /  PERSONNAGE", titleStyle);
        GUI.Label(new Rect(27, 45, 500, 20), "Createur autonome de personnages humains 3D", subtitleStyle);
        GUI.Label(new Rect(Screen.width - 300, 21, 270, 28), "BASE HUMAINE CC0  •  RIG GAME", valueStyle);
    }

    private void DrawControls()
    {
        float width = Mathf.Clamp(Screen.width * 0.32f, 335f, 410f);
        GUI.color = Panel;
        GUI.DrawTexture(new Rect(0, 74, width, Screen.height - 74), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUILayout.BeginArea(new Rect(18, 88, width - 34, Screen.height - 105));
        scroll = GUILayout.BeginScrollView(scroll);

        GUILayout.Label("IDENTITE", sectionStyle);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("HOMME", preset.sex == CharacterSex.Male ? selectedButtonStyle : buttonStyle, GUILayout.Height(34)))
            ChangeGender(CharacterSex.Male);
        if (GUILayout.Button("FEMME", preset.sex == CharacterSex.Female ? selectedButtonStyle : buttonStyle, GUILayout.Height(34)))
            ChangeGender(CharacterSex.Female);
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
        GUILayout.Label("La morphologie utilise une vraie base MakeHuman CC0 et ses cibles de forme, pas des primitives.", helpStyle);

        GUILayout.Label("CORPS", sectionStyle);
        Slider("Ventre", ref preset.belly, "plat", "fort");
        Slider("Bras - epaisseur", ref preset.armThickness, "fin", "fort");
        Slider("Bras - longueur", ref preset.armLength, "court", "long");
        Slider("Jambes - epaisseur", ref preset.legThickness, "fines", "fortes");
        Slider("Jambes - longueur", ref preset.legLength, "courtes", "longues");
        Slider("Pieds - taille", ref preset.feetSize, "petits", "grands");

        GUILayout.Label("TETE", sectionStyle);
        Slider("Tete - forme", ref preset.headShape, "ovale", "ronde");
        Slider("Yeux - forme / taille", ref preset.eyesShape, "fins", "grands");
        Slider("Nez - volume", ref preset.noseShape, "fin", "large");
        Slider("Bouche - volume", ref preset.mouthShape, "fine", "pulpeuse");
        Slider("Oreilles - taille", ref preset.earsShape, "petites", "grandes");

        GUILayout.Label("CHEVEUX", sectionStyle);
        string[] hairNames = { "Court", "Long", "Carre", "Attache", "Boucle" };
        for (int i = 0; i < hairNames.Length; i++)
        {
            if (GUILayout.Button(hairNames[i], preset.hairStyle == i ? selectedButtonStyle : buttonStyle, GUILayout.Height(29)))
            {
                preset.hairStyle = i;
                Rebuild("Coupe : " + hairNames[i]);
            }
        }

        GUILayout.Space(8);
        if (GUILayout.Button("⚄  ALEATOIRE", new GUIStyle(selectedButtonStyle) { fontSize = 16 }, GUILayout.Height(45)))
            Randomize();
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Reinitialiser", buttonStyle, GUILayout.Height(31)))
        {
            preset = CharacterPreset.Default();
            Rebuild("Personnage reinitialise");
        }
        if (GUILayout.Button("Exporter preset", buttonStyle, GUILayout.Height(31))) ExportPreset();
        GUILayout.EndHorizontal();

        GUILayout.Space(8);
        GUILayout.Label(status, helpStyle);
        showTechnical = GUILayout.Toggle(showTechnical, " Afficher les informations techniques", labelStyle);
        if (showTechnical)
        {
            GUILayout.Label("Mesh : MakeHuman hm08, 19 158 sommets\nRig : default / game-ready, poids integres\nSortie : personnage_preset.json", helpStyle);
        }
        GUILayout.EndScrollView();
        GUILayout.EndArea();
    }

    private void DrawPreviewHint()
    {
        float left = Mathf.Clamp(Screen.width * 0.32f, 335f, 410f);
        GUI.Label(new Rect(left + 26, Screen.height - 44, Screen.width - left - 40, 24),
            "Glisser dans la zone de preview pour tourner le personnage", subtitleStyle);
    }

    private void Slider(string label, ref float value, string min, string max)
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label(label, labelStyle, GUILayout.Width(150));
        GUILayout.Label((value * 50f + 50f).ToString("0", CultureInfo.InvariantCulture), valueStyle, GUILayout.Width(28));
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label(min, subtitleStyle, GUILayout.Width(34));
        float next = GUILayout.HorizontalSlider(value, -1f, 1f, GUILayout.Height(18));
        GUILayout.Label(max, subtitleStyle, GUILayout.Width(34));
        GUILayout.EndHorizontal();
        if (Mathf.Abs(next - value) > 0.002f)
        {
            value = next;
            Rebuild(label + " modifie");
        }
    }

    private void ChangeGender(CharacterSex sex)
    {
        if (preset.sex == sex) return;
        preset.sex = sex;
        Rebuild(sex == CharacterSex.Male ? "Base homme generee" : "Base femme generee");
    }

    private void Randomize()
    {
        preset.sex = UnityEngine.Random.value > 0.5f ? CharacterSex.Male : CharacterSex.Female;
        preset.belly = UnityEngine.Random.Range(-0.65f, 0.75f);
        preset.armThickness = UnityEngine.Random.Range(-0.7f, 0.75f);
        preset.armLength = UnityEngine.Random.Range(-0.65f, 0.7f);
        preset.legThickness = UnityEngine.Random.Range(-0.65f, 0.7f);
        preset.legLength = UnityEngine.Random.Range(-0.65f, 0.7f);
        preset.feetSize = UnityEngine.Random.Range(-0.65f, 0.7f);
        preset.headShape = UnityEngine.Random.Range(-0.75f, 0.75f);
        preset.eyesShape = UnityEngine.Random.Range(-0.65f, 0.75f);
        preset.noseShape = UnityEngine.Random.Range(-0.7f, 0.75f);
        preset.mouthShape = UnityEngine.Random.Range(-0.7f, 0.75f);
        preset.earsShape = UnityEngine.Random.Range(-0.65f, 0.7f);
        preset.hairStyle = UnityEngine.Random.Range(0, 5);
        Rebuild("Nouvelle combinaison aleatoire");
    }

    private void Rebuild(string message)
    {
        if (avatar == null) return;
        avatar.Build(preset);
        status = message;
    }

    private void ExportPreset()
    {
        try
        {
            string path = Path.Combine(Application.persistentDataPath, "personnage_preset.json");
            File.WriteAllText(path, JsonUtility.ToJson(preset, true), Encoding.UTF8);
            status = "Preset exporte dans : " + path;
        }
        catch (Exception ex)
        {
            status = "Export impossible : " + ex.Message;
        }
    }

    private static Material NewMaterial(Color color, float metallic, float smoothness)
    {
        var shader = Shader.Find("Standard");
        var material = new Material(shader);
        material.color = color;
        material.SetFloat("_Metallic", metallic);
        material.SetFloat("_Glossiness", smoothness);
        return material;
    }

    private static Texture2D MakeTexture(int width, int height, Color color)
    {
        var texture = new Texture2D(width, height);
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    [Serializable]
    private sealed class CharacterPreset
    {
        public CharacterSex sex;
        public float belly;
        public float armThickness;
        public float armLength;
        public float legThickness;
        public float legLength;
        public float feetSize;
        public float headShape;
        public float eyesShape;
        public float noseShape;
        public float mouthShape;
        public float earsShape;
        public int hairStyle;

        public static CharacterPreset Default()
        {
            return new CharacterPreset
            {
                sex = CharacterSex.Female,
                belly = 0f,
                armThickness = 0f,
                armLength = 0f,
                legThickness = 0f,
                legLength = 0f,
                feetSize = 0f,
                headShape = 0f,
                eyesShape = 0f,
                noseShape = 0f,
                mouthShape = 0f,
                earsShape = 0f,
                hairStyle = 2
            };
        }
    }

    private enum CharacterSex { Male, Female }

    private sealed class HumanAvatar
    {
        private const float ModelScale = 0.13f;
        private const string ResourceRoot = "Characters/";
        private readonly Dictionary<string, TextAsset> targetTexts = new Dictionary<string, TextAsset>();
        private readonly Dictionary<string, Dictionary<int, Vector3>> targetCache = new Dictionary<string, Dictionary<int, Vector3>>();
        private ObjData obj;
        private GameObject root;
        private GameObject hair;
        private Transform[] bones;
        private Dictionary<string, int> boneIndexes;
        private Material skin;
        private Material eyes;
        private Material teeth;
        private Material hairMaterial;
        private float yaw;

        public HumanAvatar()
        {
            obj = ObjData.Load(Resources.Load<TextAsset>(ResourceRoot + "MakeHumanBaseData"));
            skin = NewMaterial(new Color(0.72f, 0.42f, 0.31f), 0.02f, 0.38f);
            eyes = NewMaterial(new Color(0.93f, 0.95f, 0.98f), 0f, 0.62f);
            teeth = NewMaterial(new Color(0.92f, 0.86f, 0.7f), 0f, 0.35f);
            hairMaterial = NewMaterial(new Color(0.08f, 0.035f, 0.018f), 0f, 0.2f);
            Texture2D skinTexture = Resources.Load<Texture2D>(ResourceRoot + "SkinBase");
            Texture2D hairTexture = Resources.Load<Texture2D>(ResourceRoot + "HairDark");
            if (skinTexture != null) skin.mainTexture = skinTexture;
            if (hairTexture != null) hairMaterial.mainTexture = hairTexture;
            LoadTargets();
        }

        private void LoadTargets()
        {
            string[] names =
            {
                "universal-male-young-averagemuscle-averageweight",
                "universal-female-young-averagemuscle-averageweight",
                "stomach-pregnant-incr", "stomach-pregnant-decr",
                "torso-scale-horiz-incr", "torso-scale-horiz-decr",
                "l-upperarm-scale-horiz-incr", "l-upperarm-scale-horiz-decr",
                "r-upperarm-scale-horiz-incr", "r-upperarm-scale-horiz-decr",
                "l-upperarm-scale-vert-incr", "l-upperarm-scale-vert-decr",
                "r-upperarm-scale-vert-incr", "r-upperarm-scale-vert-decr",
                "l-lowerarm-scale-horiz-incr", "l-lowerarm-scale-horiz-decr",
                "r-lowerarm-scale-horiz-incr", "r-lowerarm-scale-horiz-decr",
                "l-lowerarm-scale-vert-incr", "l-lowerarm-scale-vert-decr",
                "r-lowerarm-scale-vert-incr", "r-lowerarm-scale-vert-decr",
                "upperlegs-height-incr", "upperlegs-height-decr",
                "l-upperleg-scale-horiz-incr", "l-upperleg-scale-horiz-decr",
                "r-upperleg-scale-horiz-incr", "r-upperleg-scale-horiz-decr",
                "l-lowerleg-scale-horiz-incr", "l-lowerleg-scale-horiz-decr",
                "r-lowerleg-scale-horiz-incr", "r-lowerleg-scale-horiz-decr",
                "l-foot-scale-incr", "l-foot-scale-decr", "r-foot-scale-incr", "r-foot-scale-decr",
                "head-round", "head-oval", "l-eye-scale-incr", "l-eye-scale-decr",
                "r-eye-scale-incr", "r-eye-scale-decr", "nose-volume-incr", "nose-volume-decr",
                "mouth-lowerlip-volume-incr", "mouth-lowerlip-volume-decr",
                "l-ear-scale-incr", "l-ear-scale-decr", "r-ear-scale-incr", "r-ear-scale-decr"
            };
            foreach (string name in names)
                targetTexts[name] = Resources.Load<TextAsset>(ResourceRoot + "MakeHumanTargets/" + name);
        }

        public void SetYaw(float value)
        {
            yaw = value;
            if (root != null) root.transform.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f);
        }

        public void Build(CharacterPreset preset)
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = new GameObject("Personnage humain rigge");
            // Build the bind pose in the root's local coordinates. Rotating the
            // root before creating bones would put the mesh and bind poses in
            // different spaces. The presentation yaw is applied at the end.

            Vector3[] deformed = (Vector3[])obj.vertices.Clone();
            if (preset.sex == CharacterSex.Male)
                Add(deformed, Target("universal-male-young-averagemuscle-averageweight"), 1f);
            else
                Add(deformed, Target("universal-female-young-averagemuscle-averageweight"), 1f);

            Signed(deformed, preset.belly, "stomach-pregnant-incr", "stomach-pregnant-decr", 0.55f);
            Signed(deformed, preset.belly, "torso-scale-horiz-incr", "torso-scale-horiz-decr", 0.28f);
            Signed(deformed, preset.armThickness, "l-upperarm-scale-horiz-incr", "l-upperarm-scale-horiz-decr", 0.5f);
            Signed(deformed, preset.armThickness, "r-upperarm-scale-horiz-incr", "r-upperarm-scale-horiz-decr", 0.5f);
            Signed(deformed, preset.armThickness, "l-lowerarm-scale-horiz-incr", "l-lowerarm-scale-horiz-decr", 0.5f);
            Signed(deformed, preset.armThickness, "r-lowerarm-scale-horiz-incr", "r-lowerarm-scale-horiz-decr", 0.5f);
            Signed(deformed, preset.armLength, "l-upperarm-scale-vert-incr", "l-upperarm-scale-vert-decr", 0.55f);
            Signed(deformed, preset.armLength, "r-upperarm-scale-vert-incr", "r-upperarm-scale-vert-decr", 0.55f);
            Signed(deformed, preset.armLength, "l-lowerarm-scale-vert-incr", "l-lowerarm-scale-vert-decr", 0.55f);
            Signed(deformed, preset.armLength, "r-lowerarm-scale-vert-incr", "r-lowerarm-scale-vert-decr", 0.55f);
            Signed(deformed, preset.legThickness, "l-upperleg-scale-horiz-incr", "l-upperleg-scale-horiz-decr", 0.52f);
            Signed(deformed, preset.legThickness, "r-upperleg-scale-horiz-incr", "r-upperleg-scale-horiz-decr", 0.52f);
            Signed(deformed, preset.legThickness, "l-lowerleg-scale-horiz-incr", "l-lowerleg-scale-horiz-decr", 0.52f);
            Signed(deformed, preset.legThickness, "r-lowerleg-scale-horiz-incr", "r-lowerleg-scale-horiz-decr", 0.52f);
            Signed(deformed, preset.legLength, "upperlegs-height-incr", "upperlegs-height-decr", 0.55f);
            Signed(deformed, preset.feetSize, "l-foot-scale-incr", "l-foot-scale-decr", 0.58f);
            Signed(deformed, preset.feetSize, "r-foot-scale-incr", "r-foot-scale-decr", 0.58f);
            Signed(deformed, preset.headShape, "head-round", "head-oval", 0.58f);
            Signed(deformed, preset.eyesShape, "l-eye-scale-incr", "l-eye-scale-decr", 0.62f);
            Signed(deformed, preset.eyesShape, "r-eye-scale-incr", "r-eye-scale-decr", 0.62f);
            Signed(deformed, preset.noseShape, "nose-volume-incr", "nose-volume-decr", 0.65f);
            Signed(deformed, preset.mouthShape, "mouth-lowerlip-volume-incr", "mouth-lowerlip-volume-decr", 0.7f);
            Signed(deformed, preset.earsShape, "l-ear-scale-incr", "l-ear-scale-decr", 0.62f);
            Signed(deformed, preset.earsShape, "r-ear-scale-incr", "r-ear-scale-decr", 0.62f);

            Vector3 min = deformed[0];
            for (int i = 1; i < deformed.Length; i++) min = Vector3.Min(min, deformed[i]);
            Vector3[] vertices = new Vector3[deformed.Length];
            for (int i = 0; i < deformed.Length; i++)
                vertices[i] = new Vector3(deformed[i].x * ModelScale, (deformed[i].y - min.y) * ModelScale, deformed[i].z * ModelScale);

            BuildBones(deformed, min.y);
            Mesh mesh = obj.CreateMesh(vertices);
            var meshObject = new GameObject("MakeHuman hm08 - mesh humain");
            meshObject.transform.SetParent(root.transform, false);
            var renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = boneIndexes.ContainsKey("root") ? bones[boneIndexes["root"]] : bones[0];
            renderer.sharedMaterials = new[] { skin, eyes, teeth };
            renderer.updateWhenOffscreen = true;
            ApplySkinWeights(mesh, renderer);
            hair = HairBuilder.Create(preset.hairStyle, root.transform, FindBone("head"), hairMaterial);
            root.transform.localRotation = Quaternion.Euler(0f, 180f + yaw, 0f);
        }

        private void BuildBones(Vector3[] rawVertices, float minY)
        {
            TextAsset rigText = Resources.Load<TextAsset>(ResourceRoot + "rig");
            if (rigText == null) rigText = Resources.Load<TextAsset>(ResourceRoot + "rig.csv");
            var definitions = new List<RigDefinition>();
            // TextAssets keep their extension out of Resources names, so rig.csv
            // is requested as "rig" by Unity. The fallback is useful in editor.
            if (rigText == null) return;
            foreach (string line in rigText.text.Split('\n'))
            {
                string[] p = line.Trim().Split('|');
                if (p.Length != 4) continue;
                definitions.Add(new RigDefinition(p[0], p[1], ParseIndexes(p[2]), ParseIndexes(p[3])));
            }
            boneIndexes = new Dictionary<string, int>();
            bones = new Transform[definitions.Count];
            var byName = new Dictionary<string, RigDefinition>();
            foreach (RigDefinition d in definitions) byName[d.name] = d;
            for (int i = 0; i < definitions.Count; i++) boneIndexes[definitions[i].name] = i;
            for (int i = 0; i < definitions.Count; i++) CreateBone(i, definitions[i], byName, rawVertices, minY);
        }

        private void CreateBone(int index, RigDefinition definition, Dictionary<string, RigDefinition> byName, Vector3[] rawVertices, float minY)
        {
            if (bones[index] != null) return;
            if (!string.IsNullOrEmpty(definition.parent) && byName.ContainsKey(definition.parent))
                CreateBone(boneIndexes[definition.parent], byName[definition.parent], byName, rawVertices, minY);
            var objectBone = new GameObject("Bone_" + definition.name);
            Transform parent = string.IsNullOrEmpty(definition.parent) || !boneIndexes.ContainsKey(definition.parent)
                ? root.transform : bones[boneIndexes[definition.parent]];
            objectBone.transform.SetParent(parent, true);
            objectBone.transform.position = Average(definition.head, rawVertices, minY);
            objectBone.transform.rotation = Quaternion.identity;
            bones[index] = objectBone.transform;
        }

        private Vector3 Average(List<int> indexes, Vector3[] vertices, float minY)
        {
            if (indexes == null || indexes.Count == 0) return Vector3.zero;
            Vector3 result = Vector3.zero;
            int count = 0;
            foreach (int index in indexes)
            {
                if (index < 0 || index >= vertices.Length) continue;
                Vector3 v = vertices[index];
                result += new Vector3(v.x * ModelScale, (v.y - minY) * ModelScale, v.z * ModelScale);
                count++;
            }
            return count == 0 ? Vector3.zero : result / count;
        }

        private Transform FindBone(string name)
        {
            if (boneIndexes != null && boneIndexes.ContainsKey(name)) return bones[boneIndexes[name]];
            return root.transform;
        }

        private void ApplySkinWeights(Mesh mesh, SkinnedMeshRenderer renderer)
        {
            var influences = new List<Influence>[obj.vertices.Length];
            for (int i = 0; i < influences.Length; i++) influences[i] = new List<Influence>();
            TextAsset weights = Resources.Load<TextAsset>(ResourceRoot + "weights");
            if (weights == null) weights = Resources.Load<TextAsset>(ResourceRoot + "weights.csv");
            if (weights != null)
            {
                foreach (string line in weights.text.Split('\n'))
                {
                    string[] pair = line.Trim().Split('|');
                    if (pair.Length != 2 || !boneIndexes.ContainsKey(pair[0])) continue;
                    int bone = boneIndexes[pair[0]];
                    foreach (string value in pair[1].Split(';'))
                    {
                        string[] bits = value.Split(':');
                        if (bits.Length != 2) continue;
                        int vertex; float amount;
                        if (int.TryParse(bits[0], out vertex) && float.TryParse(bits[1], NumberStyles.Float, CultureInfo.InvariantCulture, out amount)
                            && vertex >= 0 && vertex < influences.Length)
                            influences[vertex].Add(new Influence(bone, amount));
                    }
                }
            }
            var result = new BoneWeight[influences.Length];
            for (int i = 0; i < influences.Length; i++)
            {
                influences[i].Sort((a, b) => b.weight.CompareTo(a.weight));
                if (influences[i].Count == 0) influences[i].Add(new Influence(boneIndexes.ContainsKey("root") ? boneIndexes["root"] : 0, 1f));
                float total = 0f;
                int count = Mathf.Min(4, influences[i].Count);
                for (int j = 0; j < count; j++) total += influences[i][j].weight;
                for (int j = 0; j < count; j++)
                {
                    float value = influences[i][j].weight / Mathf.Max(0.0001f, total);
                    if (j == 0) { result[i].boneIndex0 = influences[i][j].bone; result[i].weight0 = value; }
                    if (j == 1) { result[i].boneIndex1 = influences[i][j].bone; result[i].weight1 = value; }
                    if (j == 2) { result[i].boneIndex2 = influences[i][j].bone; result[i].weight2 = value; }
                    if (j == 3) { result[i].boneIndex3 = influences[i][j].bone; result[i].weight3 = value; }
                }
            }
            mesh.boneWeights = result;
            Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
            Matrix4x4 meshMatrix = renderer.transform.localToWorldMatrix;
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * meshMatrix;
            mesh.bindposes = bindposes;
        }

        private Dictionary<int, Vector3> Target(string name)
        {
            if (targetCache.ContainsKey(name)) return targetCache[name];
            var result = new Dictionary<int, Vector3>();
            TextAsset text = targetTexts.ContainsKey(name) ? targetTexts[name] : null;
            if (text != null)
            {
                foreach (string line in text.text.Split('\n'))
                {
                    string[] p = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 4) continue;
                    int index; float x, y, z;
                    if (int.TryParse(p[0], out index)
                        && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x)
                        && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y)
                        && float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out z))
                        result[index] = new Vector3(x, y, z);
                }
            }
            targetCache[name] = result;
            return result;
        }

        private static void Add(Vector3[] vertices, Dictionary<int, Vector3> target, float amount)
        {
            foreach (KeyValuePair<int, Vector3> item in target)
                if (item.Key >= 0 && item.Key < vertices.Length) vertices[item.Key] += item.Value * amount;
        }

        private void Signed(Vector3[] vertices, float amount, string positive, string negative, float strength)
        {
            Add(vertices, Target(amount >= 0f ? positive : negative), Mathf.Abs(amount) * strength);
        }

        private sealed class RigDefinition
        {
            public readonly string name;
            public readonly string parent;
            public readonly List<int> head;
            public readonly List<int> tail;
            public RigDefinition(string name, string parent, List<int> head, List<int> tail)
            { this.name = name; this.parent = parent; this.head = head; this.tail = tail; }
        }

        private sealed class Influence
        {
            public int bone; public float weight;
            public Influence(int bone, float weight) { this.bone = bone; this.weight = weight; }
        }

        private static List<int> ParseIndexes(string value)
        {
            var result = new List<int>();
            foreach (string part in value.Split(',')) { int n; if (int.TryParse(part, out n)) result.Add(n); }
            return result;
        }
    }

    private sealed class ObjData
    {
        public Vector3[] vertices;
        private Vector2[] uv;
        private readonly List<ObjTriangle> triangles = new List<ObjTriangle>();

        public static ObjData Load(TextAsset text)
        {
            var data = new ObjData();
            var positions = new List<Vector3>();
            var uvs = new List<Vector2>();
            var uvForVertex = new List<int>();
            string group = "body";
            foreach (string line in text.text.Split('\n'))
            {
                string clean = line.Trim();
                if (clean.StartsWith("v "))
                {
                    string[] p = clean.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 4) positions.Add(new Vector3(F(p[1]), F(p[2]), F(p[3])));
                }
                else if (clean.StartsWith("vt "))
                {
                    string[] p = clean.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length >= 3) uvs.Add(new Vector2(F(p[1]), F(p[2])));
                }
                else if (clean.StartsWith("g ")) group = clean.Substring(2).Trim();
                else if (clean.StartsWith("f "))
                {
                    string[] p = clean.Substring(2).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 3) continue;
                    int first = VertexIndex(p[0], positions.Count), previous = VertexIndex(p[1], positions.Count);
                    for (int i = 2; i < p.Length; i++)
                    {
                        int current = VertexIndex(p[i], positions.Count);
                        if (first >= 0 && previous >= 0 && current >= 0)
                            data.triangles.Add(new ObjTriangle(first, previous, current, MaterialIndex(group)));
                        previous = current;
                    }
                }
            }
            data.vertices = positions.ToArray();
            data.uv = new Vector2[data.vertices.Length];
            uvForVertex = new List<int>(new int[data.vertices.Length]);
            foreach (string line in text.text.Split('\n'))
            {
                if (!line.TrimStart().StartsWith("f ")) continue;
                string[] p = line.Trim().Substring(2).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                foreach (string token in p)
                {
                    string[] bits = token.Split('/');
                    int vi = VertexIndex(token, data.vertices.Length);
                    int ti;
                    if (vi >= 0 && bits.Length > 1 && int.TryParse(bits[1], out ti))
                    {
                        ti = ti < 0 ? uvs.Count + ti : ti - 1;
                        if (ti >= 0 && ti < uvs.Count && uvForVertex[vi] == 0) { data.uv[vi] = uvs[ti]; uvForVertex[vi] = ti + 1; }
                    }
                }
            }
            return data;
        }

        public Mesh CreateMesh(Vector3[] currentVertices)
        {
            var mesh = new Mesh { name = "MakeHuman hm08 - mesh skinned", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = currentVertices;
            mesh.uv = uv;
            var byMaterial = new List<int>[3] { new List<int>(), new List<int>(), new List<int>() };
            foreach (ObjTriangle t in triangles)
            {
                byMaterial[t.material].Add(t.a); byMaterial[t.material].Add(t.b); byMaterial[t.material].Add(t.c);
            }
            mesh.subMeshCount = 3;
            for (int i = 0; i < 3; i++) mesh.SetTriangles(byMaterial[i], i, true);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static int MaterialIndex(string group)
        {
            string lower = group.ToLowerInvariant();
            if (lower.Contains("eye") || lower.Contains("eyelash")) return 1;
            if (lower.Contains("teeth") || lower.Contains("tongue")) return 2;
            if (lower.StartsWith("joint-")) return 0;
            return 0;
        }

        private static int VertexIndex(string token, int count)
        {
            string value = token.Split('/')[0];
            int index;
            if (!int.TryParse(value, out index)) return -1;
            return index < 0 ? count + index : index - 1;
        }

        private static float F(string value)
        { return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture); }

        private struct ObjTriangle
        {
            public int a, b, c, material;
            public ObjTriangle(int a, int b, int c, int material) { this.a = a; this.b = b; this.c = c; this.material = material; }
        }
    }

    private static class HairBuilder
    {
        public static GameObject Create(int style, Transform parent, Transform head, Material material)
        {
            var objectHair = new GameObject("Cheveux - coupe " + style);
            objectHair.transform.SetParent(parent, false);
            Vector3 center = head.position + new Vector3(0f, 0.02f, 0f);
            float width = style == 2 ? 0.39f : 0.37f;
            float depth = 0.34f;
            float length = style == 1 ? 0.70f : (style == 2 ? 0.55f : 0.40f);
            int rows = 7, columns = 40;
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            for (int row = 0; row < rows; row++)
            {
                float t = row / (float)(rows - 1);
                float y = center.y - 0.16f + t * length;
                float radius = Mathf.Lerp(width, 0.055f, t * t);
                for (int col = 0; col < columns; col++)
                {
                    float angle = col * Mathf.PI * 2f / columns;
                    float styleOffset = style == 4 ? Mathf.Sin(angle * 5f) * 0.018f : 0f;
                    vertices.Add(new Vector3(center.x + Mathf.Cos(angle) * (radius + styleOffset), y,
                        center.z + Mathf.Sin(angle) * (radius * depth / width + styleOffset)));
                }
            }
            for (int row = 0; row < rows - 1; row++)
                for (int col = 0; col < columns; col++)
                {
                    int a = row * columns + col;
                    int b = row * columns + (col + 1) % columns;
                    int c = (row + 1) * columns + (col + 1) % columns;
                    int d = (row + 1) * columns + col;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                }
            // A little fringe makes the short and bob styles visibly distinct.
            if (style == 0 || style == 2 || style == 4)
            {
                int baseIndex = vertices.Count;
                for (int i = 0; i < 9; i++)
                {
                    float x = Mathf.Lerp(-width * 0.72f, width * 0.72f, i / 8f);
                    float y = center.y - 0.11f - Mathf.Sin(i / 8f * Mathf.PI) * 0.08f;
                    vertices.Add(new Vector3(center.x + x, y, center.z + depth * 0.94f));
                }
                for (int i = 0; i < 8; i++)
                { triangles.Add(baseIndex + i); triangles.Add(baseIndex + i + 1); triangles.Add(baseIndex + i + 1); }
            }
            var mesh = new Mesh { name = "Cheveux integres" };
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var renderer = objectHair.AddComponent<MeshRenderer>();
            var filter = objectHair.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh; renderer.sharedMaterial = material;
            return objectHair;
        }
    }
}
