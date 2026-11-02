using System;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Riding moving traps: carry (Y, then X), soft walls, lifts, falling floors, pull-away coyote, the carrier choice.</summary>
    public class TrapRideTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float Tolerance = 1e-4f;
        const int Delay = 150;

        // width x height with a floor on row 0 unless overridden; 'P' parked at (1, 1) (tests start with BeginAt).
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

        // A player standing at (x, top) on a trap at (tx, ty) w x h that fires with a delay after a straight jump.
        static LevelScenario Rider(string[] rows, int tx, int ty, int w, TrapActionKind action, int ox, int oy, float speed, float x,
            MovementSettings? settings = null)
        {
            float top = ty + 1;
            var s = new LevelScenario(rows);
            if (settings.HasValue)
            {
                s.WithSettings(settings.Value);
            }
            s.Trap(tx, ty, w, 1, TileKind.Solid, action, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(x, top), Delay, ox, oy, speed).BeginAt(x, top);
            TrapRig.JumpAndLand(s);
            Assert.AreEqual(top, s.State.Y, "precondition: standing on the trap");
            return s;
        }

        [Test]
        public void ASlidingFloorCarriesItsRiderByExactlyItsStepAndKeepsItStanding()
        {
            LevelScenario s = Rider(Map(30, 14), 5, 3, 3, TrapActionKind.Move, 6, 0, 4f, 6.5f);
            float offset = 0;
            TrapRig.IdleUntilMoving(s);
            int ticks = 0;
            while (true)
            {
                float dx = s.Stepper.TrapOffsetX(0) - offset;
                offset = s.Stepper.TrapOffsetX(0);
                Assert.AreEqual(dx, s.State.X - s.Previous.X, 1e-5f, $"tick {ticks}: carried by the trap's step");
                Assert.AreEqual(4f, s.State.Y, $"tick {ticks}: on the top");
                Assert.IsTrue(s.State.IsStanding, $"tick {ticks}: standing");
                ticks++;
                if (!s.Stepper.IsTrapMoving(0))
                {
                    break;
                }
                s.Tick(Idle);
            }
            Assert.AreEqual(TrapMotion.MoveTicks(6, 0, 4f), ticks, "precondition: rode the whole way");
            Assert.AreEqual(12.5f, s.State.X, Tolerance, "six tiles along");
        }

        [Test]
        public void AGroundJumpMidRideAddsNoSpeedFromTheTrap()
        {
            LevelScenario s = Rider(Map(30, 14), 5, 3, 3, TrapActionKind.Move, 6, 0, 4f, 6.5f);
            TrapRig.IdleUntilMoving(s);
            s.Tick(Idle, 20);
            Assert.IsTrue(s.State.IsStanding, "precondition: riding");
            var reference = new PlayerScenario(Abilities.Jump, Map(30, 14));
            reference.Tick(Idle, 3);
            reference.Tick(Press());
            s.Tick(Press());
            Assert.AreEqual(reference.State.VelocityY, s.State.VelocityY, Tolerance, "an ordinary ground jump");
            Assert.AreEqual(0f, s.State.VelocityX, "no momentum from the trap");
            float x = s.State.X;
            s.Tick(Hold(0, 0, true), 10);
            Assert.AreEqual(x, s.State.X, "the trap moves on without the player");
        }

        [Test]
        public void AWallStopsTheRiderAndTheTrapSlidesOutFromUnderIt()
        {
            // A wall at column 10, above the trap's row.
            LevelScenario s = Rider(Map(30, 14, (x, y) => x == 10 && y >= 4 && y <= 7 ? '#' : '\0'), 5, 3, 3, TrapActionKind.Move, 8, 0, 4f, 6.5f);
            TrapRig.IdleUntilMoving(s);
            int reached = s.RunUntil(Idle, v => v.State.X >= 10f - Hw - 1e-4f, 200);
            Assert.That(reached, Is.GreaterThan(0), "carried to the wall");
            Assert.AreEqual(10f - Hw, s.State.X, Tolerance, "stopped at the face on the tick it reached it");
            Assert.That(s.Previous.X, Is.LessThan(10f - Hw - 1e-4f));
            while (5f + s.Stepper.TrapOffsetX(0) < 10f - 1e-3f)
            {
                Assert.IsTrue(s.State.IsStanding, "standing while the trap is under the feet");
                Assert.AreEqual(10f - Hw, s.State.X, Tolerance, "held by the wall");
                s.Tick(Idle);
            }
            Assert.AreEqual(PlayerContact.Air, s.State.Contact, "the tick the trap leaves the feet: falling");
            Assert.AreEqual(MovementInternals.CoyoteTicks - 1, s.State.CoyoteTicks, "with the walk-off coyote window");
        }

        [Test]
        public void ARisingLiftCarriesItsRiderWithTheFeetOnItsTop()
        {
            LevelScenario s = Rider(Map(30, 14), 5, 3, 3, TrapActionKind.Move, 0, 3, 8f, 6.5f);
            TrapRig.IdleUntilMoving(s);
            do
            {
                Assert.AreEqual(4f + s.Stepper.TrapOffsetY(0), s.State.Y, 1e-5f, "feet on the top");
                Assert.IsTrue(s.State.IsStanding);
                s.Tick(Idle);
            }
            while (s.Stepper.IsTrapMoving(0));
            Assert.AreEqual(7f, s.State.Y, "at the end");
            Assert.IsTrue(s.State.IsStanding);
        }

        [Test]
        public void ALiftCrushesItsRiderOnTheFirstTickTheHeadWouldEnterTheCeiling()
        {
            // A ceiling on row 8, one tile above the lift's end (top 7, head 8.5).
            LevelScenario s = Rider(Map(30, 14, (x, y) => y == 8 ? '#' : '\0'), 5, 3, 3, TrapActionKind.Move, 0, 3, 8f, 6.5f);
            int died = s.RunUntil(Idle, v => v.Stepper.Phase == LevelPhase.Dying, 300);
            Assert.That(died, Is.GreaterThan(0), "crushed");
            float top = 4f + s.Stepper.TrapOffsetY(0);
            Assert.That(top, Is.GreaterThan(8f - MovementInternals.HitboxHeight), "the lift's top would put the head in the ceiling");
            Assert.That(s.Previous.Y + MovementInternals.HitboxHeight, Is.LessThanOrEqualTo(8f + Tolerance), "the tick before, it fit");
            Assert.AreEqual(1, s.Stepper.Deaths);
        }

        [Test]
        public void ALiftLiftingARiderAtItsEdgeIntoACeilingNudgesItOff()
        {
            // The rider stands with 0.175 of its width on the lift: the nudge clears it (≤ 0.25).
            LevelScenario s = Rider(Map(30, 14, (x, y) => y == 8 ? '#' : '\0'), 5, 3, 3, TrapActionKind.Move, 0, 3, 8f, 8.2f);
            int nudged = s.RunUntil(Idle, v => v.State.X > 8.2f + Tolerance || v.Stepper.Phase == LevelPhase.Dying, 300);
            Assert.That(nudged, Is.GreaterThan(0));
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "the nudge was tried and saved it");
            Assert.AreEqual(8f + Hw, s.State.X, Tolerance, "shifted just off the lift's edge");
            Assert.AreEqual(0, s.Stepper.Deaths);
        }

        [Test]
        public void ARiderNudgedOffADiagonalLiftIsNotAlsoCarriedSideways()
        {
            // The lift moves 1 right and 3 up; the nudge puts the rider just past its new edge, and sides never carry (code review).
            LevelScenario s = Rider(Map(30, 14, (x, y) => y == 8 ? '#' : '\0'), 5, 3, 3, TrapActionKind.Move, 1, 3, 8f, 8.2f);
            int nudged = s.RunUntil(Idle, v => v.State.Contact == PlayerContact.Air || v.Stepper.Phase == LevelPhase.Dying, 300);
            Assert.That(nudged, Is.GreaterThan(0));
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "nudged, not crushed");
            float liftRight = 8f + s.Stepper.TrapOffsetX(0);
            Assert.That(s.Stepper.TrapOffsetX(0), Is.GreaterThan(0f), "precondition: the lift moved right");
            Assert.AreEqual(liftRight + Hw, s.State.X, Tolerance, "just off the lift's new edge, no extra drag");
        }

        [TestCase(1f)]
        [TestCase(0.25f)]
        [TestCase(2f)]
        public void AFallingFloorKeepsItsRiderStandingAtEveryGravity(float gravity)
        {
            MovementSettings settings = MovementSettings.Default;
            settings.Gravity = gravity;
            LevelScenario s = Rider(Map(30, 14), 5, 8, 3, TrapActionKind.Fall, 0, 0, 8f, 6.5f, settings);
            TrapRig.IdleUntilMoving(s);
            int ticks = 0;
            while (s.Stepper.IsTrapMoving(0))
            {
                Assert.IsTrue(s.State.IsStanding, $"tick {ticks}: standing");
                Assert.AreEqual(9f + s.Stepper.TrapOffsetY(0), s.State.Y, 1e-4f, $"tick {ticks}: on the top");
                s.Tick(Idle);
                ticks++;
            }
            Assert.That(ticks, Is.GreaterThan(5), "precondition: it fell for a while");
            Assert.IsTrue(s.State.IsStanding, "standing on the landing tick");
            Assert.AreEqual(2f, s.State.Y, "feet exactly on the integer top");
            s.Tick(Idle);
            Assert.IsTrue(s.State.IsStanding, "and the next (no coyote hiccup)");
            Assert.AreEqual(2f, s.State.Y);
        }

        [Test]
        public void AJumpOffAFallingFloorIsAnOrdinaryJump()
        {
            LevelScenario s = Rider(Map(30, 14), 5, 8, 3, TrapActionKind.Fall, 0, 0, 8f, 6.5f);
            TrapRig.IdleUntilMoving(s);
            s.Tick(Idle, 10);
            Assert.IsTrue(s.Stepper.IsTrapMoving(0), "precondition: mid-fall");
            var reference = new PlayerScenario(Abilities.Jump, Map(30, 14));
            reference.Tick(Idle, 3);
            reference.Tick(Press());
            s.Tick(Press());
            Assert.AreEqual(reference.State.VelocityY, s.State.VelocityY, Tolerance, "nothing from the trap added");
        }

        // A Move 0 x -3 at speed 8 (the defaults): faster than the ride speed on its first tick.
        static LevelScenario Sinking()
        {
            LevelScenario s = Rider(Map(30, 14), 5, 6, 3, TrapActionKind.Move, 0, -3, 8f, 6.5f);
            TrapRig.IdleUntilMoving(s);
            return s;
        }

        [Test]
        public void ASinkingFloorPullsAwayWithTheWalkOffCoyoteWindow()
        {
            LevelScenario s = Sinking();
            Assert.AreEqual(PlayerContact.Air, s.State.Contact, "pulled away on its first moving tick");
            Assert.AreEqual(MovementInternals.CoyoteTicks - 1, s.State.CoyoteTicks, "set to 6 before the player's step, counted down by it (F9)");
            Assert.AreEqual(7f, s.Previous.Y, "precondition: standing on it before");
        }

        [Test]
        public void AJumpOnTheSixthPullAwayTickFiresAndOnTheSeventhDoesNot()
        {
            LevelScenario s = Sinking();
            s.Tick(Idle, 4);
            Assert.AreEqual(PlayerContact.Air, s.State.Contact, "precondition: still in the air");
            s.Tick(Press());
            Assert.That(s.State.VelocityY, Is.GreaterThan(0f), "pull-away tick + 5: the coyote jump fires");

            LevelScenario late = Sinking();
            late.Tick(Idle, 5);
            Assert.AreEqual(PlayerContact.Air, late.State.Contact, "precondition: still in the air");
            late.Tick(Press());
            Assert.That(late.State.VelocityY, Is.LessThanOrEqualTo(0f), "pull-away tick + 6: too late");
        }

        [Test]
        public void AfterAPullAwayThePlayerLandsBackAndRidesToTheEnd()
        {
            LevelScenario s = Sinking();
            int landed = s.RunUntil(Idle, v => v.State.IsStanding, 60);
            Assert.That(landed, Is.GreaterThan(0), "landed back");
            Assert.IsTrue(s.Stepper.IsTrapMoving(0), "before the trap arrived");
            while (s.Stepper.IsTrapMoving(0))
            {
                s.Tick(Idle);
                Assert.IsTrue(s.State.IsStanding, "rides it without another pull-away (ride speed seeded from the landing fall speed, F3)");
                Assert.AreEqual(7f + s.Stepper.TrapOffsetY(0), s.State.Y, 1e-4f);
            }
            Assert.AreEqual(4f, s.State.Y);
        }

        [Test]
        public void ATrapFasterThanMaxFallLeavesThePlayerBehind()
        {
            LevelScenario s = Rider(Map(30, 14), 5, 9, 3, TrapActionKind.Move, 0, -8, 30f, 6.5f);
            TrapRig.IdleUntilMoving(s);
            Assert.AreEqual(PlayerContact.Air, s.State.Contact, "pulled away");
            int arrived = s.RunUntil(Idle, v => !v.Stepper.IsTrapMoving(0), 60);
            Assert.That(arrived, Is.GreaterThan(0));
            Assert.AreEqual(PlayerContact.Air, s.State.Contact, "still falling when it arrived");
            Assert.That(s.RunUntil(Idle, v => v.State.IsStanding, 120), Is.GreaterThan(0));
            Assert.AreEqual(2f, s.State.Y, "landed on it at its end");
        }

        [Test]
        public void StandingHalfOnALedgeAFallingFloorDoesNotCarryThePlayer()
        {
            LevelScenario s = Rider(Map(30, 14, (x, y) => x == 4 && y == 3 ? '#' : '\0'), 5, 3, 3, TrapActionKind.Fall, 0, 0, 8f, 5f);
            TrapRig.IdleUntilMoving(s);
            for (int i = 0; i < 30; i++)
            {
                Assert.IsTrue(s.State.IsStanding, $"tick {i}: standing on the ledge");
                Assert.AreEqual(4f, s.State.Y, $"tick {i}: not carried down");
                s.Tick(Idle);
            }
            Assert.That(s.Stepper.TrapOffsetY(0), Is.LessThan(-1f), "precondition: the trap fell away");
        }

        static LevelScenario TwoCarriers(int ax, int ay, int bx, int by, float bSpeed, bool reversed)
        {
            var s = new LevelScenario(Map(30, 14));
            Box zone = TrapRig.ZoneOverHead(6f, 4f);
            s.Trap(5, 3, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, zone, Delay, ax, ay, 4f)
                .Trap(6, 3, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, zone, Delay, bx, by, bSpeed);
            if (reversed)
            {
                s.Reversed();
            }
            s.BeginAt(6f, 4f);
            TrapRig.JumpAndLand(s);
            TrapRig.IdleUntilMoving(s);
            return s;
        }

        [Test]
        public void WithTwoCarriersAtOneHeightTheSmallerMoveCarries()
        {
            LevelScenario s = TwoCarriers(2, 0, 4, 0, 8f, false);
            LevelScenario r = TwoCarriers(2, 0, 4, 0, 8f, true);
            Assert.AreEqual(6f + TrapMotion.MoveStep(4f), s.State.X, 1e-5f, "carried by the slower trap");
            for (int i = 0; i < 20; i++)
            {
                Assert.AreEqual(s.State.X, r.State.X, $"tick {i}: identical with the traps reversed");
                Assert.AreEqual(s.State.Y, r.State.Y);
                s.Tick(Idle);
                r.Tick(Idle);
            }
        }

        [Test]
        public void WithTwoCarriersTheHighestTopCarries()
        {
            LevelScenario s = TwoCarriers(0, 2, 3, 0, 8f, false);
            LevelScenario r = TwoCarriers(0, 2, 3, 0, 8f, true);
            Assert.AreEqual(4f + TrapMotion.MoveStep(4f), s.State.Y, 1e-5f, "lifted by the rising trap");
            Assert.AreEqual(6f, s.State.X, "not slid by the other");
            Assert.AreEqual(s.State.X, r.State.X);
            Assert.AreEqual(s.State.Y, r.State.Y);
        }
    }
}
