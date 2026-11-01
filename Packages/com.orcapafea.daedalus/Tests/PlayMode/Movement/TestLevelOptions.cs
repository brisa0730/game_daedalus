using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>How <see cref="TestLevel.Build"/> sets up the scene; the defaults are a clean beginner setup.</summary>
    internal sealed class TestLevelOptions
    {
        public Abilities Abilities = Abilities.Jump;
        public bool WithCollisionTilemap = true;
        public string CollisionName = "Collision";
        public bool WithBackground = true;
        /// <summary>Drag the Collision Tilemap into the Player's field; otherwise the play-start pick finds it.</summary>
        public bool AssignTilemap = true;
        public bool WithSprite = true;
        public Vector2 SpritePivot = new Vector2(0.5f, 0.5f);
        public float PlayerScale = 1f;
        /// <summary>Runs after everything is built and before the Player's object is activated (so before Awake).</summary>
        public Action<TestLevel> BeforeActivate;
    }
}
