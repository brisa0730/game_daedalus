using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// A box of tiles that vanishes or appears when the player touches it or enters its zone. It looks exactly like
    /// the Tile drawn across it, and the Tile's kind is the trap's: a Hazard (spike) tile kills, any other is solid
    /// ground. Fires once per life; every death puts it back. Read once at play start.
    /// </summary>
    [AddComponentMenu("Daedalus/Trap")]
    [DisallowMultipleComponent]
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class Trap : MonoBehaviour
    {
        internal const int CurrentDataVersion = 1;

        [SerializeField, Tooltip("What sets the trap off: the player entering its zone (the yellow box), or touching the trap itself.")]
        TrapTrigger trigger = TrapTrigger.Zone;

        [SerializeField, Tooltip("The box the player must enter to set the trap off. Drag its handles in the Scene view.")]
        Rect zone = TrapLook.DefaultZoneLocal(new Vector2Int(LevelRules.DefaultTrapWidth, LevelRules.DefaultTrapHeight));

        [SerializeField, Tooltip("What the trap does: Vanish (disappears) or Appear (hidden until it fires).")]
        TrapAction action = TrapAction.Vanish;

        [SerializeField, Range(LevelRules.MinTrapDelay, LevelRules.MaxTrapDelay)]
        [Tooltip("Seconds between the trigger and the action. 0 = at once.")]
        float delay = LevelRules.DefaultTrapDelay;

        [SerializeField, Tooltip("How many tiles wide and tall the trap is. The object's Scale is ignored.")]
        Vector2Int size = new Vector2Int(LevelRules.DefaultTrapWidth, LevelRules.DefaultTrapHeight);

        [SerializeField, Tooltip("The tile drawn across the trap. A Hazard (spike) tile makes a trap that kills; any other tile makes solid ground. Use a plain tile: Rule Tiles can't be drawn on a trap.")]
        TileBase tile;

        [SerializeField, HideInInspector]
        int dataVersion = CurrentDataVersion;

        internal TrapTrigger Trigger { get => trigger; set => trigger = value; }
        internal Rect Zone { get => zone; set => zone = value; }
        internal TrapAction Action { get => action; set => action = value; }
        internal float Delay { get => delay; set => delay = value; }
        internal Vector2Int Size { get => size; set => size = value; }
        internal TileBase Tile { get => tile; set => tile = value; }
        internal int DataVersion => dataVersion;

        void Reset()
        {
            zone = TrapLook.DefaultZoneLocal(TrapLook.ClampedSize(size));
        }

        void OnDrawGizmos()
        {
            if (TrapLook.IsIgnored(this))
            {
                return;
            }
            // Always on, so a trap that looks like the ground (or is hidden) can be found.
            Rect box = TrapLook.WorldBox(this);
            Gizmos.color = TrapLook.KindOf(tile) == TileKind.Hazard ? new Color(1f, 0.3f, 0.3f) : Color.white;
            Gizmos.DrawWireCube(box.center, box.size);
            if (action == TrapAction.Appear)
            {
                // A corner mark: this trap is hidden until it fires.
                Gizmos.DrawLine(new Vector3(box.xMin, box.yMax - 0.25f), new Vector3(box.xMin + 0.25f, box.yMax));
                Gizmos.DrawLine(new Vector3(box.xMax - 0.25f, box.yMin), new Vector3(box.xMax, box.yMin + 0.25f));
            }
        }

        void OnDrawGizmosSelected()
        {
            if (trigger != TrapTrigger.Zone || TrapLook.IsIgnored(this))
            {
                return;
            }
            Rect world = TrapLook.WorldZone(this);
            Gizmos.color = new Color(1f, 0.9f, 0.2f);
            Gizmos.DrawWireCube(world.center, world.size);
        }
    }
}
