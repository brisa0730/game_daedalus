using System.Collections.Generic;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// The level-design gates students build with, at default settings and internals, with best-case inputs.
    /// A Phase 2 retune that moves a breakpoint fails here as "gate moved", not as a bug.
    /// </summary>
    public class GateIntegrityTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const string Defaults = "(defaults: run 6, jump height 3.5, gravity 1, max fall 12, time to apex 0.35 s, fall gravity ×1.75, coyote 6 ticks)";

        // Floor, and a solid ledge `height` tall from x = 6 rightwards.
        static string[] LedgeMap(int height)
        {
            return AsciiLevel.Rows(14, 16, (x, y) => y == 0 || (x >= 6 && y <= height) ? '#' : x == 1 && y == 1 ? 'P' : '.');
        }

        static bool ReachesLedge(int height, Abilities abilities, bool airJumpAtApex)
        {
            var p = new PlayerScenario(abilities, LedgeMap(height));
            p.State = p.Stepper.CreateState(5f - Hw, 1f, abilities);
            p.Tick(Press(1));
            bool airJumped = false;
            for (int i = 0; i < 240; i++)
            {
                bool press = airJumpAtApex && !airJumped && p.State.VelocityY <= 0;
                airJumped |= press;
                p.Tick(press ? Press(1) : Hold(1, 0, true));
                if (p.State.IsStanding && p.State.Y >= height + 1 - 1e-4f)
                {
                    return true;
                }
            }
            Assert.IsTrue(!airJumpAtApex || airJumped, "precondition: the air jump was pressed");
            return false;
        }

        [TestCase(3, true)]
        [TestCase(4, false)]
        public void JumpReachesAThreeTileLedgeButNotFour(int height, bool reached)
        {
            Assert.AreEqual(reached, ReachesLedge(height, Abilities.Jump, false),
                $"gate moved: a {height}-tile ledge {(reached ? "should" : "should not")} be reachable with jump alone {Defaults}");
        }

        [TestCase(6, true)]
        [TestCase(8, false)]
        public void DoubleJumpReachesASixTileLedgeButNotEight(int height, bool reached)
        {
            Assert.AreEqual(reached, ReachesLedge(height, Abilities.Jump | Abilities.DoubleJump, true),
                $"gate moved: a {height}-tile ledge {(reached ? "should" : "should not")} be reachable with double jump {Defaults}");
        }

        [Test]
        public void DoubleJumpDoublesTheOpenAirApex()
        {
            var p = new PlayerScenario(Abilities.Jump | Abilities.DoubleJump, AsciiLevel.Rows(6, 14, (x, y) => y == 0 ? '#' : x == 2 && y == 1 ? 'P' : '.'));
            p.Tick(Press());
            p.RunUntil(Hold(0, 0, true), s => s.State.VelocityY <= 0, 60);
            p.Tick(Press());
            p.RunUntil(Hold(0, 0, true), s => s.State.IsStanding, 400);
            Assert.AreEqual(7f, p.MaxY - 1f, 0.02f, $"gate moved: double-jump apex is 2 × jump height {Defaults}");
        }

        // Same-height ground with a gap from x = 12; the jump fires on W+6, the last coyote tick.
        [TestCase(5, true)]
        [TestCase(6, false)]
        public void RunningCoyoteJumpClearsAFiveTileGapButNotSix(int gap, bool clears)
        {
            int far = 12 + gap;
            var p = new PlayerScenario(Abilities.Jump, AsciiLevel.Rows(far + 10, 20, (x, y) =>
                y < 10 && (x < 12 || x >= far) ? '#' : x == 2 && y == 10 ? 'P' : '.'));
            int w = p.RunUntil(Hold(1), s => s.State.Contact == PlayerContact.Air, 300);
            Assert.That(w, Is.GreaterThan(0), "walked off");
            Assert.AreEqual(6f, p.State.VelocityX, 1e-3f, "at full speed");
            p.Tick(Hold(1), 5);
            Assert.That(p.State.CoyoteTicks, Is.GreaterThan(0), "precondition: W+6 is still in the coyote window");
            p.Tick(Press(1));
            Assert.That(p.State.VelocityY, Is.GreaterThan(10f), "precondition: the coyote jump fired");
            float jumpX = p.Previous.X;
            int landed = p.RunUntil(Hold(1, 0, true), s => s.State.IsStanding || s.State.Y < 5f, 300);
            Assert.That(landed, Is.GreaterThan(0));
            bool cleared = p.State.IsStanding && p.State.Y == 10f && p.State.X > far - Hw;
            TestContext.WriteLine($"gap {gap}: jumped at x {jumpX:F3}, {(cleared ? "landed at x " + p.State.X.ToString("F3") : "fell")}");
            Assert.AreEqual(clears, cleared, $"gate moved: a {gap}-tile gap {(clears ? "should" : "should not")} be cleared {Defaults}");
        }

        [TestCase(2, true)]
        [TestCase(1, false)]
        public void TwoTallCorridorPassesOneTallBlocks(int height, bool passes)
        {
            var p = new PlayerScenario(Abilities.Jump, AsciiLevel.Rows(16, 8, (x, y) =>
                y == 0 || (x >= 6 && x <= 10 && y > height) ? '#' : x == 1 && y == 1 ? 'P' : '.'));
            p.Tick(Hold(1), 180);
            if (passes)
            {
                Assert.That(p.State.X, Is.GreaterThan(11f), $"gate moved: a {height}-tall corridor lets the 1.5-tall hitbox through");
            }
            else
            {
                Assert.AreEqual(6f - Hw, p.State.X, 1e-3f, $"gate moved: a {height}-tall corridor blocks at its entrance");
            }
        }

        // A shaft `width` wide between walls 12 tall (tops at 13). Press every tick, hold toward the facing wall.
        static bool ClimbsShaft(int width, Abilities abilities, out float maxY, out List<float> gains)
        {
            int left = 2, right = 3 + width;
            var p = new PlayerScenario(abilities, AsciiLevel.Rows(width + 8, 22, (x, y) =>
                y == 0 || ((x == left || x == right) && y <= 12) ? '#' : x == left + 1 && y == 1 ? 'P' : '.'));
            gains = new List<float>();
            float lastStart = float.NaN;
            for (int i = 0; i < 900; i++)
            {
                p.Tick(Press(p.State.Facing));
                if (p.State.WallLockTicks == MovementInternals.WallJumpLockTicks)
                {
                    if (!float.IsNaN(lastStart))
                    {
                        gains.Add(p.Previous.Y - lastStart);
                    }
                    lastStart = p.Previous.Y;
                }
                if (p.State.Y >= 13f)
                {
                    maxY = p.MaxY;
                    return true;
                }
            }
            maxY = p.MaxY;
            return false;
        }

        [Test]
        public void WallJumpClimbsAThreeWideShaftButNotASixWideOne()
        {
            const Abilities wallJumper = Abilities.Jump | Abilities.WallJump;
            for (int width = 1; width <= 7; width++)
            {
                bool climbed = ClimbsShaft(width, wallJumper, out float maxY, out List<float> gains);
                TestContext.WriteLine($"width {width}: {(climbed ? "climbed" : "not climbed")}, max feet {maxY:F2}, gains per jump "
                    + string.Join(", ", gains.ConvertAll(g => g.ToString("F2"))));
            }
            Assert.IsTrue(ClimbsShaft(3, wallJumper, out _, out _), $"gate moved: wall jump climbs a 3-wide, 12-tall shaft {Defaults}");
            Assert.IsFalse(ClimbsShaft(3, Abilities.Jump, out _, out _), "gate moved: a 3-wide shaft needs wall jump");
            Assert.IsFalse(ClimbsShaft(6, wallJumper, out _, out _), $"gate moved: a 6-wide shaft is too wide to climb {Defaults}");
        }

        [Test]
        public void CeilingCorrectionNeverExceedsAQuarterTile()
        {
            int corrected = 0, cases = 0;
            for (int i = 0; i <= 30; i++)
            {
                float x = 5f + i * 0.05f;
                var p = new PlayerScenario(Abilities.Jump, AsciiLevel.Rows(12, 10, (cx, cy) =>
                    cy == 0 || (cx >= 6 && cy == 4) ? '#' : cx == 1 && cy == 1 ? 'P' : '.'));
                p.State = p.Stepper.CreateState(x, 1f, Abilities.Jump);
                p.Tick(Press());
                for (int t = 0; t < 40; t++)
                {
                    p.Tick(Hold(0, 0, true));
                    float expected = p.Previous.X + (p.Previous.VelocityX + p.State.VelocityX) * 0.5f * MovementInternals.TickSeconds;
                    float shift = System.Math.Abs(p.State.X - expected);
                    Assert.That(shift, Is.LessThanOrEqualTo(MovementInternals.CeilingCornerCorrection + 1e-4f), $"start x {x}, tick {t}");
                    if (shift > 1e-4f)
                    {
                        corrected++;
                    }
                }
                cases++;
            }
            Assert.AreEqual(31, cases);
            Assert.That(corrected, Is.GreaterThan(0), "precondition: some jumps were corrected past the corner");
        }
    }
}
