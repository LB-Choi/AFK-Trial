using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private sealed class SkillSheet
        {
            public Texture2D Texture;
            public int Columns, Rows, Frames;
            public float Fps, Scale, PivotY;
            public float Aspect => (float)Texture.width / Columns / ((float)Texture.height / Rows);

            public Rect FrameUV(int frame, Rect crop)
            {
                return new Rect((frame % Columns + crop.x) / Columns,
                    1f - (frame / Columns + crop.yMax) / Rows, crop.width / Columns, crop.height / Rows);
            }
        }

        private sealed class PlayingSkill
        {
            public TrialBattle.SkillCue Cue;
            public TrialBattle.Unit Caster, From, Target;
            public float CastTime, StartTime, EndTime;
            public bool IsStatus => Cue == TrialBattle.SkillCue.FrostSwordEnchant || Cue == TrialBattle.SkillCue.StunStars ||
                Cue == TrialBattle.SkillCue.Bleeding;
        }

        private readonly Dictionary<TrialBattle.SkillCue, SkillSheet> skillSheets = new Dictionary<TrialBattle.SkillCue, SkillSheet>();
        private readonly List<PlayingSkill> playingSkills = new List<PlayingSkill>();

        private void LoadSkillEffects()
        {
            var directory = Path.Combine(Application.streamingAssetsPath, "SkillEffects");
            foreach (var row in CsvRow.Read(directory, "Sheets"))
            {
                var cue = (TrialBattle.SkillCue)Enum.Parse(typeof(TrialBattle.SkillCue), row.Text("cue"));
                var sheet = new SkillSheet
                {
                    Columns = row.Integer("columns", 1), Rows = row.Integer("rows", 1), Frames = row.Integer("frames", 1),
                    Fps = row.Number("fps", true), Scale = row.Number("scale", true), PivotY = row.Number("pivotY")
                };
                if (sheet.Frames > (long)sheet.Rows * sheet.Columns) throw row.Error("not enough cells for all frames");
                string filename = row.Text("file");
                var png = File.ReadAllBytes(Path.Combine(directory, filename));
                skillSheets.Add(cue, sheet);
                sheet.Texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                    { name = filename, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
                if (!ImageConversion.LoadImage(sheet.Texture, png, true) ||
                    sheet.Texture.width % sheet.Columns != 0 || sheet.Texture.height % sheet.Rows != 0)
                    throw row.Error("invalid PNG sheet layout for " + filename);
            }
            foreach (TrialBattle.SkillCue cue in Enum.GetValues(typeof(TrialBattle.SkillCue)))
                if (cue != TrialBattle.SkillCue.KatanaCombo && !skillSheets.ContainsKey(cue))
                    throw new InvalidDataException("Missing VFX sheet for " + cue);
        }

        private void ReleaseSkillEffects()
        {
            if (battle != null) battle.SkillApplied -= PlaySkillEffect;
            foreach (var sheet in skillSheets.Values)
                if (sheet.Texture != null) Destroy(sheet.Texture);
            skillSheets.Clear();
            playingSkills.Clear();
        }

        private void PruneSkillEffects()
        {
            for (int i = playingSkills.Count - 1; i >= 0; i--)
            {
                var effect = playingSkills[i];
                if (!battle.Units.Contains(effect.Target) || BattleAnimationTime >= effect.EndTime ||
                    (effect.IsStatus && !effect.Target.Alive))
                    playingSkills.RemoveAt(i);
            }
        }

        private void PlaySkillEffect(TrialBattle.SkillCue cue, TrialBattle.Unit caster, TrialBattle.Unit recipient, float time)
        {
            // The approved katana actor atlas already draws the vertical/horizontal plus slash.
            if (cue == TrialBattle.SkillCue.KatanaCombo) return;
            PruneSkillEffects();
            var from = caster;
            float start = time;
            float bleedEnd = cue == TrialBattle.SkillCue.Bleeding
                ? recipient.Skills.BleedUntil : 0f;
            // A taunt sends one shout from the tank. Heal contacts share a cast and hop in recipient order.
            for (int i = playingSkills.Count - 1; i >= 0; i--)
            {
                var prior = playingSkills[i];
                if (cue == TrialBattle.SkillCue.AttackUp && prior.Cue == TrialBattle.SkillCue.FrostSwordEnchant && prior.Target == recipient)
                {
                    playingSkills.RemoveAt(i);
                    continue;
                }
                if (prior.IsStatus && prior.Cue == cue && prior.Target == recipient)
                {
                    if (cue == TrialBattle.SkillCue.Bleeding)
                    {
                        prior.EndTime = bleedEnd;
                        return;
                    }
                    playingSkills.RemoveAt(i);
                    continue;
                }
                if (prior.Cue != cue || prior.Caster != caster || prior.CastTime != time) continue;
                if (cue == TrialBattle.SkillCue.Taunt) return;
                if (cue == TrialBattle.SkillCue.ChainHeal)
                {
                    from = prior.Target;
                    start = prior.StartTime + 7f / skillSheets[cue].Fps;
                }
                break;
            }
            var sheet = skillSheets[cue];
            float end = start + sheet.Frames / sheet.Fps;
            if (cue == TrialBattle.SkillCue.FrostSwordEnchant) end = recipient.AttackBuffUntil;
            if (cue == TrialBattle.SkillCue.StunStars) end = recipient.StunnedUntil;
            if (cue == TrialBattle.SkillCue.Bleeding) end = bleedEnd;
            playingSkills.Add(new PlayingSkill
            {
                Cue = cue, Caster = caster, From = from,
                Target = cue == TrialBattle.SkillCue.Taunt ? caster : recipient, CastTime = time, StartTime = start, EndTime = end
            });
        }

        private void DrawSkillEffects()
        {
            PruneSkillEffects();
            foreach (var effect in playingSkills)
            {
                var sheet = skillSheets[effect.Cue];
                int frame = Mathf.FloorToInt((BattleAnimationTime - effect.StartTime) * sheet.Fps);
                if (effect.IsStatus) frame %= sheet.Frames;
                if (frame < 0 || frame >= sheet.Frames) continue;
                var point = UnitScreenPoint(effect.Target);
                var unit = CombatantArea(point.x, point.y);
                if (effect.Cue == TrialBattle.SkillCue.FrostSwordEnchant)
                {
                    DrawBladeEnchant(effect.Target, unit, sheet, frame);
                    continue;
                }
                var anchor = new Vector2(unit.center.x, unit.yMax - unit.height * 0.08f);
                float height = unit.height * sheet.Scale;
                float width = height * sheet.Aspect;
                if (effect.Cue == TrialBattle.SkillCue.Bleeding)
                {
                    // Keep the wound droplets, excluding the wide floor splash underneath.
                    var crop = new Rect(.23f, 0f, .54f, .69f);
                    var bounds = new Rect(point.x - width * crop.width / 2, point.y - height * sheet.PivotY,
                        width * crop.width, height * crop.height);
                    var tint = GUI.color;
                    GUI.color = new Color(tint.r, tint.g, tint.b, tint.a * .65f);
                    GUI.DrawTextureWithTexCoords(bounds, sheet.Texture, sheet.FrameUV(frame, crop));
                    GUI.color = tint;
                    continue;
                }
                if (effect.Cue == TrialBattle.SkillCue.ChainHeal)
                {
                    DrawHealContact(effect, sheet, frame, anchor, width, height);
                    continue;
                }
                if (effect.Cue == TrialBattle.SkillCue.StunStars) anchor = new Vector2(point.x, unit.yMin + 13f);
                GUI.DrawTextureWithTexCoords(new Rect(anchor.x - width / 2, anchor.y - height * sheet.PivotY, width, height),
                    sheet.Texture, sheet.FrameUV(frame, new Rect(0, 0, 1, 1)));
            }
        }

        private void DrawBladeEnchant(TrialBattle.Unit wielder, Rect area, SkillSheet sheet, int frame)
        {
            if (!bladeRigs.TryGetValue(wielder.Stats.Animation, out var rig) ||
                !rig.TryGetBladeSegment(area, wielder, BattleAnimationTime, out var guard, out var tip)) return;

            // The exported sword points up and right. Register its crossguard and tip to
            // the current pose in the same coordinate space as the hand-painted weapon.
            var sourceGuard = new Vector2(.35f * sheet.Aspect, sheet.PivotY);
            var sourceBlade = new Vector2(.83f * sheet.Aspect, .095f) - sourceGuard;
            var blade = tip - guard;
            float height = sheet.Scale * blade.magnitude / sourceBlade.magnitude;
            var bounds = new Rect(guard - sourceGuard * height, new Vector2(height * sheet.Aspect, height));
            var previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(Vector2.SignedAngle(sourceBlade, blade), guard);
            GUI.DrawTextureWithTexCoords(bounds, sheet.Texture, sheet.FrameUV(frame, new Rect(0, 0, 1, 1)));
            GUI.matrix = previousMatrix;
        }

        // The exported heal has three contacts in a fixed triangle. Sample its first pulse and incoming
        // lightning separately so they follow the real units when the party moves or has fewer targets.
        private void DrawHealContact(PlayingSkill effect, SkillSheet sheet, int frame, Vector2 foot, float width, float height)
        {
            var pulse = new Rect(0.18f, 0.08f, 0.285f, 0.85f);
            var origin = foot - new Vector2(0.32f * width, sheet.PivotY * height);
            if (effect.From != effect.Target && frame >= 3 && frame <= 27)
            {
                var source = UnitScreenPoint(effect.From);
                var destination = origin + new Vector2(0.32f * width, 0.56f * height);
                Vector2 ray = destination - source;
                float thickness = Mathf.Min(height * 0.16f, ray.magnitude * 0.4f);
                var matrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(Mathf.Atan2(ray.y, ray.x) * Mathf.Rad2Deg, source);
                GUI.DrawTextureWithTexCoords(new Rect(source.x, source.y - thickness / 2, ray.magnitude, thickness),
                    sheet.Texture, sheet.FrameUV(frame, new Rect(0.068f, 0.49f, 0.22f, 0.14f)));
                GUI.matrix = matrix;
            }
            GUI.DrawTextureWithTexCoords(new Rect(origin.x + pulse.x * width, origin.y + pulse.y * height,
                pulse.width * width, pulse.height * height), sheet.Texture, sheet.FrameUV(frame, pulse));
        }
    }
}
