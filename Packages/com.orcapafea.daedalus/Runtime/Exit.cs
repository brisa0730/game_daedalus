using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Touching it ends the game with a win screen. Its box is its sprite; without a sprite, one tile around its
    /// position. Read once at play start.
    /// </summary>
    [AddComponentMenu("Daedalus/Exit")]
    [DisallowMultipleComponent]
    public sealed class Exit : MonoBehaviour
    {
        internal const int CurrentDataVersion = 1;

        [SerializeField, HideInInspector]
        int dataVersion = CurrentDataVersion;

        internal int DataVersion => dataVersion;

        /// <summary>The box the player touches, in world space.</summary>
        internal Rect WorldBox => LevelCollector.ElementBox(this);

        void OnDrawGizmos()
        {
            Rect box = WorldBox;
            Gizmos.color = new Color(0.4f, 1f, 0.5f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
