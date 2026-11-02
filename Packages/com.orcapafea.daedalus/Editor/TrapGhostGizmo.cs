using UnityEditor;
using UnityEngine;

namespace Orcapafea.Daedalus.Editor
{
    /// <summary>
    /// In the Scene view (never in Play or a build), an Appear trap is drawn as a faded ghost of its tile across its
    /// box, so a hidden trap is visible while building.
    /// </summary>
    internal static class TrapGhostGizmo
    {
        /// <summary>Presentation: the ghost's opacity.</summary>
        const float GhostAlpha = 0.38f;

        // Editor-only drawing resource, made once and never saved.
        static Material ghostMaterial;

        [DrawGizmo(GizmoType.NonSelected | GizmoType.Selected)]
        static void DrawGhost(Trap trap, GizmoType type)
        {
            if (Application.isPlaying || trap.Action != TrapAction.Appear || TrapLook.IsIgnored(trap))
            {
                return;
            }
            Sprite sprite = TrapLook.SpriteOf(trap.Tile);
            if (sprite == null)
            {
                return;
            }
            if (ghostMaterial == null)
            {
                ghostMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.HideAndDontSave };
            }
            Rect box = TrapLook.WorldBox(trap);
            Rect uv = sprite.textureRect;
            Texture texture = sprite.texture;
            var uvMin = new Vector2(uv.xMin / texture.width, uv.yMin / texture.height);
            var uvMax = new Vector2(uv.xMax / texture.width, uv.yMax / texture.height);
            ghostMaterial.mainTexture = texture;
            ghostMaterial.color = new Color(1, 1, 1, GhostAlpha);
            ghostMaterial.SetPass(0);
            GL.PushMatrix();
            GL.Begin(GL.QUADS);
            GL.Color(new Color(1, 1, 1, GhostAlpha));
            for (int y = 0; y < (int)box.height; y++)
            {
                for (int x = 0; x < (int)box.width; x++)
                {
                    float x0 = box.xMin + x, y0 = box.yMin + y;
                    GL.TexCoord2(uvMin.x, uvMin.y);
                    GL.Vertex3(x0, y0, 0);
                    GL.TexCoord2(uvMin.x, uvMax.y);
                    GL.Vertex3(x0, y0 + 1, 0);
                    GL.TexCoord2(uvMax.x, uvMax.y);
                    GL.Vertex3(x0 + 1, y0 + 1, 0);
                    GL.TexCoord2(uvMax.x, uvMin.y);
                    GL.Vertex3(x0 + 1, y0, 0);
                }
            }
            GL.End();
            GL.PopMatrix();
        }
    }
}
