namespace Orcapafea.Daedalus.Sim
{
    /// <summary>What the player is touching and moving by.</summary>
    public enum PlayerContact
    {
        Air,
        /// <summary>Standing with at least one solid or climbable cell underfoot.</summary>
        Ground,
        /// <summary>Standing on one-way cells only.</summary>
        OneWayTop,
        WallSlide,
        WallCling,
        Rope,
    }
}
