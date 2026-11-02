using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// The moving-box core: traps in motion between cells, as Solid or Hazard boxes the collision queries see;
    /// a trap at rest is trap cells again (systems.md § Traps). Fixed capacity (one slot per trap), so
    /// <see cref="Set"/> and <see cref="Remove"/> allocate nothing; iteration costs only the active boxes, in slot order.
    /// </summary>
    public sealed class MovingBoxes
    {
        readonly Box[] boxes;
        readonly TileKind[] kinds;
        readonly bool[] active;
        // Active slots, ascending.
        readonly int[] activeSlots;

        public MovingBoxes(int capacity)
        {
            if (capacity < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(capacity), capacity, "A capacity can't be negative.");
            }
            boxes = new Box[capacity];
            kinds = new TileKind[capacity];
            active = new bool[capacity];
            activeSlots = new int[capacity];
        }

        public int Capacity => boxes.Length;
        public int ActiveCount { get; private set; }

        public void Set(int slot, Box box, TileKind kind)
        {
            if (kind != TileKind.Solid && kind != TileKind.Hazard)
            {
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "A moving box is Solid or Hazard.");
            }
            boxes[slot] = box;
            kinds[slot] = kind;
            if (active[slot])
            {
                return;
            }
            active[slot] = true;
            int k = ActiveCount++;
            while (k > 0 && activeSlots[k - 1] > slot)
            {
                activeSlots[k] = activeSlots[k - 1];
                k--;
            }
            activeSlots[k] = slot;
        }

        public void Remove(int slot)
        {
            if (!active[slot])
            {
                return;
            }
            active[slot] = false;
            int k = 0;
            while (activeSlots[k] != slot)
            {
                k++;
            }
            ActiveCount--;
            for (; k < ActiveCount; k++)
            {
                activeSlots[k] = activeSlots[k + 1];
            }
        }

        public void Clear()
        {
            Array.Clear(active, 0, active.Length);
            ActiveCount = 0;
        }

        /// <summary>The <paramref name="k"/>-th active box (0 ≤ k &lt; <see cref="ActiveCount"/>).</summary>
        public void GetActive(int k, out Box box, out TileKind kind)
        {
            int slot = activeSlots[k];
            box = boxes[slot];
            kind = kinds[slot];
        }
    }
}
