using System.Collections.Generic;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private readonly Dictionary<string, Texture2D> semiSDTextures = new Dictionary<string, Texture2D>();
        private Texture2D SemiSDTexture(string id, string clip)
        {
            string key = id + "/" + clip;
            if (!semiSDTextures.TryGetValue(key, out var texture))
            {
                texture = Resources.Load<Texture2D>("SemiSD/" + key);
                semiSDTextures[key] = texture;
            }
            return texture;
        }
        public static int OneShotFrame(float age, float duration) => Mathf.Clamp(Mathf.FloorToInt(age / duration * 8 + .0001f), 0, 7);

        private bool DrawSemiSD(Rect area, TrialBattle.Unit unit)
        {
            string id = unit.Stats.Animation;
            float now = BattleAnimationTime;
            string clip;
            int frame;
            if (!unit.Alive) { clip = "death"; frame = OneShotFrame(now - unit.LastHitTime, 1f); }
            else if (now - unit.SkillStart < 1f)
            { clip = "skill" + unit.LastSkillSlot; frame = OneShotFrame(now - unit.SkillStart, 1f); }
            else if ((now - unit.SwingStart) * unit.SwingSpeed < unit.Stats.AttackDuration)
            {
                clip = unit.SwingWasSpecial ? "special" : "attack";
                float age = (now - unit.SwingStart) * unit.SwingSpeed;
                // The approved basic strike touches on frame 5, regardless of attack speed.
                int contact = unit.SwingWasSpecial && id == "bandit_chief" ? 3 : 4;
                frame = age < unit.Stats.ImpactSeconds ? Mathf.Clamp((int)(age / unit.Stats.ImpactSeconds * contact), 0, contact - 1)
                    : contact + Mathf.Clamp((int)((age - unit.Stats.ImpactSeconds) / Mathf.Max(.01f, unit.Stats.AttackDuration - unit.Stats.ImpactSeconds) * (8 - contact)), 0, 7 - contact);
            }
            // Incoming damage uses Hit Flash without switching to a reaction pose.
            else if (unit.Walking) { clip = "walk"; frame = (int)(now * 8) % 8; }
            else if (id == "bandit_captain" || id == "bandit_chief") { clip = "idle"; frame = (int)(now * 8) % 8; }
            else { clip = "attack"; frame = 0; }
            var texture = SemiSDTexture(id, clip);
            if (!texture) return false;
            // Six-row review sheets have wide cells. Match body height to the square basic-action sheets.
            // Long weapons may extend beyond the actor area; never shrink the body to fit a blade.
            float bodyScale = clip.StartsWith("skill") ? 1.5f : 1f;
            if (id == "katana_knight" && clip == "skill0") bodyScale = 1.1f;
            if (id == "banner_paladin" && clip == "skill2") bodyScale = 1.3f;
            if (id == "scythe_knight" && clip == "skill2") bodyScale = 1.3f;
            if (id == "sun_paladin" && clip == "skill1" || id == "axe_warrior" && clip == "skill1") bodyScale = 1f;
            float size = area.height * 1.08f * bodyScale;
            var bounds = new Rect(area.center.x - size / 2, area.yMax - size, size, size);
            GUI.DrawTextureWithTexCoords(bounds, texture, new Rect(frame % 4 / 4f, 1 - (frame / 4 + 1) / 2f, .25f, .5f));
            if (id == "scythe_knight" && clip == "skill2" && unit.SkillTarget != null && frame >= 3 && frame <= 5)
            {
                var bell = SemiSDTexture(id, "bell");
                var target = UnitScreenPoint(unit.SkillTarget);
                if (bell) GUI.DrawTextureWithTexCoords(new Rect(target.x - 32, target.y - 114, 64, 64), bell,
                    new Rect(frame % 4 / 4f, 1 - (frame / 4 + 1) / 2f, .25f, .5f));
            }
            return true;
        }
    }
}
