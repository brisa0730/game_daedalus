#if UNITY_EDITOR
using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Plays the imported Starter's two-stage example with the real Player, keyboard and link. It runs only where the
    /// sample has been imported (Tools/install_check.py does, and fails if this was ignored).
    /// </summary>
    public class StarterStagesPlaythroughTests : StagePlayTest
    {
        [UnityTest]
        public IEnumerator TheStageExampleCanBePlayedThroughBothStages()
        {
            string path = AssetDatabase.FindAssets("\"Stage 1\" t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.StartsWith("Assets/Samples/", StringComparison.Ordinal) && p.EndsWith("/Stage Example/Stage 1.unity", StringComparison.Ordinal));
            if (path == null)
            {
                Assert.Ignore("The Starter sample isn't imported in this project.");
            }
            yield return LoadScene(path);
            Player first = Player;
            Keys(Key.RightArrow);
            yield return Until(() => Arrived(first), "reaching the second stage");
            Assert.AreEqual("Stage 2", Player.gameObject.scene.name);
            yield return Until(() => Session.Phase == LevelPhase.Won, "reaching the last Exit with the direction still held");
            Keys();
            yield return Frames(2);
            Assert.IsTrue(Player.Hud.Win.activeSelf, "the last Exit wins");
        }
    }
}
#endif
