#if UNITY_EDITOR
using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

public static class LibreViesBuild
{
    public static void BuildWindows()
    {
        string output = GetArgument("-buildPath", Path.GetFullPath("../release/game/LibreViesGame.exe"));
        output = Path.GetFullPath(output);
        Directory.CreateDirectory(Path.GetDirectoryName(output));

        // Mono est disponible avec Windows Build Support et ne nécessite pas
        // l'installation séparée du toolchain IL2CPP sur la machine de build.
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Standalone, ScriptingImplementation.Mono2x);

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
