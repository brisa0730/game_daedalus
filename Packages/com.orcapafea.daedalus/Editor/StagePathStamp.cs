using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// Writes each Exit's scene path into the build's copy of a scene, because a built game has no asset database to look
    /// a link's GUID up in. Skips Play mode, where the report is null. Saved scenes are never touched.
    /// </summary>
    internal sealed class StagePathStamp : IProcessSceneWithReport
    {
        public int callbackOrder => 0;

        public void OnProcessScene(Scene scene, BuildReport report)
        {
            if (report != null)
            {
                Stamp(scene);
            }
        }

        internal static void Stamp(Scene scene)
        {
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                foreach (Exit exit in root.GetComponentsInChildren<Exit>(true))
                {
                    ExitLink link = exit.Link;
                    if (link.Kind == ExitLink.LinkKind.Broken && exit.enabled && exit.gameObject.activeInHierarchy)
                    {
                        throw new BuildFailedException($"In {scene.name}, the Next stage of {exit.name} is missing. Drag the scene in again.");
                    }
                    exit.StampBuiltPath(link.Path);
                }
            }
        }
    }
}
