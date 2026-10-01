using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEngine;

// Convertisseur minimal des proxies MakeHuman Community vers Unity.
// Les fichiers .npz/.mhpxy sont des archives NPY produites par MakeHuman :
// le .npz contient la topologie/UV et le .mhpxy contient l'ajustement au mesh
// de base. On applique donc le meme principe que MakeHuman, sans Blender.
public static class MakeHumanClothingFactory
{
    [Serializable]
    public sealed class Option
    {
        public string id;
        public string label;
        public bool female;
        public string resourceFolder;

        public Option(string id, string label, bool female)
        {
            this.id = id;
            this.label = label;
            this.female = female;
            resourceFolder = "Characters/MakeHumanClothes/"
                + (female ? "Female/" : "Male/") + id;
        }
    }

    public sealed class BuildResult
    {
        public GameObject gameObject;
        public bool[] deleteBodyVertices;
        public string error;
    }

    private sealed class ProxyData
    {
        public int count;
        public int[] referenceVertices;
        public Vector3[] offsets;
        public Vector3[] weights;
        public bool[] deleteVertices;
        public float[] scaleValues;
        public int[] scaleIndexes;
    }

    private sealed class MeshData
    {
        public Vector3[] coordinates;
        public int[] faceVertices;
        public int[] faceUvs;
        public int faceCount;
        public int faceWidth;
        public Vector2[] texcoords;
    }

    private sealed class NpyArray
    {
        public string descriptor;
        public int[] shape;
        public byte[] data;
        public int elementSize;

        public int Count
        {
            get
            {
                int result = 1;
                if (shape == null || shape.Length == 0) return 1;
                for (int i = 0; i < shape.Length; i++) result *= shape[i];
                return result;
            }
        }

        public float FloatAt(int index)
        {
            return BitConverter.ToSingle(data, index * elementSize);
        }

        public uint UIntAt(int index)
        {
            if (elementSize == 1) return data[index];
            if (elementSize == 2) return BitConverter.ToUInt16(data, index * elementSize);
            return BitConverter.ToUInt32(data, index * elementSize);
        }

        public bool BoolAt(int index)
        {
            return data[index * elementSize] != 0;
        }
    }

    private static readonly Option[] FemaleOptions =
    {
        new Option("female_sportsuit01", "Tenue sport", true),
        new Option("female_casualsuit01", "Tenue casual", true),
        new Option("female_elegantsuit01", "Tenue elegante", true)
    };

    private static readonly Option[] MaleOptions =
    {
        new Option("male_casualsuit04", "Tenue casual", false),
        new Option("male_elegantsuit01", "Tenue elegante", false),
        new Option("male_worksuit01", "Tenue travail", false)
    };

    public static Option[] Options(bool female)
    {
        return female ? FemaleOptions : MaleOptions;
    }

    public static string SelectedLabel(bool female, int index)
    {
        Option[] options = Options(female);
        if (index < 0 || index >= options.Length) return "Aucun vetement";
        return options[index].label;
    }

    public static bool[] LoadDeleteMask(Option option, int bodyVertexCount)
    {
        if (option == null) return null;
        TextAsset proxyAsset = Resources.Load<TextAsset>(option.resourceFolder + "/proxy");
        if (proxyAsset == null) return null;
        try { return ReadProxy(proxyAsset.bytes, bodyVertexCount).deleteVertices; }
        catch (Exception) { return null; }
    }

    public static BuildResult Create(Option option, Vector3[] deformedBody,
        float bodyScale, float bodyMinY, BoneWeight[] bodyWeights,
        Transform parent, Transform[] bones, Dictionary<string, int> boneIndexes)
    {
        BuildResult result = new BuildResult();
        if (option == null)
        {
            result.error = "Option de vetement absente";
            return result;
        }
        try
        {
            TextAsset meshAsset = Resources.Load<TextAsset>(option.resourceFolder + "/mesh");
            TextAsset proxyAsset = Resources.Load<TextAsset>(option.resourceFolder + "/proxy");
            if (meshAsset == null || proxyAsset == null)
            {
                result.error = "Ressources MakeHuman manquantes pour " + option.id;
                return result;
            }
            MeshData source = ReadMesh(meshAsset.bytes);
            ProxyData proxy = ReadProxy(proxyAsset.bytes, deformedBody.Length);
            if (source == null || proxy == null || proxy.count == 0)
            {
                result.error = "Mesh ou proxy MakeHuman invalide pour " + option.id;
                return result;
            }
            Vector3[] coordinates = BuildCoordinates(proxy, deformedBody);
            Vector3[] scaled = new Vector3[coordinates.Length];
            for (int i = 0; i < coordinates.Length; i++)
                scaled[i] = new Vector3(coordinates[i].x * bodyScale,
                    (coordinates[i].y - bodyMinY) * bodyScale,
                    coordinates[i].z * bodyScale);

            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<BoneWeight> weights = new List<BoneWeight>();
            List<int> triangles = new List<int>();
            for (int face = 0; face < source.faceCount; face++)
            {
                int a = source.faceVertices[face * source.faceWidth];
                int b = source.faceVertices[face * source.faceWidth + 1];
                int c = source.faceVertices[face * source.faceWidth + 2];
                int d = source.faceVertices[face * source.faceWidth + 3];
                bool quad = d != a && d != b && d != c;
                int uvA = source.faceUvs[face * source.faceWidth];
                int uvB = source.faceUvs[face * source.faceWidth + 1];
                int uvC = source.faceUvs[face * source.faceWidth + 2];
                int uvD = source.faceUvs[face * source.faceWidth + 3];
                AddCorner(a, uvA, scaled, source.texcoords, proxy, bodyWeights,
                    vertices, uvs, weights, triangles);
                AddCorner(b, uvB, scaled, source.texcoords, proxy, bodyWeights,
                    vertices, uvs, weights, triangles);
                AddCorner(c, uvC, scaled, source.texcoords, proxy, bodyWeights,
                    vertices, uvs, weights, triangles);
                int baseIndex = vertices.Count - 3;
                triangles.Add(baseIndex);
                triangles.Add(baseIndex + 1);
                triangles.Add(baseIndex + 2);
                if (quad)
                {
                    AddCorner(d, uvD, scaled, source.texcoords, proxy, bodyWeights,
                        vertices, uvs, weights, triangles);
                    int quadIndex = vertices.Count - 1;
                    triangles.Add(baseIndex);
                    triangles.Add(quadIndex);
                    triangles.Add(baseIndex + 2);
                }
            }

            Mesh mesh = new Mesh { name = "MakeHuman - " + option.id };
            if (vertices.Count > 65535)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.boneWeights = weights.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            GameObject objectClothing = new GameObject("MakeHuman - " + option.label);
            objectClothing.transform.SetParent(parent, false);
            SkinnedMeshRenderer renderer = objectClothing.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = FindRootBone(bones, boneIndexes);
            renderer.sharedMaterial = BuildMaterial(option);
            Matrix4x4 meshMatrix = renderer.transform.localToWorldMatrix;
            Matrix4x4[] bindposes = new Matrix4x4[bones.Length];
            for (int i = 0; i < bones.Length; i++)
                bindposes[i] = bones[i].worldToLocalMatrix * meshMatrix;
            mesh.bindposes = bindposes;
            renderer.updateWhenOffscreen = true;
            result.gameObject = objectClothing;
            result.deleteBodyVertices = proxy.deleteVertices;
            return result;
        }
        catch (Exception exception)
        {
            result.error = exception.GetType().Name + ": " + exception.Message;
            return result;
        }
    }

    private static void AddCorner(int sourceIndex, int uvIndex, Vector3[] verticesSource,
        Vector2[] texcoords, ProxyData proxy, BoneWeight[] bodyWeights,
        List<Vector3> vertices, List<Vector2> uvs, List<BoneWeight> weights,
        List<int> triangles)
    {
        if (sourceIndex < 0 || sourceIndex >= verticesSource.Length) return;
        vertices.Add(verticesSource[sourceIndex]);
        uvs.Add(uvIndex >= 0 && uvIndex < texcoords.Length
            ? texcoords[uvIndex] : Vector2.zero);
        weights.Add(CombineBoneWeights(sourceIndex, proxy, bodyWeights));
    }

    private static BoneWeight CombineBoneWeights(int vertex, ProxyData proxy,
        BoneWeight[] bodyWeights)
    {
        Dictionary<int, float> accumulated = new Dictionary<int, float>();
        for (int slot = 0; slot < 3; slot++)
        {
            int reference = proxy.referenceVertices[vertex * 3 + slot];
            if (reference < 0 || reference >= bodyWeights.Length) continue;
            float factor = proxy.weights[vertex][slot];
            AddInfluence(accumulated, bodyWeights[reference].boneIndex0,
                bodyWeights[reference].weight0 * factor);
            AddInfluence(accumulated, bodyWeights[reference].boneIndex1,
                bodyWeights[reference].weight1 * factor);
            AddInfluence(accumulated, bodyWeights[reference].boneIndex2,
                bodyWeights[reference].weight2 * factor);
            AddInfluence(accumulated, bodyWeights[reference].boneIndex3,
                bodyWeights[reference].weight3 * factor);
        }
        List<KeyValuePair<int, float>> sorted = new List<KeyValuePair<int, float>>(accumulated);
        sorted.Sort((a, b) => b.Value.CompareTo(a.Value));
        BoneWeight result = new BoneWeight();
        float total = 0f;
        for (int i = 0; i < sorted.Count && i < 4; i++) total += sorted[i].Value;
        if (total <= 0.0001f)
        {
            result.boneIndex0 = 0;
            result.weight0 = 1f;
            return result;
        }
        for (int i = 0; i < sorted.Count && i < 4; i++)
        {
            int bone = sorted[i].Key;
            float weight = sorted[i].Value / total;
            if (i == 0) { result.boneIndex0 = bone; result.weight0 = weight; }
            else if (i == 1) { result.boneIndex1 = bone; result.weight1 = weight; }
            else if (i == 2) { result.boneIndex2 = bone; result.weight2 = weight; }
            else { result.boneIndex3 = bone; result.weight3 = weight; }
        }
        return result;
    }

    private static void AddInfluence(Dictionary<int, float> values, int bone, float weight)
    {
        if (weight <= 0f) return;
        float previous;
        values.TryGetValue(bone, out previous);
        values[bone] = previous + weight;
    }

    private static Transform FindRootBone(Transform[] bones, Dictionary<string, int> indexes)
    {
        int index;
        if (indexes != null && indexes.TryGetValue("root", out index)
            && index >= 0 && index < bones.Length) return bones[index];
        return bones.Length == 0 ? null : bones[0];
    }

    private static Material BuildMaterial(Option option)
    {
        Material material = new Material(Shader.Find("Standard")
            ?? Shader.Find("Universal Render Pipeline/Lit")
            ?? Shader.Find("Unlit/Color"));
        Texture2D diffuse = Resources.Load<Texture2D>(option.resourceFolder + "/diffuse");
        Texture2D normal = Resources.Load<Texture2D>(option.resourceFolder + "/normal");
        Texture2D ao = Resources.Load<Texture2D>(option.resourceFolder + "/ao");
        if (diffuse != null) material.mainTexture = diffuse;
        if (normal != null && material.HasProperty("_BumpMap"))
        {
            material.SetTexture("_BumpMap", normal);
            material.EnableKeyword("_NORMALMAP");
        }
        if (ao != null && material.HasProperty("_OcclusionMap"))
            material.SetTexture("_OcclusionMap", ao);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.55f);
        return material;
    }

    private static Vector3[] BuildCoordinates(ProxyData proxy, Vector3[] body)
    {
        Vector3[] coordinates = new Vector3[proxy.count];
        for (int i = 0; i < coordinates.Length; i++)
        {
            Vector3 coordinate = body[proxy.referenceVertices[i * 3]] * proxy.weights[i].x
                + body[proxy.referenceVertices[i * 3 + 1]] * proxy.weights[i].y
                + body[proxy.referenceVertices[i * 3 + 2]] * proxy.weights[i].z;
            Vector3 offset = proxy.offsets[i];
            if (proxy.scaleValues != null && proxy.scaleIndexes != null)
            {
                for (int axis = 0; axis < 3; axis++)
                {
                    float denominator = proxy.scaleValues[axis];
                    if (float.IsNaN(denominator) || Mathf.Abs(denominator) < 0.00001f) continue;
                    int first = proxy.scaleIndexes[axis * 2];
                    int second = proxy.scaleIndexes[axis * 2 + 1];
                    if (first >= 0 && second >= 0 && first < body.Length && second < body.Length)
                    {
                        float numerator = Mathf.Abs(body[first][axis] - body[second][axis]);
                        offset[axis] *= numerator / denominator;
                    }
                }
            }
            coordinates[i] = coordinate + offset;
        }
        return coordinates;
    }

    private static MeshData ReadMesh(byte[] bytes)
    {
        using (MemoryStream stream = new MemoryStream(bytes))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            NpyArray coord = ReadArray(archive, "coord.npy");
            NpyArray fvert = ReadArray(archive, "fvert.npy");
            NpyArray fuvs = ReadArray(archive, "fuvs.npy");
            NpyArray texco = ReadArray(archive, "texco.npy");
            MeshData data = new MeshData
            {
                coordinates = new Vector3[coord.shape[0]],
                faceCount = fvert.shape[0],
                faceWidth = fvert.shape[1],
                faceVertices = new int[fvert.Count],
                faceUvs = new int[fuvs == null ? fvert.Count : fuvs.Count],
                texcoords = new Vector2[texco.shape[0]]
            };
            for (int i = 0; i < data.coordinates.Length; i++)
                data.coordinates[i] = new Vector3(coord.FloatAt(i * 3),
                    coord.FloatAt(i * 3 + 1), coord.FloatAt(i * 3 + 2));
            for (int i = 0; i < data.faceVertices.Length; i++)
                data.faceVertices[i] = (int)fvert.UIntAt(i);
            for (int i = 0; i < data.faceUvs.Length; i++)
                data.faceUvs[i] = fuvs == null ? 0 : (int)fuvs.UIntAt(i);
            for (int i = 0; i < data.texcoords.Length; i++)
                data.texcoords[i] = new Vector2(texco.FloatAt(i * 2), texco.FloatAt(i * 2 + 1));
            return data;
        }
    }

    private static ProxyData ReadProxy(byte[] bytes, int bodyVertexCount)
    {
        using (MemoryStream stream = new MemoryStream(bytes))
        using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
        {
            NpyArray references = ReadArray(archive, "ref_vIdxs.npy");
            NpyArray offsets = ReadArray(archive, "offsets.npy");
            NpyArray weights = ReadArray(archive, "weights.npy");
            NpyArray count = ReadArray(archive, "num_refverts.npy");
            if (references == null || offsets == null || weights == null) return null;
            int vertexCount = references.shape.Length == 2 ? references.shape[0] : references.shape[0];
            ProxyData proxy = new ProxyData
            {
                count = vertexCount,
                referenceVertices = new int[vertexCount * 3],
                offsets = new Vector3[vertexCount],
                weights = new Vector3[vertexCount],
                deleteVertices = new bool[bodyVertexCount]
            };
            int referenceWidth = references.shape.Length == 2 ? references.shape[1] : 1;
            int weightWidth = weights.shape.Length == 2 ? weights.shape[1] : 1;
            for (int i = 0; i < vertexCount; i++)
            {
                proxy.referenceVertices[i * 3] = (int)references.UIntAt(i * referenceWidth);
                proxy.referenceVertices[i * 3 + 1] = referenceWidth > 1
                    ? (int)references.UIntAt(i * referenceWidth + 1) : proxy.referenceVertices[i * 3];
                proxy.referenceVertices[i * 3 + 2] = referenceWidth > 2
                    ? (int)references.UIntAt(i * referenceWidth + 2) : proxy.referenceVertices[i * 3];
                proxy.weights[i] = new Vector3(weights.FloatAt(i * weightWidth),
                    weightWidth > 1 ? weights.FloatAt(i * weightWidth + 1) : 0f,
                    weightWidth > 2 ? weights.FloatAt(i * weightWidth + 2) : 0f);
                proxy.offsets[i] = new Vector3(offsets.FloatAt(i * 3),
                    offsets.FloatAt(i * 3 + 1), offsets.FloatAt(i * 3 + 2));
            }
            NpyArray deleted = ReadArray(archive, "deleteVerts.npy");
            if (deleted != null)
                for (int i = 0; i < deleted.Count && i < proxy.deleteVertices.Length; i++)
                    proxy.deleteVertices[i] = deleted.BoolAt(i);
            NpyArray scale = ReadArray(archive, "tmat_scale.npy");
            NpyArray scaleIndexes = ReadArray(archive, "tmat_scale_idx.npy");
            if (scale != null && scaleIndexes != null)
            {
                proxy.scaleValues = new float[scale.Count];
                proxy.scaleIndexes = new int[scaleIndexes.Count];
                for (int i = 0; i < scale.Count; i++) proxy.scaleValues[i] = scale.FloatAt(i);
                for (int i = 0; i < scaleIndexes.Count; i++) proxy.scaleIndexes[i] = (int)scaleIndexes.UIntAt(i);
            }
            return proxy;
        }
    }

    private static NpyArray ReadArray(ZipArchive archive, string filename)
    {
        ZipArchiveEntry entry = archive.GetEntry(filename);
        if (entry == null) return null;
        using (Stream stream = entry.Open())
        using (MemoryStream buffer = new MemoryStream())
        {
            stream.CopyTo(buffer);
            byte[] bytes = buffer.ToArray();
            if (bytes.Length < 12 || bytes[0] != 0x93 || bytes[1] != 'N'
                || bytes[2] != 'U' || bytes[3] != 'M' || bytes[4] != 'P' || bytes[5] != 'Y')
                throw new InvalidDataException("NPY invalide : " + filename);
            int headerLength;
            int headerStart;
            if (bytes[6] == 1)
            {
                headerLength = BitConverter.ToUInt16(bytes, 8);
                headerStart = 10;
            }
            else
            {
                headerLength = (int)BitConverter.ToUInt32(bytes, 8);
                headerStart = 12;
            }
            string header = Encoding.ASCII.GetString(bytes, headerStart, headerLength);
            string descriptor = HeaderValue(header, "'descr': '", "'");
            string shapeText = HeaderValue(header, "'shape': (", ")");
            List<int> shape = new List<int>();
            foreach (string part in shapeText.Split(','))
            {
                int value;
                if (int.TryParse(part.Trim(), out value)) shape.Add(value);
            }
            int dataStart = headerStart + headerLength;
            int elementSize = descriptor.EndsWith("8") ? 8 : descriptor.EndsWith("4") ? 4
                : descriptor.EndsWith("2") ? 2 : 1;
            byte[] data = new byte[bytes.Length - dataStart];
            Buffer.BlockCopy(bytes, dataStart, data, 0, data.Length);
            return new NpyArray
            {
                descriptor = descriptor,
                shape = shape.ToArray(),
                data = data,
                elementSize = elementSize
            };
        }
    }

    private static string HeaderValue(string header, string start, string end)
    {
        int index = header.IndexOf(start, StringComparison.Ordinal);
        if (index < 0) throw new InvalidDataException("Champ NPY absent : " + start);
        index += start.Length;
        int finish = header.IndexOf(end, index, StringComparison.Ordinal);
        if (finish < 0) throw new InvalidDataException("Champ NPY invalide : " + start);
        return header.Substring(index, finish - index);
    }
}
