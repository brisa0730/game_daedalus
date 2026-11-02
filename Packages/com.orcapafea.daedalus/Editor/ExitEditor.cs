using UnityEditor;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>The Exit's Inspector: what it does.</summary>
    [CustomEditor(typeof(Exit)), CanEditMultipleObjects]
    internal sealed class ExitEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();
            EditorGUILayout.HelpBox(HelpTexts.ExitNotice, MessageType.Info);
        }
    }
}
