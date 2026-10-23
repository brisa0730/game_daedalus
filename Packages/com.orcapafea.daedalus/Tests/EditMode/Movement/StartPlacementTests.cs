using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    public class StartPlacementTests
    {
        [Test]
        public void FreeStartIsUnchanged()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "....",
                "....",
                "....",
                "####");
            StartPlacement p = StartPlacement.Find(grid, 1.3f, 1f);
            Assert.IsTrue(p.Found);
            Assert.IsFalse(p.Moved);
            Assert.AreEqual(1.3f, p.X);
            Assert.AreEqual(1f, p.Y);
        }

        [Test]
        public void StartInsideTheGroundMovesUp()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "......",
                "######",
                "######");
            StartPlacement p = StartPlacement.Find(grid, 2.5f, 1f);
            Assert.IsTrue(p.Found);
            Assert.IsTrue(p.Moved);
            Assert.AreEqual(2.5f, p.X);
            Assert.AreEqual(2f, p.Y);
        }

        [Test]
        public void StartOverlappingAWallMovesBesideIt()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "........",
                "........",
                "...#....",
                "...#....",
                "...#....",
                "...#....",
                "...#....",
                "########");
            StartPlacement p = StartPlacement.Find(grid, 3.2f, 1f);
            Assert.IsTrue(p.Found);
            Assert.AreEqual(2.5f, p.X);
            Assert.AreEqual(1f, p.Y);
        }

        [Test]
        public void TiesGoLeftThenUp()
        {
            TileGrid wall = AsciiLevel.ParseGrid(
                "........",
                "........",
                "...#....",
                "...#....",
                "...#....",
                "...#....",
                "...#....",
                "########");
            StartPlacement p = StartPlacement.Find(wall, 3.5f, 1f);
            Assert.AreEqual(2.5f, p.X, "left and right equidistant: left");
            Assert.AreEqual(1f, p.Y);

            // A full-width slab in row 3: feet 2.5 are 1.5 from row 4 (above) and from row 1 (below, head under the slab).
            TileGrid slab = AsciiLevel.ParseGrid(
                "......",
                "......",
                "######",
                "......",
                "......",
                "......");
            p = StartPlacement.Find(slab, 2.5f, 2.5f);
            Assert.IsTrue(p.Found);
            Assert.AreEqual(2.5f, p.X);
            Assert.AreEqual(4f, p.Y, "up and down equidistant: up");
        }

        [Test]
        public void StartBelowTheMapMovesInside()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "......",
                "######");
            StartPlacement p = StartPlacement.Find(grid, 2.5f, -3f);
            Assert.IsTrue(p.Found);
            Assert.IsTrue(p.Moved);
            Assert.AreEqual(2.5f, p.X);
            Assert.AreEqual(1f, p.Y);
        }

        [Test]
        public void FullyEnclosedStartIsNotFound()
        {
            var grid = new TileGrid(17, 17);
            for (int x = 0; x < 17; x++)
            {
                for (int y = 0; y < 17; y++)
                {
                    grid.Set(x, y, TileKind.Solid);
                }
            }
            StartPlacement p = StartPlacement.Find(grid, 8.5f, 8f);
            Assert.IsFalse(p.Found);
            Assert.IsTrue(p.Moved);
            Assert.AreEqual(8.5f, p.X);
            Assert.AreEqual(8f, p.Y);
        }

        [Test]
        public void OneWayCellsAreNotOccupied()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "....",
                "....",
                "----",
                "----",
                "####");
            StartPlacement p = StartPlacement.Find(grid, 1.5f, 1f);
            Assert.IsFalse(p.Moved);
            Assert.AreEqual(1f, p.Y);
        }
    }
}
