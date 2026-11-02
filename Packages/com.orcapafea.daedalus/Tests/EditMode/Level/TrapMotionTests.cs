using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Move and Fall kinematics: fixed per-tick steps, exact arrival and landing, painted-only landing, travel ticks.</summary>
    public class TrapMotionTests
    {
        const float Tolerance = 1e-4f;

        static readonly DerivedMovement Defaults = new DerivedMovement(MovementSettings.Default);

        [Test]
        public void AMoveArrivesExactlyOnItsLastTick()
        {
            var start = new Box(2, 3, 5, 4);
            Assert.AreEqual(38, TrapMotion.MoveTicks(5, 0, 8f), "5 tiles at 8 tiles/s");
            TrapMotion.MovePosition(start, 5, 0, 8f, 37, out float x, out float y, out bool arrived);
            Assert.IsFalse(arrived);
            Assert.That(x, Is.LessThan(7f), "short of the end at tick 37");
            Assert.AreEqual(3f, y);
            TrapMotion.MovePosition(start, 5, 0, 8f, 38, out x, out y, out arrived);
            Assert.IsTrue(arrived);
            Assert.AreEqual(7f, x, "exactly the end");
            Assert.AreEqual(3f, y);

            Assert.AreEqual(10, TrapMotion.MoveTicks(3, 4, 30f), "a diagonal 3 x 4 at 30 tiles/s");
            TrapMotion.MovePosition(start, 3, 4, 30f, 10, out x, out y, out arrived);
            Assert.IsTrue(arrived);
            Assert.AreEqual(5f, x);
            Assert.AreEqual(7f, y);

            Assert.AreEqual(0, TrapMotion.MoveTicks(0, 0, 8f), "a zero offset arrives at once");
            TrapMotion.MovePosition(start, 0, 0, 8f, 0, out x, out y, out arrived);
            Assert.IsTrue(arrived);
            Assert.AreEqual(2f, x);
        }

        [Test]
        public void TheLongestSlowestMoveIsBitExactAtItsEnd()
        {
            var start = new Box(0.5f, 0, 1.5f, 1);
            Assert.AreEqual(1800, TrapMotion.MoveTicks(30, 0, 1f), "30 / (1/60 as float) = 1799.9999 rounds up");
            TrapMotion.MovePosition(start, 30, 0, 1f, 1799, out float x, out _, out bool arrived);
            Assert.IsFalse(arrived, "not there at tick 1799");
            Assert.That(x, Is.LessThan(30.5f));
            TrapMotion.MovePosition(start, 30, 0, 1f, 1800, out x, out float y, out arrived);
            Assert.IsTrue(arrived);
            Assert.AreEqual(30.5f, x);
            Assert.AreEqual(0f, y);
        }

        [Test]
        public void APositionIsComputedFromTheTickCountNotAccumulated()
        {
            var start = new Box(0, 0, 1, 1);
            float step = TrapMotion.MoveStep(8f);
            TrapMotion.MovePosition(start, 0, -6, 8f, 20, out _, out float y, out _);
            Assert.AreEqual(-20 * step, y, 1e-6f);
        }

        // A column of painted rows under a 2-wide box, built top first.
        static TileGrid Column(params string[] rows)
        {
            return AsciiLevel.ParseGrid(rows);
        }

        [Test]
        public void AFallAcceleratesUnderRiseGravityUpToMaxFall()
        {
            TileGrid grid = Column(new string('.', 4), new string('.', 4), new string('.', 4), new string('.', 4));
            var box = new Box(1, 3, 3, 4);
            float vy = 0;
            Assert.IsFalse(TrapMotion.FallStep(grid, box, Defaults.RiseGravity, Defaults.MaxFallSpeed, ref vy, out float minY));
            Assert.AreEqual(Defaults.RiseGravity / 60f, vy, Tolerance, "the speed after one tick");
            Assert.AreEqual(3f - vy / 60f, minY, Tolerance);
            vy = 11.9f;
            TrapMotion.FallStep(grid, box, Defaults.RiseGravity, Defaults.MaxFallSpeed, ref vy, out _);
            Assert.AreEqual(12f, vy, "capped at max fall");
        }

        [Test]
        public void AFallLandsExactlyOnPaintedSolidThroughOneWayAndTrapCells()
        {
            TileGrid grid = Column(
                "....",
                "....",
                "----",
                "....",
                "....",
                "####",
                "....");
            grid.SetTrapCell(1, 3, TileKind.Solid);
            grid.SetTrapCell(2, 3, TileKind.Solid);
            var box = new Box(1, 6, 3, 7);
            int ticks = 0;
            float vy = 0;
            bool landed = false;
            float minY = box.MinY;
            while (!landed && ticks < 600)
            {
                landed = TrapMotion.FallStep(grid, box, Defaults.RiseGravity, Defaults.MaxFallSpeed, ref vy, out minY);
                box = new Box(1, minY, 3, minY + 1);
                ticks++;
            }
            Assert.IsTrue(landed);
            Assert.AreEqual(2f, minY, "on the painted Solid row's top, through the one-way row 4 and the trap row 3");
            Assert.AreEqual(0f, vy);
            Assert.AreEqual(ticks, TrapMotion.FallTicks(grid, new Box(1, 6, 3, 7), Defaults.RiseGravity, Defaults.MaxFallSpeed),
                "FallTicks counts the same steps");
            Assert.IsTrue(TrapMotion.TryLandingRow(grid, new Box(1, 6, 3, 7), out int row));
            Assert.AreEqual(1, row);
        }

        [Test]
        public void AFallLandsOnPaintedClimbable()
        {
            TileGrid grid = Column("..", "..", "..", "CC", "..");
            Assert.IsTrue(TrapMotion.TryLandingRow(grid, new Box(0, 4, 2, 5), out int row));
            Assert.AreEqual(1, row);
            Assert.That(TrapMotion.FallTicks(grid, new Box(0, 4, 2, 5), Defaults.RiseGravity, Defaults.MaxFallSpeed), Is.GreaterThan(0));
        }

        [Test]
        public void WithNothingBelowItNeverLands()
        {
            TileGrid grid = Column("....", "....", "-..-", "....");
            var box = new Box(1, 3, 3, 4);
            Assert.IsFalse(TrapMotion.TryLandingRow(grid, box, out _));
            Assert.AreEqual(-1, TrapMotion.FallTicks(grid, box, Defaults.RiseGravity, Defaults.MaxFallSpeed));
            float vy = 0;
            for (int i = 0; i < 120; i++)
            {
                Assert.IsFalse(TrapMotion.FallStep(grid, box, Defaults.RiseGravity, Defaults.MaxFallSpeed, ref vy, out float minY), $"tick {i}");
                box = new Box(1, minY, 3, minY + 1);
            }
            Assert.That(box.MaxY, Is.LessThan(0f), "left the map");
        }

        [Test]
        public void AFloorDirectlyUnderTheBoxMeansZeroTicks()
        {
            TileGrid grid = Column("...", "...", "###");
            var box = new Box(0, 1, 3, 2);
            Assert.IsTrue(TrapMotion.TryLandingRow(grid, box, out int row));
            Assert.AreEqual(0, row);
            Assert.AreEqual(0, TrapMotion.FallTicks(grid, box, Defaults.RiseGravity, Defaults.MaxFallSpeed));
            float vy = 0;
            Assert.IsTrue(TrapMotion.FallStep(grid, box, Defaults.RiseGravity, Defaults.MaxFallSpeed, ref vy, out float minY));
            Assert.AreEqual(1f, minY, "stays");
        }

        [Test]
        public void ABottomInsideAPaintedRowSnapsUpToItsTop()
        {
            // An exit's unsnapped sprite box can start that way (round 1, F7).
            TileGrid grid = Column("...", "...", "...", "###", "...", "...");
            var box = new Box(0.2f, 2.95f, 1.2f, 4.95f);
            Assert.IsTrue(TrapMotion.TryLandingRow(grid, box, out int row));
            Assert.AreEqual(2, row, "the row the bottom is inside");
            float vy = 0;
            Assert.IsTrue(TrapMotion.FallStep(grid, box, Defaults.RiseGravity, Defaults.MaxFallSpeed, ref vy, out float minY));
            Assert.AreEqual(3f, minY, "up onto the top");
            Assert.AreEqual(0, TrapMotion.FallTicks(grid, box, Defaults.RiseGravity, Defaults.MaxFallSpeed));
        }

        [Test]
        public void TheSpecClampsOffsetAndSpeed()
        {
            var spec = new TrapSpec(0, 0, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, new Box(0, 0, 1, 2), 0, 40, -40, 0f);
            Assert.AreEqual(30, spec.OffsetX);
            Assert.AreEqual(-30, spec.OffsetY);
            Assert.AreEqual(1f, spec.Speed);
            spec = new TrapSpec(0, 0, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, new Box(0, 0, 1, 2), 0, 1, 1, float.NaN);
            Assert.AreEqual(8f, spec.Speed, "non-finite → default");
            spec = new TrapSpec(0, 0, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, new Box(0, 0, 1, 2), 0, 1, 1, 99f);
            Assert.AreEqual(30f, spec.Speed);
            Assert.AreEqual(-1, spec.ExitIndex, "a tile trap");
            Assert.AreEqual(0f, spec.StartBox.MinX);
            Assert.AreEqual(1f, spec.StartBox.MaxY);

            TrapSpec exit = TrapSpec.ForExit(2, new Box(4.2f, 1, 5.2f, 3), TrapActionKind.Move, TrapTriggerKind.Zone, new Box(3, 0, 6, 4), 0, 6, 0, 8f);
            Assert.AreEqual(2, exit.ExitIndex);
            Assert.AreEqual(TileKind.None, exit.Kind);
            Assert.AreEqual(4.2f, exit.StartBox.MinX);
            Assert.Throws<System.ArgumentOutOfRangeException>(() =>
                new TrapSpec(0, 0, 1, 1, TileKind.None, TrapActionKind.Move, TrapTriggerKind.Zone, new Box(0, 0, 1, 2), 0));
        }
    }
}
