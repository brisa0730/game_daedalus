using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>Share my game's one question: the title. Shows the scene, the game's stages, any warnings, the title as the gallery will show it, and what to expect.</summary>
    internal sealed class ShareTitleWindow : EditorWindow
    {
        const string FieldName = "ShareTitle";

        string sceneName;
        string chain;
        IReadOnlyList<string> warnings;
        Vector2 warningsScroll;
        string gameTitle;
        Action<string> onShare;
        bool focused;

        internal static void Open(string sceneName, string title, string chain, IReadOnlyList<string> warnings, Action<string> onShare)
        {
            var window = CreateInstance<ShareTitleWindow>();
            window.titleContent = new GUIContent(ShareTexts.Title);
            window.sceneName = sceneName;
            window.chain = chain;
            window.warnings = warnings;
            window.gameTitle = title;
            window.onShare = onShare;
            var size = new Vector2(420, warnings.Count == 0 ? 195 : 320);
            window.minSize = size;
            window.maxSize = size;
            window.ShowModalUtility();
        }

        void OnGUI()
        {
            EditorGUILayout.LabelField(string.Format(ShareTexts.Scene, sceneName), EditorStyles.boldLabel);
            EditorGUILayout.LabelField(string.Format(ShareTexts.YourGame, chain), EditorStyles.wordWrappedLabel);
            if (warnings.Count > 0)
            {
                using (var scroll = new EditorGUILayout.ScrollViewScope(warningsScroll, GUILayout.Height(120)))
                {
                    warningsScroll = scroll.scrollPosition;
                    foreach (string warning in warnings)
                        EditorGUILayout.HelpBox(warning, MessageType.Warning);
                }
            }
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
