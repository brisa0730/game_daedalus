using Orcapafea.Daedalus.Sim;
using UnityEngine;

namespace Orcapafea.Daedalus
{
    /// <summary>
    /// The death beat's look: the player blinks for the first half of the beat, then is gone while a burst of
    /// pooled bits flies out from where it died and fades. Built at play start; draws only sim state.
    /// </summary>
    internal sealed class DeathBurst
    {
        // Presentation only.
        const int Bits = 8;
        const float Speed = 5f;
        const int BlinkTicks = 3;
        const float CentreHeight = MovementInternals.HitboxHeight / 2f;

        readonly SpriteRenderer player;
        readonly GameObject root;
        readonly Texture2D texture;
        readonly Sprite sprite;
        readonly Transform[] bitTransforms = new Transform[Bits];
        readonly SpriteRenderer[] bitRenderers = new SpriteRenderer[Bits];
        readonly Vector2[] directions = new Vector2[Bits];
        bool showing;

        /// <param name="player">The player's sprite (may be null); the bits share its material and sorting.</param>
        public DeathBurst(SpriteRenderer player)
        {
            this.player = player;
            root = new GameObject("Daedalus Death Burst");
            texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, name = "Daedalus Burst" };
            texture.SetPixels(new[] { Color.white, Color.white, Color.white, Color.white });
            texture.Apply();
            sprite = Sprite.Create(texture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0.5f), 16);
            for (int i = 0; i < Bits; i++)
            {
                var bit = new GameObject("Bit");
                bit.transform.SetParent(root.transform, false);
                var renderer = bit.AddComponent<SpriteRenderer>();
                renderer.sprite = sprite;
                if (player != null)
                {
                    renderer.sharedMaterial = player.sharedMaterial;
                    renderer.sortingLayerID = player.sortingLayerID;
                    renderer.sortingOrder = player.sortingOrder + 1;
                }
                renderer.enabled = false;
                bitTransforms[i] = bit.transform;
                bitRenderers[i] = renderer;
                float angle = i * Mathf.PI * 2 / Bits;
                directions[i] = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            }
        }

        /// <summary>Draws the beat for the current phase; call once per frame after the ticks.</summary>
        public void Show(LevelPhase phase, int phaseTicks, double fraction, Vector2 deathFeetWorld)
        {
            if (phase != LevelPhase.Dying)
            {
                if (showing)
                {
                    showing = false;
                    for (int i = 0; i < Bits; i++)
                    {
                        bitRenderers[i].enabled = false;
                    }
                    if (player != null)
                    {
                        player.enabled = true;
                    }
                }
                return;
            }
            showing = true;
            float elapsed = LevelRules.DeathBeatTicks - phaseTicks + (float)fraction;
            float t = Mathf.Clamp01(elapsed / LevelRules.DeathBeatTicks);
            if (player != null)
            {
                player.enabled = t < 0.5f && ((int)(elapsed / BlinkTicks) & 1) == 0;
            }
            float distance = Speed * elapsed * MovementInternals.TickSeconds;
            var centre = new Vector2(deathFeetWorld.x, deathFeetWorld.y + CentreHeight);
            var colour = new Color(1, 1, 1, 1 - t);
            for (int i = 0; i < Bits; i++)
            {
                Vector2 p = centre + directions[i] * distance;
                bitTransforms[i].position = new Vector3(p.x, p.y, 0);
                bitRenderers[i].color = colour;
                bitRenderers[i].enabled = true;
            }
        }

        public void Destroy()
        {
            Object.Destroy(root);
            Object.Destroy(sprite);
            Object.Destroy(texture);
        }
    }
}
