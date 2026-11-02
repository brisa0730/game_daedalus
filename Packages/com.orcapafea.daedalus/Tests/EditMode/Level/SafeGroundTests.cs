using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    public class SafeGroundTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float Tolerance = 1e-4f;

        [Test]
        public void NotRecordedMidAir()
        {
            LevelScenario s = new LevelScenario(
                "..........",
                "..........",
                "..........",
                "..........",
                ".P........",
                "##########").Begin();
            s.Tick(Idle, 5);
            float safeX = s.Stepper.SafeX;
            s.Tick(Press(1));
            int air = 0;
            for (int i = 0; i < 20; i++)
            {
                s.Tick(Hold(1, 0, true));
                if (s.State.Contact == PlayerContact.Air)
                {
                    air++;
                    Assert.AreEqual(safeX, s.Stepper.SafeX, $"tick {i}: unchanged mid-air");
                }
            }
            Assert.That(air, Is.GreaterThan(10), "precondition: in the air");
        }

        [Test]
        public void NotRecordedOnALedgeEdgeWithAColumnOverAir()
        {
            LevelScenario s = new LevelScenario(
                "..........",
                "..........",
                ".P........",
                "#####.....",
                "..........").Begin();
            int over = s.RunUntil(Hold(1), x => x.State.IsStanding && x.State.X - Hw < 5f && x.State.X + Hw > 5f);
            Assert.That(over, Is.GreaterThan(0), "precondition: standing with one column over air");
            Assert.That(s.Stepper.SafeX, Is.LessThanOrEqualTo(5f - Hw + Tolerance));
        }

        [Test]
        public void RecordedOnAOneWay()
        {
            LevelScenario s = new LevelScenario(
                "..........",
                "..........",
                ".P........",
                "----------",
                "..........").Begin();
            s.Tick(Hold(1), 20);
            Assert.AreEqual(PlayerContact.OneWayTop, s.State.Contact, "precondition: on the one-way");
            Assert.AreEqual(s.State.X, s.Stepper.SafeX, "a one-way is painted ground");
        }

        [Test]
        public void NotRecordedWhileDying()
        {
            LevelScenario s = new LevelScenario(
                "..........",
                "..........",
                ".P...^....",
                "##########").Begin();
            Assert.That(s.RunUntil(Hold(1), x => x.Stepper.Phase == LevelPhase.Dying), Is.GreaterThan(0));
            float safeX = s.Stepper.SafeX;
            s.Tick(Idle, LevelRules.DeathBeatTicks - 1);
            Assert.AreEqual(safeX, s.Stepper.SafeX);
            Assert.That(safeX + LevelRules.HazardInset - Hw, Is.LessThan(5f), "the safe spot doesn't touch the spike");
        }
    }
}
