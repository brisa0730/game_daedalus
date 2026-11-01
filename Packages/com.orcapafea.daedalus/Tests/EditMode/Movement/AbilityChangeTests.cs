using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Abilities switched on or off during play (<see cref="PlayerStepper.ChangeAbilities"/>): a gained ability
    /// works on the next tick, and a hold the new set can't keep lets go.
    /// </summary>
    public class AbilityChangeTests
    {
        const Abilities J = Abilities.Jump;

        // A climbable wall at column 6, an ordinary wall at column 14, a rope at column 20; open air around column 10.
        static readonly string[] Map = AsciiLevel.Rows(25, 15, (x, y) =>
            y == 0 || (x == 14 && y <= 10) ? '#' : x == 6 && y <= 10 ? 'C' : x == 20 && y >= 1 && y <= 10 ? 'R' : x == 10 && y == 12 ? 'P' : '.');

        static PlayerScenario At(float x, float y, Abilities abilities)
        {
            var p = new PlayerScenario(abilities, Map);
            p.State = p.Stepper.CreateState(x, y, abilities);
            return p;
        }

        [Test]
        public void DoubleJumpGainedInMidAirWorksOnTheNextPress()
        {
            PlayerScenario p = At(10.5f, 8f, J);
            p.Tick(Idle, 10);
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "precondition: falling");
            p.Tick(Press());
            Assert.That(p.State.VelocityY, Is.LessThan(0f), "no air jump without double jump");

            p.Tick(Idle);
            p.State = p.Stepper.ChangeAbilities(p.State, J | Abilities.DoubleJump);
            Assert.AreEqual(PlayerContact.Air, p.State.Contact, "nothing else changed");
            p.Tick(Press());
            Assert.That(p.State.VelocityY, Is.GreaterThan(0f), "the gained double jump fires");
        }

        [Test]
        public void LosingRopeClimbOnARopeLetsGo()
        {
            PlayerScenario p = At(20.5f, 1f, J | Abilities.RopeClimb);
            p.Tick(Hold(0, 1));
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact, "precondition: on the rope");
            p.Tick(Hold(0, 1), 20);

            p.State = p.Stepper.ChangeAbilities(p.State, J);
            Assert.AreEqual(PlayerContact.Air, p.State.Contact);
            Assert.AreEqual(-1, p.State.RopeIndex);
            p.Tick(Hold(0, 1), 60);
            Assert.AreEqual(PlayerContact.Ground, p.State.Contact, "fell to the floor, never re-grabbed");
        }

        [Test]
        public void LosingWallClimbWhileClingingLetsGo()
        {
            PlayerScenario p = At(6f - MovementInternals.HitboxHalfWidth, 1f, J | Abilities.WallClimb);
            p.Tick(Hold(1, 1));
            p.Tick(Hold(1, 1), 30);
            Assert.AreEqual(PlayerContact.WallCling, p.State.Contact, "precondition: clinging and climbing");

            p.State = p.Stepper.ChangeAbilities(p.State, J);
            Assert.AreEqual(PlayerContact.Air, p.State.Contact);
            Assert.AreEqual(0, p.State.WallSide);
            p.Tick(Hold(1, 1), 60);
            Assert.AreEqual(PlayerContact.Ground, p.State.Contact, "fell to the floor, never clung again");
        }

        [Test]
        public void LosingWallJumpWhileSlidingLetsGo()
        {
            PlayerScenario p = At(14f - MovementInternals.HitboxHalfWidth, 8f, J | Abilities.WallJump);
            int ticks = p.RunUntil(Hold(1), s => s.State.Contact == PlayerContact.WallSlide, 120);
            Assert.That(ticks, Is.GreaterThan(0), "precondition: sliding");

            p.State = p.Stepper.ChangeAbilities(p.State, J);
            Assert.AreEqual(PlayerContact.Air, p.State.Contact);
            p.Tick(Hold(1));
            Assert.AreNotEqual(PlayerContact.WallSlide, p.State.Contact, "no slide without wall jump");
        }

        [Test]
        public void GainingAnAbilityKeepsTheCurrentHold()
        {
            PlayerScenario p = At(20.5f, 1f, J | Abilities.RopeClimb);
            p.Tick(Hold(0, 1));
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact, "precondition: on the rope");
            PlayerState before = p.State;
            p.State = p.Stepper.ChangeAbilities(p.State, J | Abilities.RopeClimb | Abilities.DoubleJump);
            Assert.AreEqual(PlayerContact.Rope, p.State.Contact);
            Assert.AreEqual(before.RopeIndex, p.State.RopeIndex);
            Assert.AreEqual(before.Y, p.State.Y);
        }
    }
}
