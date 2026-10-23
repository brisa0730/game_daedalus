using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class WallClimbTests
    {
        const float Tolerance = 1e-3f;
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float Grip = MovementInternals.GripHeight;
        const Abilities Climber = Abilities.Jump | Abilities.WallClimb;

        // A floor and a climbable wall in column 10 (face at x = 10) from row 1 up to row top − 1.
        // above: what sits on the run in row top ('.' open, '#' solid). The map is 30 tall.
        static string[] ClimbMap(int startCol, int startRow, int top = 29, char above = '.', int bottom = 1, bool floor = true)
        {
            return AsciiLevel.Rows(14, 30, (x, y) =>
            {
                if (y == 0 && floor)
                {
                    return '#';
                }
                if (x == 10 && y >= bottom && y < top)
                {
                    return 'C';
                }
                if (x == 10 && y == top)
                {
                    return above;
                }
                return x == startCol && y == startRow ? 'P' : '.';
            });
        }

        static void ClingFromTheAir(PlayerScenario p)
        {
            int ticks = p.RunUntil(Hold(1), s => s.State.Contact == PlayerContact.WallCling, 200);
            Assert.That(ticks, Is.GreaterThan(0), "clung to the climbable wall");
        }

        [Test]
        public void WithoutWallClimbAClimbableWallIsAnOrdinaryWall()
        {
            var p = new PlayerScenario(Abilities.Jump | Abilities.WallJump, ClimbMap(8, 20));
            int slid = p.RunUntil(Hold(1, 1), s => s.State.Contact == PlayerContact.WallSlide, 200);
            Assert.That(slid, Is.GreaterThan(0), "slides like on an ordinary wall");
            Assert.IsFalse(p.ContactsSeen.Contains(PlayerContact.WallCling), "never clings");
            p.Tick(Press(1));
            Assert.AreEqual(-9f, p.State.VelocityX, Tolerance, "wall-jumps like off an ordinary wall");
        }

        [Test]
        public void ClingsFromTheAirWhileFalling()
        {
            var p = new PlayerScenario(Climber, ClimbMap(8, 20));
            ClingFromTheAir(p);
            Assert.AreEqual(1, p.State.WallSide);
            Assert.AreEqual(0f, p.State.VelocityX);
            Assert.AreEqual(0f, p.State.VelocityY);
            float y = p.State.Y;
            p.Tick(Idle, 30);
            Assert.AreEqual(PlayerContact.WallCling, p.State.Contact, "neutral holds");
            Assert.AreEqual(y, p.State.Y, "and holds still");
        }

        [Test]
        public void RisingPastAClimbableFaceClingsOnlyWithUp()
        {
            var p = new PlayerScenario(Climber, ClimbMap(8, 1));
            p.Tick(Hold(1), 60);
            Assert.IsTrue(p.State.IsStanding, "pressing toward without Up does not cling from the ground");
            p.Tick(Press(1));
            int clung = p.RunUntil(Hold(1, 0, true), s => s.State.Contact == PlayerContact.WallCling, 60);
            Assert.That(clung + 1, Is.GreaterThanOrEqualTo(21), "no cling while rising with only toward held (apex on tick 21)");
            Assert.That(p.Previous.Y, Is.GreaterThan(4f), "clung near the apex, once falling");

            p = new PlayerScenario(Climber, ClimbMap(8, 1));
            p.Tick(Hold(1), 60);
            p.Tick(Press(1));
            p.Tick(Hold(1, 1, true));
            Assert.AreEqual(PlayerContact.Air, p.Previous.Contact, "precondition: airborne after the jump");
            Assert.That(p.Previous.VelocityY, Is.GreaterThan(0f), "precondition: rising");
            Assert.AreEqual(PlayerContact.WallCling, p.State.Contact, "rising with Up + toward clings");

            p = new PlayerScenario(Climber, ClimbMap(8, 1));
            p.Tick(Hold(1), 60);
            p.Tick(Hold(1, 1));
            Assert.AreEqual(PlayerContact.WallCling, p.State.Contact, "Up + toward clings from the ground");
        }

        [TestCase(Abilities.Jump)]
        [TestCase(Abilities.Jump | Abilities.WallClimb)]
        public void PlainJumpStillReachesAClimbableFacedLedge(Abilities abilities)
        {
            // A 3-tile ledge with a climbable face at x = 6; the player stands 1 tile from it.
            var p = new PlayerScenario(abilities, AsciiLevel.Rows(12, 12, (x, y) =>
                y == 0 ? '#' : x >= 6 && y <= 3 ? 'C' : x == 4 && y == 1 ? 'P' : '.'));
            p.State = p.Stepper.CreateState(5f - Hw, 1f, abilities);
            p.Tick(Press(1));
            int landed = p.RunUntil(Hold(1, 0, true), s => s.State.IsStanding, 200);
            Assert.That(landed, Is.GreaterThan(0));
            Assert.AreEqual(4f, p.State.Y, "standing on the ledge");
            Assert.IsFalse(p.ContactsSeen.Contains(PlayerContact.WallCling));
        }

        [Test]
        public void ClimbsAtClimbSpeedAndPressingAwayLetsGo()
        {
            var p = new PlayerScenario(Climber, ClimbMap(8, 20));
            ClingFromTheAir(p);
            float y = p.State.Y;
            p.Tick(Hold(0, 1), 20);
            Assert.AreEqual(y + 1f, p.State.Y, Tolerance, "up at 3 tiles/s");
            p.Tick(Hold(1, -1), 20);
            Assert.AreEqual(y, p.State.Y, Tolerance, "down at 3 tiles/s");
            p.Tick(Hold(-1));
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "pressing away lets go");
            Assert.AreEqual(MovementInternals.WallCoyoteTicks, p.State.WallCoyoteTicks);
            Assert.IsTrue(p.State.WallCoyoteFromCling);
        }

        [Test]
        public void ClimbingOverAnOpenTopPopsOntoTheLedge()
        {
            var p = new PlayerScenario(Climber, ClimbMap(8, 8, 10));
            ClingFromTheAir(p);
            int popped = p.RunUntil(Hold(0, 1), s => s.State.IsStanding, 200);
            Assert.That(popped, Is.GreaterThan(0));
            Assert.AreEqual(PlayerContact.Ground, p.State.Contact);
            Assert.AreEqual(10f, p.State.Y, "feet at the run's top");
            Assert.AreEqual(10.75f, p.State.X, Tolerance, "centre 0.75 past the face");
            Assert.AreEqual(0f, p.State.VelocityY);
        }

        [Test]
        public void HoldsAtTheTopWhenToppedBySolid()
        {
            var p = new PlayerScenario(Climber, ClimbMap(8, 4, 10, '#'));
            ClingFromTheAir(p);
            p.Tick(Hold(0, 1), 200);
            Assert.AreEqual(PlayerContact.WallCling, p.State.Contact);
            Assert.AreEqual(10f, p.State.Y + Grip, Tolerance, "grip at T");
            p.Tick(Idle, 60);
            Assert.AreEqual(PlayerContact.WallCling, p.State.Contact, "holding at T for 60 ticks stays clinging");
            Assert.AreEqual(10f, p.State.Y + Grip, Tolerance);
        }

        [Test]
        public void PopBlockedByALowCeilingStopsAtTheTop()
        {
            string[] rows = AsciiLevel.Rows(14, 30, (x, y) =>
                y == 0 ? '#' : x == 10 && y >= 1 && y < 10 ? 'C' : (x == 10 || x == 11) && y == 11 ? '#' : x == 8 && y == 4 ? 'P' : '.');
            var p = new PlayerScenario(Climber, rows);
            ClingFromTheAir(p);
            p.Tick(Hold(0, 1), 200);
            Assert.AreEqual(PlayerContact.WallCling, p.State.Contact, "no pop under a low ceiling");
            Assert.AreEqual(10f, p.State.Y + Grip, Tolerance);
        }

        [Test]
        public void ClimbingDownLandsOnGroundOrFallsOffAFloatingStrip()
        {
            var p = new PlayerScenario(Climber, ClimbMap(8, 6));
            ClingFromTheAir(p);
            int landed = p.RunUntil(Hold(0, -1), s => s.State.IsStanding, 300);
            Assert.That(landed, Is.GreaterThan(0));
            Assert.AreEqual(PlayerContact.Ground, p.State.Contact);
            Assert.AreEqual(1f, p.State.Y);

            p = new PlayerScenario(Climber, ClimbMap(8, 22, 26, '.', 15));
            ClingFromTheAir(p);
            int fell = p.RunUntil(Hold(0, -1), s => s.State.Contact == PlayerContact.Air, 300);
            Assert.That(fell, Is.GreaterThan(0), "released at the run's bottom");
            Assert.That(p.State.Y + Grip, Is.LessThan(15f + Tolerance));
            Assert.That(p.State.Y, Is.GreaterThan(10f), "released at the strip, not on the floor");
        }

        [Test]
        public void WallClimbWithoutWallJump()
        {
            var p = new PlayerScenario(Climber, ClimbMap(8, 20));
            ClingFromTheAir(p);
            p.Tick(Press(1));
            Assert.AreEqual(-9f, p.State.VelocityX, Tolerance, "jumping off a cling pushes away");
            Assert.That(p.State.VelocityY, Is.GreaterThan(10f));

            var w = new PlayerScenario(Climber, WallJumpTests.WallMap(8, 20));
            w.RunUntil(Hold(1), s => WallJumpTests.GapToWall(s, s.State, 1) <= 1e-4f, 200);
            w.Tick(Hold(1), 5);
            Assert.AreEqual(PlayerContact.Air, w.State.Contact, "no slide on an ordinary wall");
            w.Tick(Press(1));
            Assert.That(w.State.VelocityY, Is.LessThan(0f), "no wall jump off an ordinary wall");
        }

        [Test]
        public void ClingRefillsAirJumps()
        {
            var p = new PlayerScenario(Climber | Abilities.DoubleJump, ClimbMap(6, 20));
            p.Tick(Idle, 2);
            p.Tick(Press());
            Assert.AreEqual(0, p.State.AirJumpsLeft);
            ClingFromTheAir(p);
            Assert.AreEqual(1, p.State.AirJumpsLeft);
        }
    }
}
