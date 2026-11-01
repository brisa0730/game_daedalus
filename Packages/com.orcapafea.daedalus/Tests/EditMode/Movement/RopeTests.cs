using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class RopeTests
    {
        const float Tolerance = 1e-3f;
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float Grip = MovementInternals.GripHeight;
        const Abilities Climber = Abilities.Jump | Abilities.RopeClimb;

        // A floor and a rope in column 5 (centre x = 5.5) from row bottom to row top − 1; 30 tall.
        // The 'P' marker sits away from the rope; tests place the player with CreateState.
        static string[] RopeMap(int bottom, int top, bool floor)
        {
            return AsciiLevel.Rows(12, 30, (x, y) =>
                y == 0 && floor ? '#' : x == 5 && y >= bottom && y < top ? 'R' : x == 2 && y == 20 ? 'P' : '.');
        }

        static PlayerScenario FallingThroughTheRope(Abilities abilities, int bottom = 1, int top = 25, bool floor = true)
        {
            var p = new PlayerScenario(abilities, RopeMap(bottom, top, floor));
            p.State = p.Stepper.CreateState(5.3f, 20f, abilities);
            return p;
        }

        static void Grab(PlayerScenario p, int moveY = 1)
        {
            p.Tick(Idle, 2);
            p.Tick(Hold(0, moveY));
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact, "grabbed the rope");
        }

        [Test]
        public void GrabsOnlyWithUpOrDownAndSnapsToTheCentre()
        {
            PlayerScenario p = FallingThroughTheRope(Climber);
            p.Tick(Idle, 20);
            Assert.IsFalse(p.ContactsSeen.Contains(PlayerContact.Rope), "overlapping alone does not grab");

            p = FallingThroughTheRope(Climber);
            Grab(p, 1);
            Assert.AreEqual(5.5f, p.State.X, "snapped to the rope's centre");
            Assert.AreEqual(0, p.State.RopeIndex);

            p = FallingThroughTheRope(Climber);
            Grab(p, -1);
        }

        [Test]
        public void WithoutRopeClimbARopeIsPassive()
        {
            PlayerScenario p = FallingThroughTheRope(Abilities.Jump);
            p.Tick(Hold(0, 1), 60);
            p.Tick(Hold(0, -1), 60);
            Assert.IsFalse(p.ContactsSeen.Contains(PlayerContact.Rope));
            Assert.IsTrue(p.State.IsStanding, "fell past it to the floor");
        }

        [Test]
        public void ClimbsAtClimbSpeedAndIgnoresHorizontalInput()
        {
            PlayerScenario p = FallingThroughTheRope(Climber);
            Grab(p);
            float y = p.State.Y;
            p.Tick(Hold(1, 1), 20);
            Assert.AreEqual(y + 1f, p.State.Y, Tolerance, "up at 3 tiles/s");
            Assert.AreEqual(5.5f, p.State.X, "horizontal input ignored");
            p.Tick(Hold(-1, -1), 20);
            Assert.AreEqual(y, p.State.Y, Tolerance, "down at 3 tiles/s");
            p.Tick(Idle, 20);
            Assert.AreEqual(y, p.State.Y, Tolerance, "neutral holds");
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact);
        }

        [Test]
        public void TopWithoutASurfaceStopsAndTheBottomLetsGo()
        {
            PlayerScenario p = FallingThroughTheRope(Climber, 1, 23);
            Grab(p);
            p.Tick(Hold(0, 1), 200);
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact);
            Assert.AreEqual(23f, p.State.Y + Grip, Tolerance, "grip stops at the rope's top");

            p = FallingThroughTheRope(Climber, 15, 25, false);
            Grab(p);
            int released = p.RunUntil(Hold(0, -1), s => s.State.Contact != PlayerContact.Rope, 300);
            Assert.That(released, Is.GreaterThan(0));
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "off the bottom: falls");
            Assert.AreEqual(0, p.State.CoyoteTicks, "no rope coyote");
            Assert.That(p.State.Y + Grip, Is.LessThan(15f).And.GreaterThan(14.9f));

            p = FallingThroughTheRope(Climber);
            Grab(p);
            int landed = p.RunUntil(Hold(0, -1), s => s.State.IsStanding, 400);
            Assert.That(landed, Is.GreaterThan(0), "climbing down onto ground lands");
            Assert.AreEqual(1f, p.State.Y);
        }

        [Test]
        public void RopeThroughAOneWayStepsOntoItAndDownGrabsFromAbove()
        {
            // Rope in column 5 from row 1 to the one-way in row 8 (its top, 9, is the rope's top).
            string[] rows = AsciiLevel.Rows(12, 20, (x, y) =>
                y == 0 ? '#' : y == 8 && x >= 3 && x <= 7 ? (x == 5 ? '+' : '-') : x == 5 && y >= 1 && y < 8 ? 'R' : x == 2 && y == 1 ? 'P' : '.');
            var p = new PlayerScenario(Climber, rows);
            Assert.AreEqual(9f, p.Level.Ropes[0].Top, "the rope reaches the one-way's top");
            p.State = p.Stepper.CreateState(5.5f, 1f, Climber);
            p.Tick(Idle);
            p.Tick(Hold(0, 1));
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact, "Up grabs from the ground");
            int stood = p.RunUntil(Hold(0, 1), s => s.State.IsStanding, 300);
            Assert.That(stood, Is.GreaterThan(0));
            Assert.AreEqual(PlayerContact.OneWayTop, p.State.Contact, "climbing up steps onto the platform");
            Assert.AreEqual(9f, p.State.Y);

            p.Tick(Hold(0, -1));
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact, "Down on the platform grabs the rope");
            p.Tick(Hold(0, -1), 30);
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact);
            Assert.AreEqual(9f - 31 * 0.05f, p.State.Y, Tolerance, "descending through the platform at 3 tiles/s");
        }

        [Test]
        public void RopeJumpIsAFullJumpAndSpendsNoAirJump()
        {
            PlayerScenario p = FallingThroughTheRope(Climber | Abilities.DoubleJump);
            Grab(p);
            float y = p.State.Y;
            p.ResetRecord();
            p.Tick(Press(1));
            Assert.AreEqual(PlayerContact.Air, p.State.Contact);
            Assert.AreEqual(6f, p.State.VelocityX, Tolerance, "horizontal speed = input × run speed");
            p.RunUntil(Hold(1, 0, true), s => s.State.VelocityY <= 0, 60);
            Assert.AreEqual(y + 3.5f, p.MaxY, 0.01f, "full jump height");
            Assert.AreEqual(1, p.State.AirJumpsLeft, "no air jump spent");
        }

        [Test]
        public void RegrabIsLockedOutForTwelveTicks()
        {
            PlayerScenario p = FallingThroughTheRope(Climber);
            Grab(p);
            p.Tick(Press(0, 1));
            Assert.AreEqual(MovementInternals.RopeLockoutTicks, p.State.RopeLockoutTicks, "rope jump on J");
            for (int i = 1; i <= 12; i++)
            {
                p.Tick(Hold(0, 1, true));
                Assert.AreEqual(PlayerContact.Air, p.State.Contact, $"J+{i}: no re-grab");
            }
            p.Tick(Hold(0, 1, true));
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact, "J+13: re-grab");
        }

        [Test]
        public void NoGrabWhenTheSnappedBoxIsBlocked()
        {
            TileGrid grid = AsciiLevel.ParseGrid(AsciiLevel.Rows(12, 30, (x, y) => y == 0 || (x == 4 && y > 0) ? '#' : '.'));
            var stepper = new PlayerStepper(grid, new[] { new RopeSegment(5.1f, 1f, 25f) }, MovementSettings.Default);
            PlayerState s = stepper.CreateState(5.4f, 20f, Climber);
            Assert.IsTrue(TileCollision.BoxIsFree(grid, s.X, s.Y, Hw, MovementInternals.HitboxHeight), "the player itself is free");
            s = stepper.Step(s, Idle);
            s = stepper.Step(s, Hold(0, 1));
            Assert.AreEqual(PlayerContact.Air, s.Contact, "snapping into the wall is refused");
            Assert.AreEqual(5.4f, s.X, Tolerance);
        }

        [Test]
        public void OverlappingRopesTheLowerIndexWins()
        {
            TileGrid grid = AsciiLevel.ParseGrid(AsciiLevel.Rows(12, 30, (x, y) => y == 0 ? '#' : '.'));
            var stepper = new PlayerStepper(grid, new[] { new RopeSegment(5.6f, 1f, 25f), new RopeSegment(5.5f, 1f, 25f) }, MovementSettings.Default);
            PlayerState s = stepper.CreateState(5.5f, 20f, Climber);
            s = stepper.Step(s, Hold(0, 1));
            Assert.AreEqual(PlayerContact.Rope, s.Contact);
            Assert.AreEqual(0, s.RopeIndex);
            Assert.AreEqual(5.6f, s.X);
        }

        [Test]
        public void GrabRefillsAirJumps()
        {
            PlayerScenario p = FallingThroughTheRope(Climber | Abilities.DoubleJump);
            p.Tick(Press());
            Assert.AreEqual(0, p.State.AirJumpsLeft);
            p.RunUntil(Hold(0, 1, true), s => s.State.Contact == PlayerContact.Rope, 60);
            Assert.AreEqual(1, p.State.AirJumpsLeft);
        }

        [Test]
        public void OnARopeNextToAWallAPressIsARopeJump()
        {
            const Abilities abilities = Climber | Abilities.WallJump;
            var p = new PlayerScenario(abilities, AsciiLevel.Rows(12, 30, (x, y) =>
                y == 0 || x == 6 ? '#' : x == 5 && y >= 1 && y < 25 ? 'R' : x == 2 && y == 20 ? 'P' : '.'));
            p.State = p.Stepper.CreateState(5.5f, 20f, abilities);
            Grab(p);
            Assert.AreEqual(0.125f, WallJumpTests.GapToWall(p, p.State, 1), Tolerance, "a wall is in wall-jump reach");
            p.Tick(Press(-1));
            Assert.AreEqual(-6f, p.State.VelocityX, Tolerance, "rope jump: input × run speed, no push-off");
            Assert.AreEqual(0, p.State.WallLockTicks);
        }

        // Holding Down on the platform grabs the rope first; the jump press then lets go instead of rope-jumping.
        [Test]
        public void HoldingDownThenJumpingOnARopesPlatformLetsGoWithoutAJump()
        {
            string[] rows = AsciiLevel.Rows(12, 20, (x, y) =>
                y == 0 ? '#' : y == 8 && x >= 3 && x <= 7 ? (x == 5 ? '+' : '-') : x == 5 && y >= 1 && y < 8 ? 'R' : x == 5 && y == 9 ? 'P' : '.');
            var p = new PlayerScenario(Climber, rows);
            p.Tick(Idle);
            p.Tick(Hold(0, -1));
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact, "precondition: Down grabbed the rope from the platform");
            p.Tick(Press(0, -1));
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "let go");
            Assert.That(p.State.VelocityY, Is.LessThanOrEqualTo(0f), "no rope jump");
            Assert.AreEqual(MovementInternals.RopeLockoutTicks, p.State.RopeLockoutTicks);
            p.ResetRecord();
            int landed = p.RunUntil(Hold(0, -1, true), s => s.State.IsStanding, 300);
            Assert.That(landed, Is.GreaterThan(0));
            Assert.AreEqual(1f, p.State.Y, "fell through the platform to the floor");
            Assert.That(p.MaxY, Is.LessThan(9f));
        }

        [Test]
        public void ClimbingDownOntoAOneWayUnderTheRopeLandsOnIt()
        {
            string[] rows = AsciiLevel.Rows(12, 20, (x, y) =>
                y == 0 ? '#' : y == 2 && x >= 3 && x <= 7 ? '-' : x == 5 && y >= 3 && y < 12 ? 'R' : x == 2 && y == 1 ? 'P' : '.');
            var p = new PlayerScenario(Climber, rows);
            p.State = p.Stepper.CreateState(5.5f, 8f, Climber);
            p.Tick(Hold(0, 1));
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact);
            int landed = p.RunUntil(Hold(0, -1), s => s.State.Contact != PlayerContact.Rope, 300);
            Assert.That(landed, Is.GreaterThan(0));
            Assert.AreEqual(PlayerContact.OneWayTop, p.State.Contact, "a one-way under the rope is ground");
            Assert.AreEqual(3f, p.State.Y);
        }

        [Test]
        public void DownJumpOnARopesPlatformDropsInsteadOfGrabbing()
        {
            string[] rows = AsciiLevel.Rows(12, 20, (x, y) =>
                y == 0 ? '#' : y == 8 && x >= 3 && x <= 7 ? (x == 5 ? '+' : '-') : x == 5 && y >= 1 && y < 8 ? 'R' : x == 5 && y == 9 ? 'P' : '.');
            var p = new PlayerScenario(Climber, rows);
            p.Tick(Idle);
            Assert.AreEqual(PlayerContact.OneWayTop, p.State.Contact);
            p.Tick(Press(0, -1));
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "drop-through, not a grab");
            Assert.AreEqual(-1, p.State.RopeIndex);
            Assert.That(p.State.Y, Is.LessThan(9f));
        }

        // A floor and one-way cells in columns 4–6 of the given rows; 12 wide, 30 tall.
        static TileGrid OneWayAt(params int[] rows)
        {
            return AsciiLevel.ParseGrid(AsciiLevel.Rows(12, 30, (x, y) =>
                y == 0 ? '#' : x >= 4 && x <= 6 && System.Array.IndexOf(rows, y) >= 0 ? '-' : '.'));
        }

        [Test]
        public void ARopeTopJustBelowAOneWaySnapsUpAndThePlayerStepsOnto()
        {
            TileGrid grid = OneWayAt(10);
            var rope = new RopeSegment(5.5f, 1f, 10.7f);
            Assert.AreEqual(11f, rope.SnapTopTo(grid).Top, "0.3 below the surface at 11");

            var stepper = new PlayerStepper(grid, new[] { rope }, MovementSettings.Default);
            PlayerState s = stepper.CreateState(5.5f, 1f, Climber);
            bool stood = false;
            for (int tick = 0; tick < 300 && !stood; tick++)
            {
                s = stepper.Step(s, Hold(0, 1));
                stood = s.Contact == PlayerContact.OneWayTop;
            }
            Assert.IsTrue(stood, "climbed the snapped rope onto the one-way at its top");
            Assert.AreEqual(11f, s.Y, Tolerance);
        }

        [Test]
        public void ARopeTopJustAboveAOneWaySnapsDown()
        {
            Assert.AreEqual(11f, new RopeSegment(5.5f, 1f, 11.4f).SnapTopTo(OneWayAt(10)).Top);
        }

        [Test]
        public void ARopeTopMoreThanHalfATileAwayDoesNotSnap()
        {
            Assert.AreEqual(10.4f, new RopeSegment(5.5f, 1f, 10.4f).SnapTopTo(OneWayAt(10)).Top, "0.6 below");
            Assert.AreEqual(11.6f, new RopeSegment(5.5f, 1f, 11.6f).SnapTopTo(OneWayAt(10)).Top, "0.6 above");
        }

        [Test]
        public void EqualDistanceSnapsToTheHigherSurface()
        {
            Assert.AreEqual(12f, new RopeSegment(5.5f, 1f, 11.5f).SnapTopTo(OneWayAt(10, 11)).Top);
        }

        [Test]
        public void ASolidCellIsNeverASnapTarget()
        {
            TileGrid grid = AsciiLevel.ParseGrid(AsciiLevel.Rows(12, 30, (x, y) => y == 0 || (y == 10 && x == 5) ? '#' : '.'));
            Assert.AreEqual(10.7f, new RopeSegment(5.5f, 1f, 10.7f).SnapTopTo(grid).Top);
        }

        [Test]
        public void ASnapThatWouldInvertTheRopeIsSkipped()
        {
            RopeSegment snapped = new RopeSegment(5.5f, 11.2f, 11.4f).SnapTopTo(OneWayAt(10));
            Assert.AreEqual(11.4f, snapped.Top);
            Assert.AreEqual(11.2f, snapped.Bottom);
        }
    }
}
