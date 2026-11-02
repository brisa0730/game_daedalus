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

        // --- Moving traps (Phase 4). ---

        // A ceiling 9 tiles up (bottom at 10) with its zone drawn under it: a runner clears it, a stander is crushed.
        static readonly string[] TallFloor =
        {
            "......................",
            "......................",
            "......................",
            "......................",
            "......................",
            "......................",
            "......................",
            "......................",
            "......................",
            "......................",
            "......................",
            ".P....................",
            "######################",
        };

        Trap FallingCeiling()
        {
            // The trap at cells (8, 10)–(11, 11); its position is (9.5, 10.5), so the zone under it is local (−1.5, −9.5)–(1.5, −0.5).
            return Level.AddTrap(8, 10, 3, 1, TrapAction.Fall, zone: Rect.MinMaxRect(-1.5f, -9.5f, 1.5f, -0.5f));
        }

        [UnityTest]
        public IEnumerator AFallingCeilingSparesARunnerAndCrushesAStander()
        {
            Build(TallFloor);
            FallingCeiling();
            yield return null;
            PlayerSession session = Level.Player.Session;
            Keys(Key.RightArrow);
            yield return Until(() => session.TrapCount == 1 && session.Current.X > 8.5f, 200, "ran under it");
            yield return Until(() => session.InterpolatedTrapOffset(0).y < -3f, 60, "it is falling");
            Level.TrapShot("fallingceiling");
            yield return Until(() => session.Current.X > 14f, 120, "ran clear");
            Keys();
            yield return Frames(60);
            Assert.AreEqual(0, session.Deaths, "the runner survived");
            Assert.AreEqual(-9f, session.InterpolatedTrapOffset(0).y, 1e-4f, "the ceiling landed on the floor");

            Level.Dispose();
            Build(TallFloor);
            FallingCeiling();
            yield return null;
            session = Level.Player.Session;
            Keys(Key.RightArrow);
            yield return Until(() => session.Current.X > 9f, 200, "walked under it");
            Keys();
            yield return Until(() => session.Deaths == 1, 120, "the stander is crushed");
        }

        // A pit (columns 6–14) a 3-wide sliding floor crosses, 6 tiles at speed 4.
        static readonly string[] PitRun =
        {
            "......................",
            "......................",
            "......................",
            ".P....................",
            "######.........#######",
        };

        [UnityTest]
        public IEnumerator ARiderIsDrawnExactlyOnASlidingFloorAtEveryFrame()
        {
            Build(PitRun);
            Trap trap = Level.AddTrap(6, 0, 3, 1, TrapAction.Move, offset: new Vector2Int(6, 0), speed: 4f);
            yield return null;
            Player player = Level.Player;
            PlayerSession session = player.Session;
            var trapRenderer = trap.GetComponent<SpriteRenderer>();
            Keys(Key.RightArrow);
            yield return Until(() => session.Current.X > 7.5f, 120, "stepped onto the floor");
            Keys();
            yield return Until(() => session.Current.IsStanding && session.Current.VelocityX == 0, 30, "standing still on it");
            // One more tick, so the last two ticks hold no step of the player's own (only the carry).
            yield return Frames(2);

            // Drive the frames by hand at 90 Hz, so most frames fall between ticks.
            Time.timeScale = 0;
            InputSystem.Update();
            float gap = float.NaN;
            int frames = 0;
            bool shot = false;
            for (int i = 0; i < 400 && (frames == 0 || session.InterpolatedTrapOffset(0).x < 6f); i++)
            {
                player.RunFrame(1f / 90f);
                Bounds feet = Level.PlayerSprite.bounds;
                Bounds floor = trapRenderer.bounds;
                Assert.AreEqual(floor.max.y, feet.min.y, 0.001f, $"frame {i}: the drawn feet on the drawn top");
                float now = feet.center.x - floor.center.x;
                if (!float.IsNaN(gap))
                {
                    Assert.AreEqual(gap, now, 0.001f, $"frame {i}: the rider moves with the floor (no jitter)");
                }
                gap = now;
                frames++;
                if (!shot && session.InterpolatedTrapOffset(0).x > 3f)
                {
                    Level.TrapShot("ride");
                    shot = true;
                }
            }
            Assert.That(frames, Is.GreaterThan(90), "precondition: rode most of the way (60+ ticks at 90 Hz)");
            Assert.AreEqual(6f, session.InterpolatedTrapOffset(0).x, 1e-4f, "at its end");
            Assert.AreEqual(0, session.Deaths);
        }

        [UnityTest]
        public IEnumerator ACrusherCrushesThePlayerAgainstAWall()
        {
            Build(new[]
            {
                "......................",
                "......................",
                "......................",
                "..#..P................",
                "######################",
            });
            // A 2 x 3 block at column 9 slides 7 left; its zone is a column left of the player.
            Level.AddTrap(9, 1, 2, 3, TrapAction.Move, zone: Rect.MinMaxRect(-6.5f, -1.5f, -5.5f, 0.5f), offset: new Vector2Int(-7, 0));
            yield return null;
            PlayerSession session = Level.Player.Session;
            Keys(Key.LeftArrow);
            yield return Until(() => session.TrapCount == 1 && session.Current.X < 4.3f, 60, "stepped into the zone");
            Keys();
            yield return Until(() => session.Deaths == 1, 120, "crushed against the wall");
            yield return Frames(4);
            Level.TrapShot("crush");
        }

        // A floor with a 3-wide gap (columns 6–8) a Fall floor fills.
        static readonly string[] FallFloorRun =
        {
            "......................",
            "......................",
            "......................",
            ".P....................",
            "######...#############",
        };

        [UnityTest]
        public IEnumerator AFallFloorDropsAndThePlayerJumpsOffItOntoTheLedge()
        {
            Build(FallFloorRun);
            Level.AddTrap(6, 0, 3, 1, TrapAction.Fall, delay: 0.3f);
            yield return null;
            PlayerSession session = Level.Player.Session;
            Keys(Key.RightArrow);
            yield return Until(() => session.Current.X > 7.3f, 120, "on the floor");
            Keys();
            yield return Until(() => session.InterpolatedTrapOffset(0).y < -0.3f, 60, "it is falling with the player on it");
            Assert.IsTrue(session.Current.IsStanding, "riding it down");
            Level.TrapShot("fallfloor");
            Keys(Key.RightArrow, Key.Space);
            yield return Until(() => session.Current.IsStanding && session.Current.X > 9.5f, 120, "landed on the ledge");
            Keys();
            Assert.AreEqual(1f, session.Current.Y, 1e-4f);
            Assert.AreEqual(0, session.Deaths);
        }

        [UnityTest]
        public IEnumerator AFastSpikeMoveKillsOnContact()
        {
            Build(Floor);
            Level.AddTrap(20, 1, 1, 1, TrapAction.Move, hazard: true, zone: Rect.MinMaxRect(-17.5f, -0.5f, -16.5f, 1.5f),
                offset: new Vector2Int(-18, 0), speed: 30f);
            yield return null;
            PlayerSession session = Level.Player.Session;
            Keys(Key.RightArrow);
            yield return Until(() => session.Deaths == 1, 200, "the spike killed the player");
            Keys();
            Assert.That(session.InterpolatedTrapOffset(0).x, Is.LessThan(0f), "it was moving");
            yield return Frames(4);
            Level.TrapShot("fasthazard");
        }

        [UnityTest]
        public IEnumerator ARunawayExitRunsAndIsWonAtItsEnd()
        {
            Build(Floor);
            Exit exit = Level.AddExit(10, 1);
            Level.AddExitTrap(exit, TrapAction.Move, zone: Rect.MinMaxRect(-4.5f, -0.5f, -3.5f, 1.5f), offset: new Vector2Int(8, 0));
            yield return null;
            PlayerSession session = Level.Player.Session;
            float startX = exit.transform.position.x;
            Keys(Key.RightArrow);
            yield return Until(() => session.Phase == LevelPhase.Won, 400, "caught it");
            Keys();
            Assert.AreEqual(8f, session.InterpolatedTrapOffset(0).x, 1e-4f, "won at its end");
            yield return null;
            Assert.AreEqual(startX + 8f, exit.transform.position.x, 1e-4f, "drawn at its end");
            Level.TrapShot("runaway");
        }

        [UnityTest]
        public IEnumerator ATrapResetByADeathIsDrawnAtItsStartOnTheRespawnFrame()
        {
            Build(PitRun);
            // A slow floor (1 tile/s) that starts as the player walks past; the player walks on into the pit.
            Trap trap = Level.AddTrap(15, 3, 2, 1, TrapAction.Move, zone: Rect.MinMaxRect(-13f, -3f, -12f, -1f), offset: new Vector2Int(0, -2), speed: 1f);
            yield return null;
            PlayerSession session = Level.Player.Session;
            Vector3 start = trap.transform.position;
            Keys(Key.RightArrow);
            yield return Until(() => session.Phase == LevelPhase.Dying, 300, "fell into the pit");
            Keys();
            Assert.That(trap.transform.position.y, Is.LessThan(start.y - 0.1f), "precondition: killed mid-move");
            yield return Until(() => session.Phase == LevelPhase.Playing, 40, "respawned");
            Assert.AreEqual(start, trap.transform.position, "drawn at its start on the respawn frame: no in-between frame");
        }

        Color32 ShotPixel(string name, float x, float y)
        {
            Level.TrapShot(name);
            return Level.PixelAt(x, y);
        }
    }
}
