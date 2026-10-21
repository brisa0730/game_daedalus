using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// Turns real elapsed time into fixed 60 Hz ticks (GDD architecture.md § Time Model).
    /// After a hitch at most <see cref="MaxTicksPerAdvance"/> ticks run; whole ticks beyond
    /// that are dropped so the game slows down instead of spiralling. Only the fraction
    /// of a tick carries over, which is also the render interpolation fraction.
    /// </summary>
    public sealed class FixedStepAccumulator
    {
        public const int TicksPerSecond = 60;
        public const int MaxTicksPerAdvance = 5;

        // Absorbs floating-point error so an exact 1/60 s step counts as one whole tick.
        const double TickBoundaryEpsilon = 1e-9;

        double fractionalTicks;

        /// <summary>How far the current time sits between the last tick and the next, in [0, 1).</summary>
        public double InterpolationFraction => fractionalTicks;

        /// <summary>Adds elapsed real time and returns how many ticks to run now.</summary>
        public int Advance(double elapsedSeconds)
        {
            if (double.IsNaN(elapsedSeconds) || double.IsInfinity(elapsedSeconds) || elapsedSeconds < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(elapsedSeconds), elapsedSeconds, "Elapsed time must be finite and not negative.");
            }

            double dueTicks = fractionalTicks + elapsedSeconds * TicksPerSecond;
            double wholeTicks = Math.Floor(dueTicks + TickBoundaryEpsilon);
            fractionalTicks = Math.Max(0, dueTicks - wholeTicks);

            return wholeTicks > MaxTicksPerAdvance ? MaxTicksPerAdvance : (int)wholeTicks;
        }
    }
}
