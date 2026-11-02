using UnityEditor;
using UnityEngine;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// The Trap's authoring actions (fit the zone, snap to the grid, set a Move's end, keep the look in sync with the
    /// settings), all Undo-recorded and safe on several selected traps. The box math is the runtime's <see cref="TrapLook"/>.
    /// </summary>
    [InitializeOnLoad]
    internal static class TrapAuthoring
    {
        static TrapAuthoring()
        {
            ObjectFactory.componentWasAdded += OnComponentAdded;
        }

        /// <summary>Puts the zone back to the default: the trap's box grown upward (on an Exit, the exit's box grown on every side).</summary>
        public static void FitZone(Trap trap)
        {
            Undo.RecordObject(trap, "Fit Zone Around Trap");
            trap.Zone = TrapLook.DefaultZoneLocal(trap);
            EditorUtility.SetDirty(trap);
        }

        /// <summary>The Move-end handle's write: the end's centre at <paramref name="worldEndCentre"/>, rounded to whole tiles and clamped to the range.</summary>
        public static void SetEnd(Trap trap, Vector2 worldEndCentre)
        {
            Vector2Int whole = TrapLook.OffsetForEnd(trap, worldEndCentre);
            if (whole == trap.Offset)
            {
                return;
            }
            Undo.RecordObject(trap, "Move Trap End");
            trap.Offset = whole;
            EditorUtility.SetDirty(trap);
        }

        /// <summary>Moves the trap so its box sits on whole cells.</summary>
        public static void SnapToGrid(Trap trap)
        {
            Transform t = trap.transform;
            Undo.RecordObject(t, "Snap Trap to Grid");
            Vector2 centre = TrapLook.SnappedCentre(trap);
            t.position = new Vector3(centre.x, centre.y, t.position.z);
        }

        /// <summary>
        /// The saved look is the look at play start: the tile's sprite tiled across the box, shown unless the trap is an
        /// Appear trap (the Scene view shows an Appear trap as a faded ghost instead). A Trap on an Exit moves the exit's
        /// own look, and one on a Checkpoint or a turned-off Exit does nothing, so the object's own look is left alone.
        /// In Play the look belongs to the running level (traps are read at play start), so it is left alone too.
        /// </summary>
        public static void SyncLook(Trap trap)
        {
            SyncLook(trap, EditorApplication.isPlaying);
        }

        internal static void SyncLook(Trap trap, bool playing)
        {
            if (playing || TrapLook.IsIgnored(trap) || TrapLook.IsOnExit(trap))
            {
                return;
            }
            var renderer = trap.GetComponent<SpriteRenderer>();
            if (renderer == null)
            {
                return;
            }
            Undo.RecordObject(renderer, "Trap Look");
            renderer.sprite = TrapLook.SpriteOf(trap.Tile);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = TrapLook.ClampedSize(trap);
            renderer.enabled = trap.Action != TrapAction.Appear;
            EditorUtility.SetDirty(renderer);
        }

        /// <summary>A zone that was the default for the old size follows the new size; a drawn zone stays.</summary>
        public static void OnSizeChanged(Trap trap, Vector2Int oldSize)
        {
            if (trap.Zone == TrapLook.DefaultZoneLocal(TrapLook.ClampedSize(oldSize)))
            {
                FitZone(trap);
            }
        }

        static void OnComponentAdded(Component component)
        {
            if (component is Trap trap)
            {
                SyncLook(trap);
            }
        }
    }
}
