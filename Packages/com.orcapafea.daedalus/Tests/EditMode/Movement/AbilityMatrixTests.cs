using System.Collections.Generic;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// One map (an ordinary wall, a climbable wall, a rope, a one-way, a 4-tile ledge) × ability sets:
    /// each surface responds only to the ability it is gated on.
    /// </summary>
    public class AbilityMatrixTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const Abilities J = Abilities.Jump;
        const Abilities DJ = Abilities.DoubleJump;
        const Abilities WJ = Abilities.WallJump;
        const Abilities WC = Abilities.WallClimb;
        const Abilities RC = Abilities.RopeClimb;

        // Ordinary wall at column 6, climbable wall at column 14, rope at column 20, one-way at row 5 in
        // columns 23–24, open air around column 28, and a ledge (top 5) from column 34.
        static readonly string[] Map = AsciiLevel.Rows(40, 30, (x, y) =>
        {
            if (y == 0 || (x == 6 && y < 30) || (x >= 34 && y <= 4))
            {
                return '#';
            }
            if (x == 14 && y < 26)
            {
                return 'C';
            }
            if (x == 20 && y < 26)
            {
                return 'R';
            }
            if ((x == 23 || x == 24) && y == 5)
            {
                return '-';
            }
            return x == 28 && y == 1 ? 'P' : '.';
        });

        static PlayerScenario At(Abilities abilities, float x, float y)
        {
            var p = new PlayerScenario(abilities, Map);
            p.State = p.Stepper.CreateState(x, y, abilities);
            return p;
        }

        static bool Slides(Abilities a)
        {
            PlayerScenario p = At(a, 4.5f, 20f);
            p.Tick(Hold(1), 120);
            return p.ContactsSeen.Contains(PlayerContact.WallSlide);
        }

        static bool Clings(Abilities a)
        {
            PlayerScenario p = At(a, 12.5f, 20f);
            p.Tick(Hold(1), 120);
            return p.ContactsSeen.Contains(PlayerContact.WallCling);
        }

        static bool Grabs(Abilities a)
        {
            PlayerScenario p = At(a, 20.3f, 20f);
            p.Tick(Hold(0, 1), 60);
            return p.ContactsSeen.Contains(PlayerContact.Rope);
        }

        static bool AirJumps(Abilities a)
        {
            PlayerScenario p = At(a, 28.5f, 20f);
            p.Tick(Idle, 2);
            p.Tick(Press());
            return p.State.VelocityY > 10f && p.State.AirJumpsLeft == 0;
        }

        static bool DropsThrough(Abilities a)
        {
            PlayerScenario p = At(a, 24f, 6f);
            p.Tick(Idle);
            Assert.AreEqual(PlayerContact.OneWayTop, p.State.Contact, "precondition: standing on the one-way");
            p.Tick(Press(0, -1));
            return p.State.Contact == PlayerContact.Air && p.State.Y < 6f;
        }

        // From 2.5 tiles away: jump straight up, air-jump at the apex (away from any wall), then hold toward the ledge.
        static bool ReachesFourTileLedge(Abilities a)
        {
            PlayerScenario p = At(a, 34f - 2.5f - Hw, 1f);
            p.Tick(Idle);
            p.Tick(Press());
            bool airJumped = false;
            for (int i = 0; i < 240; i++)
            {
                bool press = !airJumped && p.State.Contact == PlayerContact.Air && p.State.VelocityY <= 0;
                airJumped |= press;
                p.Tick(press ? Press() : Hold(airJumped ? 1 : 0, 0, true));
                if (p.State.IsStanding && p.State.Y >= 5f - 1e-4f)
                {
                    return true;
                }
            }
            return false;
        }

        [TestCase(J, false, false, false, false, false)]
        [TestCase(J | DJ, false, false, false, true, true)]
        [TestCase(J | WJ, true, false, false, false, false)]
        [TestCase(J | DJ | WJ, true, false, false, true, true)]
        [TestCase(J | WC, false, true, false, false, false)]
        [TestCase(J | RC, false, false, true, false, false)]
        [TestCase(J | DJ | WJ | WC | RC, true, true, true, true, true)]
        [TestCase(DJ, false, false, false, true, false)]
        public void SurfacesRespondOnlyToTheirAbility(Abilities abilities, bool slide, bool cling, bool grab, bool airJump, bool ledge)
        {
            Assert.AreEqual(slide, Slides(abilities), "wall slide on the ordinary wall");
            Assert.AreEqual(cling, Clings(abilities), "cling on the climbable wall");
            Assert.AreEqual(grab, Grabs(abilities), "rope grab");
            Assert.AreEqual(airJump, AirJumps(abilities), "air jump");
            Assert.IsTrue(DropsThrough(abilities), "drop-through needs no ability");
            Assert.AreEqual(ledge, ReachesFourTileLedge(abilities), "the 4-tile ledge (needs jump + double jump)");

            if ((abilities & (DJ | WJ)) == (DJ | WJ))
            {
                // Script B's inputs and assertions: double jump + wall jump still can't scale the ordinary wall.
                WallJumpTests.RunExploitLoop(abilities, false, out List<float> starts, out List<int> refills);
                WallJumpTests.AssertExploitBounded(starts, refills, $"matrix {abilities}");
            }
        }
    }
}
