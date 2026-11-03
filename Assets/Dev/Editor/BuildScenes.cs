using System;
using System.IO;
using UnityEditor;

namespace Orcapafea.Daedalus.Dev
{
    /// <summary>
    /// Puts exactly the scenes a build is given into Build Settings for the length of the build (ISS-001). URP 17.6 reads
    /// its 2D light scan from Build Settings, not from <see cref="BuildPlayerOptions.scenes"/>, and a missing scene there
    /// strips every URP shader from a WebGL build, which then draws nothing. Wrap only the BuildPlayer call: the caller's
    /// EditorApplication.Exit doesn't unwind, so the restore must run before it.
    /// </summary>
    public static class BuildScenes
    {
        public static IDisposable Use(string[] scenePaths)
        {
            foreach (string path in scenePaths)
            {
                if (!File.Exists(path))
                    throw new FileNotFoundException($"The scene to build doesn't exist: {path}", path);
            }
            var restore = new Restore(EditorBuildSettings.scenes);
            var scenes = new EditorBuildSettingsScene[scenePaths.Length];
            for (int i = 0; i < scenePaths.Length; i++)
                scenes[i] = new EditorBuildSettingsScene(scenePaths[i], true);
            EditorBuildSettings.scenes = scenes;
            return restore;
        }

        sealed class Restore : IDisposable
        {
            readonly EditorBuildSettingsScene[] previous;

            public Restore(EditorBuildSettingsScene[] previous)
            {
                this.previous = previous;
            }

            public void Dispose()
            {
                EditorBuildSettings.scenes = previous;
            }
        }
    }
}
