using System.Collections;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>What the student sees: feet on the floor, smooth interpolation, a clean reset cut and facing.</summary>
    public class PlayerRenderTests : MovementPlayTest
    {
        const float FloorTop = 1f;

        static readonly string[] Floor =
        {
            "................",
            "................",
            "................",
            "................",
            "................",
            "......P.........",
            "................",
            "................",
            "................",
            "################",
        };

        static readonly string[] Pit =
        {
            "................",
            "................",
            "..P.............",
            "######.......###",
        };

        IEnumerator FeetOnTheFloor(TestLevelOptions options, string what)
        {
            Build(Floor, options);
            yield return Frames(60);
            Assert.AreEqual(PlayerContact.Ground, Level.Player.Session.Current.Contact, what + ": precondition, landed");
            Assert.AreEqual(FloorTop, Level.PlayerSprite.bounds.min.y, 1e-4f, what);
        }

        [UnityTest]
        public IEnumerator TheSpriteBottomIsOnTheFloorForACentrePivot()
        {
            yield return FeetOnTheFloor(new TestLevelOptions(), "centre pivot");
        }

        [UnityTest]
        public IEnumerator TheSpriteBottomIsOnTheFloorForABottomPivot()
        {
            yield return FeetOnTheFloor(new TestLevelOptions { SpritePivot = new Vector2(0.5f, 0f) }, "bottom pivot");
        }

        [UnityTest]
        public IEnumerator TheSpriteBottomIsOnTheFloorAtScaleTwo()
        {
            yield return FeetOnTheFloor(new TestLevelOptions { PlayerScale = 2f }, "centre pivot, scale 2");
        }

        [UnityTest]
        public IEnumerator NoPixelGapAndNoSinkBetweenTheFeetAndTheFloor()
        {
            yield return FeetOnTheFloor(new TestLevelOptions(), "centre pivot");
            string path = Level.Shot("feet");
            float x = Level.Player.transform.position.x;
            const float HalfPixel = 0.5f / TestImages.PixelsPerTile;
            for (float dx = -0.3f; dx <= 0.3f; dx += 0.1f)
            {
                Color32 above = Level.PixelAt(x + dx, FloorTop + HalfPixel);
                Color32 below = Level.PixelAt(x + dx, FloorTop - HalfPixel);
                Assert.IsTrue(TestImages.Matches(above, TestLevel.PlayerColor), $"the row above the floor at dx {dx:0.0} is {above} ({path})");
                Assert.IsTrue(TestImages.Matches(below, TestLevel.SolidColor), $"the row below the floor top at dx {dx:0.0} is {below} ({path})");
            }
        }

        [UnityTest]
        public IEnumerator AtNinetyFramesPerSecondTheSpriteIsDrawnBetweenTicks()
        {
            Time.captureFramerate = 90;
            Build(Floor);
            yield return null;
            int framesBetween = 0;
            for (int frame = 0; frame < 30; frame++)
            {
                yield return null;
                PlayerSession session = Level.Player.Session;
                double t = session.InterpolationFraction;
                float previous = session.Map.SimToWorld(new Vector2(0, session.Previous.Y)).y;
                float current = session.Map.SimToWorld(new Vector2(0, session.Current.Y)).y;
                float drawn = Level.PlayerSprite.bounds.min.y;
                if (t > 1e-3 && t < 1 - 1e-3 && current < previous)
                {
                    framesBetween++;
                    Assert.That(drawn, Is.LessThan(previous).And.GreaterThan(current), $"frame {frame}, fraction {t:0.000}");
                }
            }
            Assert.That(framesBetween, Is.GreaterThan(5), "frames drawn between two falling ticks");
        }

        [UnityTest]
        public IEnumerator TheResetCutsStraightFromBelowTheMapToTheStart()
        {
            Build(Pit);
            Press(Keyboard.rightArrowKey);
            float lastY = float.NaN;
            bool reset = false;
            for (int frame = 0; frame < 300 && !reset; frame++)
            {
                yield return null;
                float y = Level.PlayerSprite.bounds.min.y;
                if (Level.Player.Session.Current.Teleported)
                {
                    reset = true;
                    Assert.That(lastY, Is.LessThan(0f), "the frame before was below the map");
                    Assert.AreEqual(Level.Start.y, y, 1e-4f, "this frame is exactly at the start");
                    Assert.AreEqual(Level.Start.x, Level.Player.transform.position.x, 1e-4f);
                }
                lastY = y;
            }
            Assert.IsTrue(reset, "walked into the pit and fell out");
        }

        [UnityTest]
        public IEnumerator AnAuthoredFlipXIsKeptAndFacingLeftReversesIt()
        {
            // A student's left-facing art, flipped to face right in the scene.
            Build(Floor, new TestLevelOptions { BeforeActivate = level => level.PlayerSprite.flipX = true });
            yield return Frames(60);
            Assert.IsTrue(Level.PlayerSprite.flipX, "facing right keeps the authored Flip X");
            Keys(Key.LeftArrow);
            yield return Frames(5);
            Assert.IsFalse(Level.PlayerSprite.flipX, "facing left reverses it");
        }

        [UnityTest]
        public IEnumerator WalkingLeftFlipsTheSpriteAndRightUnflipsIt()
        {
            Build(Floor);
            yield return Frames(60);
            Assert.IsFalse(Level.PlayerSprite.flipX, "precondition: facing right");
            Keys(Key.LeftArrow);
            yield return Frames(5);
            Assert.IsTrue(Level.PlayerSprite.flipX, "walking left");
            Keys(Key.RightArrow);
            yield return Frames(5);
            Assert.IsFalse(Level.PlayerSprite.flipX, "walking right");
        }
    }
}
