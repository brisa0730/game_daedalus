using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// One trap in sim cells: a box of whole cells of one kind, its trigger, its action and its delay, plus a Move's
    /// offset and speed. A trap on an exit (<see cref="ForExit"/>) moves the exit's box instead and has no cells.
    /// </summary>
    public readonly struct TrapSpec
    {
        public TrapSpec(int cellX, int cellY, int width, int height, TileKind kind, TrapActionKind action,
            TrapTriggerKind trigger, Box zone, int delayTicks, int offsetX = 0, int offsetY = 0, float speed = LevelRules.DefaultMoveSpeed)
            : this(cellX, cellY, width, height, kind, action, trigger, zone, delayTicks, offsetX, offsetY, speed, -1,
                new Box(cellX, cellY, cellX + Math.Max(width, 0), cellY + Math.Max(height, 0)))
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(height), $"A trap needs at least one cell; got {width} x {height}.");
            }
            if (kind != TileKind.Solid && kind != TileKind.Hazard)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "A trap is Solid or Hazard.");
            }
        }

        TrapSpec(int cellX, int cellY, int width, int height, TileKind kind, TrapActionKind action, TrapTriggerKind trigger,
            Box zone, int delayTicks, int offsetX, int offsetY, float speed, int exitIndex, Box startBox)
        {
            if (delayTicks < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(delayTicks), delayTicks, "A trap's delay can't be negative.");
            }
            CellX = cellX;
            CellY = cellY;
            Width = width;
            Height = height;
            Kind = kind;
            Action = action;
            Trigger = trigger;
            Zone = zone;
            DelayTicks = delayTicks;
            OffsetX = Math.Clamp(offsetX, LevelRules.MinMoveOffset, LevelRules.MaxMoveOffset);
            OffsetY = Math.Clamp(offsetY, LevelRules.MinMoveOffset, LevelRules.MaxMoveOffset);
            Speed = float.IsFinite(speed) ? Math.Clamp(speed, LevelRules.MinMoveSpeed, LevelRules.MaxMoveSpeed) : LevelRules.DefaultMoveSpeed;
            ExitIndex = exitIndex;
            StartBox = startBox;
        }

        /// <summary>A trap on exit <paramref name="exitIndex"/>: it moves, hides or shows that exit's box, never blocks, and has no cells (<see cref="Kind"/> None).</summary>
        public static TrapSpec ForExit(int exitIndex, Box exitBox, TrapActionKind action, TrapTriggerKind trigger, Box zone,
            int delayTicks, int offsetX, int offsetY, float speed)
        {
            if (exitIndex < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(exitIndex), exitIndex, "An exit trap needs an exit.");
            }
            return new TrapSpec(0, 0, 0, 0, TileKind.None, action, trigger, zone, delayTicks, offsetX, offsetY, speed, exitIndex, exitBox);
        }

        /// <summary>Tile traps only (0 on an exit trap).</summary>
        public int CellX { get; }
        public int CellY { get; }
        public int Width { get; }
        public int Height { get; }
        /// <summary>Solid or Hazard; None on an exit trap.</summary>
        public TileKind Kind { get; }
        public TrapActionKind Action { get; }
        public TrapTriggerKind Trigger { get; }
        /// <summary>Used only with <see cref="TrapTriggerKind.Zone"/>.</summary>
        public Box Zone { get; }
        public int DelayTicks { get; }
        /// <summary>Move only: whole tiles from the start to the end, clamped to <see cref="LevelRules.MinMoveOffset"/>..<see cref="LevelRules.MaxMoveOffset"/>.</summary>
        public int OffsetX { get; }
        public int OffsetY { get; }
        /// <summary>Move only: tiles per second, clamped to <see cref="LevelRules.MinMoveSpeed"/>..<see cref="LevelRules.MaxMoveSpeed"/>.</summary>
        public float Speed { get; }
        /// <summary>The exit this trap moves; −1 = a tile trap.</summary>
        public int ExitIndex { get; }
        /// <summary>Where the trap's box starts: <see cref="CellBox"/> for a tile trap, the exit's box for an exit trap.</summary>
        public Box StartBox { get; }

        public Box CellBox => new Box(CellX, CellY, CellX + Width, CellY + Height);
    }
}
