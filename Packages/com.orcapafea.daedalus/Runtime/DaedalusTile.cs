using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Orcapafea.Daedalus
{
    /// <summary>A tile with a collision kind. Any other tile on the Collision Tilemap counts as Solid.</summary>
    [CreateAssetMenu(menuName = "Daedalus/Tile", fileName = "New Daedalus Tile")]
    public class DaedalusTile : Tile
    {
        internal const int CurrentDataVersion = 1;

        [SerializeField, Tooltip("What this tile does on the Collision Tilemap: Solid, One-way (jump up through, drop down with Down + Jump), Climbable (climb with Wall Climb) or None (decoration). Ordinary tiles count as Solid.")]
        TileCollisionKind kind = TileCollisionKind.Solid;

        [SerializeField, HideInInspector]
        int dataVersion = CurrentDataVersion;

        public TileCollisionKind Kind
        {
            get => kind;
            internal set => kind = value;
        }

        internal int DataVersion => dataVersion;

        internal static TileKind ToSimKind(TileCollisionKind kind)
        {
            switch (kind)
            {
                case TileCollisionKind.None:
                    return TileKind.None;
                case TileCollisionKind.Solid:
                    return TileKind.Solid;
                case TileCollisionKind.OneWay:
                    return TileKind.OneWay;
                case TileCollisionKind.Climbable:
                    return TileKind.Climbable;
                default:
                    throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a tile collision kind.");
            }
        }
    }
}
