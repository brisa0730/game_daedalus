using System;
using System.Reflection;
using NUnit.Framework;
using Orcapafea.Daedalus.Editor;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Share my game's build core without a WebGL build: settings pin, exception watch, fail-closed outcome, stops, texts.</summary>
    public class ShareBuildTests
    {
        const string Folder = "Assets/DaedalusTestTemp";

        WebGLCompressionFormat compression;
        bool decompressionFallback;
        bool dataCaching;
        bool nameFilesAsHashes;
        bool threadsSupport;

        [SetUp]
        public void SaveSettings()
        {
            compression = PlayerSettings.WebGL.compressionFormat;
            decompressionFallback = PlayerSettings.WebGL.decompressionFallback;
            dataCaching = PlayerSettings.WebGL.dataCaching;
            nameFilesAsHashes = PlayerSettings.WebGL.nameFilesAsHashes;
            threadsSupport = PlayerSettings.WebGL.threadsSupport;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void RestoreSettings()
        {
            PlayerSettings.WebGL.compressionFormat = compression;
            PlayerSettings.WebGL.decompressionFallback = decompressionFallback;
            PlayerSettings.WebGL.dataCaching = dataCaching;
            PlayerSettings.WebGL.nameFilesAsHashes = nameFilesAsHashes;
            PlayerSettings.WebGL.threadsSupport = threadsSupport;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void ThePinSetsTheZipsSettingsAndRestoresTheStudents()
        {
            SetStudentSettings();
            using (new ShareBuild.PlayerSettingsPin())
            {
                Assert.AreEqual(WebGLCompressionFormat.Brotli, PlayerSettings.WebGL.compressionFormat);
                Assert.IsFalse(PlayerSettings.WebGL.decompressionFallback);
                Assert.IsFalse(PlayerSettings.WebGL.dataCaching);
                Assert.IsFalse(PlayerSettings.WebGL.nameFilesAsHashes);
                Assert.IsFalse(PlayerSettings.WebGL.threadsSupport);
            }
            AssertStudentSettings();
        }

        [Test]
        public void ThePinRestoresWhenTheBuildThrows()
        {
            SetStudentSettings();
            Assert.Throws<InvalidOperationException>(() =>
            {
                using (new ShareBuild.PlayerSettingsPin())
                    throw new InvalidOperationException("build failed");
            });
            AssertStudentSettings();
        }

        [Test]
        public void TheWatchCountsExceptionsOnly()
        {
            using (var watch = new ShareBuild.ExceptionWatch())
            {
                LogAssert.Expect(LogType.Exception, "InvalidOperationException: probe");
                Debug.LogException(new InvalidOperationException("probe"));
                LogAssert.Expect(LogType.Error, "an error");
                Debug.LogError("an error");
                Debug.LogWarning("a warning");
                Assert.AreEqual(1, watch.Count);
                StringAssert.Contains("probe", watch.First);
            }
        }

        [Test]
        public void TheWatchStopsCountingWhenClosed()
        {
            var watch = new ShareBuild.ExceptionWatch();
            watch.Dispose();
            LogAssert.Expect(LogType.Exception, "InvalidOperationException: after");
            Debug.LogException(new InvalidOperationException("after"));
            Assert.AreEqual(0, watch.Count);
        }

        [Test]
        public void TheOutcomeFailsClosed()
        {
            Assert.IsNull(ShareBuild.Outcome(BuildResult.Succeeded, 0, true));
            Assert.AreEqual(ShareTexts.BuildException, ShareBuild.Outcome(BuildResult.Succeeded, 1, true));
            Assert.AreEqual(ShareTexts.BuildFailed, ShareBuild.Outcome(BuildResult.Succeeded, 0, false));
            Assert.AreEqual(ShareTexts.BuildFailed, ShareBuild.Outcome(BuildResult.Failed, 0, true));
            Assert.AreEqual(ShareTexts.BuildFailed, ShareBuild.Outcome(BuildResult.Unknown, 0, true));
            Assert.AreEqual(ShareTexts.BuildStopped, ShareBuild.Outcome(BuildResult.Cancelled, 0, true));
        }

        [Test]
        public void NoPlayerStopsFirstEvenInAnUntitledScene()
        {
            Assert.AreEqual(ExpectedOr(ShareTexts.NoPlayer), ShareBuild.CheckBeforeBuild());
        }

        [Test]
        public void APlayerWithoutAMainCameraStops()
        {
            NewPlayer("Player");
            Assert.AreEqual(ExpectedOr(ShareTexts.NoMainCamera), ShareBuild.CheckBeforeBuild());
        }

        [Test]
        public void APlayerAndAMainCameraPass()
        {
            NewPlayer("Player");
            NewMainCamera();
            Assert.AreEqual(ExpectedOr(null), ShareBuild.CheckBeforeBuild());
        }

        [Test]
        public void RunOnAnUntitledSceneStopsWithTheSaveTextAndChangesNothing()
        {
            NewPlayer("Player");
            NewMainCamera();
            SetStudentSettings();
            ShareBuild.Result result = ShareBuild.Run("Spike Hell", "unused.zip");
            Assert.IsFalse(result.Ok);
            Assert.AreEqual(ExpectedOr(ShareTexts.Untitled), result.Message);
            AssertStudentSettings();
        }

        [Test]
        public void AnUncheckedPlayerCountsAsNoPlayer()
        {
            NewPlayer("Player").enabled = false;
            NewMainCamera();
            Assert.IsNull(ShareBuild.SharedPlayer());
            Assert.AreEqual(ExpectedOr(ShareTexts.NoPlayer), ShareBuild.CheckBeforeBuild());
        }

        [Test]
        public void TheFirstPlayerInHierarchyOrderIsShared()
        {
            Player second = NewPlayer("B");
            Player first = NewPlayer("A");
            first.transform.SetSiblingIndex(0);
            Assert.AreSame(first, ShareBuild.SharedPlayer());
            Assert.AreNotSame(second, ShareBuild.SharedPlayer());
        }

        [Test]
        public void APlayerOrCameraInAnotherOpenSceneDoesNotCount()
        {
            UnityEngine.SceneManagement.Scene active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            AssetDatabase.CreateFolder("Assets", "DaedalusTestTemp");
            EditorSceneManager.SaveScene(active, Folder + "/Active.unity"); // Unity won't open a second scene beside an unsaved one
            UnityEngine.SceneManagement.Scene other = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(other);
            NewPlayer("Elsewhere");
            NewMainCamera();
            UnityEngine.SceneManagement.SceneManager.SetActiveScene(active);
            Assert.IsNull(ShareBuild.SharedPlayer(), "the Player is in the other scene");
            Assert.AreEqual(ExpectedOr(ShareTexts.NoPlayer), ShareBuild.CheckBeforeBuild());
            Assert.IsNull(StageWalk.MainCameraOf(active), "so is the camera");
        }

        [Test]
        public void NoTextNamesCode()
        {
            foreach (FieldInfo field in typeof(ShareTexts).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
            {
                if (field.FieldType != typeof(string))
                    continue;
                string text = (string)field.GetValue(null);
                foreach (string code in new[] { "Orcapafea", ".cs", "PlayerSettings", "EditorBuildSettings", "Exception" })
                    StringAssert.DoesNotContain(code, text, field.Name);
                Assert.Less(text.Length, 350, field.Name);
            }
        }

        [Test]
        public void TheTitleIsRememberedPerScene()
        {
            const string a = "00000000000000000000000000000a11";
            const string b = "00000000000000000000000000000b22";
            try
            {
                ShareMyGame.SaveTitle(a, "함정 지옥");
                ShareMyGame.SaveTitle(b, "Spike Hell");
                Assert.AreEqual("함정 지옥", ShareMyGame.LoadTitle(a));
                Assert.AreEqual("Spike Hell", ShareMyGame.LoadTitle(b));
            }
            finally
            {
                EditorUserSettings.SetConfigValue("Daedalus.Share.Title." + a, null);
                EditorUserSettings.SetConfigValue("Daedalus.Share.Title." + b, null);
            }
            Assert.AreEqual("", ShareMyGame.LoadTitle(a), "cleared");
        }

        [TestCase(50000001L, "Your game is 50.1 MB.")]
        [TestCase(61234567L, "Your game is 61.3 MB.")]
        [TestCase(70000000L, "Your game is 70.0 MB.")]
        public void TheTooBigMessageRoundsUp(long bytes, string start)
        {
            StringAssert.StartsWith(start, ShareTexts.TooBigFor(bytes));
        }

        // The stops before the scene checks depend on this machine: both test editors have WebGL Build Support (CLAUDE.md).
        static string ExpectedOr(string expected)
        {
            return BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.WebGL, BuildTarget.WebGL) ? expected : ShareTexts.NoWebModule;
        }

        static Player NewPlayer(string name)
        {
            var playerObject = new GameObject(name);
            playerObject.AddComponent<SpriteRenderer>();
            return playerObject.AddComponent<Player>();
        }

        static void NewMainCamera()
        {
            new GameObject("Main Camera") { tag = "MainCamera" }.AddComponent<Camera>().orthographic = true;
        }

        static void SetStudentSettings()
        {
            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
            PlayerSettings.WebGL.decompressionFallback = true;
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.nameFilesAsHashes = true;
            PlayerSettings.WebGL.threadsSupport = true;
        }

        static void AssertStudentSettings()
        {
            Assert.AreEqual(WebGLCompressionFormat.Gzip, PlayerSettings.WebGL.compressionFormat);
            Assert.IsTrue(PlayerSettings.WebGL.decompressionFallback);
            Assert.IsTrue(PlayerSettings.WebGL.dataCaching);
            Assert.IsTrue(PlayerSettings.WebGL.nameFilesAsHashes);
            Assert.IsTrue(PlayerSettings.WebGL.threadsSupport);
        }
    }
}
