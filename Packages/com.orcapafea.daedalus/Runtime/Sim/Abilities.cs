using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>The player's movement abilities. Run is always on and has no flag.</summary>
    [Flags]
    public enum Abilities
    {
        None = 0,
        Jump = 1,
        DoubleJump = 2,
        WallJump = 4,
        WallClimb = 8,
        RopeClimb = 16,
    }
}
