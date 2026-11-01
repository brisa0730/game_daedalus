using System;
using System.Collections.Generic;
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
    /// Generates the dev movement test scene, its tiles, sprites and Tile Palette; the generator is the source,
    /// not the scene. Assets are updated in place, so their GUIDs stay stable. Batch mode:
    /// Unity.exe -batchmode -quit -projectPath . -executeMethod Orcapafea.Daedalus.Dev.MovementTestSceneBuilder.Build -logFile TestResults/dev-scene.log
    /// </summary>
    public static class MovementTestSceneBuilder
    {
        const string Folder = "Assets/Dev/MovementTest";
        const string ScenePath = Folder + "/MovementTest.unity";
        const string PaletteName = "MovementTest Palette";
        const int PixelsPerUnit = 16;
        const int PictureWidth = 560;
        const int PictureHeight = 350;
        const float CameraSize = 11f;

        // One screen: 35 × 22 tiles, top row first. Phase 1's AsciiLevel symbols: '.' none, '#' plain tile,
        // '-' one-way, 'C' climbable, 'R' rope, '+' one-way with a rope through it, 'P' the Player's feet;
        // plus 'b' Background-only decoration. Gates match Phase 1's gate tests: ledges 3 and 4 (single jump)
        // and 6 (double jump), gaps 5 and 6 (both drop out of the map), a 2-tall corridor (the start).
        static readonly string[] Layout =
        {
            "...................................", // 21
            "..#..#.............................", // 20  wall pair (chimney 2 wide)
            "..#..#.............................", // 19
            "..#..#.............................", // 18
            "..#..#...##........................", // 17  solid cap over the right climbable
            "..#..#....C........................", // 16
            "..#..#..C.C........................", // 15  climbable with an open top (x 8)
            "..#..#..C.C.......................#", // 14  tall single wall (x 34)
            "..#..#..C.C.......................#", // 13
            "..#..#..C.C.......................#", // 12
            "###########.......................#", // 11  upper shelf
            "..............................----#", // 10  one-way stack
            "...........R......................#", //  9  rope in a 1-wide shaft, next to two walls
            "...........R......................#", //  8
            "...........R..................-+--#", //  7  the stack's rope ends on this one-way
            "...........R##.................R..#", //  6  6-tile ledge
            "...........R##.................R..#", //  5
            ".........##R##................-+--#", //  4  4-tile ledge
            "#####.##.##R##.................R..#", //  3  corridor ceiling, 3-tile ledge
            "......##.##R##.................Rbb#", //  2  decoration to walk through
            ".P....##.##R##.................Rbb#", //  1
            "#################.....##......#####", //  0  gaps 5 (x 17–21) and 6 (x 24–29)
        };

        static readonly Color32 SolidColor = new Color32(70, 150, 80, 255);
        static readonly Color32 SolidEdge = new Color32(40, 100, 50, 255);
        static readonly Color32 OneWayColor = new Color32(220, 190, 70, 255);
        static readonly Color32 ClimbableColor = new Color32(140, 90, 200, 255);
        static readonly Color32 ClimbableStripe = new Color32(100, 60, 150, 255);
        static readonly Color32 DecorationColor = new Color32(50, 70, 110, 255);
        static readonly Color32 RopeColor = new Color32(190, 130, 60, 255);
        static readonly Color32 PlayerColor = new Color32(235, 235, 235, 255);
        static readonly Color32 FaceColor = new Color32(30, 30, 40, 255);
        static readonly Color32 Clear = new Color32(0, 0, 0, 0);
        static readonly Color BackgroundColor = new Color32(24, 28, 44, 255);

        [MenuItem("Daedalus Dev/Rebuild Movement Test Scene")]
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

            Sprite solidSprite = SaveSprite("Solid", 16, 16, (x, y) => x == 0 || y == 0 || x == 15 || y == 15 ? SolidEdge : SolidColor, new Vector2(0.5f, 0.5f));
            Sprite oneWaySprite = SaveSprite("OneWay", 16, 16, (x, y) => y >= 11 ? OneWayColor : Clear, new Vector2(0.5f, 0.5f));
            Sprite climbableSprite = SaveSprite("Climbable", 16, 16, (x, y) => (y / 2) % 2 == 0 ? ClimbableColor : ClimbableStripe, new Vector2(0.5f, 0.5f));
            Sprite decorationSprite = SaveSprite("Decoration", 16, 16, (x, y) => (x + y) % 4 == 0 ? DecorationColor : Clear, new Vector2(0.5f, 0.5f));
            Sprite ropeSprite = SaveSprite("Rope", 4, 16, (x, y) => x == 1 || x == 2 ? RopeColor : Clear, new Vector2(0.5f, 1f));
            // 12 × 24 = the 0.75 × 1.5 hitbox; the face pixels are on the right, the way it faces.
            Sprite playerSprite = SaveSprite("Player", 12, 24, (x, y) => (x == 8 || x == 9) && (y == 19 || y == 18) ? FaceColor : PlayerColor, new Vector2(0.5f, 0.5f));

            Tile solidTile = SaveTile<Tile>("Solid Tile", solidSprite);
            DaedalusTile oneWayTile = SaveTile<DaedalusTile>("One-way Tile", oneWaySprite);
            SetKind(oneWayTile, TileCollisionKind.OneWay);
            DaedalusTile climbableTile = SaveTile<DaedalusTile>("Climbable Tile", climbableSprite);
            SetKind(climbableTile, TileCollisionKind.Climbable);
            Tile decorationTile = SaveTile<Tile>("Decoration Tile", decorationSprite);
            SavePalette(solidTile, oneWayTile, climbableTile, decorationTile);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var grid = new GameObject("Grid").AddComponent<Grid>();
            Tilemap background = NewTilemap(grid, "Background", -1);
            Tilemap collision = NewTilemap(grid, "Collision", 0);
            NewTilemap(grid, "Foreground", 1);

            int height = Layout.Length;
            int width = Layout[0].Length;
            int collisionCount = 0;
            int decorationCount = 0;
            Vector2 start = default;
            var decorationCells = new List<Vector3Int>();
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
                            collision.SetTile(cell, solidTile);
                            collisionCount++;
                            break;
                        case '-':
                        case '+':
                            collision.SetTile(cell, oneWayTile);
                            collisionCount++;
                            break;
                        case 'C':
                            collision.SetTile(cell, climbableTile);
                            collisionCount++;
                            break;
                        case 'b':
                            background.SetTile(cell, decorationTile);
                            decorationCells.Add(cell);
                            decorationCount++;
                            break;
                        case 'P':
                            start = new Vector2(x + 0.5f, y);
                            break;
                        case '.':
                        case 'R':
                            break;
                        default:
                            throw new InvalidOperationException($"Unknown layout symbol '{Layout[row][x]}' at ({x}, {y}).");
                    }
                }
            }

            int ropeCount = MakeRopes(ropeSprite, width, height);
            MakePlayer(playerSprite, collision, start);
            Camera camera = MakeCamera(width, height);
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            SelfCheck(collision, background, collisionCount, decorationCount, decorationCells, ropeCount);
            EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            string picture = SavePicture(camera);
            Debug.Log($"MovementTestSceneBuilder: {ScenePath} with {collisionCount} collision tiles, {ropeCount} ropes, {decorationCount} decoration tiles; picture {picture}");
        }

        static int MakeRopes(Sprite ropeSprite, int width, int height)
        {
            int count = 0;
            for (int x = 0; x < width; x++)
            {
                int top = -1;
                for (int row = 0; row <= height; row++)
                {
                    bool rope = row < height && (Layout[row][x] == 'R' || Layout[row][x] == '+');
                    int y = height - 1 - row;
                    if (rope && top < 0)
                    {
                        top = y + 1;
                    }
                    else if (!rope && top >= 0)
                    {
                        int bottom = y + 1;
                        var ropeObject = new GameObject("Rope");
                        ropeObject.transform.position = new Vector3(x + 0.5f, top, 0);
                        Rope component = ropeObject.AddComponent<Rope>();
                        var serialized = new SerializedObject(component);
                        serialized.FindProperty("length").floatValue = top - bottom;
                        serialized.ApplyModifiedPropertiesWithoutUndo();

                        var art = new GameObject("Rope art");
                        art.transform.SetParent(ropeObject.transform, false);
                        SpriteRenderer spriteRenderer = art.AddComponent<SpriteRenderer>();
                        spriteRenderer.sprite = ropeSprite;
                        spriteRenderer.drawMode = SpriteDrawMode.Tiled;
                        spriteRenderer.size = new Vector2(0.25f, top - bottom);
                        spriteRenderer.sortingOrder = 2;
                        count++;
                        top = -1;
                    }
                }
            }
            return count;
        }

        static void MakePlayer(Sprite playerSprite, Tilemap collision, Vector2 start)
        {
            var playerObject = new GameObject("Player");
            SpriteRenderer spriteRenderer = playerObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = playerSprite;
            spriteRenderer.sortingOrder = 3;
            playerObject.transform.position = new Vector3(start.x, start.y - playerSprite.bounds.min.y, 0);
            Player player = playerObject.AddComponent<Player>();

            // The generator can't see internal members; every setting goes through its serialized name.
            var serialized = new SerializedObject(player);
            Property(serialized, "collisionTilemap").objectReferenceValue = collision;
            Property(serialized, "abilities.doubleJump").boolValue = true;
            Property(serialized, "abilities.wallJump").boolValue = true;
            Property(serialized, "abilities.wallClimb").boolValue = true;
            Property(serialized, "abilities.ropeClimb").boolValue = true;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static Camera MakeCamera(int width, int height)
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            cameraObject.transform.position = new Vector3(width / 2f, height / 2f, -10);
            return camera;
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

        static void SelfCheck(Tilemap collision, Tilemap background, int collisionCount, int decorationCount, List<Vector3Int> decorationCells, int ropeCount)
        {
            int players = Object.FindObjectsByType<Player>(FindObjectsInactive.Include).Length;
            Require(players == 1, $"exactly one Player (found {players})");
            Require(collision.GetUsedTilesCount() > 0 && CountTiles(collision) == collisionCount, $"collision tiles {CountTiles(collision)} = layout {collisionCount}");
            Require(CountTiles(background) == decorationCount && decorationCount > 0, $"background tiles {CountTiles(background)} = layout {decorationCount}");
            foreach (Vector3Int cell in decorationCells)
            {
                Require(collision.GetTile(cell) == null, $"no collision tile under decoration at {cell}");
            }
            int ropes = Object.FindObjectsByType<Rope>(FindObjectsInactive.Include).Length;
            Require(ropes == ropeCount && ropeCount == 2, $"ropes {ropes} = layout {ropeCount} (expected 2)");
        }

        static int CountTiles(Tilemap tilemap)
        {
            int count = 0;
            foreach (Vector3Int cell in tilemap.cellBounds.allPositionsWithin)
            {
                if (tilemap.GetTile(cell) != null)
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

        static string SavePicture(Camera camera)
        {
            var target = new RenderTexture(PictureWidth, PictureHeight, 24);
            try
            {
                camera.targetTexture = target;
                camera.Render();
                RenderTexture previous = RenderTexture.active;
                RenderTexture.active = target;
                var image = new Texture2D(PictureWidth, PictureHeight, TextureFormat.RGBA32, false);
                image.ReadPixels(new Rect(0, 0, PictureWidth, PictureHeight), 0, 0);
                image.Apply();
                RenderTexture.active = previous;
                string folder = Path.Combine(Application.dataPath, "..", "TestResults");
                Directory.CreateDirectory(folder);
                string path = Path.GetFullPath(Path.Combine(folder, "movement-dev-scene.png"));
                File.WriteAllBytes(path, image.EncodeToPNG());
                Object.DestroyImmediate(image);
                return path;
            }
            finally
            {
                camera.targetTexture = null;
                Object.DestroyImmediate(target);
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
