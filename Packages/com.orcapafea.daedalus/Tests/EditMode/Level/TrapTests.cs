using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class TrapTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float Tolerance = 1e-4f;

        // The floor has a 3-wide gap at columns 6–8 that a Drop Floor fills.
        static readonly string[] Gap =
        {
            "......................",
            "......................",
            ".P....................",
            "######...#############",
        };

        static readonly string[] Floor =
        {
            "......................",
            "......................",
            "......................",
            ".P....................",
            "######################",
        };

        static LevelScenario DropFloor(params string[] rows)
        {
            return new LevelScenario(rows).Trap(6, 0, 3, 1, TileKind.Solid, TrapActionKind.Vanish);
        }

        [Test]
        public void ADropFloorFiresOnEdgeEntryAndThePlayerFallsOnceFullyOffThePaintedFloor()
        {
            LevelScenario s = DropFloor(Gap).Begin();
            int fire = s.RunUntil(Hold(1), x => !x.Stepper.IsTrapPresent(0));
            Assert.That(fire, Is.GreaterThan(0), "fired");
            Assert.That(s.State.X + Hw, Is.GreaterThan(6f), "the hitbox's leading edge is past the trap's edge");
            Assert.That(s.Previous.X + Hw, Is.LessThanOrEqualTo(6f), "and was not the tick before");
            Assert.IsTrue(s.State.IsStanding, "still supported by the painted floor behind");
            Assert.AreEqual(TileKind.None, s.Level.Grid.Get(6, 0), "the cells are gone the same tick");

            int fall = s.RunUntil(Hold(1), x => x.State.Contact == PlayerContact.Air);
            Assert.That(fall, Is.GreaterThan(0), "fell");
            Assert.That(s.State.X - Hw, Is.GreaterThanOrEqualTo(6f - Tolerance), "the hitbox has fully left the painted floor");
            Assert.That(s.Previous.X - Hw, Is.LessThan(6f), "it had not the tick before");
        }

        [Test]
        public void ADropFloorFiresBeforeLandingWhenFallenOntoFromAbove()
        {
            LevelScenario s = DropFloor(
                "......................",
                "......................",
                "......................",
                "......................",
                "......................",
                "......................",
                ".P....................",
                "######...#############").Begin();
            s.BeginAt(7.5f, 5f);
            int fire = s.RunUntil(Idle, x => !x.Stepper.IsTrapPresent(0));
            Assert.That(fire, Is.GreaterThan(0));
            Assert.AreEqual(PlayerContact.Air, s.State.Contact, "not landed");
            Assert.That(s.State.Y, Is.LessThan(2f), "in the zone");
        }

        [Test]
        public void ADropFloorIgnoresAPlayerPassingOneTileAbove()
        {
            LevelScenario s = DropFloor(
                "......................",
                "......................",
                ".P....................",
                "...########...........",
                "######...#############").Begin();
            s.BeginAt(3.5f, 2f);
            s.RunUntil(Hold(1), x => x.State.X > 10f);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "never fired");
        }

        [Test]
        public void APopSpikeFiresOnEdgeEntryAndKillsWhenTheCoreArrives()
        {
            LevelScenario s = new LevelScenario(Floor).Trap(6, 1, 3, 1, TileKind.Hazard, TrapActionKind.Appear).Begin();
            int fire = s.RunUntil(Hold(1), x => x.Stepper.IsTrapPresent(0));
            Assert.That(fire, Is.GreaterThan(0), "fired");
            Assert.That(s.State.X + Hw, Is.GreaterThan(6f));
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "alive on the fire tick");
            int fireTick = s.Ticks;

            Assert.That(s.RunUntil(Hold(1), x => x.Stepper.Phase == LevelPhase.Dying), Is.GreaterThan(0), "died");
            Assert.That(s.Ticks, Is.GreaterThan(fireTick), "death comes strictly later");
            TileGrid grid = s.Level.Grid;
            Assert.IsTrue(TileCollision.CoreTouchesHazard(grid, s.State.X, s.State.Y));
            Assert.IsFalse(TileCollision.CoreTouchesHazard(grid, s.Previous.X, s.Previous.Y), "no core overlap before the death tick");
        }

        // Spikes at row 1 under a one-way walkway whose top is 2 tiles above the floor.
        static readonly string[] Walkway =
        {
            "......................",
            "......................",
            "......................",
            "...--------...........",
            ".P....................",
            "######################",
        };

        [Test]
        public void APopSpikeNeverFiresForAPlayerAboveItsZone()
        {
            LevelScenario s = new LevelScenario(Walkway).Trap(6, 1, 3, 1, TileKind.Hazard, TrapActionKind.Appear).BeginAt(3.5f, 3f);
            Assert.AreEqual(3f, s.State.Y, "precondition: on the walkway, 1 tile above the spikes' top + 1");
            s.RunUntil(Hold(1), x => x.State.X > 10f);
            Assert.IsFalse(s.Stepper.IsTrapPresent(0));
        }

        [Test]
        public void APopSpikeFiresForFeetInTheZoneAboveTheSpikesAndThePlayerLives()
        {
            LevelScenario s = new LevelScenario(Walkway)
                .Trap(6, 1, 3, 1, TileKind.Hazard, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(6, 1, 9, 4))
                .BeginAt(3.5f, 3f);
            s.RunUntil(Hold(1), x => x.State.X > 10f);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "fired");
            Assert.AreEqual(0, s.Stepper.Deaths, "lived");
        }

        [Test]
        public void ATouchTrapFiresWhenStoodOnAndWhenPushedAgainst()
        {
            LevelScenario floor = new LevelScenario(Gap).Trap(6, 0, 3, 1, TileKind.Solid, TrapActionKind.Vanish, TrapTriggerKind.Touch).Begin();
            Assert.That(floor.RunUntil(Hold(1), x => !x.Stepper.IsTrapPresent(0)), Is.GreaterThan(0), "stood on: fired");
            Assert.IsTrue(Box.Hitbox(floor.State.X, floor.State.Y).Grown(LevelRules.ContactMargin).Overlaps(new Box(6, 0, 9, 1)), "touching it");

            LevelScenario wall = new LevelScenario(Floor).Trap(6, 1, 1, 2, TileKind.Solid, TrapActionKind.Vanish, TrapTriggerKind.Touch).Begin();
            Assert.That(wall.RunUntil(Hold(1), x => !x.Stepper.IsTrapPresent(0)), Is.GreaterThan(0), "pushed against: fired");
            wall.Tick(Hold(1), 30);
            Assert.That(wall.State.X, Is.GreaterThan(7f), "walked on through");
        }

        [Test]
        public void ADelayFiresExactlyThatManyTicksAfterTheTrigger()
        {
            LevelScenario s = new LevelScenario(Gap).Trap(6, 0, 3, 1, TileKind.Solid, TrapActionKind.Vanish, TrapTriggerKind.Zone,
                new Box(3, 1, 4, 3), LevelRules.SecondsToTicks(0.5f)).Begin();
            int trigger = s.RunUntil(Hold(1), x => x.State.X + Hw > 3f);
            Assert.That(trigger, Is.GreaterThan(0), "entered the zone");
            int triggerTick = s.Ticks;
            s.Tick(Idle, 29);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "29 ticks after: not yet");
            s.Tick(Idle);
            Assert.IsFalse(s.Stepper.IsTrapPresent(0), "30 ticks after the trigger tick");
            Assert.AreEqual(triggerTick + 30, s.Ticks);
        }

        [Test]
        public void ADeathDuringADelayCancelsIt()
        {
            LevelScenario s = new LevelScenario(
                    "......................",
                    "......................",
                    ".P.......^............",
                    "######################")
                .Trap(15, 0, 3, 1, TileKind.Solid, TrapActionKind.Vanish, TrapTriggerKind.Zone, new Box(3, 1, 4, 3), 180)
                .Begin();
            Assert.That(s.RunUntil(Hold(1), x => x.Stepper.Phase == LevelPhase.Dying), Is.GreaterThan(0), "died in the delay");
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.IsTrue(s.State.Teleported, "precondition: respawned");
            Assert.That(s.State.X - Hw, Is.GreaterThanOrEqualTo(4f), "precondition: back outside the zone");
            s.Tick(Idle, 200);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "the pending fire was cancelled and the trap reset");
        }

        [Test]
        public void ATrapFiresOncePerLife()
        {
            LevelScenario s = new LevelScenario(Floor).Trap(15, 3, 1, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(5, 1, 6, 3)).Begin();
            s.RunUntil(Hold(1), x => x.State.X > 8f);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "fired");
            int changes = s.Stepper.TrapChanges;
            s.RunUntil(Hold(-1), x => x.State.X < 3f);
            s.RunUntil(Hold(1), x => x.State.X > 8f);
            Assert.AreEqual(changes, s.Stepper.TrapChanges, "re-entering did nothing");
        }

        [Test]
        public void APlayStartInsideAZoneDoesNotFireUntilThePlayerLeavesAndReenters()
        {
            LevelScenario s = new LevelScenario(Floor).Trap(15, 3, 1, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(0, 1, 4, 3)).Begin();
            s.Tick(Idle, 30);
            Assert.IsFalse(s.Stepper.IsTrapPresent(0), "started inside: not armed");
            s.RunUntil(Hold(1), x => x.State.X - Hw > 4f);
            Assert.IsFalse(s.Stepper.IsTrapPresent(0), "left");
            s.RunUntil(Hold(-1), x => x.State.X + Hw < 3.5f);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "re-entered: fired");
        }

        [Test]
        public void ACheckpointRespawnInsideAZoneDoesNotFireUntilThePlayerLeavesAndReenters()
        {
            LevelScenario s = new LevelScenario(
                    "......................",
                    "......................",
                    ".P..........^.........",
                    "######################")
                .WithRule(RespawnRule.Checkpoint)
                .Checkpoint(new Box(5, 1, 6, 2))
                .Trap(20, 3, 1, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(4, 1, 7, 3), 300)
                .Begin();
            Assert.That(s.RunUntil(Hold(1), x => x.Stepper.Phase == LevelPhase.Dying), Is.GreaterThan(0), "died on the spike");
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.AreEqual(5.5f, s.State.X, "precondition: respawned at the checkpoint, inside the zone");
            s.Tick(Idle, 30);
            Assert.That(s.Stepper.TrapCount, Is.EqualTo(1));
            int changes = s.Stepper.TrapChanges;
            s.RunUntil(Hold(1), x => x.State.X - Hw > 7f);
            s.RunUntil(Hold(-1), x => x.State.X + Hw < 6.5f);
            s.Tick(Idle, 301);
            Assert.AreNotEqual(changes, s.Stepper.TrapChanges, "fired only after leaving and re-entering");
        }

        [Test]
        public void EveryDeathResetsEveryTrap()
        {
            LevelScenario s = new LevelScenario(
                    "......................",
                    "......................",
                    ".P..........^.........",
                    "######################")
                .WithRule(RespawnRule.Checkpoint)
                .Trap(20, 0, 2, 1, TileKind.Solid, TrapActionKind.Vanish, TrapTriggerKind.Zone, new Box(3, 1, 4, 3))
                .Begin();
            s.RunUntil(Hold(1), x => !x.Stepper.IsTrapPresent(0));
            int changes = s.Stepper.TrapChanges;
            Assert.That(s.RunUntil(Hold(1), x => x.Stepper.Phase == LevelPhase.Dying), Is.GreaterThan(0), "died far from the trap");
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "back after the death");
            Assert.AreNotEqual(changes, s.Stepper.TrapChanges, "the respawn moved TrapChanges");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(20, 0));
        }

        [Test]
        public void PaintedTilesStayInCharge()
        {
            LevelScenario s = new LevelScenario(Floor)
                .Trap(6, 0, 3, 1, TileKind.Solid, TrapActionKind.Vanish)
                .Trap(12, 0, 2, 1, TileKind.Hazard, TrapActionKind.Appear)
                .Begin();
            s.RunUntil(Hold(1), x => x.State.X > 16f);
            Assert.IsFalse(s.Stepper.IsTrapPresent(0), "precondition: the Vanish fired");
            Assert.IsTrue(s.Stepper.IsTrapPresent(1), "precondition: the Appear fired");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(7, 0), "the painted floor under the vanished trap");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(12, 0), "the painted floor under the appeared spikes");
            Assert.AreEqual(0, s.Stepper.Deaths);
        }

        [Test]
        public void SolidBeatsHazardWhereTwoPresentTrapsOverlap()
        {
            LevelScenario s = new LevelScenario(Floor)
                .Trap(10, 2, 2, 1, TileKind.Hazard, TrapActionKind.Vanish)
                .Trap(11, 2, 2, 1, TileKind.Solid, TrapActionKind.Vanish)
                .Begin();
            Assert.AreEqual(TileKind.Hazard, s.Level.Grid.Get(10, 2));
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(11, 2), "both present: Solid wins");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(12, 2));
        }

        [Test]
        public void FiringOrderDoesNotChangeTheResult()
        {
            LevelScenario forward = TwoAppears(false);
            LevelScenario reversed = TwoAppears(true);
            Assert.That(forward.Stepper.TrapChanges, Is.GreaterThan(0), "precondition: both fired");
            Assert.AreEqual(forward.State.X, reversed.State.X);
            Assert.AreEqual(forward.State.Y, reversed.State.Y);
            Assert.AreEqual(forward.State.Contact, reversed.State.Contact);
            for (int x = 4; x < 9; x++)
            {
                for (int y = 1; y < 3; y++)
                {
                    Assert.AreEqual(forward.Level.Grid.Get(x, y), reversed.Level.Grid.Get(x, y), $"cell ({x}, {y})");
                }
            }
            Assert.AreEqual(forward.Stepper.IsTrapPresent(0), reversed.Stepper.IsTrapPresent(1));
            Assert.AreEqual(forward.Stepper.IsTrapPresent(1), reversed.Stepper.IsTrapPresent(0));
        }

        static LevelScenario TwoAppears(bool reversed)
        {
            var s = new LevelScenario(Floor);
            var zone = new Box(3, 1, 4, 3);
            const int Delay = 40;
            if (!reversed)
            {
                s.Trap(5, 1, 2, 2, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, zone, Delay)
                    .Trap(6, 1, 2, 2, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, zone, Delay);
            }
            else
            {
                s.Trap(6, 1, 2, 2, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, zone, Delay)
                    .Trap(5, 1, 2, 2, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, zone, Delay);
            }
            s.Begin();
            int fire = s.RunUntil(Hold(1), x => x.Stepper.IsTrapPresent(0));
            Assert.That(fire, Is.GreaterThan(0), "fired");
            Assert.IsTrue(s.Stepper.IsTrapPresent(1), "both on the same tick");
            Box hitbox = Box.Hitbox(s.Previous.X, s.Previous.Y);
            Assert.IsTrue(hitbox.Overlaps(new Box(5, 1, 7, 3)) && hitbox.Overlaps(new Box(6, 1, 8, 3)),
                "precondition: the player was inside both boxes when they appeared");
            return s;
        }

        [Test]
        public void SafeGroundIsNeverInsideATriggerOrOnATrap()
        {
            LevelScenario s = new LevelScenario(Floor)
                .Trap(15, 3, 1, 1, TileKind.Solid, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(6, 1, 9, 3))
                .Trap(12, 0, 2, 1, TileKind.Solid, TrapActionKind.Vanish, TrapTriggerKind.Touch)
                .Begin();
            for (int i = 0; i < 300; i++)
            {
                s.Tick(Hold(1));
                Box hitbox = Box.Hitbox(s.Stepper.SafeX, s.Stepper.SafeY);
                Assert.IsFalse(hitbox.Overlaps(new Box(6, 1, 9, 3)), $"tick {i}: safe ground inside a zone");
                Assert.IsFalse(hitbox.Grown(LevelRules.ContactMargin).Overlaps(new Box(12, 0, 14, 1)), $"tick {i}: safe ground touching a Touch trap");
            }
            Assert.That(s.State.X, Is.GreaterThan(15f), "precondition: walked past both");
        }
    }
}
