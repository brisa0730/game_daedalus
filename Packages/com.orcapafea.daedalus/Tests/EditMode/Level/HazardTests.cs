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

        // --- Moving Hazard traps (Phase 4): swept from the old box to the new one, so a fast one can't skip the core. ---

        static readonly PlayerInput Idle = new PlayerInput(0, 0, false, false);
        static readonly PlayerInput Right = new PlayerInput(1, 0, false, false);

        // 30 x 8, a floor on row 0, 'P' parked at (1, 1).
        static string[] Corridor()
        {
            return AsciiLevel.Rows(30, 8, (x, y) => y == 0 ? '#' : x == 1 && y == 1 ? 'P' : '.');
        }

        // Runs until death (or the limit) and returns the death tick and the first tick whose swept move (or, after the
        // player's own step, whose new box) overlaps the core, from the observed trap and player positions.
        static void Observe(LevelScenario s, Box start, PlayerInput input, out int died, out int expected, out bool movedByTrap)
        {
            died = -1;
            expected = -1;
            movedByTrap = false;
            for (int t = 1; t <= 300 && died < 0; t++)
            {
                Box before = Current(s, start);
                s.Tick(input);
                Box after = Current(s, start);
                if (expected < 0 && (after.MinX != before.MinX || after.MinY != before.MinY))
                {
                    Box core = TileCollision.HazardCore(s.Previous.X, s.Previous.Y);
                    bool swept = Box.SweptOverlaps(before, after.MinX - before.MinX, after.MinY - before.MinY, core);
                    bool atEnd = TileCollision.HazardCore(s.State.X, s.State.Y).Overlaps(after);
                    if (swept || atEnd)
                    {
                        expected = t;
                    }
                }
                if (s.Stepper.Phase == LevelPhase.Dying)
                {
                    died = t;
                    movedByTrap = s.State.X != s.Previous.X && input.MoveX == 0;
                }
            }
        }

        static Box Current(LevelScenario s, Box start)
        {
            return TrapRig.Current(s, 0, start);
        }

        [Test]
        public void AFastHazardMoveCannotSkipAStandingOrRunningPlayer()
        {
            // The spike slides 24 left from column 26, so it is still moving when it meets either player.
            var start = new Box(26, 1, 27, 2);
            int cases = 0;
            for (int k = 0; k < 64; k++)
            {
                // Standing at a sub-tick offset (the spike moves 0.5 per tick); fired by a jump into the zone overhead.
                float x0 = 8f + 0.5f * k / 64f;
                LevelScenario standing = new LevelScenario(Corridor())
                    .Trap(26, 1, 1, 1, TileKind.Hazard, TrapActionKind.Move, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(x0, 1f), 60, -24, 0, 30f)
                    .BeginAt(x0, 1f);
                TrapRig.JumpAndLand(standing);
                Observe(standing, start, Idle, out int died, out int expected, out bool moved);
                Assert.That(died, Is.GreaterThan(0), $"standing case {k}: killed");
                Assert.AreEqual(expected, died, $"standing case {k}: on the first tick the swept move overlaps the core");
                Assert.IsFalse(moved, $"standing case {k}: the hazard never moved the player");
                Assert.AreEqual(x0, standing.State.X, $"standing case {k}: X unchanged");
                cases++;

                // Running right at Run Speed 12 (the slider maximum) into the spike coming left.
                MovementSettings fast = MovementSettings.Default;
                fast.RunSpeed = MovementSettings.MaxRunSpeed;
                float r0 = 2f + 0.2f * k / 64f;
                LevelScenario running = new LevelScenario(Corridor()).WithSettings(fast)
                    .Trap(26, 1, 1, 1, TileKind.Hazard, TrapActionKind.Move, TrapTriggerKind.Zone, new Box(r0 + 0.5f, 1, r0 + 0.6f, 3), 0, -24, 0, 30f)
                    .BeginAt(r0, 1f);
                Observe(running, start, Right, out died, out expected, out _);
                Assert.That(died, Is.GreaterThan(0), $"running case {k}: killed");
                Assert.AreEqual(expected, died, $"running case {k}: on the first overlapping tick");
                cases++;
            }
            Assert.AreEqual(128, cases);
        }

        [TestCase(-0.001f, false)]
        [TestCase(0.001f, true)]
        public void AHazardMovePassingJustOutsideTheCoreSparesThePlayer(float intoCore, bool dies)
        {
            // A spike drops 6 down column 5 at speed 30 beside a standing player whose core edge is 0.001 from it
            // (the hitbox overlaps the column, inside the 0.2 inset).
            float x0 = 5f - (HalfWidth - Inset) + intoCore;
            LevelScenario s = new LevelScenario(Corridor())
                .Trap(5, 7, 1, 1, TileKind.Hazard, TrapActionKind.Move, TrapTriggerKind.Zone, TrapRig.ZoneOverHead(x0, 1f), 60, 0, -6, 30f)
                .BeginAt(x0, 1f);
            TrapRig.JumpAndLand(s);
            int died = s.RunUntil(Idle, v => v.Stepper.Phase == LevelPhase.Dying, 120);
            if (dies)
            {
                Assert.That(died, Is.GreaterThan(0), "0.001 inside the core: killed");
            }
            else
            {
                Assert.AreEqual(-1, died, "0.001 outside the core: spared");
                Assert.AreEqual(-6f, s.Stepper.TrapOffsetY(0), "precondition: it passed all the way down");
            }
        }
    }
}
