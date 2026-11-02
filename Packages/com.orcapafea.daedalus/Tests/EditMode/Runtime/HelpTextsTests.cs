using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
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
            Assert.AreEqual(story, HelpTexts.TrapStory(trap, null, MovementSettings.Default));
        }

        [Test]
        public void AZoneReachingMoreThanATilePastTheTrapSaysItFiresEarly()
        {
            Trap trap = NewTrap();
            Rect fitted = trap.Zone;
            const string Story = "Solid ground until the player enters the zone, then it vanishes.";
            Assert.AreEqual(Story, HelpTexts.TrapStory(trap, null, MovementSettings.Default), "the default zone");

            trap.Zone = Rect.MinMaxRect(fitted.xMin - HelpTexts.EarlyZoneThreshold, fitted.yMin, fitted.xMax, fitted.yMax);
            Assert.AreEqual(Story, HelpTexts.TrapStory(trap, null, MovementSettings.Default), "exactly at the threshold");

            trap.Zone = Rect.MinMaxRect(fitted.xMin - 3, fitted.yMin, fitted.xMax, fitted.yMax);
            Assert.AreEqual(Story + " The zone reaches 3 tiles left of the trap, so it fires before the player gets there.", HelpTexts.TrapStory(trap, null, MovementSettings.Default));

            trap.Zone = Rect.MinMaxRect(fitted.xMin, fitted.yMin, fitted.xMax, fitted.yMax + 2);
            Assert.AreEqual(Story + " The zone reaches 2 tiles above the trap, so it fires before the player gets there.", HelpTexts.TrapStory(trap, null, MovementSettings.Default), "beyond the default upward growth");

            trap.Trigger = TrapTrigger.Touch;
            StringAssert.DoesNotContain("zone reaches", HelpTexts.TrapStory(trap, null, MovementSettings.Default), "a Touch trap has no zone");
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
                string story = HelpTexts.TrapStory(trap, null, MovementSettings.Default);
                StringAssert.Contains("0.5 s later", story);
                StringAssert.Contains("reaches 2.5 tiles", story);
            }
            finally
            {
                CultureInfo.CurrentCulture = before;
            }
        }

        [TestCase(5, 0, 8f, 0f, "Slides 5 tiles right in 0.6 s after the player enters the zone.")]
        [TestCase(-5, 0, 8f, 0f, "Slides 5 tiles left in 0.6 s after the player enters the zone.")]
        [TestCase(0, 3, 8f, 0f, "Rises 3 tiles in 0.4 s after the player enters the zone.")]
        [TestCase(0, -1, 8f, 0f, "Sinks 1 tile in 0.1 s after the player enters the zone.")]
        [TestCase(3, 4, 30f, 0f, "Moves 3 right and 4 up in 0.2 s after the player enters the zone.")]
        [TestCase(5, 0, 8f, 0.5f, "Slides 5 tiles right in 0.6 s, 0.5 s after the player enters the zone.")]
        [TestCase(0, 0, 8f, 0f, HelpTexts.MoveStill)]
        public void AMoveStoryNamesDistanceAndTravelTime(int x, int y, float speed, float delay, string story)
        {
            Trap trap = NewTrap();
            trap.Action = TrapAction.Move;
            trap.Offset = new Vector2Int(x, y);
            trap.Speed = speed;
            trap.Delay = delay;
            Assert.AreEqual(story, HelpTexts.TrapStory(trap, null, MovementSettings.Default));
        }

        // A Collision Tilemap with a floor on cells x 0..2, y 0 (top at y = 1).
        Tilemap Floor()
        {
            var grid = new GameObject("Grid", typeof(Grid));
            created.Add(grid);
            var tilemap = new GameObject("Collision").AddComponent<Tilemap>();
            tilemap.transform.SetParent(grid.transform);
            Tile plain = ScriptableObject.CreateInstance<Tile>();
            created.Add(plain);
            for (int x = 0; x < 3; x++)
            {
                tilemap.SetTile(new Vector3Int(x, 0, 0), plain);
            }
            return tilemap;
        }

        static string Seconds(int ticks)
        {
            return (ticks / 60f).ToString("0.0", CultureInfo.InvariantCulture);
        }

        [Test]
        public void AFallStoryNamesTheDropAndTheTimeFromTheSimsArithmetic()
        {
            Tilemap floor = Floor();
            Trap trap = NewTrap();
            trap.Action = TrapAction.Fall;
            trap.transform.position = new Vector3(1.5f, 5.5f, 0);
            // The same drop on a hand-built grid: box (0, 5)–(3, 6) over a floor row 0.
            var grid = new TileGrid(3, 7);
            for (int x = 0; x < 3; x++)
            {
                grid.Set(x, 0, TileKind.Solid);
            }
            var derived = new DerivedMovement(MovementSettings.Default);
            int ticks = TrapMotion.FallTicks(grid, new Box(0, 5, 3, 6), derived.RiseGravity, derived.MaxFallSpeed);
            Assert.AreEqual($"Falls 4 tiles in {Seconds(ticks)} s after the player enters the zone.", HelpTexts.TrapStory(trap, floor, MovementSettings.Default));

            MovementSettings heavy = MovementSettings.Default;
            heavy.Gravity = 2f;
            var heavyDerived = new DerivedMovement(heavy);
            int heavyTicks = TrapMotion.FallTicks(grid, new Box(0, 5, 3, 6), heavyDerived.RiseGravity, heavyDerived.MaxFallSpeed);
            Assert.That(heavyTicks, Is.LessThan(ticks), "precondition: Gravity 2 falls faster");
            Assert.AreEqual($"Falls 4 tiles in {Seconds(heavyTicks)} s after the player enters the zone.", HelpTexts.TrapStory(trap, floor, heavy));

            Assert.IsTrue(TrapLook.FallLandingBox(trap, floor, null, out Rect landing));
            Assert.AreEqual(new Rect(0, 1, 3, 1), landing, "the editor's landing outline");

            trap.transform.position = new Vector3(1.5f, 1.5f, 0);
            Assert.AreEqual("It's already resting on painted ground, so it won't fall.", HelpTexts.TrapStory(trap, floor, MovementSettings.Default));

            trap.transform.position = new Vector3(8.5f, 5.5f, 0);
            Assert.AreEqual("Falls out of the map after the player enters the zone.", HelpTexts.TrapStory(trap, floor, MovementSettings.Default));
            Assert.AreEqual("Falls out of the map after the player enters the zone.", HelpTexts.TrapStory(trap, null, MovementSettings.Default), "no Collision Tilemap");
        }

        Trap NewExitTrap(TrapAction action, float x, float y)
        {
            var go = new GameObject("Exit");
            created.Add(go);
            go.transform.position = new Vector3(x, y, 0);
            go.AddComponent<Exit>();
            Trap trap = UnityEditor.ObjectFactory.AddComponent<Trap>(go);
            trap.Action = action;
            return trap;
        }

        [Test]
        public void AnExitTrapTellsTheExitsStory()
        {
            Tilemap floor = Floor();
            Trap runaway = NewExitTrap(TrapAction.Move, 1.5f, 1.5f);
            runaway.Offset = new Vector2Int(5, 0);
            Assert.AreEqual("The exit runs 5 tiles right in 0.6 s after the player enters the zone.", HelpTexts.TrapStory(runaway, floor, MovementSettings.Default));
            Assert.AreEqual("The exit vanishes when the player enters the zone.",
                HelpTexts.TrapStory(NewExitTrap(TrapAction.Vanish, 1.5f, 1.5f), floor, MovementSettings.Default));
            Assert.AreEqual("The exit is hidden until the player enters the zone, then appears.",
                HelpTexts.TrapStory(NewExitTrap(TrapAction.Appear, 1.5f, 1.5f), floor, MovementSettings.Default));
            StringAssert.StartsWith("The exit falls 4 tiles in ", HelpTexts.TrapStory(NewExitTrap(TrapAction.Fall, 1.5f, 5.5f), floor, MovementSettings.Default));
            Assert.AreEqual("The exit falls out of the map after the player enters the zone.",
                HelpTexts.TrapStory(NewExitTrap(TrapAction.Fall, 8.5f, 5.5f), floor, MovementSettings.Default));
        }

        [Test]
        public void TravelTimesReadTheSameOnAMachineWithDecimalCommas()
        {
            CultureInfo before = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = new CultureInfo("de-DE");
                Trap trap = NewTrap();
                trap.Action = TrapAction.Move;
                trap.Offset = new Vector2Int(5, 0);
                trap.Speed = 8f;
                StringAssert.Contains("in 0.6 s", HelpTexts.TrapStory(trap, null, MovementSettings.Default));
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
