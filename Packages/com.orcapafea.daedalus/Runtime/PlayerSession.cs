using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Runs the level's sim for the Player component: the world step, the 60 Hz clock, the two player states and trap
    /// offsets the renderer interpolates between, and restarts. Input-agnostic: the caller pulls one input per tick.
    /// </summary>
    internal sealed class PlayerSession
    {
        readonly CollisionMap map;
        readonly LevelStepper level;
        readonly FixedStepAccumulator accumulator = new FixedStepAccumulator();
        // Each trap's offset from its start (sim = world units) at the last two ticks.
        readonly Vector2[] previousTrapOffsets;
        readonly Vector2[] currentTrapOffsets;

        public PlayerSession(CollisionMap map, RopeSegment[] ropes, TrapSpec[] traps, Box[] checkpoints, Box[] exits,
            MovementSettings settings, RespawnRule rule, PlayStart start)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            level = new LevelStepper(map.Grid, ropes, traps, checkpoints, exits, settings, rule);
            previousTrapOffsets = new Vector2[level.TrapCount];
            currentTrapOffsets = new Vector2[level.TrapCount];
            Restart(start);
        }

        public CollisionMap Map => map;
        public PlayerState Current { get; private set; }
        public PlayerState Previous { get; private set; }
        public StartPlacement LastPlacement => level.LastPlacement;
        public double InterpolationFraction => accumulator.InterpolationFraction;

        public LevelPhase Phase => level.Phase;
        public int PhaseTicks => level.PhaseTicks;
        /// <summary>This stage's deaths; the game's total is the stage run's carried count plus this.</summary>
        public int Deaths => level.Deaths;
        public int WonExit => level.WonExit;
        public bool CanPlayAgain => level.CanPlayAgain;
        public int TrapChanges => level.TrapChanges;
        public int TrapCount => level.TrapCount;
        public int ActiveCheckpoint => level.ActiveCheckpoint;
        public bool IsTrapPresent(int index) => level.IsTrapPresent(index);

        /// <summary>The feet at the last death, in world space.</summary>
        public Vector2 DeathFeetWorld => map.SimToWorld(new Vector2(level.DeathX, level.DeathY));

        /// <summary>The placed start of the last restart, in world space.</summary>
        public Vector2 StartFeetWorld => map.SimToWorld(new Vector2(level.StartX, level.StartY));

        /// <summary>The feet centre in world space, between the last two ticks at the clock's fraction.</summary>
        public Vector2 InterpolatedFeetWorld
        {
            get
            {
                float t = (float)accumulator.InterpolationFraction;
                PlayerState previous = Previous;
                PlayerState current = Current;
                var feet = new Vector2(previous.X + (current.X - previous.X) * t, previous.Y + (current.Y - previous.Y) * t);
                return map.SimToWorld(feet);
            }
        }

        /// <summary>Trap <paramref name="index"/>'s offset from its start, in world units, between the last two ticks at the clock's fraction.</summary>
        public Vector2 InterpolatedTrapOffset(int index)
        {
            float t = (float)accumulator.InterpolationFraction;
            Vector2 previous = previousTrapOffsets[index];
            return previous + (currentTrapOffsets[index] - previous) * t;
        }

        /// <summary>
        /// Puts the player at <paramref name="start"/> (moved out of walls if needed), at rest, with its abilities plus Jump;
        /// every trap reset and this stage's death counter at 0.
        /// </summary>
        public void Restart(PlayStart start)
        {
            Vector2 feet = map.WorldToSim(start.Feet);
            level.Begin(feet.x, feet.y, start.Abilities | Abilities.Jump);
            Current = level.Player;
            Previous = Current;
            ReadTrapOffsets(true);
            accumulator.Reset();
        }

        /// <summary>Adds elapsed time; returns how many ticks to run now.</summary>
        public int AdvanceClock(double seconds) => accumulator.Advance(seconds);

        /// <summary>Forgets the time banked so far, so a long frame can't turn into catch-up ticks.</summary>
        public void ResetClock() => accumulator.Reset();

        public void Tick(in PlayerInput input)
        {
            Previous = Current;
            level.Step(input);
            Current = level.Player;
            // A respawn resets every trap: it snaps back instead of sliding.
            ReadTrapOffsets(Current.Teleported);
            if (Current.Teleported)
            {
                Previous = Current;
            }
        }

        void ReadTrapOffsets(bool snap)
        {
            for (int i = 0; i < currentTrapOffsets.Length; i++)
            {
                previousTrapOffsets[i] = currentTrapOffsets[i];
                currentTrapOffsets[i] = new Vector2(level.TrapOffsetX(i), level.TrapOffsetY(i));
                if (snap)
                {
                    previousTrapOffsets[i] = currentTrapOffsets[i];
                }
            }
        }

        public void ApplySettings(MovementSettings settings) => level.ApplySettings(settings);

        /// <summary>Replaces the ability set during play (Jump stays on); takes effect on the next tick.</summary>
        public void ChangeAbilities(Abilities abilities)
        {
            level.ChangeAbilities(abilities | Abilities.Jump);
            Current = level.Player;
        }

        /// <summary>Replaces the respawn rule (the Player's On hazard); the next respawn uses it.</summary>
        public void SetRule(RespawnRule rule) => level.SetRule(rule);
    }
}
