using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// A trap's box math, shared by the runtime and the editor tools so both read the same box: the size clamped to
    /// the sim's range, the box centred on the object and snapped to whole cells, the zone, the kind and the sprite.
    /// Grid lines are at whole world units (the Collision Tilemap's cells).
    /// </summary>
    internal static class TrapLook
    {
        const float OffGridTolerance = 0.01f;

        /// <summary>The one size every reader uses: each axis clamped to the sim's range (a typed 0 or 40 degrades).</summary>
        public static Vector2Int ClampedSize(Vector2Int size)
        {
            return new Vector2Int(
                Mathf.Clamp(size.x, LevelRules.MinTrapSize, LevelRules.MaxTrapSize),
                Mathf.Clamp(size.y, LevelRules.MinTrapSize, LevelRules.MaxTrapSize));
        }

        public static Vector2Int ClampedSize(Trap trap) => ClampedSize(trap.Size);

        /// <summary>
        /// The bottom-left cell of the snapped box. The box is centred on the object (where a centre-pivot tile sprite
        /// draws in Tiled mode); <c>FloorToInt(v + 0.5)</c> rounds halves up whatever their parity.
        /// </summary>
        public static Vector2Int CellMin(Trap trap)
        {
            Vector2Int size = ClampedSize(trap);
            Vector3 position = trap.transform.position;
            return new Vector2Int(
                Mathf.FloorToInt(position.x - size.x / 2f + 0.5f),
                Mathf.FloorToInt(position.y - size.y / 2f + 0.5f));
        }

        public static Vector2 SnappedCentre(Trap trap)
        {
            Vector2Int min = CellMin(trap);
            Vector2Int size = ClampedSize(trap);
            return new Vector2(min.x + size.x / 2f, min.y + size.y / 2f);
        }

        public static bool IsOffGrid(Trap trap)
        {
            Vector3 position = trap.transform.position;
            return Vector2.Distance(new Vector2(position.x, position.y), SnappedCentre(trap)) > OffGridTolerance;
        }

        /// <summary>The snapped box in world space.</summary>
        public static Rect WorldBox(Trap trap)
        {
            Vector2Int min = CellMin(trap);
            Vector2Int size = ClampedSize(trap);
            return new Rect(min.x, min.y, size.x, size.y);
        }

        /// <summary>The zone in world space (it is stored relative to the object), with a negative width or height undone.</summary>
        public static Rect WorldZone(Trap trap)
        {
            Rect local = trap.Zone;
            Vector3 position = trap.transform.position;
            return Rect.MinMaxRect(
                position.x + Mathf.Min(local.xMin, local.xMax), position.y + Mathf.Min(local.yMin, local.yMax),
                position.x + Mathf.Max(local.xMin, local.xMax), position.y + Mathf.Max(local.yMin, local.yMax));
        }

        /// <summary>The default zone of a centred box of <paramref name="size"/>, relative to the object: <see cref="LevelRules.DefaultZone"/>.</summary>
        public static Rect DefaultZoneLocal(Vector2Int size)
        {
            Vector2Int s = ClampedSize(size);
            Box zone = LevelRules.DefaultZone(new Box(-s.x / 2f, -s.y / 2f, s.x / 2f, s.y / 2f));
            return Rect.MinMaxRect(zone.MinX, zone.MinY, zone.MaxX, zone.MaxY);
        }

        /// <summary>A Hazard Daedalus tile makes a Hazard trap; any other tile, or none, a Solid one.</summary>
        public static TileKind KindOf(TileBase tile)
        {
            return tile is DaedalusTile daedalus && daedalus.Kind == TileCollisionKind.Hazard ? TileKind.Hazard : TileKind.Solid;
        }

        /// <summary>The sprite a plain tile draws; null for tiles that can't be drawn on a trap (Rule Tiles, animated tiles).</summary>
        public static Sprite SpriteOf(TileBase tile)
        {
            return tile is Tile plain ? plain.sprite : null;
        }

        /// <summary>
        /// True when a tile is painted on <paramref name="collision"/> in any cell of the trap's box (a Daedalus tile of
        /// kind None doesn't count). Those cells stay as painted, so the trap can't change them.
        /// </summary>
        public static bool CoversPaintedTiles(Trap trap, Tilemap collision)
        {
            if (collision == null)
            {
                return false;
            }
            Rect box = WorldBox(trap);
            for (int y = 0; y < (int)box.height; y++)
            {
                for (int x = 0; x < (int)box.width; x++)
                {
                    Vector3Int cell = collision.WorldToCell(new Vector3(box.xMin + x + 0.5f, box.yMin + y + 0.5f, 0));
                    TileBase tile = collision.GetTile(cell);
                    if (tile != null && !(tile is DaedalusTile daedalus && daedalus.Kind == TileCollisionKind.None))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>A Trap on an Exit or Checkpoint object does nothing yet (a later update gives it its meaning).</summary>
        public static bool IsIgnored(Trap trap)
        {
            return trap.GetComponent<Exit>() != null || trap.GetComponent<Checkpoint>() != null;
        }

        public static TrapSpec ToSpec(Trap trap, Vector2 origin)
        {
            Vector2Int min = CellMin(trap);
            Vector2Int size = ClampedSize(trap);
            // The zone moves with the box when play start snaps an off-grid trap (ApplyPlayLook).
            Vector3 position = trap.transform.position;
            Vector2 snap = SnappedCentre(trap) - new Vector2(position.x, position.y);
            Rect zone = WorldZone(trap);
            zone.position += snap;
            return new TrapSpec(
                Mathf.RoundToInt(min.x - origin.x), Mathf.RoundToInt(min.y - origin.y), size.x, size.y,
                KindOf(trap.Tile),
                trap.Action == TrapAction.Appear ? TrapActionKind.Appear : TrapActionKind.Vanish,
                trap.Trigger == TrapTrigger.Touch ? TrapTriggerKind.Touch : TrapTriggerKind.Zone,
                new Box(zone.xMin - origin.x, zone.yMin - origin.y, zone.xMax - origin.x, zone.yMax - origin.y),
                LevelRules.SecondsToTicks(Mathf.Clamp(trap.Delay, LevelRules.MinTrapDelay, LevelRules.MaxTrapDelay)));
        }

        /// <summary>The trap's Play-mode look: its tile's sprite tiled across the snapped box, shown while it is present.</summary>
        public static void ApplyPlayLook(Trap trap, SpriteRenderer renderer, bool present)
        {
            renderer.sprite = SpriteOf(trap.Tile);
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = ClampedSize(trap);
            Transform t = trap.transform;
            Vector2 centre = SnappedCentre(trap);
            t.position = new Vector3(centre.x, centre.y, t.position.z);
            renderer.enabled = present;
        }
    }
}
