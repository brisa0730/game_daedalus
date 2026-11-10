#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Plays real stage scenes with the real keyboard. The first scene loads additively; each Exit's load is Single.</summary>
    public abstract class StagePlayTest : SavedScenePlayTest
    {
        // Lives in the bootstrap scene, unprotected: an Exit's Single load would destroy it and the run would never end.
        const string TestRunnerName = "Code-based tests runner";

        internal static StageRun Run => StageRun.Find();

        [SetUp]
        public void KeepTheTestRunnerThroughSingleLoads()
        {
            GameObject runner = GameObject.Find(TestRunnerName);
            if (runner != null)
            {
                Object.DontDestroyOnLoad(runner);
            }
        }

        /// <summary>True, and <see cref="SavedScenePlayTest.Player"/> updated, once a different Player than <paramref name="old"/> has started.</summary>
        protected bool Arrived(Player old)
        {
            Player[] players = PlayStartScan.FindActive<Player>();
            if (players.Length == 1 && players[0] != old && players[0].Session != null)
            {
                Player = players[0];
                return true;
            }
            return false;
        }

        // Before the base [TearDown] removes the input fixture: a Player's actions must go first.
        [TearDown]
        public void DestroyTheStages()
        {
            foreach (Player player in PlayStartScan.FindActive<Player>())
            {
                Object.DestroyImmediate(player.gameObject);
            }
            StageRun run = Run;
            if (run != null)
            {
                Object.DestroyImmediate(run.gameObject);
            }
        }

        [UnityTearDown]
        public IEnumerator LeaveAnEmptyScene()
        {
            Scene empty = SceneManager.CreateScene($"Empty {Time.frameCount} {SceneManager.sceneCount}");
            SceneManager.SetActiveScene(empty);
            for (int i = SceneManager.sceneCount - 1; i >= 0; i--)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene != empty && scene.isLoaded)
                {
                    AsyncOperation unloading = SceneManager.UnloadSceneAsync(scene);
                    while (unloading != null && !unloading.isDone)
                    {
                        yield return null;
                    }
                }
            }
        }
    }
}
#endif
