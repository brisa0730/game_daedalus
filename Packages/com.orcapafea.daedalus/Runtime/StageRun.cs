using System.Collections;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.UI;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// One play of a game made of stages: owns the change between stages, its fade and the deaths of the stages already
    /// played. Exists only once a linked Exit has been reached, so a one-scene game never has one. Each new Player finds
    /// it with one play-start scan.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class StageRun : MonoBehaviour
    {
        // A hitch can't jump the fade: each frame's step is at most this.
        const float MaxFadeStep = 1f / 30f;

        Canvas canvas;
        Image black;

        /// <summary>From an Exit being reached until the new stage is fully showing.</summary>
        internal bool Revealing { get; private set; }

        /// <summary>The deaths of every stage already left.</summary>
        internal int CarriedDeaths { get; private set; }

        /// <summary>The scene where Play began; Play again comes back to it.</summary>
        internal string FirstScene { get; set; }

        /// <summary>The first scene had edits that weren't saved, so a reload of it won't show them.</summary>
        internal bool FirstSceneUnsaved { get; set; }

        /// <summary>The name of the stage that couldn't be opened; null when the last change worked.</summary>
        internal string FailedStage { get; private set; }

        internal bool LoadFailed => FailedStage != null;
        internal float FadeAlpha => black.color.a;
        internal int FullyBlackFrame { get; private set; }
        internal int LoadBeganFrame { get; private set; }

        internal static StageRun Create(string firstScene, bool firstSceneUnsaved)
        {
            var host = new GameObject("Daedalus Stage Run");
            DontDestroyOnLoad(host);
            StageRun run = host.AddComponent<StageRun>();
            run.FirstScene = firstScene;
            run.FirstSceneUnsaved = firstSceneUnsaved;
            run.BuildOverlay();
            return run;
        }

        internal static StageRun Find()
        {
            StageRun[] runs = PlayStartScan.FindActive<StageRun>();
            return runs.Length > 0 ? runs[0] : null;
        }

        /// <summary>Fades out, loads <paramref name="path"/> and fades in; <paramref name="deaths"/> joins the total once the load has started.</summary>
        internal void GoNext(string path, int deaths)
        {
            // One change at a time: a duplicated Player reaches the Exit too.
            if (!Revealing)
            {
                StartCoroutine(Change(path, deaths, false));
            }
        }

        /// <summary>Back to where Play began, with the total at 0; the run ends when the fade has cleared.</summary>
        internal void PlayAgain()
        {
            if (!Revealing)
            {
                StartCoroutine(Change(FirstScene, 0, true));
            }
        }

        /// <summary>A restart in place starts clean: the next Play again tries the first scene again.</summary>
        internal void ClearFailure()
        {
            FailedStage = null;
        }

        void BuildOverlay()
        {
            canvas = gameObject.AddComponent<Canvas>();
            // Covers the HUD by render order: the HUD is on a camera-space canvas, and overlays draw after cameras.
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MaxValue;
            var cover = new GameObject("Fade", typeof(RectTransform));
            var box = (RectTransform)cover.transform;
            box.SetParent(transform, false);
            box.anchorMin = Vector2.zero;
            box.anchorMax = Vector2.one;
            box.offsetMin = Vector2.zero;
            box.offsetMax = Vector2.zero;
            black = cover.AddComponent<Image>();
            black.color = Color.clear;
            black.raycastTarget = false;
            canvas.enabled = false;
        }

        IEnumerator Change(string path, int deaths, bool again)
        {
            FailedStage = null;
            Revealing = true;
            yield return Fade(1f);
            FullyBlackFrame = Time.frameCount;
            // Black must have been shown before the load: in a shared game it blocks the only thread.
            yield return null;
            yield return null;

            LoadBeganFrame = Time.frameCount;
            AsyncOperation loading = StageLoad.Begin(path);
            if (loading == null)
            {
                FailedStage = HelpTexts.StageName(path);
                Debug.LogError($"Couldn't open the stage {FailedStage}.");
                Revealing = false;
                yield return Fade(0f);
                yield break;
            }
            CarriedDeaths = again ? 0 : CarriedDeaths + deaths;
            while (!loading.isDone)
            {
                yield return null;
            }

            // The new Player has started, and its first frame is drawn under the black.
            yield return null;
            yield return null;
            // Skip the load's long frame, or the fade would lose its opening.
            yield return null;
            yield return Fade(0f);
            Revealing = false;
            if (again)
            {
                Destroy(gameObject);
            }
        }

        IEnumerator Fade(float target)
        {
            canvas.enabled = true;
            float alpha = black.color.a;
            while (alpha != target)
            {
                alpha = Mathf.MoveTowards(alpha, target, Mathf.Min(Time.deltaTime, MaxFadeStep) / LevelRules.StageFadeSeconds);
                black.color = new Color(0, 0, 0, alpha);
                yield return null;
            }
            canvas.enabled = target > 0f;
        }
    }
}
