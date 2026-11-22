using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private static readonly Color ArcadeNavy = new Color32(16, 23, 39, 250);
        private static readonly Color FrameGold = new Color32(166, 142, 98, 255);
        private static readonly Color FrameLight = new Color32(238, 213, 167, 255);
        private GUIStyle arcadeType;
        private int galleryHero;

        private void ArcadePanel(Rect area, Color fill, Color accent)
        {
            ManuscriptFade(area, Color.Lerp(fill, new Color(0.4f, 0.34f, 0.30f, fill.a), 0.1f), fill);
            Fill(new Rect(area.x, area.y, area.width, 1), Color.Lerp(fill, accent, 0.48f));
        }

        private void ArcadeText(Rect area, string value, int size, Color color, TextAnchor alignment)
        {
            if (arcadeType == null)
                arcadeType = new GUIStyle(label)
                {
                    padding = new RectOffset(), clipping = TextClipping.Clip, wordWrap = false
                };
            arcadeType.fontSize = size;
            arcadeType.fontStyle = size >= 19 ? FontStyle.Bold : FontStyle.Normal;
            arcadeType.alignment = alignment;
            arcadeType.normal.textColor = color;
            GUI.Label(area, value, arcadeType);
        }

        private bool ArcadeTab(Rect area, string caption, bool selected)
        {
            return GUI.Button(area, caption, selected ? knightSelectedButton : button);
        }

        private bool DrawRosterHeader(int width, string heading, string back)
        {
            ManuscriptFade(new Rect(0, 0, width, 65), ArcadeNavy, new Color32(16, 23, 39, 70));
            Picture("KnightUI/OrderSeal", 18, 10, 41, 41);
            CourtHeading(new Rect(70, 2, width - 235, 53), heading, 30, TextAnchor.MiddleLeft);
            CourtRule(new Rect(19, 58, width - 38, 1), FrameGold);
            return ArcadeTab(new Rect(width - 134, 10, 118, 42), back, false);
        }

        private void DrawKnightGallery(int width, int height)
        {
            bool wide = width > height;
            float bottom = height - HallDockHeight(!wide) - 10;
            var portraitArea = wide ? new Rect(20, 73, 232, bottom - 73)
                : new Rect(20, 74, width - 40, 168);
            if (!battle.Owned(galleryHero)) galleryHero = battle.FirstDeployedHero;
            int hero = galleryHero;
            GUI.BeginGroup(portraitArea);
            var area = new Rect(0, 0, portraitArea.width, portraitArea.height);
            GUI.DrawTexture(area, FeatureTexture("KnightUI/ChapterHall"), ScaleMode.ScaleAndCrop);
            ManuscriptFade(area, new Color32(11, 18, 32, 105), new Color32(11, 18, 32, 226));
            if (wide)
            {
                var image = portraits[hero];
                float artHeight = area.height * 0.83f;
                float artWidth = artHeight * image.width / image.height;
                float focalX = PortraitFocus(hero);
                GUI.DrawTexture(new Rect(area.width * 0.5f - artWidth * focalX, 7, artWidth, artHeight), image);
                ManuscriptFade(new Rect(0, area.height * 0.32f, area.width, area.height * 0.68f),
                    Color.clear, new Color32(7, 14, 25, 255));
            }
            else
                DrawKnightPortrait(new Rect(area.width * 0.44f, 0, area.width * 0.56f, area.height), hero, Color.white);

            float y = wide ? area.height - 117 : 21;
            float textWidth = wide ? area.width - 30 : area.width * 0.47f;
            ArcadeText(new Rect(15, y, textWidth, 20),
                RoleKorean(battle.Data.Heroes[hero].Role) + "    /    레벨 " + battle.HeroLevel(hero),
                12, FrameLight, TextAnchor.MiddleLeft);
            CourtHeading(new Rect(15, y + 20, textWidth, 34), battle.Heroes[hero], 27, TextAnchor.MiddleLeft);
            var stats = battle.HeroStats(hero);
            ArcadeText(new Rect(15, y + 55, textWidth, 22), $"체력 {stats.hp:0}   공격력 {stats.attack:0}",
                14, Parchment, TextAnchor.MiddleLeft);
            CourtRule(new Rect(16, y + 84, Mathf.Min(150, textWidth - 6), 1), FrameGold);
            ArcadeText(new Rect(15, y + 87, textWidth, 22), "상세 정보   ›", 13, FrameLight, TextAnchor.MiddleLeft);
            if (GUI.Button(area, GUIContent.none, GUIStyle.none)) ShowKnightDossier(hero);
            GUI.EndGroup();

            var collection = wide ? new Rect(266, 73, width - 286, bottom - 73)
                : new Rect(20, 253, width - 40, bottom - 253);
            DrawHeroGrid(collection, wide ? 5 : 3, false);
        }

        private void ShowKnightDossier(int hero)
        {
            galleryHero = featureHero = hero;
            selectedSkill = -1;
            heroView = HeroView.Info;
        }

        private void DrawKnightPortrait(Rect area, int hero, Color tint)
        {
            var image = portraits[hero];
            string id = battle.Data.Heroes[hero].Id;
            float focalX = PortraitFocus(hero);
            float topCrop = id == "banner_paladin" ? 0.18f : 0.01f;
            float height = Mathf.Max(area.height * 1.28f, area.width * 1.25f);
            float width = height * image.width / image.height;
            GUI.BeginGroup(area);
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(new Rect(area.width * 0.5f - width * focalX, -height * topCrop, width, height), image);
            GUI.color = previous;
            GUI.EndGroup();
        }

        // The flag and greatsword extend beyond their bearers; center the knight in the visible crop.
        private float PortraitFocus(int hero)
        {
            string id = battle.Data.Heroes[hero].Id;
            return id == "sun_paladin" ? 0.8f : id == "banner_paladin" ? 0.61f : 0.5f;
        }

        private void DrawRosterCard(Rect card, int hero, int deployedSlot, bool selected)
        {
            if (hero < 0)
            {
                ArcadePanel(card, HallInk, selected ? HallAmber : HallLine);
                ArcadeText(card, deployedSlot == 1 ? "전열 · 빈자리" : "후열 · 빈자리", 16, HallLight, TextAnchor.MiddleCenter);
                return;
            }
            bool owned = battle.Owned(hero);
            var role = battle.Data.Heroes[hero].Role;
            Color accent = owned ? RoleAccent(role) : new Color32(104, 119, 139, 255);
            bool hovered = owned && card.Contains(Event.current.mousePosition);
            Color edge = selected || hovered ? FrameLight : Color.Lerp(ArcadeNavy, FrameGold, 0.53f);
            Fill(card, edge);
            float inset = selected ? 2 : 1;
            var inside = new Rect(card.x + inset, card.y + inset, card.width - inset * 2, card.height - inset * 2);
            GUI.BeginGroup(inside);
            var area = new Rect(0, 0, inside.width, inside.height);
            GUI.DrawTexture(area, FeatureTexture("KnightUI/ChapterHall"), ScaleMode.ScaleAndCrop);
            ManuscriptFade(area, Color.Lerp(new Color32(23, 29, 45, 211), accent, 0.10f),
                new Color32(25, 27, 40, 126));
            DrawKnightPortrait(area, hero, owned ? Color.white : new Color(0.53f, 0.58f, 0.66f));

            float shadeHeight = Mathf.Min(62, area.height * 0.48f);
            ManuscriptFade(new Rect(0, area.height - shadeHeight, area.width, shadeHeight),
                Color.clear, new Color32(6, 12, 22, 253));
            CourtHeading(new Rect(4, area.height - 29, area.width - 8, 27),
                owned ? battle.Heroes[hero] : "잠김", area.width < 139 ? 16 : 18, TextAnchor.MiddleCenter);
            if (owned)
            {
                ManuscriptFade(new Rect(0, 0, area.width, 33), new Color32(9, 17, 28, 194), Color.clear);
                ArcadeText(new Rect(7, 3, 52, 19), "레벨 " + battle.HeroLevel(hero), 13, Parchment, TextAnchor.MiddleLeft);
                if (role != TrialData.Role.None)
                    Picture("RoleUI/" + RoleIcons[(int)role - 1], area.width - 29, 3, 25, 25);
                if (deployedSlot >= 0)
                    GUI.DrawTexture(new Rect(4, 24, 23, 27), deploymentBadges[deployedSlot], ScaleMode.ScaleToFit);
            }
            if (selected)
            {
                CourtGem(new Vector2(area.center.x, 4), 4, FrameLight);
                Fill(new Rect(area.width * 0.26f, area.height - 2, area.width * 0.48f, 2), FrameLight);
            }
            GUI.EndGroup();
        }
    }
}
