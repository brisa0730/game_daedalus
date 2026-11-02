using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Each play-start problem logs its plain-words warning exactly once, with the right object as context, and
    /// nothing is removed. The "old input setting" warning can't be produced here (both test projects run the
    /// Input System): it is covered by code review only.
    /// </summary>
    public class PlayerWarningTests : MovementPlayTest
    {
        const int Frames120 = 120;

        static readonly string[] Floor =
        {
            "............",
            "............",
            "....P.......",
            "............",
            "############",
        };

        static readonly string[] Empty =
        {
            "............",
            "....P.......",
            "............",
        };

        static readonly string[] RopeUnderACeiling =
        {
            "############",
            "......R.....",
            "......R.....",
            "......R.....",
            "..P...R.....",
            "############",
        };

        static readonly string[] RopeUnderASolidAboveAOneWay =
        {
            "............",
            "......#.....",
            ".....-+-....",
            "......R.....",
            "......R.....",
            "..P...R.....",
            "############",
        };

        static readonly string[] RopeThroughAOneWay =
        {
            "............",
            ".....-+-....",
            "......R.....",
            "......R.....",
            "..P...R.....",
            "############",
        };

        WarningLog log;

        [SetUp]
        public void Listen() => log = new WarningLog();

        [TearDown]
        public void StopListening() => log.Dispose();

        IEnumerator Play(string[] rows, TestLevelOptions options = null)
        {
            Build(rows, options);
            yield return Frames(Frames120);
        }

        void AssertOnly(string text, Object context = null)
        {
            Assert.AreEqual(1, log.Count(text), "logged exactly once: " + text);
            Assert.AreEqual(1, log.Total, "and nothing else: " + string.Join(" | ", log.Texts));
            if (context != null)
            {
                Assert.AreSame(context, log.ContextOf(text), "the context");
            }
        }

        [UnityTest]
        public IEnumerator ACleanLevelLogsNoWarning()
        {
            yield return Play(Floor);
            Assert.AreEqual(PlayerContact.Ground, Level.Player.Session.Current.Contact, "precondition: played");
            Assert.AreEqual(0, log.Total, string.Join(" | ", log.Texts));
        }

        [UnityTest]
        public IEnumerator AnEmptyFieldWithATilemapNamedCollisionLandsSilently()
        {
            yield return Play(Floor, new TestLevelOptions { AssignTilemap = false });
            Assert.IsNull(Level.Player.CollisionTilemap, "the field stays empty (nothing is written)");
            Assert.AreEqual(PlayerContact.Ground, Level.Player.Session.Current.Contact, "landed on the picked Tilemap");
            Assert.AreEqual(0, log.Total, string.Join(" | ", log.Texts));
        }

        [UnityTest]
        public IEnumerator NoTilemapAtAllWarnsOnceAndThePlayerFallsAndResets()
        {
            yield return PlayWithoutACollisionTilemap(new TestLevelOptions { WithCollisionTilemap = false, WithBackground = false, AssignTilemap = false });
        }

        [UnityTest]
        public IEnumerator TwoTilemapsNeitherNamedCollisionWarnOnce()
        {
            yield return PlayWithoutACollisionTilemap(new TestLevelOptions { CollisionName = "Ground", AssignTilemap = false });
        }

        IEnumerator PlayWithoutACollisionTilemap(TestLevelOptions options)
        {
            Build(Empty, options);
            int resets = 0;
            for (int i = 0; i < Frames120; i++)
            {
                yield return null;
                if (Level.Player.Session.Current.Teleported)
                {
                    resets++;
                }
            }
            Assert.That(resets, Is.GreaterThan(0), "the player fell out and reset");
            AssertOnly(PlayerWarnings.NoCollisionTilemap, Level.Player);
        }

        [UnityTest]
        public IEnumerator ARigidbodyAndAColliderWarnAndAreKept()
        {
            yield return Play(Floor, new TestLevelOptions
            {
                BeforeActivate = level =>
                {
                    level.Player.gameObject.AddComponent<Rigidbody2D>();
                    level.Player.gameObject.AddComponent<BoxCollider2D>();
                },
            });
            Assert.IsNotNull(Level.Player.GetComponent<Rigidbody2D>(), "the Rigidbody 2D is kept");
            Assert.IsNotNull(Level.Player.GetComponent<BoxCollider2D>(), "the Box Collider 2D is kept");
            Assert.AreEqual(PlayerContact.Ground, Level.Player.Session.Current.Contact, "still lands");
            AssertOnly(PlayerWarnings.HasPhysics, Level.Player);
        }

        [UnityTest]
        public IEnumerator AStartInsideAWallWarnsItWasMoved()
        {
            yield return Play(Floor, new TestLevelOptions { BeforeActivate = level => PlaceFeet(level, 4.5f, 0.3f) });
            Assert.IsTrue(Level.Player.Session.LastPlacement.Moved, "precondition");
            AssertOnly(PlayerWarnings.MovedStart, Level.Player);
        }

        [UnityTest]
        public IEnumerator ABuriedStartWarnsThereIsNoFreeSpot()
        {
            string[] buried = new string[20];
            for (int i = 0; i < buried.Length; i++)
            {
                buried[i] = i == 0 ? "P..................." : "####################";
            }
            yield return Play(buried, new TestLevelOptions { BeforeActivate = level => PlaceFeet(level, 10.5f, 8f) });
            Assert.IsFalse(Level.Player.Session.LastPlacement.Found, "precondition");
            AssertOnly(PlayerWarnings.NoFreeStart, Level.Player);
        }

        [UnityTest]
        public IEnumerator AHalfSizeGridCellWarns()
        {
            yield return Play(Floor, new TestLevelOptions { BeforeActivate = level => level.Grid.cellSize = new Vector3(0.5f, 0.5f, 0) });
            AssertOnly(PlayerWarnings.CellSize(new Vector3(0.5f, 0.5f, 0)), Level.Player);
        }

        [UnityTest]
        public IEnumerator AScaledGridWarns()
        {
            yield return Play(Floor, new TestLevelOptions { BeforeActivate = level => level.Grid.transform.localScale = new Vector3(2, 2, 1) });
            AssertOnly(PlayerWarnings.RotatedOrScaled, Level.Player);
        }

        [UnityTest]
        public IEnumerator AHexagonGridWarns()
        {
            yield return Play(Floor, new TestLevelOptions { BeforeActivate = level => level.Grid.cellLayout = GridLayout.CellLayout.Hexagon });
            AssertOnly(PlayerWarnings.CellLayout, Level.Player);
        }

        [UnityTest]
        public IEnumerator AnEmptyCollisionTilemapWarns()
        {
            yield return Play(Empty);
            AssertOnly(PlayerWarnings.EmptyTilemap, Level.Player);
        }

        [UnityTest]
        public IEnumerator NoSpriteWarns()
        {
            yield return Play(Floor, new TestLevelOptions { WithSprite = false });
            AssertOnly(PlayerWarnings.NoSprite, Level.Player);
        }

        [UnityTest]
        public IEnumerator ARopeTopInsideACeilingTileWarnsWithTheRopeAsContext()
        {
            // The rope's top (y 5) is the ceiling's bottom face; lift it half a tile into the ceiling.
            yield return Play(RopeUnderACeiling, new TestLevelOptions
            {
                Abilities = Abilities.Jump | Abilities.RopeClimb,
                BeforeActivate = level => level.Ropes[0].transform.position += new Vector3(0, 0.5f, 0),
            });
            AssertOnly(PlayerWarnings.RopeTopInWall, Level.Ropes[0]);
        }

        [UnityTest]
        public IEnumerator ARopeTopUnderACeilingFaceDoesNotWarn()
        {
            yield return Play(RopeUnderACeiling, new TestLevelOptions { Abilities = Abilities.Jump | Abilities.RopeClimb });
            Assert.AreEqual(0, log.Total, string.Join(" | ", log.Texts));
        }

        [UnityTest]
        public IEnumerator ARopeTopOnOrSnappingOntoAOneWayDoesNotWarn()
        {
            yield return Play(RopeThroughAOneWay, new TestLevelOptions { Abilities = Abilities.Jump | Abilities.RopeClimb });
            Assert.AreEqual(0, log.Total, "on the one-way: " + string.Join(" | ", log.Texts));

            Level.Dispose();
            yield return Play(RopeThroughAOneWay, new TestLevelOptions
            {
                Abilities = Abilities.Jump | Abilities.RopeClimb,
                BeforeActivate = level => level.Ropes[0].transform.position += new Vector3(0, -0.3f, 0),
            });
            Assert.AreEqual(0, log.Total, "snapping onto the one-way: " + string.Join(" | ", log.Texts));
        }

        [UnityTest]
        public IEnumerator ARopeTopInTheSolidJustAboveAOneWaySnapsDownAndDoesNotWarn()
        {
            // 0.3 above the one-way surface is inside the solid cell above it; only the snap keeps it quiet.
            yield return Play(RopeUnderASolidAboveAOneWay, new TestLevelOptions
            {
                Abilities = Abilities.Jump | Abilities.RopeClimb,
                BeforeActivate = level => level.Ropes[0].transform.position += new Vector3(0, 0.3f, 0),
            });
            Assert.AreEqual(0, log.Total, string.Join(" | ", log.Texts));
        }

        static void PlaceFeet(TestLevel level, float x, float y)
        {
            float offset = level.PlayerSprite.sprite.bounds.min.y;
            level.Player.transform.position = new Vector3(x, y - offset, 0);
        }
    }
}
