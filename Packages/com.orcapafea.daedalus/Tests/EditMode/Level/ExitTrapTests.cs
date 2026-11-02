using System;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>A Trap on an Exit moves, hides or shows the exit's box; it never blocks, and the exit can't be won on the tick its trap fires.</summary>
    public class ExitTrapTests
    {
        const float Hw = MovementInternals.HitboxHalfWidth;
        const float M = LevelRules.ContactMargin;
        const float Tolerance = 1e-4f;
        static readonly Box ExitBox = new Box(12, 1, 13, 2);
        static readonly Box Zone = new Box(8, 1, 9, 3);

        // 30 x 8: a floor on row 0 unless overridden, the player at (1.5, 1).
        static string[] Map(Func<int, int, char> cell = null)
        {
            return AsciiLevel.Rows(30, 8, (x, y) =>
            {
                char c = cell?.Invoke(x, y) ?? '\0';
                if (c != '\0')
                {
                    return c;
                }
                return y == 0 ? '#' : x == 1 && y == 1 ? 'P' : '.';
            });
        }

        static bool TouchesBox(LevelScenario s, Box box)
        {
            return Box.Hitbox(s.State.X, s.State.Y).Grown(M).Overlaps(box);
        }

        [Test]
        public void ARunawayExitIsWonOnlyAtItsEnd()
        {
            LevelScenario s = new LevelScenario(Map()).Exit(ExitBox)
                .ExitTrap(0, TrapActionKind.Move, TrapTriggerKind.Zone, Zone, 0, 6, 0, 8f).Begin();
            bool passedStart = false;
            int won = s.RunUntil(Hold(1), v =>
            {
                if (TouchesBox(v, ExitBox) && v.Stepper.TrapOffsetX(0) > 0)
                {
                    passedStart = true;
                    Assert.AreEqual(LevelPhase.Playing, v.Stepper.Phase, "touching where it started doesn't win");
                }
                return v.Stepper.Phase == LevelPhase.Won;
            }, 400);
            Assert.IsTrue(passedStart, "precondition: ran through the start box after the exit left");
            Assert.That(won, Is.GreaterThan(0), "caught it");
            Assert.AreEqual(6f, s.Stepper.TrapOffsetX(0), "won at its end");
            Assert.IsTrue(TouchesBox(s, new Box(18, 1, 19, 2)));
            Assert.AreEqual(0, s.Level.Grid.Boxes.ActiveCount, "an exit trap is never a moving box");
        }

        [Test]
        public void AfterADeathTheRunawayExitIsBackAtItsStart()
        {
            LevelScenario s = new LevelScenario(Map((x, y) => x == 10 && y == 1 ? '^' : '\0')).Exit(ExitBox)
                .ExitTrap(0, TrapActionKind.Move, TrapTriggerKind.Zone, Zone, 0, 6, 0, 8f).Begin();
            Assert.That(s.RunUntil(Hold(1), v => v.Stepper.Phase == LevelPhase.Dying, 200), Is.GreaterThan(0), "precondition: died on the spikes");
            Assert.That(s.Stepper.TrapOffsetX(0), Is.GreaterThan(0f), "precondition: the exit had run");
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase);
            Assert.AreEqual(0f, s.Stepper.TrapOffsetX(0), "back at its start");
            Assert.IsTrue(s.Stepper.IsTrapPresent(0));
        }

        [Test]
        public void AVanishExitCannotBeWonOnceGone()
        {
            LevelScenario s = new LevelScenario(Map()).Exit(ExitBox).ExitTrap(0, TrapActionKind.Vanish, TrapTriggerKind.Zone, Zone).Begin();
            int through = s.RunUntil(Hold(1), v => v.State.X > 14f, 300);
            Assert.That(through, Is.GreaterThan(0), "walked through where it was");
            Assert.IsFalse(s.Stepper.IsTrapPresent(0));
            Assert.IsFalse(s.Phases.Contains(LevelPhase.Won), "never won");
        }

        [Test]
        public void ATouchVanishExitVanishesAsItIsTouched()
        {
            LevelScenario s = new LevelScenario(Map()).Exit(ExitBox).ExitTrap(0, TrapActionKind.Vanish, TrapTriggerKind.Touch).Begin();
            int touched = s.RunUntil(Hold(1), v => TouchesBox(v, ExitBox), 300);
            Assert.That(touched, Is.GreaterThan(0));
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "the touch tick: triggers run before exits, so it's gone");
            Assert.IsFalse(s.Stepper.IsTrapPresent(0));
            s.RunUntil(Hold(1), v => v.State.X > 14f, 120);
            Assert.IsFalse(s.Phases.Contains(LevelPhase.Won));
        }

        [Test]
        public void AnAppearExitCanBeWonOnlyAfterItAppears()
        {
            var exit = new Box(6, 1, 7, 2);
            LevelScenario s = new LevelScenario(Map()).Exit(exit).ExitTrap(0, TrapActionKind.Appear, TrapTriggerKind.Zone, new Box(10, 1, 11, 3)).Begin();
            Assert.IsFalse(s.Stepper.IsTrapPresent(0), "hidden at the start");
            Assert.That(s.RunUntil(Hold(1), v => v.State.X > 8f, 200), Is.GreaterThan(0), "walked through it");
            Assert.IsFalse(s.Phases.Contains(LevelPhase.Won), "no win while hidden");
            Assert.That(s.RunUntil(Hold(1), v => v.Stepper.IsTrapPresent(0), 200), Is.GreaterThan(0), "it appeared");
            Assert.That(s.RunUntil(Hold(-1), v => v.Stepper.Phase == LevelPhase.Won, 200), Is.GreaterThan(0), "won after it appeared");
        }

        [Test]
        public void AFallExitLandsWithItsBottomExactlyOnTheRow()
        {
            LevelScenario s = new LevelScenario(Map()).Exit(new Box(12, 4, 13, 5.5f))
                .ExitTrap(0, TrapActionKind.Fall, TrapTriggerKind.Zone, Zone).Begin();
            Assert.That(s.RunUntil(Hold(1), v => v.Stepper.IsTrapMoving(0), 200), Is.GreaterThan(0));
            Assert.That(s.RunUntil(Idle, v => !v.Stepper.IsTrapMoving(0), 120), Is.GreaterThan(0), "landed");
            Assert.AreEqual(-3f, s.Stepper.TrapOffsetY(0), "bottom on the floor's top");
            Assert.That(s.RunUntil(Hold(1), v => v.Stepper.Phase == LevelPhase.Won, 200), Is.GreaterThan(0), "won where it landed");
        }

        [Test]
        public void AFallExitOverAPitIsGoneUntilADeath()
        {
            // The pit is wide, so the player drops in well short of where the exit fell.
            LevelScenario s = new LevelScenario(Map((x, y) => y == 0 && x >= 11 && x <= 18 ? '.' : '\0')).Exit(new Box(16, 4, 17, 5.5f))
                .ExitTrap(0, TrapActionKind.Fall, TrapTriggerKind.Zone, Zone).Begin();
            Assert.That(s.RunUntil(Hold(1), v => v.Stepper.Phase == LevelPhase.Dying, 300), Is.GreaterThan(0), "precondition: fell into the pit after it");
            Assert.IsFalse(s.Stepper.IsTrapPresent(0), "the exit fell out of the map");
            Assert.IsFalse(s.Phases.Contains(LevelPhase.Won), "unwinnable");
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "back after the death");
            Assert.AreEqual(0f, s.Stepper.TrapOffsetY(0));
        }

        [Test]
        public void AFallExitDippingIntoALedgeSnapsOntoItAndStays()
        {
            // A one-row ledge (row 2) over a pit; the exit's sprite box dips 0.05 into it (round 1, F7).
            LevelScenario s = new LevelScenario(Map((x, y) => x >= 11 && x <= 13 && y == 2 ? '#' : y == 0 && x >= 11 && x <= 13 ? '.' : '\0'))
                .Exit(new Box(12, 2.95f, 13, 4.45f)).ExitTrap(0, TrapActionKind.Fall, TrapTriggerKind.Zone, Zone).Begin();
            Assert.That(s.RunUntil(Hold(1), v => Box.Hitbox(v.State.X, v.State.Y).Overlaps(Zone), 200), Is.GreaterThan(0));
            s.Tick(Idle, 30);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "didn't fall out of the map");
            Assert.AreEqual(0.05f, s.Stepper.TrapOffsetY(0), Tolerance, "snapped up onto the ledge's top");
            Assert.IsFalse(s.Stepper.IsTrapMoving(0));
        }

        [Test]
        public void ADefaultZoneRunawayExitAlwaysRunsBeforeItCanBeReached()
        {
            // The exit's default zone is its box grown by 1 on every side (round 1, F6).
            var exit = new Box(12, 1, 13, 2.5f);
            Box zone = LevelRules.DefaultExitZone(exit);
            int cases = 0;
            foreach (float runSpeed in new[] { MovementSettings.DefaultRunSpeed, MovementSettings.MaxRunSpeed })
            {
                MovementSettings settings = MovementSettings.Default;
                settings.RunSpeed = runSpeed;
                for (int k = 0; k < 16; k++)
                {
                    float x0 = 2f + runSpeed / 60f * k / 16f;
                    LevelScenario s = new LevelScenario(Map()).WithSettings(settings).Exit(exit)
                        .ExitTrap(0, TrapActionKind.Move, TrapTriggerKind.Zone, null, 0, 6, 0, 8f).BeginAt(x0, 1f);
                    int fire = -1, touch = -1;
                    for (int t = 1; t <= 400 && s.Stepper.Phase == LevelPhase.Playing; t++)
                    {
                        s.Tick(Hold(1));
                        if (fire < 0 && Box.Hitbox(s.State.X, s.State.Y).Overlaps(zone))
                        {
                            fire = t;
                        }
                        if (touch < 0 && TouchesBox(s, exit))
                        {
                            touch = t;
                            Assert.That(s.Stepper.IsTrapMoving(0) || s.Stepper.TrapOffsetX(0) > 0, Is.True, $"speed {runSpeed} case {k}: already running when reached");
                        }
                    }
                    Assert.That(fire, Is.GreaterThan(0), $"speed {runSpeed} case {k}: fired");
                    Assert.That(touch, Is.GreaterThan(fire), $"speed {runSpeed} case {k}: fired before any touch of the start box");
                    if (s.Stepper.Phase == LevelPhase.Won)
                    {
                        Assert.That(s.Stepper.TrapOffsetX(0), Is.GreaterThan(0f), $"speed {runSpeed} case {k}: not won at the start box");
                    }
                    cases++;
                }
            }
            Assert.AreEqual(32, cases);
        }

        [Test]
        public void ATouchTriggeredMoveExitRunsInsteadOfWinning()
        {
            // The grown hitbox starts 0.005 short of the exit: one tick of walking touches it.
            LevelScenario s = new LevelScenario(Map()).Exit(ExitBox)
                .ExitTrap(0, TrapActionKind.Move, TrapTriggerKind.Touch, null, 0, 6, 0, 8f).BeginAt(12f - M - Hw - 0.005f, 1f);
            s.Tick(Idle);
            Assert.IsFalse(TouchesBox(s, ExitBox), "precondition: not touching");
            s.Tick(Hold(1));
            Assert.IsTrue(TouchesBox(s, ExitBox), "precondition: touching");
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "not won on the fire tick");
            s.Tick(Idle, 60);
            Assert.AreEqual(6f, s.Stepper.TrapOffsetX(0), "it ran");
            Assert.IsFalse(s.Phases.Contains(LevelPhase.Won));
        }

        [Test]
        public void AnExitTrapNeverBlocks()
        {
            LevelScenario s = new LevelScenario(Map()).Exit(new Box(12, 4, 13, 5.5f))
                .ExitTrap(0, TrapActionKind.Fall, TrapTriggerKind.Zone, Zone).Begin();
            Assert.That(s.RunUntil(Hold(1), v => v.Stepper.IsTrapMoving(0), 200), Is.GreaterThan(0));
            while (s.Stepper.IsTrapMoving(0))
            {
                Assert.AreEqual(0, s.Level.Grid.Boxes.ActiveCount, "not in the box layer");
                for (int y = 1; y < 6; y++)
                {
                    Assert.AreEqual(TileKind.None, s.Level.Grid.Get(12, y), "never stamped");
                }
                s.Tick(Idle);
            }
            Assert.AreEqual(TileKind.None, s.Level.Grid.Get(12, 1), "not stamped at rest either");
        }
    }
}
