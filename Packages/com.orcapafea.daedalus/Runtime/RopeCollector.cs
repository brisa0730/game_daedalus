using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Gathers the scene's ropes at play start: one <see cref="PlayStartScan"/>, then a deterministic sort,
    /// so a student never wires ropes to the Player. Never called per frame.
    /// </summary>
    internal static class RopeCollector
    {
        /// <summary>Every active and enabled Rope, sorted by (x, bottom, top) so the sim's "lowest index wins" is stable.</summary>
        public static Rope[] FindSorted()
        {
            Rope[] ropes = PlayStartScan.FindActive<Rope>();
            Array.Sort(ropes, Compare);
            return ropes;
        }

        /// <summary>The world rect around every rope's line; false (and an empty rect) when there are no ropes.</summary>
        public static bool TryGetWorldBounds(Rope[] ropes, out Rect bounds)
        {
            bounds = default;
            if (ropes.Length == 0)
            {
                return false;
            }
            Vector3 first = ropes[0].WorldLine;
            float xMin = first.x, xMax = first.x, yMin = first.y, yMax = first.z;
            for (int i = 1; i < ropes.Length; i++)
            {
                Vector3 line = ropes[i].WorldLine;
                xMin = Mathf.Min(xMin, line.x);
                xMax = Mathf.Max(xMax, line.x);
                yMin = Mathf.Min(yMin, line.y);
                yMax = Mathf.Max(yMax, line.z);
            }
            bounds = Rect.MinMaxRect(xMin, yMin, xMax, yMax);
            return true;
        }

        public static RopeSegment[] ToSegments(Rope[] ropes, Vector2 origin)
        {
            var segments = new RopeSegment[ropes.Length];
            for (int i = 0; i < ropes.Length; i++)
            {
                segments[i] = ropes[i].ToSegment(origin);
            }
            return segments;
        }

        static int Compare(Rope a, Rope b)
        {
            Vector3 lineA = a.WorldLine;
            Vector3 lineB = b.WorldLine;
            int byX = lineA.x.CompareTo(lineB.x);
            if (byX != 0)
            {
                return byX;
            }
            int byBottom = lineA.y.CompareTo(lineB.y);
            return byBottom != 0 ? byBottom : lineA.z.CompareTo(lineB.z);
        }
    }
}
