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

        static LevelRun RecordLevel(LevelScenario s, List<PlayerInput> inputs, HashSet<string> seen)
        {
            s.Begin();
            var run = new LevelRun { Stepper = s.Stepper, StartX = s.Level.StartX, StartY = s.Level.StartY, Inputs = inputs.ToArray() };
            foreach (PlayerInput input in run.Inputs)
            {
                LevelPhase before = s.Stepper.Phase;
                int changes = s.Stepper.TrapChanges;
                int checkpoint = s.Stepper.ActiveCheckpoint;
                s.Tick(input);
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
            foreach (string branch in new[] { "trap fire", "push-out", "crush", "respawn", "checkpoint", "exit" })
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
