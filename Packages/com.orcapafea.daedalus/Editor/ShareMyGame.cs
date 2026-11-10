using System;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// Daedalus → Share my game: asks for a title, then makes the one zip the class gallery accepts. Public only for
    /// <see cref="BuildFromCommandLine"/>, the entry Tools/share_check.py runs; students use the menu.
    /// </summary>
    public static class ShareMyGame
    {
        const string FolderKey = "Daedalus.Share.Folder";
        const string TitleKeyPrefix = "Daedalus.Share.Title.";

        [MenuItem("Daedalus/Share my game")]
        static void Share()
        {
            // Enabled in Play mode and while compiling on purpose: a greyed-out item gives no reason, a message does.
            string stop = ShareBuild.CheckBeforeBuild();
            if (stop != null)
            {
                EditorUtility.DisplayDialog(ShareTexts.Title, stop, "OK");
                return;
            }
            Scene scene = SceneManager.GetActiveScene();
            if (string.IsNullOrEmpty(scene.path))
            {
                string path = EditorUtility.SaveFilePanelInProject("Save Scene", "My Level", "unity", ShareTexts.Untitled);
                if (string.IsNullOrEmpty(path) || !EditorSceneManager.SaveScene(scene, path))
                    return;
            }
            StageWalk walk;
            try
            {
                walk = StageWalk.From(scene);
            }
            catch (Exception exception)
            {
                // A stage file that can't be opened: one plain message, not only the Console.
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(ShareTexts.Title, ShareTexts.Unexpected, "OK");
                return;
            }
            if (walk.Stop != null)
            {
                EditorUtility.DisplayDialog(ShareTexts.Title, walk.Stop, "OK");
                return;
            }
            string guid = AssetDatabase.AssetPathToGUID(scene.path);
            ShareTitleWindow.Open(scene.name, LoadTitle(guid), walk.Chain, walk.Warnings, title => Continue(title, guid));
        }

        static void Continue(string rawTitle, string sceneGuid)
        {
            string title = ShareTitle.Clean(rawTitle);
            if (title == null)
                return;
            string folder = EditorUserSettings.GetConfigValue(FolderKey);
            if (string.IsNullOrEmpty(folder) || !Directory.Exists(folder))
                folder = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            string zipPath = EditorUtility.SaveFilePanel(ShareTexts.Title, folder, ShareTitle.FileName(title), "zip");
            if (string.IsNullOrEmpty(zipPath))
                return;
            EditorUserSettings.SetConfigValue(FolderKey, Path.GetDirectoryName(zipPath));

            ShareBuild.Result result;
            try
            {
                result = ShareBuild.Run(title, zipPath);
            }
            catch (Exception exception)
            {
                // Settings are already back (Run's scopes); the student still gets one plain message, not only the Console.
                Debug.LogException(exception);
                EditorUtility.DisplayDialog(ShareTexts.Title, ShareTexts.Unexpected, "OK");
                return;
            }
            if (!result.Ok)
            {
                EditorUtility.DisplayDialog(ShareTexts.Title, result.Message, "OK");
                return;
            }
            SaveTitle(sceneGuid, title);
            int choice = EditorUtility.DisplayDialogComplex(ShareTexts.Title, string.Format(ShareTexts.Done, zipPath),
                ShareTexts.ShowZip, ShareTexts.Close, ShareTexts.OpenGallery);
            if (choice == 0)
                EditorUtility.RevealInFinder(zipPath);
            else if (choice == 2)
                Application.OpenURL(ShareTexts.GalleryAddress); // the student's browser; the editor itself sends nothing
        }

        /// <summary>The title this scene was last shared with (per project and scene, in user settings, never in the scene).</summary>
        internal static string LoadTitle(string sceneGuid)
        {
            return EditorUserSettings.GetConfigValue(TitleKeyPrefix + sceneGuid) ?? "";
        }

        internal static void SaveTitle(string sceneGuid, string title)
        {
            EditorUserSettings.SetConfigValue(TitleKeyPrefix + sceneGuid, title);
        }

        /// <summary>
        /// For Tools/share_check.py: <c>-shareScene PATH -shareTitleFile FILE -shareOutput ZIP</c> (the title in a UTF-8
        /// file, so non-ASCII text survives Windows command lines). Runs the menu's own <see cref="ShareBuild.Run"/>. The one
        /// difference: afterwards it switches the dev project back to its previous build target. Exits 0 or 1.
        /// </summary>
        public static void BuildFromCommandLine()
        {
            string scenePath = ArgumentAfter("-shareScene");
            string titleFile = ArgumentAfter("-shareTitleFile");
            string zipPath = ArgumentAfter("-shareOutput");
            if (scenePath == null || titleFile == null || zipPath == null)
            {
                Debug.LogError("ShareMyGame: usage: -shareScene PATH -shareTitleFile FILE -shareOutput ZIP");
                EditorApplication.Exit(1);
                return;
            }

            BuildTarget target = EditorUserBuildSettings.activeBuildTarget;
            BuildTargetGroup group = BuildPipeline.GetBuildTargetGroup(target);
            var clock = System.Diagnostics.Stopwatch.StartNew();
            EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            ShareBuild.Result result = ShareBuild.Run(File.ReadAllText(titleFile, Encoding.UTF8), Path.GetFullPath(zipPath));
            if (EditorUserBuildSettings.activeBuildTarget != target)
                EditorUserBuildSettings.SwitchActiveBuildTarget(group, target);
            // So ProjectSettings.asset on disk shows the restored Player Settings the script checks.
            AssetDatabase.SaveAssets();
            Debug.Log($"ShareMyGame: {(result.Ok ? "ok" : "fail")} {result.ZipBytes} {clock.Elapsed.TotalSeconds:0}s {result.Message}");
            EditorApplication.Exit(result.Ok ? 0 : 1);
        }

        static string ArgumentAfter(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            int index = Array.IndexOf(args, name);
            return index >= 0 && index + 1 < args.Length ? args[index + 1] : null;
        }
    }
}
