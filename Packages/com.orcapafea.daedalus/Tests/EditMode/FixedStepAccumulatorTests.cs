using System;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    public class FixedStepAccumulatorTests
    {
        const double Tolerance = 1e-9;

        [Test]
        public void OneSixtiethOfASecondIsOneTick()
        {
            var clock = new FixedStepAccumulator();
            Assert.AreEqual(1, clock.Advance(1.0 / 60));
            Assert.AreEqual(0, clock.InterpolationFraction, Tolerance);
        }

        [Test]
        public void OneThirtiethOfASecondIsTwoTicks()
        {
            var clock = new FixedStepAccumulator();
            Assert.AreEqual(2, clock.Advance(1.0 / 30));
        }

        [Test]
        public void HalfATickCarriesOver()
        {
            var clock = new FixedStepAccumulator();
            Assert.AreEqual(0, clock.Advance(0.5 / 60));
            Assert.AreEqual(0.5, clock.InterpolationFraction, Tolerance);
            Assert.AreEqual(1, clock.Advance(0.5 / 60));
            Assert.AreEqual(0, clock.InterpolationFraction, Tolerance);
        }

        [Test]
        public void HitchRunsAtMostFiveTicksAndDropsTheRest()
        {
            var clock = new FixedStepAccumulator();
            Assert.AreEqual(5, clock.Advance(60.5 / 60));
            Assert.AreEqual(0.5, clock.InterpolationFraction, Tolerance);

            Assert.AreEqual(0, clock.Advance(0));
            Assert.AreEqual(0.5, clock.InterpolationFraction, Tolerance);
        }

        [Test]
        public void FractionStaysBelowOneAcrossUnevenFrames()
        {
            var clock = new FixedStepAccumulator();
            double[] frames = { 0.016, 0.017, 0.033, 0.001, 0.25, 0.0167, 0.008, 2.0 };
            foreach (double frame in frames)
            {
                clock.Advance(frame);
                Assert.That(clock.InterpolationFraction, Is.GreaterThanOrEqualTo(0).And.LessThan(1));
            }
        }

        [TestCase(-0.001)]
        [TestCase(double.NaN)]
        [TestCase(double.PositiveInfinity)]
        public void RejectsInvalidElapsedTime(double elapsed)
        {
            var clock = new FixedStepAccumulator();
            Assert.Throws<ArgumentOutOfRangeException>(() => clock.Advance(elapsed));
        }
    }
}
