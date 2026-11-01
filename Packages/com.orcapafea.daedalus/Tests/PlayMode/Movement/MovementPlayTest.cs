using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.LowLevel;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Shared setup for the movement PlayMode tests: the Input System test fixture (by composition) with a
    /// keyboard and a gamepad, <c>Time.captureFramerate</c> = 60 so every frame is one tick, and level cleanup.
    /// </summary>
    public abstract class MovementPlayTest
    {
        protected readonly InputTestFixture Input = new InputTestFixture();
        protected Keyboard Keyboard;
        protected Gamepad Gamepad;
        internal TestLevel Level;
        int previousCaptureFramerate;
        float previousTimeScale;

        protected virtual int CaptureFramerate => 60;

        [SetUp]
        public void SetUpInput()
        {
            Input.Setup();
            Keyboard = InputSystem.AddDevice<Keyboard>();
            Gamepad = InputSystem.AddDevice<Gamepad>();
            previousCaptureFramerate = Time.captureFramerate;
            previousTimeScale = Time.timeScale;
            Time.captureFramerate = CaptureFramerate;
        }

        [TearDown]
        public void TearDownInput()
        {
            Level?.Dispose();
            Level = null;
            Time.captureFramerate = previousCaptureFramerate;
            Time.timeScale = previousTimeScale;
            Input.TearDown();
        }

        internal TestLevel Build(string[] rows, TestLevelOptions options = null)
        {
            Level = TestLevel.Build(rows, options);
            return Level;
        }

        protected static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        protected void Press(ButtonControl button) => Input.Press(button);

        /// <summary>Exactly these keys held and every other key up, in one keyboard state (as a real keyboard reports it).</summary>
        protected void Keys(params Key[] keys) => InputSystem.QueueStateEvent(Keyboard, new KeyboardState(keys));
        protected void Release(ButtonControl button) => Input.Release(button);

        /// <summary>The feet in world space, from the session (the sim's position, not the interpolated one).</summary>
        internal static Vector2 SimFeetWorld(Player player)
        {
            return player.Session.Map.SimToWorld(new Vector2(player.Session.Current.X, player.Session.Current.Y));
        }
    }
}
