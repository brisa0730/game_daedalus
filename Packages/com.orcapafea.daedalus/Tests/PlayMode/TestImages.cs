using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Screenshot helpers for PlayMode tests. Every object made here is added to the caller's list for teardown.</summary>
    internal static class TestImages
    {
        public const int ColorTolerance = 12;
        public const int PixelsPerTile = 16;

        /// <summary>Saves the picture to <c>DAEDALUS_TEST_RESULTS</c> (or TestResults) as <c>&lt;unity version&gt;-&lt;name&gt;.png</c>.</summary>
        public static string Save(Texture2D image, string name)
        {
            string folder = Environment.GetEnvironmentVariable("DAEDALUS_TEST_RESULTS");
            if (string.IsNullOrEmpty(folder))
            {
                folder = Path.Combine(Application.dataPath, "..", "TestResults");
            }
            Directory.CreateDirectory(folder);
            string path = Path.GetFullPath(Path.Combine(folder, $"{Application.unityVersion}-{name}.png"));
            File.WriteAllBytes(path, image.EncodeToPNG());
            return path;
        }

        public static Texture2D ReadBack(RenderTexture target, List<Object> created)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = target;
            var image = new Texture2D(target.width, target.height, TextureFormat.RGBA32, false);
            created.Add(image);
            image.ReadPixels(new Rect(0, 0, target.width, target.height), 0, 0);
            image.Apply();
            RenderTexture.active = previous;
            return image;
        }

        public static bool Matches(Color32 pixel, Color32 color)
        {
            return Math.Abs(pixel.r - color.r) <= ColorTolerance &&
                Math.Abs(pixel.g - color.g) <= ColorTolerance &&
                Math.Abs(pixel.b - color.b) <= ColorTolerance;
        }

        public static int Count(Color32[] pixels, Color32 color)
        {
            int count = 0;
            foreach (Color32 pixel in pixels)
            {
                if (Matches(pixel, color))
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>A point-filtered single-colour sprite at 16 pixels per tile.</summary>
        public static Sprite SolidSprite(Color32 color, int width, int height, Vector2 pivot, List<Object> created)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            created.Add(texture);
            texture.filterMode = FilterMode.Point;
            var pixels = new Color32[width * height];
            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, width, height), pivot, PixelsPerTile);
            created.Add(sprite);
            return sprite;
        }

        /// <summary>The unlit sprite shader of the active pipeline (URP 2D in both test projects).</summary>
        public static Shader UnlitSpriteShader()
        {
            string name = GraphicsSettings.currentRenderPipeline != null
                ? "Universal Render Pipeline/2D/Sprite-Unlit-Default"
                : "Sprites/Default";
            Shader shader = Shader.Find(name);
            Assert.IsNotNull(shader, $"Shader not found: {name}");
            return shader;
        }
    }
}
