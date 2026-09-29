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
        private Material braMaterial;
        private Material underwearMaterial;
        private Material hairMaterial;
        private BoneWeight[] bodyBoneWeights;
        private Matrix4x4[] bodyBindposes;
        private GameObject hair;
        private int hairStyle;
        private bool hairFemale;

        public HumanPreview(Transform parent)
        {
            this.parent = parent;
            obj = ObjData.Load(Resources.Load<TextAsset>(Root + "MakeHumanBaseData"));
            skin = NewMaterial(new Color(0.72f, 0.42f, 0.31f), 0.02f, 0.38f);
            hairMaterial = NewMaterial(new Color(0.06f, 0.025f, 0.012f), 0f, 0.22f);
            Texture2D skinTexture = Resources.Load<Texture2D>(Root + "SkinBase");
            if (skinTexture != null) skin.mainTexture = skinTexture;
            Texture2D braTexture = Resources.Load<Texture2D>("Characters/Clothing/soutien_gorge");
            braMaterial = NewBraMaterial(braTexture);
            underwearMaterial = NewMaterial(new Color(0.80f, 0.71f, 0.62f), 0f, 0.45f);
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
            Signed(deformed, values.belly, "stomach-pregnant-incr", "stomach-pregnant-decr", 0.55f);
            Signed(deformed, values.belly, "torso-scale-horiz-incr", "torso-scale-horiz-decr", 0.28f);
            BreastVolume(deformed, values.chestShape, values.female);
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

            BuildBones(deformed, min.y);
            Mesh mesh = obj.CreateMesh(vertices);
            GameObject meshObject = new GameObject("Humain - apercu ADMIN");
            meshObject.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = boneIndexes.ContainsKey("root") ? bones[boneIndexes["root"]] : bones[0];
            skin.color = SkinColor(values.skinTone);
            renderer.sharedMaterial = skin;
            renderer.updateWhenOffscreen = true;
            ApplyWeights(mesh, renderer);
            root.transform.localRotation = facePreviewCamera
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
            if (values.female)
                CreateUnderwear(values, vertices);
            hairStyle = values.hairStyle;
            hairFemale = values.female;
            hair = HairBuilder.Create(values.hairStyle, values.female, root.transform,
                FindBone("head"), hairMaterial);
            // Le fichier MakeHuman est fourni en pose de travail, jambes et
            // bras ouverts. On le remet debout avant la premiere image.
            Animate(false, false, 0f);
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
            bodyBoneWeights = result;
            Matrix4x4 meshMatrix = renderer.transform.localToWorldMatrix;
            bodyBindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++) bodyBindposes[i] = bones[i].worldToLocalMatrix * meshMatrix;
            mesh.bindposes = bodyBindposes;
        }

        private void CreateUnderwear(AdminHumanCreator values, Vector3[] bodyVertices)
        {
            GarmentMeshData bra = new GarmentMeshData("Soutien-gorge integre");
            float factor = Mathf.Clamp(values.garmentScale, 0.25f, 3f);
            float width = 0.36f * factor;
            float height = width * 300f / 322f;
            float centerY = 1.61f + values.garmentOffsetY;
            AddFrontBraSurface(bra, bodyVertices, width, height, centerY,
                values.garmentOffsetX);
            AddBackStrap(bra, bodyVertices, values.garmentOffsetX, centerY,
                factor, false);
            AddBackStrap(bra, bodyVertices, values.garmentOffsetX, centerY,
                factor, true);
            AddBackBand(bra, bodyVertices, values.garmentOffsetX, centerY,
                width, factor);
            CreateGarmentRenderer(bra, braMaterial, bodyVertices);

            GarmentMeshData panty = new GarmentMeshData("Culotte integree");
            AddPantySurface(panty, bodyVertices, values.garmentOffsetX);
            CreateGarmentRenderer(panty, underwearMaterial, bodyVertices);
        }

        private void CreateGarmentRenderer(GarmentMeshData data, Material material,
            Vector3[] bodyVertices)
        {
            if (data.vertices.Count == 0) return;
            Mesh mesh = new Mesh
            {
                name = data.name + " mesh",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.SetVertices(data.vertices);
            mesh.SetUVs(0, data.uv);
            mesh.SetTriangles(data.triangles, 0);
            mesh.boneWeights = GarmentWeights(data.vertices, bodyVertices);
            mesh.bindposes = bodyBindposes;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject objectGarment = new GameObject(data.name);
            objectGarment.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer renderer = objectGarment.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = boneIndexes.ContainsKey("root") ? bones[boneIndexes["root"]] : bones[0];
            renderer.sharedMaterial = material;
            renderer.updateWhenOffscreen = true;
        }

        private BoneWeight[] GarmentWeights(List<Vector3> garmentVertices, Vector3[] bodyVertices)
        {
            BoneWeight[] result = new BoneWeight[garmentVertices.Count];
            if (bodyBoneWeights == null || bodyBoneWeights.Length == 0) return result;
            for (int i = 0; i < garmentVertices.Count; i++)
            {
                int nearest = 0;
                float best = float.MaxValue;
                for (int j = 0; j < bodyVertices.Length; j++)
                {
                    float distance = (garmentVertices[i] - bodyVertices[j]).sqrMagnitude;
                    if (distance < best)
                    {
                        best = distance;
                        nearest = j;
                    }
                }
                result[i] = bodyBoneWeights[Mathf.Min(nearest, bodyBoneWeights.Length - 1)];
            }
            return result;
        }

        private void AddFrontBraSurface(GarmentMeshData data, Vector3[] bodyVertices,
            float width, float height, float centerY, float offsetX)
        {
            const int columns = 18;
            const int rows = 12;
            int start = data.vertices.Count;
            for (int row = 0; row < rows; row++)
            {
                float v = row / (float)(rows - 1);
                for (int column = 0; column < columns; column++)
                {
                    float u = column / (float)(columns - 1);
                    float x = offsetX + (u - 0.5f) * width;
                    float y = centerY + (v - 0.5f) * height;
                    data.vertices.Add(SurfacePoint(bodyVertices, x, y, true, 0.006f));
                    data.uv.Add(new Vector2(u, v));
                }
            }
            AddGridTriangles(data.triangles, start, columns, rows);
        }

        private void AddBackStrap(GarmentMeshData data, Vector3[] bodyVertices,
            float offsetX, float centerY, float factor, bool right)
        {
            const int samples = 12;
            int start = data.vertices.Count;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                float x = right
                    ? Mathf.Lerp(0.15f, 0.18f, t)
                    : Mathf.Lerp(-0.15f, -0.18f, t);
                float y = Mathf.Lerp(-0.28f, 0.19f, t);
                float nextX = right
                    ? Mathf.Lerp(0.15f, 0.18f, Mathf.Min(1f, t + 0.02f))
                    : Mathf.Lerp(-0.15f, -0.18f, Mathf.Min(1f, t + 0.02f));
                float nextY = Mathf.Lerp(-0.28f, 0.19f, Mathf.Min(1f, t + 0.02f));
                Vector2 tangent = new Vector2(nextX - x, nextY - y).normalized;
                Vector2 normal = new Vector2(-tangent.y, tangent.x) * 0.015f * factor;
                float strapU = right
                    ? 0.93f - (1f - t) * 0.08f
                    : 0.07f + (1f - t) * 0.08f;
                Vector3 left = SurfacePoint(bodyVertices, offsetX + x + normal.x,
                    centerY + y + normal.y, false, -0.006f);
                Vector3 rightPoint = SurfacePoint(bodyVertices, offsetX + x - normal.x,
                    centerY + y - normal.y, false, -0.006f);
                data.vertices.Add(left);
                data.vertices.Add(rightPoint);
                data.uv.Add(new Vector2(strapU - (right ? -0.018f : 0.018f), 0.45f + t * 0.55f));
                data.uv.Add(new Vector2(strapU + (right ? -0.018f : 0.018f), 0.45f + t * 0.55f));
            }
            AddRibbonTriangles(data.triangles, start, samples);
        }

        private void AddBackBand(GarmentMeshData data, Vector3[] bodyVertices,
            float offsetX, float centerY, float width, float factor)
        {
            const int samples = 14;
            int start = data.vertices.Count;
            float y = centerY - width * 0.38f;
            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)(samples - 1);
                float x = offsetX + Mathf.Lerp(-width * 0.46f, width * 0.46f, t);
                float bandWidth = 0.026f * factor;
                data.vertices.Add(SurfacePoint(bodyVertices, x, y + bandWidth, false, -0.006f));
                data.vertices.Add(SurfacePoint(bodyVertices, x, y - bandWidth, false, -0.006f));
                data.uv.Add(new Vector2(Mathf.Lerp(0.08f, 0.92f, t), 0.20f));
                data.uv.Add(new Vector2(Mathf.Lerp(0.08f, 0.92f, t), 0.20f));
            }
            AddRibbonTriangles(data.triangles, start, samples);
        }

        private void AddPantySurface(GarmentMeshData data, Vector3[] bodyVertices,
            float offsetX)
        {
            AddWrapGrid(data, bodyVertices, offsetX, 1.03f, 1.40f, 0.29f, true);
            AddWrapGrid(data, bodyVertices, offsetX, 1.03f, 1.40f, 0.29f, false);
        }

        private void AddWrapGrid(GarmentMeshData data, Vector3[] bodyVertices,
            float offsetX, float bottom, float top, float halfWidth, bool front)
        {
            const int columns = 14;
            const int rows = 8;
            int start = data.vertices.Count;
            for (int row = 0; row < rows; row++)
            {
                float v = row / (float)(rows - 1);
                for (int column = 0; column < columns; column++)
                {
                    float u = column / (float)(columns - 1);
                    float x = offsetX + (u - 0.5f) * halfWidth * 2f;
                    float y = Mathf.Lerp(bottom, top, v);
                    data.vertices.Add(SurfacePoint(bodyVertices, x, y, front, front ? 0.007f : -0.007f));
                    data.uv.Add(Vector2.zero);
                }
            }
            AddGridTriangles(data.triangles, start, columns, rows);
        }

        private Vector3 SurfacePoint(Vector3[] bodyVertices, float x, float y,
            bool front, float offset)
        {
            // On cherche d'abord la proximite dans le plan XY, puis on choisit
            // le vertex avant ou arriere parmi les voisins immediats. L'ancien
            // rayon de recherche de 10 cm pouvait attraper un bras ou une
            // jambe et etirer le vetement en grand polygone.
            float nearestXY = float.MaxValue;
            for (int i = 0; i < bodyVertices.Length; i++)
            {
                float dx = bodyVertices[i].x - x;
                float dy = bodyVertices[i].y - y;
                nearestXY = Mathf.Min(nearestXY, dx * dx + dy * dy);
            }
            float allowed = nearestXY + 0.0025f;
            int nearest = -1;
            float bestDepth = front ? -float.MaxValue : float.MaxValue;
            for (int i = 0; i < bodyVertices.Length; i++)
            {
                Vector3 candidate = bodyVertices[i];
                float dx = candidate.x - x;
                float dy = candidate.y - y;
                if (dx * dx + dy * dy > allowed) continue;
                if (front && candidate.z > bestDepth)
                {
                    bestDepth = candidate.z;
                    nearest = i;
                }
                else if (!front && candidate.z < bestDepth)
                {
                    bestDepth = candidate.z;
                    nearest = i;
                }
            }
            Vector3 result = nearest < 0 ? new Vector3(x, y, front ? 0.16f : -0.13f)
                : bodyVertices[nearest];
            result.x = x;
            result.y = y;
            result.z += offset;
            return result;
        }

        private static void AddGridTriangles(List<int> triangles, int start,
            int columns, int rows)
        {
            for (int row = 0; row < rows - 1; row++)
                for (int column = 0; column < columns - 1; column++)
                {
                    int a = start + row * columns + column;
                    int b = a + 1;
                    int c = a + columns + 1;
                    int d = a + columns;
                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(a); triangles.Add(d); triangles.Add(c);
                }
        }

        private static void AddRibbonTriangles(List<int> triangles, int start, int samples)
        {
            for (int i = 0; i < samples - 1; i++)
            {
                int a = start + i * 2;
                int b = a + 1;
                int c = a + 2;
                int d = a + 3;
                triangles.Add(a); triangles.Add(c); triangles.Add(b);
                triangles.Add(b); triangles.Add(c); triangles.Add(d);
            }
        }

        private sealed class GarmentMeshData
        {
            public string name;
            public List<Vector3> vertices = new List<Vector3>();
            public List<Vector2> uv = new List<Vector2>();
            public List<int> triangles = new List<int>();
            public GarmentMeshData(string name) { this.name = name; }
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

        private static void BreastVolume(Vector3[] vertices, float amount, bool female)
        {
            if (!female) return;
            // Une poitrine naturelle existe aussi a la valeur neutre. Le
            // curseur ne remplace donc pas la poitrine : il ajoute ou retire
            // seulement du volume a deux zones gauche/droite localisees.
            float volume = Mathf.Clamp(0.34f + amount, 0.10f, 1.10f);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (v.y < 2.7f || v.y > 4.9f || v.z < 0.25f || Mathf.Abs(v.x) > 1.35f) continue;
                float side = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.Abs(v.x) - 0.68f) / 0.62f);
                float vertical = 1f - Mathf.Clamp01(Mathf.Abs(v.y - 3.75f) / 1.1f);
                float front = Mathf.Clamp01((v.z - 0.25f) / 1.2f);
                float weight = side * vertical * front;
                // Le slider agit sur la profondeur de chaque sein, pas sur
                // toute la cage thoracique.
                v.z += volume * 0.82f * weight;
                v.x += Mathf.Sign(v.x) * volume * 0.025f * weight;
                vertices[i] = v;
            }
        }

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

        private static Material NewBraMaterial(Texture2D texture)
        {
            Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Standard");
            Material material = new Material(shader) { name = "Soutien-gorge integre" };
            if (texture != null) material.mainTexture = texture;
            material.color = Color.white;
            // Alpha seulement autour des bords du vetement : le tissu lui-meme
            // reste une surface opaque qui suit le corps.
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            if (material.HasProperty("_Cull")) material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            material.renderQueue = 3000;
            return material;
        }

        private sealed class RigDefinition { public string name, parent; public List<int> head; public RigDefinition(string name, string parent, List<int> head) { this.name = name; this.parent = parent; this.head = head; } }
        private sealed class Influence { public int bone; public float weight; public Influence(int bone, float weight) { this.bone = bone; this.weight = weight; } }
    }

    // Le PNG est maintenant compose dans le shader du meme maillage skine :
    // aucune image frontale ni bretelle flottante n'est creee.

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

        public Mesh CreateMesh(Vector3[] vertices, Func<Vector3, bool> hideTriangle)
        {
            Mesh mesh = new Mesh
            {
                name = "Humain - preview",
                indexFormat = UnityEngine.Rendering.IndexFormat.UInt32
            };
            mesh.vertices = vertices;
            mesh.uv = uv;
            List<int> indices = new List<int>();
            foreach (ObjTriangle triangle in triangles)
            {
                Vector3 centre = (vertices[triangle.a] + vertices[triangle.b]
                    + vertices[triangle.c]) / 3f;
                if (hideTriangle != null && hideTriangle(centre)) continue;
                indices.Add(triangle.a);
                indices.Add(triangle.b);
                indices.Add(triangle.c);
            }
            mesh.SetTriangles(indices, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
        private static int VertexIndex(string token, int count) { int index; if (!int.TryParse(token.Split('/')[0], out index)) return -1; return index < 0 ? count + index : index - 1; }
        private static float F(string value) { return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture); }
        private struct ObjTriangle { public int a, b, c; public ObjTriangle(int a, int b, int c) { this.a = a; this.b = b; this.c = c; } }
    }
}