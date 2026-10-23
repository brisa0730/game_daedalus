using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>The numbers the stepper uses, derived once per settings change (never per tick) from the clamped settings.</summary>
    public readonly struct DerivedMovement
    {
        public DerivedMovement(MovementSettings settings)
        {
            MovementSettings s = settings.Clamped();
            RiseGravity = MovementInternals.BaseGravity * s.Gravity;
            FallGravity = RiseGravity * MovementInternals.FallGravityFactor;
            // Rising gravity only, so the jump height in tiles holds at any gravity multiplier.
            JumpVelocity = MathF.Sqrt(2f * RiseGravity * s.JumpHeight);
            MinHopVelocity = MathF.Sqrt(2f * RiseGravity * MathF.Min(MovementInternals.MinHopHeight, s.JumpHeight));

            GroundAccel = s.RunSpeed / MathF.Max(s.TimeToFullSpeed, MovementInternals.TickSeconds);
            GroundDecel = GroundAccel * MovementInternals.DecelerationFactor;
            GroundTurnaround = GroundAccel * MovementInternals.TurnaroundFactor;
            AirAccel = GroundAccel * MovementInternals.AirControlFactor;
            AirDecel = GroundDecel * MovementInternals.AirControlFactor;
            AirTurnaround = GroundTurnaround * MovementInternals.AirControlFactor;

            WallPushSpeed = s.RunSpeed * MovementInternals.WallJumpPushFactor;
            RunSpeed = s.RunSpeed;
            MaxFallSpeed = s.MaxFallSpeed;
            WallSlideSpeed = s.WallSlideSpeed;
            ClimbSpeed = s.ClimbSpeed;
            AirJumps = s.AirJumps;
        }

        public float RiseGravity { get; }
        public float FallGravity { get; }
        public float JumpVelocity { get; }
        public float MinHopVelocity { get; }
        public float GroundAccel { get; }
        public float GroundDecel { get; }
        public float GroundTurnaround { get; }
        public float AirAccel { get; }
        public float AirDecel { get; }
        public float AirTurnaround { get; }
        public float WallPushSpeed { get; }
        public float RunSpeed { get; }
        public float MaxFallSpeed { get; }
        public float WallSlideSpeed { get; }
        public float ClimbSpeed { get; }
        public int AirJumps { get; }
    }
}
