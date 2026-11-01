using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>The Collision Tilemap as the sim sees it: the tile grid and where its cell (0, 0) sits in the world.</summary>
    internal sealed class CollisionMap
    {
        public CollisionMap(TileGrid grid, Vector2 origin, Vector2Int cellMin)
        {
            Grid = grid ?? throw new ArgumentNullException(nameof(grid));
            Origin = origin;
            CellMin = cellMin;
        }

        public TileGrid Grid { get; }
        /// <summary>The world position of sim cell (0, 0)'s lower-left corner.</summary>
        public Vector2 Origin { get; }
        /// <summary>The Tilemap cell that is sim cell (0, 0).</summary>
        public Vector2Int CellMin { get; }

        public Vector2 WorldToSim(Vector2 world) => world - Origin;
        public Vector2 SimToWorld(Vector2 sim) => sim + Origin;
    }
}
