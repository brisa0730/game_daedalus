using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>One trap in sim cells: a box of whole cells of one kind, its trigger, its action and its delay.</summary>
    public readonly struct TrapSpec
    {
        public TrapSpec(int cellX, int cellY, int width, int height, TileKind kind, TrapActionKind action,
            TrapTriggerKind trigger, Box zone, int delayTicks)
        {
            if (width < 1 || height < 1)
            {
                throw new ArgumentOutOfRangeException(width < 1 ? nameof(width) : nameof(height), $"A trap needs at least one cell; got {width} x {height}.");
            }
            if (kind != TileKind.Solid && kind != TileKind.Hazard)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "A trap is Solid or Hazard.");
            }
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
        }

        public int CellX { get; }
        public int CellY { get; }
        public int Width { get; }
        public int Height { get; }
        public TileKind Kind { get; }
        public TrapActionKind Action { get; }
        public TrapTriggerKind Trigger { get; }
        /// <summary>Used only with <see cref="TrapTriggerKind.Zone"/>.</summary>
        public Box Zone { get; }
        public int DelayTicks { get; }

        public Box CellBox => new Box(CellX, CellY, CellX + Width, CellY + Height);
    }
}
