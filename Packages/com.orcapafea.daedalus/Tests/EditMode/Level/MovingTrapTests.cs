using System;
using System.Collections.Generic;
using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>Move and Fall in the level step: start moment, arrival, landing, leaving the map, reset, the death-beat freeze.</summary>
    public class MovingTrapTests
    {
        const float Tolerance = 1e-4f;
        static readonly Box FarZone = new Box(4, 1, 5, 3);

        // 30 x 12: a floor on row 0 (unless overridden), the player at (1.5, 1).
        static string[] Map(Func<int, int, char> cell = null)
        {
            return AsciiLevel.Rows(30, 12, (x, y) =>
            {
                char c = cell?.Invoke(x, y) ?? '\0';
                if (c != '\0')
                {
                    return c;
                }
                return y == 0 ? '#' : x == 1 && y == 1 ? 'P' : '.';
            });
        }

        // Walks right into the zone; returns on the trigger tick.
        static void WalkIntoZone(LevelScenario s, Box zone)
        {
            int t = s.RunUntil(Hold(1), v => Box.Hitbox(v.State.X, v.State.Y).Overlaps(zone), 300);
            Assert.That(t, Is.GreaterThan(0), "precondition: walked into the zone");
        }

        [Test]
        public void AMoveStartsTheTickAfterItsTriggerAndArrivesExactlyOnItsEnd()
        {
            LevelScenario s = new LevelScenario(Map())
                .Trap(20, 6, 3, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, FarZone, 0, 0, -3, 8f).Begin();
            TileGrid grid = s.Level.Grid;
            Assert.AreEqual(TileKind.Solid, grid.Get(21, 6), "precondition: stamped at rest");
            WalkIntoZone(s, FarZone);
            Assert.IsFalse(s.Stepper.IsTrapMoving(0), "the trigger tick: not moving yet");
            Assert.AreEqual(TileKind.Solid, grid.Get(21, 6), "still stamped on the trigger tick (no one-tick hole)");
            Assert.AreEqual(0, grid.Boxes.ActiveCount);

            s.Tick(Idle);
            Assert.IsTrue(s.Stepper.IsTrapMoving(0), "moving on the next tick");
            Assert.AreEqual(1, grid.Boxes.ActiveCount, "its box is in the layer");
            Assert.AreEqual(TileKind.None, grid.Get(21, 6), "its cells left the cell layer in the same tick");
            Assert.AreEqual(-TrapMotion.MoveStep(8f), s.Stepper.TrapOffsetY(0), Tolerance);

            int ticks = TrapMotion.MoveTicks(0, -3, 8f);
            Assert.AreEqual(23, ticks);
            s.Tick(Idle, ticks - 2);
            Assert.IsTrue(s.Stepper.IsTrapMoving(0), "one tick short of the end");
            s.Tick(Idle);
            Assert.IsFalse(s.Stepper.IsTrapMoving(0), "arrived");
            Assert.AreEqual(-3f, s.Stepper.TrapOffsetY(0), "exactly the end");
            Assert.AreEqual(0f, s.Stepper.TrapOffsetX(0));
            Assert.AreEqual(0, grid.Boxes.ActiveCount, "out of the layer");
            Assert.AreEqual(TileKind.Solid, grid.Get(20, 3), "stamped at the end");
            Assert.AreEqual(TileKind.Solid, grid.Get(22, 3));
            Assert.AreEqual(TileKind.None, grid.Get(21, 6), "not at the start");
            for (int i = 0; i < 300; i++)
            {
                s.Tick(Idle);
                Assert.AreEqual(-3f, s.Stepper.TrapOffsetY(0), $"stays, tick {i}");
            }
        }

        [Test]
        public void AHazardMoveStillKillsOnItsTriggerTick()
        {
            LevelScenario s = new LevelScenario(Map())
                .Trap(20, 6, 1, 1, TileKind.Hazard, TrapActionKind.Move, TrapTriggerKind.Zone, FarZone, 0, 0, -3, 8f).Begin();
            WalkIntoZone(s, FarZone);
            Assert.IsTrue(TileCollision.CoreTouchesHazard(s.Level.Grid, 20.5f, 5.5f), "the death check sees it on the trigger tick (F5)");
            s.Tick(Idle);
            Assert.IsTrue(s.Stepper.IsTrapMoving(0));
            Assert.AreEqual(TileKind.None, s.Level.Grid.Get(20, 6), "its cell left the cell layer");
            Assert.IsTrue(TileCollision.CoreTouchesHazard(s.Level.Grid, 20.5f, 5.5f), "the moving box still kills");
        }

        [Test]
        public void ADelayedMoveMovesOnTheTickItsDelayEnds()
        {
            LevelScenario s = new LevelScenario(Map())
                .Trap(20, 6, 3, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, FarZone, LevelRules.SecondsToTicks(0.5f), 0, -3, 8f).Begin();
            WalkIntoZone(s, FarZone);
            s.Tick(Idle, 29);
            Assert.IsFalse(s.Stepper.IsTrapMoving(0), "29 ticks after the trigger");
            Assert.AreEqual(0f, s.Stepper.TrapOffsetY(0));
            s.Tick(Idle);
            Assert.IsTrue(s.Stepper.IsTrapMoving(0), "the 30th: the delay ends and it moves in the same tick");
            Assert.AreEqual(-TrapMotion.MoveStep(8f), s.Stepper.TrapOffsetY(0), Tolerance);
        }

        [Test]
        public void AMoveTravelsThroughPaintedTilesAndRestsUnderPaintedOnes()
        {
            // Painted Solid on row 4 in its path, painted Climbable where it ends (row 2).
            LevelScenario s = new LevelScenario(Map((x, y) => x >= 20 && x <= 22 && y == 4 ? '#' : x >= 20 && x <= 22 && y == 2 ? 'C' : '\0'))
                .Trap(20, 6, 3, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, FarZone, 0, 0, -4, 8f).Begin();
            WalkIntoZone(s, FarZone);
            Assert.That(s.RunUntil(Idle, v => !v.Stepper.IsTrapMoving(0) && v.Stepper.TrapOffsetY(0) != 0, 120), Is.GreaterThan(0));
            Assert.AreEqual(-4f, s.Stepper.TrapOffsetY(0), "through the painted row to its end");
            Assert.AreEqual(TileKind.Climbable, s.Level.Grid.Get(21, 2), "painted wins over the trap's cells");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(21, 4), "the painted row is untouched");
            Assert.AreEqual(TileKind.None, s.Level.Grid.Get(21, 6));
        }

        [Test]
        public void AFallAcceleratesFallsThroughOneWayAndATrapAndRestsExactlyOnPaintedGround()
        {
            // A one-way row 4, a resting Vanish trap on row 2 that never fires, the floor on row 0.
            LevelScenario s = new LevelScenario(Map((x, y) => y == 4 && x >= 8 && x <= 14 ? '-' : '\0'))
                .Trap(10, 8, 3, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, FarZone)
                .Trap(10, 2, 3, 1, TileKind.Solid, TrapActionKind.Vanish, TrapTriggerKind.Zone, new Box(28, 10, 29, 11))
                .Begin();
            WalkIntoZone(s, FarZone);
            Assert.AreEqual(0f, s.Stepper.TrapOffsetY(0), "at rest on the trigger tick");
            var derived = new DerivedMovement(MovementSettings.Default);
            float vy = 0, y = 0;
            for (int tick = 1; tick <= 10; tick++)
            {
                s.Tick(Idle);
                vy = MathF.Min(vy + derived.RiseGravity / 60f, derived.MaxFallSpeed);
                y -= vy / 60f;
                Assert.AreEqual(y, s.Stepper.TrapOffsetY(0), Tolerance, $"rise gravity, tick {tick}");
            }
            Assert.That(s.RunUntil(Idle, v => !v.Stepper.IsTrapMoving(0), 120), Is.GreaterThan(0), "landed");
            Assert.AreEqual(-7f, s.Stepper.TrapOffsetY(0), "on the floor's top, through the one-way and the resting trap");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(11, 1), "cells at the exact row");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(11, 2), "the resting trap is still there");
            Assert.AreEqual(0, s.Level.Grid.Boxes.ActiveCount);
        }

        [Test]
        public void AFallLandsOnPaintedClimbable()
        {
            LevelScenario s = new LevelScenario(Map((x, y) => y == 3 && x >= 10 && x <= 12 ? 'C' : '\0'))
                .Trap(10, 8, 3, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, FarZone).Begin();
            WalkIntoZone(s, FarZone);
            Assert.That(s.RunUntil(Idle, v => !v.Stepper.IsTrapMoving(0) && v.Stepper.TrapOffsetY(0) != 0, 120), Is.GreaterThan(0));
            Assert.AreEqual(-4f, s.Stepper.TrapOffsetY(0));
        }

        [Test]
        public void AFallOutOfTheMapIsGoneUntilADeath()
        {
            // A pit under the trap's columns; spikes at column 8 to die on.
            LevelScenario s = new LevelScenario(Map((x, y) => y == 0 && x >= 19 && x <= 23 ? '.' : y == 1 && x == 8 ? '^' : '\0'))
                .Trap(20, 6, 3, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, FarZone).Begin();
            WalkIntoZone(s, FarZone);
            int changes = s.Stepper.TrapChanges;
            int gone = s.RunUntil(Idle, v => !v.Stepper.IsTrapPresent(0), 200);
            Assert.That(gone, Is.GreaterThan(0), "gone");
            Assert.That(6f + s.Stepper.TrapOffsetY(0) + 1f, Is.LessThan(0f), "its box is fully below row 0");
            Assert.That(6f + s.Stepper.TrapOffsetY(0) + 1f, Is.GreaterThan(-1f), "gone on the first tick fully below");
            Assert.AreNotEqual(changes, s.Stepper.TrapChanges, "the renderer is told");
            Assert.IsFalse(s.Stepper.IsTrapMoving(0));
            Assert.AreEqual(0, s.Level.Grid.Boxes.ActiveCount);
            float offset = s.Stepper.TrapOffsetY(0);
            s.Tick(Idle, 120);
            Assert.IsFalse(s.Stepper.IsTrapPresent(0), "never comes back on its own");
            Assert.AreEqual(offset, s.Stepper.TrapOffsetY(0), "and stops");

            Assert.That(s.RunUntil(Hold(1), v => v.Stepper.Phase == LevelPhase.Dying, 200), Is.GreaterThan(0), "precondition: died on the spikes");
            s.Tick(Idle, LevelRules.DeathBeatTicks);
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "back after the death");
            Assert.AreEqual(0f, s.Stepper.TrapOffsetY(0), "at its start");
            Assert.IsFalse(s.Stepper.IsTrapMoving(0), "resting");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(21, 6));
        }

        [Test]
        public void TheDeathBeatFreezesAMovingTrapAndTheRespawnResetsIt()
        {
            LevelScenario s = new LevelScenario(Map((x, y) => y == 1 && x == 8 ? '^' : '\0'))
                .Trap(20, 6, 3, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, FarZone, 0, 0, -5, 1f).Begin();
            WalkIntoZone(s, FarZone);
            Assert.That(s.RunUntil(Hold(1), v => v.Stepper.Phase == LevelPhase.Dying, 200), Is.GreaterThan(0), "precondition: died on the spikes");
            Assert.IsTrue(s.Stepper.IsTrapMoving(0), "precondition: killed mid-move");
            float offset = s.Stepper.TrapOffsetY(0);
            int changes = s.Stepper.TrapChanges;
            for (int i = 1; i < LevelRules.DeathBeatTicks; i++)
            {
                s.Tick(Idle);
                Assert.AreEqual(LevelPhase.Dying, s.Stepper.Phase);
                Assert.AreEqual(offset, s.Stepper.TrapOffsetY(0), $"frozen on beat tick {i}");
            }
            s.Tick(Idle);
            Assert.AreEqual(LevelPhase.Playing, s.Stepper.Phase, "respawned");
            Assert.AreEqual(0f, s.Stepper.TrapOffsetY(0), "back at its start");
            Assert.IsFalse(s.Stepper.IsTrapMoving(0));
            Assert.AreNotEqual(changes, s.Stepper.TrapChanges, "the reset moved TrapChanges");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(21, 6), "stamped at the start again");

            // Re-armed: back into the zone, and it moves again.
            Assert.That(s.RunUntil(Hold(-1), v => Box.Hitbox(v.State.X, v.State.Y).Overlaps(FarZone), 200), Is.GreaterThan(0));
            s.Tick(Idle);
            Assert.IsTrue(s.Stepper.IsTrapMoving(0), "fires again after the reset");
        }

        [Test]
        public void TheZoneStaysWhereItWasDrawnAndFiresOncePerLife()
        {
            LevelScenario s = new LevelScenario(Map())
                .Trap(20, 6, 3, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, FarZone, 0, 0, -3, 30f).Begin();
            WalkIntoZone(s, FarZone);
            Assert.That(s.RunUntil(Hold(1), v => v.State.X > 7f && !v.Stepper.IsTrapMoving(0), 120), Is.GreaterThan(0), "left the zone; arrived");
            Assert.AreEqual(-3f, s.Stepper.TrapOffsetY(0));
            Assert.That(s.RunUntil(Hold(-1), v => Box.Hitbox(v.State.X, v.State.Y).Overlaps(FarZone), 120), Is.GreaterThan(0), "back in the zone");
            s.Tick(Idle, 30);
            Assert.IsFalse(s.Stepper.IsTrapMoving(0), "doesn't fire again");
            Assert.AreEqual(-3f, s.Stepper.TrapOffsetY(0), "stays at its end");
        }

        [Test]
        public void MoveAndFallArePresentAtResetAppearIsNot()
        {
            LevelScenario s = new LevelScenario(Map())
                .Trap(10, 3, 1, 1, TileKind.Solid, TrapActionKind.Vanish)
                .Trap(12, 3, 1, 1, TileKind.Solid, TrapActionKind.Appear)
                .Trap(14, 3, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, null, 0, 0, -1)
                .Trap(16, 3, 1, 1, TileKind.Solid, TrapActionKind.Fall)
                .Begin();
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "Vanish");
            Assert.IsFalse(s.Stepper.IsTrapPresent(1), "Appear");
            Assert.IsTrue(s.Stepper.IsTrapPresent(2), "Move (a Phase 3 regression: it started absent)");
            Assert.IsTrue(s.Stepper.IsTrapPresent(3), "Fall");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(14, 3), "Move stamped");
            Assert.AreEqual(TileKind.Solid, s.Level.Grid.Get(16, 3), "Fall stamped");
        }

        [Test]
        public void FiringAMoveOrFallNeverChangesPresence()
        {
            LevelScenario s = new LevelScenario(Map())
                .Trap(20, 6, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, FarZone, 0, 0, 0)
                .Trap(22, 6, 1, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, FarZone)
                .Begin();
            int changes = s.Stepper.TrapChanges;
            WalkIntoZone(s, FarZone);
            s.Tick(Idle);
            Assert.IsTrue(s.Stepper.IsTrapPresent(0), "a Move fired (a Phase 3 regression: it acted like Vanish)");
            Assert.IsTrue(s.Stepper.IsTrapPresent(1));
            Assert.AreEqual(changes, s.Stepper.TrapChanges);
            Assert.IsFalse(s.Stepper.IsTrapMoving(0), "a zero offset doesn't move");
            Assert.AreEqual(0f, s.Stepper.TrapOffsetY(0));
            Assert.IsTrue(s.Stepper.IsTrapMoving(1));
        }

        [Test]
        public void ChangingGravityDuringAFallChangesTheNextTicksAcceleration()
        {
            LevelScenario s = new LevelScenario(Map())
                .Trap(20, 10, 1, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, FarZone).Begin();
            WalkIntoZone(s, FarZone);
            var offsets = new List<float> { 0f };
            for (int i = 0; i < 3; i++)
            {
                s.Tick(Idle);
                offsets.Add(s.Stepper.TrapOffsetY(0));
            }
            MovementSettings heavy = MovementSettings.Default;
            heavy.Gravity = 2f;
            s.Stepper.ApplySettings(heavy);
            s.Tick(Idle);
            offsets.Add(s.Stepper.TrapOffsetY(0));
            float v3 = (offsets[2] - offsets[3]) * 60f;
            float v4 = (offsets[3] - offsets[4]) * 60f;
            float v2 = (offsets[1] - offsets[2]) * 60f;
            Assert.AreEqual(MovementInternals.BaseGravity / 60f, v3 - v2, 1e-2f, "gravity 1");
            Assert.AreEqual(2f * MovementInternals.BaseGravity / 60f, v4 - v3, 1e-2f, "gravity 2 from the next tick");
        }

        // Two Moves crossing paths and a Fall through a resting trap give identical worlds with the trap array reversed.
        static LevelScenario Crossing(bool reversed)
        {
            LevelScenario s = new LevelScenario(Map())
                .Trap(15, 6, 1, 1, TileKind.Solid, TrapActionKind.Move, TrapTriggerKind.Zone, FarZone, 0, 6, 0, 8f)
                .Trap(21, 6, 1, 1, TileKind.Hazard, TrapActionKind.Move, TrapTriggerKind.Zone, FarZone, 0, -6, 0, 8f)
                .Trap(17, 10, 1, 1, TileKind.Solid, TrapActionKind.Fall, TrapTriggerKind.Zone, FarZone)
                .Trap(17, 3, 1, 1, TileKind.Hazard, TrapActionKind.Vanish, TrapTriggerKind.Zone, new Box(28, 10, 29, 11));
            if (reversed)
            {
                s.Reversed();
            }
            return s.Begin();
        }

        static string Snapshot(LevelScenario s, bool reversed)
        {
            var sb = new System.Text.StringBuilder();
            TileGrid g = s.Level.Grid;
            for (int y = 0; y < g.Height; y++)
            {
                for (int x = 0; x < g.Width; x++)
                {
                    sb.Append((int)g.Get(x, y));
                }
            }
            var boxes = new List<string>();
            for (int k = 0; k < g.Boxes.ActiveCount; k++)
            {
                g.Boxes.GetActive(k, out Box b, out TileKind kind);
                boxes.Add($"{b.MinX:R},{b.MinY:R},{kind}");
            }
            boxes.Sort(StringComparer.Ordinal);
            sb.Append('|').Append(string.Join(";", boxes)).Append('|');
            int n = s.Stepper.TrapCount;
            for (int i = 0; i < n; i++)
            {
                int j = reversed ? n - 1 - i : i;
                sb.Append($"{s.Stepper.TrapOffsetX(j):R},{s.Stepper.TrapOffsetY(j):R},{s.Stepper.IsTrapPresent(j)};");
            }
            return sb.ToString();
        }

        [Test]
        public void TheResultDoesNotDependOnTheTrapOrder()
        {
            LevelScenario forward = Crossing(false);
            LevelScenario backward = Crossing(true);
            WalkIntoZone(forward, FarZone);
            WalkIntoZone(backward, FarZone);
            Assert.AreEqual(forward.Ticks, backward.Ticks);
            bool crossed = false;
            for (int t = 0; t < 90; t++)
            {
                forward.Tick(Idle);
                backward.Tick(Idle);
                Assert.AreEqual(Snapshot(forward, false), Snapshot(backward, true), $"tick {t}");
                crossed |= forward.Stepper.TrapOffsetX(0) > 3f && forward.Stepper.TrapOffsetX(1) < -3f;
            }
            Assert.IsTrue(crossed, "precondition: the two Moves crossed");
            Assert.IsFalse(forward.Stepper.IsTrapMoving(2), "precondition: the Fall landed");
            Assert.AreEqual(-9f, forward.Stepper.TrapOffsetY(2), "through the resting trap onto the floor");
        }
    }
}
