using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Where and with what the player starts: the feet centre in world space and the ability set.
    /// Play-from-here's ability set <b>replaces</b> the Player's toggles at the start; a toggle changed during play
    /// then switches just that ability. The same struct serves checkpoint respawn and tests. Jump is always added
    /// by the runtime.
    /// </summary>
    public readonly struct PlayStart
    {
        public PlayStart(Vector2 feet, Abilities abilities)
        {
            Feet = feet;
            Abilities = abilities;
        }

        public Vector2 Feet { get; }
        public Abilities Abilities { get; }
    }
}
