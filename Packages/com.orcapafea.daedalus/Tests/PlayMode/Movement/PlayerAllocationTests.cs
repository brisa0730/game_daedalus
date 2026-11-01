using System.Collections;
using System.Reflection;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using AllocIs = UnityEngine.TestTools.Constraints.Is;
using Is = NUnit.Framework.Is;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// <see cref="Player.RunFrame"/> allocates nothing in steady play (Architecture rule 6), measured with the Test
    /// Framework's GC.Alloc-recorder constraint (<c>GC.GetAllocatedBytesForCurrentThread</c> sees nothing in Unity).
    /// </summary>
    public class PlayerAllocationTests : MovementPlayTest
    {
        const float Dt = 1f / 60f;

        static readonly string[] Tour =
        {
            "........................",
            "........................",
            "........................",
            "........................",
            ".................-+-....",
            "..................R.....",
            "..................R.....",
            "..................R.....",
            "..................R.....",
            "....P.............R.....",
            "##########....##########",
        };

        Player player;
        int batchFrames;
        readonly bool[] contactsSeen = new bool[6];
        bool sawReset;

        void RunBatch()
        {
            for (int i = 0; i < batchFrames; i++)
            {
                player.RunFrame(Dt);
                PlayerState state = player.Session.Current;
                contactsSeen[(int)state.Contact] = true;
                sawReset |= state.Teleported;
            }
        }

        [UnityTest]
        public IEnumerator RunFrameAllocatesNothing()
        {
            byte[] planted = null;
            bool probeWorks = true;
            try
            {
                Assert.That(() => { planted = new byte[1024]; }, AllocIs.AllocatingGCMemory());
            }
            catch (AssertionException)
            {
                probeWorks = false;
            }
            System.GC.KeepAlive(planted);
            if (!probeWorks)
            {
                Assert.Ignore("Is.AllocatingGCMemory doesn't see a planted 1 KB allocation in PlayMode on this editor; code review carries the guarantee (framework.md).");
            }

            Build(Tour, new TestLevelOptions { Abilities = Abilities.Jump | Abilities.RopeClimb | Abilities.DoubleJump });
            player = Level.Player;
            yield return Frames(120);
            Time.timeScale = 0;

            MethodInfo onValidate = typeof(Player).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
            TestDelegate batch = RunBatch;
            int frames = 0;

            void Measure(int count, string what)
            {
                InputSystem.Update();
                batchFrames = count;
                Assert.That(batch, AllocIs.Not.AllocatingGCMemory(), what);
                frames += count;
            }

            player.Restart(new PlayStart(new Vector2(4.5f, 1), Abilities.Jump | Abilities.RopeClimb | Abilities.DoubleJump));
            Keys();
            Measure(30, "standing");
            player.Abilities.WallJump = true;
            onValidate.Invoke(player, null);
            Measure(10, "after a settings change");
            Keys(Key.Space);
            Measure(1, "jump press");
            Measure(15, "rising");
            Keys();
            Measure(1, "jump release");
            Keys(Key.Space);
            Measure(1, "air jump press");
            Keys();
            Measure(60, "falling and landing");
            Keys(Key.RightArrow);
            Measure(60, "running");
            Keys(Key.LeftArrow);
            Measure(40, "turning");

            player.Restart(new PlayStart(new Vector2(18.5f, 1), Abilities.Jump | Abilities.RopeClimb));
            Keys(Key.UpArrow);
            Measure(180, "grabbing and climbing the rope to its one-way");
            Keys(Key.DownArrow);
            Measure(20, "holding Down on the one-way");
            Keys(Key.DownArrow, Key.Space);
            Measure(20, "Down + Jump");
            Keys();
            Measure(40, "landing");

            player.Restart(new PlayStart(new Vector2(8.5f, 1), Abilities.Jump));
            Keys(Key.RightArrow);
            Measure(122, "walking into the pit and resetting");

            Assert.That(frames, Is.GreaterThanOrEqualTo(600), "measured frames");
            Assert.IsTrue(contactsSeen[(int)PlayerContact.Ground], "Ground");
            Assert.IsTrue(contactsSeen[(int)PlayerContact.Air], "Air");
            Assert.IsTrue(contactsSeen[(int)PlayerContact.OneWayTop], "OneWayTop");
            Assert.IsTrue(contactsSeen[(int)PlayerContact.Rope], "Rope");
            Assert.IsTrue(sawReset, "a fall-out reset");
        }
    }
}
