using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>An axis-aligned box in tiles (sim coordinates).</summary>
    public readonly struct Box
    {
        public Box(float minX, float minY, float maxX, float maxY)
        {
            if (!(maxX >= minX) || !(maxY >= minY))
            {
                throw new ArgumentException($"A box's max ({maxX}, {maxY}) must not be below its min ({minX}, {minY}).");
            }
            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public float MinX { get; }
        public float MinY { get; }
        public float MaxX { get; }
        public float MaxY { get; }

        /// <summary>The player's hitbox for feet at (<paramref name="feetX"/>, <paramref name="feetY"/>).</summary>
        public static Box Hitbox(float feetX, float feetY)
        {
            return new Box(feetX - MovementInternals.HitboxHalfWidth, feetY,
                feetX + MovementInternals.HitboxHalfWidth, feetY + MovementInternals.HitboxHeight);
        }

        /// <summary>Strict: the boxes share a positive area. Touching edges do not overlap.</summary>
        public bool Overlaps(in Box other)
        {
            return MinX < other.MaxX && other.MinX < MaxX && MinY < other.MaxY && other.MinY < MaxY;
        }

        /// <summary>
        /// Strict, like <see cref="Overlaps"/>: <paramref name="from"/> moving along (<paramref name="dx"/>, <paramref name="dy"/>)
        /// overlaps the still <paramref name="target"/> at some point of the move, ends included.
        /// </summary>
        public static bool SweptOverlaps(in Box from, float dx, float dy, in Box target)
        {
            float enter = float.NegativeInfinity;
            float exit = float.PositiveInfinity;
            if (!Slab(from.MinX, from.MaxX, dx, target.MinX, target.MaxX, ref enter, ref exit)
                || !Slab(from.MinY, from.MaxY, dy, target.MinY, target.MaxY, ref enter, ref exit))
            {
                return false;
            }
            // Some t in [0, 1] lies strictly inside (enter, exit).
            return enter < exit && enter < 1 && exit > 0;
        }

        // Narrows (enter, exit) to the open t-range where [min + t·d, max + t·d] strictly overlaps [targetMin, targetMax].
        static bool Slab(float min, float max, float d, float targetMin, float targetMax, ref float enter, ref float exit)
        {
            if (d == 0)
            {
                return min < targetMax && targetMin < max;
            }
            float a = (targetMin - max) / d;
            float b = (targetMax - min) / d;
            enter = MathF.Max(enter, MathF.Min(a, b));
            exit = MathF.Min(exit, MathF.Max(a, b));
            return true;
        }

        /// <summary>This box grown by <paramref name="margin"/> on every side (shrunk when negative).</summary>
        public Box Grown(float margin)
        {
            return new Box(MinX - margin, MinY - margin, MaxX + margin, MaxY + margin);
        }
    }
}
