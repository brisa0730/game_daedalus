using UnityEngine;
using UnityEngine.UI;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The death counter (a skull and a number in the top-left corner) and the win screen, built in code at play
    /// start with uGUI; nothing to set up. Text changes only when the number does, from strings cached at start, so
    /// a frame allocates nothing.
    /// </summary>
    internal sealed class PlayHud
    {
        // Presentation only.
        const float ReferenceWidth = 640;
        const float ReferenceHeight = 360;
        const float BumpScale = 1.3f;
        const float BumpSeconds = 0.15f;
        const float DimAlpha = 0.6f;
        // The last entry reads 999+.
        const int CachedCounts = 1001;
        const int TitleSize = 32;
        const int NoticeSize = 16;
        const string WinTitle = "You made it!";
        const string PlayAgainText = "Press any key to play again";

        static readonly string[] Skull =
        {
            "..####..",
            ".######.",
            "##.##.##",
            "##.##.##",
            "########",
            ".##..##.",
            "..####..",
            "..#..#..",
        };

        readonly GameObject root;
        readonly Texture2D skullTexture;
        readonly Sprite skullSprite;
        readonly RectTransform counter;
        readonly Text count;
        readonly GameObject win;
        readonly Text title;
        readonly Text winDeaths;
        readonly Text saveNote;
        readonly GameObject playAgain;
        readonly string[] counts = new string[CachedCounts];
        readonly string[] deathLines = new string[CachedCounts];

        int shownDeaths = -1;
        float bumpLeft;
        bool shownWon;
        bool shownPlayAgain;

        /// <param name="camera">The camera the HUD is drawn with (so screenshots see it); none draws it as an overlay.</param>
        public PlayHud(Camera camera)
        {
            for (int i = 0; i < CachedCounts; i++)
            {
                counts[i] = i < CachedCounts - 1 ? i.ToString() : "999+";
                deathLines[i] = "Deaths: " + counts[i];
            }

            root = new GameObject("Daedalus HUD");
            var canvas = root.AddComponent<Canvas>();
            if (camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = camera.nearClipPlane + 0.1f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.sortingOrder = short.MaxValue;
            var scaler = root.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.matchWidthOrHeight = 1;

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            skullTexture = SkullTexture();
            skullSprite = Sprite.Create(skullTexture, new Rect(0, 0, 8, 8), new Vector2(0.5f, 0.5f), 8);
            counter = Box("Death Counter", root.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(12, -12), new Vector2(80, 20));
            counter.pivot = new Vector2(0, 1);
            RectTransform skull = Box("Skull", counter, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(8, 0), new Vector2(16, 16));
            var skullImage = skull.gameObject.AddComponent<Image>();
            skullImage.sprite = skullSprite;
            count = Label("Count", counter, font, 16, TextAnchor.MiddleLeft);
            RectTransform countBox = count.rectTransform;
            countBox.anchorMin = new Vector2(0, 0);
            countBox.anchorMax = new Vector2(1, 1);
            countBox.offsetMin = new Vector2(20, 0);
            countBox.offsetMax = Vector2.zero;

            win = new GameObject("Win Screen", typeof(RectTransform));
            var winBox = (RectTransform)win.transform;
            winBox.SetParent(root.transform, false);
            Stretch(winBox);
            var dim = win.AddComponent<Image>();
            dim.color = new Color(0, 0, 0, DimAlpha);
            title = Label("Title", winBox, font, TitleSize, TextAnchor.MiddleCenter);
            Place(title.rectTransform, 30);
            title.text = WinTitle;
            winDeaths = Label("Deaths", winBox, font, 20, TextAnchor.MiddleCenter);
            Place(winDeaths.rectTransform, -10);
            Text prompt = Label("Play Again", winBox, font, 14, TextAnchor.MiddleCenter);
            Place(prompt.rectTransform, -50);
            prompt.text = PlayAgainText;
            playAgain = prompt.gameObject;
            playAgain.SetActive(false);
            saveNote = Label("Save Note", winBox, font, 14, TextAnchor.MiddleCenter);
            Place(saveNote.rectTransform, -80);
            saveNote.gameObject.SetActive(false);
            win.SetActive(false);
        }

        public GameObject Root => root;
        internal Text Count => count;
        internal GameObject Win => win;
        internal RectTransform Counter => counter;
        internal Text Title => title;
        internal Text WinDeaths => winDeaths;
        internal Text SaveNote => saveNote;
        internal GameObject PlayAgain => playAgain;

        /// <summary>Shows the counter and, once the exit is reached, the win screen. Call once per frame.</summary>
        public void Show(int deaths, bool won, bool canPlayAgain, float deltaTime)
        {
            if (deaths != shownDeaths)
            {
                if (shownDeaths >= 0 && deaths > shownDeaths)
                {
                    bumpLeft = BumpSeconds;
                }
                shownDeaths = deaths;
                int cached = Mathf.Min(deaths, CachedCounts - 1);
                count.text = counts[cached];
                winDeaths.text = deathLines[cached];
            }
            if (bumpLeft > 0)
            {
                bumpLeft = Mathf.Max(0, bumpLeft - deltaTime);
                float scale = 1 + (BumpScale - 1) * (bumpLeft / BumpSeconds);
                counter.localScale = new Vector3(scale, scale, 1);
            }
            if (won != shownWon)
            {
                shownWon = won;
                win.SetActive(won);
            }
            if (canPlayAgain != shownPlayAgain)
            {
                shownPlayAgain = canPlayAgain;
                playAgain.SetActive(canPlayAgain);
            }
        }

        /// <summary>The win screen says this instead of the win: the next stage couldn't be reached.</summary>
        public void ShowNotice(string notice)
        {
            title.fontSize = NoticeSize;
            title.text = notice;
            winDeaths.text = "";
        }

        /// <summary>Back to the plain win screen, after a restart in place.</summary>
        public void ClearNotice()
        {
            title.fontSize = TitleSize;
            title.text = WinTitle;
            winDeaths.text = deathLines[Mathf.Min(Mathf.Max(shownDeaths, 0), CachedCounts - 1)];
            saveNote.gameObject.SetActive(false);
        }

        /// <summary>One line under Play again, on the win screen only.</summary>
        public void ShowSaveNote(string note)
        {
            saveNote.text = note;
            saveNote.gameObject.SetActive(true);
        }

        public void Destroy()
        {
            Object.Destroy(root);
            Object.Destroy(skullSprite);
            Object.Destroy(skullTexture);
        }

        static Texture2D SkullTexture()
        {
            var texture = new Texture2D(8, 8, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Daedalus Skull" };
            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    texture.SetPixel(x, y, Skull[7 - y][x] == '#' ? Color.white : Color.clear);
                }
            }
            texture.Apply();
            return texture;
        }

        static RectTransform Box(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 position, Vector2 size)
        {
            var box = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
            box.SetParent(parent, false);
            box.anchorMin = anchorMin;
            box.anchorMax = anchorMax;
            box.anchoredPosition = position;
            box.sizeDelta = size;
            return box;
        }

        static Text Label(string name, Transform parent, Font font, int size, TextAnchor alignment)
        {
            var text = new GameObject(name, typeof(RectTransform)).AddComponent<Text>();
            text.transform.SetParent(parent, false);
            text.font = font;
            text.fontSize = size;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        static void Stretch(RectTransform box)
        {
            box.anchorMin = Vector2.zero;
            box.anchorMax = Vector2.one;
            box.offsetMin = Vector2.zero;
            box.offsetMax = Vector2.zero;
        }

        static void Place(RectTransform box, float y)
        {
            box.anchorMin = new Vector2(0.5f, 0.5f);
            box.anchorMax = new Vector2(0.5f, 0.5f);
            box.sizeDelta = new Vector2(400, 40);
            box.anchoredPosition = new Vector2(0, y);
        }
    }
}
