using System;
using System.Collections.Generic;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// A player on an <see cref="AsciiLevel"/>, stepped through the real <see cref="PlayerStepper.Step"/>.
    /// Keeps a record of the run (max Y, ticks, contacts seen) for apex and outcome asserts.
    /// </summary>
    sealed class PlayerScenario
    {
        public const Abilities AllAbilities = Abilities.Jump | Abilities.DoubleJump | Abilities.WallJump | Abilities.WallClimb | Abilities.RopeClimb;

        public PlayerScenario(Abilities abilities, params string[] rows)
            : this(abilities, MovementSettings.Default, rows)
        {
        }

        public PlayerScenario(Abilities abilities, MovementSettings settings, params string[] rows)
        {
            Level = AsciiLevel.Parse(rows);
            Stepper = new PlayerStepper(Level.Grid, Level.Ropes, settings);
            State = Stepper.CreateState(Level.StartX, Level.StartY, abilities);
            Previous = State;
            ResetRecord();
        }

        public AsciiLevel Level { get; }
        public PlayerStepper Stepper { get; }
        public PlayerState State { get; set; }
        /// <summary>The state before the last tick.</summary>
        public PlayerState Previous { get; private set; }
        public int Ticks { get; private set; }
        public float MaxY { get; private set; }
        public int MaxYTick { get; private set; }
        public HashSet<PlayerContact> ContactsSeen { get; } = new HashSet<PlayerContact>();

        public static PlayerInput Idle => new PlayerInput(0, 0, false, false);

        public static PlayerInput Hold(int moveX = 0, int moveY = 0, bool jumpHeld = false)
        {
            return new PlayerInput(moveX, moveY, false, jumpHeld);
        }

        /// <summary>A jump press, held unless <paramref name="jumpHeld"/> is false (a one-tick tap).</summary>
        public static PlayerInput Press(int moveX = 0, int moveY = 0, bool jumpHeld = true)
        {
            return new PlayerInput(moveX, moveY, true, jumpHeld);
        }

        public void ResetRecord()
        {
            MaxY = State.Y;
            MaxYTick = Ticks;
            ContactsSeen.Clear();
            ContactsSeen.Add(State.Contact);
        }

        public void Tick(PlayerInput input, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Previous = State;
                State = Stepper.Step(State, input);
                Ticks++;
                if (State.Y > MaxY)
                {
                    MaxY = State.Y;
                    MaxYTick = Ticks;
                }
                ContactsSeen.Add(State.Contact);
            }
        }

        /// <summary>Ticks with <paramref name="script"/>'s input until <paramref name="done"/> holds after a tick; returns the ticks run, or −1 at the limit.</summary>
        public int RunUntil(Func<PlayerScenario, PlayerInput> script, Func<PlayerScenario, bool> done, int limit = 600)
        {
            for (int i = 1; i <= limit; i++)
            {
                Tick(script(this));
                if (done(this))
                {
                    return i;
                }
            }
            return -1;
        }

        public int RunUntil(PlayerInput input, Func<PlayerScenario, bool> done, int limit = 600)
        {
            return RunUntil(_ => input, done, limit);
        }
    }
}
