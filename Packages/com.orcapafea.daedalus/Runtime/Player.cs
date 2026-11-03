using System.Collections.Generic;
using Orcapafea.Daedalus.Sim;
using UnityEngine;
using UnityEngine.Tilemaps;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// Makes this object the player: it runs, jumps and climbs on the Collision Tilemap, starting where it
    /// is placed. Needs only a sprite; no collider, Rigidbody or physics layer.
    /// </summary>
    [AddComponentMenu("Daedalus/Player")]
    [DisallowMultipleComponent]
    public sealed class Player : MonoBehaviour
    {
        internal const int CurrentDataVersion = 1;
        internal const string CollisionTilemapName = "Collision";

        [SerializeField, Tooltip("The Tilemap the player stands on and bumps into. Filled in automatically with the Tilemap named Collision. Changes take effect the next time you press Play.")]
        Tilemap collisionTilemap;

        [SerializeField]
        PlayerMovementSection movement = new PlayerMovementSection();

        [SerializeField]
        PlayerAbilitiesSection abilities = new PlayerAbilitiesSection();

        [SerializeField]
        PlayerHealthSection health = new PlayerHealthSection();

        [SerializeField, HideInInspector]
        int dataVersion = CurrentDataVersion;

        Transform cachedTransform;
        SpriteRenderer spriteRenderer;
        bool hasPhysics;
        float feetOffset;
        Vector2 placedFeet;
        // The Flip X the student set (e.g. for left-facing art); facing left flips it the other way.
        bool authoredFlip;
        bool flipped;

        PlayerControls controls;
        PlayerSession session;
        Checkpoint[] checkpoints;
        int shownCheckpoint = -1;
        SpriteRenderer[] trapRenderers;
        int shownTrapChanges;
        // Each trap's transform, its position at play start (after the Play-mode look) and the offset last drawn.
        Transform[] trapTransforms;
        Vector3[] trapBases;
        Vector2[] shownTrapOffsets;
        // The start used at play start; play again comes back to it.
        PlayStart firstStart;
        CameraFollow cameraFollow;
        PlayHud hud;
        DeathBurst burst;
        float frameDelta;
        PlayStart? pendingStart;
        bool settingsDirty;
        // The toggles as last applied, so an Inspector edit changes only the abilities whose toggle changed
        // (a slider edit leaves a play-from-here or pickup ability set alone).
        Abilities appliedToggles;

        internal Tilemap CollisionTilemap { get => collisionTilemap; set => collisionTilemap = value; }
        internal PlayerMovementSection Movement => movement;
        internal PlayerAbilitiesSection Abilities => abilities;
        internal PlayerHealthSection Health => health;
        internal int DataVersion => dataVersion;
        internal PlayerSession Session => session;
        internal PlayHud Hud => hud;
        internal CameraFollow CameraFollow => cameraFollow;

        /// <summary>Where the player was placed, with the Abilities toggles. Used when nothing calls <see cref="Restart"/> first.</summary>
        public PlayStart DefaultStart => new PlayStart(placedFeet, abilities.ToAbilities());

        /// <summary>
        /// Puts the player at <paramref name="start"/>; its abilities replace the current ones (Jump is always on).
        /// Before play start it becomes the first start. The map is fixed at play start, so a later call is
        /// reliable only for a point inside it (checkpoints are); play-from-here must call it before <c>Start</c>.
        /// </summary>
        public void Restart(PlayStart start)
        {
            if (session == null)
            {
                pendingStart = start;
                return;
            }
            session.Restart(start);
            WarnPlacement();
            Render();
        }

        internal MovementSettings BuildSettings()
        {
            return new MovementSettings
            {
                RunSpeed = movement.RunSpeed,
                TimeToFullSpeed = movement.TimeToFullSpeed,
                JumpHeight = movement.JumpHeight,
                Gravity = movement.Gravity,
                MaxFallSpeed = movement.MaxFallSpeed,
                AirJumps = abilities.AirJumps,
                WallSlideSpeed = abilities.WallSlideSpeed,
                ClimbSpeed = abilities.ClimbSpeed,
            };
        }

        /// <summary>
        /// The Tilemap named Collision, else the only Tilemap, else none. Used by <see cref="Reset"/> and, when the
        /// field is empty, at play start: the rule-5 play-start exception (one scan, never per frame).
        /// </summary>
        internal static Tilemap PickCollisionTilemap()
        {
            return Pick(ScanTilemaps());
        }

        static Tilemap[] ScanTilemaps()
        {
#if UNITY_6000_6_OR_NEWER
            // 6000.6 deprecates the FindObjectsSortMode overloads (framework.md).
            return FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
#else
            return FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif
        }

        static Tilemap Pick(Tilemap[] tilemaps)
        {
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i].name == CollisionTilemapName)
                {
                    return tilemaps[i];
                }
            }
            return tilemaps.Length == 1 ? tilemaps[0] : null;
        }

        /// <summary>The Collision Tilemap play start reads: the field, else <see cref="PickCollisionTilemap"/>. The editor tools use it too.</summary>
        internal Tilemap ResolveCollisionTilemap()
        {
            return collisionTilemap != null ? collisionTilemap : PickCollisionTilemap();
        }

        void Reset()
        {
            collisionTilemap = PickCollisionTilemap();
        }

        void Awake()
        {
            cachedTransform = transform;
            spriteRenderer = GetComponent<SpriteRenderer>();
            hasPhysics = GetComponent<Rigidbody2D>() != null || GetComponent<Collider2D>() != null;
            feetOffset = FeetOffset(spriteRenderer, cachedTransform);
            Vector3 position = cachedTransform.position;
            placedFeet = new Vector2(position.x, position.y + feetOffset);
            authoredFlip = spriteRenderer != null && spriteRenderer.flipX;
            flipped = authoredFlip;
        }

        void OnEnable()
        {
            controls = new PlayerControls();
            controls.Enable();
        }

        void OnDisable()
        {
            controls.Dispose();
            controls = null;
        }

        void Start()
        {
            try
            {
                StartPlay();
            }
            catch
            {
                // Keep the real error the only one: without a session, Update would throw every frame.
                enabled = false;
                throw;
            }
        }

        void StartPlay()
        {
            // One Tilemap scan for the pick and the decoration check (the same rule as ResolveCollisionTilemap).
            Tilemap[] tilemaps = ScanTilemaps();
            Tilemap tilemap = collisionTilemap != null ? collisionTilemap : Pick(tilemaps);
            Rope[] ropes = RopeCollector.FindSorted();
            checkpoints = LevelCollector.FindCheckpoints();
            Exit[] exits = LevelCollector.FindExits();
            Rect[] checkpointBoxes = LevelCollector.Boxes(checkpoints);
            Rect[] exitBoxes = LevelCollector.Boxes(exits);
            Trap[] traps = LevelCollector.FindTraps(out Trap[] ignoredTraps);
            PlayStart start = pendingStart ?? DefaultStart;
            pendingStart = null;

            var problems = new List<string>();
            CollisionMap map = ReadMap(tilemap, start.Feet, ropes, checkpointBoxes, exitBoxes, traps, problems);
            RopeSegment[] segments = RopeCollector.ToSegments(ropes, map.Origin);
            var specs = new TrapSpec[traps.Length];
            for (int i = 0; i < traps.Length; i++)
            {
                // A Trap on an Exit moves that exit: its index in the exit list (an enabled Exit on an active object is in it).
                int exitIndex = TrapLook.IsOnExit(traps[i]) ? System.Array.IndexOf(exits, traps[i].GetComponent<Exit>()) : -1;
                specs[i] = TrapLook.ToSpec(traps[i], map.Origin, exitIndex);
            }
            // Before the session stamps trap cells, so the check reads the grid the stepper snapped the ropes on.
            WarnRopeTops(ropes, segments, map.Grid);
            session = new PlayerSession(map, segments, specs,
                LevelCollector.ToBoxes(checkpointBoxes, map.Origin), LevelCollector.ToBoxes(exitBoxes, map.Origin),
                BuildSettings(), health.ToRule(), start);
            // Before the Play-mode look, which snaps each trap onto the grid.
            WarnLevel(tilemap, tilemaps, traps, ignoredTraps);
            trapRenderers = new SpriteRenderer[traps.Length];
            trapTransforms = new Transform[traps.Length];
            trapBases = new Vector3[traps.Length];
            shownTrapOffsets = new Vector2[traps.Length];
            for (int i = 0; i < traps.Length; i++)
            {
                trapRenderers[i] = traps[i].GetComponent<SpriteRenderer>();
                TrapLook.ApplyPlayLook(traps[i], trapRenderers[i], session.IsTrapPresent(i));
                trapTransforms[i] = traps[i].transform;
                trapBases[i] = trapTransforms[i].position;
            }
            shownTrapChanges = session.TrapChanges;
            firstStart = start;
            // The main camera, found once at play start (the rule-5 play-start exception).
            cameraFollow = new CameraFollow(Camera.main, new Rect(map.Origin, new Vector2(map.Grid.Width, map.Grid.Height)));
            hud = new PlayHud(cameraFollow.Camera);
            burst = new DeathBurst(spriteRenderer);
            appliedToggles = abilities.ToAbilities();

            for (int i = 0; i < problems.Count; i++)
            {
                Debug.LogWarning(problems[i], this);
            }
            if (hasPhysics)
            {
                Debug.LogWarning(PlayerWarnings.HasPhysics, this);
            }
            if (spriteRenderer == null || spriteRenderer.sprite == null)
            {
                Debug.LogWarning(PlayerWarnings.NoSprite, this);
            }
#if !ENABLE_INPUT_SYSTEM
            Debug.LogWarning(PlayerWarnings.OldInputSetting, this);
#endif
            WarnPlacement();
            Render();
        }

        void Update()
        {
            RunFrame(Time.deltaTime);
        }

        /// <summary>One frame: live settings, the controls, the due ticks, play again, then the drawing. Allocates nothing.</summary>
        internal void RunFrame(float deltaTime)
        {
            frameDelta = deltaTime;
            if (settingsDirty)
            {
                settingsDirty = false;
                session.ApplySettings(BuildSettings());
                session.SetRule(health.ToRule());
                ApplyChangedToggles();
            }
            controls.ReadFrame();
            int ticks = session.AdvanceClock(deltaTime);
            for (int i = 0; i < ticks; i++)
            {
                session.Tick(controls.TakeTickInput());
            }
            if (session.Phase == LevelPhase.Won && session.CanPlayAgain && controls.PlayAgainPressed)
            {
                session.Restart(firstStart);
                controls.ClearLatch();
            }
            Render();
        }

        void OnDestroy()
        {
            hud?.Destroy();
            burst?.Destroy();
        }

        void OnValidate()
        {
            // Every setting applies live in Play mode (Unity still throws Play-mode edits away on Stop).
            settingsDirty = true;
        }

        void ApplyChangedToggles()
        {
            Abilities toggles = abilities.ToAbilities();
            Abilities changed = toggles ^ appliedToggles;
            if (changed == 0)
            {
                return;
            }
            appliedToggles = toggles;
            session.ChangeAbilities((session.Current.Abilities & ~changed) | (toggles & changed));
        }

        void Render()
        {
            Vector2 feet = session.InterpolatedFeetWorld;
            cachedTransform.position = new Vector3(feet.x, feet.y - feetOffset, cachedTransform.position.z);
            bool flip = authoredFlip ^ (session.Current.Facing < 0);
            if (spriteRenderer != null && flip != flipped)
            {
                spriteRenderer.flipX = flip;
                flipped = flip;
            }
            if (session.TrapChanges != shownTrapChanges)
            {
                shownTrapChanges = session.TrapChanges;
                for (int i = 0; i < trapRenderers.Length; i++)
                {
                    trapRenderers[i].enabled = session.IsTrapPresent(i);
                }
            }
            for (int i = 0; i < trapTransforms.Length; i++)
            {
                Vector2 offset = session.InterpolatedTrapOffset(i);
                if (offset.x != shownTrapOffsets[i].x || offset.y != shownTrapOffsets[i].y)
                {
                    shownTrapOffsets[i] = offset;
                    Vector3 basePosition = trapBases[i];
                    trapTransforms[i].position = new Vector3(basePosition.x + offset.x, basePosition.y + offset.y, basePosition.z);
                }
            }
            int active = session.ActiveCheckpoint;
            if (active != shownCheckpoint)
            {
                if (shownCheckpoint >= 0)
                {
                    checkpoints[shownCheckpoint].SetReached(false);
                }
                if (active >= 0)
                {
                    checkpoints[active].SetReached(true);
                }
                shownCheckpoint = active;
            }
            burst.Show(session.Phase, session.PhaseTicks, session.InterpolationFraction, session.DeathFeetWorld);
            cameraFollow.Follow(CameraTarget(feet));
            hud.Show(session.Deaths, session.Phase == LevelPhase.Won, session.CanPlayAgain, frameDelta);
        }

        void WarnPlacement()
        {
            StartPlacement placement = session.LastPlacement;
            if (!placement.Found)
            {
                Debug.LogWarning(PlayerWarnings.NoFreeStart, this);
            }
            else if (placement.Moved)
            {
                Debug.LogWarning(PlayerWarnings.MovedStart, this);
            }
        }

        static void WarnLevel(Tilemap collision, Tilemap[] tilemaps, Trap[] traps, Trap[] ignoredTraps)
        {
            for (int i = 0; i < traps.Length; i++)
            {
                Trap trap = traps[i];
                if (TrapLook.EndCoversPaintedSolid(trap, collision))
                {
                    Debug.LogWarning(PlayerWarnings.TrapEndInGround, trap);
                }
                if (TrapLook.IsOnExit(trap))
                {
                    // The exit's own look and box: no tile, grid or scale to check.
                    continue;
                }
                if (TrapLook.CoversPaintedTiles(trap, collision))
                {
                    Debug.LogWarning(PlayerWarnings.TrapOverPaintedTiles, trap);
                }
                if (TrapLook.IsOffGrid(trap))
                {
                    Debug.LogWarning(PlayerWarnings.TrapOffGrid, trap);
                }
                if (trap.Tile == null)
                {
                    Debug.LogWarning(PlayerWarnings.TrapNoTile, trap);
                }
                else if (TrapLook.SpriteOf(trap.Tile) == null)
                {
                    Debug.LogWarning(PlayerWarnings.TrapTileNotDrawable, trap);
                }
                Transform t = trap.transform;
                if (t.lossyScale != Vector3.one || t.rotation != Quaternion.identity)
                {
                    Debug.LogWarning(PlayerWarnings.TrapScaled, trap);
                }
            }
            for (int i = 0; i < ignoredTraps.Length; i++)
            {
                Trap trap = ignoredTraps[i];
                Debug.LogWarning(TrapLook.IsOnCheckpoint(trap) ? PlayerWarnings.TrapOnCheckpoint : PlayerWarnings.TrapOnDisabledExit, trap);
            }
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i] != collision && HasHazardTiles(tilemaps[i]))
                {
                    Debug.LogWarning(PlayerWarnings.HazardOnDecorationTilemap, tilemaps[i]);
                }
            }
        }

        static bool HasHazardTiles(Tilemap tilemap)
        {
            TileBase[] tiles = tilemap.GetTilesBlock(tilemap.cellBounds);
            for (int i = 0; i < tiles.Length; i++)
            {
                if (tiles[i] is DaedalusTile daedalus && daedalus.Kind == TileCollisionKind.Hazard)
                {
                    return true;
                }
            }
            return false;
        }

        static void WarnRopeTops(Rope[] ropes, RopeSegment[] segments, TileGrid grid)
        {
            for (int i = 0; i < segments.Length; i++)
            {
                // The same snap the stepper applied at construction.
                RopeSegment rope = segments[i].SnapTopTo(grid);
                TileKind top = grid.Get(Mathf.FloorToInt(rope.X), Mathf.CeilToInt(rope.Top) - 1);
                if (top == TileKind.Solid || top == TileKind.Climbable)
                {
                    Debug.LogWarning(PlayerWarnings.RopeTopInWall, ropes[i]);
                }
            }
        }

        /// <summary>
        /// The map play start reads: the Collision Tilemap's cells around the start, ropes, checkpoints, exits and traps
        /// (a Move's end too). Without a Tilemap, an empty map of that size and a warning in <paramref name="problems"/>.
        /// </summary>
        internal static CollisionMap ReadMap(Tilemap tilemap, Vector2 feet, Rope[] ropes, Rect[] checkpointBoxes,
            Rect[] exitBoxes, Trap[] traps, List<string> problems)
        {
            Rect include = StartHitbox(feet);
            if (RopeCollector.TryGetWorldBounds(ropes, out Rect ropeBounds))
            {
                include = Rect.MinMaxRect(
                    Mathf.Min(include.xMin, ropeBounds.xMin), Mathf.Min(include.yMin, ropeBounds.yMin),
                    Mathf.Max(include.xMax, ropeBounds.xMax), Mathf.Max(include.yMax, ropeBounds.yMax));
            }
            include = LevelCollector.Grow(LevelCollector.Grow(include, checkpointBoxes), exitBoxes);
            include = LevelCollector.Grow(include, LevelCollector.Boxes(traps));
            // A Move's end is inside the map too.
            include = LevelCollector.Grow(include, LevelCollector.EndBoxes(traps));

            if (tilemap != null)
            {
                return CollisionMapReader.Read(tilemap, include, problems);
            }
            problems.Add(PlayerWarnings.NoCollisionTilemap);
            return CollisionMapReader.Empty(include);
        }

        /// <summary>
        /// The map rectangle and camera target of the game's first frame, as play start will compute them from where the
        /// player is placed; works in Edit mode (the Share screenshot frames through it). An edit-time one-off: it runs the
        /// play-start scans, so never call it per frame.
        /// </summary>
        internal Rect FirstView(out Vector2 cameraTarget)
        {
            Vector3 position = transform.position;
            var feet = new Vector2(position.x, position.y + FeetOffset(GetComponent<SpriteRenderer>(), transform));
            Trap[] traps = LevelCollector.FindTraps(out _);
            CollisionMap map = ReadMap(ResolveCollisionTilemap(), feet, RopeCollector.FindSorted(),
                LevelCollector.Boxes(LevelCollector.FindCheckpoints()), LevelCollector.Boxes(LevelCollector.FindExits()),
                traps, new List<string>());
            cameraTarget = CameraTarget(feet);
            return new Rect(map.Origin, new Vector2(map.Grid.Width, map.Grid.Height));
        }

        static Vector2 CameraTarget(Vector2 feet)
        {
            return new Vector2(feet.x, feet.y + MovementInternals.GripHeight);
        }

        static Rect StartHitbox(Vector2 feet)
        {
            return new Rect(feet.x - MovementInternals.HitboxHalfWidth, feet.y, MovementInternals.HitboxWidth, MovementInternals.HitboxHeight);
        }

        /// <summary>How far the sprite's bottom edge sits from the object's position, in world units (0 without a sprite).</summary>
        static float FeetOffset(SpriteRenderer renderer, Transform t)
        {
            if (renderer == null || renderer.sprite == null)
            {
                return 0f;
            }
            return renderer.sprite.bounds.min.y * t.lossyScale.y;
        }

        void OnDrawGizmosSelected()
        {
            Vector2 feet;
            if (session != null)
            {
                feet = session.InterpolatedFeetWorld;
            }
            else
            {
                Vector3 position = transform.position;
                feet = new Vector2(position.x, position.y + FeetOffset(GetComponent<SpriteRenderer>(), transform));
            }
            Rect box = StartHitbox(feet);
            Gizmos.color = new Color(0.3f, 0.9f, 1f);
            Gizmos.DrawWireCube(box.center, box.size);
        }
    }
}
