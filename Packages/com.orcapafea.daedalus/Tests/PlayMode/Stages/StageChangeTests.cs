#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Stage changes through a real load and the real Exit link: the fade order, the hold, the total, Play again.</summary>
    [PrebuildSetup(typeof(StageScenes)), PostBuildCleanup(typeof(StageScenes))]
    public class StageChangeTests : StagePlayTest
    {
        IEnumerator Cross()
        {
            Player old = Player;
            Keys(Key.RightArrow);
            yield return Until(() => Arrived(old), "reaching the next stage");
        }

        IEnumerator CrossToLastStage()
        {
            yield return LoadScene(StageScenes.A);
            yield return Cross();
            yield return Cross();
            Assert.AreEqual("Stage C", Player.gameObject.scene.name, "precondition");
            yield return Until(() => !Run.Revealing, "the last stage showing");
        }

        IEnumerator WinAndWait()
        {
            Keys(Key.RightArrow);
            yield return Until(() => Session.Phase == LevelPhase.Won, "reaching the exit");
            Keys();
            yield return Until(() => Session.CanPlayAgain, "the win screen's first second");
        }

        IEnumerator DieOnTheSpike(int deaths)
        {
            Keys(Key.LeftArrow);
            yield return Until(() => Session.Deaths == deaths, "dying on the spike");
            Keys();
            yield return Until(() => Session.Phase == LevelPhase.Playing, "back in play");
            yield return Frames(5);
        }

        void Picture(string name)
        {
            Camera camera = Camera.main;
            Assert.IsNotNull(camera);
            var target = new RenderTexture(640, 360, 24);
            Created.Add(target);
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            TestImages.Save(TestImages.ReadBack(target, Created), name);
        }

        [UnityTest]
        public IEnumerator ALinkedExitLoadsTheNextStageBehindBlackAndNeverShowsAWin()
        {
            yield return LoadScene(StageScenes.A);
            Player first = Player;
            PlayHud hud = first.Hud;
            Keys(Key.RightArrow);
            yield return Until(() =>
            {
                if (first != null)
                {
                    Assert.IsFalse(hud.Win.activeSelf, "no win screen on the way out");
                }
                return Arrived(first);
            }, "reaching the next stage");
            StageRun run = Run;
            Assert.AreEqual("Stage B", Player.gameObject.scene.name);
            Assert.AreEqual(1f, run.FadeAlpha, "still black when the new Player has a session");
            Assert.That(run.LoadBeganFrame - run.FullyBlackFrame, Is.GreaterThanOrEqualTo(2), "black was shown before the load");
            Assert.IsFalse(Player.Hud.Win.activeSelf);
            Assert.AreEqual(0, Session.Deaths, "the Exit is no death");
            Assert.IsFalse(EditorBuildSettings.scenes.Length > 0 && System.Array.Exists(EditorBuildSettings.scenes, s => s.path == StageScenes.B), "Build Settings untouched");
            Picture("stages-arrival");
        }

        [UnityTest]
        public IEnumerator NothingMovesUntilTheFadeHasCleared()
        {
            yield return LoadScene(StageScenes.A);
            yield return Cross();
            StageRun run = Run;
            Vector2 feet = SimFeetWorld(Player);
            Assert.IsTrue(run.Revealing, "precondition");
            bool longFrameRun = false;
            yield return Until(() =>
            {
                if (!run.Revealing)
                {
                    return true;
                }
                if (!longFrameRun)
                {
                    // Capture framerate fixes every real frame at 1/60 s, so the long frame is injected.
                    longFrameRun = true;
                    Player.RunFrame(0.5f);
                }
                Assert.AreEqual(feet, SimFeetWorld(Player), "held still during the reveal");
                Assert.AreEqual(0d, Session.InterpolationFraction, "no time banked during the reveal");
                return false;
            }, "the reveal ending");
            Assert.IsTrue(longFrameRun, "precondition: a long frame ran during the hold");
            float x = SimFeetWorld(Player).x;
            yield return Until(() => SimFeetWorld(Player).x > x + 1f, "the held direction carrying on after the reveal");
        }

        [UnityTest]
        public IEnumerator AHeldJumpNeedsAFreshPressOnArrival()
        {
            yield return LoadScene(StageScenes.A);
            Player old = Player;
            Keys(Key.RightArrow, Key.Space);
            // Standing start; Space held from the first frame is a single jump, over before the Exit.
            yield return Until(() => Arrived(old), "reaching the next stage with Space held");
            yield return Until(() => !Run.Revealing, "the reveal ending");
            yield return Frames(20);
            Assert.AreEqual(1f, Session.Current.Y - Session.Map.WorldToSim(new Vector2(0, 0)).y, 1e-3f, "a held Space doesn't jump");
            Assert.IsTrue(Session.Current.IsStanding);
            Keys();
            yield return Frames(2);
            Keys(Key.Space);
            yield return Frames(10);
            Assert.IsFalse(Session.Current.IsStanding, "a fresh press jumps");
        }

        [UnityTest]
        public IEnumerator AFirstPlayIgnoresADirectionAlreadyHeldAsItAlwaysHas()
        {
            Keys(Key.RightArrow);
            yield return null;
            yield return LoadScene(StageScenes.C);
            float x = X;
            yield return Frames(30);
            Assert.AreEqual(x, X, "only arriving at a stage carries a held direction over");
        }

        [UnityTest]
        public IEnumerator ADuplicatedPlayerMakesOneChangeAndCountsDeathsOnce()
        {
            yield return LoadScene(StageScenes.A);
            Player first = Player;
            Object.Instantiate(first.gameObject);
            yield return Frames(2);
            Keys(Key.RightArrow);
            yield return Until(() => PlayStartScan.FindActive<StageRun>().Length > 0, "reaching the Exit");
            Assert.AreEqual(1, PlayStartScan.FindActive<StageRun>().Length, "one run");
            yield return Until(() => Arrived(first), "arriving, with one Player in the next stage");
            Assert.AreEqual(1, PlayStartScan.FindActive<StageRun>().Length, "still one run");
        }

        [UnityTest]
        public IEnumerator ASecondChangeWhileOneIsRunningIsIgnored()
        {
            yield return LoadScene(StageScenes.A);
            Player first = Player;
            StageRun run = StageRun.Create(StageScenes.A, false);
            run.GoNext(StageScenes.B, 1);
            run.GoNext(StageScenes.B, 1);
            run.PlayAgain();
            yield return Until(() => Arrived(first), "arriving at the next stage");
            Assert.AreEqual(1, run.CarriedDeaths, "the deaths count once");
            Assert.AreEqual("Stage B", Player.gameObject.scene.name);
        }

        [UnityTest]
        public IEnumerator AJumpPressedDuringTheRevealIsDropped()
        {
            yield return LoadScene(StageScenes.A);
            yield return Cross();
            Keys();
            yield return Frames(1);
            Keys(Key.Space);
            yield return Frames(1);
            Keys();
            Assert.IsTrue(Run.Revealing, "precondition: the press came during the reveal");
            yield return Until(() => !Run.Revealing, "the reveal ending");
            yield return Frames(10);
            Assert.IsTrue(Session.Current.IsStanding, "the press was dropped");
        }

        [UnityTest]
        public IEnumerator DeathsAddUpAcrossStagesAndPlayAgainGoesBackToTheStart()
        {
            yield return LoadScene(StageScenes.A);
            yield return DieOnTheSpike(1);
            yield return Cross();
            Assert.AreEqual("1", Player.Hud.Count.text, "carried over");
            Assert.AreEqual(Vector3.one, Player.Hud.Counter.localScale, "no bump for arriving");
            yield return Until(() => !Run.Revealing, "the reveal ending");
            yield return DieOnTheSpike(1);
            Assert.AreEqual("2", Player.Hud.Count.text, "a death on this stage adds to it");
            yield return Cross();
            yield return Until(() => !Run.Revealing, "the reveal ending");
            yield return WinAndWait();
            Assert.IsTrue(Player.Hud.Win.activeSelf, "the last Exit wins");
            Assert.AreEqual("You made it!", Player.Hud.Title.text);
            Assert.AreEqual("Deaths: 2", Player.Hud.WinDeaths.text, "the game's total");
            Assert.IsFalse(Player.Hud.SaveNote.gameObject.activeSelf, "the saved first stage needs no note");

            Player old = Player;
            Keys(Key.A);
            yield return Until(() => Arrived(old), "Play again loading the start");
            Keys();
            Assert.AreEqual("Stage A", Player.gameObject.scene.name);
            Assert.AreEqual("0", Player.Hud.Count.text);
            yield return Until(() => Run == null, "the run ending after the reveal");
        }

        [UnityTest]
        public IEnumerator PlayAgainGoesBackToWherePlayBegan()
        {
            yield return LoadScene(StageScenes.B);
            yield return Cross();
            yield return Until(() => !Run.Revealing, "the reveal ending");
            yield return WinAndWait();
            Player old = Player;
            Keys(Key.A);
            yield return Until(() => Arrived(old), "Play again");
            Keys();
            Assert.AreEqual("Stage B", Player.gameObject.scene.name);
        }

        [UnityTest]
        public IEnumerator PlayBegunOnTheLastStagePlaysAgainInPlace()
        {
            yield return LoadScene(StageScenes.C);
            Player only = Player;
            yield return WinAndWait();
            Assert.IsNull(Run, "nothing linked was reached");
            Keys(Key.A);
            yield return null;
            Keys();
            yield return Frames(2);
            Assert.AreSame(only, Player);
            Assert.AreEqual(LevelPhase.Playing, Session.Phase);
        }

        [UnityTest]
        public IEnumerator AFailedLoadShowsANoticeAndPlayAgainRestartsInPlace()
        {
            yield return LoadScene(StageScenes.D);
            Player only = Player;
            Assert.IsTrue(AssetDatabase.DeleteAsset(StageScenes.E), "precondition");
            LogAssert.ignoreFailingMessages = true;
            Keys(Key.RightArrow);
            yield return Until(() => Run != null && Run.LoadFailed, "the load failing");
            Keys();
            yield return Until(() => Session.CanPlayAgain, "the win screen's first second");
            yield return Frames(30);
            Assert.IsTrue(Player.Hud.Win.activeSelf, "the player isn't left with nothing");
            StringAssert.StartsWith("Couldn't open Stage E", Player.Hud.Title.text);
            Keys(Key.A);
            yield return null;
            Keys();
            yield return Frames(2);
            LogAssert.ignoreFailingMessages = false;
            Assert.AreSame(only, Player);
            Assert.AreEqual(LevelPhase.Playing, Session.Phase, "restarted in place");
            Assert.AreEqual("You made it!", Player.Hud.Title.text);
        }

        [UnityTest]
        public IEnumerator AnEmptyFirstSceneShowsTheNoticeAndALaterWinReadsNormally()
        {
            yield return CrossToLastStage();
            Run.FirstScene = "";
            yield return WinAndWait();
            Player only = Player;
            LogAssert.ignoreFailingMessages = true;
            Keys(Key.A);
            yield return Until(() => Run.LoadFailed, "the load failing");
            Keys();
            yield return Frames(30);
            StringAssert.StartsWith("Couldn't open the first scene", Player.Hud.Title.text);
            Keys(Key.A);
            yield return null;
            Keys();
            yield return Frames(2);
            LogAssert.ignoreFailingMessages = false;
            Assert.AreSame(only, Player);
            Assert.AreEqual(LevelPhase.Playing, Session.Phase, "restarted in place");

            yield return WinAndWait();
            Assert.AreEqual("You made it!", Player.Hud.Title.text);
            StringAssert.StartsWith("Deaths: ", Player.Hud.WinDeaths.text);
        }

        [UnityTest]
        public IEnumerator TheWinScreenSaysSoWhenTheFirstStageHadUnsavedEdits()
        {
            yield return CrossToLastStage();
            Run.FirstSceneUnsaved = true;
            LogAssert.Expect(LogType.Warning, PlayerWarnings.PlayAgainUsesSaved("Stage A"));
            yield return WinAndWait();
            Assert.IsTrue(Player.Hud.SaveNote.gameObject.activeSelf);
            StringAssert.Contains("Stop, then press Ctrl+S", Player.Hud.SaveNote.text);
            StringAssert.Contains("Stage A", Player.Hud.SaveNote.text);
        }

        [UnityTest]
        public IEnumerator APlayerInAnotherLoadedSceneIsWarnedAbout()
        {
            var logged = new List<string>();
            Application.LogCallback capture = (message, stack, type) => logged.Add(message);
            Application.logMessageReceived += capture;
            try
            {
                yield return LoadAdditive(StageScenes.C);
                yield return LoadAdditive(StageScenes.B);
                yield return Frames(2);
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }
            CollectionAssert.Contains(logged, PlayerWarnings.PlayerInAnotherScene("Stage C"));
        }

        [UnityTest]
        public IEnumerator OnePlayerGivesNoWarningAboutAnotherScene()
        {
            var logged = new List<string>();
            Application.LogCallback capture = (message, stack, type) => logged.Add(message);
            Application.logMessageReceived += capture;
            try
            {
                yield return LoadScene(StageScenes.C);
                yield return Frames(2);
            }
            finally
            {
                Application.logMessageReceived -= capture;
            }
            Assert.IsFalse(logged.Exists(m => m.Contains("is open at the same time")));
        }

        static IEnumerator LoadAdditive(string path)
        {
            AsyncOperation loading = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!loading.isDone)
            {
                yield return null;
            }
        }
    }
}
#endif
