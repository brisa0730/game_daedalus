using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class DeathTests
    {
        const float Tolerance = 1e-4f;
        const float H = MovementInternals.HitboxHeight;
        const float Hw = MovementInternals.HitboxHalfWidth;

        // A ledge (columns 0–4) over a pit.
        static readonly string[] Pit =
        {
            "..............",
            "..............",
            ".P............",
            "#####.........",
            "..............",
        };

        // A floor with a spike strip at columns 8–9.
        static readonly string[] Spikes =
        {
            "..............",
            "..............",
            ".P......^^....",
            "##############",
        };

        static int RunToDeath(LevelScenario s, PlayerInput input)
        {
            return s.RunUntil(input, x => x.Stepper.Phase == LevelPhase.Dying);
        }

        [Test]
        public void APitKillsOnceThePlayerIsFullyBelowTheMapAndTheBeatFreezesIt()
        {
            LevelScenario s = new LevelScenario(Pit).Begin();
            Assert.That(RunToDeath(s, Hold(1)), Is.GreaterThan(0), "walked into the pit and died");
            Assert.That(s.State.Y + H, Is.LessThan(0f), "fully below the map on the death tick");
            Assert.That(s.Previous.Y + H, Is.GreaterThanOrEqualTo(0f), "not yet the tick before");
            Assert.AreEqual(1, s.Stepper.Deaths);
            Assert.AreEqual(0f, s.Stepper.DeathY, "a pit death is shown at the map bottom");

            float x = s.State.X, y = s.State.Y;
            for (int beat = 1; beat < LevelRules.DeathBeatTicks; beat++)
            {
                s.Tick(beat % 2 == 0 ? Press(1) : Hold(-1, 1, true));
                Assert.AreEqual(LevelPhase.Dying, s.Stepper.Phase, $"beat tick {beat}");
                Assert.AreEqual(x, s.State.X, $"beat tick {beat}: input changes nothing");
                Assert.AreEqual(y, s.State.Y, $"beat tick {beat}");
            }
            s.Tick(Hold(1));
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "respawn on beat tick 24");
            Assert.IsTrue(s.State.Teleported);
            Assert.AreEqual(1, s.Stepper.Deaths);
            s.Tick(Idle);
            Assert.IsFalse(s.State.Teleported, "only on the respawn tick");
        }

        [Test]
        public void SpikesKillOnTheFirstTickTheCoreTouchesThem()
        {
            LevelScenario s = new LevelScenario(Spikes).Begin();
            Assert.That(RunToDeath(s, Hold(1)), Is.GreaterThan(0));
            TileGrid grid = s.Level.Grid;
            Assert.IsTrue(TileCollision.CoreTouchesHazard(grid, s.State.X, s.State.Y), "the death tick touches");
            Assert.IsFalse(TileCollision.CoreTouchesHazard(grid, s.Previous.X, s.Previous.Y), "the tick before did not");
            Assert.That(s.State.X + Hw, Is.GreaterThan(8f + LevelRules.HazardInset), "the hitbox was past the inset");
        }

        [Test]
        public void SafeGroundReturnsToTheLastSpotFullyOnPaintedGround()
        {
            LevelScenario s = new LevelScenario(Pit).WithRule(RespawnRule.SafeGround).Begin();
            Assert.That(RunToDeath(s, Hold(1)), Is.GreaterThan(0));
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.IsTrue(s.State.Teleported, "precondition: the respawn tick");
            Assert.That(s.State.X, Is.LessThanOrEqualTo(5f - Hw + Tolerance), "fully on the ledge");
            Assert.That(s.State.X, Is.GreaterThan(4f), "the last such spot, not the start");
            Assert.AreEqual(s.Level.StartY, s.State.Y, Tolerance, "on the ledge top");
        }

        [Test]
        public void TheCheckpointRuleWithNoCheckpointReturnsToTheStart()
        {
            LevelScenario s = new LevelScenario(Pit).WithRule(RespawnRule.Checkpoint).Begin();
            Assert.That(RunToDeath(s, Hold(1)), Is.GreaterThan(0));
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.IsTrue(s.State.Teleported);
            Assert.AreEqual(s.Level.StartX, s.State.X);
            Assert.AreEqual(s.Level.StartY, s.State.Y);
        }

        [Test]
        public void ALiveRuleChangeTakesEffectAtTheNextDeath()
        {
            LevelScenario s = new LevelScenario(Pit).WithRule(RespawnRule.Checkpoint).Begin();
            s.Tick(Hold(1), 5);
            s.Stepper.SetRule(RespawnRule.SafeGround);
            Assert.That(RunToDeath(s, Hold(1)), Is.GreaterThan(0));
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.That(s.State.X, Is.GreaterThan(4f), "safe ground, not the start");
        }

        [Test]
        public void AJumpPressedDuringTheBeatAndHeldDoesNotJumpAfterRespawn()
        {
            LevelScenario s = new LevelScenario(Pit).WithRule(RespawnRule.Checkpoint).Begin();
            Assert.That(RunToDeath(s, Hold(1)), Is.GreaterThan(0));
            s.Tick(Hold(0, 0, true), LevelRules.DeathBeatTicks - 1);
            s.Tick(Press());
            Assert.IsTrue(s.State.Teleported, "the press landed on the respawn tick");
            s.Tick(Hold(0, 0, true), 20);
            Assert.AreEqual(s.Level.StartY, s.State.Y, Tolerance, "held through the respawn: no jump");
            Assert.IsTrue(s.State.IsStanding);
            s.Tick(Idle);
            s.Tick(Press());
            s.Tick(Hold(0, 0, true), 3);
            Assert.That(s.State.Y, Is.GreaterThan(s.Level.StartY + 0.5f), "a fresh press jumps");
        }

        [Test]
        public void AbilitiesChangedDuringPlaySurviveARespawn()
        {
            LevelScenario s = new LevelScenario(Pit).Begin();
            s.Stepper.ChangeAbilities(Abilities.Jump | Abilities.DoubleJump);
            Assert.That(RunToDeath(s, Hold(1)), Is.GreaterThan(0));
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.AreEqual(Abilities.Jump | Abilities.DoubleJump, s.State.Abilities);
        }

        [Test]
        public void TheCounterCountsEveryDeathAndBeginResetsIt()
        {
            LevelScenario s = new LevelScenario(Pit).WithRule(RespawnRule.Checkpoint).Begin();
            for (int i = 1; i <= 3; i++)
            {
                Assert.That(RunToDeath(s, Hold(1)), Is.GreaterThan(0), $"death {i}");
                Assert.AreEqual(i, s.Stepper.Deaths);
                s.Tick(Idle, LevelRules.DeathBeatTicks);
            }
            s.BeginAt(s.Level.StartX, s.Level.StartY);
            Assert.AreEqual(0, s.Stepper.Deaths);
        }

        [Test]
        public void APlayerPlacedOnAFloorSpikeStripLives()
        {
            // The floor runs 3 cells past each end of the strip; the map's 'P' is unused.
            var level = new LevelScenario(
                "..........",
                "..........",
                "P.........",
                "...^^^^...",
                "##########");
            level.BeginAt(4.5f, 1f);
            Assert.IsTrue(level.Stepper.LastPlacement.Moved, "precondition: the start was on the spikes");
            level.Tick(Idle, 120);
            Assert.AreEqual(0, level.Stepper.Deaths, "lives for 120 idle ticks");
            Assert.IsTrue(level.State.IsStanding);
        }
    }
}
