using System.Collections;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.TestTools;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The Player component driving the session: play start, keyboard play, restarts and live settings.</summary>
    public class PlayerPlayTests : MovementPlayTest
    {
        const float FloorTop = 1f;

        static readonly string[] Floor =
        {
            "....................",
            "....................",
            "....................",
            "....................",
            "....................",
            "....P...............",
            "....................",
            "....................",
            "....................",
            "####################",
        };

        static readonly string[] RopeLevel =
        {
            "....................",
            "........R...........",
            "........R...........",
            "........R...........",
            "........R...........",
            "........R...........",
            "........R...........",
            "........R...........",
            "...P....R...........",
            "####################",
        };

        IEnumerator Land()
        {
            yield return Frames(60);
            Assert.AreEqual(PlayerContact.Ground, Level.Player.Session.Current.Contact, "precondition: landed");
        }

        [UnityTest]
        public IEnumerator SpawnedAboveAPlainFloorTheFeetLandOnIt()
        {
            Build(Floor);
            Assert.AreEqual(3f, Level.Start.y - FloorTop, "precondition: spawned 3 tiles above the floor");
            yield return Frames(60);
            Assert.AreEqual(PlayerContact.Ground, Level.Player.Session.Current.Contact);
            Assert.AreEqual(FloorTop, SimFeetWorld(Level.Player).y, 1e-4f);
        }

        [UnityTest]
        public IEnumerator HoldingRightRunsTheExpectedDistance()
        {
            Build(Floor);
            yield return Land();
            float x0 = SimFeetWorld(Level.Player).x;

            // The same 30 ticks through a fresh stepper on the same map.
            var stepper = new PlayerStepper(Level.Player.Session.Map.Grid, new RopeSegment[0], MovementSettings.Default);
            PlayerState s = Level.Player.Session.Current;
            for (int i = 0; i < 30; i++)
            {
                s = stepper.Step(s, new PlayerInput(1, 0, false, false));
            }
            float expected = s.X - Level.Player.Session.Current.X;
            Assert.That(expected, Is.GreaterThan(1f), "precondition: the reference run moved");

            Press(Keyboard.rightArrowKey);
            yield return Frames(30);
            float moved = SimFeetWorld(Level.Player).x - x0;
            Assert.AreEqual(expected, moved, MovementSettings.DefaultRunSpeed / FixedStepAccumulator.TicksPerSecond + 1e-4f, "± one tick");
        }

        [UnityTest]
        public IEnumerator HoldingSpaceJumpsTheJumpHeight()
        {
            Build(Floor);
            yield return Land();
            Press(Keyboard.spaceKey);
            float apex = 0f;
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                apex = Mathf.Max(apex, SimFeetWorld(Level.Player).y);
            }
            Assert.AreEqual(MovementSettings.DefaultJumpHeight, apex - FloorTop, 0.05f);
        }

        [UnityTest]
        public IEnumerator RestartBeforeStartIsTheFirstStart()
        {
            var start = new Vector2(12.5f, 4f);
            Build(Floor);
            Level.Player.Restart(new PlayStart(start, Abilities.Jump));
            Assert.IsNull(Level.Player.Session, "precondition: Start hasn't run");
            yield return null;
            Vector2 first = Level.Player.Session.StartFeetWorld;
            Assert.AreEqual(start, first);
        }

        [UnityTest]
        public IEnumerator RestartAfterPlayMovesStateAndSpriteOnTheSameFrame()
        {
            Build(Floor);
            yield return Frames(30);
            var start = new Vector2(15.5f, 5f);
            Level.Player.Restart(new PlayStart(start, Abilities.Jump));
            Assert.AreEqual(start, SimFeetWorld(Level.Player));
            Assert.AreEqual(start.y, Level.PlayerSprite.bounds.min.y, 1e-4f);
            Assert.AreEqual(start.x, Level.Player.transform.position.x, 1e-4f);
        }

        [UnityTest]
        public IEnumerator AnErrorAtPlayStartStaysTheOnlyError()
        {
            Build(new[]
            {
                "....................",
                "....P...............",
                "....................",
                "#########-----######",
            }, new TestLevelOptions
            {
                // A tile kind this package doesn't know (e.g. saved by a newer version): a broken invariant.
                BeforeActivate = level => ((DaedalusTile)level.Collision.GetTile(new Vector3Int(10, 0, 0))).Kind = (TileCollisionKind)5,
            });
            LogAssert.Expect(LogType.Exception, new System.Text.RegularExpressions.Regex("ArgumentOutOfRangeException"));
            yield return Frames(10);
            Assert.IsFalse(Level.Player.enabled, "the Player switched itself off");
            Assert.IsNull(Level.Player.Session, "no session was built");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator ALevelFarFromTheOriginWithNoRopesStaysWhereItIsPainted()
        {
            Build(Floor, new TestLevelOptions
            {
                BeforeActivate = level =>
                {
                    level.Grid.transform.position = new Vector3(200, 0, 0);
                    level.Player.transform.position += new Vector3(200, 0, 0);
                },
            });
            yield return Land();
            Assert.IsEmpty(Level.Ropes, "precondition: no ropes");
            Assert.AreEqual(0, Level.Player.Session.Map.CellMin.x, "the map starts at the painted cells, not at the world origin");
            Assert.AreEqual(200f, Level.Player.Session.Map.Origin.x, 1e-4f);
        }

        IEnumerator GrabWithUp(int frames)
        {
            Press(Keyboard.upArrowKey);
            for (int i = 0; i < frames; i++)
            {
                yield return null;
                if (Level.Player.Session.Current.Contact == PlayerContact.Rope)
                {
                    break;
                }
            }
            Release(Keyboard.upArrowKey);
        }

        [UnityTest]
        public IEnumerator APlayStartsAbilitiesReplaceTheToggles()
        {
            var nextToRope = new Vector2(8.5f, FloorTop);
            Build(RopeLevel);
            Assert.IsFalse(Level.Player.Abilities.RopeClimb, "precondition: the toggle is off");
            Level.Player.Restart(new PlayStart(nextToRope, Abilities.RopeClimb));
            yield return Frames(5);
            yield return GrabWithUp(30);
            Assert.AreEqual(PlayerContact.Rope, Level.Player.Session.Current.Contact, "grabbed with the PlayStart's rope climb");

            Level.Dispose();
            Build(RopeLevel, new TestLevelOptions { Abilities = Abilities.RopeClimb });
            Assert.IsTrue(Level.Player.Abilities.RopeClimb, "precondition: the toggle is on");
            Level.Player.Restart(new PlayStart(nextToRope, Abilities.Jump));
            yield return Frames(5);
            yield return GrabWithUp(30);
            Assert.AreNotEqual(PlayerContact.Rope, Level.Player.Session.Current.Contact, "passive without rope climb in the PlayStart");
        }

#if UNITY_EDITOR
        [UnityTest]
        public IEnumerator NumberSettingsApplyLiveThroughTheInspectorPath()
        {
            Build(Floor);
            yield return Land();
            var serialized = new SerializedObject(Level.Player);
            serialized.FindProperty("movement.runSpeed").floatValue = 10;
            serialized.ApplyModifiedProperties();
            Press(Keyboard.rightArrowKey);
            float fastest = 0f;
            for (int i = 0; i < 20; i++)
            {
                yield return null;
                fastest = Mathf.Max(fastest, Level.Player.Session.Current.VelocityX);
            }
            Assert.AreEqual(10f, fastest, 1e-3f);
        }

        [UnityTest]
        public IEnumerator AnAbilityToggledOnDuringPlayWorksAtOnce()
        {
            Build(Floor);
            yield return Land();
            Press(Keyboard.spaceKey);
            yield return Frames(3);
            Release(Keyboard.spaceKey);
            yield return Frames(15);
            Assert.AreEqual(PlayerContact.Air, Level.Player.Session.Current.Contact, "precondition: in the air");
            Press(Keyboard.spaceKey);
            yield return Frames(2);
            Release(Keyboard.spaceKey);
            Assert.That(Level.Player.Session.Current.VelocityY, Is.LessThan(0f), "precondition: no air jump with the toggle off");
            yield return Land();

            var serialized = new SerializedObject(Level.Player);
            serialized.FindProperty("abilities.doubleJump").boolValue = true;
            serialized.ApplyModifiedProperties();
            Press(Keyboard.spaceKey);
            yield return Frames(3);
            Release(Keyboard.spaceKey);
            yield return Frames(15);
            Assert.AreEqual(PlayerContact.Air, Level.Player.Session.Current.Contact, "precondition: in the air");
            Press(Keyboard.spaceKey);
            yield return Frames(2);
            Assert.That(Level.Player.Session.Current.VelocityY, Is.GreaterThan(0f), "the air jump works without pressing Play again");
        }

        [UnityTest]
        public IEnumerator ASliderEditLeavesAPlayStartsAbilitiesAlone()
        {
            var nextToRope = new Vector2(8.5f, FloorTop);
            Build(RopeLevel);
            Level.Player.Restart(new PlayStart(nextToRope, Abilities.RopeClimb));
            yield return Frames(5);
            var serialized = new SerializedObject(Level.Player);
            serialized.FindProperty("movement.runSpeed").floatValue = 8;
            serialized.ApplyModifiedProperties();
            yield return Frames(2);
            Assert.IsFalse(Level.Player.Abilities.RopeClimb, "precondition: the toggle is off");
            yield return GrabWithUp(30);
            Assert.AreEqual(PlayerContact.Rope, Level.Player.Session.Current.Contact, "rope climb from the PlayStart survives a slider edit");

            serialized.Update();
            serialized.FindProperty("abilities.ropeClimb").boolValue = true;
            serialized.ApplyModifiedProperties();
            yield return null;
            Assert.AreEqual(PlayerContact.Rope, Level.Player.Session.Current.Contact, "ticking it on keeps the hold");
            serialized.FindProperty("abilities.ropeClimb").boolValue = false;
            serialized.ApplyModifiedProperties();
            yield return Frames(2);
            Assert.AreNotEqual(PlayerContact.Rope, Level.Player.Session.Current.Contact, "unticking Rope Climb lets go of the rope");
        }
#endif
    }
}
