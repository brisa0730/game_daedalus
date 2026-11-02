using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class RunAndFallTests
    {
        const float Tolerance = 1e-3f;
        const float Hw = MovementInternals.HitboxHalfWidth;

        static readonly string[] Floor =
        {
            "........................................",
            "........................................",
            "........................................",
            ".P......................................",
            "########################################",
        };

        // Open air: the player falls for well over a second before leaving the map.
        static string[] Sky()
        {
            var rows = new string[60];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = "........................................";
            }
            rows[1] = "....................P...................";
            return rows;
        }

        [Test]
        public void GroundRunAcceleratesOneTilePerSecondEachTick()
        {
            var p = new PlayerScenario(Abilities.Jump, Floor);
            for (int tick = 1; tick <= 6; tick++)
            {
                p.Tick(Hold(1));
                Assert.AreEqual(tick, p.State.VelocityX, Tolerance, $"tick {tick}");
            }
            p.Tick(Hold(1), 10);
            Assert.AreEqual(6f, p.State.VelocityX, Tolerance);
            Assert.AreEqual(PlayerContact.Ground, p.State.Contact);
        }

        [Test]
        public void GroundReleaseAndReverse()
        {
            var p = new PlayerScenario(Abilities.Jump, Floor);
            p.Tick(Hold(1), 10);
            float[] release = { 4.5f, 3f, 1.5f, 0f };
            foreach (float v in release)
            {
                p.Tick(Idle);
                Assert.AreEqual(v, p.State.VelocityX, Tolerance, "release (decel 90)");
            }

            p.Tick(Hold(1), 10);
            float[] reverse = { 4f, 2f, 0f, -1f, -2f, -3f, -4f, -5f, -6f, -6f };
            foreach (float v in reverse)
            {
                p.Tick(Hold(-1));
                Assert.AreEqual(v, p.State.VelocityX, Tolerance, "reverse (turnaround 120, then accel 60)");
            }
        }

        [Test]
        public void AirRatesAreThreeQuartersOfGround()
        {
            var p = new PlayerScenario(Abilities.Jump, Sky());
            for (int tick = 1; tick <= 8; tick++)
            {
                p.Tick(Hold(1));
                Assert.AreEqual(0.75f * tick, p.State.VelocityX, Tolerance, $"air accel tick {tick}");
            }
            float[] release = { 4.875f, 3.75f, 2.625f, 1.5f, 0.375f, 0f };
            foreach (float v in release)
            {
                p.Tick(Idle);
                Assert.AreEqual(v, p.State.VelocityX, Tolerance, "air decel 67.5");
            }

            p = new PlayerScenario(Abilities.Jump, Sky());
            p.Tick(Hold(1), 8);
            float[] reverse = { 4.5f, 3f, 1.5f, 0f, -0.75f };
            foreach (float v in reverse)
            {
                p.Tick(Hold(-1));
                Assert.AreEqual(v, p.State.VelocityX, Tolerance, "air turnaround 90, then accel 45");
            }
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "the whole check ran in the air");
        }

        [Test]
        public void ZeroTimeToFullSpeedReachesRunSpeedInOneTick()
        {
            MovementSettings s = MovementSettings.Default;
            s.TimeToFullSpeed = 0;
            var p = new PlayerScenario(Abilities.Jump, s, Floor);
            p.Tick(Hold(1));
            Assert.AreEqual(6f, p.State.VelocityX, Tolerance);
        }

        [Test]
        public void SpawnedOnTheGroundStaysPut()
        {
            var p = new PlayerScenario(Abilities.Jump, Floor);
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "CreateState starts in the air");
            p.Tick(Idle);
            Assert.AreEqual(PlayerContact.Ground, p.State.Contact, "ground on tick 1");
            p.Tick(Idle, 30);
            Assert.AreEqual(1.5f, p.State.X);
            Assert.AreEqual(1f, p.State.Y);
            Assert.AreEqual(0f, p.State.VelocityY);
        }

        [Test]
        public void FallingReachesMaxFallSpeedAndStaysThere()
        {
            var p = new PlayerScenario(Abilities.Jump, Sky());
            p.Tick(Idle, 7);
            Assert.That(p.State.VelocityY, Is.GreaterThan(-12f), "still accelerating after 7 ticks (100 tiles/s²)");
            p.Tick(Idle);
            Assert.AreEqual(-12f, p.State.VelocityY, Tolerance);
            p.Tick(Idle, 30);
            Assert.AreEqual(-12f, p.State.VelocityY, Tolerance);
        }

        [Test]
        public void WalkingOffALedgeGivesCoyoteTime()
        {
            var p = new PlayerScenario(Abilities.Jump,
                "............",
                "............",
                "............",
                ".P..........",
                "#####.......",
                "#####.......");
            int w = p.RunUntil(Hold(1), s => s.State.Contact == PlayerContact.Air);
            Assert.That(w, Is.GreaterThan(1), "walked off");
            Assert.IsTrue(p.Previous.IsStanding);
            Assert.AreEqual(MovementInternals.CoyoteTicks, p.State.CoyoteTicks);
            Assert.That(p.State.X - Hw, Is.GreaterThan(5f), "the hitbox has left the ledge");
        }

        [Test]
        public void RunningIntoAWallStopsFlush()
        {
            var p = new PlayerScenario(Abilities.Jump,
                "..........",
                "..........",
                "..........",
                ".P.....#..",
                "##########");
            p.Tick(Hold(1), 120);
            Assert.AreEqual(7f - Hw, p.State.X, Tolerance);
            Assert.AreEqual(0f, p.State.VelocityX);
        }

        [Test]
        public void HeadBumpZeroesUpwardSpeed()
        {
            var p = new PlayerScenario(Abilities.Jump,
                "......",
                "######",
                "......",
                "......",
                "......",
                ".P....",
                "######");
            p.Tick(Press());
            int bump = p.RunUntil(Hold(0, 0, true), s => s.State.Y + MovementInternals.HitboxHeight >= 5f - 1e-4f, 60);
            Assert.That(bump, Is.GreaterThan(0), "the head reached the ceiling");
            Assert.AreEqual(0f, p.State.VelocityY, "upward speed zeroed on the bump");
            Assert.AreEqual(5f - MovementInternals.HitboxHeight, p.State.Y, Tolerance);
            p.Tick(Hold(0, 0, true));
            Assert.That(p.State.VelocityY, Is.LessThan(0f), "falls after the bump");
        }

        [Test]
        public void MapSidesBlockAndTheTopIsACeiling()
        {
            var p = new PlayerScenario(Abilities.Jump,
                "......",
                "......",
                ".P....",
                "######");
            p.Tick(Hold(-1), 60);
            Assert.AreEqual(Hw, p.State.X, Tolerance, "left side");
            p.Tick(Hold(1), 120);
            Assert.AreEqual(6f - Hw, p.State.X, Tolerance, "right side");

            p.Tick(Press());
            p.RunUntil(Hold(0, 0, true), s => s.State.VelocityY <= 0, 60);
            Assert.AreEqual(4f, p.MaxY + MovementInternals.HitboxHeight, Tolerance, "the head stopped at the map top");
        }

        [Test]
        public void TheStepperAloneKeepsFallingBelowTheMap()
        {
            // Pits are deaths in the world step (DeathTests); the player's stepper itself never resets.
            var p = new PlayerScenario(Abilities.Jump,
                "............",
                ".P..........",
                "####........",
                "............",
                "............",
                "............");
            int below = p.RunUntil(Hold(1), s => s.State.Y + MovementInternals.HitboxHeight < 0);
            Assert.That(below, Is.GreaterThan(0), "walked into the pit and fell below the map");
            p.Tick(Idle, 120);
            Assert.That(p.State.Y, Is.LessThan(-20f), "still falling");
            Assert.AreEqual(-MovementSettings.DefaultMaxFallSpeed, p.State.VelocityY, Tolerance, "at max fall speed");
            Assert.IsFalse(p.State.Teleported);
        }

        [Test]
        public void TeleportedStaysFalseInOrdinaryPlay()
        {
            var p = new PlayerScenario(Abilities.Jump, Floor);
            p.Tick(Hold(1), 20);
            Assert.IsFalse(p.State.Teleported);
            p.Tick(Press(1));
            for (int tick = 0; tick < 60; tick++)
            {
                p.Tick(Hold(-1, 0, tick < 10));
                Assert.IsFalse(p.State.Teleported, $"tick {tick}");
            }
            CollectionAssert.IsSupersetOf(p.ContactsSeen, new[] { PlayerContact.Air, PlayerContact.Ground }, "ran and jumped");
        }

        [Test]
        public void ApplySettingsChangesTheRunTargetLive()
        {
            var p = new PlayerScenario(Abilities.Jump, Floor);
            p.Tick(Hold(1), 10);
            Assert.AreEqual(6f, p.State.VelocityX, Tolerance);
            MovementSettings s = MovementSettings.Default;
            s.RunSpeed = 10;
            p.Stepper.ApplySettings(s);
            p.Tick(Hold(1));
            Assert.That(p.State.VelocityX, Is.GreaterThan(6f).And.LessThan(10f));
            p.Tick(Hold(1), 5);
            Assert.AreEqual(10f, p.State.VelocityX, Tolerance);
        }
    }
}
