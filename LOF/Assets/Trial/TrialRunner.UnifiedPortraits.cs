using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private readonly Rect[] hallHeroBounds = new Rect[3];

        // Use the approved idle pose (first basic-attack cell) everywhere in the UI.
        private Texture2D LoadUnifiedPortrait(string id)
        {
            var atlas = LoadTexture("SemiSD/" + id + "/attack");
            var temporary = RenderTexture.GetTemporary(320, 320, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            try
            {
                Graphics.Blit(atlas, temporary, new Vector2(.25f, .5f), new Vector2(0, .5f));
                RenderTexture.active = temporary;
                var portrait = new Texture2D(320, 320, TextureFormat.RGBA32, false)
                { name = "SemiSD/" + id + "/portrait", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
                portrait.ReadPixels(new Rect(0, 0, 320, 320), 0, 0);
                portrait.Apply(false, false);
                return portrait;
            }
            finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(temporary); }
        }

        private void HandleHallHeroSelection(float dockTop)
        {
            var e = Event.current;
            if (e.type != EventType.MouseDown || e.button != 0 || e.mousePosition.y < 150 || e.mousePosition.y >= dockTop) return;
            // Frontmost opaque character wins; transparent margins cannot block rear allies.
            foreach (int slot in new[] { 1, 2, 0 })
            {
                int hero = battle.HeroAt(slot);
                Rect bounds = hallHeroBounds[slot];
                if (hero < 0 || !bounds.Contains(e.mousePosition)) continue;
                var art = portraits[hero];
                float x = (e.mousePosition.x - bounds.x) / bounds.width;
                float y = 1 - (e.mousePosition.y - bounds.y) / bounds.height;
                if (art.GetPixelBilinear(x, y).a < .15f) continue;
                ShowKnightDossier(hero);
                heroesFromMenu = true;
                e.Use();
                return;
            }
        }
    }
}
