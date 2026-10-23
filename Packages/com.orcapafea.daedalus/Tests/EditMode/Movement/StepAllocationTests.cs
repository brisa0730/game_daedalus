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
                if (p.Previous.Y < 0 && s.Y >= 0)
                {
                    seen.Add("fall-out reset");
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
                // Run, jump, air jump, land; then walk off the ledge and fall out (reset).
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

            foreach (string branch in new[] { "Ground", "Air", "OneWayTop", "WallSlide", "WallCling", "Rope", "wall jump", "air jump", "rope jump", "cling jump", "ledge pop", "drop", "fall-out reset" })
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
    }
}
