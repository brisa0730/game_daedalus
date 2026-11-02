using System;
using System.Collections.Generic;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// A level on an <see cref="AsciiLevel"/> plus traps, checkpoints and exits, stepped through the real
    /// <see cref="LevelStepper.Step"/>. Add elements, then <see cref="Begin"/> (which starts at the map's 'P').
    /// Records the phase per tick and the ticks deaths happened on.
    /// </summary>
    sealed class LevelScenario
    {
        readonly List<TrapSpec> traps = new List<TrapSpec>();
        readonly List<Box> checkpoints = new List<Box>();
        readonly List<Box> exits = new List<Box>();
        Abilities abilities = Abilities.Jump;
        RespawnRule rule = RespawnRule.SafeGround;
        MovementSettings settings = MovementSettings.Default;

        public LevelScenario(params string[] rows)
        {
            Level = AsciiLevel.Parse(rows);
        }

        public AsciiLevel Level { get; }
        public LevelStepper Stepper { get; private set; }
        public PlayerState State => Stepper.Player;
        /// <summary>The player before the last tick.</summary>
        public PlayerState Previous { get; private set; }
        public int Ticks { get; private set; }
        /// <summary>The tick (1-based, counted from Begin) of each death.</summary>
        public List<int> DeathTicks { get; } = new List<int>();
        public List<LevelPhase> Phases { get; } = new List<LevelPhase>();

        public LevelScenario WithAbilities(Abilities value)
        {
            abilities = value;
            return this;
        }

        public LevelScenario WithRule(RespawnRule value)
        {
            rule = value;
            return this;
        }

        public LevelScenario WithSettings(MovementSettings value)
        {
            settings = value;
            return this;
        }

        /// <summary>A trap whose box is cells (x, y) to (x + w, y + h); the zone defaults to <see cref="LevelRules.DefaultZone"/>.</summary>
        public LevelScenario Trap(int x, int y, int w, int h, TileKind kind, TrapActionKind action,
            TrapTriggerKind trigger = TrapTriggerKind.Zone, Box? zone = null, int delayTicks = 0,
            int offsetX = 0, int offsetY = 0, float speed = LevelRules.DefaultMoveSpeed)
        {
            var box = new Box(x, y, x + w, y + h);
            traps.Add(new TrapSpec(x, y, w, h, kind, action, trigger, zone ?? LevelRules.DefaultZone(box), delayTicks, offsetX, offsetY, speed));
            return this;
        }

        /// <summary>A trap on exit <paramref name="exitIndex"/> (add the exit first); the zone defaults to <see cref="LevelRules.DefaultExitZone"/>.</summary>
        public LevelScenario ExitTrap(int exitIndex, TrapActionKind action, TrapTriggerKind trigger = TrapTriggerKind.Zone,
            Box? zone = null, int delayTicks = 0, int offsetX = 0, int offsetY = 0, float speed = LevelRules.DefaultMoveSpeed)
        {
            Box exit = exits[exitIndex];
            traps.Add(TrapSpec.ForExit(exitIndex, exit, action, trigger, zone ?? LevelRules.DefaultExitZone(exit), delayTicks, offsetX, offsetY, speed));
            return this;
        }

        /// <summary>The traps in reverse order, for order-independence checks (call before <see cref="Begin"/>).</summary>
        public LevelScenario Reversed()
        {
            traps.Reverse();
            return this;
        }

        public LevelScenario Checkpoint(Box box)
        {
            checkpoints.Add(box);
            return this;
        }

        public LevelScenario Exit(Box box)
        {
            exits.Add(box);
            return this;
        }

        public LevelScenario Begin()
        {
            return BeginAt(Level.StartX, Level.StartY);
        }

        public LevelScenario BeginAt(float feetX, float feetY)
        {
            if (Stepper == null)
            {
                Stepper = new LevelStepper(Level.Grid, Level.Ropes, traps.ToArray(), checkpoints.ToArray(), exits.ToArray(), settings, rule);
            }
            Stepper.Begin(feetX, feetY, abilities);
            Previous = Stepper.Player;
            Ticks = 0;
            DeathTicks.Clear();
            Phases.Clear();
            return this;
        }

        public static PlayerInput Idle => PlayerScenario.Idle;

        public void Tick(PlayerInput input, int count = 1)
        {
            for (int i = 0; i < count; i++)
            {
                Previous = Stepper.Player;
                int deaths = Stepper.Deaths;
                Stepper.Step(input);
                Ticks++;
                Phases.Add(Stepper.Phase);
                if (Stepper.Deaths != deaths)
                {
                    DeathTicks.Add(Ticks);
                }
            }
        }

        /// <summary>Ticks with <paramref name="script"/>'s input until <paramref name="done"/> holds after a tick; returns the ticks run, or −1 at the limit.</summary>
        public int RunUntil(Func<LevelScenario, PlayerInput> script, Func<LevelScenario, bool> done, int limit = 600)
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

        public int RunUntil(PlayerInput input, Func<LevelScenario, bool> done, int limit = 600)
        {
            return RunUntil(_ => input, done, limit);
        }
    }
}
