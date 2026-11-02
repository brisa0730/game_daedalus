namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The Trap's Action setting. The values are saved in student projects and equal the sim's
    /// <see cref="Sim.TrapActionKind"/>: never renumber them (2 Move and 3 Fall are reserved for a later update).
    /// </summary>
    public enum TrapAction
    {
        Vanish = 0,
        Appear = 1,
    }
}
