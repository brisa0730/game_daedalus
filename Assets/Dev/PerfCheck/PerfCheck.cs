using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Tilemaps;

namespace Orcapafea.Daedalus.Dev
{
    /// <summary>
    /// Dev-only WebGL speed test (scaffold plan, Step 6): a full-size 500 x 200 Tilemap in Chunk
    /// mode, 200 moving sprites, and a camera sweeping the whole map width. Measures frame times
    /// after a warm-up and posts the result and a screenshot back to Tools/perf_check.py.
    /// </summary>
    public class PerfCheck : MonoBehaviour
    {
        const int MapWidth = 500;
        const int MapHeight = 200;
        const int EntityCount = 200;
        const float WarmUpSeconds = 3f;
        const float MeasureSeconds = 20f;
        const float CameraHalfHeightTiles = 12f;

        static readonly Color32[] TileColors =
        {
            new Color32(70, 140, 70, 255),
            new Color32(110, 90, 60, 255),
            new Color32(90, 100, 120, 255),
            new Color32(150, 150, 90, 255),
        };

        Camera mainCamera;
        Transform[] entities;
        Vector2[] entityOffsets;
        float[] frameIntervals;
        int frameCount;
        float startTime;
        float measureStart;
        float sweepStartX;
        bool resultsSent;
        string label = "daedalus perf check: warming up";

        [Serializable]
        class Result
        {
            public float fpsMean;
            public float frameIntervalP95Ms;
            public float refreshRate;
            public int tiles;
            public int entities;
            public float sweepTiles;
            public int frames;
            public string unityVersion;
            public int screenWidth;
            public int screenHeight;
        }

        void Start()
        {
            mainCamera = new GameObject("Camera").AddComponent<Camera>();
            mainCamera.orthographic = true;
            mainCamera.orthographicSize = CameraHalfHeightTiles;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = new Color32(20, 24, 40, 255);

            BuildMap();
            BuildEntities();

            frameIntervals = new float[16384];
            startTime = Time.unscaledTime;
            MoveCamera(0f);
        }

        void BuildMap()
        {
            var tiles = new Tile[TileColors.Length];
            for (int i = 0; i < tiles.Length; i++)
            {
                tiles[i] = ScriptableObject.CreateInstance<Tile>();
                tiles[i].sprite = CheckerSprite(TileColors[i]);
            }

            Grid grid = new GameObject("Grid").AddComponent<Grid>();
            var tilemapObject = new GameObject("Tilemap");
            tilemapObject.transform.SetParent(grid.transform);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            tilemapObject.AddComponent<TilemapRenderer>().mode = TilemapRenderer.Mode.Chunk;

            var cells = new TileBase[MapWidth * MapHeight];
            for (int y = 0; y < MapHeight; y++)
            {
                for (int x = 0; x < MapWidth; x++)
                {
                    // Leave ragged gaps so chunks differ, as a painted map would.
                    bool empty = (x * 7 + y * 13) % 11 == 0 || (y > 120 && (x / 9 + y / 5) % 3 == 0);
                    cells[y * MapWidth + x] = empty ? null : tiles[(x / 4 + y / 3) % tiles.Length];
                }
            }
            tilemap.SetTilesBlock(new BoundsInt(0, 0, 0, MapWidth, MapHeight, 1), cells);
        }

        void BuildEntities()
        {
            Sprite sprite = CheckerSprite(new Color32(230, 120, 40, 255));
            entities = new Transform[EntityCount];
            entityOffsets = new Vector2[EntityCount];
            for (int i = 0; i < EntityCount; i++)
            {
                var entity = new GameObject("Entity");
                SpriteRenderer spriteRenderer = entity.AddComponent<SpriteRenderer>();
                spriteRenderer.sprite = sprite;
                spriteRenderer.sortingOrder = 1;
                entities[i] = entity.transform;
                entityOffsets[i] = new Vector2((i % 20) * 2f - 20f, (i / 20) * 2f - 10f);
            }
        }

        void Update()
        {
            float elapsed = Time.unscaledTime - startTime;
            if (elapsed < WarmUpSeconds)
            {
                MoveCamera(0f);
                MoveEntities();
                return;
            }

            if (measureStart == 0f)
            {
                measureStart = Time.unscaledTime;
                sweepStartX = mainCamera.transform.position.x;
                label = "daedalus perf check: measuring";
            }

            float measured = Time.unscaledTime - measureStart;
            if (measured <= MeasureSeconds)
            {
                if (frameCount < frameIntervals.Length)
                {
                    frameIntervals[frameCount++] = Time.unscaledDeltaTime;
                }
                MoveCamera(measured / MeasureSeconds);
                MoveEntities();
                return;
            }

            if (!resultsSent)
            {
                resultsSent = true;
                label = "daedalus perf check: sending";
                StartCoroutine(SendResults(measured));
            }
        }

        void MoveCamera(float progress)
        {
            float x = Mathf.Lerp(-CameraHalfHeightTiles, MapWidth + CameraHalfHeightTiles, progress);
            float y = MapHeight * 0.5f + Mathf.Sin(progress * Mathf.PI * 4f) * (MapHeight * 0.35f);
            mainCamera.transform.position = new Vector3(x, y, -10f);
        }

        void MoveEntities()
        {
            Vector3 center = mainCamera.transform.position;
            float time = Time.unscaledTime;
            for (int i = 0; i < entities.Length; i++)
            {
                Vector2 offset = entityOffsets[i];
                entities[i].position = new Vector3(
                    center.x + offset.x + Mathf.Sin(time * 2f + i) * 1.5f,
                    center.y + offset.y + Mathf.Cos(time * 1.7f + i) * 1.5f,
                    0f);
            }
        }

        IEnumerator SendResults(float measuredSeconds)
        {
            var sorted = new float[frameCount];
            Array.Copy(frameIntervals, sorted, frameCount);
            Array.Sort(sorted);
            int p95Index = Mathf.Clamp(Mathf.CeilToInt(frameCount * 0.95f) - 1, 0, frameCount - 1);

            var result = new Result
            {
                fpsMean = frameCount / measuredSeconds,
                frameIntervalP95Ms = frameCount > 0 ? sorted[p95Index] * 1000f : 0f,
                refreshRate = (float)Screen.currentResolution.refreshRateRatio.value,
                tiles = MapWidth * MapHeight,
                entities = EntityCount,
                sweepTiles = mainCamera.transform.position.x - sweepStartX,
                frames = frameCount,
                unityVersion = Application.unityVersion,
                screenWidth = Screen.width,
                screenHeight = Screen.height,
            };
            label = $"daedalus perf check: done, {result.fpsMean:F1} fps, p95 {result.frameIntervalP95Ms:F1} ms";

            // Measurement is over; show the middle of the map in the picture, not the empty edge.
            MoveCamera(0.5f);
            MoveEntities();
            yield return new WaitForEndOfFrame();
            Texture2D screenshot = ScreenCapture.CaptureScreenshotAsTexture();
            byte[] png = screenshot.EncodeToPNG();

            using (UnityWebRequest post = UnityWebRequest.Post(ServerUrl("result"), JsonUtility.ToJson(result), "application/json"))
            {
                yield return post.SendWebRequest();
            }
            using (var post = new UnityWebRequest(ServerUrl("screenshot"), UnityWebRequest.kHttpVerbPOST))
            {
                post.uploadHandler = new UploadHandlerRaw(png) { contentType = "image/png" };
                post.downloadHandler = new DownloadHandlerBuffer();
                yield return post.SendWebRequest();
            }
        }

        static string ServerUrl(string path)
        {
            return new Uri(new Uri(Application.absoluteURL), path).ToString();
        }

        static Sprite CheckerSprite(Color32 color)
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
            var pixels = new Color32[16 * 16];
            var dark = new Color32((byte)(color.r * 0.8f), (byte)(color.g * 0.8f), (byte)(color.b * 0.8f), 255);
            for (int i = 0; i < pixels.Length; i++)
            {
                bool edge = i % 16 == 0 || i < 16;
                pixels[i] = edge ? dark : color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return Sprite.Create(texture, new Rect(0, 0, 16, 16), Vector2.zero, 16);
        }

        void OnGUI()
        {
            GUI.Label(new Rect(10, 10, 600, 30), label);
        }
    }
}
