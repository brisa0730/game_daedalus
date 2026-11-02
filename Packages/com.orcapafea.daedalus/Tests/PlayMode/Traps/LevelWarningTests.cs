using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Each trap and level problem logs its plain-words warning exactly once at play start, with the object to fix.</summary>
    public class LevelWarningTests : MovementPlayTest
    {
        static readonly string[] Floor =
        {
            "....................",
            "....................",
            ".P..................",
            "####################",
        };

        WarningLog log;

        [SetUp]
        public void Listen() => log = new WarningLog();

        [TearDown]
        public void StopListening() => log.Dispose();

        void AssertOnly(string text, Object context)
        {
            Assert.AreEqual(1, log.Count(text), "logged exactly once: " + text);
            Assert.AreEqual(1, log.Total, "and nothing else: " + string.Join(" | ", log.Texts));
            Assert.AreSame(context, log.ContextOf(text), "the context");
        }

        [UnityTest]
        public IEnumerator ACleanTrapLevelLogsNothing()
        {
            Build(Floor);
            Level.AddTrap(6, 1, 2, 1, TrapAction.Appear);
            Level.AddTrap(10, 1, 2, 1, TrapAction.Appear, hazard: true);
            Level.AddCheckpoint(14, 1);
            Level.AddExit(18, 1);
            yield return Frames(10);
            Assert.AreEqual(2, Level.Player.Session.TrapCount, "precondition: both traps were read");
            Assert.AreEqual(0, log.Total, string.Join(" | ", log.Texts));
        }

        [UnityTest]
        public IEnumerator ATrapOverPaintedTilesWarns()
        {
            Build(Floor);
            Trap trap = Level.AddTrap(6, 0, 3, 1, TrapAction.Vanish);
            yield return Frames(2);
            AssertOnly(PlayerWarnings.TrapOverPaintedTiles, trap);
        }

        [UnityTest]
        public IEnumerator AnOffGridTrapWarns()
        {
            Build(Floor);
            Trap trap = Level.AddTrap(6, 2, 3, 1, TrapAction.Vanish);
            trap.transform.position += new Vector3(0.3f, 0, 0);
            yield return Frames(2);
            AssertOnly(PlayerWarnings.TrapOffGrid, trap);
        }

        [UnityTest]
        public IEnumerator ATrapWithNoTileWarns()
        {
            Build(Floor);
            Trap trap = Level.AddTrap(6, 2, 3, 1, TrapAction.Vanish);
            trap.Tile = null;
            yield return Frames(2);
            AssertOnly(PlayerWarnings.TrapNoTile, trap);
        }

        [UnityTest]
        public IEnumerator ATrapWithATileThatCantBeDrawnWarns()
        {
            Build(Floor);
            Trap trap = Level.AddTrap(6, 2, 3, 1, TrapAction.Vanish);
            var tile = ScriptableObject.CreateInstance<NotAPlainTile>();
            Level.Created.Add(tile);
            trap.Tile = tile;
            yield return Frames(2);
            AssertOnly(PlayerWarnings.TrapTileNotDrawable, trap);
        }

        [UnityTest]
        public IEnumerator AScaledTrapWarns()
        {
            Build(Floor);
            Trap trap = Level.AddTrap(6, 2, 3, 1, TrapAction.Vanish);
            trap.transform.localScale = new Vector3(2, 1, 1);
            yield return Frames(2);
            AssertOnly(PlayerWarnings.TrapScaled, trap);
        }

        [UnityTest]
        public IEnumerator ATrapOnTheExitWarnsAndDoesNothing()
        {
            Build(Floor);
            Exit exit = Level.AddExit(10, 1);
            var renderer = exit.GetComponent<SpriteRenderer>();
            Sprite exitSprite = renderer.sprite;
            Trap trap = exit.gameObject.AddComponent<Trap>();
            trap.Tile = Level.SolidTile;
            yield return Frames(2);
            AssertOnly(PlayerWarnings.TrapOnExitOrCheckpoint, trap);
            Assert.AreEqual(0, Level.Player.Session.TrapCount, "no trap cells");
            Assert.AreSame(exitSprite, renderer.sprite, "the Exit's sprite is untouched");
            Assert.AreEqual(SpriteDrawMode.Simple, renderer.drawMode);
        }

        [UnityTest]
        public IEnumerator HazardTilesOnTheBackgroundWarnButOnCollisionDoNot()
        {
            Build(new[]
            {
                "....................",
                "....................",
                ".P..........^.......",
                "####################",
            });
            Level.Background.SetTile(new Vector3Int(15, 1, 0), Level.HazardTile);
            yield return Frames(2);
            AssertOnly(PlayerWarnings.HazardOnDecorationTilemap, Level.Background);
        }
    }
}
