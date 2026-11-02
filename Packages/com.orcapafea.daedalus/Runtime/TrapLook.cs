using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// A trap's box math, shared by the runtime and the editor tools so both read the same box: the size, offset and
    /// speed clamped to the sim's range, the box centred on the object and snapped to whole cells (or, on an Exit, the
    /// exit's box), the Move end, the Fall landing, the zone, the kind and the sprite.
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

        /// <summary>The one Move offset every reader uses: whole tiles, each axis clamped to the sim's range.</summary>
        public static Vector2Int ClampedOffset(Trap trap)
        {
            Vector2Int offset = trap.Offset;
            return new Vector2Int(
                Mathf.Clamp(offset.x, LevelRules.MinMoveOffset, LevelRules.MaxMoveOffset),
                Mathf.Clamp(offset.y, LevelRules.MinMoveOffset, LevelRules.MaxMoveOffset));
        }

        /// <summary>The offset that puts the end box's centre nearest <paramref name="worldEndCentre"/>: whole tiles, clamped to the range.</summary>
        public static Vector2Int OffsetForEnd(Trap trap, Vector2 worldEndCentre)
        {
            Vector2 offset = worldEndCentre - WorldBox(trap).center;
            return new Vector2Int(
                Mathf.Clamp(Mathf.RoundToInt(offset.x), LevelRules.MinMoveOffset, LevelRules.MaxMoveOffset),
                Mathf.Clamp(Mathf.RoundToInt(offset.y), LevelRules.MinMoveOffset, LevelRules.MaxMoveOffset));
        }

        /// <summary>The one Move speed every reader uses, clamped to the sim's range (non-finite → the default).</summary>
        public static float ClampedSpeed(Trap trap)
        {
            float speed = trap.Speed;
            return float.IsNaN(speed) || float.IsInfinity(speed)
                ? LevelRules.DefaultMoveSpeed
                : Mathf.Clamp(speed, LevelRules.MinMoveSpeed, LevelRules.MaxMoveSpeed);
        }

        /// <summary>
        /// A Trap beside an enabled Exit moves that exit. <c>enabled</c>, not <c>isActiveAndEnabled</c>: on a prefab asset
        /// every component reads inactive, and its runaway exit must not read as turned off (round 2, F16).
        /// </summary>
        public static bool IsOnExit(Trap trap)
        {
            var exit = trap.GetComponent<Exit>();
            return exit != null && exit.enabled;
        }

        public static bool IsOnDisabledExit(Trap trap)
        {
            var exit = trap.GetComponent<Exit>();
            return exit != null && !exit.enabled;
        }

        public static bool IsOnCheckpoint(Trap trap)
        {
            return trap.GetComponent<Checkpoint>() != null;
        }

        /// <summary>A Trap on a Checkpoint, or beside a turned-off Exit, does nothing (with a warning).</summary>
        public static bool IsIgnored(Trap trap)
        {
            return IsOnCheckpoint(trap) || IsOnDisabledExit(trap);
        }

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

        /// <summary>False on an Exit: its box is the exit's sprite, never snapped.</summary>
        public static bool IsOffGrid(Trap trap)
        {
            if (IsOnExit(trap))
            {
                return false;
            }
            Vector3 position = trap.transform.position;
            return Vector2.Distance(new Vector2(position.x, position.y), SnappedCentre(trap)) > OffGridTolerance;
        }

        /// <summary>The trap's box in world space: the exit's box on an Exit, else the snapped tile box.</summary>
        public static Rect WorldBox(Trap trap)
        {
            var exit = trap.GetComponent<Exit>();
            if (exit != null && exit.enabled)
            {
                return LevelCollector.ElementBox(exit);
            }
            Vector2Int min = CellMin(trap);
            Vector2Int size = ClampedSize(trap);
            return new Rect(min.x, min.y, size.x, size.y);
        }

        /// <summary>Where the box ends: <see cref="WorldBox"/> plus the clamped offset for a Move, else <see cref="WorldBox"/>.</summary>
        public static Rect EndBox(Trap trap)
        {
            Rect box = WorldBox(trap);
            if (trap.Action == TrapAction.Move)
            {
                box.position += (Vector2)ClampedOffset(trap);
            }
            return box;
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

        /// <summary>
        /// The trap's default zone relative to the object: on an Exit, the exit's box grown on every side
        /// (<see cref="LevelRules.DefaultExitZone"/>); else <see cref="DefaultZoneLocal(Vector2Int)"/> for its size.
        /// </summary>
        public static Rect DefaultZoneLocal(Trap trap)
        {
            if (!IsOnExit(trap))
            {
                return DefaultZoneLocal(ClampedSize(trap));
            }
            Rect box = WorldBox(trap);
            Vector3 p = trap.transform.position;
            Box zone = LevelRules.DefaultExitZone(new Box(box.xMin - p.x, box.yMin - p.y, box.xMax - p.x, box.yMax - p.y));
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
        /// kind None doesn't count). Those cells stay as painted, so the trap can't change them. False on an Exit.
        /// </summary>
        public static bool CoversPaintedTiles(Trap trap, Tilemap collision)
        {
            if (collision == null || IsOnExit(trap))
            {
                return false;
            }
            return AnyCell(WorldBox(trap), collision, kind => kind != TileKind.None);
        }

        /// <summary>True when a Move trap's end box covers painted ground (Solid or Climbable): it would stop buried there.</summary>
        public static bool EndCoversPaintedSolid(Trap trap, Tilemap collision)
        {
            if (collision == null || trap.Action != TrapAction.Move || IsOnExit(trap))
            {
                return false;
            }
            return AnyCell(EndBox(trap), collision, TileCollision.Blocks);
        }

        // The box's cells (whole-cell boxes only) through the Collision Tilemap's own tile → kind mapping.
        static bool AnyCell(Rect box, Tilemap collision, Predicate<TileKind> match)
        {
            for (int y = 0; y < (int)box.height; y++)
            {
                for (int x = 0; x < (int)box.width; x++)
                {
                    Vector3Int cell = collision.WorldToCell(new Vector3(box.xMin + x + 0.5f, box.yMin + y + 0.5f, 0));
                    TileBase tile = collision.GetTile(cell);
                    if (tile != null && match(CollisionMapReader.KindOf(tile)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Where a Fall trap lands, by the sim's own landing rule and arithmetic (<see cref="TrapMotion"/>): the column strip
        /// under its box is read from <paramref name="collision"/> down to its painted bottom. False = it falls out of the
        /// map. <paramref name="tiles"/> is the drop and <paramref name="ticks"/> the travel time at 60 Hz (0 = already
        /// resting). Editor-time and play-start only; never per frame.
        /// </summary>
        public static bool FallLanding(Trap trap, Tilemap collision, MovementSettings settings, out float tiles, out int ticks)
        {
            tiles = 0;
            ticks = 0;
            Rect box = WorldBox(trap);
            if (collision == null)
            {
                return false;
            }
            Vector3Int first = collision.WorldToCell(new Vector3(box.xMin + 1e-4f, box.yMin, 0));
            Vector3Int last = collision.WorldToCell(new Vector3(box.xMax - 1e-4f, box.yMax - 1e-4f, 0));
            int bottom = Math.Min(collision.cellBounds.yMin, first.y);
            int width = last.x - first.x + 1;
            int height = last.y - bottom + 1;
            var grid = new TileGrid(width, height);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    TileBase tile = collision.GetTile(new Vector3Int(first.x + x, bottom + y, 0));
                    if (tile != null)
                    {
                        grid.Set(x, y, CollisionMapReader.KindOf(tile));
                    }
                }
            }
            Vector3 origin = collision.CellToWorld(new Vector3Int(first.x, bottom, 0));
            var simBox = new Box(box.xMin - origin.x, box.yMin - origin.y, box.xMax - origin.x, box.yMax - origin.y);
            if (!TrapMotion.TryLandingRow(grid, simBox, out int row))
            {
                return false;
            }
            var derived = new DerivedMovement(settings);
            ticks = TrapMotion.FallTicks(grid, simBox, derived.RiseGravity, derived.MaxFallSpeed);
            tiles = simBox.MinY - (row + 1);
            return true;
        }

        /// <summary>
        /// The editor's Fall preview: the box where the trap lands (<see cref="FallLanding(Trap, Tilemap, MovementSettings, out float, out int)"/>
        /// with <paramref name="player"/>'s settings, or the defaults without one). False = it falls out of the map.
        /// </summary>
        public static bool FallLandingBox(Trap trap, Tilemap collision, Player player, out Rect landing)
        {
            landing = WorldBox(trap);
            if (!FallLanding(trap, collision, SettingsOf(player), out float tiles, out _))
            {
                return false;
            }
            landing.y -= tiles;
            return true;
        }

        /// <summary>The settings a Fall uses: <paramref name="player"/>'s, or the defaults without a Player.</summary>
        public static MovementSettings SettingsOf(Player player)
        {
            return player != null ? player.BuildSettings() : MovementSettings.Default;
        }

        /// <summary>
        /// The trap in sim cells; <paramref name="exitIndex"/> is its Exit's index in the level's exit list (a Trap on an
        /// Exit needs one). An undefined Action reads as Vanish.
        /// </summary>
        public static TrapSpec ToSpec(Trap trap, Vector2 origin, int exitIndex = -1)
        {
            TrapActionKind action = trap.Action switch
            {
                TrapAction.Appear => TrapActionKind.Appear,
                TrapAction.Move => TrapActionKind.Move,
                TrapAction.Fall => TrapActionKind.Fall,
                _ => TrapActionKind.Vanish,
            };
            TrapTriggerKind trigger = trap.Trigger == TrapTrigger.Touch ? TrapTriggerKind.Touch : TrapTriggerKind.Zone;
            int delay = LevelRules.SecondsToTicks(Mathf.Clamp(trap.Delay, LevelRules.MinTrapDelay, LevelRules.MaxTrapDelay));
            Vector2Int offset = ClampedOffset(trap);
            float speed = ClampedSpeed(trap);
            Rect zone = WorldZone(trap);
            if (IsOnExit(trap))
            {
                if (exitIndex < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(exitIndex), exitIndex, "A Trap on an Exit needs its exit's index.");
                }
                Rect exit = WorldBox(trap);
                return TrapSpec.ForExit(exitIndex,
                    new Box(exit.xMin - origin.x, exit.yMin - origin.y, exit.xMax - origin.x, exit.yMax - origin.y),
                    action, trigger,
                    new Box(zone.xMin - origin.x, zone.yMin - origin.y, zone.xMax - origin.x, zone.yMax - origin.y),
                    delay, offset.x, offset.y, speed);
            }
            Vector2Int min = CellMin(trap);
            Vector2Int size = ClampedSize(trap);
            // The zone moves with the box when play start snaps an off-grid trap (ApplyPlayLook).
            Vector3 position = trap.transform.position;
            zone.position += SnappedCentre(trap) - new Vector2(position.x, position.y);
            return new TrapSpec(
                Mathf.RoundToInt(min.x - origin.x), Mathf.RoundToInt(min.y - origin.y), size.x, size.y,
                KindOf(trap.Tile), action, trigger,
                new Box(zone.xMin - origin.x, zone.yMin - origin.y, zone.xMax - origin.x, zone.yMax - origin.y),
                delay, offset.x, offset.y, speed);
        }

        /// <summary>
        /// The trap's Play-mode look: its tile's sprite tiled across the snapped box, shown while it is present. On an
        /// Exit only the shown state: the exit keeps its own sprite and place.
        /// </summary>
        public static void ApplyPlayLook(Trap trap, SpriteRenderer renderer, bool present)
        {
            if (IsOnExit(trap))
            {
                renderer.enabled = present;
                return;
            }
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
