namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// Movement numbers students never see (CLAUDE.md Architecture rule 8). Each is defined only here.
    /// Times are authored in seconds and converted to 60 Hz ticks once, below.
    /// </summary>
    public static class MovementInternals
    {
        public const float TickSeconds = 1f / FixedStepAccumulator.TicksPerSecond;

        /// <summary>Time to the apex of a default jump at gravity 1; sets <see cref="BaseGravity"/>.</summary>
        public const float TargetTimeToApex = 0.35f;
        /// <summary>Tiles/s² at gravity multiplier 1.</summary>
        public const float BaseGravity = 2f * MovementSettings.DefaultJumpHeight / (TargetTimeToApex * TargetTimeToApex);
        /// <summary>Gravity while falling (vy ≤ 0) is rising gravity times this.</summary>
        public const float FallGravityFactor = 1.75f;
        /// <summary>Tiles; the apex of a jump released at once.</summary>
        public const float MinHopHeight = 1.25f;

        public const float CoyoteTime = 0.1f;
        public const float JumpBufferTime = 0.1f;
        public const float WallCoyoteTime = 0.1f;
        public const float WallJumpInputLock = 0.2f;
        public const float RopeRegrabLockout = 0.2f;

        public const int CoyoteTicks = (int)(CoyoteTime * FixedStepAccumulator.TicksPerSecond + 0.5f);
        public const int JumpBufferTicks = (int)(JumpBufferTime * FixedStepAccumulator.TicksPerSecond + 0.5f);
        public const int WallCoyoteTicks = (int)(WallCoyoteTime * FixedStepAccumulator.TicksPerSecond + 0.5f);
        public const int WallJumpLockTicks = (int)(WallJumpInputLock * FixedStepAccumulator.TicksPerSecond + 0.5f);
        public const int RopeLockoutTicks = (int)(RopeRegrabLockout * FixedStepAccumulator.TicksPerSecond + 0.5f);

        /// <summary>Air acceleration, deceleration and turnaround are the ground rates times this.</summary>
        public const float AirControlFactor = 0.75f;
        /// <summary>Ground deceleration is ground acceleration times this.</summary>
        public const float DecelerationFactor = 1.5f;
        /// <summary>Ground turnaround is ground acceleration times this.</summary>
        public const float TurnaroundFactor = 2f;
        /// <summary>Wall-jump push-off speed is run speed times this.</summary>
        public const float WallJumpPushFactor = 1.5f;
        /// <summary>Tiles; a wall jump is allowed up to this gap from the wall.</summary>
        public const float WallJumpReach = 0.125f;
        /// <summary>Tiles; the most a rising head is slid sideways past a ceiling corner.</summary>
        public const float CeilingCornerCorrection = 0.25f;

        public const float HitboxWidth = 0.75f;
        public const float HitboxHeight = 1.5f;
        public const float HitboxHalfWidth = HitboxWidth / 2f;
        /// <summary>The grip point (ropes and climbable walls) is the hitbox centre, this far above the feet.</summary>
        public const float GripHeight = HitboxHeight / 2f;

        /// <summary>Tiles; after a ledge pop the hitbox's near edge is this far past the wall face.</summary>
        public const float LedgePopInset = 0.375f;
        /// <summary>Tiles; the most the player's centre can be from a rope's centre line to grab it.</summary>
        public const float RopeGrabReach = 0.375f;
        /// <summary>Tiles; how close a rope's top must be to a one-way top to count as reaching it.</summary>
        public const float RopeTopTolerance = 0.0625f;
        /// <summary>Tiles; how far the feet move below a one-way top on a drop-through.</summary>
        public const float DropThroughNudge = 0.0625f;
        /// <summary>
        /// Tiles; how far a rope's top is moved onto the nearest one-way surface at play start
        /// (students place ropes without grid snap).
        /// </summary>
        public const float RopeTopSnap = 0.5f;
        /// <summary>Tiles; the touching / on-a-row tolerance.</summary>
        public const float ContactEpsilon = 0.0001f;

        /// <summary>Cells searched around a blocked start for the nearest free one.</summary>
        public const int StartSearchRadius = 8;

        /// <summary>
        /// Empty rows the runtime adds above the Collision Tilemap's bounds: the feet's highest reach with
        /// every setting at max (jump height 6 × (1 + 3 air jumps)). The head meets the map top 1.5 tiles
        /// before that apex, which matters only at extreme settings.
        /// </summary>
        public const int MapHeadroomRows = 24;
    }
}
