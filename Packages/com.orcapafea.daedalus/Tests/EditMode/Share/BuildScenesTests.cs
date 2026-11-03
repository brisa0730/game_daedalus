using System;
using System.IO;
using NUnit.Framework;
using Orcapafea.Daedalus.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The scene-list guard: exactly the shared scene during a build, the student's list back afterwards (ISS-001).</summary>
    public class BuildScenesTests
    {
        const string Folder = "Assets/DaedalusTestTemp";
        const string ScenePath = Folder + "/BuildScenesTest.unity";
        const string MissingPath = "Assets/DaedalusTestTemp/NoSuchScene.unity";

        EditorBuildSettingsScene[] original;

        [SetUp]
        public void SaveListAndMakeScene()
        {
            original = EditorBuildSettings.scenes;
            if (!AssetDatabase.IsValidFolder(Folder))
                AssetDatabase.CreateFolder("Assets", "DaedalusTestTemp");
            EditorSceneManager.SaveScene(EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single), ScenePath);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void RestoreListAndDeleteScene()
        {
            EditorBuildSettings.scenes = original;
            AssetDatabase.DeleteAsset(Folder);
        }

        [Test]
        public void UseHoldsExactlyTheSceneAndDisposeRestoresTheList()
        {
            var start = new[]
            {
                new EditorBuildSettingsScene(MissingPath, true),
                new EditorBuildSettingsScene(ScenePath, false),
            };
            EditorBuildSettings.scenes = start;
            EditorBuildSettingsScene[] before = EditorBuildSettings.scenes;

            using (BuildScenes.Use(new[] { ScenePath }))
            {
                EditorBuildSettingsScene[] during = EditorBuildSettings.scenes;
                Assert.AreEqual(1, during.Length);
                Assert.AreEqual(ScenePath, during[0].path);
                Assert.IsTrue(during[0].enabled);
            }

            AssertSameList(before, EditorBuildSettings.scenes);
        }

        [Test]
        public void AMissingSceneThrowsNamingItAndLeavesTheList()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            EditorBuildSettingsScene[] before = EditorBuildSettings.scenes;

            var error = Assert.Throws<FileNotFoundException>(() => BuildScenes.Use(new[] { MissingPath }));
            StringAssert.Contains(MissingPath, error.Message);
            AssertSameList(before, EditorBuildSettings.scenes);
        }

        [Test]
        public void TheListComesBackWhenTheBuildThrows()
        {
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(MissingPath, true) };
            EditorBuildSettingsScene[] before = EditorBuildSettings.scenes;

            Assert.Throws<InvalidOperationException>(() =>
            {
                using (BuildScenes.Use(new[] { ScenePath }))
                    throw new InvalidOperationException("build failed");
            });
            AssertSameList(before, EditorBuildSettings.scenes);
        }

        static void AssertSameList(EditorBuildSettingsScene[] expected, EditorBuildSettingsScene[] actual)
        {
            Assert.AreEqual(expected.Length, actual.Length);
            for (int i = 0; i < expected.Length; i++)
            {
                Assert.AreEqual(expected[i].path, actual[i].path);
                Assert.AreEqual(expected[i].enabled, actual[i].enabled);
                Assert.AreEqual(expected[i].guid, actual[i].guid);
            }
        }
    }
}
