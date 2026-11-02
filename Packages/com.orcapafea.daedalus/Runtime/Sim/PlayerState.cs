namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// Everything the sim knows about the player between ticks. Position is the feet centre
    /// (<see cref="X"/> = hitbox centre, <see cref="Y"/> = hitbox bottom), in tiles; velocities in tiles/s.
    /// Tick counters are active while above zero.
    /// </summary>
    public struct PlayerState
    {
        public float X;
        public float Y;
        public float VelocityX;
        public float VelocityY;
        public PlayerContact Contact;

        /// <summary>+1 right, −1 left.</summary>
        public int Facing;
        public int AirJumpsLeft;

        public int CoyoteTicks;
        public int BufferTicks;
        public int WallCoyoteTicks;
        /// <summary>The side (−1/+1) of the wall the wall-coyote window belongs to.</summary>
        public int WallCoyoteSide;
        /// <summary>The integer x of the wall face the wall-coyote window belongs to.</summary>
        public int WallCoyoteFace;
        /// <summary>The wall-coyote window started at a cling, so its wall jump needs no wall-jump ability.</summary>
        public bool WallCoyoteFromCling;

        public int WallLockTicks;
        public int RopeLockoutTicks;
        /// <summary>The side (−1/0/+1) of the wall being slid on or clung to.</summary>
        public int WallSide;
        /// <summary>Index into the stepper's ropes; −1 = none.</summary>
        public int RopeIndex;

        /// <summary>The wall last jumped from: side (−1/+1; 0 = none) and integer face x.</summary>
        public int LastWallJumpSide;
        public int LastWallJumpFace;

        /// <summary>True only on the tick the world step replaced the state (a respawn), so the renderer snaps instead of interpolating.</summary>
        public bool Teleported;

        public Abilities Abilities;

        public readonly bool IsStanding => Contact == PlayerContact.Ground || Contact == PlayerContact.OneWayTop;
    }
}
