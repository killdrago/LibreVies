using System;
using System.Collections.Generic;
using System.Globalization;
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
    // Chaque famille d'equipement a son propre slot. -1 signifie "aucun".
    public int clothingStyle;
    public int hatStyle = -1;
    public int shoeStyle = -1;

    private HumanPreview preview;
    private Camera previewCamera;
    private RenderTexture previewTexture;
    private HumanPreview appliedHuman;
    private float previewDistance = 5.00f;
    private float previewYaw;
    private float previewPitch;
    private bool previewHeadZoom;

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
        // La molette reste active apres le passage en cadrage tete.
        previewDistance = Mathf.Clamp(previewDistance + amount, 1.00f, 9.00f);
        PositionPreviewCamera();
    }

    public void ZoomHeadPreview()
    {
        previewHeadZoom = true;
        previewDistance = 2.15f;
        PositionPreviewCamera();
    }

    public void ResetHeadPreview()
    {
        previewHeadZoom = false;
        previewDistance = 5.00f;
        previewYaw = 0f;
        previewPitch = 0f;
        PositionPreviewCamera();
    }

    public void ResetPreviewCamera()
    {
        ResetHeadPreview();
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
        Vector3 cible = transform.position + Vector3.up
            * (previewHeadZoom ? 1.82f : 1.08f);
        // Le recul laisse toujours entrer les pieds et le sommet de la tete
        // dans le cadre, meme lorsque les proportions sont modifiees. En mode
        // tete, la cible monte sur le visage et la molette garde son effet.
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
        clothingStyle = UnityEngine.Random.Range(0, MakeHumanClothingFactory.ClothingOptions(female).Length);
        hatStyle = UnityEngine.Random.Range(-1, MakeHumanClothingFactory.HatOptions(female).Length);
        shoeStyle = UnityEngine.Random.Range(-1, MakeHumanClothingFactory.ShoeOptions(female).Length);
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
        clothingStyle = 0;
        hatStyle = -1;
        shoeStyle = -1;
        BuildPreview();
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
        private Texture2D skinTexture;

        public HumanPreview(Transform parent)
        {
            this.parent = parent;
            obj = ObjData.Load(Resources.Load<TextAsset>(Root + "MakeHumanBaseData"));
            skin = NewMaterial(new Color(0.72f, 0.42f, 0.31f), 0.02f, 0.38f);
            skinTexture = Resources.Load<Texture2D>(Root + "SkinBase");
            if (skinTexture != null) skin.mainTexture = skinTexture;
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
            // Retour au morph MakeHuman qui cible uniquement les deux seins
            // sur la face avant. L'ancien rectangle abdominal est supprime :
            // aucun quad ajoute et aucun volume separe.
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
            // Chaque famille d'equipement garde son propre proxy et sa propre
            // selection. Les masques de peau sont fusionnes pour que les
            // chaussures ou le chapeau n'annulent jamais la tenue.
            MakeHumanClothingFactory.Option[] clothingOptions =
                MakeHumanClothingFactory.ClothingOptions(values.female);
            MakeHumanClothingFactory.Option[] hatOptions =
                MakeHumanClothingFactory.HatOptions(values.female);
            MakeHumanClothingFactory.Option[] shoeOptions =
                MakeHumanClothingFactory.ShoeOptions(values.female);
            int clothingIndex = clothingOptions.Length == 0 ? -1
                : Mathf.Clamp(values.clothingStyle, 0, clothingOptions.Length - 1);
            int hatIndex = OptionalIndex(values.hatStyle, hatOptions.Length);
            int shoeIndex = OptionalIndex(values.shoeStyle, shoeOptions.Length);
            bool[] deleteBody = new bool[deformed.Length];
            if (clothingIndex >= 0)
                MergeDeleteMask(deleteBody,
                    MakeHumanClothingFactory.LoadDeleteMask(clothingOptions[clothingIndex],
                        deformed.Length));
            if (hatIndex >= 0)
                MergeDeleteMask(deleteBody,
                    MakeHumanClothingFactory.LoadDeleteMask(hatOptions[hatIndex],
                        deformed.Length));
            if (shoeIndex >= 0)
                MergeDeleteMask(deleteBody,
                    MakeHumanClothingFactory.LoadDeleteMask(shoeOptions[shoeIndex],
                        deformed.Length));
            Mesh mesh = obj.CreateMesh(vertices, (a, b, c) =>
                deleteBody[a] && deleteBody[b] && deleteBody[c]);
            GameObject meshObject = new GameObject("Humain - apercu ADMIN");
            meshObject.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = boneIndexes.ContainsKey("root") ? bones[boneIndexes["root"]] : bones[0];
            skin.mainTexture = skinTexture;
            skin.color = values.female ? Color.white : SkinColor(values.skinTone);
            renderer.sharedMaterial = skin;
            renderer.updateWhenOffscreen = true;
            ApplyWeights(mesh, renderer);
            if (clothingIndex >= 0)
            {
                MakeHumanClothingFactory.BuildResult clothing =
                    MakeHumanClothingFactory.Create(clothingOptions[clothingIndex], deformed,
                        Scale, min.y, mesh.boneWeights, root.transform, bones, boneIndexes);
                if (clothing.gameObject == null)
                    Debug.LogWarning("MakeHuman tenue: " + clothing.error);
            }
            GameObject fedoraObject = null;
            if (hatIndex >= 0)
            {
                MakeHumanClothingFactory.BuildResult hat =
                    MakeHumanClothingFactory.Create(hatOptions[hatIndex], deformed,
                        Scale, min.y, mesh.boneWeights, root.transform, bones, boneIndexes);
                if (hat.gameObject == null)
                    Debug.LogWarning("MakeHuman chapeau: " + hat.error);
                else if (hatOptions[hatIndex].id == "fedora01")
                {
                    fedoraObject = hat.gameObject;
                    // Le proxy MakeHuman reste intact : on ajuste seulement
                    // son placement pour que le bord recouvre les cheveux au
                    // lieu de les couper ou de les laisser passer a travers.
                    hat.gameObject.transform.localPosition += Vector3.down * 0.012f;
                    hat.gameObject.transform.localScale = new Vector3(1.04f, 1f, 1.04f);
                }
            }
            if (shoeIndex >= 0)
            {
                MakeHumanClothingFactory.BuildResult shoes =
                    MakeHumanClothingFactory.Create(shoeOptions[shoeIndex], deformed,
                        Scale, min.y, mesh.boneWeights, root.transform, bones, boneIndexes);
                if (shoes.gameObject == null)
                    Debug.LogWarning("MakeHuman chaussures: " + shoes.error);
                else if (shoeOptions[shoeIndex].id == "shoes01")
                {
                    // Ce proxy partage la forme du pied avec shoes04. Un
                    // tres leger elargissement le sort de la peau lorsque le
                    // corps et la chaussure occupent exactement la meme
                    // surface, sans modifier le mesh source MakeHuman.
                    shoes.gameObject.transform.localScale =
                        new Vector3(1.025f, 1f, 1.025f);
                }
            }
            root.transform.localRotation = facePreviewCamera
                ? Quaternion.Euler(0f, 180f, 0f)
                : Quaternion.identity;
            int hairStyle = Mathf.Clamp(values.hairStyle, 0,
                MakeHumanClothingFactory.HairOptions().Length - 1);
            MakeHumanClothingFactory.Option[] hairOptions =
                MakeHumanClothingFactory.HairOptions();
            MakeHumanClothingFactory.BuildResult hairBuild =
                MakeHumanClothingFactory.Create(hairOptions[hairStyle], deformed,
                    Scale, min.y, mesh.boneWeights, root.transform, bones, boneIndexes);
            if (hairBuild.gameObject != null)
            {
                // Le mesh cheveux est un vrai asset MakeHuman : son proxy,
                // ses poids et ses textures viennent du pack hair01. Il est
                // donc indépendant de la tenue mais partage la meme armature.
                // L'objet est deja parenté au root par la factory.
                // Avec le fedora, on rentre legerement la coiffure sous la
                // calotte puis on retire les triangles qui depassent au-dessus
                // de sa surface. Le mesh de cheveux reste celui de MakeHuman.
                if (fedoraObject != null)
                {
                    hairBuild.gameObject.transform.localPosition += Vector3.down * 0.045f;
                    SupprimerCheveuxAuDessusDuChapeau(hairBuild.gameObject, fedoraObject);
                }
            }
            else
            {
                // Une coiffure absente ne doit pas etre remplacee par une
                // geometrie approximative : le personnage reste sans cheveux
                // et l'erreur est explicite dans la console Unity.
                Debug.LogError("MakeHuman hair indisponible: " + hairBuild.error);
            }
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
        }

        private void SetBoneRotation(string name, float x, float z)
        {
            if (!boneIndexes.ContainsKey(name) || bones[boneIndexes[name]] == null) return;
            bones[boneIndexes[name]].localRotation = Quaternion.Euler(x, 0f, z);
        }

        private static void SupprimerCheveuxAuDessusDuChapeau(GameObject hairObject,
            GameObject hatObject)
        {
            if (hairObject == null || hatObject == null) return;
            SkinnedMeshRenderer hairRenderer = hairObject.GetComponent<SkinnedMeshRenderer>();
            SkinnedMeshRenderer hatRenderer = hatObject.GetComponent<SkinnedMeshRenderer>();
            if (hairRenderer == null || hatRenderer == null || hairRenderer.sharedMesh == null) return;

            Mesh hairMesh = hairRenderer.sharedMesh;
            int[] triangles = hairMesh.GetTriangles(0);
            Vector3[] vertices = hairMesh.vertices;
            if (triangles == null || triangles.Length == 0 || vertices == null) return;

            // On ne reconstruit pas le fedora : sa boite monde sert seulement
            // de limite pour couper les faces de cheveux qui passent au-dessus
            // ou a l'interieur de la calotte. Les cheveux situes hors du bord
            // restent visibles.
            Bounds hatBounds = hatRenderer.bounds;
            float cutoff = hatBounds.max.y - hatBounds.size.y * 0.40f;
            Matrix4x4 hairMatrix = hairObject.transform.localToWorldMatrix;
            List<int> kept = new List<int>(triangles.Length);
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];
                if (a < 0 || b < 0 || c < 0 || a >= vertices.Length
                    || b >= vertices.Length || c >= vertices.Length) continue;
                Vector3 pa = hairMatrix.MultiplyPoint3x4(vertices[a]);
                Vector3 pb = hairMatrix.MultiplyPoint3x4(vertices[b]);
                Vector3 pc = hairMatrix.MultiplyPoint3x4(vertices[c]);
                bool protrudes = EstSousLaCalotte(pa, hatBounds, cutoff)
                    || EstSousLaCalotte(pb, hatBounds, cutoff)
                    || EstSousLaCalotte(pc, hatBounds, cutoff);
                if (!protrudes)
                {
                    kept.Add(a);
                    kept.Add(b);
                    kept.Add(c);
                }
            }
            if (kept.Count != triangles.Length)
            {
                hairMesh.SetTriangles(kept, 0);
                hairMesh.RecalculateBounds();
            }
        }

        private static bool EstSousLaCalotte(Vector3 point, Bounds hatBounds, float cutoff)
        {
            float dx = (point.x - hatBounds.center.x) / Mathf.Max(hatBounds.extents.x, 0.001f);
            float dz = (point.z - hatBounds.center.z) / Mathf.Max(hatBounds.extents.z, 0.001f);
            // Le bord du fedora est plus large que sa calotte : on garde
            // une petite marge autour de sa texture pour supprimer aussi les
            // meches qui debordent sur les cotes.
            bool underHat = dx * dx + dz * dz < 1.12f
                && point.y >= hatBounds.min.y - 0.025f
                && point.y <= hatBounds.max.y + 0.01f;
            return point.y > cutoff || underHat;
        }

        private static int OptionalIndex(int index, int length)
        {
            return index < 0 || index >= length ? -1 : index;
        }

        private static void MergeDeleteMask(bool[] destination, bool[] source)
        {
            if (destination == null || source == null) return;
            int count = Mathf.Min(destination.Length, source.Length);
            for (int i = 0; i < count; i++)
                destination[i] = destination[i] || source[i];
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
        {
            float effectiveAmount = Mathf.Sign(amount)
                * Mathf.Pow(Mathf.Abs(amount), 0.70f);
            Add(vertices, Target(amount >= 0f ? positive : negative),
                Mathf.Abs(effectiveAmount) * strength);
        }

        private static void BreastVolume(Vector3[] vertices, float amount, bool female)
        {
            if (!female || Mathf.Abs(amount) < 0.001f) return;
            // Cette zone correspond aux deux seins du mesh hm08 : elle ne
            // descend pas jusqu'au nombril et ne remonte pas sur le haut du
            // torse. Le déplacement se fait vers l'avant, comme une vraie
            // augmentation mammaire, avec une petite ouverture latérale.
            float effectiveAmount = Mathf.Sign(amount)
                * Mathf.Pow(Mathf.Abs(amount), 0.70f);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (v.y < 2.7f || v.y > 4.9f || v.z < 0.25f
                    || Mathf.Abs(v.x) > 1.35f) continue;
                float side = 1f - Mathf.Clamp01(Mathf.Abs(Mathf.Abs(v.x) - 0.68f) / 0.62f);
                float vertical = 1f - Mathf.Clamp01(Mathf.Abs(v.y - 3.75f) / 1.1f);
                float front = Mathf.Clamp01((v.z - 0.25f) / 1.2f);
                float weight = side * vertical * front;
                if (weight <= 0.0001f) continue;
                v.z += effectiveAmount * 0.82f * weight;
                v.x += Mathf.Sign(v.x) * effectiveAmount * 0.08f * weight;
                vertices[i] = v;
            }
        }

        private static void ScaleRegion(Vector3[] vertices, float amount, float bottom, float top,
            float halfWidth, float widthFactor, float depthFactor)
        {
            if (Mathf.Abs(amount) < 0.001f) return;
            float effectiveAmount = Mathf.Sign(amount)
                * Mathf.Pow(Mathf.Abs(amount), 0.70f);
            for (int i = 0; i < vertices.Length; i++)
            {
                Vector3 v = vertices[i];
                if (v.y < bottom || v.y > top || Mathf.Abs(v.x) > halfWidth) continue;
                float centre = (bottom + top) * 0.5f;
                float radius = (top - bottom) * 0.5f;
                float vertical = 1f - Mathf.Clamp01(Mathf.Abs(v.y - centre) / radius);
                float side = 1f - Mathf.Clamp01(Mathf.Abs(v.x) / halfWidth);
                float weight = vertical * side;
                vertices[i].x *= 1f + effectiveAmount * widthFactor * weight;
                vertices[i].z *= 1f + effectiveAmount * depthFactor * weight;
            }
        }

        private static List<int> ParseIndexes(string value)
        { List<int> result = new List<int>(); foreach (string part in value.Split(',')) { int n; if (int.TryParse(part, out n)) result.Add(n); } return result; }
        private static Material NewMaterial(Color color, float metallic, float smoothness)
        { Shader shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Unlit/Color"); Material material = new Material(shader); material.color = color; if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic); if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness); return material; }

        private sealed class RigDefinition { public string name, parent; public List<int> head; public RigDefinition(string name, string parent, List<int> head) { this.name = name; this.parent = parent; this.head = head; } }
        private sealed class Influence { public int bone; public float weight; public Influence(int bone, float weight) { this.bone = bone; this.weight = weight; } }
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

        // Le masque est celui fourni par le proxy MakeHuman : seules les
        // faces dont les trois sommets sont sous le vetement sont retirees.
        public Mesh CreateMesh(Vector3[] vertices, Func<int, int, int, bool> hideTriangle)
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
                if (hideTriangle != null && hideTriangle(triangle.a, triangle.b, triangle.c))
                    continue;
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