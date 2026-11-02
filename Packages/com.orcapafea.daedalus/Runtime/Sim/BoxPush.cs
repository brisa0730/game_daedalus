using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// How moving solids move the player (systems.md § Traps, Move and Fall): only a trap's leading face pushes, a
    /// blocked push first tries a small nudge at right angles (<see cref="LevelRules.CrushNudge"/>), and only then
    /// crushes. Every push, the Appear push-out included, ends in <see cref="ApplyPush"/>. Pure and allocation-free.
    /// </summary>
    internal static class BoxPush
    {
        const float HalfWidth = MovementInternals.HitboxHalfWidth;
        const float Height = MovementInternals.HitboxHeight;

        /// <summary>
        /// The one push effect, after the position was set: the speed pointing into the pusher (−<paramref name="dirX"/>,
        /// −<paramref name="dirY"/>) is zeroed and the player is in the air (the stepper's re-probe lands it again on a
        /// top), off a wall and off a rope. A rope push starts the re-grab lockout: a push before the player's step
        /// (<paramref name="playerStepsThisTick"/>) is counted down by that step, hence the extra tick; either way the
        /// rope is blocked through push tick + <see cref="MovementInternals.RopeLockoutTicks"/> and free a tick later,
        /// as after a rope jump.
        /// </summary>
        public static void ApplyPush(ref PlayerState s, int dirX, int dirY, bool playerStepsThisTick)
        {
            if ((dirY > 0 && s.VelocityY < 0) || (dirY < 0 && s.VelocityY > 0))
            {
                s.VelocityY = 0;
            }
            if ((dirX > 0 && s.VelocityX < 0) || (dirX < 0 && s.VelocityX > 0))
            {
                s.VelocityX = 0;
            }
            if (s.RopeIndex >= 0)
            {
                s.RopeIndex = -1;
                s.RopeLockoutTicks = MovementInternals.RopeLockoutTicks + (playerStepsThisTick ? 1 : 0);
            }
            s.Contact = PlayerContact.Air;
            s.WallSide = 0;
        }

        /// <summary>
        /// Pushes the player out of the <paramref name="count"/> moving solids whose new boxes overlap its hitbox
        /// (<paramref name="oldBoxes"/>[k] → <paramref name="newBoxes"/>[k]). Each pushes along its leading face: vertically
        /// when the old box already overlapped the player in X, else horizontally when it did in Y, else (a corner) on the
        /// axis of smaller overlap, ties vertical. Pushes combine by value (the furthest target per direction); opposite
        /// pushes on one axis squeeze. A blocked spot tries <see cref="TryNudge"/>. Returns false when crushed.
        /// Before the player's step (it is counted in <see cref="ApplyPush"/>).
        /// </summary>
        public static bool Resolve(TileGrid grid, ref PlayerState s, Box[] oldBoxes, Box[] newBoxes, int count)
        {
            Box hitbox = Box.Hitbox(s.X, s.Y);
            bool left = false, right = false, up = false, down = false;
            float leftX = float.PositiveInfinity, rightX = float.NegativeInfinity;
            float upY = float.NegativeInfinity, downY = float.PositiveInfinity;
            for (int k = 0; k < count; k++)
            {
                Box o = oldBoxes[k];
                Box n = newBoxes[k];
                float dx = n.MinX - o.MinX;
                float dy = n.MinY - o.MinY;
                bool vertical;
                if (TileCollision.SpansOverlap(o.MinX, o.MaxX, hitbox.MinX, hitbox.MaxX))
                {
                    vertical = true;
                }
                else if (TileCollision.SpansOverlap(o.MinY, o.MaxY, hitbox.MinY, hitbox.MaxY))
                {
                    vertical = false;
                }
                else
                {
                    float overlapX = MathF.Min(n.MaxX - hitbox.MinX, hitbox.MaxX - n.MinX);
                    float overlapY = MathF.Min(n.MaxY - hitbox.MinY, hitbox.MaxY - n.MinY);
                    vertical = overlapY <= overlapX;
                }
                if (vertical ? dy == 0 : dx == 0)
                {
                    vertical = !vertical;
                }
                if (vertical && dy > 0)
                {
                    up = true;
                    upY = MathF.Max(upY, n.MaxY);
                }
                else if (vertical && dy < 0)
                {
                    down = true;
                    downY = MathF.Min(downY, n.MinY - Height);
                }
                else if (!vertical && dx > 0)
                {
                    right = true;
                    rightX = MathF.Max(rightX, n.MaxX + HalfWidth);
                }
                else if (!vertical && dx < 0)
                {
                    left = true;
                    leftX = MathF.Min(leftX, n.MinX - HalfWidth);
                }
            }
            if ((left && right) || (up && down))
            {
                return false;
            }
            if (!left && !right && !up && !down)
            {
                return true;
            }

            float x = right ? rightX : left ? leftX : s.X;
            float y = up ? upY : down ? downY : s.Y;
            if (IsFree(grid, x, y))
            {
                s.X = x;
                s.Y = y;
                ApplyPush(ref s, right ? 1 : left ? -1 : 0, up ? 1 : down ? -1 : 0, true);
                return true;
            }
            // A push on both axes has no right angle to nudge along.
            if ((left || right) && (up || down))
            {
                return false;
            }
            return TryNudge(grid, ref s, up || down, newBoxes, count);
        }

        /// <summary>
        /// The crush forgiveness: from the pre-push position, shift at right angles to the push (left/right for a
        /// vertical push, up/down for a horizontal one) by the least amount that clears every pusher, at most
        /// <see cref="LevelRules.CrushNudge"/>, to a free, hazard-free spot. The smaller shift wins; ties go up, left,
        /// right, down. Returns false when there is no such spot (crushed).
        /// </summary>
        public static bool TryNudge(TileGrid grid, ref PlayerState s, bool verticalPush, Box[] pushers, int count)
        {
            float lowMin = float.PositiveInfinity, highMax = float.NegativeInfinity;
            for (int k = 0; k < count; k++)
            {
                Box p = pushers[k];
                lowMin = MathF.Min(lowMin, verticalPush ? p.MinX : p.MinY);
                highMax = MathF.Max(highMax, verticalPush ? p.MaxX : p.MaxY);
            }
            // The first candidate is the tie winner: left for a vertical push, up for a horizontal one.
            float firstTo, secondTo;
            float firstShift, secondShift;
            if (verticalPush)
            {
                firstTo = lowMin - HalfWidth;
                firstShift = s.X - firstTo;
                secondTo = highMax + HalfWidth;
                secondShift = secondTo - s.X;
            }
            else
            {
                firstTo = highMax;
                firstShift = firstTo - s.Y;
                secondTo = lowMin - Height;
                secondShift = s.Y - secondTo;
            }
            bool firstOk = firstShift <= LevelRules.CrushNudge + MovementInternals.ContactEpsilon
                && (verticalPush ? IsFree(grid, firstTo, s.Y) : IsFree(grid, s.X, firstTo));
            bool secondOk = secondShift <= LevelRules.CrushNudge + MovementInternals.ContactEpsilon
                && (verticalPush ? IsFree(grid, secondTo, s.Y) : IsFree(grid, s.X, secondTo));
            if (!firstOk && !secondOk)
            {
                return false;
            }
            bool first = firstOk && (!secondOk || firstShift <= secondShift);
            float to = first ? firstTo : secondTo;
            if (verticalPush)
            {
                s.X = to;
                ApplyPush(ref s, first ? -1 : 1, 0, true);
            }
            else
            {
                s.Y = to;
                ApplyPush(ref s, 0, first ? 1 : -1, true);
            }
            return true;
        }

        static bool IsFree(TileGrid grid, float x, float y)
        {
            return TileCollision.BoxIsFree(grid, x, y, HalfWidth, Height) && !TileCollision.CoreTouchesHazard(grid, x, y);
        }
    }
}
