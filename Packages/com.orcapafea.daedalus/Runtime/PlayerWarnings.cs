using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>Every play-start warning text, in plain words, in one place so tests assert the same string.</summary>
    internal static class PlayerWarnings
    {
        public const string NoCollisionTilemap = "The Player has no Collision Tilemap, so there is nothing to stand on. Drag your Collision Tilemap into the Player's Collision Tilemap field.";
        public const string HasPhysics = "The Player doesn't need a Rigidbody 2D or a Collider 2D; Daedalus moves it by itself. Remove them from the Player, or they may push it around.";
        public const string MovedStart = "The Player started inside a wall, so it was moved to the nearest free spot. Move the Player in the Scene view to fix this.";
        public const string NoFreeStart = "The Player is buried in walls and no free spot is nearby. Move the Player out of the walls.";
        public const string RotatedOrScaled = "The Collision Tilemap (or its Grid) is rotated or scaled. Reset its Rotation to 0 and its Scale to 1, or collisions won't match the picture.";
        public const string CellLayout = "The Collision Tilemap's Grid must use the Rectangle cell layout.";
        public const string EmptyTilemap = "The Collision Tilemap is empty. Paint some tiles on it.";
        public const string NoSprite = "The Player has no sprite, so you won't see it. Add a Sprite Renderer with a sprite to this object.";
        public const string RopeTopInWall = "This rope's top is inside a wall or ceiling tile, so the player stops below it. Move the rope down so its top is in an empty cell or on a one-way platform.";
        public const string OldInputSetting = "The controls need the Input System. Set Edit > Project Settings > Player > Active Input Handling to Input System Package (New) or Both.";

        public static string CellSize(Vector3 cellSize)
        {
            return $"The Collision Tilemap's Grid has a Cell Size of {cellSize.x} × {cellSize.y}. Daedalus needs 1 × 1, so collisions won't match the picture.";
        }
    }
}
