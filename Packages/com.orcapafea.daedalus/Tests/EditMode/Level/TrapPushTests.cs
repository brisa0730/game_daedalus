using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>A Solid trap that appears on the player pushes it out the shortest free way, or crushes it.</summary>
    public class TrapPushTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float H = MovementInternals.HitboxHeight;
        const float Tolerance = 1e-4f;

        static string[] Open(int height)
        {
            return AsciiLevel.Rows(14, height, (x, y) => y == 0 ? '#' : x == 5 && y == 1 ? 'P' : '.');
        }

        // Jumps straight up under a hidden block (Solid, Appear, Touch) in row blockRow; returns the scenario on the fire tick.
        static LevelScenario JumpIntoHiddenBlock(int blockRow)
        {
            LevelScenario s = new LevelScenario(Open(12)).Trap(5, blockRow, 1, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Touch).Begin();
            s.Tick(Idle, 5);
            s.Tick(Press());
            int fire = s.RunUntil(Hold(0, 0, true), x => x.Stepper.IsTrapPresent(0), 60);
            Assert.That(fire, Is.GreaterThan(0), "the block appeared");
            return s;
        }

        [Test]
        public void AHiddenBlockAppearingAroundTheHeadPushesItDown()
        {
            LevelScenario s = JumpIntoHiddenBlock(5);
            Assert.That(s.Previous.Y + H + LevelRules.ContactMargin, Is.LessThanOrEqualTo(5f), "precondition: not touching the tick before");
            // Without a push the head would still be rising, at most at the block's bottom; zero speed proves the push.
            Assert.AreEqual(0f, s.State.VelocityY, "pushed down: the upward speed is gone (a head bump)");
            Assert.AreEqual(5f, s.State.Y + H, Tolerance, "the head is just under the block");
            Assert.AreEqual(PlayerContact.Air, s.State.Contact);
        }

        [Test]
        public void AHiddenBlockAppearingJustAboveTheHeadDoesNotPushTheSweepBonks()
        {
            LevelScenario s = JumpIntoHiddenBlock(4);
            float head = s.State.Y + H;
            Assert.That(head, Is.LessThanOrEqualTo(4f + Tolerance), "precondition: the head was just below the block on the fire tick");
            Assert.That(s.State.VelocityY, Is.GreaterThan(0f), "no push: still rising");
            s.Tick(Hold(0, 0, true));
            Assert.AreEqual(4f, s.State.Y + H, Tolerance, "the ceiling sweep stops the head at the block");
            Assert.That(s.State.VelocityY, Is.LessThanOrEqualTo(0f), "bonked");
        }

        // Walks right from the start, stops near stopX and waits; the trap fires on its delay while the player stands.
        static LevelScenario StandInside(string[] rows, int x, int y, int w, int h, float stopX, int delay = 90)
        {
            LevelScenario s = new LevelScenario(rows)
                .Trap(x, y, w, h, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(2, 1, 2.5f, 3), delay)
                .BeginAt(1.5f, 1f);
            s.RunUntil(Hold(1), v => v.State.X >= stopX);
            int fire = s.RunUntil(Idle, v => v.Stepper.IsTrapPresent(0), 200);
            Assert.That(fire, Is.GreaterThan(0), "the trap appeared");
            Assert.IsTrue(s.Previous.IsStanding, "precondition: standing when it appeared");
            Assert.IsTrue(Box.Hitbox(s.Previous.X, s.Previous.Y).Overlaps(new Box(x, y, x + w, y + h)), "precondition: inside its box");
            return s;
        }

        [Test]
        public void AnAppearAroundAStandingPlayerWithRoomAbovePushesItUpOntoTheTrap()
        {
            LevelScenario s = StandInside(Open(8), 4, 1, 3, 1, 5f);
            Assert.AreEqual(2f, s.State.Y, Tolerance, "on top of the trap");
            Assert.AreEqual(s.Previous.X, s.State.X, Tolerance, "straight up");
            s.Tick(Idle, 10);
            Assert.IsTrue(s.State.IsStanding, "stands on it");
            Assert.AreEqual(2f, s.State.Y, Tolerance);
        }

        [Test]
        public void AWallOnTheNearSideSendsThePlayerTheOtherWay()
        {
            // A corridor: a ceiling at row 3 blocks the way up, the wall at column 3 the near (left) way.
            string[] rows = AsciiLevel.Rows(14, 8, (x, y) =>
                y == 0 || (y == 3 && x >= 3 && x <= 9) || (x == 3 && y >= 1 && y <= 2) ? '#' : x == 8 && y == 1 ? 'P' : '.');
            LevelScenario s = new LevelScenario(rows)
                .Trap(4, 1, 3, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(4, 1, 4.5f, 3))
                .Begin();
            int fire = s.RunUntil(Hold(-1), v => v.Stepper.IsTrapPresent(0), 200);
            Assert.That(fire, Is.GreaterThan(0), "walked left into the zone");
            Assert.That(s.Previous.X - 4f, Is.LessThan(7f - s.Previous.X), "precondition: nearer the left side");
            Assert.AreEqual(7f + Hw, s.State.X, Tolerance, "pushed right, away from the wall");
            Assert.AreEqual(1f, s.State.Y, Tolerance);
            Assert.AreEqual(0, s.Stepper.Deaths);
        }

        [Test]
        public void InASealedCorridorTheAppearCrushes()
        {
            // Walls at columns 3 and 7, a ceiling at row 3: no way out of the trap's box.
            string[] rows = AsciiLevel.Rows(12, 8, (x, y) =>
                y == 0 || (y == 3 && x >= 3 && x <= 7) || ((x == 3 || x == 7) && y >= 1 && y <= 3) ? '#' : x == 4 && y == 1 ? 'P' : '.');
            LevelScenario s = new LevelScenario(rows)
                .Trap(4, 1, 3, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(6, 1, 7, 3))
                .Begin();
            Assert.AreEqual(4.5f, s.State.X, "precondition: in the corridor, outside the zone");
            int death = s.RunUntil(Hold(1), v => v.Stepper.Phase == LevelPhase.Dying, 120);
            Assert.That(death, Is.GreaterThan(0), "crushed");
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "the trap appeared");
            Assert.AreEqual(1, s.Stepper.Deaths);
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "the beat, then a respawn");
            Assert.IsFalse(s.Stepper.IsTrapPresent(0), "reset");
        }

        [Test]
        public void APushLetsGoOfARope()
        {
            string[] rows = AsciiLevel.Rows(12, 12, (x, y) => y == 0 ? '#' : x == 5 && y >= 1 && y <= 9 ? 'R' : x == 1 && y == 1 ? 'P' : '.');
            LevelScenario s = new LevelScenario(rows)
                .WithAbilities(Abilities.Jump | Abilities.RopeClimb)
                .Trap(4, 2, 3, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(4, 4, 7, 5))
                .BeginAt(5.5f, 1f);
            int fire = s.RunUntil(Hold(0, 1), v => v.Stepper.IsTrapPresent(0), 200);
            Assert.That(fire, Is.GreaterThan(0), "climbed into the zone");
            Assert.AreEqual(PlayerContact.Rope, s.Previous.Contact, "precondition: on the rope");
            Assert.AreEqual(PlayerContact.Air, s.State.Contact, "let go");
            Assert.AreEqual(-1, s.State.RopeIndex);
            Assert.AreEqual(3f, s.State.Y, Tolerance, "pushed up onto the trap");
        }

        [Test]
        public void AStartInsideAPresentSolidTrapIsPlacedOutsideIt()
        {
            LevelScenario s = new LevelScenario(Open(8)).Trap(4, 1, 3, 2, TileKind.Solid, TrapActionKind.Vanish).BeginAt(5.5f, 1f);
            Assert.IsTrue(s.Stepper.LastPlacement.Moved);
            Assert.IsTrue(s.Stepper.LastPlacement.Found);
            Assert.IsFalse(Box.Hitbox(s.State.X, s.State.Y).Overlaps(new Box(4, 1, 7, 3)), "outside the trap");
        }
    }
}
