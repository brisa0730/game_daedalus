namespace Orcapafea.Daedalus.Sim
{
    /// <summary>Where the player comes back after a death. Equals the Player's On hazard setting: never renumber.</summary>
    public enum RespawnRule
    {
        /// <summary>Where the player last stood safely.</summary>
        SafeGround = 0,
        /// <summary>The last checkpoint touched, else the start.</summary>
        Checkpoint = 1,
    }
}
