using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>The Exit's Inspector: the Next stage slot and what the Exit does.</summary>
    [CustomEditor(typeof(Exit)), CanEditMultipleObjects]
    internal sealed class ExitEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            SerializedProperty nextStage = serializedObject.FindProperty("nextStage");
            NextStageField(nextStage);
            bool mixed = nextStage.hasMultipleDifferentValues;
            ExitLink link = ((Exit)target).Link;
            bool broken = !mixed && link.Kind == ExitLink.LinkKind.Broken;
            if (broken && GUILayout.Button("Clear the missing stage"))
            {
                nextStage.stringValue = "";
            }
            serializedObject.ApplyModifiedProperties();

            EditorGUILayout.HelpBox(mixed ? HelpTexts.ExitsDiffer : HelpTexts.ExitStory(link), broken ? MessageType.Error : MessageType.Info);
            if (PrefabUtility.IsPartOfPrefabAsset(target) || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                EditorGUILayout.HelpBox(HelpTexts.ExitOnPrefabAsset, MessageType.Warning);
            }
        }

        static void NextStageField(SerializedProperty nextStage)
        {
            Rect row = EditorGUILayout.GetControlRect();
            GUIContent label = EditorGUI.BeginProperty(row, new GUIContent("Next stage", nextStage.tooltip), nextStage);
            EditorGUI.showMixedValue = nextStage.hasMultipleDifferentValues;
            string path = AssetDatabase.GUIDToAssetPath(nextStage.stringValue);
            SceneAsset current = string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<SceneAsset>(path);
            EditorGUI.BeginChangeCheck();
            var picked = (SceneAsset)EditorGUI.ObjectField(row, label, current, typeof(SceneAsset), false);
            if (EditorGUI.EndChangeCheck())
            {
                nextStage.stringValue = picked == null ? "" : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(picked));
            }
            EditorGUI.showMixedValue = false;
            EditorGUI.EndProperty();
        }
    }
}
