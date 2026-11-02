using UnityEditor;
using UnityEngine;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>The Checkpoint's Inspector: what it does, and a warning when the Player's On hazard ignores checkpoints.</summary>
    [CustomEditor(typeof(Checkpoint)), CanEditMultipleObjects]
    internal sealed class CheckpointEditor : UnityEditor.Editor
    {
        Player player;

        void OnEnable()
        {
            // Editor code, found once here: the scene's Player decides whether checkpoints are used.
#if UNITY_6000_6_OR_NEWER
            Player[] players = Object.FindObjectsByType<Player>(FindObjectsInactive.Exclude);
#else
            Player[] players = Object.FindObjectsByType<Player>(FindObjectsSortMode.None);
#endif
            player = players.Length > 0 ? players[0] : null;
        }

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(HelpTexts.CheckpointNotice, MessageType.Info);
            if (player != null && player.Health.OnHazard == OnHazard.ReturnToSafeGround)
            {
                EditorGUILayout.HelpBox(HelpTexts.CheckpointNoEffect, MessageType.Warning);
            }
        }
    }
}
