using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Orcapafea.Daedalus.Dev
{
    /// <summary>
    /// Builds the dev-only WebGL speed test. Run by Tools/perf_check.py:
    /// Unity.exe -batchmode -projectPath . -buildTarget WebGL -executeMethod Orcapafea.Daedalus.Dev.PerfCheckBuild.Build -perfOutput DIR
    /// </summary>
    public static class PerfCheckBuild
    {
        const string ScenePath = "Assets/Dev/PerfCheck/PerfCheck.unity";

        public static void Build()
        {
            string output = ArgumentAfter("-perfOutput") ?? "TestResults/perf-build";

            // Plain files so a simple static server can host the build; keep running when unfocused.
            WebGLCompressionFormat compression = PlayerSettings.WebGL.compressionFormat;
            bool runInBackground = PlayerSettings.runInBackground;
            BuildReport report;
            try
            {
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Disabled;
                PlayerSettings.runInBackground = true;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = BuildOptions.None,
                });
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.runInBackground = runInBackground;
                // -buildTarget WebGL left the dev project on WebGL; put it back on Windows for the Editor and tests.
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64);
            }

            bool succeeded = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"PerfCheckBuild: {report.summary.result}, {report.summary.totalErrors} errors, output {output}");
            EditorApplication.Exit(succeeded ? 0 : 1);
        }

        static string ArgumentAfter(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
