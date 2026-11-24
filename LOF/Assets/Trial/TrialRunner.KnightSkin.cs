using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private GUISkin knightSkin, priorSkin;
        private GUIStyle knightPanel, knightSelectedButton;
        private Texture2D knightAtlas, knightSeal;
        private Font courtFont;
        private Font koreanFont;
        private GUIStyle courtHeading;
        private Texture2D coatMedallion;
        private readonly System.Collections.Generic.Dictionary<(Color32, Color32), Texture2D> courtFades =
            new System.Collections.Generic.Dictionary<(Color32, Color32), Texture2D>();
        private readonly System.Collections.Generic.List<Texture2D> skinPlates = new System.Collections.Generic.List<Texture2D>();
        private static readonly Color Parchment = new Color32(249, 242, 221, 255);
        private static readonly Color BodyInk = new Color32(230, 231, 226, 255);
        private static readonly Color HudInk = new Color32(12, 24, 35, 255);
        private static readonly Color HudSteel = new Color32(96, 109, 110, 255);
        private static readonly Color HudAmber = new Color32(226, 192, 132, 255);
        private static readonly Color HudCyan = new Color32(146, 206, 204, 255);

        private static Color RoleAccent(TrialData.Role role)
        {
            if (role == TrialData.Role.Defense) return HudCyan;
            if (role == TrialData.Role.Support) return new Color32(164, 144, 228, 255);
            return new Color32(251, 125, 94, 255);
        }

        private void InitKnightSkin()
        {
            knightSkin = Instantiate(GUI.skin);
            knightSkin.hideFlags = HideFlags.HideAndDontSave;
            koreanFont = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Apple SD Gothic Neo", "Noto Sans CJK KR", "Arial" }, 18);
            knightSkin.font = koreanFont;
            label.font = small.font = damage.font = koreanFont;
            knightSkin.label.font = knightSkin.textField.font = koreanFont;
            knightAtlas = LoadTexture("KnightUI/AtlasBackdrop");
            knightSeal = LoadTexture("KnightUI/OrderSeal");

            coatMedallion = MakeMedallion();
            var face = MakeSkinPlate(new Color32(23, 40, 52, 255), new Color32(131, 130, 112, 255));
            var hover = MakeSkinPlate(new Color32(44, 63, 71, 255), HudAmber);
            var down = MakeSkinPlate(new Color32(21, 34, 43, 255), HudAmber);
            var selected = MakeSkinPlate(new Color32(111, 81, 45, 255), HudAmber);
            var common = new GUIStyle(knightSkin.button)
            {
                font = koreanFont,
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(9, 9, 9, 9),
                padding = new RectOffset(7, 7, 2, 3),
                fontSize = 14,
                fontStyle = FontStyle.Bold,
                wordWrap = false
            };
            SetPlate(common.normal, face, Parchment);
            SetPlate(common.hover, hover, Color.white);
            SetPlate(common.active, down, Parchment);
            SetPlate(common.focused, hover, Parchment);
            SetPlate(common.onNormal, selected, Parchment);
            SetPlate(common.onHover, hover, Color.white);
            SetPlate(common.onActive, down, Color.white);
            SetPlate(common.onFocused, selected, Parchment);
            knightSkin.button = common;
            button = new GUIStyle(common) { fontSize = 17 };
            knightSelectedButton = new GUIStyle(common);
            SetPlate(knightSelectedButton.normal, selected, Parchment);
            SetPlate(knightSelectedButton.focused, selected, Parchment);

            knightPanel = new GUIStyle(GUIStyle.none) { border = new RectOffset(8, 8, 8, 8) };
            SetPlate(knightPanel.normal, MakeSkinPlate(new Color32(19, 31, 47, 249), HudSteel), BodyInk);
            // Only select an installed face. The built-in player font is the portable fallback.
            if (System.Array.IndexOf(Font.GetOSInstalledFontNames(), "Georgia") >= 0)
                courtFont = Font.CreateDynamicFontFromOSFont("Georgia", 32);
            title.font = koreanFont;
            title.fontStyle = FontStyle.Normal;
            title.fontSize = 27;
            title.normal.textColor = Parchment;
            label.normal.textColor = BodyInk;
            small.normal.textColor = new Color32(205, 218, 222, 255);
            knightSkin.label.normal.textColor = BodyInk;
            summonName = new GUIStyle(small)
            {
                alignment = TextAnchor.MiddleCenter, fontSize = 14, wordWrap = false,
                padding = new RectOffset(), clipping = TextClipping.Clip
            };
            summonName.normal.textColor = Parchment;
            summonLevel = new GUIStyle(summonName) { fontSize = 13 };
            summonLevel.normal.textColor = new Color32(160, 218, 207, 255);
            battleHudText = new GUIStyle(label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false,
                clipping = TextClipping.Clip,
                padding = new RectOffset()
            };
        }

        // Nine-slice plates keep the corner engraving and thin double rim at any size.
        private Texture2D MakeSkinPlate(Color face, Color trim)
        {
            const int size = 24;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int edge = Mathf.Min(Mathf.Min(x, size - 1 - x), Mathf.Min(y, size - 1 - y));
                int bevel = Mathf.Min(x, size - 1 - x) + Mathf.Min(y, size - 1 - y) - 5;
                Color color = Color.Lerp(face, Color.white, 0.075f * y / (size - 1));
                if (edge == 0 || bevel == 0) color = Color.Lerp(trim, Color.black, 0.17f);
                else if (edge == 2 || bevel == 3) color = Color.Lerp(face, trim, 0.22f);
                if (bevel < 0) color = Color.clear;
                pixels[y * size + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            skinPlates.Add(texture);
            return texture;
        }

        private Texture2D MakeMedallion()
        {
            const int size = 96;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
                for (int x = 0; x < size; x++)
                {
                    float dx = x - 47.5f, dy = y - 47.5f;
                    float radius = Mathf.Sqrt(dx * dx + dy * dy);
                    float sheen = Mathf.Clamp01(0.48f + (dy - dx) / 110);
                    Color face = Color.Lerp(new Color32(15, 27, 39, 255), new Color32(50, 73, 79, 255), sheen * 0.7f);
                    Color metal = Color.Lerp(new Color32(87, 73, 56, 255), HudAmber, sheen);
                    if (radius >= 43.5f && radius <= 46 || radius >= 39 && radius <= 39.9f) face = metal;
                    if (radius > 46) face.a = Mathf.Clamp01(47 - radius);
                    pixels[y * size + x] = face;
                }
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            skinPlates.Add(texture);
            return texture;
        }

        private void ManuscriptFade(Rect area, Color top, Color bottom)
        {
            if (Event.current.type != EventType.Repaint) return;
            var colors = ((Color32)top, (Color32)bottom);
            if (!courtFades.TryGetValue(colors, out var texture))
            {
                const int height = 32;
                texture = new Texture2D(1, height, TextureFormat.RGBA32, false)
                    { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.HideAndDontSave };
                var pixels = new Color32[height];
                for (int i = 0; i < height; i++) pixels[i] = Color.Lerp(bottom, top, i / (height - 1f));
                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                courtFades.Add(colors, texture);
            }
            GUI.DrawTexture(area, texture);
        }

        private void CourtGem(Vector2 center, float size, Color color)
        {
            var matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(45, center);
            Fill(new Rect(center.x - size / 2, center.y - size / 2, size, size), color);
            GUI.matrix = matrix;
        }

        private void CourtRule(Rect area, Color color)
        {
            Fill(new Rect(area.x, area.center.y, area.width, 1), color);
            CourtGem(area.center, 4, color);
        }

        private void CourtHeading(Rect area, string value, int size, TextAnchor alignment)
        {
            if (courtHeading == null)
                courtHeading = new GUIStyle(title) { padding = new RectOffset(), clipping = TextClipping.Clip, wordWrap = false };
            courtHeading.fontSize = size;
            courtHeading.font = value == Application.productName && courtFont != null ? courtFont : koreanFont;
            courtHeading.alignment = alignment;
            GUI.Label(area, value, courtHeading);
        }

        private static void SetPlate(GUIStyleState state, Texture2D texture, Color ink)
        {
            state.background = texture;
#if UNITY_EDITOR
            state.scaledBackgrounds = System.Array.Empty<Texture2D>();
#endif
            state.textColor = ink;
        }

        private void ApplyKnightSkin()
        {
            priorSkin = GUI.skin;
            GUI.skin = knightSkin;
        }

        private void RestoreKnightSkin()
        {
            GUI.skin = priorSkin;
            priorSkin = null;
        }

        private void DrawKnightPanel(Rect area)
        {
            if (Event.current.type == EventType.Repaint)
                knightPanel.Draw(area, GUIContent.none, false, false, false, false);
        }

        private void DrawKnightBackdrop(int width, int height)
        {
            GUI.DrawTexture(new Rect(0, 0, width, height), knightAtlas, ScaleMode.ScaleAndCrop);
            Fill(new Rect(0, 0, width, height), new Color(0.018f, 0.042f, 0.1f, 0.63f));
            float size = Mathf.Min(width * 0.44f, height * 0.56f);
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.13f);
            GUI.DrawTexture(new Rect(width - size * 0.91f, height * 0.39f, size, size), knightSeal);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (portraits != null) foreach (var portrait in portraits) if (portrait != null) Destroy(portrait);
            if (knightSkin != null) Destroy(knightSkin);
            if (courtFont != null) Destroy(courtFont);
            if (koreanFont != null) Destroy(koreanFont);
            foreach (var fade in courtFades.Values) Destroy(fade);
            foreach (var plate in skinPlates) Destroy(plate);
        }
    }
}
