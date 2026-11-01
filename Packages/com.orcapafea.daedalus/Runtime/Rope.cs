using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>A ladder-style rope that hangs down from this object's position. Read once at play start.</summary>
    [AddComponentMenu("Daedalus/Rope")]
    public sealed class Rope : MonoBehaviour
    {
        internal const int CurrentDataVersion = 1;
        const float CrossBarHalfWidth = 0.25f;

        [SerializeField, Range(RopeSegment.MinLength, RopeSegment.MaxLength)]
        [Tooltip("How far the rope hangs down from this object, in tiles. Changes take effect the next time you press Play.")]
        float length = RopeSegment.DefaultLength;

        [SerializeField, HideInInspector]
        int dataVersion = CurrentDataVersion;

        internal float Length { get => length; set => length = value; }
        internal int DataVersion => dataVersion;

        /// <summary>The rope's centre line in world space: x, bottom, top.</summary>
        internal Vector3 WorldLine
        {
            get
            {
                Vector3 position = transform.position;
                return new Vector3(position.x, position.y - ClampedLength, position.y);
            }
        }

        float ClampedLength => Mathf.Clamp(length, RopeSegment.MinLength, RopeSegment.MaxLength);

        /// <summary>The rope in sim coordinates, where <paramref name="origin"/> is the world position of sim cell (0, 0).</summary>
        internal RopeSegment ToSegment(Vector2 origin)
        {
            Vector3 line = WorldLine;
            return new RopeSegment(line.x - origin.x, line.y - origin.y, line.z - origin.y);
        }

        void OnDrawGizmos()
        {
            Vector3 line = WorldLine;
            Vector3 top = new Vector3(line.x, line.z, transform.position.z);
            Vector3 bottom = new Vector3(line.x, line.y, top.z);
            Gizmos.color = new Color(0.85f, 0.65f, 0.3f);
            Gizmos.DrawLine(top, bottom);
            Gizmos.DrawLine(top + Vector3.left * CrossBarHalfWidth, top + Vector3.right * CrossBarHalfWidth);
        }
    }
}
