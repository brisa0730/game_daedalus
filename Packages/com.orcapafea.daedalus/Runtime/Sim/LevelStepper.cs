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

        struct TrapState
        {
            public bool Present;
            /// <summary>The player has been outside the trigger since the last reset, so it may fire.</summary>
            public bool Armed;
            public bool Fired;
            /// <summary>Ticks until a delayed fire; −1 = none.</summary>
            public int Pending;
        }

        readonly TileGrid grid;
        readonly PlayerStepper stepper;
        readonly TrapSpec[] traps;
        readonly TrapState[] trapStates;
        readonly Box[] checkpoints;
        readonly Box[] exits;
        // Solid traps that appeared in the current batch of fires.
        readonly int[] appeared;
        int appearedCount;
        bool batchFired;
        bool crushed;

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
            this.traps = (TrapSpec[])traps.Clone();
            trapStates = new TrapState[traps.Length];
            appeared = new int[traps.Length];
            this.checkpoints = (Box[])checkpoints.Clone();
            this.exits = (Box[])exits.Clone();
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
        /// One tick, in a fixed order: (1) Won: count up; (2) Dying: count down the beat, input ignored, then respawn;
        /// (3) pending trap delays; (4) the player's step; (5) trap triggers; (6) death by crush, hazard or pit (death
        /// wins over an exit on the same tick); (7) checkpoints, then exits; (8) safe ground.
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
            ResolveAppeared();

            // 4.
            Player = stepper.Step(Player, input);

            // 5.
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
                    }
                    else
                    {
                        trapStates[i].Pending = traps[i].DelayTicks;
                    }
                }
            }
            ResolveAppeared();

            // 6.
            PlayerState p = Player;
            if (crushed || TileCollision.CoreTouchesHazard(grid, p.X, p.Y) || p.Y + Height < 0)
            {
                Die();
                return;
            }

            // 7.
            for (int i = 0; i < checkpoints.Length; i++)
            {
                if (Touches(checkpoints[i]))
                {
                    ActiveCheckpoint = i;
                    break;
                }
            }
            for (int i = 0; i < exits.Length; i++)
            {
                if (Touches(exits[i]))
                {
                    Phase = LevelPhase.Won;
                    PhaseTicks = 0;
                    return;
                }
            }

            // 8.
            if (p.IsStanding && TileCollision.StandsFullyOnPainted(grid, p.X, p.Y, HalfWidth) && OutsideEveryTrap())
            {
                SafeX = p.X;
                SafeY = p.Y;
            }
        }

        /// <summary>Replaces the movement settings; takes effect on the next tick.</summary>
        public void ApplySettings(MovementSettings settings) => stepper.ApplySettings(settings);

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
                bool present = traps[i].Action == TrapActionKind.Vanish;
                changed |= trapStates[i].Present != present;
                trapStates[i] = new TrapState { Present = present, Armed = false, Fired = false, Pending = -1 };
            }
            if (changed)
            {
                TrapChanges++;
            }
            appearedCount = 0;
            batchFired = false;
            crushed = false;
            RebuildTrapCells();
        }

        // Marks only; ResolveAppeared rebuilds the cells and pushes once per batch, so the result doesn't depend on the trap order.
        void Fire(int i)
        {
            bool wasPresent = trapStates[i].Present;
            bool present = traps[i].Action == TrapActionKind.Appear;
            trapStates[i].Present = present;
            batchFired = true;
            if (present == wasPresent)
            {
                return;
            }
            TrapChanges++;
            if (present && traps[i].Kind == TileKind.Solid)
            {
                appeared[appearedCount++] = i;
            }
        }

        void ResolveAppeared()
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
                Box box = traps[appeared[k]].CellBox;
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
                PushOut(new Box(minX, minY, maxX, maxY), hitbox);
            }
        }

        // The shortest free way out of the box: up, left, right or down (ties in that order); none free = crushed.
        void PushOut(Box box, Box hitbox)
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
                if (!TileCollision.BoxIsFree(grid, x, y, HalfWidth, Height) || TileCollision.CoreTouchesHazard(grid, x, y))
                {
                    continue;
                }
                s.X = x;
                s.Y = y;
                // Zero the speed that points into the trap; let go of a rope or a wall.
                if ((best == 0 && s.VelocityY < 0) || (best == 3 && s.VelocityY > 0))
                {
                    s.VelocityY = 0;
                }
                if ((best == 1 && s.VelocityX > 0) || (best == 2 && s.VelocityX < 0))
                {
                    s.VelocityX = 0;
                }
                s.Contact = PlayerContact.Air;
                s.RopeIndex = -1;
                s.WallSide = 0;
                Player = s;
                return;
            }
            crushed = true;
        }

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
                    if (!trapStates[i].Present || t.Kind != kind)
                    {
                        continue;
                    }
                    for (int y = t.CellY; y < t.CellY + t.Height; y++)
                    {
                        for (int x = t.CellX; x < t.CellX + t.Width; x++)
                        {
                            grid.SetTrapCell(x, y, kind);
                        }
                    }
                }
            }
        }

        bool Inside(int i)
        {
            TrapSpec t = traps[i];
            return t.Trigger == TrapTriggerKind.Zone
                ? Box.Hitbox(Player.X, Player.Y).Overlaps(t.Zone)
                : Touches(t.CellBox);
        }

        // The one touch rule (Touch triggers, checkpoints, exits): the hitbox grown by the contact margin overlaps the box.
        bool Touches(in Box box)
        {
            return Box.Hitbox(Player.X, Player.Y).Grown(LevelRules.ContactMargin).Overlaps(box);
        }

        // Safe ground is never inside a trap's trigger nor on or in a trap's box.
        bool OutsideEveryTrap()
        {
            Box hitbox = Box.Hitbox(Player.X, Player.Y);
            for (int i = 0; i < traps.Length; i++)
            {
                if (Inside(i) || hitbox.Overlaps(traps[i].CellBox))
                {
                    return false;
                }
            }
            return true;
        }
    }
}
