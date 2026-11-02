using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    public class HazardTests
    {
        const float HalfWidth = MovementInternals.HitboxHalfWidth;
        const float Inset = LevelRules.HazardInset;
        const float E = 0.01f;

        static TileGrid OneSpike()
        {
            // One Hazard cell at column 3, row 3.
            return AsciiLevel.ParseGrid(
                "........",
                "........",
                "........",
                "...^....",
                "........",
                "........",
                "........");
        }

        [Test]
        public void TheCoreIsTheHitboxInsetOnEverySide()
        {
            TileGrid grid = OneSpike();
            // From the left: the hitbox's right edge reaches into the cell (left face x = 3).
            Assert.IsFalse(TileCollision.CoreTouchesHazard(grid, 3 + Inset - E - HalfWidth, 2.5f), "left, inside the inset");
            Assert.IsTrue(TileCollision.CoreTouchesHazard(grid, 3 + Inset + E - HalfWidth, 2.5f), "left, past the inset");
            // From the right (right face x = 4).
            Assert.IsFalse(TileCollision.CoreTouchesHazard(grid, 4 - Inset + E + HalfWidth, 2.5f), "right, inside the inset");
            Assert.IsTrue(TileCollision.CoreTouchesHazard(grid, 4 - Inset - E + HalfWidth, 2.5f), "right, past the inset");
            // From above: the feet sink into the cell (top y = 4).
            Assert.IsFalse(TileCollision.CoreTouchesHazard(grid, 3.5f, 4 - Inset + E), "above, inside the inset");
            Assert.IsTrue(TileCollision.CoreTouchesHazard(grid, 3.5f, 4 - Inset - E), "above, past the inset");
            // From below: the head rises into the cell (bottom y = 3).
            const float H = MovementInternals.HitboxHeight;
            Assert.IsFalse(TileCollision.CoreTouchesHazard(grid, 3.5f, 3 + Inset - E - H), "below, inside the inset");
            Assert.IsTrue(TileCollision.CoreTouchesHazard(grid, 3.5f, 3 + Inset + E - H), "below, past the inset");
        }

        [Test]
        public void AFallAtMaxSpeedCannotSkipAOneRowStrip()
        {
            // A floating spike strip in row 2 of a 20-row column.
            var rows = new string[20];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = "...";
            }
            rows[rows.Length - 1 - 2] = "^^^";
            TileGrid grid = AsciiLevel.ParseGrid(rows);
            MovementSettings settings = MovementSettings.Default;
            settings.MaxFallSpeed = MovementSettings.MaxMaxFallSpeed;
            settings.Gravity = MovementSettings.MaxGravity;
            var stepper = new PlayerStepper(grid, new RopeSegment[0], settings);
            var idle = new PlayerInput(0, 0, false, false);

            int checkedCases = 0;
            for (int k = 0; k < 64; k++)
            {
                PlayerState s = stepper.CreateState(1.5f, 14f + k / 64f, Abilities.Jump);
                bool touched = false;
                float speedNearStrip = 0;
                // Until the core is fully below the strip (its bottom is y = 2).
                for (int t = 0; t < 600 && s.Y + MovementInternals.HitboxHeight - Inset > 2f; t++)
                {
                    s = stepper.Step(s, idle);
                    if (s.Y < 4f && speedNearStrip == 0)
                    {
                        speedNearStrip = -s.VelocityY;
                    }
                    touched |= TileCollision.CoreTouchesHazard(grid, s.X, s.Y);
                }
                Assert.AreEqual(MovementSettings.MaxMaxFallSpeed, speedNearStrip, 1e-3f, $"case {k} reaches the strip at max fall speed");
                Assert.IsTrue(touched, $"case {k}: the core touched the strip");
                checkedCases++;
            }
            Assert.AreEqual(64, checkedCases);
        }

        [Test]
        public void AWallJumpPushCannotSkipAOneColumnWall()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                ".....^......",
                ".....^......",
                ".....^......",
                ".....^......");
            // The fastest sideways speed: a wall-jump push-off at max run speed, sampled once per tick.
            float perTick = MovementSettings.MaxRunSpeed * MovementInternals.WallJumpPushFactor * MovementInternals.TickSeconds;
            Assert.AreEqual(0.3f, perTick, 1e-4f);
            int checkedCases = 0;
            for (int k = 0; k < 64; k++)
            {
                bool touched = false;
                for (float x = 2f + k / 64f; x < 10f; x += perTick)
                {
                    touched |= TileCollision.CoreTouchesHazard(grid, x, 1f);
                }
                Assert.IsTrue(touched, $"case {k}");
                checkedCases++;
            }
            Assert.AreEqual(64, checkedCases);
        }

        [Test]
        public void StandsFullyOnPaintedNeedsGroundUnderEveryColumn()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "##.#--");
            Assert.IsTrue(TileCollision.StandsFullyOnPainted(grid, 1f, 1f, HalfWidth), "centred on a floor");
            Assert.IsFalse(TileCollision.StandsFullyOnPainted(grid, 2f, 1f, HalfWidth), "one column over the gap");
            Assert.IsTrue(TileCollision.StandsFullyOnPainted(grid, 5f, 1f, HalfWidth), "on a one-way");
            Assert.IsFalse(TileCollision.StandsFullyOnPainted(grid, 1f, 1.5f, HalfWidth), "mid-air");
        }

        [Test]
        public void SpikesAreNotGroundToStandOn()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "^^^^^^");
            Assert.IsFalse(TileCollision.StandsFullyOnPainted(grid, 2f, 1f, HalfWidth));
        }
    }
}
