using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Touching it makes it the place the player comes back to (when the Player's On hazard is Respawn at checkpoint).
    /// Its box is its sprite; without a sprite, one tile around its position. Read once at play start.
    /// </summary>
    [AddComponentMenu("Daedalus/Checkpoint")]
    [DisallowMultipleComponent]
    public sealed class Checkpoint : MonoBehaviour
    {
        internal const int CurrentDataVersion = 1;
        /// <summary>Presentation: the tint of the checkpoint the player last touched.</summary>
        internal static readonly Color ReachedTint = new Color(1f, 0.82f, 0.3f);

        [SerializeField, HideInInspector]
        int dataVersion = CurrentDataVersion;

        SpriteRenderer spriteRenderer;
        Color authoredColor;

        internal int DataVersion => dataVersion;

        /// <summary>The box the player touches, in world space.</summary>
        internal Rect WorldBox => LevelCollector.ElementBox(this);

        void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                authoredColor = spriteRenderer.color;
            }
        }

        /// <summary>Tints it while it is the active checkpoint; back to the authored colour otherwise. Play mode only.</summary>
        internal void SetReached(bool reached)
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = reached ? ReachedTint : authoredColor;
            }
        }

        void OnDrawGizmos()
        {
            Rect box = WorldBox;
            Gizmos.color = new Color(1f, 0.82f, 0.3f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
