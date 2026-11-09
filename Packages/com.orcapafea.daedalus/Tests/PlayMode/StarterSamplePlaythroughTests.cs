#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Plays the imported Starter sample start to finish with the real Player and keyboard. It runs only where the
    /// sample has been imported (Tools/install_check.py does, and fails if this was ignored).
    /// </summary>
    public class StarterSamplePlaythroughTests : SavedScenePlayTest
    {
        IEnumerator LoadTheStarter()
        {
            string path = AssetDatabase.FindAssets("Starter t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.StartsWith("Assets/Samples/", StringComparison.Ordinal) && p.EndsWith("/Starter.unity", StringComparison.Ordinal));
            if (path == null)
            {
                Assert.Ignore("The Starter sample isn't imported in this project.");
            }
            yield return LoadScene(path);
        }

        IEnumerator DieWalkingInto(int deaths, float fromX, float toX, string what)
        {
            int changes = Session.TrapChanges;
            Keys(Key.RightArrow);
            yield return Until(() => Session.Deaths == deaths, $"{what} kills a walking player");
            Keys();
            float at = Session.DeathFeetWorld.x;
            Assert.That(at, Is.InRange(fromX, toX), $"{what}: where the player died");
            yield return Until(() => Session.Phase == LevelPhase.Playing, $"{what}: back in play after the death");
            yield return Frames(5);
            if (what != "the Fake Floor")
            {
                Assert.Greater(Session.TrapChanges, changes, $"{what} fired");
            }
        }

        IEnumerator JumpAcross(float takeoffX, float landX, string what)
        {
            Keys(Key.RightArrow);
            yield return Until(() => X >= takeoffX, $"reaching the take-off for {what}");
            Keys(Key.RightArrow, Key.Space);
            yield return Until(() => X >= landX && Session.Current.IsStanding, $"landing past {what}");
            Keys(Key.RightArrow);
        }

        // TrapChanges counts presence only; a Move or a Fall shows in its offset.
        bool AnyTrapMoved(Func<Vector2, bool> moved)
        {
            for (int i = 0; i < Session.TrapCount; i++)
            {
                if (moved(Session.InterpolatedTrapOffset(i)))
                {
                    return true;
                }
            }
            return false;
        }

        void Picture(string name)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "The Starter has a main camera.");
            var target = new RenderTexture(640, 360, 24);
            Created.Add(target);
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            TestImages.Save(TestImages.ReadBack(target, Created), name);
        }

        [UnityTest]
        public IEnumerator TheStarterCanBePlayedToTheEnd()
        {
            yield return LoadTheStarter();
            yield return Frames(2);
            Picture("starter-section-1");

            // Section 1: the first checkpoint, then both traps by falling for them.
            Keys(Key.RightArrow);
            yield return Until(() => Session.ActiveCheckpoint == 0, "the first checkpoint");
            yield return DieWalkingInto(1, 10f, 13f, "the Fake Floor");
            yield return JumpAcross(9.6f, 13f, "the Fake Floor");
            yield return DieWalkingInto(2, 18f, 21f, "the Drop Floor");

            yield return JumpAcross(9.6f, 13f, "the Fake Floor");
            yield return JumpAcross(17.6f, 21f, "the Drop Floor");

            // Section 2.
            yield return Until(() => Session.ActiveCheckpoint == 1, "the second checkpoint");
            Picture("starter-section-2");
            yield return DieWalkingInto(3, 34f, 37f, "the Pop Spike");
            yield return JumpAcross(33.5f, 37.5f, "the Pop Spike");

            Keys(Key.RightArrow);
            yield return Until(() => X >= 47f, "running under the Falling Ceiling");
            yield return Until(() => AnyTrapMoved(offset => offset.y < -1f), "the Falling Ceiling falling");
            Assert.AreEqual(3, Session.Deaths, "a player who keeps running clears the Falling Ceiling");

            // Section 3: the "your turn" pit, then the exit that runs away.
            yield return Until(() => Session.ActiveCheckpoint == 2, "the third checkpoint");
            Picture("starter-section-3");
            yield return JumpAcross(57.6f, 61f, "the Your Turn pit");
            Keys(Key.RightArrow);
            yield return Until(() => Session.Phase == LevelPhase.Won, "reaching the exit");
            Keys();
            Assert.IsTrue(AnyTrapMoved(offset => offset.x > 1f), "the Runaway Exit ran");
            Assert.Greater(X, 70f, "the exit was caught after it ran");
            Assert.AreEqual(3, Session.Deaths);
            yield return Frames(2);
            Picture("starter-won");
        }
    }
}
#endif
