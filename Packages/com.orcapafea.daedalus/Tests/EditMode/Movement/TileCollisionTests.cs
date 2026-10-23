using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    public class TileCollisionTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float H = MovementInternals.HitboxHeight;
        const float Tolerance = 1e-4f;

        [Test]
        public void HorizontalSweepStopsExactlyOnTheFaceFromBothSides()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "........",
                "........",
                "........",
                "...#....",
                "...#....",
                "...#....");

            float x = TileCollision.SweepX(grid, 1.5f, 0.5f, Hw, H, 2f, out int side);
            Assert.AreEqual(3f - Hw, x, Tolerance);
            Assert.AreEqual(1, side);

            x = TileCollision.SweepX(grid, 5.5f, 0.5f, Hw, H, -2f, out side);
            Assert.AreEqual(4f + Hw, x, Tolerance);
            Assert.AreEqual(-1, side);

            // A box spanning rows 2 and 3, blocked only in row 2.
            x = TileCollision.SweepX(grid, 1.5f, 2.25f, Hw, H, 2f, out side);
            Assert.AreEqual(3f - Hw, x, Tolerance);
            Assert.AreEqual(1, side);

            // Above the wall it passes.
            x = TileCollision.SweepX(grid, 1.5f, 3f, Hw, H, 2f, out side);
            Assert.AreEqual(3.5f, x, Tolerance);
            Assert.AreEqual(0, side);
        }

        [Test]
        public void LongMoveIntoAThinWallStillStops()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "...#..",
                "...#..");
            float x = TileCollision.SweepX(grid, 2.5f - Hw, 0f, Hw, H, 0.9f, out int side);
            Assert.AreEqual(3f - Hw, x, Tolerance, "0.9-tile move");
            Assert.AreEqual(1, side);

            x = TileCollision.SweepX(grid, 2.5f - Hw, 0f, Hw, H, 5f, out side);
            Assert.AreEqual(3f - Hw, x, Tolerance, "5-tile move");
        }

        [Test]
        public void MapSidesAreWallsAndTheTopIsACeiling()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "....",
                "....",
                "....");
            Assert.AreEqual(Hw, TileCollision.SweepX(grid, 0.5f, 0f, Hw, H, -1f, out int side), Tolerance);
            Assert.AreEqual(-1, side);
            Assert.AreEqual(4f - Hw, TileCollision.SweepX(grid, 3.5f, 0f, Hw, H, 1f, out side), Tolerance);
            Assert.AreEqual(1, side);

            float x = 1.5f, y = 1.2f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, 1f, false, out _, out bool ceiling);
            Assert.IsTrue(ceiling);
            Assert.AreEqual(3f - H, y, Tolerance);
        }

        [Test]
        public void LandingSnapsOntoATop()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "....",
                "....",
                "....",
                "####");
            float x = 1.5f, y = 1.3f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, -0.5f, false, out SupportKind landed, out _);
            Assert.AreEqual(SupportKind.Solid, landed);
            Assert.AreEqual(1f, y);
        }

        [Test]
        public void OneWayCatchesOnlyFromAtOrAboveItsTop()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "....",
                "....",
                "....",
                "....",
                "----",
                "....",
                "####");
            // The one-way is row 2; its top is 3.
            float x = 1.5f, y = 3.4f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, -0.5f, false, out SupportKind landed, out _);
            Assert.AreEqual(SupportKind.OneWayOnly, landed, "from above");
            Assert.AreEqual(3f, y);

            y = 3f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, -0.5f, false, out landed, out _);
            Assert.AreEqual(SupportKind.OneWayOnly, landed, "from exactly on its top");
            Assert.AreEqual(3f, y);

            y = 3f - 0.00005f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, -0.5f, false, out landed, out _);
            Assert.AreEqual(SupportKind.OneWayOnly, landed, "from within the contact epsilon of its top");

            y = 2.5f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, -0.4f, false, out landed, out _);
            Assert.AreEqual(SupportKind.None, landed, "from inside");
            Assert.AreEqual(2.1f, y, Tolerance);

            y = 1f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, 2.5f, false, out landed, out bool ceiling);
            Assert.IsFalse(ceiling, "from below: a one-way is never a ceiling");
            Assert.AreEqual(3.5f, y, Tolerance);

            y = 3.4f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, -0.5f, true, out landed, out _);
            Assert.AreEqual(SupportKind.None, landed, "ignored while on a rope");
        }

        [Test]
        public void OneWayNeverBlocksHorizontally()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "..--..",
                "......");
            float x = TileCollision.SweepX(grid, 0.5f, 0.5f, Hw, H, 4f, out int side);
            Assert.AreEqual(4.5f, x, Tolerance);
            Assert.AreEqual(0, side);
        }

        [Test]
        public void FallingDiagonallyOntoAnOutsideCornerLandsOnTop()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "......",
                "......",
                "....#.",
                "....#.");
            float x = TileCollision.SweepX(grid, 3.5f, 2.2f, Hw, H, 0.3f, out int side);
            Assert.AreEqual(0, side, "the side of the corner does not snag at the old height");
            float y = 2.2f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, -0.3f, false, out SupportKind landed, out _);
            Assert.AreEqual(SupportKind.Solid, landed);
            Assert.AreEqual(2f, y);
            Assert.AreEqual(3.8f, x, Tolerance);
        }

        [Test]
        public void RisingDiagonallyIntoAnUndersideCornerBumps()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "....#.",
                "......",
                "......",
                "......",
                "......");
            // Row 4 holds the corner; after the X move the head overlaps it by 0.375 (> the 0.25 correction).
            float x = TileCollision.SweepX(grid, 3.5f, 2f, Hw, H, 0.5f, out int side);
            Assert.AreEqual(0, side);
            float y = 2f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, 0.7f, false, out _, out bool ceiling);
            Assert.IsTrue(ceiling);
            Assert.AreEqual(4f - H, y, Tolerance);
            Assert.AreEqual(4f, x, Tolerance, "no sideways shift");
        }

        [Test]
        public void CeilingCornerCorrectionShiftsSmallOverlapsOnly()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "....#.",
                "......",
                "......",
                "......",
                "......");
            // Overlap 0.2: shifted left by 0.2 and keeps rising.
            float x = 4.2f - Hw, y = 2f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, 0.7f, false, out _, out bool ceiling);
            Assert.IsFalse(ceiling, "0.2 overlap");
            Assert.AreEqual(4f - Hw, x, Tolerance);
            Assert.AreEqual(2.7f, y, Tolerance);

            // Overlap 0.3: stops.
            x = 4.3f - Hw;
            y = 2f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, 0.7f, false, out _, out ceiling);
            Assert.IsTrue(ceiling, "0.3 overlap");
            Assert.AreEqual(4.3f - Hw, x, Tolerance);
            Assert.AreEqual(4f - H, y, Tolerance);
        }

        [Test]
        public void CeilingCornerCorrectionNeedsAFreePath()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "...#..",
                "....#.",
                "......",
                "......",
                "......",
                "......");
            // The 0.2 shift would put the box under the cell at (3, 5) within this move: no shift.
            float x = 4.2f - Hw, y = 2f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, 1.7f, false, out _, out bool ceiling);
            Assert.IsTrue(ceiling);
            Assert.AreEqual(4.2f - Hw, x, Tolerance);
            Assert.AreEqual(4f - H, y, Tolerance);
        }

        [Test]
        public void CeilingCornerCorrectionNeverExceedsTheCap()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "....#.",
                "......",
                "......",
                "......",
                "......");
            int shifted = 0, stopped = 0;
            for (int i = 1; i <= 50; i++)
            {
                float overlap = i * 0.01f;
                float startX = 4f + overlap - Hw;
                float x = startX, y = 2f;
                TileCollision.SweepY(grid, ref x, ref y, Hw, H, 0.7f, false, out _, out bool ceiling);
                float shift = startX - x;
                Assert.That(shift, Is.LessThanOrEqualTo(MovementInternals.CeilingCornerCorrection + Tolerance), $"overlap {overlap}");
                if (ceiling)
                {
                    stopped++;
                    Assert.AreEqual(0f, shift, Tolerance, $"overlap {overlap}: a stopped head is not shifted");
                }
                else
                {
                    shifted++;
                    Assert.AreEqual(overlap, shift, Tolerance, $"overlap {overlap}: the shift is exactly the overlap");
                }
            }
            Assert.AreEqual(25, shifted, "overlaps 0.01 … 0.25 are corrected");
            Assert.AreEqual(25, stopped, "overlaps 0.26 … 0.50 stop the head");
        }

        [Test]
        public void SupportIsOneWayOnlyOnlyWithoutSolidUnderfoot()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "......",
                "......",
                "--#...");
            Assert.AreEqual(SupportKind.OneWayOnly, TileCollision.Support(grid, 1f, 1f, Hw), "one-way only");
            Assert.AreEqual(SupportKind.Solid, TileCollision.Support(grid, 2f, 1f, Hw), "straddling one-way and solid");
            Assert.AreEqual(SupportKind.Solid, TileCollision.Support(grid, 2.5f, 1f, Hw), "solid");
            Assert.AreEqual(SupportKind.None, TileCollision.Support(grid, 4.5f, 1f, Hw), "nothing");
            Assert.AreEqual(SupportKind.None, TileCollision.Support(grid, 1f, 1.1f, Hw), "not on a row");
            Assert.AreEqual(SupportKind.OneWayOnly, TileCollision.Support(grid, 1f, 1.00005f, Hw), "within the epsilon");
        }

        [Test]
        public void WallGapFindsWallsWithinReachOnly()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "........",
                "........",
                "#..C.-..",
                "#..C.-..");
            const float reach = MovementInternals.WallJumpReach;
            Assert.AreEqual(0.1f, TileCollision.WallGap(grid, 1.1f + Hw, 0f, Hw, H, -1, reach, true), Tolerance, "left wall at 0.1");
            Assert.IsTrue(float.IsPositiveInfinity(TileCollision.WallGap(grid, 1.2f + Hw, 0f, Hw, H, -1, reach, true)), "left wall at 0.2");
            Assert.AreEqual(0f, TileCollision.WallGap(grid, 3f - Hw, 0f, Hw, H, 1, reach, true), Tolerance, "touching a climbable face");
            Assert.IsTrue(float.IsPositiveInfinity(TileCollision.WallGap(grid, 3f - Hw, 0f, Hw, H, 1, reach, false)), "climbable not counted");
            Assert.IsTrue(float.IsPositiveInfinity(TileCollision.WallGap(grid, 5f - Hw, 0f, Hw, H, 1, reach, true)), "a one-way is never a wall");
            Assert.AreEqual(0f, TileCollision.WallGap(grid, 8f - Hw, 0f, Hw, H, 1, reach, true), Tolerance, "the map side");
        }

        [Test]
        public void BoxIsFreeChecksBlockingCellsAndMapBounds()
        {
            TileGrid grid = AsciiLevel.ParseGrid(
                "....",
                "....",
                ".-#.");
            Assert.IsTrue(TileCollision.BoxIsFree(grid, 1.5f, 0f, Hw, H), "one-way cells are not occupied");
            Assert.IsFalse(TileCollision.BoxIsFree(grid, 2.5f, 0f, Hw, H), "solid");
            Assert.IsFalse(TileCollision.BoxIsFree(grid, 0.2f, 1f, Hw, H), "past the left side");
            Assert.IsFalse(TileCollision.BoxIsFree(grid, 0.5f, 2f, Hw, H), "past the top");
            Assert.IsTrue(TileCollision.BoxIsFree(grid, 0.5f, -5f, Hw, H), "below the map is open");
        }
    }
}
