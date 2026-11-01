using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>One vertical, ladder-style rope: its centre line <see cref="X"/> from <see cref="Bottom"/> to <see cref="Top"/>, in tiles.</summary>
    public readonly struct RopeSegment
    {
        /// <summary>The Rope component's length setting, in tiles.</summary>
        public const float DefaultLength = 4f;
        public const float MinLength = 1f;
        public const float MaxLength = 50f;

        public RopeSegment(float x, float bottom, float top)
        {
            if (!(top > bottom))
            {
                throw new ArgumentException($"A rope's top ({top}) must be above its bottom ({bottom}).");
            }
            X = x;
            Bottom = bottom;
            Top = top;
        }

        public float X { get; }
        public float Bottom { get; }
        public float Top { get; }

        /// <summary>
        /// This rope with its top moved exactly onto the nearest one-way surface in its column within
        /// <see cref="MovementInternals.RopeTopSnap"/> (ties go to the higher surface), so the player can step
        /// off onto it. Unchanged when there is none, or when the snap would put the top at or below the bottom.
        /// </summary>
        public RopeSegment SnapTopTo(TileGrid grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }
            const float Snap = MovementInternals.RopeTopSnap;
            int column = (int)MathF.Floor(X);
            int highestRow = (int)MathF.Ceiling(Top + Snap) - 1;
            int lowestRow = (int)MathF.Floor(Top - Snap) - 1;
            float bestSurface = 0f;
            float bestDistance = float.MaxValue;
            // Higher rows first, and only a strictly nearer surface replaces the best: ties go up.
            for (int row = highestRow; row >= lowestRow; row--)
            {
                if (grid.Get(column, row) != TileKind.OneWay)
                {
                    continue;
                }
                float surface = row + 1;
                float distance = MathF.Abs(surface - Top);
                if (distance <= Snap && distance < bestDistance)
                {
                    bestDistance = distance;
                    bestSurface = surface;
                }
            }
            if (bestDistance == float.MaxValue || !(bestSurface > Bottom))
            {
                return this;
            }
            return new RopeSegment(X, Bottom, bestSurface);
        }
    }
}
