using System;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using PlayerInput = Orcapafea.Daedalus.Sim.PlayerInput;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The player's keyboard and gamepad controls, as Input System actions built in code (never the
    /// project-wide actions asset). <see cref="ReadFrame"/> runs once per frame; <see cref="TakeTickInput"/>
    /// once per tick, and only the first tick after a press sees it. Up never means jump.
    /// </summary>
    internal sealed class PlayerControls : IDisposable
    {
        const float Threshold = 0.5f;

        readonly InputAction left;
        readonly InputAction right;
        readonly InputAction up;
        readonly InputAction down;
        readonly InputAction stick;
        readonly InputAction jump;

        int moveX;
        int moveY;
        bool jumpLatched;
        bool jumpHeld;

        public PlayerControls()
        {
            // One button per direction (digital, diagonals not normalised). A single Value action with several
            // bindings follows only one control at a time, which lost Right when Down was added.
            left = Direction("Left", "<Keyboard>/a", "<Keyboard>/leftArrow", "<Gamepad>/dpad/left");
            right = Direction("Right", "<Keyboard>/d", "<Keyboard>/rightArrow", "<Gamepad>/dpad/right");
            up = Direction("Up", "<Keyboard>/w", "<Keyboard>/upArrow", "<Gamepad>/dpad/up");
            down = Direction("Down", "<Keyboard>/s", "<Keyboard>/downArrow", "<Gamepad>/dpad/down");

            stick = new InputAction("Stick", InputActionType.Value, "<Gamepad>/leftStick", expectedControlType: "Vector2");

            jump = new InputAction("Jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");
        }

        public bool Enabled => jump.enabled;

        public void Enable()
        {
            left.Enable();
            right.Enable();
            up.Enable();
            down.Enable();
            stick.Enable();
            jump.Enable();
        }

        public void Disable()
        {
            left.Disable();
            right.Disable();
            up.Disable();
            down.Disable();
            stick.Disable();
            jump.Disable();
        }

        /// <summary>Reads the devices once per frame. A press is latched until a tick takes it.</summary>
        public void ReadFrame()
        {
            jumpLatched |= jump.WasPressedThisFrame();
            jumpHeld = jump.IsPressed();

            moveX = (right.IsPressed() ? 1 : 0) - (left.IsPressed() ? 1 : 0);
            moveY = (up.IsPressed() ? 1 : 0) - (down.IsPressed() ? 1 : 0);

            Vector2 analog = stick.ReadValue<Vector2>();
            if (moveX == 0)
            {
                moveX = Digit(analog.x);
            }
            if (moveY == 0)
            {
                if (analog.y >= Threshold)
                {
                    moveY = 1;
                }
                // Down only when it outweighs sideways, so a running diagonal never drops through a platform.
                else if (analog.y <= -Threshold && Mathf.Abs(analog.y) > Mathf.Abs(analog.x))
                {
                    moveY = -1;
                }
            }
        }

        /// <summary>This tick's input; clears the jump latch.</summary>
        public PlayerInput TakeTickInput()
        {
            var input = new PlayerInput(moveX, moveY, jumpLatched, jumpHeld);
            jumpLatched = false;
            return input;
        }

        public void Dispose()
        {
            Disable();
            left.Dispose();
            right.Dispose();
            up.Dispose();
            down.Dispose();
            stick.Dispose();
            jump.Dispose();
        }

        static InputAction Direction(string name, string key, string arrow, string dpad)
        {
            var action = new InputAction(name, InputActionType.Button);
            action.AddBinding(key);
            action.AddBinding(arrow);
            action.AddBinding(dpad);
            return action;
        }

        static int Digit(float value)
        {
            return value >= Threshold ? 1 : value <= -Threshold ? -1 : 0;
        }
    }
}
