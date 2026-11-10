using NUnit.Framework;
using Orcapafea.Daedalus.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>An Exit's Next stage link: empty, a scene, or broken; through a rename and into a build.</summary>
    public class ExitLinkTests
    {
        const string Folder = "Assets/DaedalusTestTemp";
        const string ScenePath = Folder + "/Stage2.unity";

        [SetUp]
        public void SaveATargetScene()
        {
            AssetDatabase.CreateFolder("Assets", "DaedalusTestTemp");
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), ScenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void DeleteTheFolder()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetDatabase.DeleteAsset(Folder);
        }

        static Exit NewExit(string guid)
        {
            Exit exit = new GameObject("Exit").AddComponent<Exit>();
            exit.NextStageGuid = guid;
            return exit;
        }

        static string Guid => AssetDatabase.AssetPathToGUID(ScenePath);

        [Test]
        public void AnEmptySlotIsNoLink()
        {
            Assert.AreEqual(ExitLink.LinkKind.None, NewExit("").Link.Kind);
        }

        [Test]
        public void ASavedSceneIsALinkToIt()
        {
            ExitLink link = NewExit(Guid).Link;
            Assert.AreEqual(ExitLink.LinkKind.Scene, link.Kind);
            Assert.AreEqual(ScenePath, link.Path);
        }

        [Test]
        public void ALinkFollowsItsSceneThroughAMove()
        {
            Exit exit = NewExit(Guid);
            string moved = Folder + "/Renamed.unity";
            Assert.IsEmpty(AssetDatabase.MoveAsset(ScenePath, moved));
            ExitLink link = exit.Link;
            Assert.AreEqual(ExitLink.LinkKind.Scene, link.Kind);
            Assert.AreEqual(moved, link.Path);
        }

        [Test]
        public void ADeletedSceneIsBrokenNotEmpty()
        {
            Exit exit = NewExit(Guid);
            Assert.IsTrue(AssetDatabase.DeleteAsset(ScenePath), "precondition");
            Assert.AreEqual(ExitLink.LinkKind.Broken, exit.Link.Kind);
        }

        [Test]
        public void ALinkToSomethingThatIsNotASceneIsBroken()
        {
            string asset = Folder + "/NotAScene.asset";
            AssetDatabase.CreateAsset(ScriptableObject.CreateInstance<DaedalusTile>(), asset);
            Assert.AreEqual(ExitLink.LinkKind.Broken, NewExit(AssetDatabase.AssetPathToGUID(asset)).Link.Kind);
        }

        [Test]
        public void InABuildAMissingPathIsBrokenSoItCanNeverReadAsAWin()
        {
            Assert.AreEqual(ExitLink.LinkKind.None, StageLoad.ResolveBuilt("", "").Kind);
            Assert.AreEqual(ExitLink.LinkKind.Broken, StageLoad.ResolveBuilt("abc", "").Kind);
            ExitLink link = StageLoad.ResolveBuilt("abc", ScenePath);
            Assert.AreEqual(ExitLink.LinkKind.Scene, link.Kind);
            Assert.AreEqual(ScenePath, link.Path);
        }

        [Test]
        public void TheStampWritesEachExitsPathEvenOnAnInactiveObject()
        {
            Exit linked = NewExit(Guid);
            Exit inactive = NewExit(Guid);
            inactive.gameObject.SetActive(false);
            Exit empty = NewExit("");
            StagePathStamp.Stamp(SceneManager.GetActiveScene());
            Assert.AreEqual(ScenePath, linked.BuiltNextStagePath);
            Assert.AreEqual(ScenePath, inactive.BuiltNextStagePath);
            Assert.AreEqual("", empty.BuiltNextStagePath);
        }

        [Test]
        public void PlayModeHasNoReportAndIsNotStamped()
        {
            Exit linked = NewExit(Guid);
            new StagePathStamp().OnProcessScene(SceneManager.GetActiveScene(), null);
            Assert.AreEqual("", linked.BuiltNextStagePath);
        }

        [Test]
        public void ABrokenLinkFailsTheBuildNamingTheExit()
        {
            NewExit("00000000000000000000000000000001");
            var failed = Assert.Throws<BuildFailedException>(() => StagePathStamp.Stamp(SceneManager.GetActiveScene()));
            StringAssert.Contains("Exit", failed.Message);
        }

        [Test]
        public void ABrokenLinkOnATurnedOffExitDoesNotFailTheBuild()
        {
            NewExit("00000000000000000000000000000001").enabled = false;
            Assert.DoesNotThrow(() => StagePathStamp.Stamp(SceneManager.GetActiveScene()));
        }
    }
}
