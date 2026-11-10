using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Orcapafea.Daedalus;

namespace Orcapafea.Daedalus.Dev
{
    /// <summary>
    /// Generates the two-stage bench Tools/share_check.py builds and plays in Chrome. Stage A: the Player falls onto its
    /// Exit with no input, after half a second of clean frames. Stage B: a 200 × 60 map, its Exit out of reach. Both have
    /// bright backgrounds of different hues, far from black. The generator is the source; GUIDs stay stable. Batch mode:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod Orcapafea.Daedalus.Dev.StageTestSceneBuilder.Build -logFile TestResults/stage-scene.log
    /// </summary>
    public static class StageTestSceneBuilder
    {
        const string Folder = "Assets/Dev/StageTest";
        const string TilePath = Folder + "/Ground.asset";
        const string StageA = Folder + "/Stage A.unity";
        const string StageB = Folder + "/Stage B.unity";

        static readonly Color BlueStage = new Color(0.15f, 0.55f, 1f);
        static readonly Color OrangeStage = new Color(1f, 0.55f, 0.1f);

        [MenuItem("Daedalus Dev/Rebuild Stage Test Scenes")]
        public static void Rebuild()
        {
            Generate();
        }

        /// <summary>Batch-mode entry: generates, self-checks and exits 0 (ok) or 1.</summary>
        public static void Build()
        {
            try
            {
                Generate();
                SelfCheck();
                Debug.Log("StageTestSceneBuilder: ok");
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static void Generate()
        {
            if (!AssetDatabase.IsValidFolder(Folder))
            {
                AssetDatabase.CreateFolder("Assets/Dev", "StageTest");
            }
            if (AssetDatabase.LoadAssetAtPath<Tile>(TilePath) == null)
            {
                AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<Tile>(), TilePath);
                AssetDatabase.SaveAssets();
            }

            // B first, so A can link to its GUID.
            Save(StageB, 200, 60, new Vector3(4.5f, 1f, 0), new Vector3(190.5f, 1.5f, 0), "", OrangeStage);
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(StageB)))
            {
                AssetDatabase.ImportAsset(StageB);
            }
            Save(StageA, 40, 22, new Vector3(20.5f, 11f, 0), new Vector3(20.5f, 1.5f, 0), AssetDatabase.AssetPathToGUID(StageB), BlueStage);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        static void Save(string path, int width, int height, Vector3 playerAt, Vector3 exitAt, string next, Color background)
        {
            // The scene first: NewScene unloads assets loaded before it (framework.md).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = AssetDatabase.LoadAssetAtPath<Tile>(TilePath);

            var collision = new GameObject("Collision", typeof(Tilemap), typeof(TilemapRenderer)).GetComponent<Tilemap>();
            collision.transform.SetParent(new GameObject("Grid", typeof(Grid)).transform);
            // The outline only: it sets the map's size without a scene file of many thousand tile lines.
            for (int x = 0; x < width; x++)
            {
                collision.SetTile(new Vector3Int(x, 0, 0), ground);
                collision.SetTile(new Vector3Int(x, height - 1, 0), ground);
            }
            for (int y = 1; y < height - 1; y++)
            {
                collision.SetTile(new Vector3Int(0, y, 0), ground);
                collision.SetTile(new Vector3Int(width - 1, y, 0), ground);
            }

            new GameObject("Player").AddComponent<Player>().transform.position = playerAt;

            Exit exit = new GameObject("Exit").AddComponent<Exit>();
            exit.transform.position = exitAt;
            var link = new SerializedObject(exit);
            link.FindProperty("nextStage").stringValue = next;
            link.ApplyModifiedPropertiesWithoutUndo();

            var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
            camera.tag = "MainCamera";
            camera.transform.position = new Vector3(20, 11, -10);
            camera.orthographic = true;
            camera.orthographicSize = 8f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = background;

            if (!EditorSceneManager.SaveScene(scene, path))
            {
                throw new InvalidOperationException($"Couldn't save {path}");
            }
        }

        static void SelfCheck()
        {
            EditorSceneManager.OpenScene(StageA, OpenSceneMode.Single);
            Exit exit = UnityEngine.Object.FindAnyObjectByType<Exit>();
            string guid = new SerializedObject(exit).FindProperty("nextStage").stringValue;
            if (AssetDatabase.GUIDToAssetPath(guid) != StageB)
            {
                throw new InvalidOperationException($"Stage A's Exit goes to '{AssetDatabase.GUIDToAssetPath(guid)}', expected {StageB}.");
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }
    }
}
