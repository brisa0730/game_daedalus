using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class JumpTests
    {
        const float Tolerance = 1e-3f;

        static readonly string[] Floor =
        {
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            ".P........",
            "##########",
        };

        // A ledge over a deep pit: the player walks right off it.
        static readonly string[] Ledge =
        {
            "..............",
            "..............",
            "..............",
            "..............",
            "..............",
            ".P............",
            "#####.........",
            "..............",
            "..............",
            "..............",
            "..............",
            "..............",
            "..............",
            "..............",
            "..............",
        };

        // The player starts in the air and falls onto the floor.
        static readonly string[] Drop =
        {
            "..........",
            "..........",
            "....P.....",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "..........",
            "##########",
        };

        // Jump velocity after the jump tick's own gravity: 20 − 57.142857 / 60.
        const float AfterJumpTick = 19.047619f;

        static PlayerInput HeldJump => Hold(0, 0, true);

        [TestCase(1f, 21)]
        [TestCase(2f, 15)]
        public void FullJumpPeaksAtJumpHeightAtAnyGravity(float gravity, int ticksToApex)
        {
            MovementSettings s = MovementSettings.Default;
            s.Gravity = gravity;
            var p = new PlayerScenario(Abilities.Jump, s, Floor);
            p.Tick(Press());
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "the jump fired");
            p.RunUntil(HeldJump, x => x.State.IsStanding, 200);
            TestContext.WriteLine($"gravity {gravity}: apex {p.MaxY - 1f:F4} on tick {p.MaxYTick}");
            Assert.AreEqual(3.5f, p.MaxY - 1f, 0.01f);
            Assert.That(p.MaxYTick, Is.InRange(ticksToApex - 1, ticksToApex + 1));
        }

        [Test]
        public void OneTickTapIsAMinimumHop()
        {
            var p = new PlayerScenario(Abilities.Jump, Floor);
            p.Tick(Press(0, 0, false));
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "the jump fired");
            p.RunUntil(Idle, x => x.State.IsStanding, 200);
            TestContext.WriteLine($"tap apex {p.MaxY - 1f:F4}");
            Assert.AreEqual(1.25f, p.MaxY - 1f, 0.01f);
        }

        [Test]
        public void ReleasingMidRiseGivesAHeightBetweenHopAndFullMonotonically()
        {
            float previous = 0;
            for (int release = 1; release <= 22; release++)
            {
                var p = new PlayerScenario(Abilities.Jump, Floor);
                p.Tick(Press());
                p.Tick(HeldJump, release - 1);
                p.RunUntil(Idle, x => x.State.IsStanding, 200);
                float apex = p.MaxY - 1f;
                Assert.That(apex, Is.InRange(1.25f - 0.01f, 3.5f + 0.01f), $"release after {release} ticks");
                Assert.That(apex, Is.GreaterThanOrEqualTo(previous - 1e-5f), $"release after {release} ticks");
                previous = apex;
            }
            Assert.AreEqual(3.5f, previous, 0.01f, "a late release is a full jump");
        }

        [TestCase(6, true)]
        [TestCase(7, false)]
        public void CoyoteJumpWorksForSixTicksAfterWalkingOff(int pressTick, bool jumps)
        {
            Abilities abilities = jumps ? Abilities.Jump | Abilities.DoubleJump : Abilities.Jump;
            var p = new PlayerScenario(abilities, Ledge);
            int w = p.RunUntil(Hold(1), s => s.State.Contact == PlayerContact.Air);
            Assert.That(w, Is.GreaterThan(0), "walked off on tick W");
            p.Tick(Hold(1), pressTick - 1);
            p.Tick(Press(1));
            if (jumps)
            {
                Assert.That(p.State.VelocityY, Is.GreaterThan(10f), $"a press on W+{pressTick} jumps");
                Assert.AreEqual(1, p.State.AirJumpsLeft, "a coyote jump spends no air jump");
                Assert.AreEqual(0, p.State.CoyoteTicks, "the jump clears the window");
            }
            else
            {
                Assert.That(p.State.VelocityY, Is.LessThan(0f), $"a press on W+{pressTick} does not jump");
            }
        }

        [Test]
        public void NoCoyoteAfterAJump()
        {
            var p = new PlayerScenario(Abilities.Jump, Floor);
            p.Tick(Press());
            p.RunUntil(HeldJump, s => s.State.VelocityY < 0, 60);
            p.Tick(Press());
            Assert.That(p.State.VelocityY, Is.LessThan(0f), "pressing on the descent does nothing");
            Assert.AreEqual(0, p.State.CoyoteTicks);
        }

        static int LandingTick()
        {
            var dry = new PlayerScenario(Abilities.Jump, Drop);
            int landing = dry.RunUntil(Idle, s => s.State.IsStanding, 200);
            Assert.That(landing, Is.GreaterThan(10), "the dry run lands");
            return landing;
        }

        [TestCase(5, true)]
        [TestCase(6, false)]
        public void BufferedPressJumpsOnTheFirstStandingTick(int ticksBeforeLanding, bool jumps)
        {
            int landing = LandingTick();
            int press = landing - ticksBeforeLanding;
            var p = new PlayerScenario(Abilities.Jump, Drop);
            p.Tick(HeldJump, press - 1);
            p.Tick(Press());
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "the press was in the air");
            Assert.AreEqual(MovementInternals.JumpBufferTicks, p.State.BufferTicks, "and was buffered");
            p.Tick(HeldJump, landing - press);
            Assert.IsTrue(p.State.IsStanding, "landed on tick L");
            p.Tick(HeldJump);
            if (jumps)
            {
                Assert.That(p.State.VelocityY, Is.GreaterThan(10f), $"a press on L-{ticksBeforeLanding} jumps on L+1");
            }
            else
            {
                Assert.IsTrue(p.State.IsStanding, $"a press on L-{ticksBeforeLanding} does not jump");
                p.Tick(HeldJump, 10);
                Assert.IsTrue(p.State.IsStanding, "and never does");
            }
        }

        [Test]
        public void BufferedPressReleasedBeforeLandingIsAMinimumHop()
        {
            int landing = LandingTick();
            var p = new PlayerScenario(Abilities.Jump, Drop);
            p.Tick(Idle, landing - 4);
            p.Tick(Press(0, 0, false));
            p.Tick(Idle, 3);
            Assert.IsTrue(p.State.IsStanding);
            float floor = p.State.Y;
            p.ResetRecord();
            p.Tick(Idle);
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "the buffered jump fired");
            p.RunUntil(Idle, s => s.State.IsStanding, 200);
            Assert.AreEqual(1.25f, p.MaxY - floor, 0.01f);
        }

        [TestCase(1, false)]
        [TestCase(2, true)]
        public void AirJumpsAreLimitedByAirJumps(int airJumps, bool secondFires)
        {
            MovementSettings s = MovementSettings.Default;
            s.AirJumps = airJumps;
            var p = new PlayerScenario(Abilities.Jump | Abilities.DoubleJump, s, Floor);
            p.Tick(Press());
            p.RunUntil(HeldJump, x => x.State.VelocityY <= 0, 60);
            p.Tick(Press());
            Assert.AreEqual(AfterJumpTick, p.State.VelocityY, Tolerance, "the first air jump fires at full speed");
            Assert.AreEqual(airJumps - 1, p.State.AirJumpsLeft);
            p.RunUntil(HeldJump, x => x.State.VelocityY <= 0, 60);
            p.Tick(Press());
            if (secondFires)
            {
                Assert.AreEqual(AfterJumpTick, p.State.VelocityY, Tolerance, "a second air jump");
                Assert.AreEqual(0, p.State.AirJumpsLeft);
            }
            else
            {
                Assert.That(p.State.VelocityY, Is.LessThan(0f), "no second air jump");
            }

            p.RunUntil(HeldJump, x => x.State.IsStanding, 400);
            Assert.AreEqual(airJumps, p.State.AirJumpsLeft, "landing refills the air jumps");
        }

        [Test]
        public void AirJumpFromTheApexDoublesTheHeight()
        {
            var p = new PlayerScenario(Abilities.Jump | Abilities.DoubleJump, Floor);
            p.Tick(Press());
            p.RunUntil(HeldJump, x => x.State.VelocityY <= 0, 60);
            p.Tick(Press());
            p.RunUntil(HeldJump, x => x.State.IsStanding, 400);
            TestContext.WriteLine($"double-jump apex {p.MaxY - 1f:F4}");
            Assert.AreEqual(7f, p.MaxY - 1f, 0.02f);
        }

        [Test]
        public void WithJumpOffThereAreNoGroundOrCoyoteJumps()
        {
            var p = new PlayerScenario(Abilities.None, Floor);
            p.Tick(Idle);
            p.Tick(Press());
            p.Tick(HeldJump, 10);
            Assert.IsTrue(p.State.IsStanding, "no ground jump");
            Assert.AreEqual(1f, p.MaxY, "never left the floor");

            p = new PlayerScenario(Abilities.None, Ledge);
            p.RunUntil(Hold(1), s => s.State.Contact == PlayerContact.Air);
            p.Tick(Press(1));
            Assert.That(p.State.VelocityY, Is.LessThan(0f), "no coyote jump");

            p = new PlayerScenario(Abilities.DoubleJump, Ledge);
            p.RunUntil(Hold(1), s => s.State.Contact == PlayerContact.Air);
            p.Tick(Press(1));
            Assert.AreEqual(AfterJumpTick, p.State.VelocityY, Tolerance, "with double jump the press is an air jump");
            Assert.AreEqual(0, p.State.AirJumpsLeft);
        }
    }
}
