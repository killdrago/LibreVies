#if UNITY_EDITOR
using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class LibreViesBuild
{
    public static void BuildWindows()
    {
        // Chemin de secours uniquement : build_launcher.bat et
        // build_unity_game.bat passent toujours -buildPath (jeu\game\). On
        // garde le meme dossier par defaut pour ne jamais recreer de dossier
        // release\ (regle : le joueur recoit tout dans jeu\).
        string versionAttendue = VerifierVersionSources();
        string output = GetArgument("-buildPath", Path.GetFullPath("../jeu/game/LibreViesGame.exe"));
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        // Le script build_launcher.bat sélectionne Mono dans ProjectSettings
        // avant de lancer Unity. Aucune API de backend n'est appelée ici : cela
        // évite toute différence d'énumération entre versions de Unity.
        var scenes = new[] { "Assets/Scenes/LibreVies.unity" };
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("La build Unity a échoué : " + report.summary.result);
        VerifierExport(output, versionAttendue);
        EcrireVersionExport(output, versionAttendue);
        Debug.Log("LibreVies Unity exporté et verifie : v" + versionAttendue + " - " + output);
    }

    private static string VerifierVersionSources()
    {
        string source = File.ReadAllText(Path.Combine(Application.dataPath, "Scripts/LibreViesGame.cs"));
        Match version = Regex.Match(source, "VersionJeu\\s*=\\s*\"([^\"]+)\"");
        if (!version.Success) throw new Exception("Version du jeu introuvable dans les sources.");
        string attendue = version.Groups[1].Value;
        FieldInfo champ = typeof(LibreViesGame).GetField("VersionJeu", BindingFlags.NonPublic | BindingFlags.Static);
        if (champ == null || (string)champ.GetRawConstantValue() != attendue)
            throw new Exception("Unity utilise une ancienne compilation des scripts. Attendu : " + attendue
                + ". Reimportez les sources avant de publier le jeu.");
        if (PlayerSettings.bundleVersion != attendue)
            throw new Exception("Version du projet Unity differente des sources : "
                + PlayerSettings.bundleVersion + " au lieu de " + attendue + ".");
        return attendue;
    }

    private static void VerifierExport(string executable, string version)
    {
        string dossier = Path.Combine(Path.GetDirectoryName(executable), Path.GetFileNameWithoutExtension(executable) + "_Data");
        string dll = Path.Combine(dossier, "Managed/Assembly-CSharp.dll");
        if (!File.Exists(dll)) throw new Exception("Assembly-CSharp.dll absente : export Mono incomplet.");
        byte[] contenu = File.ReadAllBytes(dll);
        if (!Contient(contenu, Encoding.Unicode.GetBytes(version)))
            throw new Exception("Export Unity obsolete : la version " + version + " est absente du binaire.");
        foreach (string nom in new[] { "InitialiserPseudoJoueur", "CreerNomJoueur", "MettreAJourNomJoueur",
            "LibreViesCompte", "LibreViesPersonnage", "CreerPoigneesPorte", "AjusterTexteDansPanneau" })
        {
            if (!Contient(contenu, Encoding.ASCII.GetBytes(nom + "\0")))
                throw new Exception("Export Unity incomplet : " + nom
                    + " est absent. Le pseudo et le profil du compte ne seraient pas disponibles en jeu.");
        }
    }

    [Serializable]
    private sealed class VersionExport
    {
        public string version;
        public string assembly_sha256;
    }

    private static void EcrireVersionExport(string executable, string version)
    {
        string dossier = Path.GetDirectoryName(executable);
        string dll = Path.Combine(dossier, Path.GetFileNameWithoutExtension(executable) + "_Data/Managed/Assembly-CSharp.dll");
        string hash;
        using (SHA256 sha = SHA256.Create())
            hash = BitConverter.ToString(sha.ComputeHash(File.ReadAllBytes(dll))).Replace("-", "").ToLowerInvariant();
        VersionExport informations = new VersionExport { version = version, assembly_sha256 = hash };
        File.WriteAllText(Path.Combine(dossier, "version_jeu.json"), JsonUtility.ToJson(informations, true));
    }

    private static bool Contient(byte[] contenu, byte[] cherche)
    {
        for (int i = 0; i <= contenu.Length - cherche.Length; i++)
        {
            int j = 0;
            while (j < cherche.Length && contenu[i + j] == cherche[j]) j++;
            if (j == cherche.Length) return true;
        }
        return false;
    }

    private static string GetArgument(string name, string fallback)
    {
        string[] args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
        return fallback;
    }
}
#endif
