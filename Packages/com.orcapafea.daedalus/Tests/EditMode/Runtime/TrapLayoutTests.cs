using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The serialized layout of the level elements (Checkpoint, Exit, Trap), their boxes and their play-start scan.</summary>
    public class TrapLayoutTests
    {
        const float Tolerance = 1e-5f;
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

        [Test]
        public void CheckpointHasItsMenuNoVisibleSettingsAndDataVersionOne()
        {
            Assert.AreEqual("Daedalus/Checkpoint", typeof(Checkpoint).GetCustomAttribute<AddComponentMenu>().componentMenu);
            AssertLayout(NewObject("Checkpoint").AddComponent<Checkpoint>(), "m_Script");
        }

        [Test]
        public void ExitHasItsMenuTheNextStageSettingAndDataVersionOne()
        {
            Assert.AreEqual("Daedalus/Exit", typeof(Exit).GetCustomAttribute<AddComponentMenu>().componentMenu);
            Exit exit = NewObject("Exit").AddComponent<Exit>();
            AssertLayout(exit, "m_Script", "nextStage");
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            Assert.IsNotEmpty(typeof(Exit).GetField("nextStage", F).GetCustomAttribute<TooltipAttribute>().tooltip);
            Assert.IsNotNull(typeof(Exit).GetField("builtNextStagePath", F).GetCustomAttribute<HideInInspector>());
            Assert.AreEqual(ExitLink.LinkKind.None, exit.Link.Kind, "a new Exit ends the game");
        }

        [Test]
        public void AnExitSavedBeforeStagesLoadsWithNoNextStage()
        {
            // 0.1.0 saved only the data version; the new fields take their field initializers.
            Exit fresh = NewObject("Fresh").AddComponent<Exit>();
            string json = EditorJsonUtility.ToJson(fresh);
            json = System.Text.RegularExpressions.Regex.Replace(json, ",\\s*\"nextStage\":\"[^\"]*\"", "");
            json = System.Text.RegularExpressions.Regex.Replace(json, ",\\s*\"builtNextStagePath\":\"[^\"]*\"", "");
            StringAssert.DoesNotContain("nextStage", json, "precondition: the old data has no link");
            Exit old = NewObject("Old").AddComponent<Exit>();
            EditorJsonUtility.FromJsonOverwrite(json, old);
            Assert.AreEqual(ExitLink.LinkKind.None, old.Link.Kind);
            Assert.AreEqual(1, old.DataVersion);
        }

        static void AssertLayout(Component component, params string[] paths)
        {
            var serialized = new SerializedObject(component);
            var visible = new List<string>();
            SerializedProperty iterator = serialized.GetIterator();
            while (iterator.NextVisible(true))
            {
                visible.Add(iterator.propertyPath);
            }
            CollectionAssert.AreEquivalent(paths, visible, component.GetType().Name);
            Assert.AreEqual(1, serialized.FindProperty("dataVersion").intValue, component.GetType().Name);
            Assert.IsNotNull(component.GetType().GetField("dataVersion", BindingFlags.NonPublic | BindingFlags.Instance).GetCustomAttribute<HideInInspector>());
        }

        [Test]
        public void TheBoxIsTheSpriteElseOneTileAroundThePosition()
        {
            Checkpoint bare = NewObject("Bare").AddComponent<Checkpoint>();
            bare.transform.position = new Vector3(3, 4, 0);
            Rect box = bare.WorldBox;
            Assert.AreEqual(new Rect(2.5f, 3.5f, 1, 1), box);

            Exit drawn = NewObject("Drawn").AddComponent<Exit>();
            var renderer = drawn.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = BottomPivotSprite();
            drawn.transform.position = new Vector3(5, 1, 0);
            drawn.transform.localScale = new Vector3(2, 2, 1);
            box = drawn.WorldBox;
            Assert.AreEqual(4f, box.xMin, Tolerance);
            Assert.AreEqual(6f, box.xMax, Tolerance);
            Assert.AreEqual(1f, box.yMin, Tolerance, "a bottom pivot sits on the position");
            Assert.AreEqual(3f, box.yMax, Tolerance);
        }

        [Test]
        public void CheckpointsComeBackSortedAndDisabledOnesAreSkipped()
        {
            Checkpoint c = NewCheckpoint(9, 1);
            Checkpoint a = NewCheckpoint(2, 5);
            Checkpoint b = NewCheckpoint(2, 1);
            NewCheckpoint(4, 1).enabled = false;
            NewCheckpoint(5, 1).gameObject.SetActive(false);
            CollectionAssert.AreEqual(new[] { b, a, c }, LevelCollector.FindCheckpoints());
        }

        [Test]
        public void ACheckpointRightOfEveryPaintedTileGrowsTheMap()
        {
            var tilemap = new GameObject("Collision", typeof(Grid)).AddComponent<Tilemap>();
            created.Add(tilemap.transform.root.gameObject);
            Tile plain = ScriptableObject.CreateInstance<Tile>();
            created.Add(plain);
            for (int x = 0; x < 5; x++)
            {
                tilemap.SetTile(new Vector3Int(x, 0, 0), plain);
            }
            NewCheckpoint(20, 1);
            Rect include = LevelCollector.Grow(new Rect(1, 1, 0.75f, 1.5f), LevelCollector.Boxes(LevelCollector.FindCheckpoints()));
            CollisionMap map = CollisionMapReader.Read(tilemap, include, new List<string>());
            Assert.That(map.Grid.Width, Is.GreaterThanOrEqualTo(21), "the checkpoint at x = 20 is inside the map");
            Box[] boxes = LevelCollector.ToBoxes(LevelCollector.Boxes(LevelCollector.FindCheckpoints()), map.Origin);
            Assert.That(boxes[0].MaxX, Is.LessThanOrEqualTo(map.Grid.Width));
        }

        static readonly string[] TrapPaths = { "m_Script", "trigger", "zone", "action", "offset", "speed", "delay", "size", "tile" };

        [Test]
        public void TrapFieldsDefaultsRangesTooltipsAndMenuArePinned()
        {
            Trap trap = NewTrap(0, 0);
            var serialized = new SerializedObject(trap);
            var visible = new List<string>();
            SerializedProperty iterator = serialized.GetIterator();
            // Top-level fields only (the Rect and Vector2Int children are not settings of their own).
            for (bool enter = true; iterator.NextVisible(enter); enter = false)
            {
                visible.Add(iterator.propertyPath);
            }
            CollectionAssert.AreEqual(TrapPaths, visible, "eight settings, in this order");
            Assert.AreEqual((int)TrapTrigger.Zone, serialized.FindProperty("trigger").enumValueIndex);
            Assert.AreEqual((int)TrapAction.Vanish, serialized.FindProperty("action").enumValueIndex);
            Assert.AreEqual(LevelRules.DefaultTrapDelay, serialized.FindProperty("delay").floatValue);
            Assert.AreEqual(new Vector2Int(LevelRules.DefaultMoveOffsetX, LevelRules.DefaultMoveOffsetY), serialized.FindProperty("offset").vector2IntValue);
            Assert.AreEqual(LevelRules.DefaultMoveSpeed, serialized.FindProperty("speed").floatValue);
            Assert.AreEqual(new Vector2Int(LevelRules.DefaultTrapWidth, LevelRules.DefaultTrapHeight), serialized.FindProperty("size").vector2IntValue);
            Assert.IsNull(serialized.FindProperty("tile").objectReferenceValue);
            Assert.AreEqual(1, serialized.FindProperty("dataVersion").intValue);
            const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
            Assert.IsNotNull(typeof(Trap).GetField("dataVersion", F).GetCustomAttribute<HideInInspector>());
            var range = typeof(Trap).GetField("delay", F).GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.AreEqual(LevelRules.MinTrapDelay, range.min);
            Assert.AreEqual(LevelRules.MaxTrapDelay, range.max);
            var speedRange = typeof(Trap).GetField("speed", F).GetCustomAttribute<UnityEngine.RangeAttribute>();
            Assert.AreEqual(LevelRules.MinMoveSpeed, speedRange.min);
            Assert.AreEqual(LevelRules.MaxMoveSpeed, speedRange.max);
            foreach (string path in TrapPaths)
            {
                if (path == "m_Script")
                {
                    continue;
                }
                var tooltip = typeof(Trap).GetField(path, F).GetCustomAttribute<TooltipAttribute>();
                Assert.IsNotNull(tooltip, path);
                Assert.IsNotEmpty(tooltip.tooltip, path);
            }
            Assert.AreEqual("Daedalus/Trap", typeof(Trap).GetCustomAttribute<AddComponentMenu>().componentMenu);
            Assert.AreEqual(typeof(SpriteRenderer), typeof(Trap).GetCustomAttribute<RequireComponent>().m_Type0);
            Assert.AreEqual(0, (int)TrapTrigger.Zone);
            Assert.AreEqual(1, (int)TrapTrigger.Touch);
            Assert.AreEqual(0, (int)TrapAction.Vanish);
            Assert.AreEqual(1, (int)TrapAction.Appear);
            Assert.AreEqual(2, (int)TrapAction.Move);
            Assert.AreEqual(3, (int)TrapAction.Fall);
            Assert.AreEqual(0, (int)TrapActionKind.Vanish);
            Assert.AreEqual(1, (int)TrapActionKind.Appear);
            Assert.AreEqual(2, (int)TrapActionKind.Move);
            Assert.AreEqual(3, (int)TrapActionKind.Fall);
            Assert.AreEqual((int)TrapTriggerKind.Touch, (int)TrapTrigger.Touch);
        }

        [Test]
        public void ResetGivesTheDefaultZoneOfTheBox()
        {
            Trap trap = NewTrap(0, 0);
            Assert.AreEqual(TrapLook.DefaultZoneLocal(new Vector2Int(3, 1)), trap.Zone);
            Assert.AreEqual(Rect.MinMaxRect(-1.5f, -0.5f, 1.5f, 1.5f), trap.Zone, "the 3 × 1 box grown up by 1");
        }

        [Test]
        public void TheBoxIsCentredOnTheObjectAndSnapsToWholeCells()
        {
            Trap trap = NewTrap(1.5f, 0.5f);
            Assert.AreEqual(new Vector2Int(0, 0), TrapLook.CellMin(trap));
            Assert.IsFalse(TrapLook.IsOffGrid(trap));

            trap.transform.position = new Vector3(1.8f, 0.5f, 0);
            Assert.AreEqual(new Vector2Int(0, 0), TrapLook.CellMin(trap));
            Assert.IsTrue(TrapLook.IsOffGrid(trap));

            trap.Size = new Vector2Int(2, 2);
            trap.transform.position = new Vector3(1, 1, 0);
            Assert.AreEqual(new Vector2Int(0, 0), TrapLook.CellMin(trap));

            // Half-tile positions snap the same way whatever their parity.
            trap.Size = new Vector2Int(2, 1);
            trap.transform.position = new Vector3(0.5f, 0.5f, 0);
            Assert.AreEqual(1f, TrapLook.SnappedCentre(trap).x, "0.5 snaps up");
            trap.transform.position = new Vector3(1.5f, 0.5f, 0);
            Assert.AreEqual(2f, TrapLook.SnappedCentre(trap).x, "1.5 snaps up");
        }

        [Test]
        public void TheKindComesFromTheTile()
        {
            Assert.AreEqual(TileKind.Solid, TrapLook.KindOf(null));
            Tile plain = ScriptableObject.CreateInstance<Tile>();
            created.Add(plain);
            Assert.AreEqual(TileKind.Solid, TrapLook.KindOf(plain));
            Assert.AreEqual(TileKind.Hazard, TrapLook.KindOf(Daedalus(TileCollisionKind.Hazard)));
            Assert.AreEqual(TileKind.Solid, TrapLook.KindOf(Daedalus(TileCollisionKind.OneWay)));
        }

        [Test]
        public void ToSpecConvertsToSimCellsAndNormalisesTheZone()
        {
            Trap trap = NewTrap(4.5f, 2.5f);
            trap.Action = TrapAction.Appear;
            trap.Trigger = TrapTrigger.Touch;
            trap.Delay = 0.5f;
            trap.Tile = Daedalus(TileCollisionKind.Hazard);
            var origin = new Vector2(-2, 1);
            TrapSpec spec = TrapLook.ToSpec(trap, origin);
            Assert.AreEqual(5, spec.CellX, "world cell 3 minus the origin");
            Assert.AreEqual(1, spec.CellY, "world cell 2 minus the origin");
            Assert.AreEqual(3, spec.Width);
            Assert.AreEqual(1, spec.Height);
            Assert.AreEqual(TileKind.Hazard, spec.Kind);
            Assert.AreEqual(TrapActionKind.Appear, spec.Action);
            Assert.AreEqual(TrapTriggerKind.Touch, spec.Trigger);
            Assert.AreEqual(30, spec.DelayTicks);

            trap.Zone = new Rect(1, 2, -3, -4);
            TrapSpec flipped = TrapLook.ToSpec(trap, origin);
            trap.Zone = Rect.MinMaxRect(-2, -2, 1, 2);
            TrapSpec normal = TrapLook.ToSpec(trap, origin);
            Assert.AreEqual(normal.Zone.MinX, flipped.Zone.MinX);
            Assert.AreEqual(normal.Zone.MinY, flipped.Zone.MinY);
            Assert.AreEqual(normal.Zone.MaxX, flipped.Zone.MaxX);
            Assert.AreEqual(normal.Zone.MaxY, flipped.Zone.MaxY);
        }

        [Test]
        public void AnOffGridTrapsZoneMovesWithItsSnappedBox()
        {
            Trap trap = NewTrap(4.5f, 2.8f);
            Rect local = trap.Zone;
            TrapSpec spec = TrapLook.ToSpec(trap, Vector2.zero);
            TrapLook.ApplyPlayLook(trap, trap.GetComponent<SpriteRenderer>(), true);
            Rect drawn = TrapLook.WorldZone(trap);
            Assert.AreEqual(2.5f, trap.transform.position.y, "precondition: play start snapped the trap");
            Assert.AreEqual(drawn.yMin, spec.Zone.MinY, 1e-5f, "the sim zone is the zone drawn around the snapped trap");
            Assert.AreEqual(drawn.xMin, spec.Zone.MinX, 1e-5f);
            Assert.AreEqual(2.5f + local.yMin, spec.Zone.MinY, 1e-5f);
        }

        [Test]
        public void AnOutOfRangeDelayIsClampedNotThrown()
        {
            Trap trap = NewTrap(4.5f, 0.5f);
            trap.Delay = 1e9f;
            Assert.AreEqual(LevelRules.SecondsToTicks(LevelRules.MaxTrapDelay), TrapLook.ToSpec(trap, Vector2.zero).DelayTicks);
            trap.Delay = -5;
            Assert.AreEqual(0, TrapLook.ToSpec(trap, Vector2.zero).DelayTicks);
            trap.Delay = float.NaN;
            Assert.AreEqual(0, TrapLook.ToSpec(trap, Vector2.zero).DelayTicks);
        }

        [Test]
        public void AnOutOfRangeSizeIsClampedForTheSimAndTheLookAlike()
        {
            Trap trap = NewTrap(0, 0.5f);
            trap.Tile = PlainTile();
            var renderer = trap.GetComponent<SpriteRenderer>();

            trap.Size = new Vector2Int(40, 1);
            TrapSpec spec = TrapLook.ToSpec(trap, Vector2.zero);
            TrapLook.ApplyPlayLook(trap, renderer, true);
            Assert.AreEqual(30, spec.Width);
            Assert.AreEqual(30f, renderer.size.x);
            Assert.AreEqual(spec.CellX + spec.Width / 2f, trap.transform.position.x, "the look is centred on the sim box");

            trap.Size = new Vector2Int(0, 1);
            spec = TrapLook.ToSpec(trap, Vector2.zero);
            TrapLook.ApplyPlayLook(trap, renderer, true);
            Assert.AreEqual(1, spec.Width);
            Assert.AreEqual(1f, renderer.size.x);
            Assert.AreEqual(spec.CellX + 0.5f, trap.transform.position.x);
        }

        [Test]
        public void ATrapOnACheckpointOrBesideATurnedOffExitIsLeftOutOfTheScan()
        {
            Trap kept = NewTrap(1, 0);
            Trap onExit = NewTrap(5, 0);
            onExit.gameObject.AddComponent<Exit>();
            Trap onCheckpoint = NewTrap(9, 0);
            onCheckpoint.gameObject.AddComponent<Checkpoint>();
            Trap offExit = NewTrap(13, 0);
            offExit.gameObject.AddComponent<Exit>().enabled = false;
            CollectionAssert.AreEqual(new[] { kept, onExit }, LevelCollector.FindTraps(out Trap[] ignored), "a Trap on an Exit moves it");
            CollectionAssert.AreEquivalent(new[] { onCheckpoint, offExit }, ignored);
            Assert.IsTrue(TrapLook.IsOnCheckpoint(onCheckpoint));
            Assert.IsTrue(TrapLook.IsOnDisabledExit(offExit));
        }

        [Test]
        public void IsOnExitFollowsTheExitsEnabledToggleEvenOnAnInactiveObject()
        {
            // An inactive object stands in for a prefab asset, where every component reads inactive (round 2, F16).
            Trap trap = NewTrap(5, 0);
            Exit exit = trap.gameObject.AddComponent<Exit>();
            trap.gameObject.SetActive(false);
            Assert.IsTrue(TrapLook.IsOnExit(trap), "an enabled Exit on an inactive object");
            Assert.IsFalse(TrapLook.IsIgnored(trap));
            exit.enabled = false;
            Assert.IsFalse(TrapLook.IsOnExit(trap), "unticked");
            Assert.IsTrue(TrapLook.IsIgnored(trap));
        }

        [Test]
        public void OldTrapDataLoadsWithTheMoveDefaults()
        {
            // A Phase 3 Trap saved only six settings; the new ones take their field initializers.
            Trap old = NewTrap(0, 0.5f);
            old.Action = TrapAction.Appear;
            old.Size = new Vector2Int(2, 1);
            old.Zone = Rect.MinMaxRect(-3, -1, 2, 4);
            string json = EditorJsonUtility.ToJson(old);
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"offset\":\\{[^}]*\\},?", "");
            json = System.Text.RegularExpressions.Regex.Replace(json, "\"speed\":[^,}]*,?", "");
            StringAssert.DoesNotContain("offset", json, "precondition: the old data has no offset");
            StringAssert.DoesNotContain("speed", json, "precondition: nor a speed");
            Trap loaded = NewTrap(0, 0.5f);
            EditorJsonUtility.FromJsonOverwrite(json, loaded);
            Assert.AreEqual(TrapAction.Appear, loaded.Action);
            Assert.AreEqual(new Vector2Int(2, 1), loaded.Size);
            Assert.AreEqual(old.Zone, loaded.Zone);
            Assert.AreEqual(new Vector2Int(0, -3), loaded.Offset);
            Assert.AreEqual(8f, loaded.Speed);
            Assert.AreEqual(1, loaded.DataVersion);
        }

        [Test]
        public void ToSpecCarriesTheMoveClampedAndFall()
        {
            Trap trap = NewTrap(4.5f, 2.5f);
            trap.Action = TrapAction.Move;
            trap.Offset = new Vector2Int(40, 2);
            trap.Speed = 50f;
            TrapSpec spec = TrapLook.ToSpec(trap, Vector2.zero);
            Assert.AreEqual(TrapActionKind.Move, spec.Action);
            Assert.AreEqual(30, spec.OffsetX);
            Assert.AreEqual(2, spec.OffsetY);
            Assert.AreEqual(30f, spec.Speed);
            Assert.AreEqual(-1, spec.ExitIndex);
            trap.Action = TrapAction.Fall;
            Assert.AreEqual(TrapActionKind.Fall, TrapLook.ToSpec(trap, Vector2.zero).Action);
            trap.Action = (TrapAction)7;
            Assert.AreEqual(TrapActionKind.Vanish, TrapLook.ToSpec(trap, Vector2.zero).Action, "an undefined value reads as Vanish");
        }

        [Test]
        public void ToSpecOnAnExitMovesTheExitsBox()
        {
            Exit exit = NewObject("Exit").AddComponent<Exit>();
            var renderer = exit.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = BottomPivotSprite();
            exit.transform.position = new Vector3(5.2f, 1, 0);
            Trap trap = ObjectFactory.AddComponent<Trap>(exit.gameObject);
            trap.Action = TrapAction.Move;
            trap.Offset = new Vector2Int(6, 0);
            var origin = new Vector2(-2, 1);
            TrapSpec spec = TrapLook.ToSpec(trap, origin, 3);
            Assert.AreEqual(3, spec.ExitIndex);
            Assert.AreEqual(TileKind.None, spec.Kind);
            Assert.AreEqual(4.7f + 2f, spec.StartBox.MinX, Tolerance, "the exit's box, in sim coordinates");
            Assert.AreEqual(0f, spec.StartBox.MinY, Tolerance);
            Assert.AreEqual(5.7f + 2f, spec.StartBox.MaxX, Tolerance);
            Assert.AreEqual(6, spec.OffsetX);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => TrapLook.ToSpec(trap, origin), "a Trap on an Exit needs its index");
            Assert.IsFalse(TrapLook.IsOffGrid(trap), "an exit's box is never snapped");
        }

        [Test]
        public void TheDefaultZoneOfATrapOnAnExitIsTheExitBoxGrownOnEverySide()
        {
            Exit exit = NewObject("Exit").AddComponent<Exit>();
            var renderer = exit.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = BottomPivotSprite();
            exit.transform.position = new Vector3(5.2f, 1, 0);
            Trap trap = ObjectFactory.AddComponent<Trap>(exit.gameObject);
            Assert.AreEqual(-1.5f, trap.Zone.xMin, Tolerance, "Reset: the 1-wide exit box grown by 1, relative to the position");
            Assert.AreEqual(1.5f, trap.Zone.xMax, Tolerance);
            Assert.AreEqual(-1f, trap.Zone.yMin, Tolerance, "a bottom pivot: the box starts at the position");
            Assert.AreEqual(2f, trap.Zone.yMax, Tolerance);
            Assert.AreEqual(TrapLook.DefaultZoneLocal(trap), trap.Zone);
        }

        [Test]
        public void AMoveEndRightOfEveryPaintedTileGrowsTheMap()
        {
            var tilemap = new GameObject("Collision", typeof(Grid)).AddComponent<Tilemap>();
            created.Add(tilemap.transform.root.gameObject);
            Tile plain = ScriptableObject.CreateInstance<Tile>();
            created.Add(plain);
            for (int x = 0; x < 5; x++)
            {
                tilemap.SetTile(new Vector3Int(x, 0, 0), plain);
            }
            Trap trap = NewTrap(2.5f, 3.5f);
            trap.Size = new Vector2Int(1, 1);
            trap.Action = TrapAction.Move;
            trap.Offset = new Vector2Int(20, 0);
            Rect end = TrapLook.EndBox(trap);
            Assert.AreEqual(new Rect(22, 3, 1, 1), end);
            Trap[] traps = LevelCollector.FindTraps();
            Rect include = LevelCollector.Grow(new Rect(1, 1, 0.75f, 1.5f), LevelCollector.EndBoxes(traps));
            CollisionMap map = CollisionMapReader.Read(tilemap, include, new List<string>());
            Assert.That(map.Grid.Width, Is.GreaterThanOrEqualTo(23), "the Move's end at x = 22 is inside the map");
            trap.Action = TrapAction.Fall;
            Assert.AreEqual(TrapLook.WorldBox(trap), TrapLook.EndBox(trap), "only a Move has an end");
        }

        Trap NewTrap(float x, float y)
        {
            GameObject go = NewObject("Trap");
            go.transform.position = new Vector3(x, y, 0);
            // The Inspector's Add Component path (it calls Reset; framework.md).
            return ObjectFactory.AddComponent<Trap>(go);
        }

        DaedalusTile Daedalus(TileCollisionKind kind)
        {
            var tile = ScriptableObject.CreateInstance<DaedalusTile>();
            tile.Kind = kind;
            created.Add(tile);
            return tile;
        }

        Tile PlainTile()
        {
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            tile.sprite = BottomPivotSprite();
            created.Add(tile);
            return tile;
        }

        Checkpoint NewCheckpoint(float x, float y)
        {
            Checkpoint checkpoint = NewObject("Checkpoint").AddComponent<Checkpoint>();
            checkpoint.transform.position = new Vector3(x, y, 0);
            return checkpoint;
        }

        Sprite BottomPivotSprite()
        {
            var texture = new Texture2D(16, 16);
            created.Add(texture);
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, 16, 16), new Vector2(0.5f, 0f), 16);
            created.Add(sprite);
            return sprite;
        }

        GameObject NewObject(string name)
        {
            var go = new GameObject(name);
            created.Add(go);
            return go;
        }
    }
}
