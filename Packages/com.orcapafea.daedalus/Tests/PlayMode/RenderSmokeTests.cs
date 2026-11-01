using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Draws a small tile floor and one sprite, saves the picture to TestResults and checks it.</summary>
    public class RenderSmokeTests
    {
        const int ImageSize = 256;

        static readonly Color32 BackgroundColor = new Color32(20, 24, 40, 255);
        static readonly Color32 TileColor = new Color32(60, 170, 90, 255);
        static readonly Color32 SpriteColor = new Color32(230, 120, 40, 255);

        readonly List<Object> created = new List<Object>();

        [UnityTest]
        public IEnumerator TilesAndASpriteReachTheCamera()
        {
            Material material = Track(new Material(TestImages.UnlitSpriteShader()));

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

            Texture2D image = TestImages.ReadBack(target, created);
            string path = TestImages.Save(image, "render-smoke");

            Color32[] pixels = image.GetPixels32();
            Assert.That(TestImages.Count(pixels, TileColor), Is.GreaterThan(0), $"No tile colour in the picture ({path}).");
            Assert.That(TestImages.Count(pixels, SpriteColor), Is.GreaterThan(0), $"No sprite colour in the picture ({path}).");
            Assert.That(TestImages.Count(pixels, BackgroundColor), Is.LessThan(pixels.Length), $"The picture is only background ({path}).");
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

        Sprite SolidSprite(Color32 color)
        {
            return TestImages.SolidSprite(color, 16, 16, new Vector2(0.5f, 0.5f), created);
        }
    }
}
