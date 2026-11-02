namespace Orcapafea.Daedalus.Sim
{
    /// <summary>What sets a trap off. Equals the Trap component's Trigger setting: never renumber.</summary>
    public enum TrapTriggerKind
    {
        /// <summary>The player's hitbox enters the trap's zone.</summary>
        Zone = 0,
        /// <summary>The player touches the trap's box (<see cref="LevelRules.ContactMargin"/>).</summary>
        Touch = 1,
    }
}
