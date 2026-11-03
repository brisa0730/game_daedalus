using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using Orcapafea.Daedalus.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The Share screenshot: the first frame's view at 1280 × 720, clamped to the map, the scene left untouched.</summary>
    public class ShareScreenshotTests
    {
        const string Folder = "Assets/DaedalusTestTemp";
        const string ScenePath = Folder + "/ShareScreenshotTest.unity";
        const int MapWidth = 40;
        const int MapHeight = 14;
        const int Tolerance = 12;

        static readonly Color32 TileColor = new Color32(40, 160, 60, 255);
        static readonly Color32 PlayerColor = new Color32(230, 60, 200, 255);
        static readonly Color32 Background = new Color32(10, 10, 30, 255);

        readonly List<Object> created = new List<Object>();
        Player player;
        Camera mainCamera;

        [SetUp]
        public void NewScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void Clean()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            foreach (Object item in created)
            {
                if (item != null)
                    Object.DestroyImmediate(item);
            }
            created.Clear();
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void ThePictureIs1280By720AndShowsThePlayerAndTheMap()
        {
            BuildLevel(20.5f);
            byte[] png = ShareScreenshot.Take(player, mainCamera);
            Assert.IsNotNull(png);
            Assert.LessOrEqual(png.Length, ShareScreenshot.MaxBytes);
            Texture2D image = Decode(png);
            Assert.AreEqual(1280, image.width);
            Assert.AreEqual(720, image.height);
            Color32[] pixels = image.GetPixels32();
            Assert.Greater(Count(pixels, PlayerColor), 100, "the player is drawn");
            Assert.Greater(Count(pixels, TileColor), 10000, "the map is drawn");
            Save(png, "share-screenshot");
        }

        [Test]
        public void AtTheMapsLeftEdgeTheViewIsClampedToTheMap()
        {
            BuildLevel(1.5f);
            Texture2D image = Decode(ShareScreenshot.Take(player, mainCamera));
            // The bottom-left corner is floor: unclamped, a view centred on x = 1.5 would show background there.
            Assert.IsTrue(Matches(image.GetPixel(2, 2), TileColor), $"bottom-left is floor, was {(Color32)image.GetPixel(2, 2)}");
        }

        [Test]
        public void TheSceneCameraDoesNotMoveAndTheSceneStaysSaved()
        {
            BuildLevel(20.5f);
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "DaedalusTestTemp");
            Scene scene = SceneManager.GetActiveScene();
            Assert.IsTrue(EditorSceneManager.SaveScene(scene, ScenePath));
            Vector3 before = mainCamera.transform.position;

            ShareScreenshot.Take(player, mainCamera);

            Assert.AreEqual(before, mainCamera.transform.position);
            Assert.IsFalse(scene.isDirty, "taking the picture doesn't dirty the scene");
        }

        [Test]
        public void TheFirstSizeUnderTheLimitIsUsed()
        {
            var sizes = new[] { new Vector2Int(1280, 720), new Vector2Int(960, 540), new Vector2Int(640, 360) };
            byte[] second = ShareScreenshot.FirstThatFits(sizes, (w, h) => new byte[w == 1280 ? 2000 : w], 1000);
            Assert.AreEqual(960, second.Length);
            Assert.IsNull(ShareScreenshot.FirstThatFits(sizes, (w, h) => new byte[w], 100), "nothing fits");
        }

        void BuildLevel(float playerX)
        {
            Shader unlit = Shader.Find(GraphicsSettings.currentRenderPipeline != null
                ? "Universal Render Pipeline/2D/Sprite-Unlit-Default"
                : "Sprites/Default");
            Assert.IsNotNull(unlit);
            var material = new Material(unlit);
            created.Add(material);

            var grid = new GameObject("Grid").AddComponent<Grid>();
            var tilemapObject = new GameObject(Player.CollisionTilemapName);
            tilemapObject.transform.SetParent(grid.transform, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>().sharedMaterial = material;
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            created.Add(tile);
            tile.sprite = SolidSprite(TileColor, 16, 16, new Vector2(0.5f, 0.5f));
            for (int x = 0; x < MapWidth; x++)
            {
                tilemap.SetTile(new Vector3Int(x, 0, 0), tile);
                tilemap.SetTile(new Vector3Int(x, 1, 0), tile);
                tilemap.SetTile(new Vector3Int(x, MapHeight - 1, 0), tile);
            }

            var playerObject = new GameObject("Player");
            var renderer = playerObject.AddComponent<SpriteRenderer>();
            renderer.sprite = SolidSprite(PlayerColor, 12, 24, new Vector2(0.5f, 0f));
            renderer.sharedMaterial = material;
            playerObject.transform.position = new Vector3(playerX, 2f, 0f);
            player = playerObject.AddComponent<Player>();

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            mainCamera = cameraObject.AddComponent<Camera>();
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = 5f;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Background;
            cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        }

        Sprite SolidSprite(Color32 color, int width, int height, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            created.Add(texture);
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = color;
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, 16);
            created.Add(sprite);
            return sprite;
        }

        Texture2D Decode(byte[] png)
        {
            var image = new Texture2D(2, 2);
            created.Add(image);
            Assert.IsTrue(image.LoadImage(png));
            return image;
        }

        static int Count(Color32[] pixels, Color32 color)
        {
            int count = 0;
            foreach (Color32 pixel in pixels)
            {
                if (Matches(pixel, color))
                    count++;
            }
            return count;
        }

        static bool Matches(Color32 pixel, Color32 color)
        {
            return Math.Abs(pixel.r - color.r) <= Tolerance && Math.Abs(pixel.g - color.g) <= Tolerance
                && Math.Abs(pixel.b - color.b) <= Tolerance;
        }

        // TestImages' folder rule; that helper lives in the PlayMode assembly, which EditMode can't see.
        static void Save(byte[] png, string name)
        {
            string folder = Environment.GetEnvironmentVariable("DAEDALUS_TEST_RESULTS");
            if (string.IsNullOrEmpty(folder))
                folder = Path.Combine(Application.dataPath, "..", "TestResults");
            Directory.CreateDirectory(folder);
            File.WriteAllBytes(Path.Combine(folder, $"{Application.unityVersion}-{name}.png"), png);
        }
    }
}
