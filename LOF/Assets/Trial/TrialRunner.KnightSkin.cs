using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private GUISkin knightSkin, priorSkin;
        private Font knightTitleFont;
        private GUIStyle knightPanel, knightSelectedButton;
        private Texture2D knightAtlas, knightSeal;
        private static readonly Color Parchment = new Color32(246, 235, 208, 255);
        private static readonly Color BodyInk = new Color32(221, 230, 229, 255);

        private void InitKnightSkin()
        {
            knightSkin = Instantiate(GUI.skin);
            knightSkin.hideFlags = HideFlags.HideAndDontSave;
            knightAtlas = LoadTexture("KnightUI/AtlasBackdrop");
            knightSeal = LoadTexture("KnightUI/OrderSeal");

            var face = LoadTexture("KnightUI/Button");
            var hover = LoadTexture("KnightUI/ButtonHover");
            var down = LoadTexture("KnightUI/ButtonPressed");
            var selected = LoadTexture("KnightUI/ButtonSelected");
            var common = new GUIStyle(knightSkin.button)
            {
                alignment = TextAnchor.MiddleCenter,
                border = new RectOffset(15, 15, 9, 9),
                padding = new RectOffset(7, 7, 2, 3),
                fontSize = 14,
                fontStyle = FontStyle.Normal,
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

            knightPanel = new GUIStyle(GUIStyle.none) { border = new RectOffset(19, 19, 17, 17) };
            SetPlate(knightPanel.normal, LoadTexture("KnightUI/Panel"), BodyInk);
            knightTitleFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Liberation Serif", "Times New Roman" }, 32);
            if (knightTitleFont != null) title.font = knightTitleFont;
            title.fontStyle = FontStyle.Bold;
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
            float size = Mathf.Min(width * 0.44f, height * 0.56f);
            var previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0.13f);
            GUI.DrawTexture(new Rect(width - size * 0.91f, height * 0.39f, size, size), knightSeal);
            GUI.color = previous;
        }

        private void OnDestroy()
        {
            if (knightSkin != null) Destroy(knightSkin);
            if (knightTitleFont != null) Destroy(knightTitleFont);
        }
    }
}
