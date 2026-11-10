using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Debug = UnityEngine.Debug;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// Share my game's build: the active scene and every stage it leads to as a Brotli WebGL build plus a first-frame picture and the title, zipped in
    /// the shape the class gallery accepts. The menu and the command-line entry both run <see cref="Run"/>, so the test
    /// script exercises the student's path. Every setting it changes comes back before it returns.
    /// </summary>
    internal static class ShareBuild
    {
        /// <summary>The gallery's <c>max_upload_bytes</c> (Deploy/gallery/src/config.rs).</summary>
        internal const long MaxZipBytes = 50000000;

        const string OutputFolder = "Temp/DaedalusShare";

        internal sealed class Result
        {
            public bool Ok;
            public string Message;
            public string ZipPath;
            public long ZipBytes;
        }

        /// <summary>The first reason not to share right now, or null. Changes nothing. Fixed order; an Untitled scene is the
        /// caller's to handle after these (the menu asks Unity's save prompt).</summary>
        internal static string CheckBeforeBuild()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return ShareTexts.PlayMode;
            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
                return ShareTexts.Compiling;
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL))
                return ShareTexts.NoWebModule;
            if (SharedPlayer() == null)
                return ShareTexts.NoPlayer;
            if (StageWalk.MainCameraOf(SceneManager.GetActiveScene()) == null)
                return ShareTexts.NoMainCamera;
            return null;
        }

        /// <summary>The active scene's Player the picture frames: the first in hierarchy order when there are several (v1 scenes have one).</summary>
        internal static Player SharedPlayer()
        {
            // Editor code, found once per share (the rule-5 scan exception), then sorted so the pick is deterministic.
#if UNITY_6000_6_OR_NEWER
            Player[] players = Object.FindObjectsByType<Player>(FindObjectsInactive.Exclude);
#else
            Player[] players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
#endif
            Player first = null;
            List<int> firstOrder = null;
            Scene active = SceneManager.GetActiveScene();
            foreach (Player player in players)
            {
                // An unchecked Player never starts, so it would share an empty game. Another open scene isn't what's shared.
                if (!player.isActiveAndEnabled || player.gameObject.scene != active)
                    continue;
                List<int> order = HierarchyOrder(player.transform);
                if (first == null || Compare(order, firstOrder) < 0)
                {
                    first = player;
                    firstOrder = order;
                }
            }
            return first;
        }

        /// <summary>Shares the active scene to <paramref name="zipPath"/>. A student mistake gives a result with a message;
        /// a file system failure (a locked zip, a full disk) throws, after every setting has come back.</summary>
        internal static Result Run(string rawTitle, string zipPath)
        {
            string stop = CheckBeforeBuild();
            Scene scene = SceneManager.GetActiveScene();
            if (stop == null && string.IsNullOrEmpty(scene.path))
                stop = ShareTexts.Untitled;
            if (stop != null)
                return Fail(stop);
            string title = ShareTitle.Clean(rawTitle);
            if (title == null)
                return Fail(ShareTexts.EmptyTitle);

            var clock = Stopwatch.StartNew();
            if (scene.isDirty && !EditorSceneManager.SaveScene(scene))
                return Fail(ShareTexts.SaveFailed);
            StageWalk walk = StageWalk.From(scene);
            if (walk.Stop != null)
                return Fail(walk.Stop);
            string[] stages = new string[walk.Stages.Count];
            for (int i = 0; i < stages.Length; i++)
            {
                stages[i] = walk.Stages[i];
                Scene stage = SceneManager.GetSceneByPath(stages[i]);
                if (stage.isLoaded && stage.isDirty && !EditorSceneManager.SaveScene(stage))
                    return Fail(ShareTexts.SaveFailed);
            }
            // Before the platform switch, which can reload scripts.
            byte[] picture = ShareScreenshot.Take(SharedPlayer(), StageWalk.MainCameraOf(scene));
            if (picture == null)
                return Fail(ShareTexts.NoPicture);

            // Stays on WebGL afterwards: switching back costs a reimport on every share, and students only build WebGL.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.WebGL)
                EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            // An incremental build keeps unchanged files with their old timestamps; start from an empty folder.
            if (Directory.Exists(OutputFolder))
                Directory.Delete(OutputFolder, true);
            string buildDir = Path.Combine(OutputFolder, ShareZip.BuildName);

            BuildReport report;
            int exceptions;
            using (new PlayerSettingsPin())
            {
                IDisposable scenes;
                try
                {
                    scenes = BuildScenes.Use(stages);
                }
                catch (FileNotFoundException missing)
                {
                    return Fail(string.Format(ShareTexts.SceneMissing, missing.FileName));
                }
                using (scenes)
                using (var watch = new ExceptionWatch())
                {
                    report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                    {
                        scenes = stages,
                        locationPathName = buildDir,
                        target = BuildTarget.WebGL,
                        options = BuildOptions.None,
                    });
                    exceptions = watch.Count;
                    if (exceptions > 0)
                        Debug.LogWarning($"Share my game: an exception was logged during the build: {watch.First}");
                }
            }

            string failure = Outcome(report.summary.result, exceptions, ShareZip.HasBuildFiles(buildDir));
            if (failure != null)
                return Fail(failure);

            long bytes = ShareZip.Write(zipPath, title, picture, buildDir, MaxZipBytes);
            if (bytes > MaxZipBytes)
                return Fail(ShareTexts.TooBigFor(bytes));
            Debug.Log($"Share my game: {walk.Chain} → {zipPath}, {bytes} bytes, {clock.Elapsed.TotalSeconds:0}s");
            return new Result { Ok = true, ZipPath = zipPath, ZipBytes = bytes };
        }

        /// <summary>Fails closed: anything but a clean, complete build gives a message and no zip.</summary>
        internal static string Outcome(BuildResult result, int exceptions, bool filesPresent)
        {
            if (result == BuildResult.Cancelled)
                return ShareTexts.BuildStopped;
            if (exceptions > 0)
                return ShareTexts.BuildException;
            if (result != BuildResult.Succeeded || !filesPresent)
                return ShareTexts.BuildFailed;
            return null;
        }

        static Result Fail(string message)
        {
            return new Result { Ok = false, Message = message };
        }

        static List<int> HierarchyOrder(Transform transform)
        {
            var order = new List<int>();
            for (Transform t = transform; t != null; t = t.parent)
                order.Insert(0, t.GetSiblingIndex());
            return order;
        }

        static int Compare(List<int> a, List<int> b)
        {
            for (int i = 0; i < a.Count && i < b.Count; i++)
            {
                if (a[i] != b[i])
                    return a[i].CompareTo(b[i]);
            }
            return a.Count.CompareTo(b.Count);
        }

        /// <summary>
        /// The WebGL settings the Share zip depends on, for the length of the build: Brotli without the decompression
        /// fallback (the gallery sends Content-Encoding itself), no data caching (as the gallery was tested in Phase 5),
        /// plain file names (the zip needs <c>game.*</c>, not hashes) and no threads (a threaded build needs headers the
        /// gallery doesn't send, so it wouldn't run there). The student's values come back on Dispose.
        /// </summary>
        internal sealed class PlayerSettingsPin : IDisposable
        {
            readonly WebGLCompressionFormat compression;
            readonly bool decompressionFallback;
            readonly bool dataCaching;
            readonly bool nameFilesAsHashes;
            readonly bool threadsSupport;

            public PlayerSettingsPin()
            {
                compression = PlayerSettings.WebGL.compressionFormat;
                decompressionFallback = PlayerSettings.WebGL.decompressionFallback;
                dataCaching = PlayerSettings.WebGL.dataCaching;
                nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes;
                threadsSupport = PlayerSettings.WebGL.threadsSupport;
                PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
                PlayerSettings.WebGL.decompressionFallback = false;
                PlayerSettings.WebGL.dataCaching = false;
                PlayerSettings.WebGL.nameFilesAsHashes = false;
                PlayerSettings.WebGL.threadsSupport = false;
            }

            public void Dispose()
            {
                PlayerSettings.WebGL.compressionFormat = compression;
                PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
                PlayerSettings.WebGL.dataCaching = dataCaching;
                PlayerSettings.WebGL.nameFilesAsHashes = nameFilesAsHashes;
                PlayerSettings.WebGL.threadsSupport = threadsSupport;
            }
        }

        /// <summary>
        /// Counts exceptions logged while it is open. Any exception in URP's build preprocessing can strip every shader and
        /// ship a game that draws nothing (ISS-001), while the build still reports success. The threaded event, because
        /// build steps may log from worker threads. Errors aren't counted: every WebGL build logs licensing and network
        /// noise as errors (framework.md).
        /// </summary>
        internal sealed class ExceptionWatch : IDisposable
        {
            int count;
            string first;

            public ExceptionWatch()
            {
                Application.logMessageReceivedThreaded += OnLog;
            }

            public int Count => Volatile.Read(ref count);
            public string First => Volatile.Read(ref first);

            public void Dispose()
            {
                Application.logMessageReceivedThreaded -= OnLog;
            }

            void OnLog(string condition, string stackTrace, LogType type)
            {
                if (type != LogType.Exception)
                    return;
                Interlocked.Increment(ref count);
                Interlocked.CompareExchange(ref first, condition, null);
            }
        }
    }
}
