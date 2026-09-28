#if UNITY_EDITOR
using System;
using System.IO;
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
        Debug.Log("LibreVies Unity exporté : " + output);
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
