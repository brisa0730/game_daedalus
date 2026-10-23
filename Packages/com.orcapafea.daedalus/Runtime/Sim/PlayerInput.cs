using System;

namespace Orcapafea.Daedalus.Sim
{
    /// <summary>
    /// One tick of player input. <see cref="JumpPressed"/> is a per-tick edge (the runtime latches it);
    /// a press with <see cref="JumpHeld"/> false is a one-tick tap. Up never means jump.
    /// </summary>
    public readonly struct PlayerInput
    {
        public PlayerInput(int moveX, int moveY, bool jumpPressed, bool jumpHeld)
        {
            MoveX = Math.Sign(moveX);
            MoveY = Math.Sign(moveY);
            JumpPressed = jumpPressed;
            JumpHeld = jumpHeld;
        }

        /// <summary>−1 left, 0, +1 right.</summary>
        public int MoveX { get; }
        /// <summary>−1 down, 0, +1 up.</summary>
        public int MoveY { get; }
        public bool JumpPressed { get; }
        public bool JumpHeld { get; }
    }
}
