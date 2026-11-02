namespace Orcapafea.Daedalus.Sim
{
    /// <summary>What a trap does when it fires. Equals the Trap component's Action setting: never renumber (2 Move and 3 Fall are reserved).</summary>
    public enum TrapActionKind
    {
        /// <summary>Present until it fires, then gone.</summary>
        Vanish = 0,
        /// <summary>Hidden until it fires, then present.</summary>
        Appear = 1,
    }
}
