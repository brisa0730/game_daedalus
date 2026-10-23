namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// What a Collision Tilemap cell is to the sim. The values are saved in student projects:
    /// never renumber them. 4 is reserved for hazard (combat-and-enemies).
    /// </summary>
    public enum TileKind : byte
    {
        None = 0,
        Solid = 1,
        OneWay = 2,
        /// <summary>Collides exactly like Solid; differs only when the player has wall climb.</summary>
        Climbable = 3,
    }
}
