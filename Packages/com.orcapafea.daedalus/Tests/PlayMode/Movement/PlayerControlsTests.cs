using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.TestTools;
using PlayerInput = Orcapafea.Daedalus.Sim.PlayerInput;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The bindings, digitizing and the jump latch, through real Input System devices and actions.</summary>
    public class PlayerControlsTests : MovementPlayTest
    {
        PlayerControls controls;

        [SetUp]
        public void CreateControls()
        {
            controls = new PlayerControls();
            controls.Enable();
        }

        [TearDown]
        public void DisposeControls()
        {
            controls?.Dispose();
            controls = null;
        }

        IEnumerator Frame()
        {
            yield return null;
            controls.ReadFrame();
        }

        IEnumerator Held(ButtonControl[] buttons, System.Action<PlayerInput> check)
        {
            foreach (ButtonControl button in buttons)
            {
                Press(button);
            }
            yield return Frame();
            check(controls.TakeTickInput());
            foreach (ButtonControl button in buttons)
            {
                Release(button);
            }
            yield return Frame();
            PlayerInput released = controls.TakeTickInput();
            Assert.AreEqual(0, released.MoveX, "released");
            Assert.AreEqual(0, released.MoveY, "released");
        }

        static void Move(PlayerInput input, int x, int y, string what)
        {
            Assert.AreEqual(x, input.MoveX, what + " x");
            Assert.AreEqual(y, input.MoveY, what + " y");
            Assert.IsFalse(input.JumpPressed, what + " never jumps");
        }

        [UnityTest]
        public IEnumerator ArrowsAndWasdMoveAndUpNeverJumps()
        {
            yield return Held(new[] { Keyboard.rightArrowKey }, i => Move(i, 1, 0, "right arrow"));
            yield return Held(new[] { Keyboard.dKey }, i => Move(i, 1, 0, "D"));
            yield return Held(new[] { Keyboard.leftArrowKey }, i => Move(i, -1, 0, "left arrow"));
            yield return Held(new[] { Keyboard.aKey }, i => Move(i, -1, 0, "A"));
            yield return Held(new[] { Keyboard.sKey }, i => Move(i, 0, -1, "S"));
            yield return Held(new[] { Keyboard.downArrowKey }, i => Move(i, 0, -1, "down arrow"));
            yield return Held(new[] { Keyboard.upArrowKey }, i => Move(i, 0, 1, "up arrow"));
            yield return Held(new[] { Keyboard.wKey }, i => Move(i, 0, 1, "W"));
            Keys(Key.RightArrow, Key.DownArrow);
            yield return Frame();
            Move(controls.TakeTickInput(), 1, -1, "right + down");
            Keys(Key.RightArrow, Key.S);
            yield return Frame();
            Move(controls.TakeTickInput(), 1, -1, "right arrow + S");
            Keys(Key.LeftArrow, Key.RightArrow);
            yield return Frame();
            Move(controls.TakeTickInput(), 0, 0, "left + right cancel");
            Keys();
            yield return Frame();
            Move(controls.TakeTickInput(), 0, 0, "released");
        }

        [UnityTest]
        public IEnumerator DpadMovesAndUpNeverJumps()
        {
            yield return Held(new[] { Gamepad.dpad.left }, i => Move(i, -1, 0, "d-pad left"));
            yield return Held(new[] { Gamepad.dpad.up }, i => Move(i, 0, 1, "d-pad up"));
        }

        IEnumerator Stick(Vector2 value, int x, int y)
        {
            Input.Set(Gamepad.leftStick, value);
            yield return Frame();
            Move(controls.TakeTickInput(), x, y, $"stick {value}");
            Input.Set(Gamepad.leftStick, Vector2.zero);
            yield return Frame();
            Move(controls.TakeTickInput(), 0, 0, "stick centred");
        }

        [UnityTest]
        public IEnumerator StickHasAHalfDeadZoneAndDownOnlyWhenItOutweighsSideways()
        {
            yield return Stick(new Vector2(0.4f, 0), 0, 0);
            yield return Stick(new Vector2(0.6f, 0), 1, 0);
            yield return Stick(new Vector2(0.6f, -0.7f), 1, -1);
            yield return Stick(new Vector2(0.8f, -0.7f), 1, 0);
            yield return Stick(new Vector2(0, 0.6f), 0, 1);
        }

        IEnumerator JumpButton(ButtonControl button, string what)
        {
            Press(button);
            yield return Frame();
            PlayerInput first = controls.TakeTickInput();
            Assert.IsTrue(first.JumpPressed, what + ": pressed on the first take");
            Assert.IsTrue(first.JumpHeld, what + ": held");
            PlayerInput second = controls.TakeTickInput();
            Assert.IsFalse(second.JumpPressed, what + ": not pressed on the second take");
            Assert.IsTrue(second.JumpHeld, what + ": still held");
            yield return Frame();
            Assert.IsFalse(controls.TakeTickInput().JumpPressed, what + ": not pressed again while held");
            Release(button);
            yield return Frame();
            Assert.IsFalse(controls.TakeTickInput().JumpHeld, what + ": released");
        }

        [UnityTest]
        public IEnumerator SpaceAndSouthJump()
        {
            yield return JumpButton(Keyboard.spaceKey, "Space");
            yield return JumpButton(Gamepad.buttonSouth, "South");
        }

        [UnityTest]
        public IEnumerator UpInputsNeverSetJump()
        {
            foreach (ButtonControl up in new[] { Keyboard.upArrowKey, Keyboard.wKey, Gamepad.dpad.up })
            {
                Press(up);
                yield return Frame();
                PlayerInput input = controls.TakeTickInput();
                Assert.AreEqual(1, input.MoveY, $"{up.name}: precondition, Up is read");
                Assert.IsFalse(input.JumpPressed, up.name);
                Assert.IsFalse(input.JumpHeld, up.name);
                Release(up);
                yield return Frame();
            }
            Input.Set(Gamepad.leftStick, new Vector2(0, 1));
            yield return Frame();
            PlayerInput stick = controls.TakeTickInput();
            Assert.AreEqual(1, stick.MoveY, "stick up: precondition");
            Assert.IsFalse(stick.JumpPressed, "stick up");
        }

        [UnityTest]
        public IEnumerator PressAndReleaseInOneFrameIsATap()
        {
            Input.Press(Keyboard.spaceKey, queueEventOnly: true);
            Input.Release(Keyboard.spaceKey, queueEventOnly: true);
            yield return Frame();
            PlayerInput input = controls.TakeTickInput();
            Assert.IsTrue(input.JumpPressed);
            Assert.IsFalse(input.JumpHeld);
        }

        [UnityTest]
        public IEnumerator TheLatchSurvivesAFrameWithNoTick()
        {
            Press(Keyboard.spaceKey);
            yield return Frame();
            // No tick this frame.
            yield return Frame();
            Assert.IsTrue(controls.TakeTickInput().JumpPressed, "the press from the earlier frame");
            Assert.IsFalse(controls.TakeTickInput().JumpPressed, "taken once");
        }

        [UnityTest]
        public IEnumerator PlayAgainIsAnyKeyOrAGamepadButtonForOneFrame()
        {
            yield return Frame();
            Assert.IsFalse(controls.PlayAgainPressed, "nothing pressed");
            Press(Keyboard.kKey);
            yield return Frame();
            Assert.IsTrue(controls.PlayAgainPressed, "a letter key");
            yield return Frame();
            Assert.IsFalse(controls.PlayAgainPressed, "held: only the frame it went down");
            Release(Keyboard.kKey);
            Press(Gamepad.startButton);
            yield return Frame();
            Assert.IsTrue(controls.PlayAgainPressed, "gamepad Start");
            Release(Gamepad.startButton);
            Press(Gamepad.buttonEast);
            yield return Frame();
            Assert.IsTrue(controls.PlayAgainPressed, "gamepad East");
        }

        [UnityTest]
        public IEnumerator ClearLatchDropsALatchedJump()
        {
            Press(Keyboard.spaceKey);
            yield return Frame();
            controls.ClearLatch();
            PlayerInput input = controls.TakeTickInput();
            Assert.IsFalse(input.JumpPressed, "the press is gone");
            Assert.IsTrue(input.JumpHeld, "the key is still held");
        }

        [Test]
        public void DisposeDisablesTheActions()
        {
            Assert.IsTrue(controls.Enabled, "precondition");
            controls.Dispose();
            Assert.IsFalse(controls.Enabled);
            controls = null;
        }
    }
}
