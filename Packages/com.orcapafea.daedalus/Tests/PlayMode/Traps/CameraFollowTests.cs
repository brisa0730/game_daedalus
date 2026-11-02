using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>The minimal follow camera: it follows the player, stays inside the map and cuts on a respawn.</summary>
    public class CameraFollowTests : MovementPlayTest
    {
        const int Width = 60;
        const int ViewWidth = 20;
        const float Half = ViewWidth / 2f;

        // A long floor with a pit at columns 40–42.
        static string[] Long()
        {
            var rows = new string[10];
            for (int i = 0; i < rows.Length - 1; i++)
            {
                rows[i] = new string('.', Width);
            }
            char[] floor = new string('#', Width).ToCharArray();
            floor[40] = floor[41] = floor[42] = '.';
            rows[rows.Length - 1] = new string(floor);
            char[] start = rows[rows.Length - 2].ToCharArray();
            start[2] = 'P';
            rows[rows.Length - 2] = new string(start);
            return rows;
        }

        [UnityTest]
        public IEnumerator TheCameraFollowsAndIsClampedAtTheEndsAndTheBottom()
        {
            Build(Long(), new TestLevelOptions { MainCamera = true, ViewWidth = ViewWidth, Abilities = Sim.Abilities.Jump });
            yield return null;
            Transform cam = Level.Camera.transform;
            Assert.AreEqual(Half, cam.position.x, 1e-4f, "clamped at the left end");
            float bottom = Level.Player.Session.Map.Origin.y + Level.Camera.orthographicSize;
            Assert.AreEqual(bottom, cam.position.y, 1e-4f, "clamped at the bottom");

            Keys(Key.RightArrow);
            int followed = 0;
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                float x = Level.Player.transform.position.x;
                if (x > Half + 0.5f && x < 38f)
                {
                    Assert.AreEqual(x, cam.position.x, 1e-3f, $"frame {frame}: follows");
                    followed++;
                }
            }
            Assert.That(followed, Is.GreaterThan(20), "precondition: the player crossed the middle");
            Assert.AreEqual(-10f, cam.position.z, "z is kept");
        }

        [UnityTest]
        public IEnumerator ARespawnCutsTheCameraWithNoFrameInBetween()
        {
            Build(Long(), new TestLevelOptions
            {
                MainCamera = true,
                ViewWidth = ViewWidth,
                BeforeActivate = level => level.Player.Health.OnHazard = OnHazard.RespawnAtCheckpoint,
            });
            yield return null;
            Transform cam = Level.Camera.transform;
            Keys(Key.RightArrow);
            float before = float.NaN;
            bool cut = false;
            for (int frame = 0; frame < 900 && !cut; frame++)
            {
                yield return null;
                if (Level.Player.Session.Current.Teleported)
                {
                    cut = true;
                    Assert.That(before, Is.GreaterThan(30f), "the frame before showed the pit");
                    Assert.AreEqual(Half, cam.position.x, 1e-4f, "this frame shows the start (clamped)");
                }
                before = cam.position.x;
            }
            Assert.IsTrue(cut, "fell in the pit and came back");
        }

        [UnityTest]
        public IEnumerator NoMainCameraMeansNoFollowAndNoWarning()
        {
            var log = new WarningLog();
            Build(Long());
            yield return Frames(30);
            Assert.IsNull(Level.Player.CameraFollow.Camera);
            Assert.IsTrue(Level.Player.enabled, "the Player still runs");
            Assert.AreEqual(0, log.Total, string.Join(" | ", log.Texts));
            log.Dispose();
        }
    }
}
