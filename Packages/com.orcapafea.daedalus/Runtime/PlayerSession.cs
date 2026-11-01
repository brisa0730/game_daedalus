using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Runs the player's sim for the Player component: the stepper, the 60 Hz clock, the two states the
    /// renderer interpolates between, and restarts. Input-agnostic: the caller pulls one input per tick.
    /// </summary>
    internal sealed class PlayerSession
    {
        readonly CollisionMap map;
        readonly PlayerStepper stepper;
        FixedStepAccumulator accumulator;

        public PlayerSession(CollisionMap map, RopeSegment[] ropes, MovementSettings settings, PlayStart start)
        {
            this.map = map ?? throw new ArgumentNullException(nameof(map));
            stepper = new PlayerStepper(map.Grid, ropes, settings);
            Restart(start);
        }

        public CollisionMap Map => map;
        public PlayerState Current { get; private set; }
        public PlayerState Previous { get; private set; }
        public StartPlacement LastPlacement { get; private set; }
        public double InterpolationFraction => accumulator.InterpolationFraction;

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

        /// <summary>Puts the player at <paramref name="start"/> (moved out of walls if needed), at rest, with its abilities plus Jump.</summary>
        public void Restart(PlayStart start)
        {
            Vector2 feet = map.WorldToSim(start.Feet);
            LastPlacement = StartPlacement.Find(map.Grid, feet.x, feet.y);
            Current = stepper.CreateState(LastPlacement.X, LastPlacement.Y, start.Abilities | Abilities.Jump);
            Previous = Current;
            accumulator = new FixedStepAccumulator();
        }

        /// <summary>Adds elapsed time; returns how many ticks to run now.</summary>
        public int AdvanceClock(double seconds) => accumulator.Advance(seconds);

        public void Tick(in PlayerInput input)
        {
            Previous = Current;
            Current = stepper.Step(Current, input);
            if (Current.Teleported)
            {
                Previous = Current;
            }
        }

        public void ApplySettings(MovementSettings settings) => stepper.ApplySettings(settings);

        /// <summary>Replaces the ability set during play (Jump stays on); takes effect on the next tick.</summary>
        public void ChangeAbilities(Abilities abilities)
        {
            Current = stepper.ChangeAbilities(Current, abilities | Abilities.Jump);
        }
    }
}
