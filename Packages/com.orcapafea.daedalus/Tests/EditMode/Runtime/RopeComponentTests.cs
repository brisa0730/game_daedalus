using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;
using RangeAttribute = UnityEngine.RangeAttribute;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The Rope component and the play-start rope scan (Phase 1's sim rope rules live in <c>RopeTests</c>).</summary>
    public class RopeComponentTests
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
        public void LengthStartsAtTheSimDefaultWithTheSimRange()
        {
            FieldInfo field = typeof(Rope).GetField("length", BindingFlags.NonPublic | BindingFlags.Instance);
            var range = field.GetCustomAttribute<RangeAttribute>();
            Assert.AreEqual(RopeSegment.MinLength, range.min);
            Assert.AreEqual(RopeSegment.MaxLength, range.max);
            StringAssert.EndsWith("Changes take effect the next time you press Play.", field.GetCustomAttribute<TooltipAttribute>().tooltip);

            var serialized = new SerializedObject(NewRope(0, 0, RopeSegment.DefaultLength));
            Assert.AreEqual(RopeSegment.DefaultLength, serialized.FindProperty("length").floatValue);
            SerializedProperty dataVersion = serialized.FindProperty("dataVersion");
            Assert.IsNotNull(dataVersion, "field 'dataVersion'");
            Assert.AreEqual(1, dataVersion.intValue);
            Assert.IsNotNull(typeof(Rope).GetField("dataVersion", BindingFlags.NonPublic | BindingFlags.Instance).GetCustomAttribute<HideInInspector>());
        }

        [Test]
        public void ANewRopeHasTheDefaultLength()
        {
            var rope = NewObject("Rope").AddComponent<Rope>();
            Assert.AreEqual(RopeSegment.DefaultLength, rope.Length);
        }

        [Test]
        public void AddComponentMenuIsDaedalusRope()
        {
            Assert.AreEqual("Daedalus/Rope", typeof(Rope).GetCustomAttribute<AddComponentMenu>().componentMenu);
        }

        [Test]
        public void ToSegmentHangsDownFromTheObjectInSimCoordinates()
        {
            RopeSegment segment = NewRope(3.5f, 10, 4).ToSegment(new Vector2(-2, 1));
            Assert.AreEqual(5.5f, segment.X, Tolerance);
            Assert.AreEqual(5f, segment.Bottom, Tolerance);
            Assert.AreEqual(9f, segment.Top, Tolerance);
        }

        [Test]
        public void FindSortedOrdersByXThenBottom()
        {
            Rope c = NewRope(5, 10, 4);
            Rope a = NewRope(1, 10, 4);
            Rope bHigh = NewRope(3, 12, 4);
            Rope bLow = NewRope(3, 9, 4);
            Rope[] ropes = RopeCollector.FindSorted();
            CollectionAssert.AreEqual(new[] { a, bLow, bHigh, c }, ropes);
        }

        [Test]
        public void FindSortedSkipsDisabledRopesAndInactiveObjects()
        {
            Rope kept = NewRope(1, 10, 4);
            NewRope(2, 10, 4).enabled = false;
            NewRope(3, 10, 4).gameObject.SetActive(false);
            CollectionAssert.AreEqual(new[] { kept }, RopeCollector.FindSorted());
        }

        [Test]
        public void SegmentsMatchToSegmentBeforeTheSimSnap()
        {
            NewRope(2.5f, 7.3f, 3);
            NewRope(6.5f, 4f, 2);
            Rope[] ropes = RopeCollector.FindSorted();
            var origin = new Vector2(-1, -2);
            RopeSegment[] segments = RopeCollector.ToSegments(ropes, origin);
            Assert.AreEqual(2, segments.Length);
            for (int i = 0; i < ropes.Length; i++)
            {
                RopeSegment expected = ropes[i].ToSegment(origin);
                Assert.AreEqual(expected.X, segments[i].X);
                Assert.AreEqual(expected.Bottom, segments[i].Bottom);
                Assert.AreEqual(expected.Top, segments[i].Top);
            }
            Assert.AreEqual(9.3f, segments[0].Top, Tolerance, "not snapped by the runtime");
        }

        [Test]
        public void WorldBoundsCoverEveryRope()
        {
            NewRope(2, 10, 4);
            NewRope(8, 5, 2);
            Assert.IsTrue(RopeCollector.TryGetWorldBounds(RopeCollector.FindSorted(), out Rect bounds));
            Assert.AreEqual(2f, bounds.xMin, Tolerance);
            Assert.AreEqual(8f, bounds.xMax, Tolerance);
            Assert.AreEqual(3f, bounds.yMin, Tolerance);
            Assert.AreEqual(10f, bounds.yMax, Tolerance);
        }

        [Test]
        public void NoRopesGiveAnEmptyArrayAndNoBounds()
        {
            Rope[] ropes = RopeCollector.FindSorted();
            Assert.IsNotNull(ropes);
            Assert.IsEmpty(ropes);
            Assert.IsFalse(RopeCollector.TryGetWorldBounds(ropes, out _));
            Assert.IsEmpty(RopeCollector.ToSegments(ropes, Vector2.zero));
        }

        [Test]
        public void NoRopesDoNotGrowTheMapToTheWorldOrigin()
        {
            Grid grid = NewObject("Grid").AddComponent<Grid>();
            var tilemapObject = new GameObject("Collision");
            tilemapObject.transform.SetParent(grid.transform);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            Tile tile = ScriptableObject.CreateInstance<Tile>();
            created.Add(tile);
            for (int x = 200; x <= 220; x++)
            {
                tilemap.SetTile(new Vector3Int(x, 0, 0), tile);
            }

            // What the Player's Start does: the start hitbox, grown by the rope bounds only when there are ropes.
            var include = new Rect(210 - MovementInternals.HitboxHalfWidth, 4, MovementInternals.HitboxWidth, MovementInternals.HitboxHeight);
            Assert.IsFalse(RopeCollector.TryGetWorldBounds(RopeCollector.FindSorted(), out _));
            CollisionMap map = CollisionMapReader.Read(tilemap, include, new List<string>());
            Assert.AreEqual(new Vector2Int(200, 0), map.CellMin);
            Assert.AreEqual(21, map.Grid.Width);
        }

        Rope NewRope(float x, float y, float length)
        {
            Rope rope = NewObject("Rope").AddComponent<Rope>();
            rope.transform.position = new Vector3(x, y, 0);
            rope.Length = length;
            return rope;
        }

        GameObject NewObject(string name)
        {
            var gameObject = new GameObject(name);
            created.Add(gameObject);
            return gameObject;
        }
    }
}
