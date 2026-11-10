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
using Orcapafea.Daedalus.Sim;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Dev
{
    /// <summary>
    /// Generates the Starter sample (scene, prefabs, tiles, palette) into Assets/StarterBuild; the generator is the
    /// source. Tools/build_starter.py seeds that folder from the committed sample, runs this on 6000.0 and mirrors the
    /// result back. Assets are reused by path, so GUIDs stay stable. Compiles on both editors: no API newer than 6000.0.
    /// </summary>
    public static class StarterBuilder
    {
        const string Folder = "Assets/StarterBuild";
        const string ArtFolder = Folder + "/Art";
        const string TileFolder = Folder + "/Tiles";
        const string PrefabFolder = Folder + "/Prefabs";
        const string ScenePath = Folder + "/Starter.unity";
        const string StagePlayerPath = PrefabFolder + "/Stage Player.prefab";
        const string StageFolder = Folder + "/Stage Example";
        const string StageOnePath = StageFolder + "/Stage 1.unity";
        const string StageTwoPath = StageFolder + "/Stage 2.unity";
        const int StageWidth = 24;
        const string TrapDemoFolder = Folder + "/Trap Demo";
        const string TrapDemoPath = TrapDemoFolder + "/Trap Demo.unity";
        const string PaletteName = "Starter Palette";
        const string FrozenArgument = "-daedalusFrozenList";
        const int PixelsPerUnit = 16;
        const int Margin = 2;
        const float CameraSize = 8f;
        const int Height = 14;
        const int CeilingDrop = 9;

        // The one ground row. Section 1: Fake Floor (10–12), Drop Floor pit (18–20). Section 2: flat, for the Pop Spike
        // and the Falling Ceiling. Section 3: the "your turn" pit (58–60), then the Runaway Exit. 'f' is a None-kind tile.
        static readonly string Ground =
            new string('#', 10) + "fff" + new string('#', 5) + "..." + new string('#', 37) + "..." + new string('#', 15);

        static readonly Vector2 PlayerFeet = new Vector2(1.5f, 1f);
        static readonly Vector2Int[] CheckpointCells = { new Vector2Int(4, 1), new Vector2Int(29, 1), new Vector2Int(52, 1) };
        static readonly Vector2 DropFloorCentre = new Vector2(19.5f, 0.5f);
        static readonly Vector2 PopSpikeCentre = new Vector2(36f, 1.5f);
        static readonly Vector2 CeilingCentre = new Vector2(43.5f, 10.5f);
        static readonly Vector2 RunawayExitCentre = new Vector2(66.5f, 1.5f);
        static readonly Vector2 YourTurnCentre = new Vector2(59.5f, 0.5f);
        static readonly Vector2Int RunawayOffset = new Vector2Int(6, 0);
        static readonly Vector2Int FloorSize = new Vector2Int(3, 1);
        static readonly Vector2Int SpikeSize = new Vector2Int(2, 1);

        // The Trap Demo: the dev trap bench's 120 × 18 layout (TrapTestSceneBuilder), top row first, without its
        // adversarial fixtures. '#' ground, '^' painted spikes, 'f' Fake Floor, 'P' the Player's feet.
        static readonly string[] DemoLayout =
        {
            "........................................................................................................................", // 17
            "........................................................................................................................", // 16
            "........................................................................................................................", // 15
            "........................................................................................................................", // 14
            "........................................................................................................................", // 13
            "........................................................................................................................", // 12
            "........................................................................................................................", // 11
            "........................................................................................................................", // 10
            "........................................................................................................................", //  9
            "........................................................................................................................", //  8
            "........................................................................................................................", //  7
            "........................................................................................................................", //  6
            "........................................................................................................................", //  5
            "......................................................................................................##########........", //  4  corridor ceiling (x 102–111)
            "........................................................................................................................", //  3
            "...................................................................................#....................................", //  2  crusher wall (x 83)
            "..P.................................#......^^......................................#....................................", //  1  hop block (x 36), spike strip (x 43–44), crusher wall (x 83)
            "#####...####...#############fff##############...######################.........###############.....#####################", //  0
        };

        static readonly Vector2[] DemoDropFloors = { new Vector2(6.5f, 0.5f), new Vector2(13.5f, 0.5f), new Vector2(46.5f, 0.5f) };
        static readonly Vector2 DemoPopSpike = new Vector2(21f, 1.5f);
        static readonly Vector2 DemoCeiling = new Vector2(63.5f, 10.5f);
        static readonly Vector2Int[] DemoCheckpoints = { new Vector2Int(40, 1), new Vector2Int(57, 1), new Vector2Int(100, 1) };
        static readonly Vector2 DemoExit = new Vector2(112.5f, 1.5f);

        // The traps with no Starter prefab: name, bottom-left cell, size, action, trigger, hazard, offset, speed, zone
        // (world; null = the default, the box grown up by one tile).
        static readonly DemoTrap[] DemoTraps =
        {
            new DemoTrap("Hidden Block (appears when touched)", 35, 4, 1, 1, TrapAction.Appear, TrapTrigger.Touch, false, Vector2Int.zero, 0, null),
            new DemoTrap("Sliding Floor (moves right)", 70, 0, 3, 1, TrapAction.Move, TrapTrigger.Zone, false, new Vector2Int(6, 0), 4f, null),
            new DemoTrap("Crusher (moves left)", 88, 1, 2, 3, TrapAction.Move, TrapTrigger.Zone, false, new Vector2Int(-4, 0), LevelRules.DefaultMoveSpeed, null),
            new DemoTrap("Fall Floor", 94, 0, 3, 1, TrapAction.Fall, TrapTrigger.Zone, false, Vector2Int.zero, 0, null),
            new DemoTrap("Fast Spikes (shoot left)", 110, 1, 1, 1, TrapAction.Move, TrapTrigger.Zone, true, new Vector2Int(-7, 0), 30f, Rect.MinMaxRect(101, 1, 102, 3)),
        };

        sealed class DemoTrap
        {
            public DemoTrap(string name, int x, int y, int w, int h, TrapAction action, TrapTrigger trigger, bool hazard, Vector2Int offset, float speed, Rect? zone)
            {
                Name = name;
                Cell = new Vector2Int(x, y);
                Size = new Vector2Int(w, h);
                Action = action;
                Trigger = trigger;
                Hazard = hazard;
                Offset = offset;
                Speed = speed;
                Zone = zone ?? Rect.MinMaxRect(x, y, x + w, y + h + LevelRules.DefaultZoneGrowUp);
            }

            public readonly string Name;
            public readonly Vector2Int Cell, Size, Offset;
            public readonly TrapAction Action;
            public readonly TrapTrigger Trigger;
            public readonly bool Hazard;
            public readonly float Speed;
            public readonly Rect Zone;
        }

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

        static readonly HashSet<string> Produced = new HashSet<string>();
        static readonly HashSet<string> Frozen = new HashSet<string>();

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
            // First: a new scene unloads assets loaded before it, and a frozen tile would paint as nothing.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Produced.Clear();
            ReadFrozenList();
            Directory.CreateDirectory(ArtFolder);
            Directory.CreateDirectory(TileFolder);
            Directory.CreateDirectory(PrefabFolder);
            AssetDatabase.Refresh();

            Sprite groundSprite = SaveSprite("Ground", 16, 16, (x, y) => (x * 7 + y * 3) % 11 == 0 ? GroundSpeck : GroundColor);
            Sprite spikeSprite = SaveSprite("Spikes", 16, 16, (x, y) => y < 12 && Mathf.Abs((x % 8) - 3.5f) < (12 - y) / 3f ? SpikeColor : Clear);
            Sprite checkpointSprite = SaveSprite("Checkpoint", 16, 16, (x, y) => x == 3 ? PoleColor : x > 3 && x < 13 && y > 8 && y < 15 ? FlagColor : Clear);
            Sprite exitSprite = SaveSprite("Exit", 16, 16, (x, y) => x < 2 || x > 13 || y > 13 ? DoorFrame : DoorColor);
            Sprite playerSprite = SaveSprite("Player", 12, 24, (x, y) => (x == 8 || x == 9) && (y == 19 || y == 18) ? FaceColor : PlayerColor);

            Tile groundTile = SaveTile<Tile>("Ground Tile", groundSprite);
            DaedalusTile spikeTile = SaveTile<DaedalusTile>("Spike Tile", spikeSprite);
            SetKind(spikeTile, TileCollisionKind.Hazard);
            DaedalusTile fakeFloorTile = SaveTile<DaedalusTile>("Fake Floor Tile", groundSprite);
            SetKind(fakeFloorTile, TileCollisionKind.None);
            SavePalette(groundTile, spikeTile, fakeFloorTile);

            GameObject dropFloor = SaveTrapPrefab("Drop Floor", TrapAction.Vanish, FloorSize, groundTile, groundSprite, DefaultZone(FloorSize));
            GameObject popSpike = SaveTrapPrefab("Pop Spike", TrapAction.Appear, SpikeSize, spikeTile, spikeSprite, DefaultZone(SpikeSize));
            GameObject fallingCeiling = SaveTrapPrefab("Falling Ceiling", TrapAction.Fall, FloorSize, groundTile, groundSprite, CeilingZone());
            GameObject checkpointPrefab = SaveElementPrefab<Checkpoint>("Checkpoint", checkpointSprite);
            SaveElementPrefab<Exit>("Exit", exitSprite);
            GameObject runawayExit = SaveRunawayExitPrefab(exitSprite);

            var grid = new GameObject("Grid").AddComponent<Grid>();
            Tilemap collision = NewTilemap(grid, "Collision");
            PaintGround(collision, groundTile, fakeFloorTile);

            foreach (Vector2Int cell in CheckpointCells)
            {
                Place(checkpointPrefab, new Vector2(cell.x + 0.5f, cell.y + 0.5f));
            }
            Place(dropFloor, DropFloorCentre);
            Place(popSpike, PopSpikeCentre);
            Place(fallingCeiling, CeilingCentre);
            Place(runawayExit, RunawayExitCentre);
            MakeYourTurn(groundSprite);
            MakePlayer(playerSprite, collision, PlayerFeet);
            MakeCamera();
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            SelfCheck(scene);
            EditorSceneManager.SaveScene(scene, ScenePath);
            Produced.Add(ScenePath);
            Produced.Add(StagePlayerPath);
            Produced.Add(StageOnePath);
            Produced.Add(StageTwoPath);
            Produced.Add(TrapDemoPath); // before the cleanup below, or each rebuild would delete the seeded scene
            AssetDatabase.SaveAssets();
            DeleteWhatWasNotProduced();
            string picture = SavePicture(Ground.Length, Height, "starter-picture.png");
            MakeStageExample();
            string demoPicture = MakeTrapDemo();
            Debug.Log($"StarterBuilder: {ScenePath}, {Produced.Count} files; pictures {picture}, {demoPicture}");
        }

        // Two stages in their own folder, the first Exit linked to the second. After the picture: each scene replaces the last.
        static void MakeStageExample()
        {
            Directory.CreateDirectory(StageFolder);
            AssetDatabase.Refresh();
            SaveStagePlayerPrefab();
            SaveStage(StageTwoPath, "", new Color32(44, 28, 44, 255));
            SaveStage(StageOnePath, AssetDatabase.AssetPathToGUID(StageTwoPath), BackgroundColor);
            AssetDatabase.SaveAssets();
        }

        static void SaveStagePlayerPrefab()
        {
            if (Frozen.Contains(StagePlayerPath))
            {
                return;
            }
            GameObject source = NewElement("Stage Player", Load<Sprite>($"{ArtFolder}/Player.png"));
            try
            {
                source.GetComponent<SpriteRenderer>().sortingOrder = 3;
                source.AddComponent<Player>();
                SavePrefab(source, StagePlayerPath);
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        static void SaveStage(string path, string nextStage, Color background)
        {
            // First: a new scene unloads assets loaded before it.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = Load<Tile>($"{TileFolder}/Ground Tile.asset");
            var grid = new GameObject("Grid").AddComponent<Grid>();
            Tilemap collision = NewTilemap(grid, "Collision");
            for (int x = 0; x < StageWidth; x++)
            {
                collision.SetTile(new Vector3Int(x, 0, 0), ground);
            }

            GameObject player = Place(Load<GameObject>(StagePlayerPath), Vector2.zero);
            float pivot = player.GetComponent<SpriteRenderer>().sprite.bounds.min.y;
            player.transform.position = new Vector3(PlayerFeet.x, PlayerFeet.y - pivot, 0);
            GameObject exit = Place(Load<GameObject>($"{PrefabFolder}/Exit.prefab"), new Vector2(StageWidth - 3.5f, 1.5f));
            var link = new SerializedObject(exit.GetComponent<Exit>());
            Property(link, "nextStage").stringValue = nextStage;
            link.ApplyModifiedPropertiesWithoutUndo();

            MakeCamera();
            Require(All<Camera>(scene).Count == 1, "a stage has one camera");
            All<Camera>(scene)[0].backgroundColor = background;
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            Require(All<Player>(scene).Count == 1 && All<Exit>(scene).Count == 1, "a stage has one Player and one Exit");
            Require(path != StageOnePath || !string.IsNullOrEmpty(nextStage), "the first stage links to the second");
            Require(EditorSceneManager.SaveScene(scene, path), $"saved {path}");
        }

        // Every kind of trap in one troll level, from the Starter's own tiles and prefabs; returns its picture's path.
        static string MakeTrapDemo()
        {
            Directory.CreateDirectory(TrapDemoFolder);
            AssetDatabase.Refresh();
            // First: a new scene unloads assets loaded before it.
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var ground = Load<Tile>($"{TileFolder}/Ground Tile.asset");
            var spike = Load<DaedalusTile>($"{TileFolder}/Spike Tile.asset");
            var fakeFloor = Load<DaedalusTile>($"{TileFolder}/Fake Floor Tile.asset");
            var groundSprite = Load<Sprite>($"{ArtFolder}/Ground.png");
            var spikeSprite = Load<Sprite>($"{ArtFolder}/Spikes.png");

            var grid = new GameObject("Grid").AddComponent<Grid>();
            Tilemap collision = NewTilemap(grid, "Collision");
            int height = DemoLayout.Length;
            int width = DemoLayout[0].Length;
            Vector2 start = default;
            for (int row = 0; row < height; row++)
            {
                Require(DemoLayout[row].Length == width, $"demo layout row {row} is {width} wide");
                int y = height - 1 - row;
                for (int x = 0; x < width; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    switch (DemoLayout[row][x])
                    {
                        case '#': collision.SetTile(cell, ground); break;
                        case '^': collision.SetTile(cell, spike); break;
                        case 'f': collision.SetTile(cell, fakeFloor); break;
                        case 'P': start = new Vector2(x + 0.5f, y); break;
                        case '.': break;
                        default: throw new InvalidOperationException($"Unknown demo layout symbol '{DemoLayout[row][x]}' at ({x}, {y}).");
                    }
                }
            }

            var dropFloor = Load<GameObject>($"{PrefabFolder}/Drop Floor.prefab");
            for (int i = 0; i < DemoDropFloors.Length; i++)
            {
                Place(dropFloor, DemoDropFloors[i]).name = $"Drop Floor {(char)('A' + i)}";
            }
            Place(Load<GameObject>($"{PrefabFolder}/Pop Spike.prefab"), DemoPopSpike);
            Place(Load<GameObject>($"{PrefabFolder}/Falling Ceiling.prefab"), DemoCeiling);
            foreach (DemoTrap def in DemoTraps)
            {
                MakeDemoTrap(def, def.Hazard ? (TileBase)spike : ground, def.Hazard ? spikeSprite : groundSprite);
            }
            var checkpoint = Load<GameObject>($"{PrefabFolder}/Checkpoint.prefab");
            foreach (Vector2Int cell in DemoCheckpoints)
            {
                Place(checkpoint, new Vector2(cell.x + 0.5f, cell.y + 0.5f));
            }
            Place(Load<GameObject>($"{PrefabFolder}/Runaway Exit.prefab"), DemoExit);
            MakePlayer(Load<Sprite>($"{ArtFolder}/Player.png"), collision, start);
            MakeCamera();
            All<Camera>(scene)[0].transform.position = new Vector3(start.x, Height / 2f, -10);
            var light = new GameObject("Global Light 2D").AddComponent<Light2D>();
            light.lightType = Light2D.LightType.Global;

            SelfCheckTrapDemo(scene, collision, start);
            Require(EditorSceneManager.SaveScene(scene, TrapDemoPath), $"saved {TrapDemoPath}");
            AssetDatabase.SaveAssets();
            return SavePicture(width, height, "trap-demo-picture.png");
        }

        static void MakeDemoTrap(DemoTrap def, TileBase tile, Sprite sprite)
        {
            var trapObject = new GameObject(def.Name);
            trapObject.transform.position = new Vector3(def.Cell.x + def.Size.x / 2f, def.Cell.y + def.Size.y / 2f, 0);
            Trap trap = trapObject.AddComponent<Trap>();
            Vector3 p = trapObject.transform.position;
            var serialized = new SerializedObject(trap);
            Property(serialized, "trigger").intValue = (int)def.Trigger;
            Property(serialized, "action").intValue = (int)def.Action;
            Property(serialized, "size").vector2IntValue = def.Size;
            if (def.Action == TrapAction.Move)
            {
                Property(serialized, "offset").vector2IntValue = def.Offset;
                Property(serialized, "speed").floatValue = def.Speed;
            }
            Property(serialized, "tile").objectReferenceValue = tile;
            // The zone is stored relative to the trap.
            Property(serialized, "zone").rectValue = new Rect(def.Zone.xMin - p.x, def.Zone.yMin - p.y, def.Zone.width, def.Zone.height);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // The saved look, as the Trap inspector keeps it: the tile tiled across the box; Appear traps hidden.
            SpriteRenderer renderer = trapObject.GetComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = def.Size;
            renderer.enabled = def.Action != TrapAction.Appear;
        }

        static void SelfCheckTrapDemo(Scene scene, Tilemap collision, Vector2 start)
        {
            Require(All<Tilemap>(scene).Count == 1, "the demo has one Tilemap");
            Require(All<Player>(scene).Count == 1, "the demo has one Player");
            Require(All<Exit>(scene).Count == 1, "the demo has one Exit");
            Require(All<Checkpoint>(scene).Count == DemoCheckpoints.Length, "the demo's checkpoints");
            List<Trap> traps = All<Trap>(scene);
            int expected = DemoDropFloors.Length + 2 + DemoTraps.Length + 1; // + Pop Spike, Falling Ceiling; + the exit's
            Require(traps.Count == expected, $"the demo's {expected} traps (found {traps.Count})");

            var actions = new HashSet<int>();
            var triggers = new HashSet<int>();
            var zones = new List<Rect>();
            foreach (Trap trap in traps)
            {
                var serialized = new SerializedObject(trap);
                actions.Add(Property(serialized, "action").intValue);
                triggers.Add(Property(serialized, "trigger").intValue);
                Vector2 p = trap.transform.position;
                Rect zone = Property(serialized, "zone").rectValue;
                zones.Add(new Rect(zone.x + p.x, zone.y + p.y, zone.width, zone.height));
                if (trap.GetComponent<Exit>() != null)
                {
                    continue;
                }
                Vector2Int size = Property(serialized, "size").vector2IntValue;
                Vector2 corner = p - (Vector2)size / 2f;
                Require(Mathf.Approximately(corner.x, Mathf.Round(corner.x)) && Mathf.Approximately(corner.y, Mathf.Round(corner.y)), $"'{trap.name}' sits on whole cells");
                for (int x = 0; x < size.x; x++)
                {
                    for (int y = 0; y < size.y; y++)
                    {
                        var cell = new Vector3Int(Mathf.RoundToInt(corner.x) + x, Mathf.RoundToInt(corner.y) + y, 0);
                        Require(collision.GetTile(cell) == null, $"'{trap.name}' is not over a painted tile at {cell}");
                    }
                }
            }
            foreach (TrapAction action in (TrapAction[])Enum.GetValues(typeof(TrapAction)))
            {
                Require(actions.Contains((int)action), $"the demo has a {action} trap");
            }
            foreach (TrapTrigger trigger in (TrapTrigger[])Enum.GetValues(typeof(TrapTrigger)))
            {
                Require(triggers.Contains((int)trigger), $"the demo has a {trigger} trigger");
            }
            var safe = new List<Vector2> { start + new Vector2(0, 0.5f) };
            foreach (Vector2Int cell in DemoCheckpoints)
            {
                safe.Add(new Vector2(cell.x + 0.5f, cell.y + 0.5f));
            }
            foreach (Vector2 point in safe)
            {
                Require(!zones.Exists(z => z.Contains(point)), $"no trap zone covers the start or a checkpoint at {point}");
            }
        }

        // Once a release is tagged, the pieces any release froze are left exactly as seeded (Step 5): a re-save could change their bytes.
        static void ReadFrozenList()
        {
            Frozen.Clear();
            string[] arguments = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(arguments, FrozenArgument);
            if (at < 0 || at + 1 >= arguments.Length)
            {
                return;
            }
            foreach (string line in File.ReadAllLines(arguments[at + 1]))
            {
                string[] fields = line.Split('\t');
                if (fields.Length == 3 && !fields[0].EndsWith(".meta", StringComparison.Ordinal))
                {
                    Frozen.Add(Folder + "/" + fields[0]);
                }
            }
        }

        static Rect DefaultZone(Vector2Int size)
        {
            return Rect.MinMaxRect(-size.x / 2f, -size.y / 2f, size.x / 2f, size.y / 2f + LevelRules.DefaultZoneGrowUp);
        }

        // A trap's default zone grows upward; a ceiling hanging high needs the column under it instead.
        static Rect CeilingZone()
        {
            return Rect.MinMaxRect(-FloorSize.x / 2f, -FloorSize.y / 2f - CeilingDrop, FloorSize.x / 2f, -FloorSize.y / 2f);
        }

        static GameObject SaveTrapPrefab(string name, TrapAction action, Vector2Int size, TileBase tile, Sprite sprite, Rect zone)
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            Produced.Add(path);
            if (Frozen.Contains(path))
            {
                return Load<GameObject>(path);
            }
            var trapObject = new GameObject(name);
            try
            {
                Trap trap = trapObject.AddComponent<Trap>();
                var serialized = new SerializedObject(trap);
                Property(serialized, "action").intValue = (int)action;
                Property(serialized, "size").vector2IntValue = size;
                Property(serialized, "tile").objectReferenceValue = tile;
                Property(serialized, "zone").rectValue = zone;
                serialized.ApplyModifiedPropertiesWithoutUndo();

                // The saved look, as the Trap inspector keeps it: the tile tiled across the box; Appear traps hidden.
                SpriteRenderer renderer = trapObject.GetComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                renderer.drawMode = SpriteDrawMode.Tiled;
                renderer.size = size;
                renderer.enabled = action != TrapAction.Appear;
                return SavePrefab(trapObject, path);
            }
            finally
            {
                Object.DestroyImmediate(trapObject);
            }
        }

        static GameObject SaveElementPrefab<T>(string name, Sprite sprite) where T : Component
        {
            string path = $"{PrefabFolder}/{name}.prefab";
            Produced.Add(path);
            if (Frozen.Contains(path))
            {
                return Load<GameObject>(path);
            }
            GameObject element = NewElement(name, sprite);
            try
            {
                element.AddComponent<T>();
                return SavePrefab(element, path);
            }
            finally
            {
                Object.DestroyImmediate(element);
            }
        }

        // An Exit carrying a Move trap: the exit keeps its own look, and the zone is the default for a Trap on an Exit.
        static GameObject SaveRunawayExitPrefab(Sprite sprite)
        {
            const string Name = "Runaway Exit";
            string path = $"{PrefabFolder}/{Name}.prefab";
            Produced.Add(path);
            if (Frozen.Contains(path))
            {
                return Load<GameObject>(path);
            }
            GameObject element = NewElement(Name, sprite);
            try
            {
                element.AddComponent<Exit>();
                Trap trap = element.AddComponent<Trap>();
                var serialized = new SerializedObject(trap);
                Property(serialized, "action").intValue = (int)TrapAction.Move;
                Property(serialized, "offset").vector2IntValue = RunawayOffset;
                float grow = LevelRules.DefaultExitZoneGrow;
                Property(serialized, "zone").rectValue = Rect.MinMaxRect(-0.5f - grow, -0.5f - grow, 0.5f + grow, 0.5f + grow);
                serialized.ApplyModifiedPropertiesWithoutUndo();
                return SavePrefab(element, path);
            }
            finally
            {
                Object.DestroyImmediate(element);
            }
        }

        static GameObject NewElement(string name, Sprite sprite)
        {
            var element = new GameObject(name);
            SpriteRenderer spriteRenderer = element.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 1;
            return element;
        }

        static GameObject SavePrefab(GameObject source, string path)
        {
            GameObject saved = PrefabUtility.SaveAsPrefabAsset(source, path, out bool success);
            Require(success && saved != null, $"saved the prefab {path}");
            return saved;
        }

        static GameObject Place(GameObject prefab, Vector2 centre)
        {
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.position = new Vector3(centre.x, centre.y, 0);
            return instance;
        }

        static Tilemap NewTilemap(Grid grid, string name)
        {
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(grid.transform, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
            tilemapRenderer.mode = TilemapRenderer.Mode.Chunk;
            return tilemap;
        }

        static void PaintGround(Tilemap collision, TileBase ground, TileBase fakeFloor)
        {
            for (int x = 0; x < Ground.Length; x++)
            {
                if (Ground[x] != '.')
                {
                    collision.SetTile(new Vector3Int(x, 0, 0), Ground[x] == 'f' ? fakeFloor : ground);
                }
            }
        }

        // Looks like floor but is only a picture: the student's Add Component → Daedalus → Trap makes it a trap.
        static void MakeYourTurn(Sprite ground)
        {
            var yourTurn = new GameObject("Your Turn (add a Trap here)");
            yourTurn.transform.position = new Vector3(YourTurnCentre.x, YourTurnCentre.y, 0);
            SpriteRenderer renderer = yourTurn.AddComponent<SpriteRenderer>();
            renderer.sprite = ground;
            renderer.drawMode = SpriteDrawMode.Tiled;
            renderer.size = FloorSize;
        }

        static void MakePlayer(Sprite sprite, Tilemap collision, Vector2 feet)
        {
            var playerObject = new GameObject("Player");
            SpriteRenderer spriteRenderer = playerObject.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = sprite;
            spriteRenderer.sortingOrder = 3;
            playerObject.transform.position = new Vector3(feet.x, feet.y - sprite.bounds.min.y, 0);
            Player player = playerObject.AddComponent<Player>();
            var serialized = new SerializedObject(player);
            Property(serialized, "collisionTilemap").objectReferenceValue = collision;
            Property(serialized, "health.onHazard").intValue = (int)OnHazard.RespawnAtCheckpoint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void MakeCamera()
        {
            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = CameraSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = BackgroundColor;
            cameraObject.transform.position = new Vector3(PlayerFeet.x, Height / 2f, -10);
        }

        static void SelfCheck(Scene scene)
        {
            Require(All<Tilemap>(scene).Count == 1 && All<Tilemap>(scene)[0].name == "Collision", "exactly one Tilemap, named Collision");
            int painted = All<Tilemap>(scene)[0].GetUsedTilesCount();
            Debug.Log($"StarterBuilder: {painted} kinds of tile painted, bounds {All<Tilemap>(scene)[0].cellBounds}");
            Require(painted == 2, $"ground and Fake Floor are painted (found {painted} kinds of tile)");
            Require(All<Camera>(scene).Count == 1 && All<Camera>(scene)[0].CompareTag("MainCamera"), "one camera tagged MainCamera");
            List<Light2D> lights = All<Light2D>(scene);
            Require(lights.Count == 1 && lights[0].lightType == Light2D.LightType.Global, "one Global Light 2D");

            List<Player> players = All<Player>(scene);
            Require(players.Count == 1 && players[0].enabled, "one enabled Player");
            Require(Property(new SerializedObject(players[0]), "health.onHazard").intValue == (int)OnHazard.RespawnAtCheckpoint, "the Player respawns at checkpoints");

            Require(All<Checkpoint>(scene).Count == CheckpointCells.Length, "a Checkpoint per section");
            Require(All<Exit>(scene).Count == 1, "one Exit: touching any Exit wins, so a second would end the level early");
            List<Trap> traps = All<Trap>(scene);
            Require(traps.Count == 4, $"the four placed traps (found {traps.Count})");
            GameObject yourTurn = Array.Find(scene.GetRootGameObjects(), o => o.name.StartsWith("Your Turn", StringComparison.Ordinal));
            Require(yourTurn != null && yourTurn.GetComponent<Trap>() == null && yourTurn.GetComponent<SpriteRenderer>() != null, "the Your Turn object has a sprite and no Trap");

            Trap ceiling = traps.Find(t => t.name == "Falling Ceiling");
            Require(ceiling != null, "a trap named Falling Ceiling");
            Rect zone = Property(new SerializedObject(ceiling), "zone").rectValue;
            float zoneBottom = ceiling.transform.position.y + zone.yMin;
            Require(Mathf.Approximately(zoneBottom, 1f), $"the Falling Ceiling's zone reaches the floor (bottom {zoneBottom})");
        }

        static List<T> All<T>(Scene scene) where T : Component
        {
            var found = new List<T>();
            foreach (GameObject root in scene.GetRootGameObjects())
            {
                found.AddRange(root.GetComponentsInChildren<T>(true));
            }
            return found;
        }

        // A file left in the seed that the generator no longer makes would otherwise be copied back forever.
        static void DeleteWhatWasNotProduced()
        {
            foreach (string file in Directory.GetFiles(Folder, "*", SearchOption.AllDirectories))
            {
                string path = file.Replace('\\', '/');
                if (!path.EndsWith(".meta", StringComparison.Ordinal) && !Produced.Contains(path))
                {
                    Debug.Log($"StarterBuilder: removing {path}, which the generator no longer makes");
                    Require(AssetDatabase.DeleteAsset(path), $"deleted {path}");
                }
            }
        }

        // A separate wide camera renders the whole map with a margin (the scene's own camera follows the player).
        static string SavePicture(int width, int height, string fileName)
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
                string path = Path.GetFullPath(Path.Combine(Application.dataPath, "..", fileName));
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

        // The Starter's plain art, drawn here once; a file already seeded is left as it is.
        static Sprite SaveSprite(string name, int width, int height, Func<int, int, Color32> pixel)
        {
            string path = $"{ArtFolder}/{name}.png";
            Produced.Add(path);
            if (Frozen.Contains(path))
            {
                return Load<Sprite>(path);
            }
            if (!File.Exists(path))
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
                File.WriteAllBytes(path, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
            }

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
            settings.spritePivot = new Vector2(0.5f, 0.5f);
            settings.spriteMeshType = SpriteMeshType.FullRect;
            importer.SetTextureSettings(settings);
            importer.SaveAndReimport();
            return Load<Sprite>(path);
        }

        static T SaveTile<T>(string name, Sprite sprite) where T : Tile
        {
            string path = $"{TileFolder}/{name}.asset";
            Produced.Add(path);
            if (Frozen.Contains(path))
            {
                return Load<T>(path);
            }
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
            if (Frozen.Contains(AssetDatabase.GetAssetPath(tile)))
            {
                return;
            }
            var serialized = new SerializedObject(tile);
            Property(serialized, "kind").intValue = (int)kind;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        static void SavePalette(params TileBase[] tiles)
        {
            string path = $"{Folder}/{PaletteName}.prefab";
            Produced.Add(path);
            if (Frozen.Contains(path))
            {
                return;
            }
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

        static T Load<T>(string path) where T : Object
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);
            Require(asset != null, $"{path} loads as a {typeof(T).Name}");
            return asset;
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
    }
}
