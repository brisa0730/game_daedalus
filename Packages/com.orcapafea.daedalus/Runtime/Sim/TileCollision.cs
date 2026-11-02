using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// Box-against-tile collision on the <see cref="TileGrid"/>. A box is given as its feet centre
    /// (x = centre, y = bottom), half width and height. It covers the cells it overlaps, half-open:
    /// a box resting exactly on a face does not overlap the next cell. Solid and Climbable block
    /// both axes; OneWay blocks only a downward move whose start was at or above its top.
    /// Callers sweep X first (at the old height), then Y.
    /// Every query also tests the grid's moving boxes (<see cref="TileGrid.Boxes"/>): a Solid box blocks like a Solid
    /// cell wherever it is (never one-way, never Climbable), a Hazard box only kills, and the nearest hit, cell or box,
    /// wins. Box overlap is strict within the contact epsilon, as cells are covered: touching faces don't overlap.
    /// </summary>
    public static class TileCollision
    {
        const float Epsilon = MovementInternals.ContactEpsilon;

        public static bool Blocks(TileKind kind)
        {
            return kind == TileKind.Solid || kind == TileKind.Climbable;
        }

        /// <summary>Moves the box horizontally by <paramref name="dx"/>, stopping with its leading edge on the first blocking face.</summary>
        public static float SweepX(TileGrid grid, float x, float y, float halfWidth, float height, float dx, out int hitSide)
        {
            hitSide = 0;
            if (dx == 0)
            {
                return x;
            }
            int rowFirst = FirstCovered(y);
            int rowLast = LastCovered(y + height);
            MovingBoxes boxes = grid.Boxes;
            if (dx > 0)
            {
                float right = x + halfWidth;
                float end = right + dx;
                float face = float.PositiveInfinity;
                for (int c = (int)MathF.Ceiling(right - Epsilon); c < end; c++)
                {
                    if (ColumnBlocks(grid, c, rowFirst, rowLast, true))
                    {
                        face = c;
                        break;
                    }
                }
                for (int k = 0; k < boxes.ActiveCount; k++)
                {
                    boxes.GetActive(k, out Box b, out TileKind kind);
                    if (kind == TileKind.Solid && b.MinX >= right - Epsilon && b.MinX < end && b.MinX < face
                        && SpansOverlap(y, y + height, b.MinY, b.MaxY))
                    {
                        face = b.MinX;
                    }
                }
                if (face < end)
                {
                    hitSide = 1;
                    return face - halfWidth;
                }
            }
            else
            {
                float left = x - halfWidth;
                float end = left + dx;
                float face = float.NegativeInfinity;
                for (int c = (int)MathF.Floor(left + Epsilon) - 1; c + 1 > end; c--)
                {
                    if (ColumnBlocks(grid, c, rowFirst, rowLast, true))
                    {
                        face = c + 1;
                        break;
                    }
                }
                for (int k = 0; k < boxes.ActiveCount; k++)
                {
                    boxes.GetActive(k, out Box b, out TileKind kind);
                    if (kind == TileKind.Solid && b.MaxX <= left + Epsilon && b.MaxX > end && b.MaxX > face
                        && SpansOverlap(y, y + height, b.MinY, b.MaxY))
                    {
                        face = b.MaxX;
                    }
                }
                if (face > end)
                {
                    hitSide = -1;
                    return face + halfWidth;
                }
            }
            return x + dx;
        }

        /// <summary>
        /// Moves the box vertically by <paramref name="dy"/>. Down: lands on the first Solid/Climbable top, or
        /// OneWay top, and reports what it landed on. Up: stops the head on a blocking cell's bottom and reports
        /// <paramref name="ceiling"/>, unless the ceiling-corner correction slides the box sideways (at most
        /// <see cref="MovementInternals.CeilingCornerCorrection"/>) past it. On a rope (<paramref name="onRope"/>)
        /// one-ways are ignored and the box is never slid off the rope.
        /// </summary>
        public static void SweepY(TileGrid grid, ref float x, ref float y, float halfWidth, float height, float dy,
            bool onRope, out SupportKind landedOn, out bool ceiling)
        {
            landedOn = SupportKind.None;
            ceiling = false;
            if (dy == 0)
            {
                return;
            }
            int colFirst = FirstCovered(x - halfWidth);
            int colLast = LastCovered(x + halfWidth);
            MovingBoxes boxes = grid.Boxes;
            if (dy < 0)
            {
                // Only rows whose top is at or below the start bottom are crossed, which is the one-way rule:
                // a one-way catches the box only if its feet were at or above the top before the move.
                float end = y + dy;
                float landTop = float.NegativeInfinity;
                for (int r = (int)MathF.Floor(y + Epsilon) - 1; r + 1 > end; r--)
                {
                    SupportKind support = RowSupport(grid, r, colFirst, colLast, onRope);
                    if (support != SupportKind.None)
                    {
                        landTop = r + 1;
                        landedOn = support;
                        break;
                    }
                }
                // A Solid box top the feet cross (from at or above it) lands them too; the higher landing wins.
                for (int k = 0; k < boxes.ActiveCount; k++)
                {
                    boxes.GetActive(k, out Box b, out TileKind kind);
                    if (kind == TileKind.Solid && b.MaxY <= y + Epsilon && b.MaxY > end && b.MaxY >= landTop
                        && SpansOverlap(x - halfWidth, x + halfWidth, b.MinX, b.MaxX))
                    {
                        landTop = b.MaxY;
                        landedOn = SupportKind.Solid;
                    }
                }
                y = landedOn != SupportKind.None ? landTop : end;
                return;
            }

            float top = y + height;
            float endTop = top + dy;
            // The nearest Solid box bottom the head crosses; a box nearer than every blocking row stops the head
            // without the ceiling-corner correction, which slides past cell corners only.
            float boxBottom = float.PositiveInfinity;
            for (int k = 0; k < boxes.ActiveCount; k++)
            {
                boxes.GetActive(k, out Box b, out TileKind kind);
                if (kind == TileKind.Solid && b.MinY >= top - Epsilon && b.MinY < endTop && b.MinY < boxBottom
                    && SpansOverlap(x - halfWidth, x + halfWidth, b.MinX, b.MaxX))
                {
                    boxBottom = b.MinY;
                }
            }
            for (int r = (int)MathF.Ceiling(top - Epsilon); r < endTop && r <= boxBottom; r++)
            {
                if (!RowBlocks(grid, r, colFirst, colLast))
                {
                    continue;
                }
                if (!onRope && TryCornerCorrection(grid, ref x, y, halfWidth, height, dy, r, colFirst, colLast))
                {
                    y += dy;
                    return;
                }
                y = r - height;
                ceiling = true;
                return;
            }
            if (boxBottom < endTop)
            {
                y = boxBottom - height;
                ceiling = true;
                return;
            }
            y += dy;
        }

        /// <summary>
        /// What lies under a box's bottom: cells when the bottom is on an integer row, and Solid boxes whose top is at
        /// the bottom (at any height), each within the contact epsilon. Straddling solid wins.
        /// </summary>
        public static SupportKind Support(TileGrid grid, float x, float y, float halfWidth)
        {
            MovingBoxes boxes = grid.Boxes;
            for (int k = 0; k < boxes.ActiveCount; k++)
            {
                boxes.GetActive(k, out Box b, out TileKind kind);
                if (kind == TileKind.Solid && MathF.Abs(b.MaxY - y) <= Epsilon && SpansOverlap(x - halfWidth, x + halfWidth, b.MinX, b.MaxX))
                {
                    return SupportKind.Solid;
                }
            }
            float row = MathF.Round(y);
            if (MathF.Abs(y - row) > Epsilon)
            {
                return SupportKind.None;
            }
            return RowSupport(grid, (int)row - 1, FirstCovered(x - halfWidth), LastCovered(x + halfWidth), false);
        }

        /// <summary>
        /// Distance from the box's side to the nearest wall face on <paramref name="side"/> (−1 left, +1 right)
        /// within <paramref name="reach"/>, else +∞. A wall is Solid (and Climbable when <paramref name="climbableIsWall"/>)
        /// or the map side; OneWay never is.
        /// </summary>
        public static float WallGap(TileGrid grid, float x, float y, float halfWidth, float height, int side, float reach, bool climbableIsWall)
        {
            int rowFirst = FirstCovered(y);
            int rowLast = LastCovered(y + height);
            float gap = float.PositiveInfinity;
            float near = side > 0 ? x + halfWidth : x - halfWidth;
            if (side > 0)
            {
                for (int c = (int)MathF.Ceiling(near - Epsilon); c - near <= reach + Epsilon; c++)
                {
                    if (ColumnBlocks(grid, c, rowFirst, rowLast, climbableIsWall))
                    {
                        gap = MathF.Max(0, c - near);
                        break;
                    }
                }
            }
            else
            {
                for (int c = (int)MathF.Floor(near + Epsilon) - 1; near - (c + 1) <= reach + Epsilon; c--)
                {
                    if (ColumnBlocks(grid, c, rowFirst, rowLast, climbableIsWall))
                    {
                        gap = MathF.Max(0, near - (c + 1));
                        break;
                    }
                }
            }
            // A Solid box's face is a wall (never Climbable, so climbableIsWall doesn't matter).
            MovingBoxes boxes = grid.Boxes;
            for (int k = 0; k < boxes.ActiveCount; k++)
            {
                boxes.GetActive(k, out Box b, out TileKind kind);
                if (kind != TileKind.Solid || !SpansOverlap(y, y + height, b.MinY, b.MaxY))
                {
                    continue;
                }
                float boxGap = side > 0 ? b.MinX - near : near - b.MaxX;
                if (boxGap >= -Epsilon && boxGap <= reach + Epsilon)
                {
                    gap = MathF.Min(gap, MathF.Max(0, boxGap));
                }
            }
            return gap;
        }

        public static bool IsClimbableAt(TileGrid grid, float x, float y)
        {
            return grid.Get((int)MathF.Floor(x), (int)MathF.Floor(y)) == TileKind.Climbable;
        }

        /// <summary>True when the box overlaps no blocking cell nor Solid moving box and lies inside the map's sides and top. Below the map is open.</summary>
        public static bool BoxIsFree(TileGrid grid, float x, float y, float halfWidth, float height)
        {
            MovingBoxes boxes = grid.Boxes;
            for (int k = 0; k < boxes.ActiveCount; k++)
            {
                boxes.GetActive(k, out Box b, out TileKind kind);
                if (kind == TileKind.Solid && SpansOverlap(x - halfWidth, x + halfWidth, b.MinX, b.MaxX) && SpansOverlap(y, y + height, b.MinY, b.MaxY))
                {
                    return false;
                }
            }
            int rowFirst = FirstCovered(y);
            int rowLast = LastCovered(y + height);
            int colLast = LastCovered(x + halfWidth);
            for (int c = FirstCovered(x - halfWidth); c <= colLast; c++)
            {
                if (ColumnBlocks(grid, c, rowFirst, rowLast, true))
                {
                    return false;
                }
            }
            return true;
        }

        /// <summary>
        /// True when the player's hazard core (the hitbox with feet at (<paramref name="x"/>, <paramref name="y"/>)
        /// shrunk by <see cref="LevelRules.HazardInset"/> on every side) overlaps a Hazard cell, half-open, or a Hazard moving box.
        /// </summary>
        public static bool CoreTouchesHazard(TileGrid grid, float x, float y)
        {
            const float Inset = LevelRules.HazardInset;
            const float CoreHalfWidth = MovementInternals.HitboxHalfWidth - Inset;
            MovingBoxes boxes = grid.Boxes;
            for (int k = 0; k < boxes.ActiveCount; k++)
            {
                boxes.GetActive(k, out Box b, out TileKind kind);
                if (kind == TileKind.Hazard && SpansOverlap(x - CoreHalfWidth, x + CoreHalfWidth, b.MinX, b.MaxX)
                    && SpansOverlap(y + Inset, y + MovementInternals.HitboxHeight - Inset, b.MinY, b.MaxY))
                {
                    return true;
                }
            }
            int rowFirst = FirstCovered(y + Inset);
            int rowLast = LastCovered(y + MovementInternals.HitboxHeight - Inset);
            int colLast = LastCovered(x + CoreHalfWidth);
            for (int c = FirstCovered(x - CoreHalfWidth); c <= colLast; c++)
            {
                for (int r = rowFirst; r <= rowLast; r++)
                {
                    if (grid.Get(c, r) == TileKind.Hazard)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// True when a box whose bottom is on an integer row (within the contact epsilon) has a painted Solid,
        /// Climbable or OneWay cell (traps never count) directly under every column it covers: where safe ground may be recorded.
        /// </summary>
        public static bool StandsFullyOnPainted(TileGrid grid, float x, float y, float halfWidth)
        {
            float row = MathF.Round(y);
            if (MathF.Abs(y - row) > Epsilon)
            {
                return false;
            }
            int below = (int)row - 1;
            int colLast = LastCovered(x + halfWidth);
            for (int c = FirstCovered(x - halfWidth); c <= colLast; c++)
            {
                TileKind kind = grid.GetPainted(c, below);
                if (!Blocks(kind) && kind != TileKind.OneWay)
                {
                    return false;
                }
            }
            return true;
        }

        static bool TryCornerCorrection(TileGrid grid, ref float x, float y, float halfWidth, float height, float dy,
            int row, int colFirst, int colLast)
        {
            int minBlocking = int.MaxValue;
            int maxBlocking = int.MinValue;
            for (int c = colFirst; c <= colLast; c++)
            {
                if (Blocks(grid.Get(c, row)))
                {
                    minBlocking = Math.Min(minBlocking, c);
                    maxBlocking = Math.Max(maxBlocking, c);
                }
            }

            // How far the blocking cells reach into the box from each side; at most one can be within the cap.
            float overlapFromLeft = maxBlocking + 1 - (x - halfWidth);
            float overlapFromRight = x + halfWidth - minBlocking;
            float shift;
            if (overlapFromLeft <= MovementInternals.CeilingCornerCorrection)
            {
                shift = overlapFromLeft;
            }
            else if (overlapFromRight <= MovementInternals.CeilingCornerCorrection)
            {
                shift = -overlapFromRight;
            }
            else
            {
                return false;
            }

            float shiftedX = x + shift;
            if (!BoxIsFree(grid, shiftedX, y, halfWidth, height + dy))
            {
                return false;
            }
            x = shiftedX;
            return true;
        }

        static SupportKind RowSupport(TileGrid grid, int row, int colFirst, int colLast, bool ignoreOneWay)
        {
            bool oneWay = false;
            for (int c = colFirst; c <= colLast; c++)
            {
                TileKind kind = grid.Get(c, row);
                if (Blocks(kind))
                {
                    return SupportKind.Solid;
                }
                oneWay |= kind == TileKind.OneWay && !ignoreOneWay;
            }
            return oneWay ? SupportKind.OneWayOnly : SupportKind.None;
        }

        static bool RowBlocks(TileGrid grid, int row, int colFirst, int colLast)
        {
            for (int c = colFirst; c <= colLast; c++)
            {
                if (Blocks(grid.Get(c, row)))
                {
                    return true;
                }
            }
            return false;
        }

        static bool ColumnBlocks(TileGrid grid, int column, int rowFirst, int rowLast, bool climbableIsWall)
        {
            for (int r = rowFirst; r <= rowLast; r++)
            {
                TileKind kind = grid.Get(column, r);
                if (kind == TileKind.Solid || (climbableIsWall && kind == TileKind.Climbable))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The player's hazard core with feet at (<paramref name="x"/>, <paramref name="y"/>), as <see cref="CoreTouchesHazard"/> tests it.</summary>
        public static Box HazardCore(float x, float y)
        {
            const float Inset = LevelRules.HazardInset;
            const float CoreHalfWidth = MovementInternals.HitboxHalfWidth - Inset;
            return new Box(x - CoreHalfWidth, y + Inset, x + CoreHalfWidth, y + MovementInternals.HitboxHeight - Inset);
        }

        /// <summary>The spans share more than the contact epsilon: the box rule matching how cells are covered.</summary>
        internal static bool SpansOverlap(float aMin, float aMax, float bMin, float bMax)
        {
            return aMin < bMax - Epsilon && bMin < aMax - Epsilon;
        }

        internal static int FirstCovered(float min)
        {
            return (int)MathF.Floor(min + Epsilon);
        }

        internal static int LastCovered(float max)
        {
            return (int)MathF.Ceiling(max - Epsilon) - 1;
        }
    }
}
