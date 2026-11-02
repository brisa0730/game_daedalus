using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The Player's On hazard setting. The values are saved in student projects and equal the sim's
    /// <see cref="Sim.RespawnRule"/>: never renumber them.
    /// </summary>
    public enum OnHazard
    {
        [InspectorName("Return to safe ground")] ReturnToSafeGround = 0,
        [InspectorName("Respawn at checkpoint")] RespawnAtCheckpoint = 1,
    }
}
