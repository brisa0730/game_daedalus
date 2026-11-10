using UnityEngine;
using UnityEngine.SceneManagement;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Reads an Exit's Next stage link and loads the scene. This is the one sanctioned use of <c>UnityEditor</c> in the
    /// runtime (a guard test pins it): students never open Build Settings, so in the Editor a link is resolved from the
    /// asset database and a stage is loaded by path.
    /// </summary>
    internal static class StageLoad
    {
        internal static ExitLink Resolve(string guid, string builtPath)
        {
#if UNITY_EDITOR
            if (string.IsNullOrEmpty(guid))
            {
                return ExitLink.None;
            }
            string path = AssetDatabase.GUIDToAssetPath(guid);
            bool exists = !string.IsNullOrEmpty(path)
                && !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(path, AssetPathToGUIDOptions.OnlyExistingAssets));
            return exists && path.EndsWith(".unity", System.StringComparison.OrdinalIgnoreCase) ? ExitLink.ToScene(path) : ExitLink.Broken;
#else
            return ResolveBuilt(guid, builtPath);
#endif
        }

        /// <summary>Starts loading a stage in place of the current scene; null when it can't start.</summary>
        internal static AsyncOperation Begin(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return null;
            }
            try
            {
#if UNITY_EDITOR
                return EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Single));
#else
                return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
#endif
            }
            catch (System.Exception exception)
            {
                // The caller logs the failure in plain words and carries on.
                Debug.LogWarning(exception.Message);
                return null;
            }
        }

        /// <summary>In a built game: a link with no built path is broken, never an empty slot, so it can't read as a win.</summary>
        internal static ExitLink ResolveBuilt(string guid, string builtPath)
        {
            if (string.IsNullOrEmpty(guid))
            {
                return ExitLink.None;
            }
            return string.IsNullOrEmpty(builtPath) ? ExitLink.Broken : ExitLink.ToScene(builtPath);
        }
    }
}
