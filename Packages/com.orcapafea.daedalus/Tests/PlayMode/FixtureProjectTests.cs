#if UNITY_EDITOR
using System.Collections;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// A project saved on 0.1.0, shaped like a student's, still opens and plays on today's package (Architecture rule 4).
    /// It runs only inside a copy of the fixture (Tools/install_check.py does, and fails if this was ignored).
    /// </summary>
    public class FixtureProjectTests : SavedScenePlayTest
    {
        const string ScenePath = "Assets/My Level.unity";
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
        }
    }
}
#endif
