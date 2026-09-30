using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// Createur humain reutilisable dans le jeu.
// Le composant fournit aussi la camera et la texture de rendu du panneau ADMIN.
public sealed class AdminHumanCreator : MonoBehaviour
{
    private const int PreviewLayer = 31;
    public bool female = true;
    public int skinTone;
    public float belly;
    public float chestShape;
    public float hipShape;
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

    // Placement de l'image de soutien-gorge dans le preview ADMIN.
    public float garmentScale = 1f;
    public float garmentOffsetX;
    public float garmentOffsetY = 0.0509090908f;
    public float garmentOffsetZ = 0.24318181f;

    [Serializable]
    private sealed class GarmentPlacementData
    {
        public string asset = "soutien_gorge.png";
        public string anchor = "torse";
        public string coordinateSpace = "preview_human_local";
        public int version = 1;
        public float scale;
        public float offsetX;
        public float offsetY;
        public float offsetZ;
    }

    private HumanPreview preview;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private HumanPreview appliedHuman;
    private float previewDistance = 5.00f;
    private float previewYaw;
    private float previewPitch;

    public float PreviewDistance
    {
        get { return previewDistance; }
    }

    public RenderTexture PreviewTexture
    {
        get { return previewTexture; }
    }

    public bool PreviewReady
    {
        get { return previewTexture != null && previewTexture.IsCreated(); }
    }

    public void BuildPreview()
    {
        EnsurePreviewCamera();
        if (preview == null) preview = new HumanPreview(transform);
        preview.Build(this);
        preview.SetPresentationLayer(PreviewLayer);
        PositionPreviewCamera();
    }

    public void OrbitPreview(float yawDelta, float pitchDelta)
    {
        previewYaw = Mathf.Repeat(previewYaw + yawDelta + 180f, 360f) - 180f;
        previewPitch = Mathf.Clamp(previewPitch + pitchDelta, -75f, 75f);
        PositionPreviewCamera();
    }

    public void ZoomPreview(float amount)
    {
        previewDistance = Mathf.Clamp(previewDistance + amount, 2.25f, 9.00f);
        PositionPreviewCamera();
    }

    public void ResetPreviewCamera()
    {
        previewDistance = 5.00f;
        previewYaw = 0f;
        previewPitch = 0f;
        PositionPreviewCamera();
    }

    // Applique le meme maillage humain rigge et les memes morphs au personnage
    // reel. Le panneau n'est plus seulement une image de demonstration.
    public bool ApplyTo(Transform target)
    {
        if (target == null) return false;
        if (appliedHuman != null) appliedHuman.DestroyRoot();
        appliedHuman = new HumanPreview(target);
        appliedHuman.Build(this, false);
        appliedHuman.SetPresentationLayer(target.gameObject.layer);
        return appliedHuman.IsBuilt;
    }

    public void AnimateAppliedHuman(bool moving, bool running, float clock)
    {
        if (appliedHuman != null) appliedHuman.Animate(moving, running, clock);
    }

    private void EnsurePreviewCamera()
    {
        if (previewCamera != null) return;
        previewTexture = new RenderTexture(480, 640, 24, RenderTextureFormat.ARGB32);
        previewTexture.name = "ADMIN_HumanPreview_RenderTexture";
        previewTexture.Create();
        GameObject cameraObject = new GameObject("ADMIN - Camera apercu humain");
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.clearFlags = CameraClearFlags.SolidColor;
        // Fond gris neutre : il reste lisible quel que soit le type de
        // daltonisme et contraste mieux avec la peau et les vetements fonces.
        previewCamera.backgroundColor = new Color(0.32f, 0.32f, 0.32f, 1f);
        previewCamera.fieldOfView = 30f;
        previewCamera.nearClipPlane = 0.03f;
        previewCamera.farClipPlane = 20f;
        previewCamera.cullingMask = 1 << PreviewLayer;
        previewCamera.targetTexture = previewTexture;
        previewCamera.enabled = true;
    }

    private void PositionPreviewCamera()
    {
        if (previewCamera == null) return;
        Vector3 cible = transform.position + Vector3.up * 1.08f;
        // Le recul laisse toujours entrer les pieds et le sommet de la tete
        // dans le cadre, meme lorsque les proportions sont modifiees.
        Quaternion orbite = Quaternion.Euler(previewPitch, previewYaw, 0f);
        Vector3 direction = orbite * Vector3.back;
        previewCamera.transform.position = cible + direction * previewDistance;
        previewCamera.transform.LookAt(cible);
    }

    private void OnDestroy()
    {
        if (appliedHuman != null) appliedHuman.DestroyRoot();
        if (previewCamera != null) UnityEngine.Object.Destroy(previewCamera.gameObject);
        if (previewTexture != null)
        {
            previewTexture.Release();
            UnityEngine.Object.Destroy(previewTexture);
        }
    }

    public void Randomize()
    {
        female = UnityEngine.Random.value > 0.5f;
        skinTone = UnityEngine.Random.Range(0, 7);
        belly = UnityEngine.Random.Range(-0.65f, 0.75f);
        chestShape = UnityEngine.Random.Range(-0.24f, 0.75f);
        hipShape = UnityEngine.Random.Range(-0.65f, 0.75f);
        hairStyle = UnityEngine.Random.Range(0, 5);
        armThickness = UnityEngine.Random.Range(-0.7f, 0.75f);
        armLength = UnityEngine.Random.Range(-0.65f, 0.7f);
        legThickness = UnityEngine.Random.Range(-0.65f, 0.7f);
        legLength = UnityEngine.Random.Range(-0.65f, 0.7f);
        feetSize = UnityEngine.Random.Range(-0.65f, 0.7f);
        headShape = UnityEngine.Random.Range(-0.75f, 0.75f);
        eyesShape = UnityEngine.Random.Range(-0.65f, 0.75f);
        noseShape = UnityEngine.Random.Range(-0.7f, 0.75f);
        mouthShape = UnityEngine.Random.Range(-0.7f, 0.75f);
        earsShape = UnityEngine.Random.Range(-0.65f, 0.7f);
        BuildPreview();
    }

    public void ResetPreview()
    {
        female = true;
        skinTone = 0;
        belly = chestShape = hipShape = armThickness = armLength = legThickness = legLength = feetSize = 0f;
        headShape = eyesShape = noseShape = mouthShape = earsShape = 0f;
        hairStyle = 0;
        BuildPreview();
    }

    public string SaveGarmentPlacement()
    {
        string directory = Path.Combine(Application.persistentDataPath, "LibreVies");
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "soutien_gorge_placement.json");
        GarmentPlacementData data = new GarmentPlacementData
        {
            scale = garmentScale,
            offsetX = garmentOffsetX,
            offsetY = garmentOffsetY,
            offsetZ = garmentOffsetZ
        };
        File.WriteAllText(path, JsonUtility.ToJson(data, true));
        return path;
    }

    private sealed class HumanPreview
    {
        private const float Scale = 0.13f;
        private const string Root = "Characters/MakeHuman/";
        private readonly Transform parent;
        private readonly Dictionary<string, TextAsset> targetTexts = new Dictionary<string, TextAsset>();
        private readonly Dictionary<string, Dictionary<int, Vector3>> targets = new Dictionary<string, Dictionary<int, Vector3>>();
        private ObjData obj;
        private GameObject root;
        private Transform[] bones;
        private Dictionary<string, int> boneIndexes;
        private Material skin;
        private Material underwearMaterial;
        private Material hairMaterial;
        private Texture2D skinTexture;
        private Texture2D underwearTexture;
        private GameObject hair;
        private int hairStyle;
        private bool hairFemale;

        public HumanPreview(Transform parent)
        {
            this.parent = parent;
            obj = ObjData.Load(Resources.Load<TextAsset>(Root + "MakeHumanBaseData"));
            skin = NewMaterial(new Color(0.72f, 0.42f, 0.31f), 0.02f, 0.38f);
            hairMaterial = NewMaterial(new Color(0.06f, 0.025f, 0.012f), 0f, 0.22f);
            skinTexture = Resources.Load<Texture2D>(Root + "SkinBase");
            if (skinTexture != null) skin.mainTexture = skinTexture;
            underwearTexture = Resources.Load<Texture2D>("Characters/Clothing/soutien_gorge_uv");
            underwearMaterial = NewMaterial(Color.white, 0f, 0.45f);
            if (underwearTexture != null) underwearMaterial.mainTexture = underwearTexture;
            if (underwearMaterial.HasProperty("_Cull"))
                underwearMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            // La coiffure procedurale reste volontairement brune et mate :
            // la texture plate HairDark formait un bandeau noir dans le preview.
            LoadTargets();
        }

        public bool IsBuilt
        {
            get { return root != null; }
        }

        public void DestroyRoot()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            root = null;
        }

        private void LoadTargets()
        {
            string[] names =
            {
                "universal-male-young-averagemuscle-averageweight", "universal-female-young-averagemuscle-averageweight",
                "stomach-pregnant-incr", "stomach-pregnant-decr", "torso-scale-horiz-incr", "torso-scale-horiz-decr",
                "l-upperarm-scale-horiz-incr", "l-upperarm-scale-horiz-decr", "r-upperarm-scale-horiz-incr", "r-upperarm-scale-horiz-decr",
                "l-upperarm-scale-vert-incr", "l-upperarm-scale-vert-decr", "r-upperarm-scale-vert-incr", "r-upperarm-scale-vert-decr",
                "l-lowerarm-scale-horiz-incr", "l-lowerarm-scale-horiz-decr", "r-lowerarm-scale-horiz-incr", "r-lowerarm-scale-horiz-decr",
                "l-lowerarm-scale-vert-incr", "l-lowerarm-scale-vert-decr", "r-lowerarm-scale-vert-incr", "r-lowerarm-scale-vert-decr",
                "upperlegs-height-incr", "upperlegs-height-decr", "l-upperleg-scale-horiz-incr", "l-upperleg-scale-horiz-decr",
                "r-upperleg-scale-horiz-incr", "r-upperleg-scale-horiz-decr", "l-lowerleg-scale-horiz-incr", "l-lowerleg-scale-horiz-decr",
                "r-lowerleg-scale-horiz-incr", "r-lowerleg-scale-horiz-decr", "l-foot-scale-incr", "l-foot-scale-decr",
                "r-foot-scale-incr", "r-foot-scale-decr", "head-round", "head-oval", "l-eye-scale-incr", "l-eye-scale-decr",
                "r-eye-scale-incr", "r-eye-scale-decr", "nose-volume-incr", "nose-volume-decr",
                "mouth-lowerlip-volume-incr", "mouth-lowerlip-volume-decr", "l-ear-scale-incr", "l-ear-scale-decr",
                "r-ear-scale-incr", "r-ear-scale-decr"
            };
            foreach (string name in names)
                targetTexts[name] = Resources.Load<TextAsset>(Root + "MakeHumanTargets/" + name);
        }

        public void SetPresentationLayer(int layer)
        {
            if (root == null) return;
            SetLayerRecursively(root.transform, layer);
        }

        private static void SetLayerRecursively(Transform node, int layer)
        {
            node.gameObject.layer = layer;
            for (int i = 0; i < node.childCount; i++)
                SetLayerRecursively(node.GetChild(i), layer);
        }

        public void Build(AdminHumanCreator values)
        {
            Build(values, true);
        }

        public void Build(AdminHumanCreator values, bool facePreviewCamera)
        {
            if (obj == null || obj.vertices == null || obj.vertices.Length == 0) return;
            if (root != null) UnityEngine.Object.Destroy(root);
            root = new GameObject("ADMIN - apercu humain");
            root.transform.SetParent(parent, false);

            Vector3[] deformed = (Vector3[])obj.vertices.Clone();
            Add(deformed, Target(values.female ? "universal-female-young-averagemuscle-averageweight" : "universal-male-young-averagemuscle-averageweight"), 1f);
            Vector3[] maleChest = values.female ? (Vector3[])obj.vertices.Clone() : null;
            if (maleChest != null)
                Add(maleChest, Target("universal-male-young-averagemuscle-averageweight"), 1f);
            Signed(deformed, values.belly, "stomach-pregnant-incr", "stomach-pregnant-decr", 0.55f);
            Signed(deformed, values.belly, "torso-scale-horiz-incr", "torso-scale-horiz-decr", 0.28f);
            if (maleChest != null)
            {
                Signed(maleChest, values.belly, "stomach-pregnant-incr", "stomach-pregnant-decr", 0.55f);
                Signed(maleChest, values.belly, "torso-scale-horiz-incr", "torso-scale-horiz-decr", 0.28f);
                ReplaceFemaleChestWithMaleChest(deformed, maleChest);
            }
            ScaleRegion(deformed, values.hipShape, -1.2f, 2.0f, 2.55f, 0.15f, 0.12f);
            Signed(deformed, values.armThickness, "l-upperarm-scale-horiz-incr", "l-upperarm-scale-horiz-decr", 0.5f);
            Signed(deformed, values.armThickness, "r-upperarm-scale-horiz-incr", "r-upperarm-scale-horiz-decr", 0.5f);
            Signed(deformed, values.armThickness, "l-lowerarm-scale-horiz-incr", "l-lowerarm-scale-horiz-decr", 0.5f);
            Signed(deformed, values.armThickness, "r-lowerarm-scale-horiz-incr", "r-lowerarm-scale-horiz-decr", 0.5f);
            Signed(deformed, values.armLength, "l-upperarm-scale-vert-incr", "l-upperarm-scale-vert-decr", 0.55f);
            Signed(deformed, values.armLength, "r-upperarm-scale-vert-incr", "r-upperarm-scale-vert-decr", 0.55f);
            Signed(deformed, values.armLength, "l-lowerarm-scale-vert-incr", "l-lowerarm-scale-vert-decr", 0.55f);
            Signed(deformed, values.armLength, "r-lowerarm-scale-vert-incr", "r-lowerarm-scale-vert-decr", 0.55f);
            Signed(deformed, values.legThickness, "l-upperleg-scale-horiz-incr", "l-upperleg-scale-horiz-decr", 0.52f);
            Signed(deformed, values.legThickness, "r-upperleg-scale-horiz-incr", "r-upperleg-scale-horiz-decr", 0.52f);
            Signed(deformed, values.legThickness, "l-lowerleg-scale-horiz-incr", "l-lowerleg-scale-horiz-decr", 0.52f);
            Signed(deformed, values.legThickness, "r-lowerleg-scale-horiz-incr", "r-lowerleg-scale-horiz-decr", 0.52f);
            Signed(deformed, values.legLength, "upperlegs-height-incr", "upperlegs-height-decr", 0.55f);
            Signed(deformed, values.feetSize, "l-foot-scale-incr", "l-foot-scale-decr", 0.58f);
            Signed(deformed, values.feetSize, "r-foot-scale-incr", "r-foot-scale-decr", 0.58f);
            Signed(deformed, values.headShape, "head-round", "head-oval", 0.58f);
            Signed(deformed, values.eyesShape, "l-eye-scale-incr", "l-eye-scale-decr", 0.62f);
            Signed(deformed, values.eyesShape, "r-eye-scale-incr", "r-eye-scale-decr", 0.62f);
            Signed(deformed, values.noseShape, "nose-volume-incr", "nose-volume-decr", 0.65f);
            Signed(deformed, values.mouthShape, "mouth-lowerlip-volume-incr", "mouth-lowerlip-volume-decr", 0.7f);
            Signed(deformed, values.earsShape, "l-ear-scale-incr", "l-ear-scale-decr", 0.62f);
            Signed(deformed, values.earsShape, "r-ear-scale-incr", "r-ear-scale-decr", 0.62f);

            Vector3 min = deformed[0];
            for (int i = 1; i < deformed.Length; i++) min = Vector3.Min(min, deformed[i]);
            Vector3[] vertices = new Vector3[deformed.Length];
            for (int i = 0; i < deformed.Length; i++)
                vertices[i] = new Vector3(deformed[i].x * Scale, (deformed[i].y - min.y) * Scale, deformed[i].z * Scale);
            RemoveNippleTips(vertices, values.garmentScale, values.garmentOffsetX,
                values.garmentOffsetY);
            BuildBones(deformed, min.y);
            Func<Vector3, bool> underwearCoverage = values.female
                ? (Func<Vector3, bool>)(point => UnderwearCoverage.IsCovered(point,
                    values.garmentScale, values.garmentOffsetX,
                    values.garmentOffsetY))
                : null;
            Mesh mesh = obj.CreateMesh(vertices, underwearCoverage);
            GameObject meshObject = new GameObject("Humain - apercu ADMIN");
            meshObject.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = boneIndexes.ContainsKey("root") ? bones[boneIndexes["root"]] : bones[0];
            if (values.female)
            {
                // La forme reste celle du mesh feminin, sans l'enfoncer.
                // La peau et la texture UV du soutien-gorge sont maintenant
                // visibles directement sur les deux sous-maillages.
                skin.mainTexture = skinTexture;
                skin.color = Color.white;
                underwearMaterial.color = Color.white;
                hairMaterial.color = Color.white;
            }
            else
            {
                skin.mainTexture = skinTexture;
                skin.color = SkinColor(values.skinTone);
                underwearMaterial.color = new Color(0.80f, 0.71f, 0.62f);
                hairMaterial.color = new Color(0.06f, 0.025f, 0.012f);
            }
            renderer.sharedMaterials = values.female
                ? new[] { skin, underwearMaterial }
                : new[] { skin };
            renderer.updateWhenOffscreen = true;
            ApplyWeights(mesh, renderer);
            root.transform.localRotation = facePreviewCamera
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
            hairStyle = values.hairStyle;
            hairFemale = values.female;
            hair = HairBuilder.Create(values.hairStyle, values.female, root.transform,
                FindBone("head"), hairMaterial);
            // Le fichier MakeHuman est fourni en pose de travail, jambes et
            // bras ouverts. On le remet debout avant la premiere image.
            Animate(false, false, 0f);
        }

        private static void RemoveNippleTips(Vector3[] vertices, float scale,
            float offsetX, float offsetY)
        {
            float factor = Mathf.Clamp(scale, 0.25f, 3f);
            float width = 0.36f * factor;
            float height = width * 300f / 322f;
            float centerX = width * 0.23f;
            float centerY = 1.61f + offsetY - height * 0.33f;
            for (int side = -1; side <= 1; side += 2)
            {
                float x = offsetX + side * centerX;
                float ringDepth = 0f;
                int ringCount = 0;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 point = vertices[i];
                    float distance = Vector2.Distance(
                        new Vector2(point.x, point.y), new Vector2(x, centerY));
                    if (point.z > 0f && distance >= width * 0.12f
                        && distance <= width * 0.24f)
                    {
                        ringDepth += point.z;
                        ringCount++;
                    }
                }
                if (ringCount == 0) continue;
                float smoothDepth = ringDepth / ringCount;
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 point = vertices[i];
                    float distance = Vector2.Distance(
                        new Vector2(point.x, point.y), new Vector2(x, centerY));
                    if (point.z <= 0f || distance >= width * 0.12f) continue;
                    float amount = 1f - Mathf.Clamp01(distance / (width * 0.12f));
                    // On repousse le sommet sous la surface voisine : aucun
                    // point ne peut donc rester visible sous le soutien-gorge.
                    vertices[i].z = Mathf.Min(point.z,
                        smoothDepth - 0.045f * amount);
                }
            }
        }

        private static void ReplaceFemaleChestWithMaleChest(Vector3[] vertices,
            Vector3[] maleChest)
        {
            // Le torse feminin garde exactement la cage thoracique male dans
            // cette zone : aucun volume de sein ni point de teton ne peut donc
            // rester sous le soutien-gorge.
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 point = vertices[i];
                if (point.y < 2.7f || point.y > 4.9f
                    || point.z < 0.25f || Mathf.Abs(point.x) > 1.20f) continue;
                vertices[i] = maleChest[i];
            }
        }

        public void Animate(bool moving, bool running, float clock)
        {
            if (!IsBuilt || bones == null || boneIndexes == null) return;
            float cycle = moving ? Mathf.Sin(clock) : 0f;
            float legSwing = (running ? 38f : 30f) * cycle;
            float armSwing = (running ? 32f : 24f) * cycle;
            float kneeAmplitude = running ? 48f : 36f;
            float leftKnee = moving ? kneeAmplitude * Mathf.Max(0f, -cycle) : 0f;
            float rightKnee = moving ? kneeAmplitude * Mathf.Max(0f, cycle) : 0f;
            // Z rapproche les bras et les jambes du tronc ; X conserve le
            // balancement avant-arriere de la marche et de la course.
            SetBoneRotation("pelvis.L", 0f, -32f);
            SetBoneRotation("pelvis.R", 0f, 32f);
            SetBoneRotation("upperleg01.L", legSwing, 28f);
            SetBoneRotation("upperleg01.R", -legSwing, -28f);
            SetBoneRotation("lowerleg01.L", leftKnee, 0f);
            SetBoneRotation("lowerleg01.R", rightKnee, 0f);
            SetBoneRotation("upperarm01.L", -armSwing, -30f);
            SetBoneRotation("upperarm01.R", armSwing, 30f);
            SetBoneRotation("lowerarm01.L", moving ? Mathf.Max(0f, cycle) * (running ? 22f : 14f) : 0f, 0f);
            SetBoneRotation("lowerarm01.R", moving ? Mathf.Max(0f, -cycle) * (running ? 22f : 14f) : 0f, 0f);
            HairBuilder.Animate(hair, hairStyle, hairFemale, moving, running, clock);
        }

        private Transform FindBone(string name)
        {
            if (boneIndexes != null && boneIndexes.ContainsKey(name))
                return bones[boneIndexes[name]];
            return root.transform;
        }

        private void SetBoneRotation(string name, float x, float z)
        {
            if (!boneIndexes.ContainsKey(name) || bones[boneIndexes[name]] == null) return;
            bones[boneIndexes[name]].localRotation = Quaternion.Euler(x, 0f, z);
        }

        private static Color SkinColor(int tone)
        {
            Color[] colors =
            {
                new Color(1.00f, 0.92f, 0.88f), // peau tres claire ivoire
                new Color(1.00f, 0.72f, 0.70f), // peau claire rose
                new Color(0.95f, 0.70f, 0.56f), // peau claire chaude
                new Color(0.78f, 0.49f, 0.37f),
                new Color(0.62f, 0.32f, 0.21f),
                new Color(0.40f, 0.19f, 0.12f),
                new Color(0.25f, 0.10f, 0.06f)
            };
            return colors[Mathf.Clamp(tone, 0, colors.Length - 1)];
        }

        private void BuildBones(Vector3[] raw, float minY)
        {
            TextAsset rig = Resources.Load<TextAsset>(Root + "rig");
            if (rig == null) return;
            List<RigDefinition> definitions = new List<RigDefinition>();
            foreach (string line in rig.text.Split('\n'))
            {
                string[] parts = line.Trim().Split('|');
                if (parts.Length == 4) definitions.Add(new RigDefinition(parts[0], parts[1], ParseIndexes(parts[2])));
            }
            boneIndexes = new Dictionary<string, int>();
            bones = new Transform[definitions.Count];
            Dictionary<string, RigDefinition> byName = new Dictionary<string, RigDefinition>();
            foreach (RigDefinition definition in definitions) byName[definition.name] = definition;
            for (int i = 0; i < definitions.Count; i++) boneIndexes[definitions[i].name] = i;
            for (int i = 0; i < definitions.Count; i++) CreateBone(i, definitions[i], byName, raw, minY);
        }

        private void CreateBone(int index, RigDefinition definition, Dictionary<string, RigDefinition> all, Vector3[] raw, float minY)
        {
            if (bones[index] != null) return;
            if (!string.IsNullOrEmpty(definition.parent) && all.ContainsKey(definition.parent))
                CreateBone(boneIndexes[definition.parent], all[definition.parent], all, raw, minY);
            GameObject bone = new GameObject("Bone_" + definition.name);
            Transform parentBone = string.IsNullOrEmpty(definition.parent) || !boneIndexes.ContainsKey(definition.parent)
                ? root.transform : bones[boneIndexes[definition.parent]];
            bone.transform.SetParent(parentBone, true);
            bone.transform.position = root.transform.TransformPoint(Average(definition.head, raw, minY));
            bones[index] = bone.transform;
        }

        private Vector3 Average(List<int> indexes, Vector3[] raw, float minY)
        {
            if (indexes.Count == 0) return Vector3.zero;
            Vector3 result = Vector3.zero; int count = 0;
            foreach (int index in indexes)
            {
                if (index < 0 || index >= raw.Length) continue;
                result += new Vector3(raw[index].x * Scale, (raw[index].y - minY) * Scale, raw[index].z * Scale);
                count++;
            }
            return count == 0 ? Vector3.zero : result / count;
        }

        private void ApplyWeights(Mesh mesh, SkinnedMeshRenderer renderer)
        {
            TextAsset weights = Resources.Load<TextAsset>(Root + "weights");
            BoneWeight[] result = new BoneWeight[obj.vertices.Length];
            List<Influence>[] influences = new List<Influence>[obj.vertices.Length];
            for (int i = 0; i < influences.Length; i++) influences[i] = new List<Influence>();
            if (weights != null)
            {
                foreach (string line in weights.text.Split('\n'))
                {
                    string[] pair = line.Trim().Split('|');
                    if (pair.Length != 2 || !boneIndexes.ContainsKey(pair[0])) continue;
                    int bone = boneIndexes[pair[0]];
                    foreach (string value in pair[1].Split(';'))
                    {
                        string[] bits = value.Split(':'); int vertex; float amount;
                        if (bits.Length == 2 && int.TryParse(bits[0], out vertex) && float.TryParse(bits[1], NumberStyles.Float, CultureInfo.InvariantCulture, out amount) && vertex >= 0 && vertex < influences.Length)
                            influences[vertex].Add(new Influence(bone, amount));
                    }
                }
            }
            for (int i = 0; i < influences.Length; i++)
            {
                influences[i].Sort((a, b) => b.weight.CompareTo(a.weight));
                if (influences[i].Count == 0) influences[i].Add(new Influence(boneIndexes.ContainsKey("root") ? boneIndexes["root"] : 0, 1f));
                int count = Mathf.Min(4, influences[i].Count); float total = 0f;
                for (int j = 0; j < count; j++) total += influences[i][j].weight;
                for (int j = 0; j < count; j++)
                {
                    float weight = influences[i][j].weight / Mathf.Max(0.0001f, total);
                    if (j == 0) { result[i].boneIndex0 = influences[i][j].bone; result[i].weight0 = weight; }
                    if (j == 1) { result[i].boneIndex1 = influences[i][j].bone; result[i].weight1 = weight; }
                    if (j == 2) { result[i].boneIndex2 = influences[i][j].bone; result[i].weight2 = weight; }
                    if (j == 3) { result[i].boneIndex3 = influences[i][j].bone; result[i].weight3 = weight; }
                }
            }
            mesh.boneWeights = result;
            Matrix4x4 meshMatrix = renderer.transform.localToWorldMatrix;
            Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bindposes[i] = bones[i].worldToLocalMatrix * meshMatrix;
            mesh.bindposes = bindposes;
        }

        private Dictionary<int, Vector3> Target(string name)
        {
            if (targets.ContainsKey(name)) return targets[name];
            Dictionary<int, Vector3> result = new Dictionary<int, Vector3>();
            TextAsset text = targetTexts.ContainsKey(name) ? targetTexts[name] : null;
            if (text != null)
            {
                foreach (string line in text.text.Split('\n'))
                {
                    string[] p = line.Trim().Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
                    if (p.Length < 4) continue;
                    int index; float x, y, z;
                    if (int.TryParse(p[0], out index) && float.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out x) && float.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out y) && float.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out z)) result[index] = new Vector3(x, y, z);
                }
            }
            targets[name] = result; return result;
        }

        private static void Add(Vector3[] vertices, Dictionary<int, Vector3> target, float amount)
        { foreach (KeyValuePair<int, Vector3> item in target) if (item.Key >= 0 && item.Key < vertices.Length) vertices[item.Key] += item.Value * amount; }
        private void Signed(Vector3[] vertices, float amount, string positive, string negative, float strength)
        { Add(vertices, Target(amount >= 0f ? positive : negative), Mathf.Abs(amount) * strength); }

        private static void ScaleRegion(Vector3[] vertices, float amount, float bottom, float top,
            float halfWidth, float widthFactor, float depthFactor)
        {
            if (Mathf.Abs(amount) < 0.001f) return;
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (v.y < bottom || v.y > top || Mathf.Abs(v.x) > halfWidth) continue;
                float centre = (bottom + top) * 0.5f;
                float radius = (top - bottom) * 0.5f;
                float vertical = 1f - Mathf.Clamp01(Mathf.Abs(v.y - centre) / radius);
                float side = 1f - Mathf.Clamp01(Mathf.Abs(v.x) / halfWidth);
                float weight = vertical * side;
                vertices[i].x *= 1f + amount * widthFactor * weight;
                vertices[i].z *= 1f + amount * depthFactor * weight;
            }
        }

        private static List<int> ParseIndexes(string value)
        { List<int> result = new List<int>(); foreach (string part in value.Split(',')) { int n; if (int.TryParse(part, out n)) result.Add(n); } return result; }
        private static Material NewMaterial(Color color, float metallic, float smoothness)
        { Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color"); Material material = new Material(shader); material.color = color; if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic); if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness); return material; }

        private sealed class RigDefinition { public string name, parent; public List<int> head; public RigDefinition(string name, string parent, List<int> head) { this.name = name; this.parent = parent; this.head = head; } }
        private sealed class Influence { public int bone; public float weight; public Influence(int bone, float weight) { this.bone = bone; this.weight = weight; } }
    }

    private static class UnderwearCoverage
    {
        private const int MaskWidth = 64;
        private const int MaskHeight = 60;
        private static string[] alphaRows;

        public static bool IsCovered(Vector3 point, float scale, float offsetX, float offsetY)
        {
            float factor = Mathf.Clamp(scale, 0.25f, 3f);
            float width = 0.36f * factor;
            float height = width * 300f / 322f;
            float centerY = 1.61f + offsetY;
            Vector2 p = new Vector2(point.x - offsetX, point.y - centerY);

            if (point.z >= 0f)
            {
                float imageU = p.x / width + 0.5f;
                float imageV = p.y / height + 0.5f;
                if (imageU >= 0f && imageU <= 1f && imageV >= 0f && imageV <= 1f
                    && SampleAlpha(imageU, imageV)) return true;
                float cupY = -height * 0.14f;
                float cupX = width * 0.29f;
                // Petite marge continue pour englober les points de la
                // poitrine sans laisser de peau entre deux triangles.
                float cupRadiusX = width * 0.38f * 1.15f;
                float cupRadiusY = height * 0.38f * 1.15f;
                if (InsideEllipse(p, new Vector2(-cupX, cupY), cupRadiusX, cupRadiusY)
                    || InsideEllipse(p, new Vector2(cupX, cupY), cupRadiusX, cupRadiusY)) return true;
                // Deux petites zones de recouvrement suppriment les points
                // de peau qui depassent parfois la limite du bonnet.
                float upperCupY = cupY + height * 0.18f;
                float upperRadiusX = width * 0.16f;
                float upperRadiusY = height * 0.18f;
                if (InsideEllipse(p, new Vector2(-cupX, upperCupY),
                        upperRadiusX, upperRadiusY)
                    || InsideEllipse(p, new Vector2(cupX, upperCupY),
                        upperRadiusX, upperRadiusY)) return true;
                float pointY = cupY - height * 0.19f;
                float pointX = width * 0.23f;
                if (InsideEllipse(p, new Vector2(-pointX, pointY),
                        width * 0.14f, height * 0.14f)
                    || InsideEllipse(p, new Vector2(pointX, pointY),
                        width * 0.14f, height * 0.14f)) return true;
                if ((Mathf.Abs(p.x - pointX) <= width * 0.18f
                        && Mathf.Abs(p.y - pointY) <= height * 0.20f)
                    || (Mathf.Abs(p.x + pointX) <= width * 0.18f
                        && Mathf.Abs(p.y - pointY) <= height * 0.20f)) return true;
                if (Mathf.Abs(p.x) <= width * 0.20f
                    && Mathf.Abs(p.y + 0.025f) <= 0.040f) return true;
                if (StrapDistance(p, -cupX, cupY + height * 0.02f,
                    -width * 0.40f, cupY + height * 0.02f + width * 0.68f) <= width * 0.035f) return true;
                if (StrapDistance(p, cupX, cupY + height * 0.02f,
                    width * 0.40f, cupY + height * 0.02f + width * 0.68f) <= width * 0.035f) return true;
            }
            else
            {
                if (StrapDistance(p, -0.15f, -0.28f, -0.18f, 0.19f) <= 0.025f * factor) return true;
                if (StrapDistance(p, 0.15f, -0.28f, 0.18f, 0.19f) <= 0.025f * factor) return true;
                if (Mathf.Abs(p.y + width * 0.38f) <= 0.050f
                    && Mathf.Abs(p.x) <= width * 0.52f) return true;
            }

            // Les faces avant et arriere de la culotte sont colorees dans le
            // second sous-maillage : aucune peau ne reste sous cette zone.
            if (point.y >= 0.96f && point.y <= 1.22f)
            {
                float t = Mathf.InverseLerp(0.96f, 1.22f, point.y);
                float halfWidth = Mathf.Lerp(0.205f, 0.27f, t);
                if (Mathf.Abs(point.x - offsetX) <= halfWidth) return true;
            }
            return false;
        }

        private static bool SampleAlpha(float u, float v)
        {
            EnsureMask();
            if (alphaRows == null || alphaRows.Length != MaskHeight) return false;
            int x = Mathf.Clamp(Mathf.FloorToInt(u * MaskWidth), 0, MaskWidth - 1);
            int y = Mathf.Clamp(Mathf.FloorToInt((1f - v) * MaskHeight), 0, MaskHeight - 1);
            return alphaRows[y][x] == '1';
        }

        private static void EnsureMask()
        {
            if (alphaRows != null) return;
            TextAsset mask = Resources.Load<TextAsset>("Characters/Clothing/soutien_gorge_alpha");
            if (mask == null) { alphaRows = new string[0]; return; }
            List<string> rows = new List<string>();
            foreach (string line in mask.text.Split('\n'))
            {
                string row = line.Trim();
                if (row.Length >= MaskWidth) rows.Add(row.Substring(0, MaskWidth));
            }
            alphaRows = rows.Count == MaskHeight ? rows.ToArray() : new string[0];
        }

        private static bool InsideEllipse(Vector2 point, Vector2 center,
            float radiusX, float radiusY)
        {
            float x = (point.x - center.x) / Mathf.Max(radiusX, 0.001f);
            float y = (point.y - center.y) / Mathf.Max(radiusY, 0.001f);
            return x * x + y * y <= 1f;
        }

        private static float StrapDistance(Vector2 point, float x0, float y0,
            float x1, float y1)
        {
            Vector2 start = new Vector2(x0, y0);
            Vector2 end = new Vector2(x1, y1);
            Vector2 segment = end - start;
            float amount = Mathf.Clamp01(Vector2.Dot(point - start, segment)
                / Mathf.Max(segment.sqrMagnitude, 0.0001f));
            return Vector2.Distance(point, start + segment * amount);
        }
    }

    private static class HairBuilder
    {
        public static GameObject Create(int style, bool female, Transform parent, Transform head,
            Material material)
        {
            style = Mathf.Clamp(style, 0, 4);
            // La coupe 0 est la base MakeHuman sans cheveux : elle ne doit
            // produire aucun halo autour du crane.
            if (style == 0) return null;
            GameObject objectHair = new GameObject("Cheveux - coupe " + style);
            objectHair.transform.SetParent(head == null ? parent : head, false);
            float width = female ? 0.115f : 0.105f;
            float depth = female ? 0.105f : 0.095f;
            CreateCap(objectHair, width, depth, material);

            if (female)
            {
                if (style == 0)
                {
                    CreateLock(objectHair, "meche gauche", -width * 0.78f, 0.08f, 0.035f,
                        0.045f, 0.055f, 0.14f, material, 0.01f);
                    CreateLock(objectHair, "meche droite", width * 0.78f, 0.08f, 0.035f,
                        0.045f, 0.055f, 0.14f, material, -0.01f);
                }
                else if (style == 1)
                {
                    CreateLock(objectHair, "longue gauche", -width * 0.86f, 0.08f, 0.0f,
                        0.042f, 0.05f, 0.34f, material, 0.012f);
                    CreateLock(objectHair, "longue droite", width * 0.86f, 0.08f, 0.0f,
                        0.042f, 0.05f, 0.34f, material, -0.012f);
                    CreateLock(objectHair, "longue arriere", 0f, 0.04f, -0.085f,
                        0.075f, 0.045f, 0.34f, material, 0.015f);
                }
                else if (style == 2)
                {
                    CreateLock(objectHair, "queue gauche", -width * 0.72f, 0.05f, -0.09f,
                        0.04f, 0.04f, 0.29f, material, 0.02f);
                    CreateLock(objectHair, "queue droite", width * 0.72f, 0.05f, -0.09f,
                        0.04f, 0.04f, 0.29f, material, -0.02f);
                    CreateLock(objectHair, "frange", 0f, 0.10f, 0.085f,
                        0.055f, 0.025f, 0.09f, material, 0f);
                }
                else if (style == 3)
                {
                    CreateLock(objectHair, "carre gauche", -width * 0.88f, 0.08f, 0.025f,
                        0.048f, 0.05f, 0.24f, material, 0.008f);
                    CreateLock(objectHair, "carre droite", width * 0.88f, 0.08f, 0.025f,
                        0.048f, 0.05f, 0.24f, material, -0.008f);
                }
                else
                {
                    for (int i = 0; i < 3; i++)
                    {
                        float x = (i - 1) * width * 0.72f;
                        CreateLock(objectHair, "boucle " + i, x, 0.06f, -0.07f,
                            0.045f, 0.045f, 0.27f + i * 0.025f, material,
                            (i - 1) * 0.025f);
                    }
                }
            }
            else
            {
                if (style == 0)
                {
                    CreateLock(objectHair, "frange courte", 0f, 0.105f, 0.07f,
                        0.07f, 0.025f, 0.055f, material, 0f);
                }
                else if (style == 1)
                {
                    CreateLock(objectHair, "meche coiffee", -0.035f, 0.10f, 0.075f,
                        0.065f, 0.03f, 0.09f, material, -0.012f);
                }
                else if (style == 2)
                {
                    for (int i = 0; i < 3; i++)
                        CreateLock(objectHair, "point " + i, (i - 1) * 0.045f, 0.11f, 0.0f,
                            0.035f, 0.035f, 0.10f + i * 0.025f, material, 0f);
                }
                else if (style == 3)
                {
                    CreateLock(objectHair, "cote gauche", -0.09f, 0.07f, 0.01f,
                        0.035f, 0.04f, 0.19f, material, 0.01f);
                    CreateLock(objectHair, "cote droit", 0.09f, 0.07f, 0.01f,
                        0.035f, 0.04f, 0.19f, material, -0.01f);
                }
                else
                {
                    CreateLock(objectHair, "arriere homme", 0f, 0.06f, -0.075f,
                        0.065f, 0.045f, 0.25f, material, 0f);
                }
            }
            return objectHair;
        }

        private static void CreateCap(GameObject parent, float width, float depth, Material material)
        {
            const int rows = 5;
            const int columns = 24;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int row = 0; row < rows; row++)
            {
                float t = row / (float)(rows - 1);
                float radius = Mathf.Sin(t * Mathf.PI * 0.5f);
                float y = 0.18f - t * 0.16f;
                for (int col = 0; col < columns; col++)
                {
                    float angle = col * Mathf.PI * 2f / columns;
                    vertices.Add(new Vector3(Mathf.Cos(angle) * width * radius, y,
                        Mathf.Sin(angle) * depth * radius));
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
            AddMesh(parent, "calotte", vertices, triangles, material);
        }

        private static void CreateLock(GameObject parent, string name, float centerX, float startY,
            float centerZ, float radiusX, float radiusZ, float length, Material material, float bend)
        {
            const int rows = 5;
            const int columns = 10;
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            for (int row = 0; row < rows; row++)
            {
                float t = row / (float)(rows - 1);
                float y = startY - length * t;
                float x = centerX + bend * Mathf.Sin(t * Mathf.PI);
                float taper = 1f - t * 0.28f;
                for (int col = 0; col < columns; col++)
                {
                    float angle = col * Mathf.PI * 2f / columns;
                    vertices.Add(new Vector3(x + Mathf.Cos(angle) * radiusX * taper, y,
                        centerZ + Mathf.Sin(angle) * radiusZ * taper));
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
            AddMesh(parent, name, vertices, triangles, material);
        }

        private static void AddMesh(GameObject parent, string name, List<Vector3> vertices,
            List<int> triangles, Material material)
        {
            Mesh mesh = new Mesh { name = "Cheveux - " + name + " mesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            GameObject part = new GameObject("Cheveux - " + name);
            part.transform.SetParent(parent.transform, false);
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        public static void Animate(GameObject hair, int style, bool female, bool moving,
            bool running, float clock)
        {
            if (hair == null) return;
            bool longHair = female ? style == 1 || style == 2 || style == 3 || style == 4
                : style == 3 || style == 4;
            float amplitude = longHair ? (running ? 5f : 3f) : 1.2f;
            float phase = Time.time * (running ? 2.2f : 1.7f);
            float wave = Mathf.Sin(phase + clock * 0.15f) * amplitude
                * (moving ? 1f : (longHair ? 0.28f : 0.08f));
            hair.transform.localRotation = Quaternion.Euler(wave * 0.45f, wave * 0.25f, wave);
        }
    }

    private sealed class ObjData
    {
        public Vector3[] vertices;
        private Vector2[] uv;
        private readonly List<ObjTriangle> triangles = new List<ObjTriangle>();
        public static ObjData Load(TextAsset text)
        {
            ObjData data = new ObjData(); if (text == null) return data;
            List<Vector3> positions = new List<Vector3>(); List<Vector2> uvs = new List<Vector2>(); List<int> uvForVertex = new List<int>(); string group = "body";
            foreach (string line in text.text.Split('\n'))
            {
                string clean = line.Trim();
                if (clean.StartsWith("v ")) { string[] p = clean.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); if (p.Length >= 4) positions.Add(new Vector3(F(p[1]), F(p[2]), F(p[3]))); }
                else if (clean.StartsWith("vt ")) { string[] p = clean.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); if (p.Length >= 3) uvs.Add(new Vector2(F(p[1]), F(p[2]))); }
                else if (clean.StartsWith("g ")) group = clean.Substring(2).Trim();
                else if (clean.StartsWith("f "))
                {
                    string[] p = clean.Substring(2).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); if (p.Length < 3 || group.StartsWith("helper-") || group.StartsWith("joint-")) continue;
                    int first = VertexIndex(p[0], positions.Count), previous = VertexIndex(p[1], positions.Count);
                    for (int i = 2; i < p.Length; i++) { int current = VertexIndex(p[i], positions.Count); if (first >= 0 && previous >= 0 && current >= 0) data.triangles.Add(new ObjTriangle(first, previous, current)); previous = current; }
                }
            }
            data.vertices = positions.ToArray(); data.uv = new Vector2[data.vertices.Length]; uvForVertex = new List<int>(new int[data.vertices.Length]);
            foreach (string line in text.text.Split('\n')) if (line.TrimStart().StartsWith("f ")) { string[] p = line.Trim().Substring(2).Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries); foreach (string token in p) { string[] bits = token.Split('/'); int vi = VertexIndex(token, data.vertices.Length), ti; if (vi >= 0 && bits.Length > 1 && int.TryParse(bits[1], out ti)) { ti = ti < 0 ? uvs.Count + ti : ti - 1; if (ti >= 0 && ti < uvs.Count && uvForVertex[vi] == 0) { data.uv[vi] = uvs[ti]; uvForVertex[vi] = ti + 1; } } } }
            return data;
        }
        public Mesh CreateMesh(Vector3[] vertices)
        {
            return CreateMesh(vertices, null);
        }

        // Les triangles vetement restent dans le meme SkinnedMeshRenderer que
        // le corps, mais utilisent le second materiau. Il n'y a donc ni peau
        // dessinee sous le vetement ni objet vetement ajoute devant le corps.
        public Mesh CreateMesh(Vector3[] vertices, Func<Vector3, bool> garmentTriangle)
        {
            Mesh mesh = new Mesh
            {
                name = "Humain - preview",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.vertices = vertices;
            mesh.uv = uv;
            List<int> bodyIndices = new List<int>();
            List<int> garmentIndices = new List<int>();
            foreach (ObjTriangle triangle in triangles)
            {
                Vector3 centre = (vertices[triangle.a] + vertices[triangle.b]
                    + vertices[triangle.c]) / 3f;
                bool garment = garmentTriangle != null
                    && (garmentTriangle(centre)
                        || garmentTriangle(vertices[triangle.a])
                        || garmentTriangle(vertices[triangle.b])
                        || garmentTriangle(vertices[triangle.c]));
                List<int> destination = garment ? garmentIndices : bodyIndices;
                destination.Add(triangle.a);
                destination.Add(triangle.b);
                destination.Add(triangle.c);
            }
            if (garmentTriangle == null)
            {
                mesh.SetTriangles(bodyIndices, 0);
            }
            else
            {
                mesh.subMeshCount = 2;
                mesh.SetTriangles(bodyIndices, 0);
                mesh.SetTriangles(garmentIndices, 1);
            }
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        private static int VertexIndex(string token, int count) { int index; if (!int.TryParse(token.Split('/')[0], out index)) return -1; return index < 0 ? count + index : index - 1; }
        private static float F(string value) { return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture); }
        private struct ObjTriangle { public int a, b, c; public ObjTriangle(int a, int b, int c) { this.a = a; this.b = b; this.c = c; } }
    }
}