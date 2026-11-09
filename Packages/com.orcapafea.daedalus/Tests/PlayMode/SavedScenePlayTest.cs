#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Plays a saved scene with the real Player and keyboard: the imported Starter, or a student's project.</summary>
    public abstract class SavedScenePlayTest : MovementPlayTest
    {
        const int Limit = 900;
        protected readonly List<Object> Created = new List<Object>();
        Scene scene;
        protected Player Player;

        internal PlayerSession Session => Player.Session;
        protected float X => SimFeetWorld(Player).x;

        // Called from the test, not [UnitySetUp]: that runs before [SetUp], and a Player started before the input
        // fixture would never hear its keyboard.
        protected IEnumerator LoadScene(string path)
        {
            AsyncOperation loading = EditorSceneManager.LoadSceneAsyncInPlayMode(path, new LoadSceneParameters(LoadSceneMode.Additive));
            while (!loading.isDone)
            {
                yield return null;
            }
            scene = SceneManager.GetSceneByPath(path);
            SceneManager.SetActiveScene(scene);
            yield return null;
            Player[] players = PlayStartScan.FindActive<Player>();
            Assert.AreEqual(1, players.Length, "The scene has one Player.");
            Player = players[0];
            Assert.IsNotNull(Session, "The Player started.");
        }

        // Before the base [TearDown] removes the input fixture, as TestLevel.Dispose does.
        [TearDown]
        public void DestroyTheLevel()
        {
            if (scene.IsValid() && scene.isLoaded)
            {
                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        [UnityTearDown]
        public IEnumerator UnloadTheScene()
        {
            foreach (Object item in Created)
            {
                Object.DestroyImmediate(item);
            }
            Created.Clear();
            if (scene.IsValid() && scene.isLoaded)
            {
                AsyncOperation unloading = SceneManager.UnloadSceneAsync(scene);
                while (unloading != null && !unloading.isDone)
                {
                    yield return null;
                }
            }
        }

        protected IEnumerator Until(Func<bool> done, string what)
        {
            for (int i = 0; i < Limit && !done(); i++)
            {
                yield return null;
            }
            Assert.IsTrue(done(), $"{what} (x = {X}, deaths = {Session.Deaths}, checkpoint = {Session.ActiveCheckpoint}, phase = {Session.Phase})");
        }
    }
}
#endif
