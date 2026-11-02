using System;
using System.Collections.Generic;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine.TestTools.Constraints;
using static Orcapafea.Daedalus.Tests.PlayerScenario;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Is = NUnit.Framework.Is;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// <see cref="PlayerStepper.Step"/> allocates nothing in steady play (CLAUDE.md Architecture rule 6).
    /// Scripted runs exercising every branch are recorded once, then replayed from arrays and measured.
    /// </summary>
    public class StepAllocationTests
    {
        const Abilities All = AllAbilities;

        sealed class Run
        {
            public PlayerStepper Stepper;
            public PlayerState Start;
            public PlayerInput[] Inputs;
        }

        static Run Record(string[] rows, List<PlayerInput> inputs, HashSet<string> seen)
        {
            var p = new PlayerScenario(All, rows);
            var run = new Run { Stepper = p.Stepper, Start = p.State, Inputs = inputs.ToArray() };
            foreach (PlayerInput input in run.Inputs)
            {
                p.Tick(input);
                PlayerState s = p.State;
                seen.Add(s.Contact.ToString());
                if (s.WallLockTicks == MovementInternals.WallJumpLockTicks)
                {
                    seen.Add("wall jump");
                }
                if (p.Previous.Contact == PlayerContact.Air && p.Previous.AirJumpsLeft == 1 && s.AirJumpsLeft == 0)
                {
                    seen.Add("air jump");
                }
                if (p.Previous.Contact == PlayerContact.WallCling && s.Contact == PlayerContact.Ground)
                {
                    seen.Add("ledge pop");
                }
                if (p.Previous.Contact == PlayerContact.Rope && s.Contact == PlayerContact.Air && s.RopeLockoutTicks == MovementInternals.RopeLockoutTicks)
                {
                    seen.Add("rope jump");
                }
                if (p.Previous.Contact == PlayerContact.WallCling && s.WallLockTicks == MovementInternals.WallJumpLockTicks)
                {
                    seen.Add("cling jump");
                }
                if (p.Previous.Contact == PlayerContact.OneWayTop && s.Contact == PlayerContact.Air && input.MoveY < 0)
                {
                    seen.Add("drop");
                }
                if (s.Y + MovementInternals.HitboxHeight < 0)
                {
                    seen.Add("below the map");
                }
            }
            return run;
        }

        static List<PlayerInput> Script(params (PlayerInput input, int ticks)[] parts)
        {
            var list = new List<PlayerInput>();
            foreach ((PlayerInput input, int ticks) in parts)
            {
                for (int i = 0; i < ticks; i++)
                {
                    list.Add(input);
                }
            }
            return list;
        }

        static Run[] RecordAll(out int steps)
        {
            var seen = new HashSet<string>();
            Run[] runs =
            {
                // Run, jump, air jump, land; then walk off the ledge and fall below the map.
                Record(AsciiLevel.Rows(30, 14, (x, y) => y == 0 && x < 20 ? '#' : x == 2 && y == 1 ? 'P' : '.'),
                    Script((Hold(1), 20), (Press(1), 1), (Hold(1, 0, true), 15), (Press(1), 1), (Hold(1, 0, true), 220)), seen),
                // Slide and wall jump on an ordinary wall.
                Record(WallJumpTests.WallMap(8, 20),
                    Script((Hold(1), 40), (Press(1), 1), (Hold(1, 0, true), 80)), seen),
                // Cling, climb and pop over a climbable wall's open top.
                Record(AsciiLevel.Rows(14, 20, (x, y) => y == 0 ? '#' : x == 10 && y < 8 ? 'C' : x == 8 && y == 6 ? 'P' : '.'),
                    Script((Hold(1), 30), (Hold(0, 1), 120)), seen),
                // Cling to a tall climbable wall and jump off it.
                Record(AsciiLevel.Rows(14, 30, (x, y) => y == 0 ? '#' : x == 10 && y < 28 ? 'C' : x == 8 && y == 20 ? 'P' : '.'),
                    Script((Hold(1), 30), (Press(1), 1), (Hold(1), 60)), seen),
                // Rope grab, climb, rope jump.
                Record(AsciiLevel.Rows(12, 30, (x, y) => y == 0 ? '#' : x == 5 && y < 25 ? 'R' : x == 4 && y == 20 ? 'P' : '.'),
                    Script((Hold(1, 1), 40), (Press(-1), 1), (Hold(-1), 40)), seen),
                // Drop through a one-way.
                Record(AsciiLevel.Rows(8, 10, (x, y) => y == 0 ? '#' : y == 4 ? '-' : x == 3 && y == 5 ? 'P' : '.'),
                    Script((Idle, 2), (Press(0, -1), 1), (Hold(0, -1), 60)), seen),
            };

            foreach (string branch in new[] { "Ground", "Air", "OneWayTop", "WallSlide", "WallCling", "Rope", "wall jump", "air jump", "rope jump", "cling jump", "ledge pop", "drop", "below the map" })
            {
                Assert.That(seen, Does.Contain(branch), $"precondition: the recorded scripts exercise {branch}");
            }
            steps = 0;
            foreach (Run run in runs)
            {
                steps += run.Inputs.Length;
            }
            return runs;
        }

        static void Replay(Run[] runs)
        {
            for (int r = 0; r < runs.Length; r++)
            {
                Run run = runs[r];
                PlayerState s = run.Start;
                PlayerInput[] inputs = run.Inputs;
                for (int i = 0; i < inputs.Length; i++)
                {
                    s = run.Stepper.Step(s, inputs[i]);
                }
            }
        }

        static bool ProbeWorks()
        {
            long before = GC.GetAllocatedBytesForCurrentThread();
            byte[] planted = new byte[1024];
            long after = GC.GetAllocatedBytesForCurrentThread();
            GC.KeepAlive(planted);
            return after - before >= 1024;
        }

        // Two probes: GC.GetAllocatedBytesForCurrentThread where the runtime supports it, else the Test
        // Framework's GC.Alloc-recorder constraint. Each must first see a planted allocation (no vacuous pass).
        [Test]
        public void StepAllocatesNothing()
        {
            Run[] runs = RecordAll(out int steps);
            Assert.That(steps, Is.GreaterThanOrEqualTo(600), "at least 600 measured steps");
            Replay(runs);

            if (ProbeWorks())
            {
                long before = GC.GetAllocatedBytesForCurrentThread();
                Replay(runs);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                TestContext.WriteLine($"probe GC.GetAllocatedBytesForCurrentThread: {steps} steps allocated {allocated} bytes");
                Assert.AreEqual(0, allocated, $"Step allocated {allocated} bytes over {steps} ticks");
                return;
            }

            byte[] planted = null;
            Assert.That(() => { planted = new byte[1024]; }, AllocIs.AllocatingGCMemory(),
                "neither probe sees a planted 1 KB allocation; the allocation guarantee rests on code review");
            GC.KeepAlive(planted);
            TestContext.WriteLine($"probe Is.Not.AllocatingGCMemory (GC.GetAllocatedBytesForCurrentThread is unsupported): {steps} steps");
            Assert.That(() => Replay(runs), AllocIs.Not.AllocatingGCMemory(), $"Step allocated over {steps} ticks");
        }

        sealed class LevelRun
        {
            public LevelStepper Stepper;
            public float StartX;
            public float StartY;
            public PlayerInput[] Inputs;
        }

        // A branch a recorded level run must reach: checked after each tick with trap 0's motion before the tick.
        delegate bool Probe(LevelScenario s, PlayerInput input, bool trapWasMoving);

        static LevelRun RecordLevel(LevelScenario s, List<PlayerInput> inputs, HashSet<string> seen, params (string name, Probe probe)[] probes)
        {
            return RecordLevelAt(s, s.Level.StartX, s.Level.StartY, inputs, seen, probes);
        }

        static LevelRun RecordLevelAt(LevelScenario s, float x, float y, List<PlayerInput> inputs, HashSet<string> seen, params (string name, Probe probe)[] probes)
        {
            s.BeginAt(x, y);
            var run = new LevelRun { Stepper = s.Stepper, StartX = x, StartY = y, Inputs = inputs.ToArray() };
            foreach (PlayerInput input in run.Inputs)
            {
                LevelPhase before = s.Stepper.Phase;
                int changes = s.Stepper.TrapChanges;
                int checkpoint = s.Stepper.ActiveCheckpoint;
                bool wasMoving = s.Stepper.TrapCount > 0 && s.Stepper.IsTrapMoving(0);
                s.Tick(input);
                foreach ((string name, Probe probe) in probes)
                {
                    if (probe(s, input, wasMoving))
                    {
                        seen.Add(name);
                    }
                }
                if (s.Stepper.TrapChanges != changes)
                {
                    seen.Add("trap fire");
                }
                if (s.Stepper.TrapChanges != changes && s.State.Y - s.Previous.Y > 0.5f)
                {
                    seen.Add("push-out");
                }
                if (before == LevelPhase.Playing && s.Stepper.Phase == LevelPhase.Dying)
                {
                    seen.Add(s.Stepper.IsTrapPresent(0) && TileCollision.BoxIsFree(s.Level.Grid, s.State.X, s.State.Y, MovementInternals.HitboxHalfWidth, MovementInternals.HitboxHeight) ? "death" : "crush");
                }
                if (before == LevelPhase.Dying && s.Stepper.Phase == LevelPhase.Playing)
                {
                    seen.Add("respawn");
                }
                if (checkpoint < 0 && s.Stepper.ActiveCheckpoint >= 0)
                {
                    seen.Add("checkpoint");
                }
                if (s.Stepper.Phase == LevelPhase.Won)
                {
                    seen.Add("exit");
                }
            }
            return run;
        }

        static LevelRun[] RecordLevels(out int steps)
        {
            var seen = new HashSet<string>();
            LevelRun[] runs =
            {
                // A corridor trap with nowhere to push: trigger, fire, crush, the beat, a respawn.
                RecordLevel(new LevelScenario(AsciiLevel.Rows(12, 8, (x, y) =>
                        y == 0 || (y == 3 && x >= 3 && x <= 7) || ((x == 3 || x == 7) && y >= 1 && y <= 3) ? '#' : x == 4 && y == 1 ? 'P' : '.'))
                        .Trap(4, 1, 3, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(6, 1, 7, 3)),
                    Script((Hold(1), 60), (Idle, 40)), seen),
                // A delayed Appear pushes the standing player up; then a checkpoint and the exit.
                RecordLevel(new LevelScenario(AsciiLevel.Rows(20, 8, (x, y) => y == 0 ? '#' : x == 1 && y == 1 ? 'P' : '.'))
                        .Trap(4, 1, 3, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(2, 1, 2.5f, 3), 90)
                        .Checkpoint(new Box(10, 1, 11, 2))
                        .Exit(new Box(16, 1, 17, 2)),
                    Script((Hold(1), 33), (Idle, 110), (Hold(1), 200), (Press(1), 20)), seen),
            };
            runs = Concat(runs, RecordMovingTraps(seen));
            foreach (string branch in new[] { "trap fire", "push-out", "crush", "respawn", "checkpoint", "exit",
                "move", "carry", "pull-away", "push", "moving crush", "nudge", "fall land", "fall gone", "swept hazard", "exit trap" })
            {
                Assert.That(seen, Does.Contain(branch), $"precondition: the recorded level scripts exercise {branch}");
            }
            steps = 0;
            foreach (LevelRun run in runs)
            {
                steps += run.Inputs.Length;
            }
            return runs;
        }

        static LevelRun[] Concat(LevelRun[] a, LevelRun[] b)
        {
            var all = new LevelRun[a.Length + b.Length];
            a.CopyTo(all, 0);
            b.CopyTo(all, a.Length);
            return all;
        }

        // width x height with a floor on row 0 unless overridden; 'P' parked at (1, 1).
        static string[] Map(int width, int height, Func<int, int, char> cell = null)
        {
            return AsciiLevel.Rows(width, height, (x, y) =>
            {
                char c = cell?.Invoke(x, y) ?? '\0';
                if (c != '\0')
                {
                    return c;
                }
                return y == 0 ? '#' : x == 1 && y == 1 ? 'P' : '.';
            });
        }

        // Phase 4: each run fires its traps with a straight jump into a zone overhead (TrapRig), then plays out.
        static LevelRun[] RecordMovingTraps(HashSet<string> seen)
        {
            const int Delay = 60;
            List<PlayerInput> jumpThenIdle = Script((Press(), 1), (Hold(0, 0, true), 60), (Idle, 140));
            Probe moving = (s, input, was) => s.Stepper.IsTrapMoving(0);
            Probe carry = (s, input, was) => was && s.State.IsStanding && input.MoveX == 0 && s.State.X != s.Previous.X;
            Probe pullAway = (s, input, was) => was && s.Previous.IsStanding && s.State.Contact == PlayerContact.Air
                && s.State.CoyoteTicks == MovementInternals.CoyoteTicks - 1;
            return new[]
            {
                // A sliding floor carries its rider into a wall and slides out from under it.
                RecordLevelAt(new LevelScenario(Map(30, 14, (x, y) => x == 10 && y >= 4 && y <= 7 ? '#' : '\0'))
                        .Trap(5, 3, 3, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(6.5f, 4f), Delay, 8, 0, 4f),
                    6.5f, 4f, jumpThenIdle, seen, ("move", moving), ("carry", carry), ("pull-away", pullAway)),
                // A sinking floor pulls away, then carries the player down after it lands back.
                RecordLevelAt(new LevelScenario(Map(30, 14))
                        .Trap(5, 6, 3, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(6.5f, 7f), Delay, 0, -3, 8f),
                    6.5f, 7f, jumpThenIdle, seen, ("pull-away", pullAway)),
                // A crusher pushes the player against a wall and crushes it; the beat and a respawn follow.
                RecordLevelAt(new LevelScenario(Map(14, 8, (x, y) => x == 2 && y >= 1 ? '#' : '\0'))
                        .Trap(8, 1, 2, 3, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(5f, 1f), Delay, -6, 0, 8f),
                    5f, 1f, Script((Press(), 1), (Hold(0, 0, true), 60), (Hold(1), 80), (Idle, 40)), seen,
                    ("push", (s, input, was) => was && input.MoveX > 0 && s.State.X < s.Previous.X),
                    ("moving crush", (s, input, was) => was && s.Stepper.Phase == LevelPhase.Dying && s.Previous.X == s.State.X && s.Phases.Count > 1
                        && s.Phases[s.Phases.Count - 2] == LevelPhase.Playing)),
                // A falling ceiling clipping the player's edge nudges it clear, then lands.
                RecordLevelAt(new LevelScenario(Map(14, 10))
                        .Trap(4, 7, 3, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(7.175f, 1f), Delay),
                    7.175f, 1f, jumpThenIdle, seen,
                    ("nudge", (s, input, was) => was && MathF.Abs(s.State.X - s.Previous.X - 0.2f) < 1e-3f),
                    ("fall land", (s, input, was) => was && !s.Stepper.IsTrapMoving(0) && s.Stepper.IsTrapPresent(0))),
                // A falling floor lands with its rider.
                RecordLevelAt(new LevelScenario(Map(30, 14))
                        .Trap(5, 8, 3, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(6.5f, 9f), Delay),
                    6.5f, 9f, jumpThenIdle, seen,
                    ("fall land", (s, input, was) => was && !s.Stepper.IsTrapMoving(0) && s.State.IsStanding)),
                // A falling block over a pit leaves the map.
                RecordLevelAt(new LevelScenario(Map(30, 14, (x, y) => y == 0 && x >= 9 && x <= 13 ? '.' : '\0'))
                        .Trap(10, 6, 3, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(4.5f, 1f), Delay),
                    4.5f, 1f, jumpThenIdle, seen,
                    ("fall gone", (s, input, was) => was && !s.Stepper.IsTrapPresent(0))),
                // A fast spike slides into the standing player.
                RecordLevelAt(new LevelScenario(Map(30, 8))
                        .Trap(20, 1, 1, 1, TileKind.Hazard, TrapActionKind.Move, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(8.5f, 1f), Delay, -12, 0, 30f),
                    8.5f, 1f, jumpThenIdle, seen,
                    ("swept hazard", (s, input, was) => was && s.Stepper.Phase == LevelPhase.Dying && s.State.X == s.Previous.X && s.Phases.Count > 1
                        && s.Phases[s.Phases.Count - 2] == LevelPhase.Playing)),
                // A runaway exit, chased and won.
                RecordLevel(new LevelScenario(Map(30, 8)).Exit(new Box(12, 1, 13, 2))
                        .ExitTrap(0, TrapActionKind.Move, TrapTriggerKind.Zone, new Box(8, 1, 9, 3), 0, 6, 0, 8f),
                    Script((Hold(1), 300)), seen,
                    ("exit trap", (s, input, was) => s.Stepper.Phase == LevelPhase.Won && s.Stepper.TrapOffsetX(0) == 6f)),
            };
        }

        static void ReplayLevels(LevelRun[] runs)
        {
            for (int r = 0; r < runs.Length; r++)
            {
                LevelRun run = runs[r];
                run.Stepper.Begin(run.StartX, run.StartY, Abilities.Jump);
                PlayerInput[] inputs = run.Inputs;
                for (int i = 0; i < inputs.Length; i++)
                {
                    run.Stepper.Step(inputs[i]);
                }
            }
        }

        [Test]
        public void LevelStepAllocatesNothing()
        {
            LevelRun[] runs = RecordLevels(out int steps);
            Assert.That(steps, Is.GreaterThanOrEqualTo(400), "at least 400 measured steps");
            ReplayLevels(runs);

            byte[] planted = null;
            Assert.That(() => { planted = new byte[1024]; }, AllocIs.AllocatingGCMemory(), "the probe sees a planted 1 KB allocation");
            GC.KeepAlive(planted);
            Assert.That(() => ReplayLevels(runs), AllocIs.Not.AllocatingGCMemory(), $"LevelStepper.Step or Begin allocated over {steps} ticks");
        }
    }
}
