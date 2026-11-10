#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Real scene files for the stage tests: A → B → C (the win), and D → E, whose target one test deletes. Each is a
    /// floor with a spike on the left, the Player, and an Exit on the right. Saved tile assets, because the compat project
    /// reloads the domain on Play and drops in-memory tiles (framework.md).
    /// </summary>
    public sealed class StageScenes : IPrebuildSetup, IPostBuildCleanup
    {
        const string FolderName = "DaedalusStageTest";
        const string Folder = "Assets/" + FolderName;
        const string FloorPath = Folder + "/Floor.asset";
        const string SpikePath = Folder + "/Spike.asset";

        public const string A = Folder + "/Stage A.unity";
        public const string B = Folder + "/Stage B.unity";
        public const string C = Folder + "/Stage C.unity";
        public const string D = Folder + "/Stage D.unity";
        public const string E = Folder + "/Stage E.unity";

        public void Setup()
        {
            Cleanup();
            AssetDatabase.CreateFolder("Assets", FolderName);
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Tile>(), FloorPath);
            var spike = ScriptableObject.CreateInstance<DaedalusTile>();
            spike.Kind = TileCollisionKind.Hazard;
            AssetDatabase.CreateAsset(spike, SpikePath);
            AssetDatabase.SaveAssets();

            // Last first, so each next stage's GUID exists.
            Save(C, null, new Color(0.2f, 0.8f, 0.3f));
            Save(B, C, new Color(0.9f, 0.5f, 0.2f));
            Save(A, B, new Color(0.3f, 0.5f, 0.9f));
            Save(E, null, new Color(0.8f, 0.3f, 0.8f));
            Save(D, E, new Color(0.8f, 0.8f, 0.3f));
        }

        public void Cleanup()
        {
            if (AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.DeleteAsset(Folder);
            }
        }

        static void Save(string path, string next, Color background)
        {
            // Additive: this runs after the test runner's bootstrap scene exists, and a Single scene would replace it.
            Scene previous = SceneManager.GetActiveScene();
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            var floor = AssetDatabase.LoadAssetAtPath<Tile>(FloorPath);
            var spike = AssetDatabase.LoadAssetAtPath<DaedalusTile>(SpikePath);

            var collision = new GameObject("Collision", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
            collision.transform.SetParent(new GameObject("Grid", typeof(Grid)).transform);
            for (int x = 0; x < 30; x++)
            {
                collision.SetTile(new Vector3Int(x, 0, 0), floor);
            }
            collision.SetTile(new Vector3Int(3, 1, 0), spike);

            Player player = new GameObject("Player").AddComponent<Player>();
            player.transform.position = new Vector3(6.5f, 1f, 0);
            player.CollisionTilemap = collision;

            Exit exit = new GameObject("Exit").AddComponent<Exit>();
            exit.transform.position = new Vector3(12.5f, 1.5f, 0);
            exit.NextStageGuid = next == null ? "" : AssetDatabase.AssetPathToGUID(next);

            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.orthographic = true;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;

            EditorSceneManager.SaveScene(scene, path);
            SceneManager.SetActiveScene(previous);
            EditorSceneManager.CloseScene(scene, true);
        }
    }
}
#endif
