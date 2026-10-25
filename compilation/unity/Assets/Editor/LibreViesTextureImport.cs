using System.IO;
using UnityEditor;
using UnityEngine;

// Import automatique des textures deposees dans Assets/Resources/LVTextures.
// Les cartes de relief (_N ou _NormalGL) et de rugosite (_R ou _Roughness)
// sont importees en lineaire (sRGB desactive), comme les cartes d'origine.
// Chaque texture doit etre un Texture2D simple (pas un Sprite).
public sealed class LibreViesTextureImport : AssetPostprocessor
{
    private void OnPreprocessTexture()
    {
        string chemin = assetPath.Replace('\\', '/');
        if (!chemin.Contains("/Resources/LVTextures/")) return;

        var importeur = (TextureImporter)assetImporter;
        importeur.textureType = TextureImporterType.Default;

        string nom = Path.GetFileNameWithoutExtension(chemin);
        bool carte = nom.EndsWith("_N") || nom.EndsWith("_NormalGL")
            || nom.EndsWith("_R") || nom.EndsWith("_Roughness");
        importeur.sRGBTexture = !carte;
    }
}
