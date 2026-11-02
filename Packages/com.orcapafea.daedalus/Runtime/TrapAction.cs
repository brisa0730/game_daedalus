namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The Trap's Action setting. The values are saved in student projects and equal the sim's
    /// <see cref="Sim.TrapActionKind"/>: never renumber them.
    /// </summary>
    public enum TrapAction
    {
        Vanish = 0,
        Appear = 1,
        Move = 2,
        Fall = 3,
    }
}
