namespace Orcapafea.Daedalus.Sim
{
    /// <summary>What a trap does when it fires. Equals the Trap component's Action setting: never renumber.</summary>
    public enum TrapActionKind
    {
        /// <summary>Present until it fires, then gone.</summary>
        Vanish = 0,
        /// <summary>Hidden until it fires, then present.</summary>
        Appear = 1,
        /// <summary>Present; slides in a straight line to its end at constant speed, then rests there.</summary>
        Move = 2,
        /// <summary>Present; falls under the player's rise gravity until it lands on painted Solid/Climbable or leaves the map.</summary>
        Fall = 3,
    }
}
