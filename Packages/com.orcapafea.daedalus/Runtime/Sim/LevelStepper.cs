using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// The world step around <see cref="PlayerStepper"/>: traps, death and the death beat, respawn, checkpoints and
    /// exits, one 60 Hz tick at a time. Pure and deterministic like the player's stepper, but it keeps its state
    /// (the player, the phase, the trap states) inside itself instead of passing a state struct: copying the trap
    /// arrays every tick would cost more than it buys. <see cref="Step"/> allocates nothing.
    /// Call <see cref="Begin"/> before the first <see cref="Step"/>.
    /// </summary>
    public sealed class LevelStepper
    {
        const float HalfWidth = MovementInternals.HitboxHalfWidth;
        const float Height = MovementInternals.HitboxHeight;
        const float Dt = MovementInternals.TickSeconds;
        const float Epsilon = MovementInternals.ContactEpsilon;
        // Tiles/s; how much faster than the ride speed a trap going down may sink and still carry its rider (float slack).
        const float RideSlack = 0.001f;

        enum Motion
        {
            Resting = 0,
            /// <summary>Fired, not yet advanced: still stamped as cells, so collision never has a one-tick hole.</summary>
            Starting = 1,
            Moving = 2,
            /// <summary>A Fall that left the map; back at its start on the next reset.</summary>
            Gone = 3,
        }

        struct TrapState
        {
            public bool Present;
            /// <summary>The player has been outside the trigger since the last reset, so it may fire.</summary>
            public bool Armed;
            public bool Fired;
            /// <summary>Ticks until a delayed fire; −1 = none.</summary>
            public int Pending;
            public Motion Motion;
            /// <summary>The current box's min (sim); the box keeps the start box's size.</summary>
            public float MinX;
            public float MinY;
            /// <summary>Fall only: downward speed, tiles/s.</summary>
            public float FallSpeed;
            /// <summary>Move only: ticks travelled.</summary>
            public int MoveTick;
        }

        readonly TileGrid grid;
        readonly PlayerStepper stepper;
        readonly TrapSpec[] traps;
        readonly TrapState[] trapStates;
        readonly Box[] checkpoints;
        readonly Box[] exits;
        // The trap on each exit; −1 = none.
        readonly int[] exitTraps;
        // Exits whose own trap fired in this tick's trigger phase: not winnable this tick.
        readonly bool[] exitHeld;
        readonly MovingBoxes boxes;
        // Phase 4 scratch: the traps that advanced this tick, their boxes before the advance, and the pushers.
        readonly int[] movers;
        readonly Box[] oldBoxes;
        readonly Box[] pushOld;
        readonly Box[] pushNew;
        // Solid traps that appeared in the current batch of fires.
        readonly int[] appeared;
        int appearedCount;
        bool batchFired;
        bool crushed;
        DerivedMovement derived;
        // Tiles/s; the most a trap going down may sink per second and keep its rider (systems.md § Traps, Riding).
        float rideSpeed;

        public LevelStepper(TileGrid grid, RopeSegment[] ropes, TrapSpec[] traps, Box[] checkpoints, Box[] exits,
            MovementSettings settings, RespawnRule rule)
        {
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
            if (traps == null)
            {
                throw new ArgumentNullException(nameof(traps));
            }
            if (checkpoints == null)
            {
                throw new ArgumentNullException(nameof(checkpoints));
            }
            if (exits == null)
            {
                throw new ArgumentNullException(nameof(exits));
            }
            stepper = new PlayerStepper(grid, ropes, settings);
            derived = new DerivedMovement(stepper.Settings);
            this.traps = (TrapSpec[])traps.Clone();
            trapStates = new TrapState[traps.Length];
            appeared = new int[traps.Length];
            movers = new int[traps.Length];
            oldBoxes = new Box[traps.Length];
            pushOld = new Box[traps.Length];
            pushNew = new Box[traps.Length];
            this.checkpoints = (Box[])checkpoints.Clone();
            this.exits = (Box[])exits.Clone();
            exitTraps = new int[exits.Length];
            exitHeld = new bool[exits.Length];
            for (int e = 0; e < exits.Length; e++)
            {
                exitTraps[e] = -1;
            }
            for (int i = 0; i < traps.Length; i++)
            {
                int e = traps[i].ExitIndex;
                if (e < 0)
                {
                    continue;
                }
                if (e >= exits.Length || exitTraps[e] >= 0)
                {
                    throw new ArgumentException($"Trap {i} names exit {e}, which is missing or already has a trap.", nameof(traps));
                }
                exitTraps[e] = i;
            }
            boxes = new MovingBoxes(traps.Length);
            grid.AttachBoxes(boxes);
            Rule = rule;
            ActiveCheckpoint = -1;
            ResetTraps();
        }

        public PlayerState Player { get; private set; }
        public LevelPhase Phase { get; private set; }
        /// <summary>Dying: beat ticks left. Won: ticks since the exit was reached.</summary>
        public int PhaseTicks { get; private set; }
        /// <summary>Deaths since the last <see cref="Begin"/>.</summary>
        public int Deaths { get; private set; }
        /// <summary>The feet at the last death (Y no lower than the map bottom, for pits).</summary>
        public float DeathX { get; private set; }
        public float DeathY { get; private set; }
        /// <summary>The placed start of the last <see cref="Begin"/>.</summary>
        public float StartX { get; private set; }
        public float StartY { get; private set; }
        public StartPlacement LastPlacement { get; private set; }
        public RespawnRule Rule { get; private set; }
        public float SafeX { get; private set; }
        public float SafeY { get; private set; }
        /// <summary>Index of the last checkpoint touched; −1 = none.</summary>
        public int ActiveCheckpoint { get; private set; }
        public bool CanPlayAgain => Phase == LevelPhase.Won && PhaseTicks >= LevelRules.WinInputLockTicks;
        /// <summary>Changes whenever any trap's presence changes, so the renderer redraws only then.</summary>
        public int TrapChanges { get; private set; }
        public int TrapCount => traps.Length;
        public MovementSettings Settings => stepper.Settings;

        public bool IsTrapPresent(int index) => trapStates[index].Present;

        /// <summary>Tiles the trap's box is from its start, X (a Move or Fall in progress or done).</summary>
        public float TrapOffsetX(int index) => trapStates[index].MinX - traps[index].StartBox.MinX;

        public float TrapOffsetY(int index) => trapStates[index].MinY - traps[index].StartBox.MinY;

        /// <summary>True while the trap travels between cells (a moving box).</summary>
        public bool IsTrapMoving(int index) => trapStates[index].Motion == Motion.Moving;

        /// <summary>
        /// A start source (play start, play-from-here, play again): every trap reset, the counter at 0, no checkpoint,
        /// and the player at rest at the nearest free start, which is also the first safe ground.
        /// </summary>
        public void Begin(float feetX, float feetY, Abilities abilities)
        {
            ResetTraps();
            Deaths = 0;
            ActiveCheckpoint = -1;
            LastPlacement = StartPlacement.Find(grid, feetX, feetY);
            StartX = LastPlacement.X;
            StartY = LastPlacement.Y;
            SafeX = StartX;
            SafeY = StartY;
            Player = stepper.CreateState(StartX, StartY, abilities);
            Phase = LevelPhase.Playing;
            PhaseTicks = 0;
        }

        /// <summary>
        /// One tick, in a fixed order: (1) Won: count up; (2) Dying: count down the beat, input ignored, then respawn
        /// (traps don't move during the beat); (3) pending trap delays; (4) traps move: each moving trap advances one
        /// fixed step, riders are carried (Y, then X), leading faces push, a blocked push nudges or crushes, and a moving
        /// Hazard is swept against the player (a crush or a hit dies at once: the player doesn't step); (5) the player's
        /// step; (6) trap triggers; (7) death by crush, hazard or pit (death wins over an exit on the same tick);
        /// (8) checkpoints, then exits; (9) safe ground.
        /// A Move or Fall fired by its trigger (6) starts moving on the next tick's (4); one fired by its Delay (3)
        /// moves in the same tick's (4). Until its first advance it stays stamped as cells.
        /// </summary>
        public void Step(in PlayerInput input)
        {
            // 1.
            if (Phase == LevelPhase.Won)
            {
                PhaseTicks++;
                return;
            }

            // 2.
            if (Phase == LevelPhase.Dying)
            {
                PhaseTicks--;
                if (PhaseTicks <= 0)
                {
                    Respawn();
                }
                return;
            }

            // 3.
            for (int i = 0; i < traps.Length; i++)
            {
                if (trapStates[i].Pending > 0 && --trapStates[i].Pending == 0)
                {
                    trapStates[i].Pending = -1;
                    Fire(i);
                }
            }
            ResolveAppeared(true);

            // 4.
            if (MoveTraps())
            {
                Die();
                return;
            }

            // 5. The ride speed starts at the fall speed the player lands on a moving trap with.
            bool onBoxBefore = StandsOnMovingBox(Player);
            float fallVelocity = Player.VelocityY;
            Player = stepper.Step(Player, input);
            if (!StandsOnMovingBox(Player))
            {
                rideSpeed = 0;
            }
            else if (!onBoxBefore)
            {
                rideSpeed = MathF.Max(0, -fallVelocity);
            }

            // 6.
            for (int e = 0; e < exitHeld.Length; e++)
            {
                exitHeld[e] = false;
            }
            for (int i = 0; i < traps.Length; i++)
            {
                bool inside = Inside(i);
                if (!trapStates[i].Armed)
                {
                    trapStates[i].Armed = !inside;
                }
                else if (!trapStates[i].Fired && inside)
                {
                    trapStates[i].Fired = true;
                    if (traps[i].DelayTicks == 0)
                    {
                        Fire(i);
                        if (traps[i].ExitIndex >= 0)
                        {
                            exitHeld[traps[i].ExitIndex] = true;
                        }
                    }
                    else
                    {
                        trapStates[i].Pending = traps[i].DelayTicks;
                    }
                }
            }
            ResolveAppeared(false);

            // 7.
            PlayerState p = Player;
            if (crushed || TileCollision.CoreTouchesHazard(grid, p.X, p.Y) || p.Y + Height < 0)
            {
                Die();
                return;
            }

            // 8.
            for (int i = 0; i < checkpoints.Length; i++)
            {
                if (Touches(checkpoints[i]))
                {
                    ActiveCheckpoint = i;
                    break;
                }
            }
            for (int e = 0; e < exits.Length; e++)
            {
                if (ExitTouched(e))
                {
                    Phase = LevelPhase.Won;
                    PhaseTicks = 0;
                    return;
                }
            }

            // 9.
            if (p.IsStanding && TileCollision.StandsFullyOnPainted(grid, p.X, p.Y, HalfWidth) && OutsideEveryTrap())
            {
                SafeX = p.X;
                SafeY = p.Y;
            }
        }

        /// <summary>Replaces the movement settings; takes effect on the next tick.</summary>
        public void ApplySettings(MovementSettings settings)
        {
            stepper.ApplySettings(settings);
            derived = new DerivedMovement(stepper.Settings);
        }

        /// <summary>Replaces the player's ability set between ticks (see <see cref="PlayerStepper.ChangeAbilities"/>).</summary>
        public void ChangeAbilities(Abilities abilities)
        {
            Player = stepper.ChangeAbilities(Player, abilities);
        }

        /// <summary>Replaces the respawn rule; the next respawn uses it.</summary>
        public void SetRule(RespawnRule rule)
        {
            Rule = rule;
        }

        void Die()
        {
            Deaths++;
            Phase = LevelPhase.Dying;
            PhaseTicks = LevelRules.DeathBeatTicks;
            DeathX = Player.X;
            DeathY = MathF.Max(0, Player.Y);
            crushed = false;
            for (int i = 0; i < trapStates.Length; i++)
            {
                trapStates[i].Pending = -1;
            }
        }

        void Respawn()
        {
            ResetTraps();
            float x, y;
            if (Rule == RespawnRule.Checkpoint && ActiveCheckpoint >= 0)
            {
                Box box = checkpoints[ActiveCheckpoint];
                x = (box.MinX + box.MaxX) / 2;
                y = box.MinY;
            }
            else if (Rule == RespawnRule.Checkpoint)
            {
                x = StartX;
                y = StartY;
            }
            else
            {
                x = SafeX;
                y = SafeY;
            }
            StartPlacement placed = StartPlacement.Find(grid, x, y);
            PlayerState s = stepper.CreateState(placed.X, placed.Y, Player.Abilities);
            s.Teleported = true;
            Player = s;
            Phase = LevelPhase.Playing;
            PhaseTicks = 0;
        }

        void ResetTraps()
        {
            bool changed = false;
            for (int i = 0; i < traps.Length; i++)
            {
                bool present = traps[i].Action != TrapActionKind.Appear;
                Box start = traps[i].StartBox;
                TrapState old = trapStates[i];
                changed |= old.Present != present || old.MinX != start.MinX || old.MinY != start.MinY;
                trapStates[i] = new TrapState
                {
                    Present = present, Armed = false, Fired = false, Pending = -1,
                    Motion = Motion.Resting, MinX = start.MinX, MinY = start.MinY,
                };
            }
            if (changed)
            {
                TrapChanges++;
            }
            boxes.Clear();
            appearedCount = 0;
            batchFired = false;
            crushed = false;
            rideSpeed = 0;
            RebuildTrapCells();
        }

        // Marks only; ResolveAppeared rebuilds the cells and pushes once per batch, so the result doesn't depend on the trap order.
        // Move and Fall only start (the next phase 4 advances them) and never change presence.
        void Fire(int i)
        {
            TrapSpec t = traps[i];
            if (t.Action == TrapActionKind.Move || t.Action == TrapActionKind.Fall)
            {
                bool still = t.Action == TrapActionKind.Move && t.OffsetX == 0 && t.OffsetY == 0;
                if (!still && trapStates[i].Motion == Motion.Resting)
                {
                    trapStates[i].Motion = Motion.Starting;
                }
                return;
            }
            batchFired = true;
            bool wasPresent = trapStates[i].Present;
            bool present = t.Action == TrapActionKind.Appear;
            trapStates[i].Present = present;
            if (present == wasPresent)
            {
                return;
            }
            TrapChanges++;
            if (present && t.Kind == TileKind.Solid)
            {
                appeared[appearedCount++] = i;
            }
        }

        // Phase 4. Returns true when the player was crushed or hit (the caller dies at once).
        bool MoveTraps()
        {
            // 1–2. Advance every started or moving trap one tick from its old state.
            int moverCount = 0;
            bool restamp = false;
            for (int i = 0; i < traps.Length; i++)
            {
                ref TrapState t = ref trapStates[i];
                if (t.Motion != Motion.Starting && t.Motion != Motion.Moving)
                {
                    continue;
                }
                TrapSpec spec = traps[i];
                Box old = CurrentBox(i);
                oldBoxes[i] = old;
                movers[moverCount++] = i;
                if (t.Motion == Motion.Starting)
                {
                    t.Motion = Motion.Moving;
                    restamp = true;
                }
                if (spec.Action == TrapActionKind.Move)
                {
                    TrapMotion.MovePosition(spec.StartBox, spec.OffsetX, spec.OffsetY, spec.Speed, ++t.MoveTick,
                        out t.MinX, out t.MinY, out bool arrived);
                    if (arrived)
                    {
                        t.Motion = Motion.Resting;
                        restamp = true;
                    }
                }
                else if (TrapMotion.FallStep(grid, old, derived.RiseGravity, derived.MaxFallSpeed, ref t.FallSpeed, out t.MinY))
                {
                    t.Motion = Motion.Resting;
                    restamp = true;
                }
                else if (t.MinY + (old.MaxY - old.MinY) < 0)
                {
                    t.Motion = Motion.Gone;
                    t.Present = false;
                    TrapChanges++;
                    restamp = true;
                }
            }
            if (moverCount == 0)
            {
                return false;
            }

            // 3–4. The box layer holds the moving tile traps; the cells change only when one starts, rests or goes.
            for (int k = 0; k < moverCount; k++)
            {
                int i = movers[k];
                if (traps[i].ExitIndex >= 0)
                {
                    continue;
                }
                if (trapStates[i].Motion == Motion.Moving)
                {
                    boxes.Set(i, CurrentBox(i), traps[i].Kind);
                }
                else
                {
                    boxes.Remove(i);
                }
            }
            if (restamp)
            {
                RebuildTrapCells();
            }

            // 5. Riders: standing on a moving Solid trap's old top. Several carriers: the highest new top, then the
            // smaller |dx|, then the smaller dx (by value, never by index).
            PlayerState p = Player;
            int carrier = -1;
            if (p.IsStanding)
            {
                Box hitbox = Box.Hitbox(p.X, p.Y);
                for (int k = 0; k < moverCount; k++)
                {
                    int i = movers[k];
                    Box o = oldBoxes[i];
                    if (!IsPusher(i) || MathF.Abs(p.Y - o.MaxY) > Epsilon
                        || !TileCollision.SpansOverlap(o.MinX, o.MaxX, hitbox.MinX, hitbox.MaxX))
                    {
                        continue;
                    }
                    if (carrier < 0 || CarriesBetter(i, carrier))
                    {
                        carrier = i;
                    }
                }
            }
            if (carrier >= 0)
            {
                Box o = oldBoxes[carrier];
                Box n = CurrentBox(carrier);
                float dx = n.MinX - o.MinX;
                float dy = n.MaxY - o.MaxY;
                // Still on the carrier's top after the Y carry; only then does it carry X (sides never carry).
                bool rides = true;
                if (dy > 0)
                {
                    rideSpeed = 0;
                    if (BoxIsFreeForPlayer(p.X, n.MaxY))
                    {
                        p.Y = n.MaxY;
                    }
                    else
                    {
                        // Lifted into a ceiling: the crush rule, nudge first.
                        pushNew[0] = n;
                        if (!BoxPush.TryNudge(grid, ref p, true, pushNew, 1))
                        {
                            Player = p;
                            return true;
                        }
                        rides = false;
                    }
                }
                else if (dy < 0)
                {
                    rideSpeed = MathF.Min(rideSpeed + derived.FallGravity * Dt, derived.MaxFallSpeed);
                    bool otherSupport = TileCollision.Support(grid, p.X, p.Y, HalfWidth) != SupportKind.None;
                    rides = !otherSupport && -dy / Dt <= rideSpeed + RideSlack;
                    if (rides)
                    {
                        float y = p.Y;
                        TileCollision.SweepY(grid, ref p.X, ref y, HalfWidth, Height, n.MaxY - p.Y, false, out SupportKind landedOn, out _);
                        p.Y = landedOn != SupportKind.None && y > n.MaxY + Epsilon ? y : n.MaxY;
                    }
                }
                else
                {
                    rideSpeed = 0;
                }
                if (dx != 0 && rides)
                {
                    // Soft: a wall stops the rider and the trap slides out from under it.
                    p.X = TileCollision.SweepX(grid, p.X, p.Y, HalfWidth, Height, dx, out _);
                }
                if (p.IsStanding && TileCollision.Support(grid, p.X, p.Y, HalfWidth) == SupportKind.None)
                {
                    // Pulled away (or slid off): in the air with the walk-off coyote window from this tick.
                    p.Contact = PlayerContact.Air;
                    p.CoyoteTicks = MovementInternals.CoyoteTicks;
                }
            }

            // 6. Leading-face pushes from every moving Solid trap now overlapping the player.
            int pushCount = 0;
            Box after = Box.Hitbox(p.X, p.Y);
            for (int k = 0; k < moverCount; k++)
            {
                int i = movers[k];
                if (!IsPusher(i))
                {
                    continue;
                }
                Box n = CurrentBox(i);
                if (TileCollision.SpansOverlap(n.MinX, n.MaxX, after.MinX, after.MaxX) && TileCollision.SpansOverlap(n.MinY, n.MaxY, after.MinY, after.MaxY))
                {
                    pushOld[pushCount] = oldBoxes[i];
                    pushNew[pushCount] = n;
                    pushCount++;
                }
            }
            if (pushCount > 0 && !BoxPush.Resolve(grid, ref p, pushOld, pushNew, pushCount))
            {
                Player = p;
                return true;
            }
            Player = p;

            // 7. A moving Hazard is swept from its old box to its new one against the player's hazard core.
            Box core = TileCollision.HazardCore(p.X, p.Y);
            for (int k = 0; k < moverCount; k++)
            {
                int i = movers[k];
                if (traps[i].ExitIndex >= 0 || traps[i].Kind != TileKind.Hazard)
                {
                    continue;
                }
                Box o = oldBoxes[i];
                if (Box.SweptOverlaps(o, trapStates[i].MinX - o.MinX, trapStates[i].MinY - o.MinY, core))
                {
                    return true;
                }
            }
            return false;
        }

        // A moving tile trap that carries and pushes: Solid, not on an exit, not gone.
        bool IsPusher(int i)
        {
            return traps[i].ExitIndex < 0 && traps[i].Kind == TileKind.Solid && trapStates[i].Motion != Motion.Gone;
        }

        bool CarriesBetter(int a, int b)
        {
            float topA = CurrentBox(a).MaxY;
            float topB = CurrentBox(b).MaxY;
            if (topA != topB)
            {
                return topA > topB;
            }
            float dxA = trapStates[a].MinX - oldBoxes[a].MinX;
            float dxB = trapStates[b].MinX - oldBoxes[b].MinX;
            if (MathF.Abs(dxA) != MathF.Abs(dxB))
            {
                return MathF.Abs(dxA) < MathF.Abs(dxB);
            }
            return dxA < dxB;
        }

        bool BoxIsFreeForPlayer(float x, float y)
        {
            return TileCollision.BoxIsFree(grid, x, y, HalfWidth, Height) && !TileCollision.CoreTouchesHazard(grid, x, y);
        }

        // Standing with the feet on the top of a box in the moving-box layer.
        bool StandsOnMovingBox(in PlayerState p)
        {
            if (!p.IsStanding)
            {
                return false;
            }
            for (int k = 0; k < boxes.ActiveCount; k++)
            {
                boxes.GetActive(k, out Box b, out TileKind kind);
                if (kind == TileKind.Solid && MathF.Abs(b.MaxY - p.Y) <= Epsilon
                    && TileCollision.SpansOverlap(p.X - HalfWidth, p.X + HalfWidth, b.MinX, b.MaxX))
                {
                    return true;
                }
            }
            return false;
        }

        void ResolveAppeared(bool playerStepsThisTick)
        {
            if (!batchFired)
            {
                return;
            }
            batchFired = false;
            RebuildTrapCells();

            // The bounding box of the appeared Solid traps that overlap the player.
            Box hitbox = Box.Hitbox(Player.X, Player.Y);
            bool any = false;
            float minX = 0, minY = 0, maxX = 0, maxY = 0;
            for (int k = 0; k < appearedCount; k++)
            {
                Box box = CurrentBox(appeared[k]);
                if (!box.Overlaps(hitbox))
                {
                    continue;
                }
                if (!any)
                {
                    minX = box.MinX;
                    minY = box.MinY;
                    maxX = box.MaxX;
                    maxY = box.MaxY;
                    any = true;
                }
                else
                {
                    minX = MathF.Min(minX, box.MinX);
                    minY = MathF.Min(minY, box.MinY);
                    maxX = MathF.Max(maxX, box.MaxX);
                    maxY = MathF.Max(maxY, box.MaxY);
                }
            }
            appearedCount = 0;
            if (any)
            {
                PushOut(new Box(minX, minY, maxX, maxY), hitbox, playerStepsThisTick);
            }
        }

        // The shortest free way out of the box: up, left, right or down (ties in that order); none free = crushed.
        void PushOut(Box box, Box hitbox, bool playerStepsThisTick)
        {
            PlayerState s = Player;
            int tried = 0;
            for (int pass = 0; pass < 4; pass++)
            {
                int best = -1;
                float bestDistance = float.MaxValue;
                for (int way = 0; way < 4; way++)
                {
                    if ((tried & (1 << way)) != 0)
                    {
                        continue;
                    }
                    float distance = way == 0 ? box.MaxY - hitbox.MinY
                        : way == 1 ? hitbox.MaxX - box.MinX
                        : way == 2 ? box.MaxX - hitbox.MinX
                        : hitbox.MaxY - box.MinY;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = way;
                    }
                }
                tried |= 1 << best;
                float x = best == 1 ? box.MinX - HalfWidth : best == 2 ? box.MaxX + HalfWidth : s.X;
                float y = best == 0 ? box.MaxY : best == 3 ? box.MinY - Height : s.Y;
                if (!BoxIsFreeForPlayer(x, y))
                {
                    continue;
                }
                s.X = x;
                s.Y = y;
                BoxPush.ApplyPush(ref s, best == 1 ? -1 : best == 2 ? 1 : 0, best == 0 ? 1 : best == 3 ? -1 : 0, playerStepsThisTick);
                Player = s;
                return;
            }
            crushed = true;
        }

        // Present tile traps at rest (or started, not yet advanced) are cells at their current rest cells.
        void RebuildTrapCells()
        {
            grid.ClearTrapCells();
            // Hazard traps first, so a Solid trap on the same cell wins.
            for (int pass = 0; pass < 2; pass++)
            {
                TileKind kind = pass == 0 ? TileKind.Hazard : TileKind.Solid;
                for (int i = 0; i < traps.Length; i++)
                {
                    TrapSpec t = traps[i];
                    TrapState state = trapStates[i];
                    if (!state.Present || t.Kind != kind || (state.Motion != Motion.Resting && state.Motion != Motion.Starting))
                    {
                        continue;
                    }
                    int cellX = (int)MathF.Floor(state.MinX + 0.5f);
                    int cellY = (int)MathF.Floor(state.MinY + 0.5f);
                    for (int y = cellY; y < cellY + t.Height; y++)
                    {
                        for (int x = cellX; x < cellX + t.Width; x++)
                        {
                            grid.SetTrapCell(x, y, kind);
                        }
                    }
                }
            }
        }

        // The trap's box now: the start box's size at the current min.
        Box CurrentBox(int i)
        {
            Box start = traps[i].StartBox;
            float minX = trapStates[i].MinX;
            float minY = trapStates[i].MinY;
            return new Box(minX, minY, minX + (start.MaxX - start.MinX), minY + (start.MaxY - start.MinY));
        }

        // The zone never moves; a Touch trigger uses the current box (a gone trap has none).
        bool Inside(int i)
        {
            TrapSpec t = traps[i];
            if (t.Trigger == TrapTriggerKind.Zone)
            {
                return Box.Hitbox(Player.X, Player.Y).Overlaps(t.Zone);
            }
            return trapStates[i].Motion != Motion.Gone && Touches(CurrentBox(i));
        }

        // An exit with a trap is where its trap's box is now; hidden or gone, it can't be touched, and not on the tick its trap fired.
        bool ExitTouched(int e)
        {
            int i = exitTraps[e];
            if (i < 0)
            {
                return Touches(exits[e]);
            }
            return trapStates[i].Present && !exitHeld[e] && Touches(CurrentBox(i));
        }

        // The one touch rule (Touch triggers, checkpoints, exits): the hitbox grown by the contact margin overlaps the box.
        bool Touches(in Box box)
        {
            return Box.Hitbox(Player.X, Player.Y).Grown(LevelRules.ContactMargin).Overlaps(box);
        }

        // Safe ground is never inside a trap's trigger nor on or in a tile trap's current box (an exit trap counts by its trigger only).
        bool OutsideEveryTrap()
        {
            Box hitbox = Box.Hitbox(Player.X, Player.Y);
            for (int i = 0; i < traps.Length; i++)
            {
                if (Inside(i))
                {
                    return false;
                }
                if (traps[i].ExitIndex < 0 && trapStates[i].Motion != Motion.Gone && hitbox.Overlaps(CurrentBox(i)))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
