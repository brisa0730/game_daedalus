using System.Collections;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The troll tour: each first-lesson trap played through the real Player, with a picture of each.</summary>
    public class TrapPlayTests : MovementPlayTest
    {
        const float Hw = MovementInternals.HitboxHalfWidth;

        // A floor with a 3-wide gap (columns 6–8) that a Drop Floor fills; the pit under it is the map bottom.
        static readonly string[] DropFloorRun =
        {
            "......................",
            "......................",
            "......................",
            ".P....................",
            "######...#############",
        };

        static readonly string[] Floor =
        {
            "......................",
            "......................",
            "......................",
            "......................",
            ".P....................",
            "######################",
        };

        IEnumerator Until(System.Func<bool> done, int limit, string what)
        {
            for (int i = 0; i < limit && !done(); i++)
            {
                yield return null;
            }
            Assert.IsTrue(done(), what);
        }

        [UnityTest]
        public IEnumerator ADropFloorLooksLikeTheGroundThenDropsThePlayerAndComesBack()
        {
            Build(DropFloorRun);
            Trap trap = Level.AddTrap(6, 0, 3, 1, TrapAction.Vanish);
            yield return null;
            PlayerSession session = Level.Player.Session;

            // The look matches the painted ground beside it (a seamless tile), on the grid.
            Level.TrapShot("dropfloor-start");
            for (int x = 4; x < 11; x++)
            {
                Color32 pixel = Level.PixelAt(x + 0.5f, 0.5f);
                Assert.IsTrue(TestImages.Matches(pixel, TestLevel.SolidColor), $"cell {x} looks like the ground: {pixel}");
            }

            Keys(Key.RightArrow);
            yield return Until(() => session.Phase == LevelPhase.Dying, 300, "fell into the pit and died");
            Keys();
            Assert.IsFalse(session.IsTrapPresent(0), "the floor dropped");
            yield return Until(() => session.PhaseTicks <= LevelRules.DeathBeatTicks / 2 - 2, 30, "mid-beat");
            Assert.IsFalse(Level.PlayerSprite.enabled, "the player is gone in the second half of the beat");
            Level.TrapShot("burst");

            yield return Until(() => session.Phase == LevelPhase.Playing, 30, "respawned");
            yield return Frames(10);
            Assert.That(session.Current.X + Hw, Is.LessThanOrEqualTo(6f + 1e-4f), "back on safe ground before the trap");
            Assert.IsTrue(session.IsTrapPresent(0), "the floor is back");
            Assert.IsTrue(trap.GetComponent<SpriteRenderer>().enabled, "and drawn");
            Assert.IsTrue(Level.PlayerSprite.enabled, "the player is drawn again");
            Level.TrapShot("dropfloor");
            Assert.IsTrue(TestImages.Matches(Level.PixelAt(7.5f, 0.5f), TestLevel.SolidColor), "the floor is drawn again");
        }

        [UnityTest]
        public IEnumerator APopSpikeKillsAWalkerAndSparesAJumper()
        {
            Build(Floor);
            Level.AddTrap(8, 1, 2, 1, TrapAction.Appear, hazard: true);
            yield return null;
            PlayerSession session = Level.Player.Session;
            Assert.IsTrue(TestImages.Matches(ShotPixel("popspike-start", 8.5f, 1.5f), TestLevel.BackgroundColor), "hidden at first");

            Keys(Key.RightArrow);
            yield return Until(() => session.Deaths == 1, 300, "walked in and died");
            Keys();
            yield return Until(() => session.Phase == LevelPhase.Playing, 40, "respawned");
            Assert.IsFalse(session.IsTrapPresent(0), "reset");

            // Back off, then run and jump high over it.
            Keys(Key.LeftArrow);
            yield return Until(() => session.Current.X < 4f, 120, "backed off");
            Keys(Key.RightArrow);
            yield return Until(() => session.Current.X >= 6.6f, 120, "ran up");
            Keys(Key.RightArrow, Key.Space);
            yield return Until(() => session.Current.X > 11f && session.Current.IsStanding, 120, "landed past it");
            Keys();
            Assert.IsTrue(session.IsTrapPresent(0), "it fired under the jump");
            Assert.AreEqual(1, session.Deaths, "survived");
            Level.TrapShot("popspike");
            Assert.IsTrue(TestImages.Matches(Level.PixelAt(8.5f, 1.5f), TestLevel.HazardColor), "the spikes are drawn on the floor");
        }

        [UnityTest]
        public IEnumerator AHiddenBlockAppearsWhenTheHeadReachesItAndBonksThePlayer()
        {
            Build(Floor);
            Level.AddTrap(1, 4, 1, 1, TrapAction.Appear, trigger: TrapTrigger.Touch);
            yield return Frames(5);
            PlayerSession session = Level.Player.Session;
            float highest = 0;
            Keys(Key.Space);
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                highest = Mathf.Max(highest, session.Current.Y + MovementInternals.HitboxHeight);
            }
            Keys();
            yield return Frames(30);
            Assert.IsTrue(session.IsTrapPresent(0), "the block appeared");
            Assert.That(highest, Is.LessThanOrEqualTo(4f + 1e-3f), "the head stopped under the block");
            Assert.IsTrue(session.Current.IsStanding, "landed below");
            Assert.AreEqual(1f, session.Current.Y, 1e-4f);
            Level.TrapShot("hiddenblock");
            Assert.IsTrue(TestImages.Matches(Level.PixelAt(1.5f, 4.5f), TestLevel.SolidColor), "the block is drawn");
        }

        [UnityTest]
        public IEnumerator ACheckpointTouchedBeforeAPitIsWhereThePlayerComesBack()
        {
            Build(new[]
            {
                "......................",
                "......................",
                "......................",
                ".P....................",
                "############...#######",
            }, new TestLevelOptions { BeforeActivate = level => level.Player.Health.OnHazard = OnHazard.RespawnAtCheckpoint });
            Checkpoint checkpoint = Level.AddCheckpoint(6, 1);
            yield return null;
            PlayerSession session = Level.Player.Session;
            Keys(Key.RightArrow);
            yield return Until(() => session.Deaths == 1, 300, "touched the checkpoint and died in the pit");
            Keys();
            yield return Until(() => session.Phase == LevelPhase.Playing, 40, "respawned");
            yield return Frames(5);
            Assert.AreEqual(0, session.ActiveCheckpoint);
            Assert.AreEqual(6.5f, session.Current.X, 1e-4f, "at the checkpoint");
            Assert.AreEqual((Color)Checkpoint.ReachedTint, checkpoint.GetComponent<SpriteRenderer>().color, "tinted while active");
            Level.TrapShot("checkpoint");
        }

        Color32 ShotPixel(string name, float x, float y)
        {
            Level.TrapShot(name);
            return Level.PixelAt(x, y);
        }
    }
}
