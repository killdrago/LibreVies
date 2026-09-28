#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class CharacterCreatorBuild
{
    public static void BuildWindows()
    {
        // Le createur doit s'ouvrir dans une fenetre redimensionnable, jamais
        // en plein ecran. Le reglage runtime dans CharacterCreatorApp protege
        // aussi les anciennes configurations Unity du projet.
        PlayerSettings.fullScreenMode = UnityEngine.FullScreenMode.Windowed;
        PlayerSettings.defaultScreenWidth = 1280;
        PlayerSettings.defaultScreenHeight = 800;
        PlayerSettings.resizableWindow = true;
        PlayerSettings.runInBackground = true;

        string output = GetArgument("-buildPath", Path.GetFullPath("../jeu/personnage/LibreViesPersonnage.exe"));
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(output));
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/Scenes/Personnage.unity" },
            locationPathName = output,
            target = BuildTarget.StandaloneWindows64,
            options = BuildOptions.None
        };
        BuildReport report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != BuildResult.Succeeded)
            throw new Exception("La build du createur de personnages a echoue : " + report.summary.result);
        UnityEngine.Debug.Log("Createur de personnages exporte : " + output);
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
