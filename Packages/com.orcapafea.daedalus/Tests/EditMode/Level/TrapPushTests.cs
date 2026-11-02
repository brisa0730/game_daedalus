using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// A Solid trap that appears on the player pushes it out the shortest free way, or crushes it; a moving solid pushes
    /// along its leading face, nudges a blocked push up to a quarter tile, then crushes. Every push lets go of a rope.
    /// </summary>
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

        // --- Moving solids (Phase 4): leading-face push, squeeze, crush and the nudge. ---

        // width x height with a floor on row 0 unless overridden; 'P' parked at (1, 1) (tests start with BeginAt).
        static string[] Map(int width, int height, System.Func<int, int, char> cell = null)
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

        const int Delay = 120;

        // Starts at (x, feet), jumps straight up into the zone over the head; the traps fire after the landing.
        static LevelScenario Primed(LevelScenario s, float x, float feet)
        {
            s.BeginAt(x, feet);
            TrapRig.JumpAndLand(s);
            Assert.AreEqual(feet, s.State.Y, "precondition: landed where it started");
            return s;
        }

        [Test]
        public void ACrusherPushesAlongItsLeadingFaceAndCrushesAgainstAWall()
        {
            // A wall at column 2; a 2 x 3 block at column 8 slides 6 left (its end is inside the wall).
            Box zone = TrapRig.ZoneOverHead(5f, 1f);
            var start = new Box(8, 1, 10, 4);
            LevelScenario s = Primed(new LevelScenario(Map(14, 8, (x, y) => x == 2 && y >= 1 ? '#' : '\0'))
                .Trap(8, 1, 2, 3, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, zone, Delay, -6, 0, 8f), 5f, 1f);
            TrapRig.IdleUntilMoving(s);
            int pushes = 0;
            while (s.Stepper.Phase == LevelPhase.Playing)
            {
                Box trap = TrapRig.Current(s, 0, start);
                if (trap.MinX < s.Previous.X + Hw - 1e-4f)
                {
                    pushes++;
                    Assert.AreEqual(trap.MinX - Hw, s.State.X, Tolerance, "pushed to the trap's new face");
                    Assert.AreEqual(0f, s.State.VelocityX, "no speed into the trap");
                }
                s.Tick(Hold(1));
            }
            Assert.That(pushes, Is.GreaterThan(3), "precondition: pushed for several ticks");
            Box end = TrapRig.Current(s, 0, start);
            Assert.That(end.MinX - Hw, Is.LessThan(3f + Hw), "crushed on the tick the wall blocks the push");
            Assert.That(s.Previous.X, Is.GreaterThanOrEqualTo(3f + Hw - Tolerance), "the tick before it still fit");
            Assert.AreEqual(1, s.Stepper.Deaths);
            Assert.AreEqual(s.Previous.X, s.Stepper.DeathX, Tolerance, "the death spot is where it was crushed");
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "the beat, then a respawn");
        }

        static LevelScenario FallingCeiling(float x)
        {
            return Primed(new LevelScenario(Map(14, 10))
                .Trap(4, 7, 3, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(x, 1f), Delay), x, 1f);
        }

        [Test]
        public void AFallingCeilingWithNoNudgeRoomCrushesWhenItReachesTheHead()
        {
            LevelScenario s = FallingCeiling(5.5f);
            int died = s.RunUntil(Idle, v => v.Stepper.Phase == LevelPhase.Dying, 120);
            Assert.That(died, Is.GreaterThan(0), "crushed");
            float bottom = 7f + s.Stepper.TrapOffsetY(0);
            Assert.That(bottom, Is.LessThan(1f + H), "its bottom reached the head this tick");
            Assert.AreEqual(1f, s.State.Y, "standing on the floor when it died");
        }

        [TestCase(0.2f, true)]
        [TestCase(0.3f, false)]
        public void AFallingCeilingClippingTheEdgeNudgesUpToAQuarterTile(float overlap, bool survives)
        {
            // The ceiling spans x 4..7; the hitbox overlaps it by `overlap` from the right.
            float x = 7f - overlap + Hw;
            LevelScenario s = FallingCeiling(x);
            int done = s.RunUntil(Idle, v => v.Stepper.Phase == LevelPhase.Dying || !v.Stepper.IsTrapMoving(0) && v.Stepper.TrapOffsetY(0) != 0, 120);
            Assert.That(done, Is.GreaterThan(0));
            if (survives)
            {
                Assert.AreEqual(0, s.Stepper.Deaths, "nudged clear");
                Assert.AreEqual(7f + Hw, s.State.X, Tolerance, $"shifted {overlap} sideways");
            }
            else
            {
                Assert.AreEqual(1, s.Stepper.Deaths, "past the nudge: crushed");
            }
        }

        // On a rope beside a wall (column 2) at feet `feet`; a 2 x 1 block at column 4 slides left at speed 30.
        static LevelScenario RopeBesideAWall(float feet)
        {
            string[] rows = AsciiLevel.Rows(12, 10, (x, y) =>
                y == 0 || (x == 2 && y >= 1) ? '#' : x == 3 && y >= 1 && y <= 8 ? 'R' : x == 6 && y == 1 ? 'P' : '.');
            LevelScenario s = new LevelScenario(rows).WithAbilities(Abilities.Jump | Abilities.RopeClimb)
                .Trap(4, 1, 2, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, new Box(3, 2.55f, 4, 2.6f), 60, -3, 0, 30f)
                .BeginAt(3.5f, 1f);
            int climbed = s.RunUntil(Hold(0, 1), v => v.State.Contact == PlayerContact.Rope && v.State.Y >= feet - 1e-4f, 120);
            Assert.That(climbed, Is.GreaterThan(0), "precondition: climbed the rope");
            Assert.AreEqual(feet, s.State.Y, 1e-3f, "precondition: feet at the planned height");
            Assert.IsFalse(s.Stepper.IsTrapMoving(0), "precondition: before the block moves");
            return s;
        }

        [TestCase(1.8f, true)]
        [TestCase(1.7f, false)]
        public void ASidewaysPushAgainstAWallNudgesUpOntoThePusherWithinAQuarterTile(float feet, bool survives)
        {
            // The block's top is at 2: feet 0.2 below it are nudged up onto it; 0.3 below are crushed (F10: airborne or riding only).
            LevelScenario s = RopeBesideAWall(feet);
            int moved = s.RunUntil(Idle, v => v.Stepper.IsTrapMoving(0) || v.Stepper.Phase == LevelPhase.Dying, 120);
            Assert.That(moved, Is.GreaterThan(0));
            if (survives)
            {
                Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "nudged");
                Assert.AreEqual(3.5f, s.State.X, Tolerance, "not pushed into the wall");
                Assert.That(s.State.Y, Is.GreaterThan(2f - 0.05f), "lifted onto the block's top (then its own step)");
                Assert.AreEqual(-1, s.State.RopeIndex, "off the rope");
            }
            else
            {
                Assert.AreEqual(LevelPhase.Dying, s.Stepper.Phase, "crushed");
            }
        }

        [Test]
        public void ABlockMovingDownPushesAJumpingPlayerDown()
        {
            // A 3 x 1 block at row 6 sinks 3 as the player jumps under it (zone low, no delay).
            LevelScenario s = new LevelScenario(Map(14, 10))
                .Trap(4, 6, 3, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, new Box(5, 2.7f, 6, 2.8f), 0, 0, -3, 8f)
                .BeginAt(5.5f, 1f);
            s.Tick(Idle, 2);
            s.Tick(Press());
            bool pushed = false;
            for (int i = 0; i < 60 && s.Stepper.Phase == LevelPhase.Playing; i++)
            {
                s.Tick(Hold(0, 0, true));
                Box block = TrapRig.Current(s, 0, new Box(4, 6, 7, 7));
                Assert.That(s.State.Y + H, Is.LessThanOrEqualTo(block.MinY + Tolerance), $"tick {i}: never inside the block");
                if (s.Stepper.IsTrapMoving(0) && s.Previous.Y + H > block.MinY + Tolerance)
                {
                    // The block's new bottom went below where the head was: a push.
                    pushed = true;
                    Assert.That(s.State.VelocityY, Is.LessThanOrEqualTo(0f), $"tick {i}: no upward speed after the push");
                }
            }
            Assert.IsTrue(pushed, "pushed down while rising");
            Assert.AreEqual(0, s.Stepper.Deaths, "alive");
        }

        [Test]
        public void ATrapRisingIntoAFallingPlayerLiftsItOntoItsTop()
        {
            // Standing on a block that rises 5 at speed 30 once the player is in the air above it.
            Box zone = TrapRig.ZoneOverHead(5.5f, 2f);
            LevelScenario s = new LevelScenario(Map(14, 12))
                .Trap(4, 1, 3, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, zone, 22, 0, 5, 30f)
                .BeginAt(5.5f, 2f);
            s.Tick(Press());
            bool lifted = false;
            for (int i = 0; i < 90; i++)
            {
                s.Tick(Hold(0, 0, true));
                lifted |= s.Previous.Contact == PlayerContact.Air && s.Previous.VelocityY < 0 && s.State.Y > s.Previous.Y;
            }
            Assert.IsTrue(lifted, "lifted while falling");
            Assert.IsTrue(s.State.IsStanding, "standing on it");
            Assert.AreEqual(7f, s.State.Y, "on its top at its end");
            Assert.AreEqual(0, s.Stepper.Deaths);
        }

        static LevelScenario TwoPushers(int ax, int ay, int aox, float aSpeed, int bx, int by, int box, float bSpeed, float x, bool reversed)
        {
            Box zone = TrapRig.ZoneOverHead(x, 1f);
            var s = new LevelScenario(Map(14, 8))
                .Trap(ax, ay, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, zone, Delay, aox, 0, aSpeed)
                .Trap(bx, by, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, zone, Delay, box, 0, bSpeed);
            if (reversed)
            {
                s.Reversed();
            }
            Primed(s, x, 1f);
            s.RunUntil(Idle, v => v.Stepper.IsTrapMoving(0) || v.Stepper.Phase != LevelPhase.Playing, 200);
            return s;
        }

        [Test]
        public void TwoTrapsPushingTheSameWayPushToTheFurthestTarget()
        {
            // Both overlap on their first tick: the slow one's target is 7.867 − Hw, the fast one's 7.5 − Hw.
            foreach (bool reversed in new[] { false, true })
            {
                LevelScenario s = TwoPushers(8, 1, -3, 8f, 8, 2, -3, 30f, 7.9f - Hw, reversed);
                Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase);
                Assert.AreEqual(7.5f - Hw, s.State.X, Tolerance, $"the furthest target (reversed: {reversed})");
            }
        }

        [Test]
        public void TwoTrapsPushingOppositeWaysSqueeze()
        {
            foreach (bool reversed in new[] { false, true })
            {
                LevelScenario s = TwoPushers(3, 1, 3, 30f, 7, 1, -3, 30f, 5.5f, reversed);
                int died = s.RunUntil(Idle, v => v.Stepper.Phase == LevelPhase.Dying, 30);
                Assert.That(died, Is.GreaterThan(0), $"squeezed (reversed: {reversed})");
                Assert.AreEqual(1, s.Stepper.Deaths);
            }
        }

        [Test]
        public void ATrapMovingUpBesideThePlayerDoesNotCarryIt()
        {
            LevelScenario s = Primed(new LevelScenario(Map(14, 10))
                .Trap(8, 1, 1, 2, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(8f - Hw, 1f), Delay, 0, 3, 8f), 8f - Hw, 1f);
            TrapRig.IdleUntilMoving(s);
            s.RunUntil(Idle, v => !v.Stepper.IsTrapMoving(0), 60);
            Assert.AreEqual(8f - Hw, s.State.X, "touching its side, left in place");
            Assert.AreEqual(1f, s.State.Y);
        }

        // On a rope (column 5), Up held; a block above sinks 1 at speed 30 onto the head. The lockout counts from the
        // push that lets go of the rope; the second push (already off it) doesn't restart it.
        [Test]
        public void AMovePushingThePlayerOffARopeBlocksTheRegrabForTwelveTicks()
        {
            string[] rows = AsciiLevel.Rows(12, 12, (x, y) => y == 0 ? '#' : x == 5 && y >= 1 && y <= 10 ? 'R' : x == 1 && y == 1 ? 'P' : '.');
            LevelScenario s = new LevelScenario(rows)
                .WithAbilities(Abilities.Jump | Abilities.RopeClimb)
                .Trap(5, 8, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, new Box(5, 7.6f, 6, 7.65f), 0, 0, -1, 30f)
                .BeginAt(5.5f, 1f);
            int trigger = s.RunUntil(Hold(0, 1), v => Box.Hitbox(v.State.X, v.State.Y).Overlaps(new Box(5, 7.6f, 6, 7.65f)), 200);
            Assert.That(trigger, Is.GreaterThan(0), "precondition: climbed into the zone");
            Assert.AreEqual(PlayerContact.Rope, s.State.Contact);
            s.Tick(Hold(0, 1));
            Assert.IsTrue(s.Stepper.IsTrapMoving(0));
            Assert.AreEqual(-1, s.State.RopeIndex, "pushed off on the first moving tick");
            for (int k = 1; k <= MovementInternals.RopeLockoutTicks; k++)
            {
                s.Tick(Hold(0, 1));
                Assert.AreNotEqual(PlayerContact.Rope, s.State.Contact, $"push tick + {k}: blocked");
                if (k == 1)
                {
                    Assert.IsFalse(s.Stepper.IsTrapMoving(0), "precondition: the second push tick (arrived)");
                }
            }
            s.Tick(Hold(0, 1));
            Assert.AreEqual(PlayerContact.Rope, s.State.Contact, "push tick + 13: grabbed again");
        }

        // A hidden block appears around the player on a rope (column 5) and pushes it up onto itself; Up held after.
        [TestCase(0)]
        [TestCase(30)]
        public void AnAppearPushOutOffARopeBlocksTheRegrabForTwelveTicks(int delay)
        {
            string[] rows = AsciiLevel.Rows(12, 12, (x, y) => y == 0 ? '#' : x == 5 && y >= 1 && y <= 9 ? 'R' : x == 1 && y == 1 ? 'P' : '.');
            LevelScenario s = new LevelScenario(rows)
                .WithAbilities(Abilities.Jump | Abilities.RopeClimb)
                .Trap(4, 2, 3, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(4, 4, 7, 5), delay)
                .BeginAt(5.5f, 1f);
            Assert.That(s.RunUntil(Hold(0, 1), v => Box.Hitbox(v.State.X, v.State.Y).Overlaps(new Box(4, 4, 7, 5)), 200), Is.GreaterThan(0));
            int fired = s.Stepper.IsTrapPresent(0) ? 0 : s.RunUntil(Idle, v => v.Stepper.IsTrapPresent(0), 60);
            Assert.That(fired, Is.GreaterThanOrEqualTo(0), "precondition: the block appeared");
            Assert.AreEqual(-1, s.State.RopeIndex, "pushed off the rope");
            Assert.AreEqual(3f, s.State.Y, Tolerance, "onto the block");
            for (int k = 1; k <= MovementInternals.RopeLockoutTicks; k++)
            {
                s.Tick(Hold(0, 1));
                Assert.AreNotEqual(PlayerContact.Rope, s.State.Contact, $"push tick + {k}: blocked (delay {delay})");
            }
            s.Tick(Hold(0, 1));
            Assert.AreEqual(PlayerContact.Rope, s.State.Contact, $"push tick + 13: grabbed again (delay {delay})");
        }
    }
}
