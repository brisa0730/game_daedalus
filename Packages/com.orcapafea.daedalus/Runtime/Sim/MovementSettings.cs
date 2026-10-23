using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// The player's student-facing movement settings. The <c>Default*</c>, <c>Min*</c> and <c>Max*</c>
    /// constants are the single source of truth for their initial values and Inspector ranges;
    /// <c>Documents/GDD/entities.md</c> mirrors them.
    /// </summary>
    public struct MovementSettings
    {
        public const float DefaultRunSpeed = 6f;
        public const float MinRunSpeed = 1f;
        public const float MaxRunSpeed = 12f;

        public const float DefaultTimeToFullSpeed = 0.1f;
        public const float MinTimeToFullSpeed = 0f;
        public const float MaxTimeToFullSpeed = 0.5f;

        public const float DefaultJumpHeight = 3.5f;
        public const float MinJumpHeight = 1f;
        public const float MaxJumpHeight = 6f;

        public const float DefaultGravity = 1f;
        public const float MinGravity = 0.25f;
        public const float MaxGravity = 2f;

        public const float DefaultMaxFallSpeed = 12f;
        public const float MinMaxFallSpeed = 2f;
        public const float MaxMaxFallSpeed = 24f;

        public const int DefaultAirJumps = 1;
        public const int MinAirJumps = 1;
        public const int MaxAirJumps = 3;

        public const float DefaultWallSlideSpeed = 2f;
        public const float MinWallSlideSpeed = 0.5f;
        public const float MaxWallSlideSpeed = 6f;

        public const float DefaultClimbSpeed = 3f;
        public const float MinClimbSpeed = 1f;
        public const float MaxClimbSpeed = 8f;

        /// <summary>Tiles per second.</summary>
        public float RunSpeed;
        /// <summary>Seconds from rest to run speed on the ground.</summary>
        public float TimeToFullSpeed;
        /// <summary>Tiles.</summary>
        public float JumpHeight;
        /// <summary>Multiplier over the base gravity.</summary>
        public float Gravity;
        /// <summary>Tiles per second.</summary>
        public float MaxFallSpeed;
        /// <summary>Used only with double jump.</summary>
        public int AirJumps;
        /// <summary>Tiles per second; used only with wall jump.</summary>
        public float WallSlideSpeed;
        /// <summary>Tiles per second, on climbable walls and ropes.</summary>
        public float ClimbSpeed;

        public static MovementSettings Default => new MovementSettings
        {
            RunSpeed = DefaultRunSpeed,
            TimeToFullSpeed = DefaultTimeToFullSpeed,
            JumpHeight = DefaultJumpHeight,
            Gravity = DefaultGravity,
            MaxFallSpeed = DefaultMaxFallSpeed,
            AirJumps = DefaultAirJumps,
            WallSlideSpeed = DefaultWallSlideSpeed,
            ClimbSpeed = DefaultClimbSpeed,
        };

        /// <summary>A copy with every field inside its range (non-finite values fall back to the default) and wall slide no faster than max fall.</summary>
        public MovementSettings Clamped()
        {
            var clamped = new MovementSettings
            {
                RunSpeed = Clamp(RunSpeed, DefaultRunSpeed, MinRunSpeed, MaxRunSpeed),
                TimeToFullSpeed = Clamp(TimeToFullSpeed, DefaultTimeToFullSpeed, MinTimeToFullSpeed, MaxTimeToFullSpeed),
                JumpHeight = Clamp(JumpHeight, DefaultJumpHeight, MinJumpHeight, MaxJumpHeight),
                Gravity = Clamp(Gravity, DefaultGravity, MinGravity, MaxGravity),
                MaxFallSpeed = Clamp(MaxFallSpeed, DefaultMaxFallSpeed, MinMaxFallSpeed, MaxMaxFallSpeed),
                AirJumps = Math.Clamp(AirJumps, MinAirJumps, MaxAirJumps),
                WallSlideSpeed = Clamp(WallSlideSpeed, DefaultWallSlideSpeed, MinWallSlideSpeed, MaxWallSlideSpeed),
                ClimbSpeed = Clamp(ClimbSpeed, DefaultClimbSpeed, MinClimbSpeed, MaxClimbSpeed),
            };
            clamped.WallSlideSpeed = MathF.Min(clamped.WallSlideSpeed, clamped.MaxFallSpeed);
            return clamped;
        }

        static float Clamp(float value, float fallback, float min, float max)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                return fallback;
            }
            return Math.Clamp(value, min, max);
        }
    }
}
