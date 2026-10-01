#if UNITY_EDITOR
using System.IO;
using UnityEditor;
using UnityEngine;

// Les fichiers .mhmat MakeHuman declarent explicitement les cartes normales.
// Unity ne peut pas deduire ce type correctement pour les PNG charges depuis
// Resources : sans cet import, la carte normale est lue comme une couleur et
// accentue les triangles du maillage par un eclairage incoherent.
public sealed class MakeHumanTextureImporter : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string path = assetPath.Replace('\\', '/');
        if (path.IndexOf("/MakeHumanClothes/", System.StringComparison.OrdinalIgnoreCase) < 0)
            return;
        string name = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
        TextureImporter importer = assetImporter as TextureImporter;
        if (importer == null) return;
        if (name == "normal")
        {
            importer.textureType = TextureImporterType.NormalMap;
            importer.sRGBTexture = false;
        }
        else if (name == "ao")
        {
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = false;
        }
    }
}
#endif
