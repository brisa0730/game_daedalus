using System;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    public class TileGridTests
    {
        [Test]
        public void SetThenGetRoundTrips()
        {
            var grid = new TileGrid(4, 3);
            grid.Set(0, 0, TileKind.Solid);
            grid.Set(3, 2, TileKind.Climbable);
            grid.Set(2, 1, TileKind.OneWay);
            Assert.AreEqual(TileKind.Solid, grid.Get(0, 0));
            Assert.AreEqual(TileKind.Climbable, grid.Get(3, 2));
            Assert.AreEqual(TileKind.OneWay, grid.Get(2, 1));
            Assert.AreEqual(TileKind.None, grid.Get(1, 1));
        }

        [TestCase(-1, 0)]
        [TestCase(4, 0)]
        [TestCase(0, -1)]
        [TestCase(0, 3)]
        public void SetOutsideTheGridThrows(int x, int y)
        {
            var grid = new TileGrid(4, 3);
            Assert.Throws<ArgumentOutOfRangeException>(() => grid.Set(x, y, TileKind.Solid));
        }

        [TestCase(0, 1)]
        [TestCase(1, 0)]
        public void EmptyGridThrows(int width, int height)
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => new TileGrid(width, height));
        }

        [Test]
        public void SidesAndTopAreSolidAndBelowIsOpen()
        {
            var grid = new TileGrid(4, 3);
            Assert.AreEqual(TileKind.Solid, grid.Get(-1, 1), "left side");
            Assert.AreEqual(TileKind.Solid, grid.Get(4, 1), "right side");
            Assert.AreEqual(TileKind.Solid, grid.Get(-1, -5), "left side, below the map");
            Assert.AreEqual(TileKind.Solid, grid.Get(1, 3), "top");
            Assert.AreEqual(TileKind.None, grid.Get(1, -1), "below the map");
        }

        [Test]
        public void AsciiLevelBuildsCellsRopesAndStart()
        {
            AsciiLevel level = AsciiLevel.Parse(
                "..R.",
                "C.+.",
                ".PR#",
                "--##");
            TileGrid grid = level.Grid;
            Assert.AreEqual(4, grid.Width);
            Assert.AreEqual(4, grid.Height);
            Assert.AreEqual(TileKind.OneWay, grid.Get(0, 0));
            Assert.AreEqual(TileKind.Solid, grid.Get(3, 0));
            Assert.AreEqual(TileKind.Solid, grid.Get(3, 1));
            Assert.AreEqual(TileKind.Climbable, grid.Get(0, 2));
            Assert.AreEqual(TileKind.OneWay, grid.Get(2, 2));
            Assert.AreEqual(TileKind.None, grid.Get(2, 1));
            Assert.AreEqual(TileKind.None, grid.Get(1, 1));

            Assert.AreEqual(1.5f, level.StartX);
            Assert.AreEqual(1f, level.StartY);

            Assert.AreEqual(1, level.Ropes.Length, "a rope crossing a one-way is one segment");
            Assert.AreEqual(2.5f, level.Ropes[0].X);
            Assert.AreEqual(1f, level.Ropes[0].Bottom);
            Assert.AreEqual(4f, level.Ropes[0].Top);
        }

        [Test]
        public void AsciiLevelOrdersRopesByColumnThenBottom()
        {
            AsciiLevel level = AsciiLevel.Parse(
                "R..",
                "..R",
                "R.R",
                "R#.",
                ".P.");
            Assert.AreEqual(3, level.Ropes.Length);
            Assert.AreEqual(0.5f, level.Ropes[0].X);
            Assert.AreEqual(1f, level.Ropes[0].Bottom);
            Assert.AreEqual(3f, level.Ropes[0].Top);
            Assert.AreEqual(0.5f, level.Ropes[1].X);
            Assert.AreEqual(4f, level.Ropes[1].Bottom);
            Assert.AreEqual(5f, level.Ropes[1].Top);
            Assert.AreEqual(2.5f, level.Ropes[2].X);
            Assert.AreEqual(2f, level.Ropes[2].Bottom);
            Assert.AreEqual(4f, level.Ropes[2].Top);
        }

        [Test]
        public void MalformedMapsThrow()
        {
            Assert.Throws<ArgumentException>(() => AsciiLevel.Parse("..", "P"), "uneven rows");
            Assert.Throws<ArgumentException>(() => AsciiLevel.Parse("...", "###"), "no start");
            Assert.Throws<ArgumentException>(() => AsciiLevel.Parse("P.P", "###"), "two starts");
            Assert.Throws<ArgumentException>(() => AsciiLevel.Parse(".P?", "###"), "unknown character");
        }

        [Test]
        public void RopeLengthDefaultAndRangeArePinned()
        {
            Assert.AreEqual(4f, RopeSegment.DefaultLength);
            Assert.AreEqual(1f, RopeSegment.MinLength);
            Assert.AreEqual(50f, RopeSegment.MaxLength);
        }

        [Test]
        public void RopeNeedsTopAboveBottom()
        {
            Assert.Throws<ArgumentException>(() => new RopeSegment(0.5f, 2f, 2f));
        }
    }
}
