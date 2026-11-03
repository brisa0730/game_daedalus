using System;
using UnityEditor;
using UnityEngine;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>Share my game's one question: the title. Shows the scene, the title as the gallery will show it, and what to expect.</summary>
    internal sealed class ShareTitleWindow : EditorWindow
    {
        const string FieldName = "ShareTitle";

        string sceneName;
        string gameTitle;
        Action<string> onShare;
        bool focused;

        internal static void Open(string sceneName, string title, Action<string> onShare)
        {
            var window = CreateInstance<ShareTitleWindow>();
            window.titleContent = new GUIContent(ShareTexts.Title);
            window.sceneName = sceneName;
            window.gameTitle = title;
            window.onShare = onShare;
            var size = new Vector2(420, 170);
            window.minSize = size;
            window.maxSize = size;
            window.ShowModalUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(string.Format(ShareTexts.Scene, sceneName), EditorStyles.boldLabel);
            EditorGUILayout.Space();
            GUI.SetNextControlName(FieldName);
            gameTitle = EditorGUILayout.TextField("Title", gameTitle);
            if (!focused)
            {
                EditorGUI.FocusTextInControl(FieldName);
                focused = true;
            }

            string clean = ShareTitle.Clean(gameTitle);
            if (clean == null)
                EditorGUILayout.LabelField(ShareTexts.EmptyTitle, EditorStyles.miniLabel);
            else if (clean != gameTitle)
                EditorGUILayout.LabelField(string.Format(ShareTexts.GalleryWillShow, clean), EditorStyles.miniLabel);
            else
                EditorGUILayout.LabelField(" ", EditorStyles.miniLabel);
            EditorGUILayout.Space();
            EditorGUILayout.LabelField(ShareTexts.WindowNote, EditorStyles.wordWrappedMiniLabel);

            GUILayout.FlexibleSpace();
            bool enter = Event.current.type == EventType.KeyDown
                && (Event.current.keyCode == KeyCode.Return || Event.current.keyCode == KeyCode.KeypadEnter);
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Cancel", GUILayout.Width(90)))
                    Close();
                using (new EditorGUI.DisabledScope(clean == null))
                {
                    if (GUILayout.Button("Share", GUILayout.Width(90)) || enter && clean != null)
                        ShareAndClose(clean);
                }
            }
        }

        void ShareAndClose(string clean)
        {
            Action<string> share = onShare;
            Close();
            // Never build from inside OnGUI.
            EditorApplication.delayCall += () => share(clean);
        }
    }
}
