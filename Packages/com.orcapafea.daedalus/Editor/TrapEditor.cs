using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// The Trap's Inspector: the zone shows only for the zone trigger, Offset and Speed only for Move, Size and Tile only
    /// off an Exit; the trap's story and its warnings in plain words; the zone typed in whole tiles, Snap zone to grid and Snap to grid buttons; in the Scene
    /// view, zone handles, a Move's end arrow and handle, and a Fall's landing outline. No gameplay logic: the story, the
    /// boxes, the landing and every warning text come from the runtime (<see cref="HelpTexts"/>, <see cref="TrapLook"/>,
    /// <see cref="PlayerWarnings"/>).
    /// </summary>
    [CustomEditor(typeof(Trap)), CanEditMultipleObjects]
    internal sealed class TrapEditor : UnityEditor.Editor
    {
        static readonly Color ZoneColor = new Color(1f, 0.9f, 0.2f);
        static readonly Color PathColor = new Color(0.4f, 0.85f, 1f);
        const string ZoneLabel = "Zone (tiles)";

        SerializedProperty trigger;
        SerializedProperty zone;
        SerializedProperty action;
        SerializedProperty offset;
        SerializedProperty speed;
        SerializedProperty delay;
        SerializedProperty size;
        SerializedProperty tile;
        Tilemap collision;
        Player player;
        readonly BoxBoundsHandle zoneHandle = new BoxBoundsHandle();

        // The Fall landing, recomputed only when the trap's box or action changes or a tile is painted.
        bool landingValid;
        Rect landingFor;
        bool landingFound;
        Rect landing;

        void OnEnable()
        {
            trigger = serializedObject.FindProperty("trigger");
            zone = serializedObject.FindProperty("zone");
            action = serializedObject.FindProperty("action");
            offset = serializedObject.FindProperty("offset");
            speed = serializedObject.FindProperty("speed");
            delay = serializedObject.FindProperty("delay");
            size = serializedObject.FindProperty("size");
            tile = serializedObject.FindProperty("tile");
            player = ScenePlayer();
            collision = player != null ? player.ResolveCollisionTilemap() : Player.PickCollisionTilemap();
            zoneHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
            Tilemap.tilemapTileChanged += OnTilesChanged;
        }

        void OnDisable()
        {
            Tilemap.tilemapTileChanged -= OnTilesChanged;
        }

        void OnTilesChanged(Tilemap tilemap, Tilemap.SyncTile[] tiles)
        {
            landingValid = false;
        }

        public override void OnInspectorGUI()
        {
            var trap = (Trap)target;
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(HelpTexts.TrapPlayModeNote, MessageType.Info);
            }
            if (TrapLook.IsOnCheckpoint(trap))
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapOnCheckpoint, MessageType.Warning);
            }
            else if (TrapLook.IsOnDisabledExit(trap))
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapOnDisabledExit, MessageType.Warning);
            }

            serializedObject.Update();
            Vector2Int[] oldSizes = Sizes();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(trigger);
            bool zoneShown = trigger.hasMultipleDifferentValues || trigger.enumValueIndex == (int)TrapTrigger.Zone;
            if (zoneShown)
            {
                ZoneField(trap);
                if (GUILayout.Button("Snap zone to grid"))
                {
                    foreach (Object t in targets)
                    {
                        TrapAuthoring.SnapZoneToGrid((Trap)t);
                    }
                }
            }
            EditorGUILayout.PropertyField(action);
            // A setting that applies to one mode only shows only in that mode (CLAUDE.md Architecture rule 8).
            if (!action.hasMultipleDifferentValues && action.intValue == (int)TrapAction.Move)
            {
                EditorGUILayout.PropertyField(offset);
                EditorGUILayout.PropertyField(speed);
            }
            EditorGUILayout.PropertyField(delay);
            if (!AnyOnExit())
            {
                EditorGUILayout.PropertyField(size);
                EditorGUILayout.PropertyField(tile);
            }
            if (EditorGUI.EndChangeCheck())
            {
                serializedObject.ApplyModifiedProperties();
                for (int i = 0; i < targets.Length; i++)
                {
                    var changed = (Trap)targets[i];
                    TrapAuthoring.OnSizeChanged(changed, oldSizes[i]);
                    TrapAuthoring.SyncLook(changed);
                }
            }

            EditorGUILayout.Space();
            if (!TrapLook.IsIgnored(trap))
            {
                EditorGUILayout.HelpBox(HelpTexts.TrapStoryInScene(trap, collision, player), MessageType.Info);
                Warnings(trap);
            }
        }

        // The zone in whole tiles from the trap's bottom-left corner; typing gives every selected trap the same zone.
        void ZoneField(Trap active)
        {
            Rect shared = TrapLook.WorldZone(active);
            bool mixed = false;
            var traps = new Trap[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                traps[i] = (Trap)targets[i];
                mixed |= TrapLook.WorldZone(traps[i]) != shared;
            }
            EditorGUI.showMixedValue = mixed;
            EditorGUI.BeginChangeCheck();
            RectInt typed = EditorGUILayout.RectIntField(new GUIContent(ZoneLabel, zone.tooltip), TrapAuthoring.ZoneInTiles(active));
            if (EditorGUI.EndChangeCheck())
            {
                TrapAuthoring.SetZoneInTiles(active, traps, typed);
            }
            EditorGUI.showMixedValue = false;
        }

        void Warnings(Trap trap)
        {
            if (TrapLook.EndCoversPaintedSolid(trap, collision))
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapEndInGround, MessageType.Warning);
            }
            if (TrapLook.IsOnExit(trap))
            {
                // The exit's own look and box: no tile, grid or scale to check.
                return;
            }
            if (TrapLook.CoversPaintedTiles(trap, collision))
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapOverPaintedTiles, MessageType.Warning);
            }
            if (TrapLook.IsOffGrid(trap))
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapOffGrid, MessageType.Warning);
                if (GUILayout.Button("Snap to grid"))
                {
                    foreach (Object t in targets)
                    {
                        TrapAuthoring.SnapToGrid((Trap)t);
                    }
                }
            }
            if (trap.Tile == null)
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapNoTile, MessageType.Warning);
            }
            else if (TrapLook.SpriteOf(trap.Tile) == null)
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapTileNotDrawable, MessageType.Warning);
            }
            else if (!IsFullRect(TrapLook.SpriteOf(trap.Tile)))
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapSpriteNotFullRect, MessageType.Warning);
            }
            Transform t2 = trap.transform;
            if (t2.lossyScale != Vector3.one || t2.rotation != Quaternion.identity)
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapScaled, MessageType.Warning);
            }
        }

        void OnSceneGUI()
        {
            var trap = (Trap)target;
            if (TrapLook.IsIgnored(trap))
            {
                return;
            }
            if (trap.Trigger == TrapTrigger.Zone)
            {
                ZoneHandle(trap);
            }
            if (trap.Action == TrapAction.Move)
            {
                MoveEnd(trap);
            }
            else if (trap.Action == TrapAction.Fall)
            {
                FallOutline(trap);
            }
        }

        void ZoneHandle(Trap trap)
        {
            Rect world = TrapLook.WorldZone(trap);
            zoneHandle.center = world.center;
            zoneHandle.size = world.size;
            zoneHandle.SetColor(ZoneColor);
            EditorGUI.BeginChangeCheck();
            zoneHandle.DrawHandle();
            if (EditorGUI.EndChangeCheck())
            {
                // Snap the edges to whole tiles; the zone is stored relative to the trap's position.
                Vector3 c = zoneHandle.center;
                Vector3 s = zoneHandle.size;
                float xMin = Mathf.Round(c.x - s.x / 2), xMax = Mathf.Round(c.x + s.x / 2);
                float yMin = Mathf.Round(c.y - s.y / 2), yMax = Mathf.Round(c.y + s.y / 2);
                Undo.RecordObject(trap, "Move Trap Zone");
                Vector3 p = trap.transform.position;
                trap.Zone = Rect.MinMaxRect(xMin - p.x, yMin - p.y, Mathf.Max(xMax, xMin + 1) - p.x, Mathf.Max(yMax, yMin + 1) - p.y);
                EditorUtility.SetDirty(trap);
            }
        }

        // An arrow from the box to its end, the end's outline, and a handle on the end that snaps to whole tiles.
        static void MoveEnd(Trap trap)
        {
            Rect box = TrapLook.WorldBox(trap);
            Rect end = TrapLook.EndBox(trap);
            Handles.color = PathColor;
            Arrow(box.center, end.center);
            Outline(end, false);
            float handleSize = HandleUtility.GetHandleSize(end.center) * 0.08f;
            EditorGUI.BeginChangeCheck();
            Vector3 moved = Handles.FreeMoveHandle(end.center, handleSize, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                TrapAuthoring.SetEnd(trap, moved);
            }
        }

        // Where the trap lands (dashed), or a short arrow down off the box when it falls out of the map.
        void FallOutline(Trap trap)
        {
            Rect box = TrapLook.WorldBox(trap);
            if (!landingValid || landingFor != box)
            {
                landingFound = TrapLook.FallLandingBox(trap, collision, player, out landing);
                landingFor = box;
                landingValid = true;
            }
            Handles.color = PathColor;
            if (landingFound)
            {
                Outline(landing, true);
                if (landing.yMin < box.yMin)
                {
                    Arrow(new Vector2(box.center.x, box.yMin), new Vector2(box.center.x, landing.yMax));
                }
                return;
            }
            var from = new Vector2(box.center.x, box.yMin);
            var to = new Vector2(box.center.x, box.yMin - 2f);
            Arrow(from, to);
            Handles.Label(to, "Falls out of the map");
        }

        static void Arrow(Vector2 from, Vector2 to)
        {
            Handles.DrawLine(from, to);
            Vector2 along = to - from;
            if (along.sqrMagnitude < 1e-6f)
            {
                return;
            }
            along.Normalize();
            var side = new Vector2(-along.y, along.x);
            const float Head = 0.3f;
            Handles.DrawLine(to, to - along * Head + side * Head * 0.6f);
            Handles.DrawLine(to, to - along * Head - side * Head * 0.6f);
        }

        static void Outline(Rect r, bool dashed)
        {
            var a = new Vector3(r.xMin, r.yMin);
            var b = new Vector3(r.xMax, r.yMin);
            var c = new Vector3(r.xMax, r.yMax);
            var d = new Vector3(r.xMin, r.yMax);
            if (dashed)
            {
                const float Dash = 4f;
                Handles.DrawDottedLine(a, b, Dash);
                Handles.DrawDottedLine(b, c, Dash);
                Handles.DrawDottedLine(c, d, Dash);
                Handles.DrawDottedLine(d, a, Dash);
                return;
            }
            Handles.DrawLine(a, b);
            Handles.DrawLine(b, c);
            Handles.DrawLine(c, d);
            Handles.DrawLine(d, a);
        }

        bool AnyOnExit()
        {
            foreach (Object t in targets)
            {
                if (TrapLook.IsOnExit((Trap)t))
                {
                    return true;
                }
            }
            return false;
        }

        Vector2Int[] Sizes()
        {
            var sizes = new Vector2Int[targets.Length];
            for (int i = 0; i < targets.Length; i++)
            {
                sizes[i] = ((Trap)targets[i]).Size;
            }
            return sizes;
        }

        static bool IsFullRect(Sprite sprite)
        {
            var importer = AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(sprite.texture)) as TextureImporter;
            if (importer == null)
            {
                return true;
            }
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            return settings.spriteMeshType == SpriteMeshType.FullRect;
        }

        // The scene's Player (editor code, found once here): its Collision Tilemap and its settings for the Fall preview.
        static Player ScenePlayer()
        {
#if UNITY_6000_6_OR_NEWER
            Player[] players = Object.FindObjectsByType<Player>(FindObjectsInactive.Exclude);
#else
            Player[] players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
#endif
            return players.Length > 0 ? players[0] : null;
        }
    }
}
