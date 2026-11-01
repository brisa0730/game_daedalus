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
        PlayStart? pendingStart;
        bool settingsDirty;
        // The toggles as last applied, so an Inspector edit changes only the abilities whose toggle changed
        // (a slider edit leaves a play-from-here or pickup ability set alone).
        Abilities appliedToggles;

        internal Tilemap CollisionTilemap { get => collisionTilemap; set => collisionTilemap = value; }
        internal PlayerMovementSection Movement => movement;
        internal PlayerAbilitiesSection Abilities => abilities;
        internal int DataVersion => dataVersion;
        internal PlayerSession Session => session;

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
#if UNITY_6000_6_OR_NEWER
            // 6000.6 deprecates the FindObjectsSortMode overloads (framework.md).
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude);
#else
            Tilemap[] tilemaps = FindObjectsByType<Tilemap>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
#endif
            for (int i = 0; i < tilemaps.Length; i++)
            {
                if (tilemaps[i].name == CollisionTilemapName)
                {
                    return tilemaps[i];
                }
            }
            return tilemaps.Length == 1 ? tilemaps[0] : null;
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
            Tilemap tilemap = collisionTilemap != null ? collisionTilemap : PickCollisionTilemap();
            Rope[] ropes = RopeCollector.FindSorted();
            PlayStart start = pendingStart ?? DefaultStart;
            pendingStart = null;

            Rect include = StartHitbox(start.Feet);
            if (RopeCollector.TryGetWorldBounds(ropes, out Rect ropeBounds))
            {
                include = Rect.MinMaxRect(
                    Mathf.Min(include.xMin, ropeBounds.xMin), Mathf.Min(include.yMin, ropeBounds.yMin),
                    Mathf.Max(include.xMax, ropeBounds.xMax), Mathf.Max(include.yMax, ropeBounds.yMax));
            }

            var problems = new List<string>();
            CollisionMap map;
            if (tilemap != null)
            {
                map = CollisionMapReader.Read(tilemap, include, problems);
            }
            else
            {
                map = CollisionMapReader.Empty(include);
                problems.Add(PlayerWarnings.NoCollisionTilemap);
            }
            RopeSegment[] segments = RopeCollector.ToSegments(ropes, map.Origin);
            session = new PlayerSession(map, segments, BuildSettings(), start);
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
            WarnRopeTops(ropes, segments, map.Grid);
            WarnPlacement();
            Render();
        }

        void Update()
        {
            RunFrame(Time.deltaTime);
        }

        /// <summary>One frame: live settings, the controls, the due ticks, then the sprite. Allocates nothing.</summary>
        internal void RunFrame(float deltaTime)
        {
            if (settingsDirty)
            {
                settingsDirty = false;
                session.ApplySettings(BuildSettings());
                ApplyChangedToggles();
            }
            controls.ReadFrame();
            int ticks = session.AdvanceClock(deltaTime);
            for (int i = 0; i < ticks; i++)
            {
                session.Tick(controls.TakeTickInput());
            }
            Render();
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
