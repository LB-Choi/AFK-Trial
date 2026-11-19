using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using UnityEngine;

namespace AFKTrial
{
    // Spine region attachments with stepped slot timelines for hand-drawn action poses.
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

        private sealed class Drawing
        {
            public Texture2D Texture;
            public Rect Bounds;
        }

        private readonly bool hasSetupPose;
        private readonly Slot[] slots;
        private readonly Dictionary<string, Drawing>[] drawings;
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
            string folder = resource.Substring(0, resource.LastIndexOf('/') + 1);
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

            var origins = new Dictionary<string, Vector2>();
            foreach (Bone bone in data.bones)
            {
                var parent = bone.parent == null ? Vector2.zero : origins[bone.parent];
                origins.Add(bone.name, parent + new Vector2(bone.x, -bone.y));
            }

            slots = data.slots;
            hasSetupPose = Array.Exists(slots, slot => slot.attachment != null);
            drawings = new Dictionary<string, Drawing>[slots.Length];
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                drawings[i] = new Dictionary<string, Drawing>();
                Vector2 origin = origins[slot.bone];
                foreach (var entry in data.skins[0].attachments[slot.name])
                {
                    Region region = entry.Value;
                    var texture = Resources.Load<Texture2D>(folder + entry.Key);
                    if (!texture) throw new InvalidDataException("Missing rig texture " + entry.Key);
                    drawings[i].Add(entry.Key, new Drawing {
                        Texture = texture,
                        Bounds = new Rect(origin.x + region.x - region.width / 2,
                            origin.y - region.y - region.height / 2, region.width, region.height)
                    });
                }
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

        public void Draw(Rect area, TrialBattle.Unit unit, float elapsed, Texture2D portrait)
        {
            if (Event.current.type != EventType.Repaint) return;
            Clip clip = SelectClip(unit, elapsed, out float time);
            if (clip == null && !hasSetupPose)
            {
                GUI.DrawTexture(area, portrait, ScaleMode.ScaleToFit);
                return;
            }
            Vector2 origin = ScreenPointAtHeight(area, 0, out float scale);
            for (int i = 0; i < slots.Length; i++)
            {
                Slot slot = slots[i];
                string name = slot.attachment;
                if (clip != null && clip.slots.TryGetValue(slot.name, out Track track))
                {
                    foreach (Key key in track.attachment)
                    {
                        if (key.time > time) break;
                        name = key.name;
                    }
                }
                if (name == null) continue;
                Drawing pose = drawings[i][name];
                GUI.DrawTexture(new Rect(origin + pose.Bounds.position * scale, pose.Bounds.size * scale), pose.Texture);
            }
        }
    }
}
