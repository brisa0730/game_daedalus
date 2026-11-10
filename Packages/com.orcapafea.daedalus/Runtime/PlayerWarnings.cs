using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>Every play-start warning text, in plain words, in one place so tests assert the same string.</summary>
    internal static class PlayerWarnings
    {
        public const string NoCollisionTilemap = "The Player has no Collision Tilemap, so there is nothing to stand on. Drag your Collision Tilemap into the Player's Collision Tilemap field.";
        public const string HasPhysics = "The Player doesn't need a Rigidbody 2D or a Collider 2D; Daedalus moves it by itself. Remove them from the Player, or they may push it around.";
        public const string MovedStart = "The Player started inside a wall or on spikes, so it was moved to the nearest free spot. Move the Player in the Scene view to fix this.";
        public const string TrapOverPaintedTiles = "This trap sits on tiles painted on the Collision Tilemap. The painted tiles stay in charge, so the trap won't change those cells. Erase the painted tiles under it, or move the trap.";
        public const string HazardOnDecorationTilemap = "This Tilemap has Hazard (spike) tiles, but only the Collision Tilemap can hurt the player. Paint spikes on the Collision Tilemap.";
        public const string TrapOffGrid = "This trap isn't lined up with the grid, so it works as if it were snapped to the nearest cells. Click Snap to grid on the trap.";
        public const string TrapNoTile = "This trap has no Tile, so you won't see it. Pick a Tile on the trap.";
        public const string TrapTileNotDrawable = "This trap's Tile can't be drawn on a trap (Rule Tiles and animated tiles aren't supported). Pick a plain tile.";
        public const string TrapSpriteNotFullRect = "This trap's tile picture may look wrong when repeated. In the sprite's import settings, set Mesh Type to Full Rect.";
        public const string TrapScaled = "This trap is scaled or rotated, but traps ignore Scale and Rotation. Set them back to 1 and 0 and use Size.";
        public const string TrapOnCheckpoint = "A Trap on a Checkpoint does nothing. Put the Trap on its own object.";
        public const string TrapOnDisabledExit = "This Trap's Exit is turned off, so the Trap does nothing.";
        public const string TrapEndInGround = "This Move trap ends inside painted ground. It moves through tiles, so it will stop buried there. Drag its end (the arrow's handle) out of the ground.";
        public const string NoFreeStart = "The Player is buried in walls or spikes and no free spot with ground under it is nearby. Move the Player out of the walls.";
        public const string RotatedOrScaled = "The Collision Tilemap (or its Grid) is rotated or scaled. Reset its Rotation to 0 and its Scale to 1, or collisions won't match the picture.";
        public const string CellLayout = "The Collision Tilemap's Grid must use the Rectangle cell layout.";
        public const string EmptyTilemap = "The Collision Tilemap is empty. Paint some tiles on it.";
        public const string NoSprite = "The Player has no sprite, so you won't see it. Add a Sprite Renderer with a sprite to this object.";
        public const string RopeTopInWall = "This rope's top is inside a wall or ceiling tile, so the player stops below it. Move the rope down so its top is in an empty cell or on a one-way platform.";
        public const string OldInputSetting = "The controls need the Input System. Set Edit > Project Settings > Player > Active Input Handling to Input System Package (New) or Both.";

        public const string NextStageMissing = "This Exit's Next stage is missing: its scene was deleted, or moved outside Unity. Drag the scene into Next stage again.";
        public const string NextStageMissingOnScreen = "This Exit's next stage is missing. Fix it in the Inspector.";

        public static string PlayerInAnotherScene(string sceneName)
        {
            return $"{sceneName} is open at the same time and has a Player too, so both are running. To close it, right-click {sceneName} in the Hierarchy and choose Remove Scene.";
        }

        public static string PlayAgainUsesSaved(string sceneName)
        {
            return $"Play again uses the last saved {sceneName}. Your changes aren't lost: Stop, then press Ctrl+S.";
        }

        public static string CellSize(Vector3 cellSize)
        {
            return $"The Collision Tilemap's Grid has a Cell Size of {cellSize.x} × {cellSize.y}. Daedalus needs 1 × 1, so collisions won't match the picture.";
        }
    }
}
