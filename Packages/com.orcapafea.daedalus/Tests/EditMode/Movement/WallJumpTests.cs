using System.Collections.Generic;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class WallJumpTests
    {
        const float Tolerance = 1e-3f;
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float H = MovementInternals.HitboxHeight;
        const float Reach = MovementInternals.WallJumpReach;
        const Abilities WallJumper = Abilities.Jump | Abilities.WallJump;

        // A floor and an ordinary wall in column 10 (face at x = 10) up to the map top.
        internal static string[] WallMap(int startCol, int startRow, int height = 40)
        {
            return AsciiLevel.Rows(14, height, (x, y) =>
                y == 0 || x == 10 ? '#' : x == startCol && y == startRow ? 'P' : '.');
        }

        internal static float GapToWall(PlayerScenario p, in PlayerState s, int side)
        {
            return TileCollision.WallGap(p.Level.Grid, s.X, s.Y, Hw, H, side, 1f, true);
        }

        static void SlideOnTheWall(PlayerScenario p)
        {
            int ticks = p.RunUntil(Hold(1), s => s.State.Contact == PlayerContact.WallSlide, 200);
            Assert.That(ticks, Is.GreaterThan(0), "reached a wall slide");
        }

        static bool WallJumped(PlayerScenario p)
        {
            return p.State.WallLockTicks == MovementInternals.WallJumpLockTicks;
        }

        [Test]
        public void SlideStartsWhileFallingAndPressingInAndCapsItsSpeed()
        {
            var p = new PlayerScenario(WallJumper, WallMap(8, 20));
            SlideOnTheWall(p);
            Assert.That(p.State.VelocityY, Is.LessThanOrEqualTo(0f));
            Assert.AreEqual(1, p.State.WallSide);
            p.Tick(Hold(1), 30);
            Assert.AreEqual(PlayerContact.WallSlide, p.State.Contact);
            Assert.AreEqual(-2f, p.State.VelocityY, Tolerance, "slide speed caps at 2");
        }

        [Test]
        public void NoSlideWhileRisingOrWithoutPressingIn()
        {
            var p = new PlayerScenario(WallJumper, WallMap(8, 1));
            p.Tick(Hold(1), 60);
            Assert.AreEqual(10f - Hw, p.State.X, Tolerance, "standing against the wall");

            p.Tick(Press());
            p.RunUntil(Hold(0, 0, true), s => s.State.IsStanding, 200);
            Assert.IsFalse(p.ContactsSeen.Contains(PlayerContact.WallSlide), "neutral: no slide");

            p.Tick(Press(1));
            int slid = p.RunUntil(Hold(1, 0, true), s =>
            {
                if (s.State.VelocityY > 0)
                {
                    Assert.AreNotEqual(PlayerContact.WallSlide, s.State.Contact, "no slide while rising");
                }
                return s.State.Contact == PlayerContact.WallSlide;
            }, 200);
            Assert.That(slid + 1, Is.GreaterThanOrEqualTo(21), "the slide started at the apex (tick 21), not while rising");
        }

        [TestCase(6, true)]
        [TestCase(7, false)]
        public void WallCoyoteAllowsAWallJumpForSixTicksAfterLeaving(int pressTick, bool jumps)
        {
            var p = new PlayerScenario(WallJumper, WallMap(8, 20));
            SlideOnTheWall(p);
            p.Tick(Hold(-1));
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "the slide ended on tick E");
            Assert.AreEqual(MovementInternals.WallCoyoteTicks, p.State.WallCoyoteTicks);
            p.Tick(Hold(-1), pressTick - 1);
            Assert.That(GapToWall(p, p.State, 1), Is.GreaterThan(Reach), "the wall is out of reach: only wall coyote can fire");
            p.Tick(Press(-1));
            if (jumps)
            {
                Assert.IsTrue(WallJumped(p), $"a wall jump on E+{pressTick}");
                Assert.AreEqual(-9f, p.State.VelocityX, Tolerance, "pushed away from the wall");
            }
            else
            {
                Assert.IsFalse(WallJumped(p), $"no wall jump on E+{pressTick}");
                Assert.That(p.State.VelocityY, Is.LessThan(0f));
            }
        }

        [Test]
        public void WallJumpPushesAwayAndLocksHorizontalInputForTwelveTicks()
        {
            var p = new PlayerScenario(WallJumper, WallMap(8, 20));
            SlideOnTheWall(p);
            p.Tick(Press(1));
            Assert.IsTrue(WallJumped(p), "jump on J");
            Assert.AreEqual(-9f, p.State.VelocityX, Tolerance);
            Assert.AreEqual(-1, p.State.Facing);
            for (int i = 1; i <= 12; i++)
            {
                p.Tick(Hold(1, 0, true));
                Assert.AreEqual(-9f, p.State.VelocityX, Tolerance, $"J+{i}: pressing back changes nothing");
            }
            p.Tick(Hold(1, 0, true));
            Assert.AreEqual(-7.5f, p.State.VelocityX, Tolerance, "J+13: air turnaround applies");
        }

        [Test]
        public void BufferedPressFiresAsAWallJump()
        {
            var p = new PlayerScenario(WallJumper, WallMap(8, 20));
            int near = p.RunUntil(Hold(1), s => GapToWall(s, s.State, 1) <= 0.4f, 200);
            Assert.That(near, Is.GreaterThan(0));
            Assert.That(GapToWall(p, p.State, 1), Is.GreaterThan(Reach), "out of reach when pressed");
            p.Tick(Press(1));
            Assert.AreEqual(MovementInternals.JumpBufferTicks, p.State.BufferTicks, "buffered");
            int fired = p.RunUntil(Hold(1, 0, true), WallJumped, 6);
            Assert.That(fired, Is.GreaterThan(0), "the buffer fired a wall jump");
        }

        [TestCase(0.125f, true)]
        [TestCase(0.2f, false)]
        public void WallJumpReach(float gap, bool jumps)
        {
            var p = new PlayerScenario(WallJumper, WallMap(2, 20));
            p.State = p.Stepper.CreateState(10f - Hw - gap, 20f, WallJumper);
            p.Tick(Idle, 3);
            Assert.AreEqual(gap, GapToWall(p, p.State, 1), Tolerance);
            p.Tick(Press());
            Assert.AreEqual(jumps, WallJumped(p), $"gap {gap}");
        }

        [TestCase(true)]
        [TestCase(false)]
        public void SlideEntryRefillsAirJumpsButMereContactDoesNot(bool wallJump)
        {
            Abilities abilities = Abilities.Jump | Abilities.DoubleJump | (wallJump ? Abilities.WallJump : Abilities.None);
            var p = new PlayerScenario(abilities, WallMap(6, 20));
            p.Tick(Idle, 2);
            p.Tick(Press());
            Assert.AreEqual(0, p.State.AirJumpsLeft, "the air jump was spent");
            int touched = p.RunUntil(Hold(1), s => GapToWall(s, s.State, 1) <= 1e-4f && s.State.VelocityY < 0, 300);
            Assert.That(touched, Is.GreaterThan(0), "touching the wall while falling");
            Assert.AreEqual(wallJump ? 1 : 0, p.State.AirJumpsLeft);
            Assert.AreEqual(wallJump, p.State.Contact == PlayerContact.WallSlide);
        }

        [Test]
        public void SameWallDoesNotRefillButTheOtherChimneyWallAndLandingDo()
        {
            const Abilities abilities = Abilities.Jump | Abilities.DoubleJump | Abilities.WallJump;
            var p = new PlayerScenario(abilities, WallMap(8, 20));
            SlideOnTheWall(p);
            p.Tick(Press(1));
            p.Tick(Press(1));
            Assert.AreEqual(0, p.State.AirJumpsLeft, "air jump spent right after the wall jump");
            int back = p.RunUntil(Hold(1, 0, true), s => s.State.Contact == PlayerContact.WallSlide, 300);
            Assert.That(back, Is.GreaterThan(0), "back on the same wall");
            Assert.AreEqual(0, p.State.AirJumpsLeft, "same face: no refill");
            p.RunUntil(Idle, s => s.State.IsStanding, 600);
            Assert.AreEqual(1, p.State.AirJumpsLeft, "landing refills");

            // Chimney: walls at columns 4 and 8 (faces at 5 and 8).
            var c = new PlayerScenario(abilities, AsciiLevel.Rows(13, 40, (x, y) =>
                y == 0 || x == 4 || x == 8 ? '#' : x == 7 && y == 25 ? 'P' : '.'));
            SlideOnTheWall(c);
            c.Tick(Press(1));
            c.Tick(Press(-1));
            Assert.AreEqual(0, c.State.AirJumpsLeft);
            int other = c.RunUntil(Hold(-1, 0, true), s => s.State.Contact == PlayerContact.WallSlide, 300);
            Assert.That(other, Is.GreaterThan(0), "slid on the other wall");
            Assert.AreEqual(-1, c.State.WallSide);
            Assert.AreEqual(1, c.State.AirJumpsLeft, "the other wall refills");
        }

        [Test]
        public void InTheAirNearAWallAWallJumpBeatsAnAirJump()
        {
            const Abilities abilities = Abilities.Jump | Abilities.DoubleJump | Abilities.WallJump;
            var p = new PlayerScenario(abilities, WallMap(9, 20));
            p.Tick(Idle, 3);
            p.Tick(Press());
            Assert.IsTrue(WallJumped(p));
            Assert.AreEqual(1, p.State.AirJumpsLeft, "no air jump spent");
        }

        [Test]
        public void InCoyoteNextToAWallAGroundJumpBeatsAWallJump()
        {
            // A ledge (top at 4), a one-wide pit, and a wall beside the pit below the ledge's height.
            var p = new PlayerScenario(WallJumper, AsciiLevel.Rows(12, 20, (x, y) =>
                y < 4 && (x <= 4 || x == 6) ? '#' : x == 1 && y == 4 ? 'P' : '.'));
            int w = p.RunUntil(Hold(1), s => s.State.Contact == PlayerContact.Air, 200);
            Assert.That(w, Is.GreaterThan(0), "walked off");
            int touching = p.RunUntil(Hold(1), s => GapToWall(s, s.State, 1) <= Reach, 4);
            Assert.That(touching, Is.GreaterThan(0), "a wall in reach while coyote is still active");
            Assert.That(p.State.CoyoteTicks, Is.GreaterThan(0));
            p.Tick(Press(1));
            Assert.That(p.State.VelocityY, Is.GreaterThan(10f), "jumped");
            Assert.IsFalse(WallJumped(p), "a ground (coyote) jump, not a wall jump");
            Assert.That(p.State.VelocityX, Is.GreaterThanOrEqualTo(0f), "no push-off away from the wall");
        }

        // Script A: DoubleJump off. Press every tick and hold toward the wall; each wall jump fires with the
        // wall within reach, before contact. Each must start lower than the one before.
        [Test]
        public void ScriptA_RepeatedWallJumpsLoseHeight()
        {
            var p = new PlayerScenario(WallJumper, WallMap(8, 30));
            SlideOnTheWall(p);
            var starts = new List<float>();
            for (int tick = 0; tick < 2000 && starts.Count < 4; tick++)
            {
                p.Tick(Press(1));
                if (WallJumped(p))
                {
                    Assert.That(GapToWall(p, p.Previous, 1), Is.LessThanOrEqualTo(Reach + 1e-4f), "fired with the wall in reach");
                    if (starts.Count > 0)
                    {
                        Assert.AreEqual(PlayerContact.Air, p.Previous.Contact, "fired from the air, before contact");
                        Assert.That(GapToWall(p, p.Previous, 1), Is.GreaterThan(0f), "fired before contact");
                    }
                    starts.Add(p.Previous.Y);
                }
            }
            Assert.AreEqual(4, starts.Count, "four wall jumps");
            TestContext.WriteLine("Script A starts: " + string.Join(", ", starts.ConvertAll(y => y.ToString("F3"))));
            for (int i = 1; i < starts.Count; i++)
            {
                TestContext.WriteLine($"  loss {i}: {starts[i - 1] - starts[i]:F3}");
                Assert.That(starts[i], Is.LessThanOrEqualTo(starts[i - 1] - 0.1f),
                    $"gate (defaults): wall jump {i + 1} starts at least 0.1 below wall jump {i}; no single-wall scaling");
            }
        }

        // Script B (the exploit loop): DoubleJump on. Wall-jump only from an active slide, air-jump at each apex,
        // hold toward the wall. Rule (b) stops a slide on the same face from refilling the air jump.
        [Test]
        public void ScriptB_DoubleJumpCannotScaleOneWall()
        {
            RunExploitLoop(Abilities.Jump | Abilities.DoubleJump | Abilities.WallJump, false, out List<float> starts, out List<int> refills);
            AssertExploitBounded(starts, refills, "Script B");
        }

        // Script C: as B, but each wall jump fires from wall coyote after stepping off, with no wall in reach.
        [Test]
        public void ScriptC_CoyoteDetourDoesNotRefill()
        {
            RunExploitLoop(Abilities.Jump | Abilities.DoubleJump | Abilities.WallJump, true, out List<float> starts, out List<int> refills);
            AssertExploitBounded(starts, refills, "Script C");
        }

        internal static void RunExploitLoop(Abilities abilities, bool coyoteDetour, out List<float> starts, out List<int> airJumpsAtSlideEntry)
        {
            var p = new PlayerScenario(abilities, WallMap(8, 20, 48));
            starts = new List<float>();
            airJumpsAtSlideEntry = new List<int>();
            bool sinceJump = false;      // a wall jump has fired this cycle
            bool apexPressed = false;
            int awayTicks = -1;          // ≥ 0 while stepping off the wall (Script C)
            for (int tick = 0; tick < 4000 && starts.Count < 4; tick++)
            {
                PlayerInput input;
                if (awayTicks >= 0)
                {
                    awayTicks++;
                    input = awayTicks > 5 ? Press(-1) : Hold(-1, 0, true);
                }
                else if (p.State.Contact == PlayerContact.WallSlide)
                {
                    if (coyoteDetour)
                    {
                        awayTicks = 0;
                        input = Hold(-1, 0, true);
                    }
                    else
                    {
                        input = Press(1);
                    }
                }
                else if (sinceJump && !apexPressed && p.State.VelocityY <= 0)
                {
                    apexPressed = true;
                    input = Press(1);
                }
                else
                {
                    input = Hold(1, 0, true);
                }

                p.Tick(input);

                if (p.Previous.Contact != PlayerContact.WallSlide && p.State.Contact == PlayerContact.WallSlide)
                {
                    airJumpsAtSlideEntry.Add(p.State.AirJumpsLeft);
                }
                if (WallJumped(p))
                {
                    if (coyoteDetour)
                    {
                        Assert.That(GapToWall(p, p.Previous, 1), Is.GreaterThan(Reach), "a wall-coyote jump with no wall in reach");
                        Assert.AreEqual(PlayerContact.Air, p.Previous.Contact);
                        awayTicks = -1;
                    }
                    else
                    {
                        Assert.AreEqual(PlayerContact.WallSlide, p.Previous.Contact,
                            "precondition: the cycle reached a wall slide (after a retune, a buffered apex press can fire a wall jump "
                            + "from the air if the wall comes within reach within 6 ticks of the apex: re-time the script, the gate may not have moved)");
                    }
                    Assert.AreEqual(1, p.State.LastWallJumpSide);
                    Assert.AreEqual(10, p.State.LastWallJumpFace);
                    starts.Add(p.Previous.Y);
                    sinceJump = true;
                    apexPressed = false;
                }
            }
        }

        internal static void AssertExploitBounded(List<float> starts, List<int> airJumpsAtSlideEntry, string script)
        {
            Assert.AreEqual(4, starts.Count, $"{script}: four wall jumps");
            TestContext.WriteLine($"{script} starts: " + string.Join(", ", starts.ConvertAll(y => y.ToString("F3"))));
            TestContext.WriteLine($"{script} air jumps at slide entry: " + string.Join(", ", airJumpsAtSlideEntry));
            Assert.That(starts[1] - starts[0], Is.LessThanOrEqualTo(7f), $"{script}: the first cycle gains at most the one air jump the player started with");
            for (int i = 2; i < starts.Count; i++)
            {
                Assert.That(starts[i], Is.LessThanOrEqualTo(starts[i - 1] - 0.1f),
                    $"{script} gate (defaults): cycle {i + 1} starts at least 0.1 below cycle {i}; double jump + wall jump can't scale one wall");
            }
            Assert.That(airJumpsAtSlideEntry.Count, Is.GreaterThanOrEqualTo(4), $"{script}: slide entries recorded");
            for (int i = 1; i < airJumpsAtSlideEntry.Count; i++)
            {
                Assert.AreEqual(0, airJumpsAtSlideEntry[i], $"{script}: slide {i + 1} on the face just jumped from must not refill");
            }
        }
    }
}
