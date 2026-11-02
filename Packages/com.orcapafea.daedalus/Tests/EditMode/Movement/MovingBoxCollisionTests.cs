using NUnit.Framework;
using Orcapafea.Daedalus.Sim;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Every <see cref="TileCollision"/> query sees the moving-box layer: a Solid box blocks wherever it is, a Hazard box only kills.</summary>
    public class MovingBoxCollisionTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float H = MovementInternals.HitboxHeight;
        const float Inset = LevelRules.HazardInset;
        const float Tolerance = 1e-4f;
        const float E = 0.001f;

        // An empty 12 x 10 grid with one box: (4.3, 1.37) to (6.3, 2.37), top at 2.37.
        static TileGrid WithBox(TileKind kind, out Box box)
        {
            var grid = new TileGrid(12, 10);
            var layer = new MovingBoxes(2);
            box = new Box(4.3f, 1.37f, 6.3f, 2.37f);
            layer.Set(1, box, kind);
            grid.AttachBoxes(layer);
            return grid;
        }

        [Test]
        public void TheFeetLandOnABoxTopAndStandOnIt()
        {
            TileGrid grid = WithBox(TileKind.Solid, out Box box);
            float x = 5f, y = 4f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, -3f, false, out SupportKind landed, out bool ceiling);
            Assert.AreEqual(SupportKind.Solid, landed);
            Assert.IsFalse(ceiling);
            Assert.AreEqual(box.MaxY, y, "exactly on the fractional top");
            Assert.AreEqual(SupportKind.Solid, TileCollision.Support(grid, x, y, Hw), "stands on it at a fractional height");
            Assert.AreEqual(SupportKind.None, TileCollision.Support(grid, x, y + 0.01f, Hw), "not just above");
            Assert.AreEqual(SupportKind.None, TileCollision.Support(grid, 4.3f - Hw, y, Hw), "touching its side edge isn't standing on it");
        }

        [Test]
        public void RunningIntoItsSideStopsAtTheFace()
        {
            TileGrid grid = WithBox(TileKind.Solid, out Box box);
            float x = TileCollision.SweepX(grid, 2f, 1f, Hw, H, 3f, out int side);
            Assert.AreEqual(box.MinX - Hw, x, Tolerance);
            Assert.AreEqual(1, side);
            x = TileCollision.SweepX(grid, 9f, 1f, Hw, H, -4f, out side);
            Assert.AreEqual(box.MaxX + Hw, x, Tolerance);
            Assert.AreEqual(-1, side);
            x = TileCollision.SweepX(grid, 2f, box.MaxY, Hw, H, 4f, out side);
            Assert.AreEqual(6f, x, Tolerance, "over its top it passes");
            Assert.AreEqual(0, side);
        }

        [Test]
        public void TheHeadBonksItsBottom()
        {
            TileGrid grid = WithBox(TileKind.Solid, out Box box);
            float x = 5f, y = -0.5f;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, 1f, false, out _, out bool ceiling);
            Assert.IsTrue(ceiling);
            Assert.AreEqual(box.MinY - H, y, Tolerance);
        }

        [Test]
        public void ItsFaceIsAWallWithinReach()
        {
            TileGrid grid = WithBox(TileKind.Solid, out Box box);
            float x = box.MinX - Hw - 0.1f;
            Assert.AreEqual(0.1f, TileCollision.WallGap(grid, x, 1.5f, Hw, H, 1, 0.125f, true), Tolerance);
            Assert.AreEqual(0.1f, TileCollision.WallGap(grid, x, 1.5f, Hw, H, 1, 0.125f, false), Tolerance, "never Climbable");
            Assert.IsTrue(float.IsPositiveInfinity(TileCollision.WallGap(grid, x, 1.5f, Hw, H, 1, 0.05f, true)), "out of reach");
            Assert.IsFalse(TileCollision.IsClimbableAt(grid, box.MinX + 0.1f, 2f), "no cling on a trap");
        }

        [Test]
        public void BoxIsFreeSeesItWithinTheContactEpsilon()
        {
            TileGrid grid = WithBox(TileKind.Solid, out Box box);
            Assert.IsFalse(TileCollision.BoxIsFree(grid, box.MinX - Hw + E, 1f, Hw, H), "overlapping by E");
            Assert.IsTrue(TileCollision.BoxIsFree(grid, box.MinX - Hw - E, 1f, Hw, H), "E clear");
            Assert.IsTrue(TileCollision.BoxIsFree(grid, 5f, box.MaxY, Hw, H), "standing on its top");
            Assert.IsFalse(TileCollision.BoxIsFree(grid, 5f, box.MaxY - E, Hw, H));
        }

        [Test]
        public void AHazardBoxKillsTheCoreAndNeitherBlocksNorSupports()
        {
            TileGrid grid = WithBox(TileKind.Hazard, out Box box);
            Assert.IsTrue(TileCollision.CoreTouchesHazard(grid, box.MinX - Hw + Inset + E, 1.5f), "reaching E past the inset");
            Assert.IsFalse(TileCollision.CoreTouchesHazard(grid, box.MinX - Hw + Inset - E, 1.5f), "E inside the inset");
            Assert.IsTrue(TileCollision.BoxIsFree(grid, 5f, 1.5f, Hw, H), "doesn't block");
            float x = TileCollision.SweepX(grid, 2f, 1.5f, Hw, H, 5f, out int side);
            Assert.AreEqual(7f, x, Tolerance, "sweeps through");
            Assert.AreEqual(0, side);
            float fx = 5f, fy = 4f;
            TileCollision.SweepY(grid, ref fx, ref fy, Hw, H, -3f, false, out SupportKind landed, out _);
            Assert.AreEqual(SupportKind.None, landed, "falls through");
            Assert.AreEqual(SupportKind.None, TileCollision.Support(grid, 5f, box.MaxY, Hw));
        }

        [Test]
        public void TheCornerCorrectionSlidesPastACellCornerButNotABoxCorner()
        {
            // A ceiling cell at column 5, row 4; the head 0.2 under its left corner.
            var grid = new TileGrid(12, 10);
            grid.Set(5, 4, TileKind.Solid);
            float x = 5f + 0.2f - Hw, y = 4f - H - 0.05f;
            float startX = x;
            TileCollision.SweepY(grid, ref x, ref y, Hw, H, 0.2f, false, out _, out bool ceiling);
            Assert.IsFalse(ceiling, "slid past the cell corner");
            Assert.AreEqual(startX - 0.2f, x, Tolerance);

            var boxGrid = new TileGrid(12, 10);
            var layer = new MovingBoxes(1);
            layer.Set(0, new Box(5, 4, 6, 5), TileKind.Solid);
            boxGrid.AttachBoxes(layer);
            x = startX;
            y = 4f - H - 0.05f;
            TileCollision.SweepY(boxGrid, ref x, ref y, Hw, H, 0.2f, false, out _, out ceiling);
            Assert.IsTrue(ceiling, "a box corner stops the head");
            Assert.AreEqual(startX, x, "no slide");
            Assert.AreEqual(4f - H, y, Tolerance);
        }

        [Test]
        public void TheNearestHitWinsBetweenCellAndBox()
        {
            TileGrid grid = WithBox(TileKind.Solid, out Box box);
            grid.Set(8, 1, TileKind.Solid);
            float x = TileCollision.SweepX(grid, 2f, 1f, Hw, H, 9f, out _);
            Assert.AreEqual(box.MinX - Hw, x, Tolerance, "the box is nearer than the cell");
            grid.Set(3, 1, TileKind.Solid);
            x = TileCollision.SweepX(grid, 1.5f, 1f, Hw, H, 9f, out _);
            Assert.AreEqual(3f - Hw, x, Tolerance, "the cell is nearer than the box");
        }

        [Test]
        public void SweptOverlapsCatchesAPassThroughAndNotAGrazeOrAMiss()
        {
            var target = new Box(5, 1, 5.35f, 2.1f);
            Assert.IsTrue(Box.SweptOverlaps(new Box(3, 1, 4, 2), 3f, 0, target), "fully through within one call");
            Assert.IsFalse(Box.SweptOverlaps(new Box(3, 3, 4, 4), 3f, 0, target), "parallel miss");
            Assert.IsFalse(Box.SweptOverlaps(new Box(3, 2.1f, 4, 3.1f), 3f, 0, target), "edge graze");
            Assert.IsFalse(Box.SweptOverlaps(new Box(3, 1, 4, 2), 1f, 0, target), "stops touching the face");
            Assert.IsTrue(Box.SweptOverlaps(new Box(3, 1, 4, 2), 1.01f, 0, target), "just past the face");
            Assert.IsTrue(Box.SweptOverlaps(new Box(4.9f, 1, 5.1f, 2), 0, 0, target), "a still overlap");
            Assert.IsTrue(Box.SweptOverlaps(new Box(3, 4, 4, 5), 2f, -3f, target), "diagonal through");
        }
    }
}
