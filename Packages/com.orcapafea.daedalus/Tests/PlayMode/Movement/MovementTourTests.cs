using System;
using System.Collections;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Scripted keyboard runs over each surface; each asserts its outcome and saves a picture to look at.</summary>
    public class MovementTourTests : MovementPlayTest
    {
        PlayerState State => Level.Player.Session.Current;

        IEnumerator Until(Func<bool> done, int limit, string what)
        {
            for (int i = 0; i < limit; i++)
            {
                yield return null;
                if (done())
                {
                    break;
                }
            }
            Assert.IsTrue(done(), what);
        }

        [UnityTest]
        public IEnumerator JumpUpThroughAOneWayLandOnItAndDropBack()
        {
            Build(new[]
            {
                "..........",
                "..........",
                "..........",
                "..........",
                "..........",
                "...----...",
                "..........",
                "....P.....",
                "##########",
            });
            yield return Until(() => State.Contact == PlayerContact.Ground, 60, "landed on the floor");
            Keys(Key.Space);
            yield return Until(() => State.Contact == PlayerContact.OneWayTop, 90, "jumped up through and landed on the one-way");
            Assert.AreEqual(4f, State.Y, 1e-4f);
            Keys();
            yield return Frames(5);
            Level.Shot("oneway");
            Keys(Key.DownArrow);
            yield return null;
            Keys(Key.DownArrow, Key.Space);
            yield return Until(() => State.Contact == PlayerContact.Air, 5, "dropped through");
            Keys();
            yield return Until(() => State.Contact == PlayerContact.Ground, 90, "back on the floor");
            Assert.AreEqual(1f, State.Y, 1e-4f);
        }

        [UnityTest]
        public IEnumerator SlideOnAnOrdinaryWallAndWallJumpAway()
        {
            var rows = new string[12];
            for (int i = 0; i < 11; i++)
            {
                rows[i] = i == 9 ? "......P.#..." : "........#...";
            }
            rows[11] = "############";
            Build(rows, new TestLevelOptions { Abilities = Abilities.Jump | Abilities.WallJump });
            yield return Until(() => State.Contact == PlayerContact.Ground, 60, "landed");
            Keys(Key.RightArrow, Key.Space);
            yield return Until(() => State.Contact == PlayerContact.WallSlide, 120, "sliding on the wall");
            Level.Shot("wall");
            Keys(Key.RightArrow);
            yield return null;
            Keys(Key.RightArrow, Key.Space);
            yield return null;
            Assert.That(State.VelocityX, Is.LessThan(0f), "pushed away from the wall");
            Assert.That(State.VelocityY, Is.GreaterThan(0f), "jumped");
            Assert.AreEqual(PlayerContact.Air, State.Contact);
        }

        [UnityTest]
        public IEnumerator ClingClimbAndPopOverAClimbableWall()
        {
            Build(new[]
            {
                "............",
                "............",
                "............",
                "............",
                "........C...",
                "........C...",
                "........C...",
                "......P.C...",
                "############",
            }, new TestLevelOptions { Abilities = Abilities.Jump | Abilities.WallClimb });
            yield return Until(() => State.Contact == PlayerContact.Ground, 60, "landed");
            Keys(Key.RightArrow, Key.UpArrow);
            yield return Until(() => State.Contact == PlayerContact.WallCling, 60, "clinging to the climbable wall");
            yield return Frames(10);
            Assert.AreEqual(PlayerContact.WallCling, State.Contact, "still clinging while climbing");
            Level.Shot("climb");
            Keys(Key.UpArrow);
            yield return Until(() => State.IsStanding, 240, "popped over the top");
            Assert.AreEqual(5f, State.Y, 1e-4f, "standing on top of the wall");
        }

        [UnityTest]
        public IEnumerator GrabARopeWithUpClimbToItsOneWayAndStandOnIt()
        {
            Build(new[]
            {
                "............",
                ".....-+-....",
                "......R.....",
                "......R.....",
                "......R.....",
                "......R.....",
                "..P...R.....",
                "############",
            }, new TestLevelOptions { Abilities = Abilities.Jump | Abilities.RopeClimb });
            yield return Until(() => State.Contact == PlayerContact.Ground, 60, "landed");
            Keys(Key.RightArrow);
            yield return Until(() => SimFeetWorld(Level.Player).x > 5.5f, 60, "walked to the rope");
            Assert.AreEqual(PlayerContact.Ground, State.Contact, "walking past the rope never grabs it");
            Keys(Key.RightArrow, Key.UpArrow);
            yield return Until(() => State.Contact == PlayerContact.Rope, 30, "grabbed the rope with Up");
            Keys(Key.UpArrow);
            yield return Frames(20);
            Level.Shot("rope");
            yield return Until(() => State.Contact == PlayerContact.OneWayTop, 240, "stepped onto the one-way at the rope's top");
            Assert.AreEqual(7f, State.Y, 1e-4f);
        }

        [UnityTest]
        public IEnumerator UpAloneNeverLeavesTheGround()
        {
            Build(new[]
            {
                "..........",
                "..........",
                "..........",
                "....P.....",
                "##########",
            });
            yield return Until(() => State.Contact == PlayerContact.Ground, 60, "landed");
            Keys(Key.UpArrow);
            for (int i = 0; i < 60; i++)
            {
                yield return null;
                Assert.AreEqual(PlayerContact.Ground, State.Contact, $"frame {i}");
                Assert.AreEqual(1f, State.Y, 1e-4f, $"frame {i}");
            }
        }
    }
}
