using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The Trap's Trigger setting. The values are saved in student projects and equal the sim's
    /// <see cref="Sim.TrapTriggerKind"/>: never renumber them.
    /// </summary>
    public enum TrapTrigger
    {
        [InspectorName("Player enters zone")] Zone = 0,
        [InspectorName("Player touches")] Touch = 1,
    }
}
