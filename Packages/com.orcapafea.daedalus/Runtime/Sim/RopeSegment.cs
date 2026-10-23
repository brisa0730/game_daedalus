using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>One vertical, ladder-style rope: its centre line <see cref="X"/> from <see cref="Bottom"/> to <see cref="Top"/>, in tiles.</summary>
    public readonly struct RopeSegment
    {
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
    }
}
