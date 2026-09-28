using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

// Createur MakeHuman reutilisable dans le jeu.
// Il est volontairement separe de l'interface ADMIN : le meme composant pourra
// etre appele plus tard par l'ecran Nouvelle partie pour creer le joueur.
public sealed class AdminHumanCreator : MonoBehaviour
{
    public bool female = true;
    public int skinTone;
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

    private HumanPreview preview;

    public void BuildPreview()
    {
        if (preview == null) preview = new HumanPreview(transform);
        preview.Build(this);
    }

    public void Randomize()
    {
        female = UnityEngine.Random.value > 0.5f;
        skinTone = UnityEngine.Random.Range(0, 4);
        belly = UnityEngine.Random.Range(-0.65f, 0.75f);
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
        belly = armThickness = armLength = legThickness = legLength = feetSize = 0f;
        headShape = eyesShape = noseShape = mouthShape = earsShape = 0f;
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

        public HumanPreview(Transform parent)
        {
            this.parent = parent;
            obj = ObjData.Load(Resources.Load<TextAsset>(Root + "MakeHumanBaseData"));
            skin = NewMaterial(new Color(0.72f, 0.42f, 0.31f), 0.02f, 0.38f);
            Texture2D texture = Resources.Load<Texture2D>(Root + "SkinBase");
            if (texture != null) skin.mainTexture = texture;
            LoadTargets();
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

        public void Build(AdminHumanCreator values)
        {
            if (obj == null || obj.vertices == null || obj.vertices.Length == 0) return;
            if (root != null) UnityEngine.Object.Destroy(root);
            root = new GameObject("ADMIN - apercu humain MakeHuman");
            root.transform.SetParent(parent, false);

            Vector3[] deformed = (Vector3[])obj.vertices.Clone();
            Add(deformed, Target(values.female ? "universal-female-young-averagemuscle-averageweight" : "universal-male-young-averagemuscle-averageweight"), 1f);
            Signed(deformed, values.belly, "stomach-pregnant-incr", "stomach-pregnant-decr", 0.55f);
            Signed(deformed, values.belly, "torso-scale-horiz-incr", "torso-scale-horiz-decr", 0.28f);
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
            GameObject meshObject = new GameObject("MakeHuman - apercu ADMIN");
            meshObject.transform.SetParent(root.transform, false);
            SkinnedMeshRenderer renderer = meshObject.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = boneIndexes.ContainsKey("root") ? bones[boneIndexes["root"]] : bones[0];
            skin.color = SkinColor(values.skinTone);
            renderer.sharedMaterial = skin;
            renderer.updateWhenOffscreen = true;
            ApplyWeights(mesh, renderer);
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
        }

        private static Color SkinColor(int tone)
        {
            Color[] colors = { new Color(0.78f, 0.49f, 0.37f), new Color(0.62f, 0.32f, 0.21f), new Color(0.40f, 0.19f, 0.12f), new Color(0.25f, 0.10f, 0.06f) };
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
        { Mesh mesh = new Mesh { name = "MakeHuman hm08 - preview", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 }; mesh.vertices = vertices; mesh.uv = uv; List<int> indices = new List<int>(); foreach (ObjTriangle t in triangles) { indices.Add(t.a); indices.Add(t.b); indices.Add(t.c); } mesh.SetTriangles(indices, 0); mesh.RecalculateNormals(); mesh.RecalculateBounds(); return mesh; }
        private static int VertexIndex(string token, int count) { int index; if (!int.TryParse(token.Split('/')[0], out index)) return -1; return index < 0 ? count + index : index - 1; }
        private static float F(string value) { return float.Parse(value, NumberStyles.Float, CultureInfo.InvariantCulture); }
        private struct ObjTriangle { public int a, b, c; public ObjTriangle(int a, int b, int c) { this.a = a; this.b = b; this.c = c; } }
    }
}