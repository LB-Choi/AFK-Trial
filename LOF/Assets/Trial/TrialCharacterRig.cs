using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using UnityEngine;

namespace AFKTrial
{
    // The battle uses the rig timelines for attack timing and the ice sword's effect anchors.
    public sealed class TrialCharacterRig
    {
        public class Bone { public string name, parent; public float x, y; }
        public class Slot { public string name, bone, attachment; }
        public class Region { public float x, y, width, height; }
        public class Skin { public Dictionary<string, Dictionary<string, Region>> attachments; }
        public class Key { public string name; public float time; }
        public class Track { public Key[] attachment; }
        public class Clip { public Dictionary<string, Track> slots; public Key[] events; }
        public class Data
        {
            public Bone[] bones;
            public Slot[] slots;
            public Skin[] skins;
            public Dictionary<string, Clip> animations;
        }

        internal const string FrostBladeResource = "IceKnightRig/IceKnight";

        private sealed class BladePose
        {
            public Rect Bounds;
            public Vector2 Guard, Tip;
        }

        private readonly Slot bladeSlot;
        private readonly Dictionary<string, BladePose> bladePoses;
        private readonly Clip idle, walk, attack;
        private readonly float breathCycle, stride, recovery;
        public readonly float ImpactTime;

        public static Data ReadData(string resource)
        {
            var json = Resources.Load<TextAsset>(resource);
            if (!json) throw new InvalidDataException("Missing animation in Resources: " + resource);
            var reader = new DataContractJsonSerializer(typeof(Data),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            using (var stream = new MemoryStream(json.bytes))
                return (Data)reader.ReadObject(stream);
        }

        public static float AttackDurationAtImpact(string resource, float combatImpactTime)
        {
            var clip = ReadData(resource).animations["attack"];
            return Duration(clip) * combatImpactTime / ImpactMarker(clip);
        }

        private static float ImpactMarker(Clip clip) => Array.Find(clip.events, marker => marker.name == "impact").time;

        public TrialCharacterRig(string resource)
        {
            Data data = ReadData(resource);

            if (data.animations.TryGetValue("idle", out idle))
            {
                breathCycle = Duration(idle);
                if (breathCycle <= 0) throw new InvalidDataException("Empty idle cycle: " + resource);
            }
            walk = data.animations["walk"];
            attack = data.animations["attack"];
            stride = Duration(walk);
            recovery = Duration(attack);
            ImpactTime = ImpactMarker(attack);
            if (resource != FrostBladeResource) return;

            bladeSlot = Array.Find(data.slots, slot => slot.name == "pose");
            if (bladeSlot == null) throw new InvalidDataException("Missing ice sword pose slot: " + resource);
            var origins = new Dictionary<string, Vector2>();
            foreach (Bone bone in data.bones)
            {
                var parent = bone.parent == null ? Vector2.zero : origins[bone.parent];
                origins.Add(bone.name, parent + new Vector2(bone.x, -bone.y));
            }

            var origin = origins[bladeSlot.bone];
            var regions = data.skins[0].attachments[bladeSlot.name];
            bladePoses = new Dictionary<string, BladePose>();
            string directory = Path.Combine(Application.streamingAssetsPath, "SkillEffects");
            foreach (var row in CsvRow.Read(directory, "IceKnightBlade"))
            {
                string attachment = row.Text("attachment");
                if (!regions.TryGetValue(attachment, out var region)) throw row.Error("unknown blade attachment " + attachment);
                bladePoses.Add(attachment, new BladePose {
                    Bounds = new Rect(origin.x + region.x - region.width / 2,
                        origin.y - region.y - region.height / 2, region.width, region.height),
                    Guard = new Vector2(row.Number("guardX"), row.Number("guardY")),
                    Tip = new Vector2(row.Number("tipX"), row.Number("tipY"))
                });
            }
        }

        private static float Duration(Clip clip)
        {
            float end = 0;
            foreach (Track track in clip.slots.Values)
                end = Mathf.Max(end, track.attachment[track.attachment.Length - 1].time);
            return end;
        }

        private Clip SelectClip(TrialBattle.Unit unit, float elapsed, out float time)
        {
            time = 0;
            if (!unit.Alive) return null;

            const float tickTolerance = 0.00001f;
            float age = (elapsed - unit.SwingStart) * unit.SwingSpeed * ImpactTime / unit.Stats.ImpactSeconds;
            if (age + tickTolerance < recovery)
            {
                time = age + tickTolerance;
                return attack;
            }
            if (unit.Walking)
            {
                time = Mathf.Repeat(elapsed + tickTolerance, stride);
                return walk;
            }
            if (idle != null) time = Mathf.Repeat(elapsed, breathCycle);
            return idle;
        }

        internal static Vector2 ScreenPointAtHeight(Rect area, float height, out float scale)
        {
            scale = Mathf.Min(area.width / 302, area.height / 320);
            return area.center + Vector2.up * ((151 - height) * scale);
        }

        internal bool TryGetBladeSegment(Rect area, TrialBattle.Unit unit, float elapsed,
            out Vector2 guard, out Vector2 tip)
        {
            guard = tip = Vector2.zero;
            if (bladeSlot == null) return false;
            Clip clip = SelectClip(unit, elapsed, out float time);
            string name = bladeSlot.attachment;
            if (clip != null && clip.slots.TryGetValue(bladeSlot.name, out Track track))
            {
                foreach (Key key in track.attachment)
                {
                    if (key.time > time) break;
                    name = key.name;
                }
            }
            if (name == null || !bladePoses.TryGetValue(name, out var pose) || pose.Guard == pose.Tip) return false;
            Vector2 origin = ScreenPointAtHeight(area, 0, out float scale);
            var bounds = new Rect(origin + pose.Bounds.position * scale, pose.Bounds.size * scale);
            guard = Rect.NormalizedToPoint(bounds, pose.Guard);
            tip = Rect.NormalizedToPoint(bounds, pose.Tip);
            return true;
        }
    }
}
