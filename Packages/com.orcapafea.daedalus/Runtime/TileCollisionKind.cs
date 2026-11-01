using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// What a tile does on the Collision Tilemap. The values are saved in student projects and equal the
    /// sim's <see cref="Sim.TileKind"/>: never renumber them. 4 is reserved for hazard (combat-and-enemies).
    /// </summary>
    public enum TileCollisionKind
    {
        None = 0,
        Solid = 1,
        [InspectorName("One-way")] OneWay = 2,
        Climbable = 3,
    }
}
