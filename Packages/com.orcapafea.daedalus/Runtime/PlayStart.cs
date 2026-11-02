using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Where and with what the player starts: the feet centre in world space and the ability set.
    /// Play-from-here's ability set <b>replaces</b> the Player's toggles at the start; a toggle changed during play
    /// then switches just that ability. Every start (play start, play-from-here, play again) resets the traps and the
    /// death counter, and arms each trap only once the player has been outside its trigger. Jump is always added
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
