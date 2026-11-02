using NUnit.Framework;
using Orcapafea.Daedalus.Sim;
using static Orcapafea.Daedalus.Tests.PlayerScenario;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Fires a trap while the player stands still at an exact spot: the trap's zone floats over the player's head
    /// (<see cref="ZoneOverHead"/>), a straight jump enters it, and the trap's Delay lets the player land first.
    /// </summary>
    static class TrapRig
    {
        /// <summary>A thin zone a straight jump from feet (<paramref name="x"/>, <paramref name="feet"/>) enters; standing, the head stays under it.</summary>
        public static Box ZoneOverHead(float x, float feet)
        {
            return new Box(x - 0.5f, feet + 2f, x + 0.5f, feet + 2.2f);
        }

        /// <summary>Jumps straight up and lands again (no horizontal input, so X is unchanged).</summary>
        public static void JumpAndLand(LevelScenario s)
        {
            float x = s.State.X;
            s.Tick(Press());
            int landed = s.RunUntil(Hold(0, 0, true), v => v.State.IsStanding, 240);
            Assert.That(landed, Is.GreaterThan(0), "precondition: landed after the trigger jump");
            Assert.AreEqual(x, s.State.X, "precondition: a straight jump");
        }

        /// <summary>Idles until trap <paramref name="trap"/> moves; returns the scenario after its first moving tick.</summary>
        public static void IdleUntilMoving(LevelScenario s, int trap = 0, int limit = 300)
        {
            int ticks = s.RunUntil(Idle, v => v.Stepper.IsTrapMoving(trap), limit);
            Assert.That(ticks, Is.GreaterThan(0), "precondition: the trap started moving");
        }

        /// <summary>The trap's current box from its start box and the stepper's offset.</summary>
        public static Box Current(LevelScenario s, int trap, Box start)
        {
            float dx = s.Stepper.TrapOffsetX(trap);
            float dy = s.Stepper.TrapOffsetY(trap);
            return new Box(start.MinX + dx, start.MinY + dy, start.MaxX + dx, start.MaxY + dy);
        }
    }
}
