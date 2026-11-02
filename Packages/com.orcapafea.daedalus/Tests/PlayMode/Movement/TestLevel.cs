using System;
using System.Collections.Generic;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Tests
{
    /// <summary>
    /// A PlayMode level built in code from ASCII rows (top row first), with Phase 1's <c>AsciiLevel</c> symbols:
    /// <c>.</c> none, <c>#</c> plain tile, <c>-</c> one-way, <c>C</c> climbable, <c>R</c> rope, <c>+</c> one-way with
    /// a rope, <c>^</c> hazard (spikes), <c>P</c> the Player's feet (cell bottom centre), plus <c>b</c> Background-only
    /// decoration. Traps, checkpoints and exits are added after <see cref="Build"/> (before the Player's Start).
    /// Cell (x, 0) is the bottom row; the Grid sits at the world origin. A camera renders it at 16 px per tile.
    /// </summary>
    internal sealed class TestLevel : IDisposable
    {
        public static readonly Color32 BackgroundColor = new Color32(20, 24, 40, 255);
        public static readonly Color32 SolidColor = new Color32(60, 170, 90, 255);
        public static readonly Color32 OneWayColor = new Color32(200, 200, 60, 255);
        public static readonly Color32 ClimbableColor = new Color32(140, 90, 200, 255);
        public static readonly Color32 RopeColor = new Color32(200, 140, 70, 255);
        public static readonly Color32 DecorationColor = new Color32(60, 80, 120, 255);
        public static readonly Color32 PlayerColor = new Color32(240, 240, 240, 255);
        public static readonly Color32 HazardColor = new Color32(220, 50, 50, 255);
        public static readonly Color32 CheckpointColor = new Color32(80, 160, 230, 255);
        public static readonly Color32 ExitColor = new Color32(250, 140, 30, 255);

        readonly List<Object> created = new List<Object>();
        Material material;

        TestLevel()
        {
        }

        public Grid Grid { get; private set; }
        public Tilemap Collision { get; private set; }
        public Tilemap Background { get; private set; }
        public Player Player { get; private set; }
        public SpriteRenderer PlayerSprite { get; private set; }
        public Camera Camera { get; private set; }
        public List<Rope> Ropes { get; } = new List<Rope>();
        public List<Object> Created => created;
        public int Width { get; private set; }
        public int Height { get; private set; }
        /// <summary>The 'P' cell's feet position in world space.</summary>
        public Vector2 Start { get; private set; }
        public Texture2D LastImage { get; private set; }
        public Tile SolidTile { get; private set; }
        public DaedalusTile HazardTile { get; private set; }

        public static TestLevel Build(string[] rows, TestLevelOptions options = null)
        {
            options = options ?? new TestLevelOptions();
            var level = new TestLevel();
            level.Make(rows, options);
            return level;
        }

        /// <summary>Renders the camera and saves <c>&lt;unity version&gt;-movement-&lt;name&gt;.png</c>; returns the path.</summary>
        public string Shot(string name)
        {
            return ShotAs("movement-" + name);
        }

        /// <summary>Renders the camera and saves <c>&lt;unity version&gt;-traps-&lt;name&gt;.png</c>; returns the path.</summary>
        public string TrapShot(string name)
        {
            return ShotAs("traps-" + name);
        }

        string ShotAs(string fullName)
        {
            Camera.Render();
            LastImage = TestImages.ReadBack(Camera.targetTexture, created);
            return TestImages.Save(LastImage, fullName);
        }

        /// <summary>A Trap whose box is cells (x, y) to (x + w, y + h), drawn with the level's solid or hazard tile.</summary>
        public Trap AddTrap(int x, int y, int w, int h, TrapAction action, bool hazard = false,
            TrapTrigger trigger = TrapTrigger.Zone, Rect? zone = null, float delay = 0,
            Vector2Int? offset = null, float speed = Sim.LevelRules.DefaultMoveSpeed)
        {
            var trapObject = Track(new GameObject(hazard ? "Spike Trap" : "Trap"));
            trapObject.transform.position = new Vector3(x + w / 2f, y + h / 2f, 0);
            Trap trap = trapObject.AddComponent<Trap>();
            trap.GetComponent<SpriteRenderer>().sharedMaterial = material;
            trap.Size = new Vector2Int(w, h);
            trap.Action = action;
            trap.Trigger = trigger;
            trap.Delay = delay;
            trap.Tile = hazard ? (TileBase)HazardTile : SolidTile;
            trap.Zone = zone ?? TrapLook.DefaultZoneLocal(trap.Size);
            if (offset.HasValue)
            {
                trap.Offset = offset.Value;
            }
            trap.Speed = speed;
            return trap;
        }

        /// <summary>A Trap on <paramref name="exit"/>; the zone defaults to the exit's default zone (its box grown on every side).</summary>
        public Trap AddExitTrap(Exit exit, TrapAction action, TrapTrigger trigger = TrapTrigger.Zone, Rect? zone = null, float delay = 0,
            Vector2Int? offset = null, float speed = Sim.LevelRules.DefaultMoveSpeed)
        {
            Trap trap = exit.gameObject.AddComponent<Trap>();
            trap.Action = action;
            trap.Trigger = trigger;
            trap.Delay = delay;
            trap.Zone = zone ?? TrapLook.DefaultZoneLocal(trap);
            if (offset.HasValue)
            {
                trap.Offset = offset.Value;
            }
            trap.Speed = speed;
            return trap;
        }

        /// <summary>A one-tile Checkpoint in cell (x, y).</summary>
        public Checkpoint AddCheckpoint(int x, int y)
        {
            return Element<Checkpoint>("Checkpoint", x, y, CheckpointColor);
        }

        /// <summary>A one-tile Exit in cell (x, y).</summary>
        public Exit AddExit(int x, int y)
        {
            return Element<Exit>("Exit", x, y, ExitColor);
        }

        T Element<T>(string name, int x, int y, Color32 color) where T : Component
        {
            var element = Track(new GameObject(name));
            element.transform.position = new Vector3(x + 0.5f, y + 0.5f, 0);
            var spriteRenderer = element.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = TestImages.SolidSprite(color, 16, 16, new Vector2(0.5f, 0.5f), created);
            spriteRenderer.sharedMaterial = material;
            return element.AddComponent<T>();
        }

        /// <summary>The pixel at a world position in <see cref="LastImage"/>.</summary>
        public Color32 PixelAt(float worldX, float worldY)
        {
            int x = Mathf.FloorToInt(worldX * TestImages.PixelsPerTile);
            int y = Mathf.FloorToInt(worldY * TestImages.PixelsPerTile);
            return LastImage.GetPixel(x, y);
        }

        public void Dispose()
        {
            foreach (Object item in created)
            {
                if (item != null)
                {
                    Object.Destroy(item);
                }
            }
            created.Clear();
        }

        void Make(string[] rows, TestLevelOptions options)
        {
            Height = rows.Length;
            Width = rows[0].Length;
            material = Track(new Material(TestImages.UnlitSpriteShader()));

            Grid = Track(new GameObject("Grid")).AddComponent<Grid>();
            if (options.WithCollisionTilemap)
            {
                Collision = NewTilemap(options.CollisionName, 0);
            }
            if (options.WithBackground)
            {
                Background = NewTilemap("Background", -1);
            }

            Tile solid = PlainTile(SolidColor);
            SolidTile = solid;
            HazardTile = KindTile(TileCollisionKind.Hazard, HazardColor);
            DaedalusTile oneWay = KindTile(TileCollisionKind.OneWay, OneWayColor);
            DaedalusTile climbable = KindTile(TileCollisionKind.Climbable, ClimbableColor);
            Tile decoration = PlainTile(DecorationColor);
            bool started = false;
            for (int row = 0; row < Height; row++)
            {
                int y = Height - 1 - row;
                for (int x = 0; x < Width; x++)
                {
                    var cell = new Vector3Int(x, y, 0);
                    switch (rows[row][x])
                    {
                        case '#':
                            Collision.SetTile(cell, solid);
                            break;
                        case '-':
                        case '+':
                            Collision.SetTile(cell, oneWay);
                            break;
                        case 'C':
                            Collision.SetTile(cell, climbable);
                            break;
                        case '^':
                            Collision.SetTile(cell, HazardTile);
                            break;
                        case 'b':
                            Background.SetTile(cell, decoration);
                            break;
                        case 'P':
                            Start = new Vector2(x + 0.5f, y);
                            started = true;
                            break;
                    }
                }
            }
            if (!started)
            {
                throw new ArgumentException("The level has no 'P'.");
            }
            MakeRopes(rows);
            MakePlayer(options);
            MakeCamera(options);
            options.BeforeActivate?.Invoke(this);
            Player.gameObject.SetActive(true);
        }

        void MakeRopes(string[] rows)
        {
            Sprite ropeSprite = TestImages.SolidSprite(RopeColor, 4, 16, new Vector2(0.5f, 1f), created);
            for (int x = 0; x < Width; x++)
            {
                int top = -1;
                for (int row = 0; row <= Height; row++)
                {
                    bool rope = row < Height && (rows[row][x] == 'R' || rows[row][x] == '+');
                    int y = Height - 1 - row;
                    if (rope && top < 0)
                    {
                        top = y + 1;
                    }
                    else if (!rope && top >= 0)
                    {
                        int bottom = y + 1;
                        Rope r = Track(new GameObject("Rope")).AddComponent<Rope>();
                        r.transform.position = new Vector3(x + 0.5f, top, 0);
                        r.Length = top - bottom;
                        var art = new GameObject("Rope art");
                        art.transform.SetParent(r.transform, false);
                        art.transform.localScale = new Vector3(1, top - bottom, 1);
                        SpriteRenderer spriteRenderer = art.AddComponent<SpriteRenderer>();
                        spriteRenderer.sprite = ropeSprite;
                        spriteRenderer.sharedMaterial = material;
                        Ropes.Add(r);
                        top = -1;
                    }
                }
            }
        }

        void MakePlayer(TestLevelOptions options)
        {
            var playerObject = Track(new GameObject("Player"));
            playerObject.SetActive(false);
            playerObject.transform.localScale = new Vector3(options.PlayerScale, options.PlayerScale, 1);
            float feetOffset = 0f;
            if (options.WithSprite)
            {
                PlayerSprite = playerObject.AddComponent<SpriteRenderer>();
                PlayerSprite.sprite = TestImages.SolidSprite(PlayerColor, 12, 24, options.SpritePivot, created);
                PlayerSprite.sharedMaterial = material;
                PlayerSprite.sortingOrder = 1;
                feetOffset = PlayerSprite.sprite.bounds.min.y * options.PlayerScale;
            }
            playerObject.transform.position = new Vector3(Start.x, Start.y - feetOffset, 0);
            Player = playerObject.AddComponent<Player>();
            if (options.AssignTilemap)
            {
                Player.CollisionTilemap = Collision;
            }
            Player.Abilities.DoubleJump = (options.Abilities & Abilities.DoubleJump) != 0;
            Player.Abilities.WallJump = (options.Abilities & Abilities.WallJump) != 0;
            Player.Abilities.WallClimb = (options.Abilities & Abilities.WallClimb) != 0;
            Player.Abilities.RopeClimb = (options.Abilities & Abilities.RopeClimb) != 0;
        }

        void MakeCamera(TestLevelOptions options)
        {
            int viewWidth = options.ViewWidth > 0 ? options.ViewWidth : Width;
            var target = Track(new RenderTexture(viewWidth * TestImages.PixelsPerTile, Height * TestImages.PixelsPerTile, 24));
            var cameraObject = Track(new GameObject("Level Camera"));
            if (options.MainCamera)
            {
                cameraObject.tag = "MainCamera";
            }
            Camera = cameraObject.AddComponent<Camera>();
            Camera.orthographic = true;
            Camera.orthographicSize = Height / 2f;
            Camera.clearFlags = CameraClearFlags.SolidColor;
            Camera.backgroundColor = BackgroundColor;
            Camera.transform.position = new Vector3(viewWidth / 2f, Height / 2f, -10);
            Camera.targetTexture = target;
        }

        Tilemap NewTilemap(string name, int sortingOrder)
        {
            var tilemapObject = new GameObject(name);
            tilemapObject.transform.SetParent(Grid.transform, false);
            Tilemap tilemap = tilemapObject.AddComponent<Tilemap>();
            TilemapRenderer tilemapRenderer = tilemapObject.AddComponent<TilemapRenderer>();
            tilemapRenderer.mode = TilemapRenderer.Mode.Chunk;
            tilemapRenderer.sharedMaterial = material;
            tilemapRenderer.sortingOrder = sortingOrder;
            return tilemap;
        }

        Tile PlainTile(Color32 color)
        {
            Tile tile = Track(ScriptableObject.CreateInstance<Tile>());
            tile.sprite = TestImages.SolidSprite(color, 16, 16, new Vector2(0.5f, 0.5f), created);
            return tile;
        }

        DaedalusTile KindTile(TileCollisionKind kind, Color32 color)
        {
            DaedalusTile tile = Track(ScriptableObject.CreateInstance<DaedalusTile>());
            tile.Kind = kind;
            tile.sprite = TestImages.SolidSprite(color, 16, 16, new Vector2(0.5f, 0.5f), created);
            return tile;
        }

        T Track<T>(T item) where T : Object
        {
            created.Add(item);
            return item;
        }
    }
}
