using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class OneWayTests
    {
        static PlayerInput HeldJump => Hold(0, 0, true);

        [Test]
        public void JumpingUpThroughLandsOnTopOnlyIfTheFeetClearIt()
        {
            // One-way in row 3, top at 4: the apex (feet at 4.5) clears it.
            var p = new PlayerScenario(Abilities.Jump,
                "......",
                "......",
                "......",
                "......",
                "......",
                "......",
                "------",
                "......",
                ".P....",
                "######");
            p.Tick(Press());
            int landed = p.RunUntil(HeldJump, s => s.State.IsStanding, 200);
            Assert.That(landed, Is.GreaterThan(0));
            Assert.AreEqual(PlayerContact.OneWayTop, p.State.Contact);
            Assert.AreEqual(4f, p.State.Y);
        }

        [Test]
        public void JumpThatLeavesTheFeetInsideFallsBackThrough()
        {
            // One-way in row 4 (4 to 5): the apex (feet at 4.5) leaves the feet inside the platform.
            var p = new PlayerScenario(Abilities.Jump,
                "......",
                "......",
                "......",
                "......",
                "......",
                "------",
                "......",
                "......",
                ".P....",
                "######");
            p.Tick(Press());
            p.RunUntil(HeldJump, s => s.State.IsStanding, 200);
            Assert.AreEqual(4.5f, p.MaxY, 0.01f);
            Assert.AreEqual(PlayerContact.Ground, p.State.Contact, "fell back to the floor");
            Assert.AreEqual(1f, p.State.Y);
            Assert.IsFalse(p.ContactsSeen.Contains(PlayerContact.OneWayTop), "never snapped up onto it");
        }

        [Test]
        public void SidesAreNeverWalls()
        {
            var p = new PlayerScenario(Abilities.Jump,
                "..........",
                "..........",
                "..........",
                ".....-....",
                ".P...-....",
                "##########");
            p.Tick(Hold(1), 90);
            Assert.That(p.State.X, Is.GreaterThan(7f), "ran through the one-way's side");

            p = new PlayerScenario(Abilities.Jump | Abilities.WallJump,
                "..........",
                "...-......",
                "...-......",
                "...-......",
                "...-......",
                "...-P.....",
                "...-......",
                "...-......",
                "...-......",
                "##########");
            p.Tick(Hold(-1), 40);
            Assert.IsFalse(p.ContactsSeen.Contains(PlayerContact.WallSlide), "no wall slide on a one-way column");
            Assert.That(p.State.X, Is.LessThan(3f), "passed through the column");
        }

        [Test]
        public void StraddlingOneWayAndSolidDownJumpJumps()
        {
            var p = new PlayerScenario(Abilities.Jump,
                "......",
                "......",
                "......",
                "......",
                "..P...",
                "--##--");
            p.Tick(Hold(-1), 6);
            p.Tick(Idle, 4);
            Assert.AreEqual(2f, p.State.X, 1e-3f, "walked onto the one-way/solid seam");
            Assert.AreEqual(PlayerContact.Ground, p.State.Contact, "straddling: solid wins");
            p.Tick(Press(0, -1));
            Assert.That(p.State.VelocityY, Is.GreaterThan(10f), "Down + jump is a normal jump");
        }

        [TestCase(Abilities.Jump)]
        [TestCase(Abilities.None)]
        public void DownJumpDropsThroughAndConsumesThePress(Abilities abilities)
        {
            var p = new PlayerScenario(abilities,
                "......",
                "......",
                "......",
                "..P...",
                "------",
                "......",
                "......",
                "......",
                "######");
            p.Tick(Idle);
            Assert.AreEqual(PlayerContact.OneWayTop, p.State.Contact);
            p.Tick(Press(0, -1));
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "dropped");
            Assert.That(p.State.Y, Is.LessThan(5f));
            Assert.AreEqual(0, p.State.CoyoteTicks, "no coyote after a drop");
            Assert.AreEqual(0, p.State.BufferTicks, "the press is consumed, not buffered");
            p.ResetRecord();
            int landed = p.RunUntil(Hold(0, -1, true), s => s.State.IsStanding, 200);
            Assert.That(landed, Is.GreaterThan(0));
            Assert.AreEqual(1f, p.State.Y, "fell to the floor");
            Assert.That(p.MaxY, Is.LessThan(5f), "no jump after leaving the platform");
            p.Tick(HeldJump, 10);
            Assert.IsTrue(p.State.IsStanding, "and none on landing");
        }

        [Test]
        public void DroppingOntoALowerOneWayWhileHoldingDownLandsOnIt()
        {
            var p = new PlayerScenario(Abilities.Jump,
                "......",
                "......",
                "......",
                "..P...",
                "------",
                "......",
                "......",
                "------",
                "......",
                "######");
            p.Tick(Idle);
            p.Tick(Press(0, -1));
            p.RunUntil(Hold(0, -1), s => s.State.IsStanding, 200);
            Assert.AreEqual(PlayerContact.OneWayTop, p.State.Contact);
            Assert.AreEqual(3f, p.State.Y);
        }

        [Test]
        public void BufferedPressLandingOnAOneWayWithDownHeldJumps()
        {
            var p = new PlayerScenario(Abilities.Jump,
                "......",
                "..P...",
                "......",
                "......",
                "......",
                "------",
                "......",
                "######");
            int landing = new PlayerScenario(Abilities.Jump,
                "......",
                "..P...",
                "......",
                "......",
                "......",
                "------",
                "......",
                "######").RunUntil(Idle, s => s.State.IsStanding, 200);
            p.Tick(Hold(0, -1), landing - 3);
            p.Tick(Press(0, -1));
            p.Tick(Hold(0, -1, true), 2);
            Assert.AreEqual(PlayerContact.OneWayTop, p.State.Contact, "landed on the one-way");
            p.Tick(Hold(0, -1, true));
            Assert.That(p.State.VelocityY, Is.GreaterThan(10f), "the buffered press jumps, it does not drop");
        }

        [Test]
        public void DownAloneDoesNothing()
        {
            var p = new PlayerScenario(Abilities.Jump,
                "......",
                "......",
                "......",
                "..P...",
                "------",
                "......",
                "######");
            p.Tick(Hold(0, -1), 30);
            Assert.AreEqual(PlayerContact.OneWayTop, p.State.Contact);
            Assert.AreEqual(3f, p.State.Y);
        }
    }
}
