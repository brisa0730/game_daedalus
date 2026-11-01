using System.Collections;
using NUnit.Framework;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// The Input System's test fixture runs on both editors, and an action sees a press and a release
    /// queued in the same frame as pressed this frame but not held (the jump latch relies on it).
    /// </summary>
    public class InputFixtureTests
    {
        readonly InputTestFixture input = new InputTestFixture();
        Keyboard keyboard;
        Gamepad gamepad;
        InputAction jump;

        [SetUp]
        public void SetUp()
        {
            input.Setup();
            keyboard = InputSystem.AddDevice<Keyboard>();
            gamepad = InputSystem.AddDevice<Gamepad>();
            jump = new InputAction("jump", InputActionType.Button);
            jump.AddBinding("<Keyboard>/space");
            jump.AddBinding("<Gamepad>/buttonSouth");
            jump.Enable();
        }

        [TearDown]
        public void TearDown()
        {
            jump.Dispose();
            input.TearDown();
        }

        [UnityTest]
        public IEnumerator KeyboardPressAndReleaseReachTheAction()
        {
            input.Press(keyboard.spaceKey);
            yield return null;
            Assert.IsTrue(jump.WasPressedThisFrame(), "Space was not pressed this frame.");
            Assert.IsTrue(jump.IsPressed(), "Space is not held.");

            input.Release(keyboard.spaceKey);
            yield return null;
            Assert.IsFalse(jump.IsPressed(), "Space is still held after the release.");
        }

        [UnityTest]
        public IEnumerator GamepadSouthReachesTheAction()
        {
            input.Press(gamepad.buttonSouth);
            yield return null;
            Assert.IsTrue(jump.WasPressedThisFrame(), "South was not pressed this frame.");
            Assert.IsTrue(jump.IsPressed(), "South is not held.");
        }

        [UnityTest]
        public IEnumerator PressAndReleaseInOneFrameIsPressedButNotHeld()
        {
            Assert.IsFalse(jump.IsPressed(), "Precondition: Space starts released.");
            input.Press(keyboard.spaceKey, queueEventOnly: true);
            input.Release(keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            Assert.IsTrue(jump.WasPressedThisFrame(), "A press released in the same frame was lost.");
            Assert.IsFalse(jump.IsPressed(), "A press released in the same frame still reads as held.");
        }
    }
}
