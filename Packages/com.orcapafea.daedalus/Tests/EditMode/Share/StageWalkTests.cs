using System.Linq;
using NUnit.Framework;
using Orcapafea.Daedalus.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Share my game following a game's stages: what gets built, what stops it, what it warns about.</summary>
    public class StageWalkTests
    {
        const string Folder = "Assets/DaedalusTestTemp";
        const string A = Folder + "/StageA.unity";
        const string B = Folder + "/StageB.unity";
        const string C = Folder + "/StageC.unity";
        const string Win = "";

        [SetUp]
        public void SaveEmptyStages()
        {
            AssetDatabase.CreateFolder("Assets", "DaedalusTestTemp");
            foreach (string path in new[] { A, B, C })
            {
                EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), path);
            }
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void DeleteTheFolder()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Folder);
        }

        // Rewrites a stage with one Exit per link. Win is an empty link.
        static void Stage(string path, params string[] links)
        {
            Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            new GameObject("Player").AddComponent<Player>();
            new GameObject("Main Camera", typeof(Camera)).tag = "MainCamera";
            AddExits(links);
            EditorSceneManager.SaveScene(scene);
        }

        static void AddExits(string[] links)
        {
            for (int i = 0; i < links.Length; i++)
            {
                Exit exit = new GameObject("Exit " + i).AddComponent<Exit>();
                exit.NextStageGuid = links[i] == Win ? "" : AssetDatabase.AssetPathToGUID(links[i]);
            }
        }

        static StageWalk WalkFrom(string path)
        {
            return StageWalk.From(EditorSceneManager.OpenScene(path, OpenSceneMode.Single));
        }

        [Test]
        public void ALinearChainListsEveryStageEndingInAWin()
        {
            Stage(A, B);
            Stage(B, C);
            Stage(C, Win);
            StageWalk walk = WalkFrom(A);
            Assert.IsNull(walk.Stop);
            CollectionAssert.AreEqual(new[] { A, B, C }, walk.Stages);
            Assert.AreEqual("StageA → StageB → StageC → win", walk.Chain);
            Assert.IsEmpty(walk.Warnings);
        }

        [Test]
        public void OneSceneIsOneStageEndingInAWin()
        {
            Stage(A, Win);
            StageWalk walk = WalkFrom(A);
            Assert.IsNull(walk.Stop);
            CollectionAssert.AreEqual(new[] { A }, walk.Stages);
            Assert.AreEqual("StageA → win", walk.Chain);
        }

        [Test]
        public void OneSceneWithNoExitStillSharesWithAWarningAsItAlwaysHas()
        {
            Stage(A);
            StageWalk walk = WalkFrom(A);
            Assert.IsNull(walk.Stop);
            CollectionAssert.AreEqual(new[] { string.Format(ShareTexts.StageNoExit, "StageA") }, walk.Warnings);
        }

        [Test]
        public void TheScenesOpenedToReadAreClosedAndTheActiveSceneStays()
        {
            Stage(A, B);
            Stage(B, C);
            Stage(C, Win);
            Scene start = EditorSceneManager.OpenScene(A, OpenSceneMode.Single);
            StageWalk.From(start);
            Assert.AreEqual(1, SceneManager.sceneCount);
            Assert.AreEqual(start, SceneManager.GetActiveScene());
        }

        [Test]
        public void AStageAlreadyOpenStaysOpen()
        {
            Stage(A, B);
            Stage(B, Win);
            Scene start = EditorSceneManager.OpenScene(A, OpenSceneMode.Single);
            Scene second = EditorSceneManager.OpenScene(B, OpenSceneMode.Additive);
            Assert.IsNull(StageWalk.From(start).Stop);
            Assert.IsTrue(second.isLoaded);
            Assert.AreEqual(2, SceneManager.sceneCount);
        }

        [Test]
        public void ABrokenLinkStopsNamingTheStageAndTheExit()
        {
            Stage(A, B);
            Stage(B, C);
            Assert.IsTrue(AssetDatabase.DeleteAsset(C), "precondition");
            StageWalk walk = WalkFrom(A);
            Assert.AreEqual(string.Format(ShareTexts.StageBroken, "StageB", "Exit 0"), walk.Stop);
        }

        [Test]
        public void ADisabledExitIsIgnored()
        {
            Stage(A, Win);
            Scene scene = EditorSceneManager.OpenScene(A, OpenSceneMode.Single);
            Exit off = new GameObject("Off").AddComponent<Exit>();
            off.NextStageGuid = "00000000000000000000000000000001";
            off.enabled = false;
            EditorSceneManager.SaveScene(scene);
            StageWalk walk = WalkFrom(A);
            Assert.IsNull(walk.Stop);
            CollectionAssert.AreEqual(new[] { A }, walk.Stages);
        }

        [Test]
        public void ALoopNoExitCanWinStops()
        {
            Stage(A, B);
            Stage(B, A);
            StageWalk walk = WalkFrom(A);
            Assert.AreEqual(string.Format(ShareTexts.NoWayToWin, "StageA → StageB"), walk.Stop);
        }

        [Test]
        public void AChainEndingWithNoExitStops()
        {
            Stage(A, B);
            Stage(B);
            StringAssert.StartsWith("Nobody could ever win", WalkFrom(A).Stop);
        }

        [Test]
        public void ABackLinkInAChainThatCanBeWonWarnsAndBuildsEveryStage()
        {
            Stage(A, B);
            Stage(B, A, C);
            Stage(C, Win);
            StageWalk walk = WalkFrom(A);
            Assert.IsNull(walk.Stop, "a fake Exit that sends you back is a legitimate troll");
            CollectionAssert.AreEqual(new[] { A, B, C }, walk.Stages);
            CollectionAssert.Contains(walk.Warnings, string.Format(ShareTexts.BackLink, "StageB", "StageA"));
        }

        [Test]
        public void ExitsGoingToDifferentPlacesWarnAndEveryStageIsBuilt()
        {
            Stage(A, B, C);
            Stage(B, Win);
            Stage(C, Win);
            StageWalk walk = WalkFrom(A);
            Assert.IsNull(walk.Stop);
            CollectionAssert.AreEquivalent(new[] { A, B, C }, walk.Stages);
            CollectionAssert.Contains(walk.Warnings, string.Format(ShareTexts.ExitsDiffer, "StageA", "StageB, StageC"));
        }

        [Test]
        public void TwoExitsGoingTheSamePlaceDoNotWarn()
        {
            Stage(A, B, B);
            Stage(B, Win);
            Assert.IsFalse(WalkFrom(A).Warnings.Any(w => w.Contains("different places")));
        }

        [Test]
        public void AStageWithNoPlayerStopsNamingIt()
        {
            Stage(A, B);
            Scene scene = EditorSceneManager.OpenScene(B, OpenSceneMode.Single);
            new GameObject("Main Camera", typeof(Camera)).tag = "MainCamera";
            AddExits(new[] { Win });
            EditorSceneManager.SaveScene(scene);
            Assert.AreEqual(string.Format(ShareTexts.StageNoPlayer, "StageB"), WalkFrom(A).Stop);
        }

        [Test]
        public void AStageWithNoMainCameraStopsNamingIt()
        {
            Stage(A, B);
            Scene scene = EditorSceneManager.OpenScene(B, OpenSceneMode.Single);
            new GameObject("Player").AddComponent<Player>();
            AddExits(new[] { Win });
            EditorSceneManager.SaveScene(scene);
            Assert.AreEqual(string.Format(ShareTexts.StageNoCamera, "StageB"), WalkFrom(A).Stop);
        }

        [Test]
        public void AnotherSceneLinkingToTheStartWarnsNamingIt()
        {
            Stage(A, B);
            Stage(B, C);
            Stage(C, Win);
            StageWalk walk = WalkFrom(B);
            Assert.IsNull(walk.Stop, "a warning, never a stop");
            CollectionAssert.Contains(walk.Warnings, string.Format(ShareTexts.LinksHere, "StageA"));
        }

        [Test]
        public void NothingLinkingToTheStartGivesNoSuchWarning()
        {
            Stage(A, B);
            Stage(B, Win);
            Assert.IsFalse(WalkFrom(A).Warnings.Any(w => w.Contains("goes to this scene")));
        }

        [Test]
        public void TheWalkFollowsALinkThroughARename()
        {
            Stage(A, B);
            Stage(B, Win);
            string renamed = Folder + "/Renamed.unity";
            Assert.IsEmpty(AssetDatabase.MoveAsset(B, renamed));
            CollectionAssert.AreEqual(new[] { A, renamed }, WalkFrom(A).Stages);
        }
    }
}
