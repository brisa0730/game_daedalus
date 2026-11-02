using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The minimal follow camera: the main camera snaps to the player every frame, kept inside the map (an
    /// orthographic view never shows past the map's edges; a map narrower than the view is centred). No smoothing
    /// and no look-ahead. A respawn cuts, because the player's own interpolation snaps.
    /// </summary>
    internal sealed class CameraFollow
    {
        readonly Camera camera;
        readonly Transform cameraTransform;
        readonly Rect map;

        /// <param name="camera">The camera to move; none means nothing follows.</param>
        /// <param name="map">The map's world rect.</param>
        public CameraFollow(Camera camera, Rect map)
        {
            this.camera = camera;
            cameraTransform = camera != null ? camera.transform : null;
            this.map = map;
        }

        public Camera Camera => camera;

        /// <summary>Moves the camera to <paramref name="target"/> (the player's hitbox centre in world space), clamped.</summary>
        public void Follow(Vector2 target)
        {
            if (camera == null)
            {
                return;
            }
            float x = target.x;
            float y = target.y;
            if (camera.orthographic)
            {
                float halfHeight = camera.orthographicSize;
                float halfWidth = halfHeight * camera.aspect;
                x = Clamp(x, map.xMin, map.xMax, halfWidth);
                y = Clamp(y, map.yMin, map.yMax, halfHeight);
            }
            Vector3 position = cameraTransform.position;
            cameraTransform.position = new Vector3(x, y, position.z);
        }

        static float Clamp(float value, float min, float max, float half)
        {
            if (max - min <= 2 * half)
            {
                return (min + max) / 2;
            }
            return Mathf.Clamp(value, min + half, max - half);
        }
    }
}
