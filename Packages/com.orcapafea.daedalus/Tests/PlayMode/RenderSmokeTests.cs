using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Draws a small tile floor and one sprite, saves the picture to TestResults and checks it.</summary>
    public class RenderSmokeTests
    {
        const int ImageSize = 256;
        const int ColorTolerance = 12;

        static readonly Color32 BackgroundColor = new Color32(20, 24, 40, 255);
        static readonly Color32 TileColor = new Color32(60, 170, 90, 255);
        static readonly Color32 SpriteColor = new Color32(230, 120, 40, 255);

        readonly List<Object> created = new List<Object>();

        [UnityTest]
        public IEnumerator TilesAndASpriteReachTheCamera()
        {
            Material material = Track(new Material(UnlitSpriteShader()));

            var target = Track(new RenderTexture(ImageSize, ImageSize, 24));
            Camera camera = Track(new GameObject("Smoke Camera")).AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 4;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            camera.transform.position = new Vector3(0, 0, -10);
            camera.targetTexture = target;

            Grid grid = Track(new GameObject("Grid")).AddComponent<Grid>();
            var tilemapObject = new GameObject("Tilemap");
            tilemapObject.transform.SetParent(grid.transform);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
            tilemapRenderer.mode = TilemapRenderer.Mode.Chunk;
            tilemapRenderer.sharedMaterial = material;

            Tile tile = Track(ScriptableObject.CreateInstance<Tile>());
            tile.sprite = SolidSprite(TileColor);
            for (int x = -4; x < 4; x++)
            {
                for (int y = -4; y < -2; y++)
                {
                    tilemap.SetTile(new Vector3Int(x, y, 0), tile);
                }
            }

            SpriteRenderer spriteRenderer = Track(new GameObject("Sprite")).AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = SolidSprite(SpriteColor);
            spriteRenderer.sharedMaterial = material;

            yield return null;
            yield return null;
            camera.Render();

            Texture2D image = ReadBack(target);
            string path = Save(image);

            Color32[] pixels = image.GetPixels32();
            Assert.That(Count(pixels, TileColor), Is.GreaterThan(0), $"No tile colour in the picture ({path}).");
            Assert.That(Count(pixels, SpriteColor), Is.GreaterThan(0), $"No sprite colour in the picture ({path}).");
            Assert.That(Count(pixels, BackgroundColor), Is.LessThan(pixels.Length), $"The picture is only background ({path}).");
        }

        [TearDown]
        public void DestroyCreated()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.Destroy(item);
                }
            }
            created.Clear();
        }

        T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }

        static Shader UnlitSpriteShader()
        {
            string name = GraphicsSettings.currentRenderPipeline != null
                ? "Universal Render Pipeline/2D/Sprite-Unlit-Default"
                : "Sprites/Default";
            Shader shader = Shader.Find(name);
            Assert.IsNotNull(shader, $"Shader not found: {name}");
            return shader;
        }

        Sprite SolidSprite(Color32 color)
        {
            var texture = Track(new Texture2D(16, 16, TextureFormat.RGBA32, false));
            texture.filterMode = FilterMode.Point;
            var pixels = new Color32[16 * 16];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Track(Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0.5f), 16));
        }

        Texture2D ReadBack(RenderTexture target)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = Track(new Texture2D(ImageSize, ImageSize, TextureFormat.RGBA32, false));
            image.ReadPixels(new Rect(0, 0, ImageSize, ImageSize), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        static string Save(Texture2D image)
        {
            string folder = Environment.GetEnvironmentVariable("DAEDALUS_TEST_RESULTS");
            if (string.IsNullOrEmpty(folder))
            {
                folder = Path.Combine(Application.dataPath, "..", "TestResults");
            }
            Directory.CreateDirectory(folder);
            string path = Path.GetFullPath(Path.Combine(folder, $"{Application.unityVersion}-render-smoke.png"));
            File.WriteAllBytes(path, image.EncodeToPNG());
            return path;
        }

        static int Count(Color32[] pixels, Color32 color)
        {
            int count = 0;
            foreach (Color32 pixel in pixels)
            {
                if (Math.Abs(pixel.r - color.r) <= ColorTolerance &&
                    Math.Abs(pixel.g - color.g) <= ColorTolerance &&
                    Math.Abs(pixel.b - color.b) <= ColorTolerance)
                {
                    count++;
                }
            }
            return count;
        }
    }
}
