using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class CheckpointExitTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float H = MovementInternals.HitboxHeight;
        const float M = LevelRules.ContactMargin;
        const float E = 0.01f;

        // A floor with a pit at columns 12–15.
        static readonly string[] Floor =
        {
            "......................",
            "......................",
            ".P....................",
            "############....######",
        };

        static void Die(LevelScenario s)
        {
            Assert.That(s.RunUntil(Hold(1), x => x.Stepper.Phase == LevelPhase.Dying), Is.GreaterThan(0), "died");
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.IsTrue(s.State.Teleported, "precondition: the respawn tick");
        }

        [Test]
        public void TouchingACheckpointSetsTheRespawnPointAtItsBottomCentre()
        {
            LevelScenario s = new LevelScenario(Floor).WithRule(RespawnRule.Checkpoint).Checkpoint(new Box(6, 1, 7, 2)).Begin();
            Die(s);
            Assert.AreEqual(0, s.Stepper.ActiveCheckpoint);
            Assert.AreEqual(6.5f, s.State.X);
            Assert.AreEqual(1f, s.State.Y);
        }

        [Test]
        public void TheLatestCheckpointTouchedWinsAndRetouchingSetsItBack()
        {
            LevelScenario s = new LevelScenario(Floor).Checkpoint(new Box(4, 1, 5, 2)).Checkpoint(new Box(8, 1, 9, 2)).Begin();
            s.RunUntil(Hold(1), x => x.State.X > 10f);
            Assert.AreEqual(1, s.Stepper.ActiveCheckpoint, "the later one");
            s.RunUntil(Hold(-1), x => x.State.X < 4.5f);
            Assert.AreEqual(0, s.Stepper.ActiveCheckpoint, "touched the first again");
        }

        [Test]
        public void UnderSafeGroundATouchedCheckpointIsRecordedButNotUsed()
        {
            LevelScenario s = new LevelScenario(Floor).WithRule(RespawnRule.SafeGround).Checkpoint(new Box(6, 1, 7, 2)).Begin();
            Die(s);
            Assert.AreEqual(0, s.Stepper.ActiveCheckpoint, "recorded");
            Assert.That(s.State.X, Is.GreaterThan(10f), "back at safe ground by the pit, not at the checkpoint");
        }

        [Test]
        public void ACheckpointBuriedInTheFloorRespawnsAtTheNearestFreeSpot()
        {
            LevelScenario s = new LevelScenario(Floor).WithRule(RespawnRule.Checkpoint).Checkpoint(new Box(6, 0, 7, 1.5f)).Begin();
            Die(s);
            Assert.AreEqual(6.5f, s.State.X);
            Assert.AreEqual(1f, s.State.Y, "moved up out of the floor");
        }

        [Test]
        public void AnExitTouchWinsAndPlayAgainWaitsOneSecond()
        {
            LevelScenario s = new LevelScenario(Floor).Exit(new Box(6, 1, 7, 2)).Begin();
            Assert.That(s.RunUntil(Hold(1), x => x.Stepper.Phase == LevelPhase.Won), Is.GreaterThan(0));
            float x0 = s.State.X;
            for (int tick = 1; tick < LevelRules.WinInputLockTicks; tick++)
            {
                s.Tick(Press(1));
                Assert.IsFalse(s.Stepper.CanPlayAgain, $"tick {tick}");
            }
            s.Tick(Idle);
            Assert.IsTrue(s.Stepper.CanPlayAgain, "at 60");
            Assert.AreEqual(x0, s.State.X, "the player is no longer stepped");

            s.BeginAt(s.Level.StartX, s.Level.StartY);
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase);
            Assert.AreEqual(0, s.Stepper.Deaths);
            Assert.AreEqual(-1, s.Stepper.ActiveCheckpoint);
            Assert.AreEqual(s.Level.StartX, s.State.X);
        }

        [Test]
        public void DeathWinsOverAnExitTouchedOnTheSameTick()
        {
            // A Pop Spike over the player's path whose zone starts where the exit's touch begins:
            // entering the zone and touching the exit happen on the same tick.
            LevelScenario s = new LevelScenario(Floor)
                .Trap(6, 1, 6, 2, TileKind.Hazard, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(8, 1, 12, 4))
                .Exit(new Box(8 + M, 1, 12, 3))
                .Begin();
            Assert.That(s.RunUntil(Hold(1), x => x.Stepper.Phase != LevelPhase.Playing), Is.GreaterThan(0));
            Assert.AreEqual(LevelPhase.Dying, s.Stepper.Phase);
            Assert.IsTrue(Box.Hitbox(s.State.X, s.State.Y).Grown(M).Overlaps(new Box(8 + M, 1, 12, 3)), "precondition: the exit was touched");
        }

        [Test]
        public void TheTouchMarginWorksOnAllFourSides()
        {
            var box = new Box(5, 5, 6, 6);
            // Left, right, below, above: within the margin touches; margin + E away does not.
            AssertTouch(box, 5 - Hw - M + E, 5f, true, "left inside margin");
            AssertTouch(box, 5 - Hw - M - E, 5f, false, "left beyond margin");
            AssertTouch(box, 6 + Hw + M - E, 5f, true, "right inside margin");
            AssertTouch(box, 6 + Hw + M + E, 5f, false, "right beyond margin");
            AssertTouch(box, 5.5f, 5 - H - M + E, true, "below inside margin");
            AssertTouch(box, 5.5f, 5 - H - M - E, false, "below beyond margin");
            AssertTouch(box, 5.5f, 6 + M - E, true, "above inside margin");
            AssertTouch(box, 5.5f, 6 + M + E, false, "above beyond margin");
        }

        static void AssertTouch(Box checkpoint, float feetX, float feetY, bool expected, string message)
        {
            var s = new LevelScenario(
                "............",
                "............",
                "............",
                "............",
                "............",
                "............",
                "............",
                "............",
                "P...........").Checkpoint(checkpoint).WithSettings(Floating()).Begin();
            // Teleport-free probe: place the player by Begin (free, unmoved) and run one tick with gravity ~0.
            s.BeginAt(feetX, feetY);
            Assert.IsFalse(s.Stepper.LastPlacement.Moved, $"{message}: precondition, placed as asked");
            s.Tick(Idle);
            Assert.AreEqual(expected ? 0 : -1, s.Stepper.ActiveCheckpoint, message);
        }

        // The weakest gravity, so one tick moves the player by less than E / 2.
        static MovementSettings Floating()
        {
            MovementSettings settings = MovementSettings.Default;
            settings.Gravity = MovementSettings.MinGravity;
            return settings;
        }
    }
}
