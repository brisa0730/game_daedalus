using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;
using UnityEngine.Tilemaps;
using Object = UnityEngine.Object;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// The Trap's Inspector: the zone shows only for the zone trigger; the trap's story and its warnings in plain
    /// words; Fit zone and Snap to grid buttons; zone handles in the Scene view. No gameplay logic: the story, the box
    /// and every warning text come from the runtime (<see cref="HelpTexts"/>, <see cref="TrapLook"/>, <see cref="PlayerWarnings"/>).
    /// </summary>
    [CustomEditor(typeof(Trap)), CanEditMultipleObjects]
    internal sealed class TrapEditor : UnityEditor.Editor
    {
        SerializedProperty trigger;
        SerializedProperty zone;
        SerializedProperty action;
        SerializedProperty delay;
        SerializedProperty size;
        SerializedProperty tile;
        Tilemap collision;
        readonly BoxBoundsHandle zoneHandle = new BoxBoundsHandle();

        void OnEnable()
        {
            trigger = serializedObject.FindProperty("trigger");
            zone = serializedObject.FindProperty("zone");
            action = serializedObject.FindProperty("action");
            delay = serializedObject.FindProperty("delay");
            size = serializedObject.FindProperty("size");
            tile = serializedObject.FindProperty("tile");
            collision = SceneCollisionTilemap();
            zoneHandle.axes = PrimitiveBoundsHandle.Axes.X | PrimitiveBoundsHandle.Axes.Y;
        }

        public override void OnInspectorGUI()
        {
            var trap = (Trap)target;
            if (EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox(HelpTexts.TrapPlayModeNote, MessageType.Info);
            }
            if (TrapLook.IsIgnored(trap))
            {
                EditorGUILayout.HelpBox(PlayerWarnings.TrapOnExitOrCheckpoint, MessageType.Warning);
            }

            serializedObject.Update();
            Vector2Int[] oldSizes = Sizes();
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(trigger);
            bool zoneShown = trigger.hasMultipleDifferentValues || trigger.enumValueIndex == (int)TrapTrigger.Zone;
            if (zoneShown)
            {
                using (new EditorGUI.DisabledScope(true))
                {
                    EditorGUILayout.PropertyField(zone);
                }
                if (GUILayout.Button("Fit zone around trap"))
                {
                    foreach (Object t in targets)
                    {
                        TrapAuthoring.FitZone((Trap)t);
                    }
                }
            }
            EditorGUILayout.PropertyField(action);
            EditorGUILayout.PropertyField(delay);
            EditorGUILayout.PropertyField(size);
            EditorGUILayout.PropertyField(tile);
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
                EditorGUILayout.HelpBox(HelpTexts.TrapStory(trap), MessageType.Info);
                Warnings(trap);
            }
        }

        void Warnings(Trap trap)
        {
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
            if (trap.Trigger != TrapTrigger.Zone || TrapLook.IsIgnored(trap))
            {
                return;
            }
            Rect world = TrapLook.WorldZone(trap);
            zoneHandle.center = world.center;
            zoneHandle.size = world.size;
            zoneHandle.SetColor(new Color(1f, 0.9f, 0.2f));
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

        // The Collision Tilemap play start will read: the scene's Player's own choice (editor code, found once here).
        static Tilemap SceneCollisionTilemap()
        {
#if UNITY_6000_6_OR_NEWER
            Player[] players = Object.FindObjectsByType<Player>(FindObjectsInactive.Exclude);
#else
            Player[] players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
#endif
            return players.Length > 0 ? players[0].ResolveCollisionTilemap() : Player.PickCollisionTilemap();
        }
    }
}
