namespace Orcapafea.Daedalus.Sim
{
    public enum LevelPhase
    {
        Playing = 0,
        /// <summary>The death beat: input is ignored until the player reappears.</summary>
        Dying = 1,
        /// <summary>The exit was reached; the win screen shows.</summary>
        Won = 2,
    }
}
