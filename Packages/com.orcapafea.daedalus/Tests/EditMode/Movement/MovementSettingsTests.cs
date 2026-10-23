using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Pins every default, range and internal. A retune must change this file and
    /// Documents/GDD/entities.md together.
    /// </summary>
    public class MovementSettingsTests
    {
        const float Tolerance = 1e-3f;

        [Test]
        public void DefaultsAndRangesArePinned()
        {
            AssertRange(MovementSettings.DefaultRunSpeed, MovementSettings.MinRunSpeed, MovementSettings.MaxRunSpeed, 6, 1, 12, "RunSpeed");
            AssertRange(MovementSettings.DefaultTimeToFullSpeed, MovementSettings.MinTimeToFullSpeed, MovementSettings.MaxTimeToFullSpeed, 0.1f, 0, 0.5f, "TimeToFullSpeed");
            AssertRange(MovementSettings.DefaultJumpHeight, MovementSettings.MinJumpHeight, MovementSettings.MaxJumpHeight, 3.5f, 1, 6, "JumpHeight");
            AssertRange(MovementSettings.DefaultGravity, MovementSettings.MinGravity, MovementSettings.MaxGravity, 1, 0.25f, 2, "Gravity");
            AssertRange(MovementSettings.DefaultMaxFallSpeed, MovementSettings.MinMaxFallSpeed, MovementSettings.MaxMaxFallSpeed, 12, 2, 24, "MaxFallSpeed");
            AssertRange(MovementSettings.DefaultAirJumps, MovementSettings.MinAirJumps, MovementSettings.MaxAirJumps, 1, 1, 3, "AirJumps");
            AssertRange(MovementSettings.DefaultWallSlideSpeed, MovementSettings.MinWallSlideSpeed, MovementSettings.MaxWallSlideSpeed, 2, 0.5f, 6, "WallSlideSpeed");
            AssertRange(MovementSettings.DefaultClimbSpeed, MovementSettings.MinClimbSpeed, MovementSettings.MaxClimbSpeed, 3, 1, 8, "ClimbSpeed");

            MovementSettings d = MovementSettings.Default;
            Assert.AreEqual(MovementSettings.DefaultRunSpeed, d.RunSpeed);
            Assert.AreEqual(MovementSettings.DefaultTimeToFullSpeed, d.TimeToFullSpeed);
            Assert.AreEqual(MovementSettings.DefaultJumpHeight, d.JumpHeight);
            Assert.AreEqual(MovementSettings.DefaultGravity, d.Gravity);
            Assert.AreEqual(MovementSettings.DefaultMaxFallSpeed, d.MaxFallSpeed);
            Assert.AreEqual(MovementSettings.DefaultAirJumps, d.AirJumps);
            Assert.AreEqual(MovementSettings.DefaultWallSlideSpeed, d.WallSlideSpeed);
            Assert.AreEqual(MovementSettings.DefaultClimbSpeed, d.ClimbSpeed);
        }

        [Test]
        public void InternalsArePinned()
        {
            Assert.AreEqual(0.35f, MovementInternals.TargetTimeToApex);
            Assert.AreEqual(1.75f, MovementInternals.FallGravityFactor);
            Assert.AreEqual(1.25f, MovementInternals.MinHopHeight);
            Assert.AreEqual(0.1f, MovementInternals.CoyoteTime);
            Assert.AreEqual(0.1f, MovementInternals.JumpBufferTime);
            Assert.AreEqual(0.1f, MovementInternals.WallCoyoteTime);
            Assert.AreEqual(0.2f, MovementInternals.WallJumpInputLock);
            Assert.AreEqual(0.2f, MovementInternals.RopeRegrabLockout);
            Assert.AreEqual(6, MovementInternals.CoyoteTicks);
            Assert.AreEqual(6, MovementInternals.JumpBufferTicks);
            Assert.AreEqual(6, MovementInternals.WallCoyoteTicks);
            Assert.AreEqual(12, MovementInternals.WallJumpLockTicks);
            Assert.AreEqual(12, MovementInternals.RopeLockoutTicks);
            Assert.AreEqual(0.75f, MovementInternals.AirControlFactor);
            Assert.AreEqual(1.5f, MovementInternals.DecelerationFactor);
            Assert.AreEqual(2f, MovementInternals.TurnaroundFactor);
            Assert.AreEqual(1.5f, MovementInternals.WallJumpPushFactor);
            Assert.AreEqual(0.125f, MovementInternals.WallJumpReach);
            Assert.AreEqual(0.25f, MovementInternals.CeilingCornerCorrection);
            Assert.AreEqual(0.75f, MovementInternals.HitboxWidth);
            Assert.AreEqual(1.5f, MovementInternals.HitboxHeight);
            Assert.AreEqual(0.375f, MovementInternals.HitboxHalfWidth);
            Assert.AreEqual(0.75f, MovementInternals.GripHeight);
            Assert.AreEqual(0.375f, MovementInternals.LedgePopInset);
            Assert.AreEqual(0.375f, MovementInternals.RopeGrabReach);
            Assert.AreEqual(0.0625f, MovementInternals.RopeTopTolerance);
            Assert.AreEqual(0.0625f, MovementInternals.DropThroughNudge);
            Assert.AreEqual(0.0001f, MovementInternals.ContactEpsilon);
            Assert.AreEqual(8, MovementInternals.StartSearchRadius);
        }

        [Test]
        public void DerivedNumbersAtDefaults()
        {
            var d = new DerivedMovement(MovementSettings.Default);
            Assert.AreEqual(57.142857f, MovementInternals.BaseGravity, Tolerance);
            Assert.AreEqual(57.142857f, d.RiseGravity, Tolerance);
            Assert.AreEqual(100f, d.FallGravity, Tolerance);
            Assert.AreEqual(20f, d.JumpVelocity, Tolerance);
            Assert.AreEqual(11.952f, d.MinHopVelocity, Tolerance);
            Assert.AreEqual(60f, d.GroundAccel, Tolerance);
            Assert.AreEqual(90f, d.GroundDecel, Tolerance);
            Assert.AreEqual(120f, d.GroundTurnaround, Tolerance);
            Assert.AreEqual(45f, d.AirAccel, Tolerance);
            Assert.AreEqual(67.5f, d.AirDecel, Tolerance);
            Assert.AreEqual(90f, d.AirTurnaround, Tolerance);
            Assert.AreEqual(9f, d.WallPushSpeed, Tolerance);
            Assert.AreEqual(6f, d.RunSpeed);
            Assert.AreEqual(12f, d.MaxFallSpeed);
            Assert.AreEqual(2f, d.WallSlideSpeed);
            Assert.AreEqual(3f, d.ClimbSpeed);
            Assert.AreEqual(1, d.AirJumps);
        }

        [Test]
        public void JumpHeightHoldsAtAnyGravity()
        {
            MovementSettings s = MovementSettings.Default;
            s.Gravity = 2f;
            var d = new DerivedMovement(s);
            Assert.AreEqual(28.284f, d.JumpVelocity, Tolerance);
            Assert.AreEqual(3.5f, d.JumpVelocity * d.JumpVelocity / (2f * d.RiseGravity), Tolerance);
        }

        [Test]
        public void ClampedKeepsEveryFieldInRange()
        {
            MovementSettings s = MovementSettings.Default;
            s.Gravity = 0f;
            s.AirJumps = 0;
            Assert.AreEqual(0.25f, s.Clamped().Gravity);
            Assert.AreEqual(1, s.Clamped().AirJumps);

            s = MovementSettings.Default;
            s.MaxFallSpeed = 2f;
            s.WallSlideSpeed = 6f;
            Assert.AreEqual(2f, s.Clamped().WallSlideSpeed, "wall slide is never faster than max fall");

            s = MovementSettings.Default;
            s.RunSpeed = float.NaN;
            s.JumpHeight = float.PositiveInfinity;
            s.ClimbSpeed = 100f;
            Assert.AreEqual(MovementSettings.DefaultRunSpeed, s.Clamped().RunSpeed);
            Assert.AreEqual(MovementSettings.DefaultJumpHeight, s.Clamped().JumpHeight);
            Assert.AreEqual(MovementSettings.MaxClimbSpeed, s.Clamped().ClimbSpeed);

            s = MovementSettings.Default;
            s.TimeToFullSpeed = 0f;
            var d = new DerivedMovement(s);
            Assert.AreEqual(s.RunSpeed, d.GroundAccel * MovementInternals.TickSeconds, Tolerance, "time to full speed 0 reaches run speed in one tick");
        }

        [Test]
        public void NoSettingMovesThePlayerThreeQuartersOfATilePerTick()
        {
            float[] run = { MovementSettings.MinRunSpeed, MovementSettings.MaxRunSpeed };
            float[] jump = { MovementSettings.MinJumpHeight, MovementSettings.MaxJumpHeight };
            float[] gravity = { MovementSettings.MinGravity, MovementSettings.MaxGravity };
            float[] fall = { MovementSettings.MinMaxFallSpeed, MovementSettings.MaxMaxFallSpeed };
            float[] climb = { MovementSettings.MinClimbSpeed, MovementSettings.MaxClimbSpeed };
            float worst = 0;
            int combinations = 0;
            foreach (float r in run)
            foreach (float j in jump)
            foreach (float g in gravity)
            foreach (float f in fall)
            foreach (float c in climb)
            {
                MovementSettings s = MovementSettings.Default;
                s.RunSpeed = r;
                s.JumpHeight = j;
                s.Gravity = g;
                s.MaxFallSpeed = f;
                s.ClimbSpeed = c;
                var d = new DerivedMovement(s);
                float fastest = System.Math.Max(System.Math.Max(d.JumpVelocity, d.MaxFallSpeed),
                    System.Math.Max(System.Math.Max(d.WallPushSpeed, d.RunSpeed), d.ClimbSpeed));
                worst = System.Math.Max(worst, fastest * MovementInternals.TickSeconds);
                combinations++;
            }
            Assert.AreEqual(32, combinations);
            TestContext.WriteLine($"Worst case per tick: {worst:F3} tiles");
            Assert.AreEqual(0.617f, worst, Tolerance, "expected worst case: jump 6 at gravity 2");
            Assert.That(worst, Is.LessThan(0.75f));
        }

        static void AssertRange(float def, float min, float max, float expectedDef, float expectedMin, float expectedMax, string name)
        {
            Assert.AreEqual(expectedDef, def, name + " default");
            Assert.AreEqual(expectedMin, min, name + " min");
            Assert.AreEqual(expectedMax, max, name + " max");
        }
    }
}
