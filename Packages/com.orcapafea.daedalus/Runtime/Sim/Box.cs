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

        /// <summary>This box grown by <paramref name="margin"/> on every side (shrunk when negative).</summary>
        public Box Grown(float margin)
        {
            return new Box(MinX - margin, MinY - margin, MaxX + margin, MaxY + margin);
        }
    }
}
