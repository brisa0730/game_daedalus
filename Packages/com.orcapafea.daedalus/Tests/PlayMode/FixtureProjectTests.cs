#if UNITY_EDITOR
using System.Collections;
using System.IO;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// A project saved on a released version, shaped like a student's, still opens and plays on today's package
    /// (Architecture rule 4). From 0.2.0 the fixture has a second level its Exit links to. It runs only inside a copy of a
    /// fixture (Tools/install_check.py does, and fails if this was ignored).
    /// The counts below hold for every fixture because a later fixture relinks the Starter's Exit rather than adding one.
    /// </summary>
    public class FixtureProjectTests : SavedScenePlayTest
    {
        const string ScenePath = "Assets/My Level.unity";
        const string SecondLevelPath = "Assets/My Level 2.unity";
        const int PlacedTraps = 6;

        static GameObject Named(string name)
        {
            GameObject found = GameObject.Find(name);
            Assert.IsNotNull(found, $"{name} is still in the level");
            return found;
        }

        static void AssertTrap(string name, TrapAction action, Vector2Int size)
        {
            var trap = new SerializedObject(Named(name).GetComponent<Trap>());
            Assert.AreEqual((int)action, trap.FindProperty("action").intValue, $"{name}: action");
            Assert.AreEqual(size, trap.FindProperty("size").vector2IntValue, $"{name}: size");
            Assert.IsNotNull(trap.FindProperty("tile").objectReferenceValue, $"{name}: tile");
        }

        [UnityTest]
        public IEnumerator TheSavedLevelOpensKeepsItsWorkAndPlays()
        {
            if (string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath)))
            {
                Assert.Ignore("Not a fixture project.");
            }
            yield return LoadScene(ScenePath);

            Assert.AreEqual(PlacedTraps, PlayStartScan.FindActive<Trap>().Length, "the Starter's four, the placed spike and the student's");
            Assert.AreEqual(PlacedTraps, Session.TrapCount, "every trap reached the running level");
            AssertTrap("Drop Floor", TrapAction.Vanish, new Vector2Int(3, 1));
            AssertTrap("Pop Spike", TrapAction.Appear, new Vector2Int(2, 1));
            AssertTrap("Falling Ceiling", TrapAction.Fall, new Vector2Int(3, 1));
            Rect ceilingZone = new SerializedObject(Named("Falling Ceiling").GetComponent<Trap>()).FindProperty("zone").rectValue;
            Assert.AreEqual(-9.5f, ceilingZone.yMin, "the ceiling's zone still reaches the floor");
            var runaway = new SerializedObject(Named("Runaway Exit").GetComponent<Trap>());
            Assert.AreEqual((int)TrapAction.Move, runaway.FindProperty("action").intValue, "the Runaway Exit moves");
            Assert.AreEqual(new Vector2Int(6, 0), runaway.FindProperty("offset").vector2IntValue, "how far it runs");
            Assert.AreEqual(3, PlayStartScan.FindActive<Checkpoint>().Length, "the three checkpoints");
            Assert.AreEqual(1, PlayStartScan.FindActive<Exit>().Length, "one exit");
            var player = new SerializedObject(Player);
            Assert.AreEqual(7.5f, player.FindProperty("movement.runSpeed").floatValue, "a tuned setting");
            Assert.AreEqual(4f, player.FindProperty("movement.jumpHeight").floatValue, "a tuned setting");
            var yourTurn = GameObject.Find("Your Turn (add a Trap here)");
            Assert.IsNotNull(yourTurn.GetComponent<Trap>(), "the student's trap");
            Assert.IsNotNull(new SerializedObject(yourTurn.GetComponent<Trap>()).FindProperty("tile").objectReferenceValue, "the student's tile");

            float start = X;
            Keys(Key.RightArrow);
            yield return Until(() => X > start + 3f, "running");
            Keys();

            // Keyed on the second level's file, not on the link, so a link that went missing can't pass.
            ExitLink link = Named("Runaway Exit").GetComponent<Exit>().Link;
            if (!File.Exists(SecondLevelPath))
            {
                Assert.AreEqual(ExitLink.LinkKind.None, link.Kind, "a one-level fixture's Exit wins");
                yield break;
            }
            Assert.AreEqual(ExitLink.LinkKind.Scene, link.Kind, "My Level's Exit leads to the second level");
            Assert.AreEqual(SecondLevelPath, link.Path, "My Level's Exit leads to the second level");

            Scene first = SceneManager.GetSceneByPath(ScenePath);
            foreach (GameObject root in first.GetRootGameObjects())
            {
                Object.DestroyImmediate(root);
            }
            AsyncOperation unloading = SceneManager.UnloadSceneAsync(first);
            while (unloading != null && !unloading.isDone)
            {
                yield return null;
            }
            yield return LoadScene(link.Path);
            Exit[] exits = PlayStartScan.FindActive<Exit>();
            Assert.AreEqual(1, exits.Length, "the second level has one Exit");
            Assert.AreEqual(ExitLink.LinkKind.None, exits[0].Link.Kind, "the second level's Exit ends the game");
            Assert.AreEqual(PlacedTraps, Session.TrapCount, "the second level's traps reached the running level");
        }
    }
}
#endif
