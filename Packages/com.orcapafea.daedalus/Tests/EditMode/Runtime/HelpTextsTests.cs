using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The trap's story in plain words, for every setting combination, and the early-zone line.</summary>
    public class HelpTextsTests
    {
        const string Spike = " It's a spike trap: touching it while it's out kills.";
        readonly List<Object> created = new List<Object>();

        [SetUp]
        public void NewScene()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        }

        [TearDown]
        public void DestroyCreated()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.DestroyImmediate(item);
                }
            }
            created.Clear();
        }

        [TestCase(TrapTrigger.Zone, TrapAction.Vanish, false, 0f, "Solid ground until the player enters the zone, then it vanishes.")]
        [TestCase(TrapTrigger.Zone, TrapAction.Vanish, false, 0.5f, "Solid ground until the player enters the zone, then it vanishes 0.5 s later.")]
        [TestCase(TrapTrigger.Zone, TrapAction.Vanish, true, 0f, "Spikes until the player enters the zone, then it vanishes." + Spike)]
        [TestCase(TrapTrigger.Zone, TrapAction.Vanish, true, 0.5f, "Spikes until the player enters the zone, then it vanishes 0.5 s later." + Spike)]
        [TestCase(TrapTrigger.Zone, TrapAction.Appear, false, 0f, "Hidden until the player enters the zone, then it appears.")]
        [TestCase(TrapTrigger.Zone, TrapAction.Appear, false, 0.5f, "Hidden until the player enters the zone, then it appears 0.5 s later.")]
        [TestCase(TrapTrigger.Zone, TrapAction.Appear, true, 0f, "Hidden until the player enters the zone, then it appears." + Spike)]
        [TestCase(TrapTrigger.Zone, TrapAction.Appear, true, 0.5f, "Hidden until the player enters the zone, then it appears 0.5 s later." + Spike)]
        [TestCase(TrapTrigger.Touch, TrapAction.Vanish, false, 0f, "Solid ground until the player touches it, then it vanishes.")]
        [TestCase(TrapTrigger.Touch, TrapAction.Vanish, false, 0.5f, "Solid ground until the player touches it, then it vanishes 0.5 s later.")]
        [TestCase(TrapTrigger.Touch, TrapAction.Vanish, true, 0f, "Spikes until the player touches it, then it vanishes." + Spike)]
        [TestCase(TrapTrigger.Touch, TrapAction.Vanish, true, 0.5f, "Spikes until the player touches it, then it vanishes 0.5 s later." + Spike)]
        [TestCase(TrapTrigger.Touch, TrapAction.Appear, false, 0f, "Hidden until the player touches it, then it appears.")]
        [TestCase(TrapTrigger.Touch, TrapAction.Appear, false, 0.5f, "Hidden until the player touches it, then it appears 0.5 s later.")]
        [TestCase(TrapTrigger.Touch, TrapAction.Appear, true, 0f, "Hidden until the player touches it, then it appears." + Spike)]
        [TestCase(TrapTrigger.Touch, TrapAction.Appear, true, 0.5f, "Hidden until the player touches it, then it appears 0.5 s later." + Spike)]
        public void TheStoryTellsWhatTheTrapDoes(TrapTrigger trigger, TrapAction action, bool hazard, float delay, string story)
        {
            Trap trap = NewTrap();
            trap.Trigger = trigger;
            trap.Action = action;
            trap.Delay = delay;
            if (hazard)
            {
                var spikes = ScriptableObject.CreateInstance<DaedalusTile>();
                spikes.Kind = TileCollisionKind.Hazard;
                created.Add(spikes);
                trap.Tile = spikes;
            }
            Assert.AreEqual(story, HelpTexts.TrapStory(trap));
        }

        [Test]
        public void AZoneReachingMoreThanATilePastTheTrapSaysItFiresEarly()
        {
            Trap trap = NewTrap();
            Rect fitted = trap.Zone;
            const string Story = "Solid ground until the player enters the zone, then it vanishes.";
            Assert.AreEqual(Story, HelpTexts.TrapStory(trap), "the default zone");

            trap.Zone = Rect.MinMaxRect(fitted.xMin - HelpTexts.EarlyZoneThreshold, fitted.yMin, fitted.xMax, fitted.yMax);
            Assert.AreEqual(Story, HelpTexts.TrapStory(trap), "exactly at the threshold");

            trap.Zone = Rect.MinMaxRect(fitted.xMin - 3, fitted.yMin, fitted.xMax, fitted.yMax);
            Assert.AreEqual(Story + " The zone reaches 3 tiles left of the trap, so it fires before the player gets there.", HelpTexts.TrapStory(trap));

            trap.Zone = Rect.MinMaxRect(fitted.xMin, fitted.yMin, fitted.xMax, fitted.yMax + 2);
            Assert.AreEqual(Story + " The zone reaches 2 tiles above the trap, so it fires before the player gets there.", HelpTexts.TrapStory(trap), "beyond the default upward growth");

            trap.Trigger = TrapTrigger.Touch;
            StringAssert.DoesNotContain("zone reaches", HelpTexts.TrapStory(trap), "a Touch trap has no zone");
        }

        [Test]
        public void NumbersReadTheSameOnAMachineWithDecimalCommas()
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Assert.AreEqual("0,5", 0.5f.ToString("0.##"), "precondition: the culture writes decimal commas");
                Trap trap = NewTrap();
                trap.Delay = 0.5f;
                trap.Zone = Rect.MinMaxRect(trap.Zone.xMin - 2.5f, trap.Zone.yMin, trap.Zone.xMax, trap.Zone.yMax);
                string story = HelpTexts.TrapStory(trap);
                StringAssert.Contains("0.5 s later", story);
                StringAssert.Contains("reaches 2.5 tiles", story);
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }

        Trap NewTrap()
        {
            var go = new GameObject("Trap");
            created.Add(go);
            go.transform.position = new Vector3(1.5f, 0.5f, 0);
            return UnityEditor.ObjectFactory.AddComponent<Trap>(go);
        }
    }
}
