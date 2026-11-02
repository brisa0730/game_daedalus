using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Dev
{
    /// <summary>
    /// Generates the dev trap bench (the troll-level test scene), its tiles, sprites and Tile Palette; the generator
    /// is the source, not the scene. Assets are updated in place, so their GUIDs stay stable. Batch mode:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod Orcapafea.Daedalus.Dev.TrapTestSceneBuilder.Build -logFile TestResults/trap-scene.log
    /// </summary>
    public static class TrapTestSceneBuilder
    {
        const string Folder = "Assets/Dev/TrapTest";
        const string ScenePath = Folder + "/TrapTest.unity";
        const string PaletteName = "TrapTest Palette";
        const int PixelsPerUnit = 16;
        const int Margin = 2;
        const float CameraSize = 8f;

        // 60 × 16, top row first: '.' none, '#' ground, '^' painted spikes, 'f' Fake Floor (ground painted on
        // Background over a pit), 'P' the Player's feet. Traps, the checkpoint and the exit are placed below by cell.
        static readonly string[] Layout =
        {
            "............................................................", // 15
            "............................................................", // 14
            "............................................................", // 13
            "............................................................", // 12
            "............................................................", // 11
            "............................................................", // 10
            "............................................................", //  9
            "............................................................", //  8
            "............................................................", //  7
            "............................................................", //  6
            "............................................................", //  5
            "............................................................", //  4
            "............................................................", //  3
            "............................................................", //  2
            "..P.................................#......^^...............", //  1  hop block (x 36), spike strip (x 43–44)
            "#####...####...#############fff##############...############", //  0  pits: Drop Floor A (5–7), B (12–14), Fake Floor (28–30), C (45–47)
        };

        // Traps: x, y (bottom-left cell), width, height, action, hazard, trigger, zone (world rect; null = default), off-grid nudge.
        static readonly TrapDef[] Traps =
        {
            new TrapDef("Drop Floor A (start inside its zone)", 5, 0, 3, 1, TrapAction.Vanish, false, TrapTrigger.Zone, Rect.MinMaxRect(1, 1, 8, 3)),
            new TrapDef("Drop Floor B", 12, 0, 3, 1, TrapAction.Vanish, false, TrapTrigger.Zone, null),
            new TrapDef("Pop Spike", 20, 1, 2, 1, TrapAction.Appear, true, TrapTrigger.Zone, null),
            new TrapDef("Hidden Block", 35, 4, 1, 1, TrapAction.Appear, false, TrapTrigger.Touch, null),
            new TrapDef("Drop Floor C (checkpoint inside its zone)", 45, 0, 3, 1, TrapAction.Vanish, false, TrapTrigger.Zone, Rect.MinMaxRect(39, 1, 48, 3)),
            new TrapDef("Adversarial: over painted tiles", 50, 0, 2, 1, TrapAction.Vanish, false, TrapTrigger.Zone, null),
            new TrapDef("Adversarial: off the grid", 53, 3, 2, 1, TrapAction.Appear, false, TrapTrigger.Zone, null, 0.3f),
        };

        static readonly Vector2Int CheckpointCell = new Vector2Int(40, 1);
        static readonly Vector2Int ExitCell = new Vector2Int(57, 1);

        static readonly Color32 GroundColor = new Color32(70, 150, 80, 255);
        static readonly Color32 GroundSpeck = new Color32(60, 135, 70, 255);
        static readonly Color32 SpikeColor = new Color32(220, 220, 230, 255);
        static readonly Color32 FlagColor = new Color32(240, 240, 240, 255);
        static readonly Color32 PoleColor = new Color32(120, 100, 80, 255);
        static readonly Color32 DoorColor = new Color32(240, 150, 40, 255);
        static readonly Color32 DoorFrame = new Color32(120, 70, 20, 255);
        static readonly Color32 PlayerColor = new Color32(235, 235, 235, 255);
        static readonly Color32 FaceColor = new Color32(30, 30, 40, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color BackgroundColor = new Color32(24, 28, 44, 255);

        sealed class TrapDef
        {
            public TrapDef(string name, int x, int y, int w, int h, TrapAction action, bool hazard, TrapTrigger trigger, Rect? zone, float nudge = 0)
            {
                Name = name;
                X = x;
                Y = y;
                W = w;
                H = h;
                Action = action;
                Hazard = hazard;
                Trigger = trigger;
                Zone = zone;
                Nudge = nudge;
            }

            public readonly string Name;
            public readonly int X, Y, W, H;
            public readonly TrapAction Action;
            public readonly bool Hazard;
            public readonly TrapTrigger Trigger;
            public readonly Rect? Zone;
            public readonly float Nudge;
        }

        [MenuItem("Daedalus Dev/Rebuild Trap Test Scene")]
        public static void Rebuild()
        {
            Generate();
        }

        /// <summary>Batch-mode entry: generates, self-checks and exits 0 (ok) or 1.</summary>
        public static void Build()
        {
            try
            {
                Generate();
                EditorApplication.Exit(0);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                EditorApplication.Exit(1);
            }
        }

        static void Generate()
        {
            Directory.CreateDirectory(Folder);

            // A seamless single-tile ground: no edge pixels, so a trap drawn with it can't be told from the ground.
            Sprite groundSprite = SaveSprite("Ground", 16, 16, (x, y) => (x * 7 + y * 3) % 11 == 0 ? GroundSpeck : GroundColor, new Vector2(0.5f, 0.5f));
            Sprite spikeSprite = SaveSprite("Spikes", 16, 16, (x, y) => y < 12 && Mathf.Abs((x % 8) - 3.5f) < (12 - y) / 3f ? SpikeColor : Clear, new Vector2(0.5f, 0.5f));
            Sprite checkpointSprite = SaveSprite("Checkpoint", 16, 16, (x, y) => x == 3 ? PoleColor : x > 3 && x < 13 && y > 8 && y < 15 ? FlagColor : Clear, new Vector2(0.5f, 0.5f));
            Sprite exitSprite = SaveSprite("Exit", 16, 16, (x, y) => x < 2 || x > 13 || y > 13 ? DoorFrame : DoorColor, new Vector2(0.5f, 0.5f));
            Sprite playerSprite = SaveSprite("Player", 12, 24, (x, y) => (x == 8 || x == 9) && (y == 19 || y == 18) ? FaceColor : PlayerColor, new Vector2(0.5f, 0.5f));

            Tile groundTile = SaveTile<Tile>("Ground Tile", groundSprite);
            DaedalusTile spikeTile = SaveTile<DaedalusTile>("Spike Tile", spikeSprite);
            SetKind(spikeTile, TileCollisionKind.Hazard);
            SavePalette(groundTile, spikeTile);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var grid = new GameObject("Grid").AddComponent<Grid>();
            Tilemap background = NewTilemap(grid, "Background", -1);
            Tilemap collision = NewTilemap(grid, "Collision", 0);
            NewTilemap(grid, "Foreground", 1);

            int height = Layout.Length;
            int width = Layout[0].Length;
            int groundCount = 0, spikeCount = 0, fakeCount = 0;
            Vector2 start = default;
            for (int row = 0; row < height; row++)
            {
                if (Layout[row].Length != width)
                {
                    throw new InvalidOperationException($"Layout row {row} is {Layout[row].Length} wide, not {width}.");
                }
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    switch (Layout[row][x])
                    {
                        case '#':
                            collision.SetTile(cell, groundTile);
                            groundCount++;
                            break;
                        case '^':
                            collision.SetTile(cell, spikeTile);
                            spikeCount++;
                            break;
                        case 'f':
                            background.SetTile(cell, groundTile);
                            fakeCount++;
                            break;
                        case 'P':
                            start = new Vector2(x + 0.5f, y);
                            break;
                        case '.':
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown layout symbol '{Layout[row][x]}' at ({x}, {y}).");
                    }
                }
            }

            foreach (TrapDef def in Traps)
            {
                MakeTrap(def, def.Hazard ? (TileBase)spikeTile : groundTile, def.Hazard ? spikeSprite : groundSprite);
            }
            MakeElement<Checkpoint>("Checkpoint", CheckpointCell, checkpointSprite);
            MakeElement<Exit>("Exit", ExitCell, exitSprite);
            MakePlayer(playerSprite, collision, start);
            MakeCamera(width, height);
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            SelfCheck(collision, background, groundCount, spikeCount, fakeCount);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            string picture = SavePicture(width, height);
            Debug.Log($"TrapTestSceneBuilder: {ScenePath} with {groundCount} ground, {spikeCount} spike and {fakeCount} fake-floor tiles, {Traps.Length} traps; picture {picture}");
        }

        static void MakeTrap(TrapDef def, TileBase tile, Sprite sprite)
        {
            var trapObject = new GameObject(def.Name);
            trapObject.transform.position = new Vector3(def.X + def.W / 2f + def.Nudge, def.Y + def.H / 2f, 0);
            Trap trap = trapObject.AddComponent<Trap>();
            Vector3 p = trapObject.transform.position;

            // The generator can't see internal members; every setting goes through its serialized name.
            var serialized = new SerializedObject(trap);
            Property(serialized, "trigger").intValue = (int)def.Trigger;
            Property(serialized, "action").intValue = (int)def.Action;
            Property(serialized, "size").vector2IntValue = new Vector2Int(def.W, def.H);
            Property(serialized, "tile").objectReferenceValue = tile;
            // The zone is stored relative to the trap; the default is the box grown up by one tile.
            Rect world = def.Zone ?? Rect.MinMaxRect(def.X, def.Y, def.X + def.W, def.Y + def.H + 1);
            Property(serialized, "zone").rectValue = new Rect(world.xMin - p.x, world.yMin - p.y, world.width, world.height);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The saved look, as the Trap inspector keeps it: the tile tiled across the box; Appear traps hidden.
            SpriteRenderer renderer = trapObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = new Vector2(def.W, def.H);
            renderer.enabled = def.Action == TrapAction.Vanish;
        }

        static void MakeElement<T>(string name, Vector2Int cell, Sprite sprite) where T : Component
        {
            var element = new GameObject(name);
            element.transform.position = new Vector3(cell.x + 0.5f, cell.y + 0.5f, 0);
            SpriteRenderer spriteRenderer = element.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 1;
            element.AddComponent<T>();
        }

        static void MakePlayer(Sprite playerSprite, Tilemap collision, Vector2 start)
        {
            var playerObject = new GameObject("Player");
            SpriteRenderer spriteRenderer = playerObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = playerSprite;
            spriteRenderer.sortingOrder = 3;
            playerObject.transform.position = new Vector3(start.x, start.y - playerSprite.bounds.min.y, 0);
            Player player = playerObject.AddComponent<Player>();
            var serialized = new SerializedObject(player);
            Property(serialized, "collisionTilemap").objectReferenceValue = collision;
            Property(serialized, "health.onHazard").intValue = (int)OnHazard.RespawnAtCheckpoint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MakeCamera(int width, int height)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            cameraObject.transform.position = new Vector3(width / 2f, height / 2f, -10);
        }

        static Tilemap NewTilemap(Grid grid, string name, int sortingOrder)
        {
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(grid.transform, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
            tilemapRenderer.mode = TilemapRenderer.Mode.Chunk;
            tilemapRenderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        static void SelfCheck(Tilemap collision, Tilemap background, int groundCount, int spikeCount, int fakeCount)
        {
            int players = Object.FindObjectsByType<Player>(FindObjectsInactive.Include).Length;
            Require(players == 1, $"exactly one Player (found {players})");
            Require(CountTiles(collision, t => !(t is DaedalusTile)) == groundCount && groundCount > 0, $"ground tiles = layout {groundCount}");
            Require(CountTiles(collision, t => t is DaedalusTile) == spikeCount && spikeCount == 2, $"spike tiles = layout {spikeCount}");
            Require(CountTiles(background, t => true) == fakeCount && fakeCount == 3, $"fake-floor tiles = layout {fakeCount}");

            Trap[] traps = Object.FindObjectsByType<Trap>(FindObjectsInactive.Include);
            Require(traps.Length == Traps.Length, $"{Traps.Length} traps (found {traps.Length})");
            foreach (Trap trap in traps)
            {
                TrapDef def = Array.Find(Traps, d => d.Name == trap.name);
                Require(def != null, $"trap '{trap.name}' is in the layout");
                var serialized = new SerializedObject(trap);
                Require(Property(serialized, "action").intValue == (int)def.Action, $"'{trap.name}' action");
                Require(Property(serialized, "trigger").intValue == (int)def.Trigger, $"'{trap.name}' trigger");
                bool hazard = Property(serialized, "tile").objectReferenceValue is DaedalusTile;
                Require(hazard == def.Hazard, $"'{trap.name}' kind");
            }
            Require(Object.FindObjectsByType<Checkpoint>(FindObjectsInactive.Include).Length == 1, "one checkpoint");
            Require(Object.FindObjectsByType<Exit>(FindObjectsInactive.Include).Length == 1, "one exit");
        }

        static int CountTiles(Tilemap tilemap, Func<TileBase, bool> which)
        {
            int count = 0;
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                TileBase tile = tilemap.GetTile(cell);
                if (tile != null && which(tile))
                {
                    count++;
                }
            }
            return count;
        }

        static void Require(bool condition, string what)
        {
            if (!condition)
            {
                throw new InvalidOperationException("Self-check failed: " + what);
            }
        }

        static SerializedProperty Property(SerializedObject serialized, string path)
        {
            SerializedProperty property = serialized.FindProperty(path);
            Require(property != null, $"{serialized.targetObject.GetType().Name} has the field '{path}'");
            return property;
        }

        // A separate wide camera renders the whole map with a margin (the scene's own camera follows the player).
        static string SavePicture(int width, int height)
        {
            int pictureWidth = (width + 2 * Margin) * PixelsPerUnit;
            int pictureHeight = (height + 2 * Margin) * PixelsPerUnit;
            var cameraObject = new GameObject("Picture Camera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = (height + 2 * Margin) / 2f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            cameraObject.transform.position = new Vector3(width / 2f, height / 2f, -10);
            var target = new RenderTexture(pictureWidth, pictureHeight, 24);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(pictureWidth, pictureHeight, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, pictureWidth, pictureHeight), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                string folder = Path.Combine(Application.dataPath, "..", "TestResults");
                Directory.CreateDirectory(folder);
                string path = Path.GetFullPath(Path.Combine(folder, "trap-dev-scene.png"));
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return path;
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
                Object.DestroyImmediate(cameraObject);
            }
        }

        static Sprite SaveSprite(string name, int width, int height, Func<int, int, Color32> pixel, Vector2 pivot)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = pixel(x, y);
                }
            }
            texture.SetPixels32(pixels);
            string path = $"{Folder}/{name}.png";
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport);
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteAlignment = (int)SpriteAlignment.Custom;
            settings.spritePivot = pivot;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }

        static T SaveTile<T>(string name, Sprite sprite) where T : Tile
        {
            string path = $"{Folder}/{name}.asset";
            T tile = AssetDatabase.LoadAssetAtPath<T>(path);
            if (tile == null)
            {
                tile = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(tile, path);
            }
            tile.sprite = sprite;
            EditorUtility.SetDirty(tile);
            return tile;
        }

        static void SetKind(DaedalusTile tile, TileCollisionKind kind)
        {
            var serialized = new SerializedObject(tile);
            Property(serialized, "kind").intValue = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SavePalette(params TileBase[] tiles)
        {
            string path = $"{Folder}/{PaletteName}.prefab";
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                GridPaletteUtility.CreateNewPalette(Folder, PaletteName, GridLayout.CellLayout.Rectangle,
                    GridPalette.CellSizing.Automatic, Vector3.one, GridLayout.CellSwizzle.XYZ);
            }
            GameObject contents = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Tilemap tilemap = contents.GetComponentInChildren<Tilemap>();
                Require(tilemap != null, "the palette has a Tilemap");
                tilemap.ClearAllTiles();
                for (int i = 0; i < tiles.Length; i++)
                {
                    tilemap.SetTile(new Vector3Int(i, 0, 0), tiles[i]);
                }
                PrefabUtility.SaveAsPrefabAsset(contents, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(contents);
            }
        }
    }
}
