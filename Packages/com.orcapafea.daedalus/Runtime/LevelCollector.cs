using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Gathers the scene's checkpoints, exits and traps at play start: one <see cref="PlayStartScan"/> each, then a
    /// deterministic sort by world box (min x, min y, width, height), so a student never wires them to the Player.
    /// Never called per frame.
    /// </summary>
    internal static class LevelCollector
    {
        public static Checkpoint[] FindCheckpoints()
        {
            Checkpoint[] found = PlayStartScan.FindActive<Checkpoint>();
            Array.Sort(found, (a, b) => Compare(a.WorldBox, b.WorldBox));
            return found;
        }

        public static Exit[] FindExits()
        {
            Exit[] found = PlayStartScan.FindActive<Exit>();
            Array.Sort(found, (a, b) => Compare(a.WorldBox, b.WorldBox));
            return found;
        }

        /// <summary>Every active Trap except those on an Exit or Checkpoint object (they do nothing yet), sorted by snapped box.</summary>
        public static Trap[] FindTraps()
        {
            return FindTraps(out _);
        }

        /// <summary>As <see cref="FindTraps()"/>; <paramref name="ignored"/> gets the Traps left out (on an Exit or Checkpoint).</summary>
        public static Trap[] FindTraps(out Trap[] ignored)
        {
            Trap[] found = PlayStartScan.FindActive<Trap>();
            int count = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (!TrapLook.IsIgnored(found[i]))
                {
                    count++;
                }
            }
            var traps = new Trap[count];
            ignored = new Trap[found.Length - count];
            int kept = 0, left = 0;
            for (int i = 0; i < found.Length; i++)
            {
                if (TrapLook.IsIgnored(found[i]))
                {
                    ignored[left++] = found[i];
                }
                else
                {
                    traps[kept++] = found[i];
                }
            }
            Array.Sort(traps, (a, b) => Compare(TrapLook.WorldBox(a), TrapLook.WorldBox(b)));
            return traps;
        }

        public static Rect[] Boxes(Trap[] traps)
        {
            var boxes = new Rect[traps.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i] = TrapLook.WorldBox(traps[i]);
            }
            return boxes;
        }

        public static Rect[] Boxes(Checkpoint[] checkpoints)
        {
            var boxes = new Rect[checkpoints.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i] = checkpoints[i].WorldBox;
            }
            return boxes;
        }

        public static Rect[] Boxes(Exit[] exits)
        {
            var boxes = new Rect[exits.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                boxes[i] = exits[i].WorldBox;
            }
            return boxes;
        }

        /// <summary>
        /// A checkpoint's or exit's box: its sprite's world bounds when it has a sprite, else one tile centred on its
        /// position.
        /// </summary>
        public static Rect ElementBox(Component element)
        {
            var renderer = element.GetComponent<SpriteRenderer>();
            if (renderer != null && renderer.sprite != null)
            {
                Bounds bounds = renderer.bounds;
                return Rect.MinMaxRect(bounds.min.x, bounds.min.y, bounds.max.x, bounds.max.y);
            }
            Vector3 position = element.transform.position;
            return new Rect(position.x - 0.5f, position.y - 0.5f, 1f, 1f);
        }

        /// <summary>The union of <paramref name="include"/> and every box.</summary>
        public static Rect Grow(Rect include, Rect[] boxes)
        {
            for (int i = 0; i < boxes.Length; i++)
            {
                include = Rect.MinMaxRect(
                    Mathf.Min(include.xMin, boxes[i].xMin), Mathf.Min(include.yMin, boxes[i].yMin),
                    Mathf.Max(include.xMax, boxes[i].xMax), Mathf.Max(include.yMax, boxes[i].yMax));
            }
            return include;
        }

        /// <summary>The boxes in sim coordinates, where <paramref name="origin"/> is the world position of sim cell (0, 0).</summary>
        public static Box[] ToBoxes(Rect[] world, Vector2 origin)
        {
            var boxes = new Box[world.Length];
            for (int i = 0; i < boxes.Length; i++)
            {
                Rect r = world[i];
                boxes[i] = new Box(r.xMin - origin.x, r.yMin - origin.y, r.xMax - origin.x, r.yMax - origin.y);
            }
            return boxes;
        }

        static int Compare(Rect a, Rect b)
        {
            int c = a.xMin.CompareTo(b.xMin);
            if (c == 0)
            {
                c = a.yMin.CompareTo(b.yMin);
            }
            if (c == 0)
            {
                c = a.width.CompareTo(b.width);
            }
            return c != 0 ? c : a.height.CompareTo(b.height);
        }
    }
}
