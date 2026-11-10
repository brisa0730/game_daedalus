using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// Share my game's look at a game's stages: follows every Exit from the start scene, says what would stop the share
    /// and what the student should know. Runs once per share and reads saved scenes, closing only what it opened.
    /// </summary>
    internal sealed class StageWalk
    {
        readonly List<string> stages = new List<string>();
        readonly List<string> warnings = new List<string>();
        bool canWin;
        bool hasBackLink;

        /// <summary>Every stage reachable from the start, the start first.</summary>
        internal IReadOnlyList<string> Stages => stages;

        /// <summary>Why this game can't be shared, naming the scene; null when it can.</summary>
        internal string Stop { get; private set; }

        internal IReadOnlyList<string> Warnings => warnings;

        /// <summary>"Stage1 → Stage2 → win".</summary>
        internal string Chain => string.Join(" → ", stages.Select(HelpTexts.StageName)) + (canWin ? " → win" : "");

        internal static StageWalk From(Scene start)
        {
            var walk = new StageWalk();
            var opened = new List<(Scene scene, bool wasListed)>();
            try
            {
                walk.Follow(start, opened);
                if (walk.Stop == null && !walk.canWin && (walk.stages.Count > 1 || walk.hasBackLink))
                {
                    walk.Stop = string.Format(ShareTexts.NoWayToWin, string.Join(" → ", walk.stages.Select(HelpTexts.StageName)));
                }
                if (walk.Stop == null)
                {
                    walk.FindScenesLinkingTo(start);
                }
            }
            finally
            {
                foreach ((Scene scene, bool wasListed) in opened)
                {
                    if (scene.IsValid())
                    {
                        // A stage listed in the Hierarchy but unloaded goes back to that.
                        EditorSceneManager.CloseScene(scene, !wasListed);
                    }
                }
            }
            return walk;
        }

        /// <summary>The enabled Main Camera in <paramref name="scene"/>, or null.</summary>
        internal static Camera MainCameraOf(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Camera camera in root.GetComponentsInChildren<Camera>(true))
                {
                    if (camera.enabled && camera.gameObject.activeInHierarchy && camera.CompareTag("MainCamera"))
                    {
                        return camera;
                    }
                }
            }
            return null;
        }

        void Follow(Scene start, List<(Scene scene, bool wasListed)> opened)
        {
            var queue = new Queue<string>();
            queue.Enqueue(start.path);
            stages.Add(start.path);
            while (queue.Count > 0 && Stop == null)
            {
                string path = queue.Dequeue();
                Scene scene = SceneManager.GetSceneByPath(path);
                if (!scene.isLoaded)
                {
                    bool wasListed = scene.IsValid();
                    scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);
                    opened.Add((scene, wasListed));
                }
                ReadStage(scene, queue);
            }
        }

        void ReadStage(Scene scene, Queue<string> queue)
        {
            if (!HasPlayer(scene))
            {
                Stop = string.Format(ShareTexts.StageNoPlayer, scene.name);
                return;
            }
            if (MainCameraOf(scene) == null)
            {
                Stop = string.Format(ShareTexts.StageNoCamera, scene.name);
                return;
            }
            List<Exit> exits = Exits(scene);
            if (exits.Count == 0)
            {
                warnings.Add(string.Format(ShareTexts.StageNoExit, scene.name));
            }
            var places = new List<string>();
            foreach (Exit exit in exits)
            {
                ExitLink link = exit.Link;
                if (link.Kind == ExitLink.LinkKind.Broken)
                {
                    Stop = string.Format(ShareTexts.StageBroken, scene.name, exit.name);
                    return;
                }
                string place = link.Kind == ExitLink.LinkKind.Scene ? HelpTexts.StageName(link.Path) : "win";
                if (!places.Contains(place))
                {
                    places.Add(place);
                }
                if (link.Kind == ExitLink.LinkKind.None)
                {
                    canWin = true;
                }
                else if (stages.Contains(link.Path))
                {
                    hasBackLink = true;
                    warnings.Add(string.Format(ShareTexts.BackLink, scene.name, HelpTexts.StageName(link.Path)));
                }
                else
                {
                    stages.Add(link.Path);
                    queue.Enqueue(link.Path);
                }
            }
            if (places.Count > 1)
            {
                warnings.Add(string.Format(ShareTexts.ExitsDiffer, scene.name, string.Join(", ", places)));
            }
        }

        static bool HasPlayer(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Player player in root.GetComponentsInChildren<Player>(true))
                {
                    if (player.enabled && player.gameObject.activeInHierarchy)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        static List<Exit> Exits(Scene scene)
        {
            var exits = new List<Exit>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Exit exit in root.GetComponentsInChildren<Exit>(true))
                {
                    if (exit.enabled && exit.gameObject.activeInHierarchy)
                    {
                        exits.Add(exit);
                    }
                }
            }
            return exits;
        }

        // A text search, because a link is a GUID string, not an asset reference Unity tracks. A warning, never a stop.
        void FindScenesLinkingTo(Scene start)
        {
            if (EditorSettings.serializationMode != SerializationMode.ForceText)
            {
                warnings.Add(ShareTexts.CantCheckLinks);
                return;
            }
            string guid = AssetDatabase.AssetPathToGUID(start.path);
            foreach (string found in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
            {
                string path = AssetDatabase.GUIDToAssetPath(found);
                if (!stages.Contains(path) && File.ReadAllText(path).Contains(guid))
                {
                    warnings.Add(string.Format(ShareTexts.LinksHere, HelpTexts.StageName(path)));
                }
            }
        }
    }
}
