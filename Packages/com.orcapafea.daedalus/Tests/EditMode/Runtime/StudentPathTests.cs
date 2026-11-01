using System;
using System.Collections;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// A beginner's first Play: a Tilemap named Collision painted with plain tiles, a sprite, Add Component →
    /// Player, Play. Nothing else is touched. The tile and sprites are saved as temporary assets so they survive
    /// the scene and domain reload of entering Play mode; warnings are read from the editor log, which survives too.
    /// </summary>
    public class StudentPathTests
    {
        const string Folder = "Assets/DaedalusStudentPathTemp";
        const float FloorTop = 1f;
        const string MarkerKey = "Orcapafea.Daedalus.Tests.StudentPath.Marker";

        [UnityTest]
        public IEnumerator AddAPlayerPaintPlainTilesAndPressPlay()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.CreateFolder("Assets", "DaedalusStudentPathTemp");

            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = SavedSprite("TileArt", 16, 16, new Color32(60, 170, 90, 255));
            AssetDatabase.CreateAsset(tile, Folder + "/Plain Tile.asset");

            Grid grid = new GameObject("Grid").AddComponent<Grid>();
            var tilemapObject = new GameObject("Collision");
            tilemapObject.transform.SetParent(grid.transform, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>();
            for (int x = 0; x < 10; x++)
            {
                tilemap.SetTile(new Vector3Int(x, 0, 0), tile);
            }

            var square = new GameObject("Square");
            SpriteRenderer spriteRenderer = square.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = SavedSprite("PlayerArt", 12, 24, new Color32(240, 240, 240, 255));
            square.transform.position = new Vector3(4.5f, FloorTop + 3 - spriteRenderer.sprite.bounds.min.y, 0);

            Player added = ObjectFactory.AddComponent<Player>(square);
            Assert.AreSame(tilemap, added.CollisionTilemap, "the Collision Tilemap field filled itself in");

            // Locals don't survive the domain reload of entering Play mode (6000.0 compat project); SessionState does.
            SessionState.SetString(MarkerKey, "daedalus student path " + Guid.NewGuid().ToString("N"));
            Debug.Log(SessionState.GetString(MarkerKey, ""));
            yield return new EnterPlayMode();

            // References die in the reload; test code may look the Player up again.
            Player player = Object.FindAnyObjectByType<Player>();
            Assert.IsNotNull(player, "the Player survived entering Play mode");
            // Batch mode runs frames unthrottled; a fixed 60 fps makes 90 frames 1.5 s of play (as in the PlayMode tests).
            Time.captureFramerate = 60;
            int firstFrame = Time.frameCount;
            for (int i = 0; i < 90; i++)
            {
                yield return null;
            }
            PlayerState state = player.Session.Current;
            Assert.AreEqual(PlayerContact.Ground, state.Contact,
                $"landed (feet y {state.Y}, {Time.frameCount - firstFrame} frames, dt {Time.deltaTime}, t {Time.time})");
            Assert.AreEqual(FloorTop, player.GetComponent<SpriteRenderer>().bounds.min.y, 1e-4f, "the sprite's bottom is on the floor");

            string log = LogAfter(SessionState.GetString(MarkerKey, ""));
            foreach (FieldInfo field in typeof(PlayerWarnings).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                if (field.IsLiteral)
                {
                    StringAssert.DoesNotContain((string)field.GetValue(null), log, field.Name);
                }
            }
            StringAssert.DoesNotContain("Daedalus needs 1 × 1", log, "cell size");

            yield return new ExitPlayMode();
        }

        [UnityTearDown]
        public IEnumerator CleanUp()
        {
            Time.captureFramerate = 0;
            if (Application.isPlaying)
            {
                yield return new ExitPlayMode();
            }
            AssetDatabase.DeleteAsset(Folder);
            SessionState.EraseString(MarkerKey);
        }

        static Sprite SavedSprite(string name, int width, int height, Color32 color)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { name = name, filterMode = FilterMode.Point };
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            string path = $"{Folder}/{name}.asset";
            AssetDatabase.CreateAsset(texture, path);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), new Vector2(0.5f, 0.5f), 16);
            sprite.name = name;
            AssetDatabase.AddObjectToAsset(sprite, path);
            AssetDatabase.SaveAssets();
            return sprite;
        }

        static string LogAfter(string marker)
        {
            string path = Application.consoleLogPath;
            Assert.IsNotEmpty(marker, "the marker survived entering Play mode");
            Assert.IsTrue(File.Exists(path), $"the editor log is readable at {path}");
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete))
            using (var reader = new StreamReader(stream))
            {
                string text = reader.ReadToEnd();
                int at = text.LastIndexOf(marker, StringComparison.Ordinal);
                Assert.That(at, Is.GreaterThanOrEqualTo(0), "the marker reached the log");
                return text.Substring(at);
            }
        }
    }
}
