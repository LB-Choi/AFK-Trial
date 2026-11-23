using System.Globalization;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private string HeroAcquisitionText(int hero)
        {
            string result = "홀 → 소환 · 기본 확률 " +
                battle.Data.SummonRates[hero].ToString("0.####", CultureInfo.InvariantCulture) + "%";
            foreach (var reward in battle.Data.FirstClearRecruits)
                if (reward.Value == hero)
                    result += "\n메인 " + reward.Key + "스테이지 최초 클리어 보상";
            return result + "\n신규 영웅 보장 규칙과 현재 진행도는 소환 화면에서 확인하세요.";
        }

        // Landscape dossier: read-only information uses level-one skill previews, never grants ownership.
        private void DrawUnownedHeroDossier(float width, float height)
        {
            var hero = battle.Data.Heroes[featureHero];
            float bottom = height - 144;
            float split = 256;
            DrawKnightPortrait(new Rect(8, 4, split - 16, bottom), featureHero, Color.white);
            Fill(new Rect(8, bottom - 78, split - 16, 78), new Color(.04f, .07f, .12f, .9f));
            CourtHeading(new Rect(16, bottom - 72, split - 32, 32), hero.Name, 25, TextAnchor.MiddleCenter);
            ArcadeText(new Rect(16, bottom - 35, split - 32, 27), RoleKorean(hero.Role) + " · 미보유", 16, HallAmber, TextAnchor.MiddleCenter);
            CourtHeading(new Rect(split + 16, 6, width - split - 32, 34), "스킬 · 영입 시 레벨 1", 23, TextAnchor.MiddleLeft);
            string prefix = CultureInfo.InvariantCulture.TextInfo.ToTitleCase(hero.Id.Replace('_', ' ')).Replace(" ", "");
            GUI.enabled = selectedSkill < 0;
            for (int slot = 0; slot < 3; slot++)
            {
                var skill = battle.Data.Skills[featureHero, slot];
                string trigger = slot == 0 ? "기본 공격 " + skill.Interval + "회마다" : skill.Interval + "초마다";
                if (DrawDossierRow(new Rect(split + 12, 48 + slot * 67, width - split - 28, 63),
                    "SkillUI/" + prefix + "Skill" + (slot + 1), trigger + " · 상세 보기", skill.Name, false))
                { selectedSkill = slot; skillScroll = Vector2.zero; }
            }
            GUI.enabled = true;
            Fill(new Rect(0, bottom, width, height - bottom), HallInk);
            CourtRule(new Rect(12, bottom, width - 24, 1), HallLine);
            CourtHeading(new Rect(18, bottom + 6, width - 36, 29), "획득 경로", 21, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(18, bottom + 39, width - 36, 70), HeroAcquisitionText(featureHero),
                new GUIStyle(small) { wordWrap = true, fontSize = 16 });
            ArcadeText(new Rect(18, height - 30, width - 36, 25), "미보유 · 영입 후 레벨업, 스킬 강화, 장비 장착과 편성이 가능합니다.",
                15, HallAmber, TextAnchor.MiddleLeft);
            if (selectedSkill < 0) return;
            Fill(new Rect(0, 0, width, height), new Color(.01f, .02f, .03f, .85f));
            float popupWidth = Mathf.Min(520, width - 16);
            GUI.BeginGroup(new Rect((width - popupWidth) / 2, (height - 392) / 2, popupWidth, 392));
            DrawSkillDetails(popupWidth);
            GUI.EndGroup();
        }
    }
}
