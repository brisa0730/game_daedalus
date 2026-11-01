using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// The player's movement rules, one 60 Hz tick at a time. Pure: the result depends only on the
    /// state, the input, the map and the settings. <see cref="Step"/> allocates nothing.
    /// </summary>
    public sealed class PlayerStepper
    {
        const float HalfWidth = MovementInternals.HitboxHalfWidth;
        const float Height = MovementInternals.HitboxHeight;
        const float Grip = MovementInternals.GripHeight;
        const float Dt = MovementInternals.TickSeconds;
        const float Epsilon = MovementInternals.ContactEpsilon;

        readonly TileGrid grid;
        readonly RopeSegment[] ropes;
        DerivedMovement derived;

        public PlayerStepper(TileGrid grid, RopeSegment[] ropes, MovementSettings settings)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (ropes == null)
            {
                throw new ArgumentNullException(nameof(ropes));
            }
            // Play start, once: rope tops snap onto a nearby one-way surface.
            this.ropes = new RopeSegment[ropes.Length];
            for (int i = 0; i < ropes.Length; i++)
            {
                this.ropes[i] = ropes[i].SnapTopTo(grid);
            }
            ApplySettings(settings);
        }

        /// <summary>The clamped settings in use.</summary>
        public MovementSettings Settings { get; private set; }

        /// <summary>Replaces the settings; takes effect on the next tick.</summary>
        public void ApplySettings(MovementSettings settings)
        {
            Settings = settings.Clamped();
            derived = new DerivedMovement(Settings);
        }

        /// <summary>
        /// The state with a new ability set, between ticks (abilities switched on or off during play). A hold the new
        /// set can't keep lets go into the air: a rope without rope climb, a cling without wall climb, a wall slide
        /// without wall jump. Everything else carries on.
        /// </summary>
        public PlayerState ChangeAbilities(in PlayerState state, Abilities abilities)
        {
            PlayerState s = state;
            s.Abilities = abilities;
            bool letGo = (s.Contact == PlayerContact.Rope && !Has(s, Abilities.RopeClimb))
                || (s.Contact == PlayerContact.WallCling && !Has(s, Abilities.WallClimb))
                || (s.Contact == PlayerContact.WallSlide && !Has(s, Abilities.WallJump));
            if (letGo)
            {
                s.Contact = PlayerContact.Air;
                s.RopeIndex = -1;
                s.WallSide = 0;
            }
            return s;
        }

        /// <summary>A player at rest at the given feet (apply <see cref="StartPlacement.Find"/> first). Falling out returns here.</summary>
        public PlayerState CreateState(float feetX, float feetY, Abilities abilities)
        {
            return new PlayerState
            {
                X = feetX,
                Y = feetY,
                Contact = PlayerContact.Air,
                Facing = 1,
                AirJumpsLeft = derived.AirJumps,
                RopeIndex = -1,
                Abilities = abilities,
                StartX = feetX,
                StartY = feetY,
            };
        }

        /// <summary>
        /// One tick, in a fixed order: (2) tick-start contacts, (3) the jump press, (4) input transitions,
        /// (5) velocity by contact, (6) move X then Y, (7) post-move classification, (8) facing,
        /// (9) counters. A counter set on tick T is active on ticks T+1 … T+N.
        /// </summary>
        public PlayerState Step(in PlayerState state, in PlayerInput input)
        {
            PlayerState s = state;
            s.Teleported = false;
            var set = new SetThisTick();

            // 2. Tick-start contacts. Only standing is re-probed; wall, cling and rope contacts carry over.
            SupportKind support = TileCollision.Support(grid, s.X, s.Y, HalfWidth);
            if (s.IsStanding)
            {
                s.Contact = support == SupportKind.None ? PlayerContact.Air : StandingContact(support);
            }
            else if (s.Contact == PlayerContact.Air && s.VelocityY <= 0 && support != SupportKind.None)
            {
                Land(ref s, support);
            }

            // 3. The jump press, then the variable-height cut (on the jump tick too, so a tap is a minimum hop).
            bool jumped;
            if (input.JumpPressed)
            {
                jumped = ResolveJumpPress(ref s, input, ref set);
            }
            else
            {
                jumped = s.BufferTicks > 0 && TryBufferedJump(ref s, input, ref set);
            }
            if (s.Contact == PlayerContact.Air && !input.JumpHeld && s.VelocityY > derived.MinHopVelocity)
            {
                s.VelocityY = derived.MinHopVelocity;
            }

            // 4. Input transitions.
            if (!jumped)
            {
                ApplyInputTransitions(ref s, input, ref set);
            }

            // 5. Velocity by contact. Impulses above already set the velocity; v0 is that post-impulse value.
            float vx0 = s.VelocityX;
            float vy0 = s.VelocityY;
            float vx1, vy1;
            bool onRope = s.Contact == PlayerContact.Rope;
            switch (s.Contact)
            {
                case PlayerContact.Rope:
                case PlayerContact.WallCling:
                    vx0 = vx1 = 0;
                    vy0 = vy1 = input.MoveY * derived.ClimbSpeed;
                    break;
                case PlayerContact.WallSlide:
                    vx0 = vx1 = 0;
                    vy1 = MathF.Max(Gravity(vy0), -derived.WallSlideSpeed);
                    break;
                default:
                    bool standing = s.IsStanding;
                    vx1 = s.WallLockTicks > 0 ? vx0 : Horizontal(vx0, input.MoveX, standing);
                    if (standing)
                    {
                        vy0 = vy1 = 0;
                    }
                    else
                    {
                        vy1 = MathF.Max(Gravity(vy0), -derived.MaxFallSpeed);
                    }
                    break;
            }

            // 6. Move: trapezoid on both axes, X swept first at the old height, then Y.
            float dx = (vx0 + vx1) * 0.5f * Dt;
            float dy = (vy0 + vy1) * 0.5f * Dt;
            bool popped = false;
            bool ropeSurfaceReached = false;
            if (s.Contact == PlayerContact.WallCling && dy > 0)
            {
                dy = LimitClimbUp(ref s, dy, out popped);
            }
            else if (onRope && dy > 0)
            {
                dy = LimitRopeUp(s, dy, out ropeSurfaceReached);
            }
            else if (onRope && dy < 0)
            {
                dy = LimitRopeDown(s, dy, out ropeSurfaceReached);
            }

            SupportKind landedOn = SupportKind.None;
            if (!popped)
            {
                s.X = TileCollision.SweepX(grid, s.X, s.Y, HalfWidth, Height, dx, out int hitSide);
                if (hitSide != 0)
                {
                    vx1 = 0;
                }
                TileCollision.SweepY(grid, ref s.X, ref s.Y, HalfWidth, Height, dy, onRope, out landedOn, out bool ceiling);
                if (ceiling && vy1 > 0)
                {
                    vy1 = 0;
                }
                s.VelocityX = vx1;
                s.VelocityY = vy1;
            }

            // 7. Post-move classification.
            if (!popped)
            {
                ClassifyAfterMove(ref s, input, landedOn, ropeSurfaceReached, jumped, ref set);
            }

            // Falling out of the bottom resets to the start (temporary until pits damage in combat-and-enemies).
            if (s.Y + Height < 0)
            {
                PlayerState reset = CreateState(s.StartX, s.StartY, s.Abilities);
                reset.Teleported = true;
                return reset;
            }

            // 8. Facing.
            if (s.WallLockTicks == 0 && input.MoveX != 0)
            {
                s.Facing = input.MoveX;
            }

            // 9. Counters not set this tick count down.
            if (!set.Coyote && s.CoyoteTicks > 0)
            {
                s.CoyoteTicks--;
            }
            if (!set.Buffer && s.BufferTicks > 0)
            {
                s.BufferTicks--;
            }
            if (!set.WallCoyote && s.WallCoyoteTicks > 0)
            {
                s.WallCoyoteTicks--;
            }
            if (!set.WallLock && s.WallLockTicks > 0)
            {
                s.WallLockTicks--;
            }
            if (!set.RopeLockout && s.RopeLockoutTicks > 0)
            {
                s.RopeLockoutTicks--;
            }
            return s;
        }

        // Jump-press priority, first match wins: drop-through > ground/coyote > rope > wall > air > buffer.
        bool ResolveJumpPress(ref PlayerState s, in PlayerInput input, ref SetThisTick set)
        {
            if (input.MoveY < 0 && s.Contact == PlayerContact.OneWayTop)
            {
                DropThrough(ref s);
                return true;
            }
            if (Has(s, Abilities.Jump) && (s.IsStanding || s.CoyoteTicks > 0))
            {
                GroundJump(ref s);
                return true;
            }
            if (s.Contact == PlayerContact.Rope)
            {
                if (input.MoveY < 0)
                {
                    LetGoOfRope(ref s, ref set);
                }
                else
                {
                    RopeJump(ref s, input, ref set);
                }
                return true;
            }
            if (TryWallJump(ref s, input, ref set))
            {
                return true;
            }
            if (s.Contact == PlayerContact.Air && Has(s, Abilities.DoubleJump) && s.AirJumpsLeft > 0)
            {
                s.VelocityY = derived.JumpVelocity;
                s.AirJumpsLeft--;
                s.CoyoteTicks = 0;
                s.BufferTicks = 0;
                return true;
            }
            s.BufferTicks = MovementInternals.JumpBufferTicks;
            set.Buffer = true;
            return false;
        }

        // A buffered press fires only a ground/coyote jump or a wall jump, never a drop, rope jump or air jump.
        bool TryBufferedJump(ref PlayerState s, in PlayerInput input, ref SetThisTick set)
        {
            if (Has(s, Abilities.Jump) && (s.IsStanding || s.CoyoteTicks > 0))
            {
                GroundJump(ref s);
                return true;
            }
            return TryWallJump(ref s, input, ref set);
        }

        void DropThrough(ref PlayerState s)
        {
            s.Y -= MovementInternals.DropThroughNudge;
            s.VelocityY = 0;
            s.Contact = PlayerContact.Air;
            s.CoyoteTicks = 0;
            s.BufferTicks = 0;
        }

        void GroundJump(ref PlayerState s)
        {
            s.VelocityY = derived.JumpVelocity;
            s.Contact = PlayerContact.Air;
            s.WallSide = 0;
            s.CoyoteTicks = 0;
            s.BufferTicks = 0;
        }

        void RopeJump(ref PlayerState s, in PlayerInput input, ref SetThisTick set)
        {
            s.VelocityY = derived.JumpVelocity;
            s.VelocityX = input.MoveX * derived.RunSpeed;
            s.Contact = PlayerContact.Air;
            s.RopeIndex = -1;
            s.RopeLockoutTicks = MovementInternals.RopeLockoutTicks;
            set.RopeLockout = true;
            s.BufferTicks = 0;
        }

        // Down + jump on a rope lets go without a jump, like a drop-through; the lockout keeps a held Down
        // from catching the rope again at once.
        void LetGoOfRope(ref PlayerState s, ref SetThisTick set)
        {
            s.VelocityX = 0;
            s.VelocityY = 0;
            s.Contact = PlayerContact.Air;
            s.RopeIndex = -1;
            s.RopeLockoutTicks = MovementInternals.RopeLockoutTicks;
            set.RopeLockout = true;
            s.BufferTicks = 0;
        }

        bool TryWallJump(ref PlayerState s, in PlayerInput input, ref SetThisTick set)
        {
            bool wallJump = Has(s, Abilities.WallJump);
            int side = 0;
            int face = 0;
            switch (s.Contact)
            {
                case PlayerContact.WallCling:
                    // Jumping off a cling always pushes away, with or without the wall-jump ability.
                    side = s.WallSide;
                    face = FaceAt(s.X, side, 0);
                    break;
                case PlayerContact.WallSlide:
                    if (!wallJump)
                    {
                        return false;
                    }
                    side = s.WallSide;
                    face = FaceAt(s.X, side, 0);
                    break;
                case PlayerContact.Air:
                    if (wallJump)
                    {
                        bool climbableIsWall = !Has(s, Abilities.WallClimb);
                        float left = TileCollision.WallGap(grid, s.X, s.Y, HalfWidth, Height, -1, MovementInternals.WallJumpReach, climbableIsWall);
                        float right = TileCollision.WallGap(grid, s.X, s.Y, HalfWidth, Height, 1, MovementInternals.WallJumpReach, climbableIsWall);
                        bool hasLeft = !float.IsPositiveInfinity(left);
                        bool hasRight = !float.IsPositiveInfinity(right);
                        if (hasLeft && hasRight)
                        {
                            // Both sides: the pressed side, then the nearer wall, then the left one.
                            side = input.MoveX != 0 ? input.MoveX : (right < left ? 1 : -1);
                        }
                        else if (hasLeft || hasRight)
                        {
                            side = hasRight ? 1 : -1;
                        }
                        if (side != 0)
                        {
                            face = FaceAt(s.X, side, side > 0 ? right : left);
                        }
                    }
                    if (side == 0 && s.WallCoyoteTicks > 0 && (wallJump || s.WallCoyoteFromCling))
                    {
                        side = s.WallCoyoteSide;
                        face = s.WallCoyoteFace;
                    }
                    if (side == 0)
                    {
                        return false;
                    }
                    break;
                default:
                    return false;
            }

            s.VelocityX = -side * derived.WallPushSpeed;
            s.VelocityY = derived.JumpVelocity;
            s.Contact = PlayerContact.Air;
            s.Facing = -side;
            s.WallSide = 0;
            s.WallLockTicks = MovementInternals.WallJumpLockTicks;
            set.WallLock = true;
            s.LastWallJumpSide = side;
            s.LastWallJumpFace = face;
            s.WallCoyoteTicks = 0;
            s.CoyoteTicks = 0;
            s.BufferTicks = 0;
            return true;
        }

        void ApplyInputTransitions(ref PlayerState s, in PlayerInput input, ref SetThisTick set)
        {
            bool canGrabFrom = s.Contact == PlayerContact.Air || s.IsStanding;
            if (canGrabFrom && input.MoveY != 0 && Has(s, Abilities.RopeClimb) && s.RopeLockoutTicks == 0)
            {
                TryGrabRope(ref s, input);
            }
            if (s.Contact == PlayerContact.WallSlide && input.MoveX != s.WallSide)
            {
                LeaveWall(ref s, false, ref set);
            }
            else if (s.Contact == PlayerContact.WallCling && input.MoveX == -s.WallSide)
            {
                LeaveWall(ref s, true, ref set);
            }
        }

        // Grab only with Up or Down held. Standing, Up grabs a rope in reach and Down grabs only from the
        // one-way top the rope hangs through. The first rope in list order that is in reach is the one.
        void TryGrabRope(ref PlayerState s, in PlayerInput input)
        {
            float gripY = s.Y + Grip;
            for (int i = 0; i < ropes.Length; i++)
            {
                RopeSegment rope = ropes[i];
                if (MathF.Abs(s.X - rope.X) > MovementInternals.RopeGrabReach + Epsilon)
                {
                    continue;
                }
                bool gripOnRope = rope.Bottom <= gripY && gripY <= rope.Top;
                bool inReach;
                if (s.IsStanding && input.MoveY < 0)
                {
                    inReach = s.Contact == PlayerContact.OneWayTop && MathF.Abs(s.Y - rope.Top) <= MovementInternals.RopeTopTolerance;
                }
                else
                {
                    inReach = gripOnRope;
                }
                if (!inReach)
                {
                    continue;
                }
                if (!TileCollision.BoxIsFree(grid, rope.X, s.Y, HalfWidth, Height))
                {
                    return;
                }
                s.Contact = PlayerContact.Rope;
                s.RopeIndex = i;
                s.X = rope.X;
                s.VelocityX = 0;
                s.VelocityY = 0;
                ResetAirState(ref s);
                return;
            }
        }

        void LeaveWall(ref PlayerState s, bool fromCling, ref SetThisTick set)
        {
            s.WallCoyoteTicks = MovementInternals.WallCoyoteTicks;
            s.WallCoyoteSide = s.WallSide;
            s.WallCoyoteFace = FaceAt(s.X, s.WallSide, 0);
            s.WallCoyoteFromCling = fromCling;
            set.WallCoyote = true;
            s.Contact = PlayerContact.Air;
            s.WallSide = 0;
        }

        void ClassifyAfterMove(ref PlayerState s, in PlayerInput input, SupportKind landedOn, bool ropeSurfaceReached, bool jumped, ref SetThisTick set)
        {
            SupportKind support = landedOn != SupportKind.None ? landedOn : TileCollision.Support(grid, s.X, s.Y, HalfWidth);
            switch (s.Contact)
            {
                case PlayerContact.Ground:
                case PlayerContact.OneWayTop:
                    if (support == SupportKind.None)
                    {
                        // Walked off a ledge.
                        s.Contact = PlayerContact.Air;
                        s.CoyoteTicks = MovementInternals.CoyoteTicks;
                        set.Coyote = true;
                    }
                    else
                    {
                        s.Contact = StandingContact(support);
                    }
                    break;
                case PlayerContact.Air:
                case PlayerContact.WallSlide:
                    if (s.VelocityY <= 0 && support != SupportKind.None)
                    {
                        Land(ref s, support);
                    }
                    else if (s.Contact == PlayerContact.WallSlide && !TouchingWall(s, s.WallSide, !Has(s, Abilities.WallClimb)))
                    {
                        LeaveWall(ref s, false, ref set);
                    }
                    break;
                case PlayerContact.WallCling:
                    if (landedOn != SupportKind.None)
                    {
                        Land(ref s, landedOn);
                    }
                    else if (!GripIsClimbable(s, s.WallSide))
                    {
                        // Climbed off the bottom of the climbable run.
                        s.Contact = PlayerContact.Air;
                        s.WallSide = 0;
                    }
                    break;
                case PlayerContact.Rope:
                    if (ropeSurfaceReached)
                    {
                        Land(ref s, SupportKind.OneWayOnly);
                    }
                    else if (landedOn != SupportKind.None)
                    {
                        Land(ref s, landedOn);
                    }
                    else if (s.Y + Grip < ropes[s.RopeIndex].Bottom - Epsilon)
                    {
                        s.Contact = PlayerContact.Air;
                        s.RopeIndex = -1;
                    }
                    break;
            }

            if (jumped || s.WallLockTicks > 0 || input.MoveX == 0)
            {
                return;
            }
            int side = input.MoveX;
            bool airborne = s.Contact == PlayerContact.Air;
            if ((airborne || s.IsStanding) && Has(s, Abilities.WallClimb)
                && (s.IsStanding ? input.MoveY > 0 : s.VelocityY <= 0 || input.MoveY > 0)
                && TouchingWall(s, side, true) && GripIsClimbable(s, side))
            {
                s.Contact = PlayerContact.WallCling;
                s.WallSide = side;
                s.VelocityX = 0;
                s.VelocityY = 0;
                ResetAirState(ref s);
            }
            else if (airborne && Has(s, Abilities.WallJump) && s.VelocityY <= 0 && TouchingWall(s, side, !Has(s, Abilities.WallClimb)))
            {
                s.Contact = PlayerContact.WallSlide;
                s.WallSide = side;
                s.VelocityX = 0;
                s.WallCoyoteTicks = 0;
                // Refill only on a different wall than the one last jumped from, so one wall can't be scaled.
                if (s.LastWallJumpSide != side || s.LastWallJumpFace != FaceAt(s.X, side, 0))
                {
                    s.AirJumpsLeft = derived.AirJumps;
                }
            }
        }

        // Climbing up a climbable wall stops with the grip at the run's top T; over an open top, the ledge pop
        // stands the player on the wall top instead.
        float LimitClimbUp(ref PlayerState s, float dy, out bool popped)
        {
            popped = false;
            int side = s.WallSide;
            float face = s.X + side * HalfWidth;
            int column = side > 0 ? (int)MathF.Round(face) : (int)MathF.Round(face) - 1;
            float gripY = s.Y + Grip;
            int topRow = (int)MathF.Floor(gripY - Epsilon);
            while (grid.Get(column, topRow + 1) == TileKind.Climbable)
            {
                topRow++;
            }
            float top = topRow + 1;
            if (gripY + dy <= top)
            {
                return dy;
            }

            float popX = face + side * (HalfWidth + MovementInternals.LedgePopInset);
            SupportKind popSupport = TileCollision.Support(grid, popX, top, HalfWidth);
            if (popSupport != SupportKind.None && TileCollision.BoxIsFree(grid, popX, top, HalfWidth, Height))
            {
                s.X = popX;
                s.Y = top;
                s.VelocityX = 0;
                s.VelocityY = 0;
                Land(ref s, popSupport);
                popped = true;
                return 0;
            }
            return MathF.Max(0, top - gripY);
        }

        // Climbing up a rope stops with the grip at its top, unless a one-way platform sits there: then the
        // player climbs until the feet reach it and stands on it.
        float LimitRopeUp(in PlayerState s, float dy, out bool topReached)
        {
            topReached = false;
            RopeSegment rope = ropes[s.RopeIndex];
            float surface = MathF.Round(rope.Top);
            bool hasSurface = MathF.Abs(surface - rope.Top) <= MovementInternals.RopeTopTolerance
                && TileCollision.Support(grid, rope.X, surface, HalfWidth) == SupportKind.OneWayOnly;
            if (hasSurface)
            {
                if (s.Y + dy >= surface)
                {
                    topReached = true;
                    return surface - s.Y;
                }
                return dy;
            }
            float gripY = s.Y + Grip;
            return gripY + dy <= rope.Top ? dy : MathF.Max(0, rope.Top - gripY);
        }

        // Climbing down a rope that ends on a one-way platform stands the player on it (rope sweeps otherwise
        // ignore one-ways, which the rope passes through).
        float LimitRopeDown(in PlayerState s, float dy, out bool bottomReached)
        {
            bottomReached = false;
            RopeSegment rope = ropes[s.RopeIndex];
            float surface = MathF.Round(rope.Bottom);
            bool hasSurface = MathF.Abs(surface - rope.Bottom) <= MovementInternals.RopeTopTolerance
                && TileCollision.Support(grid, rope.X, surface, HalfWidth) == SupportKind.OneWayOnly;
            if (hasSurface && s.Y >= surface - Epsilon && s.Y + dy <= surface)
            {
                bottomReached = true;
                return surface - s.Y;
            }
            return dy;
        }

        bool TouchingWall(in PlayerState s, int side, bool climbableIsWall)
        {
            return TileCollision.WallGap(grid, s.X, s.Y, HalfWidth, Height, side, Epsilon, climbableIsWall) <= Epsilon;
        }

        // The cell beside the hitbox at grip height (top edge inclusive) is climbable, and the player can climb.
        bool GripIsClimbable(in PlayerState s, int side)
        {
            return Has(s, Abilities.WallClimb)
                && TileCollision.IsClimbableAt(grid, s.X + side * (HalfWidth + Epsilon), s.Y + Grip - Epsilon);
        }

        void Land(ref PlayerState s, SupportKind support)
        {
            s.Contact = StandingContact(support);
            s.VelocityY = 0;
            s.WallSide = 0;
            s.RopeIndex = -1;
            s.WallLockTicks = 0;
            ResetAirState(ref s);
        }

        // Landing, a cling and a rope grab refill the air jumps and end every airborne window.
        void ResetAirState(ref PlayerState s)
        {
            s.AirJumpsLeft = derived.AirJumps;
            s.LastWallJumpSide = 0;
            s.CoyoteTicks = 0;
            s.WallCoyoteTicks = 0;
        }

        float Horizontal(float vx, int moveX, bool standing)
        {
            float target = moveX * derived.RunSpeed;
            float rate;
            if (moveX == 0)
            {
                rate = standing ? derived.GroundDecel : derived.AirDecel;
            }
            else if (vx != 0 && MathF.Sign(vx) != moveX)
            {
                rate = standing ? derived.GroundTurnaround : derived.AirTurnaround;
            }
            else if (MathF.Abs(vx) <= derived.RunSpeed)
            {
                rate = standing ? derived.GroundAccel : derived.AirAccel;
            }
            else
            {
                rate = standing ? derived.GroundDecel : derived.AirDecel;
            }
            float step = rate * Dt;
            return vx < target ? MathF.Min(vx + step, target) : MathF.Max(vx - step, target);
        }

        float Gravity(float vy)
        {
            return vy - (vy > 0 ? derived.RiseGravity : derived.FallGravity) * Dt;
        }

        static int FaceAt(float x, int side, float gap)
        {
            return (int)MathF.Round(x + side * (HalfWidth + gap));
        }

        static PlayerContact StandingContact(SupportKind support)
        {
            return support == SupportKind.OneWayOnly ? PlayerContact.OneWayTop : PlayerContact.Ground;
        }

        static bool Has(in PlayerState s, Abilities ability)
        {
            return (s.Abilities & ability) != 0;
        }

        /// <summary>Which counters were set during this tick, so they are not decremented at its end.</summary>
        struct SetThisTick
        {
            public bool Coyote;
            public bool Buffer;
            public bool WallCoyote;
            public bool WallLock;
            public bool RopeLockout;
        }
    }
}
