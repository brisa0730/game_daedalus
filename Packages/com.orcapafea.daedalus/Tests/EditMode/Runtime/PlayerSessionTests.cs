using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The plain C# driver: clock, interpolation, the respawn snap, restarts and live settings.</summary>
    public class PlayerSessionTests
    {
        const float Tolerance = 1e-4f;
        static readonly Vector2 Origin = new Vector2(-3.5f, 2f);

        static readonly string[] Pit =
        {
            "............",
            "............",
            ".P..........",
            "####........",
            "............",
            "............",
        };

        static readonly string[] Tall =
        {
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "............",
            "......P.....",
            "............",
            "............",
            "............",
            "............",
            "############",
        };

        static PlayerSession Session(string[] rows, Abilities abilities = Abilities.Jump, RespawnRule rule = RespawnRule.SafeGround)
        {
            AsciiLevel level = AsciiLevel.Parse(rows);
            var map = new CollisionMap(level.Grid, Origin, Vector2Int.zero);
            return new PlayerSession(map, level.Ropes, new TrapSpec[0], new Box[0], new Box[0], MovementSettings.Default, rule,
                StartAt(level.StartX, level.StartY, abilities));
        }

        // Walks right into the pit; returns the ticks until the respawn tick (−1 if none within 600).
        static int WalkIntoThePitUntilRespawn(PlayerSession session, out int deathTick)
        {
            deathTick = -1;
            for (int tick = 1; tick <= 600; tick++)
            {
                Assert.AreEqual(1, session.AdvanceClock(1 / 60.0));
                session.Tick(Hold(1));
                if (deathTick < 0 && session.Phase == LevelPhase.Dying)
                {
                    deathTick = tick;
                }
                if (session.Current.Teleported)
                {
                    return tick;
                }
            }
            return -1;
        }

        static PlayStart StartAt(float simX, float simY, Abilities abilities)
        {
            return new PlayStart(new Vector2(simX, simY) + Origin, abilities);
        }

        [Test]
        public void OneFrameAtSixtyHertzIsOneTickAndAHitchIsCappedAtFive()
        {
            PlayerSession session = Session(Tall);
            Assert.AreEqual(1, session.AdvanceClock(1 / 60.0));
            Assert.AreEqual(5, session.AdvanceClock(0.5));
        }

        [Test]
        public void TheRenderedPositionLiesBetweenTheLastTwoTicksAtTheFraction()
        {
            PlayerSession session = Session(Tall);
            int framesBetween = 0;
            for (int frame = 0; frame < 30; frame++)
            {
                int ticks = session.AdvanceClock(1 / 90.0);
                for (int i = 0; i < ticks; i++)
                {
                    session.Tick(Idle);
                }
                float t = (float)session.InterpolationFraction;
                Vector2 previous = session.Map.SimToWorld(new Vector2(session.Previous.X, session.Previous.Y));
                Vector2 current = session.Map.SimToWorld(new Vector2(session.Current.X, session.Current.Y));
                Vector2 rendered = session.InterpolatedFeetWorld;
                Assert.AreEqual(Mathf.Lerp(previous.y, current.y, t), rendered.y, Tolerance, $"frame {frame}");
                Assert.AreEqual(Mathf.Lerp(previous.x, current.x, t), rendered.x, Tolerance, $"frame {frame}");
                if (t > 1e-3f && t < 1 - 1e-3f && previous.y != current.y)
                {
                    framesBetween++;
                    Assert.That(rendered.y, Is.LessThan(previous.y).And.GreaterThan(current.y), $"frame {frame}: strictly between while falling");
                }
            }
            Assert.That(framesBetween, Is.GreaterThan(5), "the fraction cycled while the player fell");
        }

        [Test]
        public void TheRespawnTickAfterTheBeatRendersExactlyAtTheStart()
        {
            PlayerSession session = Session(Pit, Abilities.Jump, RespawnRule.Checkpoint);
            Vector2 start = session.Map.SimToWorld(new Vector2(session.Current.X, session.Current.Y));
            Assert.AreEqual(start, session.StartFeetWorld);
            int respawn = WalkIntoThePitUntilRespawn(session, out int death);
            Assert.That(death, Is.GreaterThan(0), "walked into the pit and died");
            Assert.AreEqual(death + LevelRules.DeathBeatTicks, respawn, "the respawn comes after the beat");
            Assert.AreEqual(1, session.Deaths);
            Assert.AreEqual(session.Current.X, session.Previous.X, "no point between the pit and the start");
            Assert.AreEqual(session.Current.Y, session.Previous.Y);
            Assert.AreEqual(start, session.InterpolatedFeetWorld);
        }

        [Test]
        public void ALiveRuleChangeMovesTheNextRespawn()
        {
            PlayerSession session = Session(Pit, Abilities.Jump, RespawnRule.Checkpoint);
            session.SetRule(RespawnRule.SafeGround);
            Assert.That(WalkIntoThePitUntilRespawn(session, out _), Is.GreaterThan(0));
            Assert.That(session.Current.X, Is.GreaterThan(session.StartFeetWorld.x - Origin.x + 1f), "back at the ledge, not the start");
            session.SetRule(RespawnRule.Checkpoint);
            Assert.That(WalkIntoThePitUntilRespawn(session, out _), Is.GreaterThan(0));
            Assert.AreEqual(session.StartFeetWorld, session.Map.SimToWorld(new Vector2(session.Current.X, session.Current.Y)), "back at the start");
            Assert.AreEqual(2, session.Deaths);
        }

        [Test]
        public void RestartMidFallPutsThePlayerAtRestAtTheNewStartWithTheGivenAbilities()
        {
            PlayerSession session = Session(Tall);
            session.AdvanceClock(1 / 90.0 * 4);
            session.Tick(Idle);
            session.Tick(Idle);
            Assert.That(session.Current.VelocityY, Is.LessThan(0f), "precondition: falling");

            session.Restart(StartAt(2.5f, 3f, Abilities.RopeClimb));
            Assert.AreEqual(2.5f, session.Current.X);
            Assert.AreEqual(3f, session.Current.Y);
            Assert.AreEqual(0f, session.Current.VelocityY);
            Assert.AreEqual(session.Current.X, session.Previous.X);
            Assert.AreEqual(session.Current.Y, session.Previous.Y);
            Assert.AreEqual(0.0, session.InterpolationFraction);
            Assert.AreEqual(Abilities.RopeClimb | Abilities.Jump, session.Current.Abilities, "the given set plus Jump");
        }

        [Test]
        public void RestartAfterADeathPutsTheCounterBackToZero()
        {
            PlayerSession session = Session(Pit);
            Assert.That(WalkIntoThePitUntilRespawn(session, out _), Is.GreaterThan(0));
            Assert.AreEqual(1, session.Deaths, "precondition: one death");
            session.Restart(StartAt(1.5f, 3f, Abilities.Jump));
            Assert.AreEqual(0, session.Deaths);
            Assert.AreEqual(LevelPhase.Playing, session.Phase);
            Assert.AreEqual(-1, session.ActiveCheckpoint);
        }

        [Test]
        public void AStartInsideSolidIsMovedAndAnEnclosedOneIsNotFound()
        {
            PlayerSession session = Session(Tall);
            session.Restart(StartAt(5.5f, 0f, Abilities.Jump));
            Assert.IsTrue(session.LastPlacement.Moved);
            Assert.IsTrue(session.LastPlacement.Found);

            string[] buried = AsciiLevel.Rows(30, 30, (x, y) => x == 15 && y == 15 ? 'P' : '#');
            PlayerSession enclosed = Session(buried);
            Assert.IsTrue(enclosed.LastPlacement.Moved);
            Assert.IsFalse(enclosed.LastPlacement.Found);
        }

        [Test]
        public void ApplySettingsChangesTheRunTargetOnTheNextTick()
        {
            PlayerSession session = Session(Tall);
            for (int i = 0; i < 30; i++)
            {
                session.Tick(Hold(1));
            }
            Assert.AreEqual(MovementSettings.DefaultRunSpeed, session.Current.VelocityX, 1e-3f, "precondition: running at the default");
            MovementSettings settings = MovementSettings.Default;
            settings.RunSpeed = 10;
            session.ApplySettings(settings);
            session.Tick(Hold(1));
            Assert.That(session.Current.VelocityX, Is.GreaterThan(MovementSettings.DefaultRunSpeed));
        }
    }
}
