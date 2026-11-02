using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Pins every death, respawn and trap number. A retune must change this file and
    /// Documents/GDD/entities.md together.
    /// </summary>
    public class LevelRulesTests
    {
        const float Tolerance = 1e-5f;

        [Test]
        public void ValuesArePinned()
        {
            Assert.AreEqual(0.4f, LevelRules.DeathBeatSeconds, Tolerance);
            Assert.AreEqual(24, LevelRules.DeathBeatTicks);
            Assert.AreEqual(0.2f, LevelRules.HazardInset, Tolerance);
            Assert.AreEqual(0.0625f, LevelRules.ContactMargin, Tolerance);
            Assert.AreEqual(1f, LevelRules.DefaultZoneGrowUp, Tolerance);
            Assert.AreEqual(1f, LevelRules.DefaultExitZoneGrow, Tolerance);
            Assert.AreEqual(0f, LevelRules.DefaultTrapDelay, Tolerance);
            Assert.AreEqual(0f, LevelRules.MinTrapDelay, Tolerance);
            Assert.AreEqual(3f, LevelRules.MaxTrapDelay, Tolerance);
            Assert.AreEqual(3, LevelRules.DefaultTrapWidth);
            Assert.AreEqual(1, LevelRules.DefaultTrapHeight);
            Assert.AreEqual(1, LevelRules.MinTrapSize);
            Assert.AreEqual(30, LevelRules.MaxTrapSize);
            Assert.AreEqual(1f, LevelRules.WinInputLockSeconds, Tolerance);
            Assert.AreEqual(60, LevelRules.WinInputLockTicks);
            Assert.AreEqual(0, LevelRules.DefaultMoveOffsetX);
            Assert.AreEqual(-3, LevelRules.DefaultMoveOffsetY);
            Assert.AreEqual(-30, LevelRules.MinMoveOffset);
            Assert.AreEqual(30, LevelRules.MaxMoveOffset);
            Assert.AreEqual(8f, LevelRules.DefaultMoveSpeed, Tolerance);
            Assert.AreEqual(1f, LevelRules.MinMoveSpeed, Tolerance);
            Assert.AreEqual(30f, LevelRules.MaxMoveSpeed, Tolerance);
            Assert.AreEqual(0.25f, LevelRules.CrushNudge, Tolerance);
            Assert.AreEqual(MovementInternals.CeilingCornerCorrection, LevelRules.CrushNudge, "the nudge is the jump's corner correction");
            Assert.AreEqual(0.5f, LevelRules.MaxMoveSpeed / FixedStepAccumulator.TicksPerSecond, Tolerance,
                "the largest per-tick step, which the no-skip tests rely on");
        }

        [Test]
        public void SecondsToTicksRoundsAndRejectsBadInput()
        {
            Assert.AreEqual(30, LevelRules.SecondsToTicks(0.5f));
            Assert.AreEqual(0, LevelRules.SecondsToTicks(0f));
            Assert.AreEqual(180, LevelRules.SecondsToTicks(LevelRules.MaxTrapDelay));
            Assert.AreEqual(0, LevelRules.SecondsToTicks(-1f));
            Assert.AreEqual(0, LevelRules.SecondsToTicks(float.NaN));
            Assert.AreEqual(0, LevelRules.SecondsToTicks(float.PositiveInfinity));
        }

        [Test]
        public void DefaultZoneGrowsTheBoxUpward()
        {
            Box zone = LevelRules.DefaultZone(new Box(2, 1, 5, 2));
            Assert.AreEqual(2f, zone.MinX);
            Assert.AreEqual(1f, zone.MinY);
            Assert.AreEqual(5f, zone.MaxX);
            Assert.AreEqual(3f, zone.MaxY);
        }

        [Test]
        public void BoxOverlapIsStrict()
        {
            var a = new Box(0, 0, 1, 1);
            Assert.IsFalse(a.Overlaps(new Box(1, 0, 2, 1)), "edge contact (x)");
            Assert.IsFalse(a.Overlaps(new Box(0, 1, 1, 2)), "edge contact (y)");
            Assert.IsTrue(a.Overlaps(new Box(0.999f, 0, 2, 1)), "0.001 overlap (x)");
            Assert.IsTrue(a.Overlaps(new Box(0, 0.999f, 1, 2)), "0.001 overlap (y)");
            Assert.IsTrue(a.Grown(0.01f).Overlaps(new Box(1, 0, 2, 1)), "grown reaches a neighbour");
        }

        [Test]
        public void HitboxIsTheMovementHitbox()
        {
            Box h = Box.Hitbox(2.5f, 1f);
            Assert.AreEqual(2.5f - MovementInternals.HitboxHalfWidth, h.MinX, Tolerance);
            Assert.AreEqual(1f, h.MinY, Tolerance);
            Assert.AreEqual(2.5f + MovementInternals.HitboxHalfWidth, h.MaxX, Tolerance);
            Assert.AreEqual(1f + MovementInternals.HitboxHeight, h.MaxY, Tolerance);
        }

        [Test]
        public void AnInvertedBoxThrows()
        {
            Assert.Throws<System.ArgumentException>(() => new Box(1, 0, 0, 1));
            Assert.Throws<System.ArgumentException>(() => new Box(0, 0, 1, float.NaN));
        }
    }
}
