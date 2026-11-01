using System.Collections.Generic;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    public class CollisionMapReaderTests
    {
        const float Tolerance = 1e-5f;
        static readonly Rect Nothing = default;

        readonly List<Object> created = new List<Object>();
        readonly List<string> problems = new List<string>();
        Tile plain;

        [SetUp]
        public void SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            plain = Track(ScriptableObject.CreateInstance<Tile>());
            problems.Clear();
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
        public void PlainTilesAreSolidAndDaedalusTilesKeepTheirKind()
        {
            Tilemap tilemap = NewTilemap("Collision", Vector3.zero);
            tilemap.SetTile(new Vector3Int(0, 0, 0), plain);
            tilemap.SetTile(new Vector3Int(1, 0, 0), Kind(TileCollisionKind.OneWay));
            tilemap.SetTile(new Vector3Int(2, 0, 0), Kind(TileCollisionKind.Climbable));
            tilemap.SetTile(new Vector3Int(3, 0, 0), Kind(TileCollisionKind.None));
            tilemap.SetTile(new Vector3Int(5, 0, 0), Kind(TileCollisionKind.Solid));

            TileGrid grid = CollisionMapReader.Read(tilemap, Nothing, problems).Grid;
            Assert.AreEqual(TileKind.Solid, grid.Get(0, 0));
            Assert.AreEqual(TileKind.OneWay, grid.Get(1, 0));
            Assert.AreEqual(TileKind.Climbable, grid.Get(2, 0));
            Assert.AreEqual(TileKind.None, grid.Get(3, 0));
            Assert.AreEqual(TileKind.None, grid.Get(4, 0), "an empty cell");
            Assert.AreEqual(TileKind.Solid, grid.Get(5, 0));
            Assert.IsEmpty(problems);
        }

        [Test]
        public void OnlyTheGivenTilemapIsRead()
        {
            Tilemap collision = NewTilemap("Collision", Vector3.zero);
            Tilemap background = NewTilemap("Background", Vector3.zero);
            collision.SetTile(new Vector3Int(0, 0, 0), plain);
            collision.SetTile(new Vector3Int(3, 0, 0), plain);
            background.SetTile(new Vector3Int(1, 0, 0), plain);
            TileGrid grid = CollisionMapReader.Read(collision, Nothing, problems).Grid;
            Assert.AreEqual(TileKind.None, grid.Get(1, 0));
            Assert.AreEqual(TileKind.Solid, grid.Get(3, 0));
        }

        [Test]
        public void OriginCoversTheCellOffsetAndTheGridPosition()
        {
            Tilemap tilemap = NewTilemap("Collision", new Vector3(0.5f, 2, 0));
            tilemap.SetTile(new Vector3Int(-5, -3, 0), plain);
            tilemap.SetTile(new Vector3Int(4, 1, 0), plain);
            CollisionMap map = CollisionMapReader.Read(tilemap, Nothing, problems);
            Assert.AreEqual(new Vector2Int(-5, -3), map.CellMin);
            Assert.AreEqual(-4.5f, map.Origin.x, Tolerance);
            Assert.AreEqual(-1f, map.Origin.y, Tolerance);
            Assert.AreEqual(TileKind.Solid, map.Grid.Get(0, 0));
            Assert.AreEqual(TileKind.Solid, map.Grid.Get(9, 4));

            var p = new Vector2(3.25f, 7.75f);
            Vector2 back = map.SimToWorld(map.WorldToSim(p));
            Assert.AreEqual(p.x, back.x, Tolerance);
            Assert.AreEqual(p.y, back.y, Tolerance);
        }

        [Test]
        public void BoundsGrowToIncludeTheStartPlusHeadroom()
        {
            Tilemap tilemap = FloorRow(0, 9);
            // Feet 3 tiles above the floor top (row 0's top is y = 1).
            CollisionMap map = CollisionMapReader.Read(tilemap, Hitbox(5, 4), problems);
            int topRow = map.CellMin.y + map.Grid.Height - 1;
            Assert.That(topRow, Is.GreaterThanOrEqualTo(4 + 1 + MovementInternals.MapHeadroomRows - 1), "the start's top row plus 24 rows");
            Assert.AreEqual(0, map.CellMin.y);
            Assert.AreEqual(10, map.Grid.Width);
        }

        [Test]
        public void BoundsGrowToIncludeARopeRightOfEveryTile()
        {
            Tilemap tilemap = FloorRow(0, 9);
            var rope = Rect.MinMaxRect(15.5f, 2, 15.5f, 6);
            CollisionMap map = CollisionMapReader.Read(tilemap, rope, problems);
            Assert.That(map.CellMin.x + map.Grid.Width - 1, Is.GreaterThanOrEqualTo(15));
        }

        [Test]
        public void BoundsGrowToIncludeAStartBelowTheFloor()
        {
            Tilemap tilemap = FloorRow(0, 9);
            CollisionMap map = CollisionMapReader.Read(tilemap, Hitbox(5, -5), problems);
            Assert.That(map.CellMin.y, Is.LessThanOrEqualTo(-5));
            Assert.AreEqual(TileKind.Solid, map.Grid.Get(0, -map.CellMin.y), "the floor moved up in sim rows");
        }

        [Test]
        public void ReadingWritesNothing()
        {
            Tilemap tilemap = FloorRow(0, 9);
            tilemap.SetTile(new Vector3Int(9, 0, 0), null);
            BoundsInt before = tilemap.cellBounds;
            int tiles = tilemap.GetUsedTilesCount();
            CollisionMapReader.Read(tilemap, Hitbox(30, 30), problems);
            Assert.AreEqual(before, tilemap.cellBounds, "no CompressBounds or growth of the authored bounds");
            Assert.AreEqual(tiles, tilemap.GetUsedTilesCount());
        }

        [Test]
        public void ErasedCellsDoNotKeepTheMapWide()
        {
            Tilemap tilemap = FloorRow(0, 9);
            tilemap.SetTile(new Vector3Int(8, 0, 0), null);
            tilemap.SetTile(new Vector3Int(9, 0, 0), null);
            Assert.AreEqual(10, tilemap.cellBounds.size.x, "precondition: the Tilemap's bounds didn't shrink");
            Assert.AreEqual(8, CollisionMapReader.Read(tilemap, Nothing, problems).Grid.Width, "an invisible wall would stand at x = 10");
        }

        [Test]
        public void AHalfSizeCellAddsOneProblem()
        {
            Tilemap tilemap = FloorRow(0, 3);
            tilemap.layoutGrid.GetComponent<Grid>().cellSize = new Vector3(0.5f, 0.5f, 0);
            CollisionMapReader.Read(tilemap, Nothing, problems);
            CollectionAssert.AreEqual(new[] { PlayerWarnings.CellSize(new Vector3(0.5f, 0.5f, 0)) }, problems);
        }

        [Test]
        public void ARotatedTilemapAddsOneProblem()
        {
            Tilemap tilemap = FloorRow(0, 3);
            tilemap.transform.rotation = Quaternion.Euler(0, 0, 90);
            CollisionMapReader.Read(tilemap, Nothing, problems);
            CollectionAssert.AreEqual(new[] { PlayerWarnings.RotatedOrScaled }, problems);
        }

        [Test]
        public void AScaledGridAddsOneProblem()
        {
            Tilemap tilemap = FloorRow(0, 3);
            tilemap.layoutGrid.transform.localScale = new Vector3(2, 2, 1);
            CollisionMapReader.Read(tilemap, Nothing, problems);
            CollectionAssert.AreEqual(new[] { PlayerWarnings.RotatedOrScaled }, problems);
        }

        [Test]
        public void AHexagonGridAddsOneProblem()
        {
            Tilemap tilemap = FloorRow(0, 3);
            tilemap.layoutGrid.GetComponent<Grid>().cellLayout = GridLayout.CellLayout.Hexagon;
            CollisionMapReader.Read(tilemap, Nothing, problems);
            CollectionAssert.AreEqual(new[] { PlayerWarnings.CellLayout }, problems);
        }

        [Test]
        public void ACleanSetupAddsNoProblem()
        {
            CollisionMapReader.Read(FloorRow(0, 3), Hitbox(1, 3), problems);
            Assert.IsEmpty(problems);
        }

        [Test]
        public void AnEmptyTilemapGivesOneEmptyCellAndAProblem()
        {
            CollisionMap map = CollisionMapReader.Read(NewTilemap("Collision", Vector3.zero), Nothing, problems);
            Assert.AreEqual(1, map.Grid.Width);
            Assert.AreEqual(1, map.Grid.Height);
            Assert.AreEqual(TileKind.None, map.Grid.Get(0, 0));
            CollectionAssert.AreEqual(new[] { PlayerWarnings.EmptyTilemap }, problems);
        }

        [Test]
        public void NoTilemapGivesAnEmptyMapAroundTheStart()
        {
            CollisionMap map = CollisionMapReader.Empty(Hitbox(5.5f, 3));
            Vector2 feet = map.WorldToSim(new Vector2(5.5f, 3));
            StartPlacement placement = StartPlacement.Find(map.Grid, feet.x, feet.y);
            Assert.IsTrue(placement.Found);
            Assert.IsFalse(placement.Moved, "the start is free, so the player falls");
            Assert.That(map.Grid.Height, Is.GreaterThan(MovementInternals.MapHeadroomRows));
        }

        static Rect Hitbox(float feetX, float feetY)
        {
            return new Rect(feetX - MovementInternals.HitboxHalfWidth, feetY, MovementInternals.HitboxWidth, MovementInternals.HitboxHeight);
        }

        Tilemap FloorRow(int fromX, int toX)
        {
            Tilemap tilemap = NewTilemap("Collision", Vector3.zero);
            for (int x = fromX; x <= toX; x++)
            {
                tilemap.SetTile(new Vector3Int(x, 0, 0), plain);
            }
            return tilemap;
        }

        DaedalusTile Kind(TileCollisionKind kind)
        {
            DaedalusTile tile = Track(ScriptableObject.CreateInstance<DaedalusTile>());
            tile.Kind = kind;
            return tile;
        }

        Tilemap NewTilemap(string name, Vector3 gridPosition)
        {
            Grid grid = Track(new GameObject("Grid")).AddComponent<Grid>();
            grid.transform.position = gridPosition;
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(grid.transform, false);
            return tilemapObject.AddComponent<Tilemap>();
        }

        T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
