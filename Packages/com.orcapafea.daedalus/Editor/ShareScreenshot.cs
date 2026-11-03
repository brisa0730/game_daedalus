using System;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// The class gallery card's picture: the main camera's view of the game's first frame, rendered off-screen in Edit
    /// mode. Framed through the runtime's own <see cref="Player.FirstView"/> and <see cref="CameraFollow"/>, so it
    /// matches what a classmate sees when the game opens. Appear traps are already hidden in Edit mode, as at play start.
    /// </summary>
    internal static class ShareScreenshot
    {
        /// <summary>The gallery's screenshot limits (<c>max_screenshot_*</c> in Deploy/gallery/src/config.rs).</summary>
        internal const int MaxBytes = 1000000;

        static readonly Vector2Int[] Sizes = { new Vector2Int(1280, 720), new Vector2Int(960, 540), new Vector2Int(640, 360) };

        /// <summary>PNG bytes of the first frame, the largest size under the limit; null if none fits.</summary>
        internal static byte[] Take(Player player, Camera mainCamera)
        {
            Rect map = player.FirstView(out Vector2 target); // once: it runs the play-start scans
            return FirstThatFits(Sizes, (width, height) => Render(mainCamera, map, target, width, height), MaxBytes);
        }

        internal static byte[] FirstThatFits(Vector2Int[] sizes, Func<int, int, byte[]> render, int maxBytes)
        {
            foreach (Vector2Int size in sizes)
            {
                byte[] png = render(size.x, size.y);
                if (png != null && png.Length <= maxBytes)
                    return png;
            }
            return null;
        }

        static byte[] Render(Camera mainCamera, Rect map, Vector2 target, int width, int height)
        {
            // A temporary copy, so the scene's own camera never moves and the scene isn't dirtied.
            var cameraObject = new GameObject("Share screenshot camera") { hideFlags = HideFlags.HideAndDontSave };
            var texture = new RenderTexture(width, height, 24);
            Texture2D image = null;
            RenderTexture previous = RenderTexture.active;
            try
            {
                Camera camera = cameraObject.AddComponent<Camera>();
                camera.CopyFrom(mainCamera);
                camera.targetTexture = texture;
                // The picture's aspect, not one copied from the main camera, so the map clamp frames 16:9.
                camera.ResetAspect();
                new CameraFollow(camera, map).Follow(target);
                camera.Render();

                RenderTexture.active = texture;
                image = new Texture2D(width, height, TextureFormat.RGB24, false);
                image.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                image.Apply();
                return image.EncodeToPNG();
            }
            finally
            {
                RenderTexture.active = previous;
                Object.DestroyImmediate(cameraObject);
                Object.DestroyImmediate(texture);
                if (image != null)
                    Object.DestroyImmediate(image);
            }
        }
    }
}
