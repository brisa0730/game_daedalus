#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// Opens the imported Starter's Trap Demo with the real Player and keyboard: it starts with nothing to warn about,
    /// and the Player runs into the first section. Finishing it is the operator's play-check. It runs only where the
    /// sample has been imported (Tools/install_check.py does, and fails if this was ignored).
    /// </summary>
    public class StarterTrapDemoTests : SavedScenePlayTest
    {
        readonly List<string> logged = new List<string>();

        void Heard(string message, string stackTrace, LogType type)
        {
            if (type != LogType.Log)
            {
                logged.Add($"{type}: {message}");
            }
        }

        [UnityTest]
        public IEnumerator TheTrapDemoStartsCleanAndPlays()
        {
            string path = AssetDatabase.FindAssets("\"Trap Demo\" t:Scene", new[] { "Assets" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .FirstOrDefault(p => p.StartsWith("Assets/Samples/", StringComparison.Ordinal) && p.EndsWith("/Trap Demo/Trap Demo.unity", StringComparison.Ordinal));
            if (path == null)
            {
                Assert.Ignore("The Starter sample isn't imported in this project.");
            }
            Application.logMessageReceived += Heard;
            try
            {
                yield return LoadScene(path);
                yield return Frames(5);
            }
            finally
            {
                Application.logMessageReceived -= Heard;
            }
            Assert.IsEmpty(logged, "The Trap Demo starts with no warning or error.");
            Assert.AreEqual(11, Session.TrapCount, "Ten traps and the runaway exit's, none dropped by a warning.");

            Keys(Key.RightArrow);
            yield return Until(() => X > 5f || Session.Deaths > 0, "the Player running into the first section");
            Keys();

            Camera camera = Camera.main;
            Assert.IsNotNull(camera, "The Trap Demo has a main camera.");
            var target = new RenderTexture(640, 360, 24);
            Created.Add(target);
            camera.targetTexture = target;
            camera.Render();
            camera.targetTexture = null;
            TestImages.Save(TestImages.ReadBack(target, Created), "starter-trap-demo");
        }
    }
}
#endif
