using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace Orcapafea.Daedalus.Dev
{
    /// <summary>
    /// Builds the trap bench to WebGL for the class gallery spike (Documents/Plans/archive/class-gallery-spike.md), with the
    /// save probe compiled in:
    /// Unity -batchmode -projectPath . -buildTarget WebGL -executeMethod Orcapafea.Daedalus.Dev.GallerySpikeBuild.Build -spikeCompression brotli|gzip|none -spikeOutput DIR [-spikeDevelopment]
    /// </summary>
    public static class GallerySpikeBuild
    {
        const string ScenePath = "Assets/Dev/TrapTest/TrapTest.unity";

        public static void Build()
        {
            string output = ArgumentAfter("-spikeOutput") ?? "TestResults/gallery-spike/build";
            string mode = ArgumentAfter("-spikeCompression") ?? "brotli";
            WebGLCompressionFormat format = mode switch
            {
                "brotli" => WebGLCompressionFormat.Brotli,
                "gzip" => WebGLCompressionFormat.Gzip,
                "none" => WebGLCompressionFormat.Disabled,
                _ => throw new ArgumentException($"-spikeCompression must be brotli, gzip or none, not '{mode}'"),
            };

            // The server sends the encoding headers itself, and nothing is cached in the browser, so every load
            // measures the download and only PlayerPrefs survive a reload.
            WebGLCompressionFormat compression = PlayerSettings.WebGL.compressionFormat;
            bool decompressionFallback = PlayerSettings.WebGL.decompressionFallback;
            bool dataCaching = PlayerSettings.WebGL.dataCaching;
            BuildReport report;
            try
            {
                PlayerSettings.WebGL.compressionFormat = format;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.dataCaching = false;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { ScenePath },
                    locationPathName = output,
                    target = BuildTarget.WebGL,
                    options = Array.IndexOf(Environment.GetCommandLineArgs(), "-spikeDevelopment") >= 0
                        ? BuildOptions.Development : BuildOptions.None,
                    extraScriptingDefines = new[] { "DAEDALUS_GALLERY_SPIKE" },
                });
            }
            finally
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
                PlayerSettings.WebGL.dataCaching = dataCaching;
                // -buildTarget WebGL left the dev project on WebGL; put it back on this machine's standalone target for the Editor and tests.
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone,
                    Application.platform == RuntimePlatform.OSXEditor ? BuildTarget.StandaloneOSX : BuildTarget.StandaloneWindows64);
            }

            // URP's record of which graphics settings reached the player; Unity deletes Temp/ on exit.
            const string StrippingReport = "Temp/graphics-settings-stripping.json";
            if (System.IO.File.Exists(StrippingReport) && System.IO.Directory.Exists(output))
                System.IO.File.Copy(StrippingReport, System.IO.Path.Combine(output, "graphics-settings-stripping.json"), true);

            bool succeeded = report.summary.result == BuildResult.Succeeded;
            Debug.Log($"GallerySpikeBuild: {report.summary.result}, {report.summary.totalErrors} errors, {mode}, output {output}");
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
