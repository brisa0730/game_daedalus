using System.Collections;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The death counter, the win screen and play again, through the real Player and HUD.</summary>
    public class HudAndWinTests : MovementPlayTest
    {
        // 40 × 22 tiles = 640 × 352 pixels, about the HUD's reference size, so the pictures show it as a player sees it.
        static readonly string[] SpikeRun = Run('^');
        static readonly string[] ExitRun = Run('.');

        static string[] Run(char atSix)
        {
            var rows = new string[22];
            for (int i = 0; i < rows.Length; i++)
            {
                rows[i] = new string(i == rows.Length - 1 ? '#' : '.', 40);
            }
            char[] walk = rows[rows.Length - 2].ToCharArray();
            walk[1] = 'P';
            walk[6] = atSix;
            rows[rows.Length - 2] = new string(walk);
            return rows;
        }

        static readonly TestLevelOptions WithCamera = new TestLevelOptions { MainCamera = true };

        IEnumerator UntilDeaths(int deaths, int limit = 300)
        {
            for (int i = 0; i < limit && Level.Player.Session.Deaths < deaths; i++)
            {
                yield return null;
            }
            Assert.AreEqual(deaths, Level.Player.Session.Deaths, "died");
        }

        IEnumerator UntilWon(int limit = 300)
        {
            for (int i = 0; i < limit && Level.Player.Session.Phase != LevelPhase.Won; i++)
            {
                yield return null;
            }
            Assert.AreEqual(LevelPhase.Won, Level.Player.Session.Phase, "reached the exit");
        }

        [UnityTest]
        public IEnumerator TheCounterShowsZeroThenOneAfterASpikeDeath()
        {
            Build(SpikeRun, WithCamera);
            yield return null;
            PlayHud hud = Level.Player.Hud;
            Assert.AreEqual("0", hud.Count.text);
            Keys(Key.RightArrow);
            yield return UntilDeaths(1);
            Keys();
            yield return null;
            Assert.AreEqual("1", hud.Count.text);
            string shown = hud.Count.text;
            yield return Frames(LevelRules.DeathBeatTicks + 10);
            Assert.AreSame(shown, hud.Count.text, "the text is not set again on frames without a death");
            Level.TrapShot("counter");
        }

        [UnityTest]
        public IEnumerator TheExitShowsTheWinScreenAndAKeyPlaysAgainOnlyAfterOneSecond()
        {
            Build(ExitRun, WithCamera);
            Level.AddExit(8, 1);
            Trap floor = Level.AddTrap(12, 0, 2, 1, TrapAction.Vanish, trigger: TrapTrigger.Zone, zone: new Rect(-10, -0.5f, 1, 2));
            yield return null;
            PlayHud hud = Level.Player.Hud;
            Assert.IsFalse(hud.Win.activeSelf, "no win screen while playing");
            Keys(Key.RightArrow);
            yield return UntilWon();
            Keys();
            yield return null;
            Assert.IsTrue(hud.Win.activeSelf, "the dim and the lines");
            Assert.AreEqual("Deaths: 0", hud.WinDeaths.text);
            Assert.IsFalse(hud.PlayAgain.activeSelf, "no prompt in the first second");
            Assert.IsFalse(Level.Player.Session.IsTrapPresent(0), "precondition: the floor trap fired on the way");

            // Keys pressed during the first second do nothing.
            for (int frame = 0; frame < LevelRules.WinInputLockTicks - 3; frame++)
            {
                Keys(frame % 2 == 0 ? Key.A : Key.None);
                yield return null;
                Assert.AreEqual(LevelPhase.Won, Level.Player.Session.Phase, $"frame {frame}");
            }
            Keys();
            yield return Frames(3);
            Assert.IsTrue(Level.Player.Session.CanPlayAgain, "precondition: a second has passed");
            Assert.IsTrue(hud.PlayAgain.activeSelf, "the prompt");
            Level.TrapShot("win");

            // Space plays again, and holding it through the restart does not jump.
            Keys(Key.Space);
            yield return null;
            Assert.AreEqual(LevelPhase.Playing, Level.Player.Session.Phase, "playing again");
            Assert.AreEqual(0, Level.Player.Session.Deaths);
            Assert.IsTrue(Level.Player.Session.IsTrapPresent(0), "the traps were reset");
            Assert.AreEqual(Level.Start, Level.Player.Session.StartFeetWorld);
            Assert.IsFalse(hud.Win.activeSelf, "the win screen is gone");
            yield return Frames(20);
            Assert.AreEqual(Level.Start.y, SimFeetWorld(Level.Player).y, 1e-4f, "no jump from the held key");
            Assert.AreEqual(Level.Start.x, SimFeetWorld(Level.Player).x, 1e-4f, "at the start");
            Assert.IsNotNull(floor);
        }
    }
}
