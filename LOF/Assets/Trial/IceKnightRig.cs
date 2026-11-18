using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using UnityEngine;

namespace AFKTrial
{
    // Spine region attachments with stepped slot timelines for hand-drawn action poses.
    public sealed class IceKnightRig
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

        private readonly Slot[] slots;
        private readonly Dictionary<string, Drawing>[] drawings;
        private readonly Clip walk, attack;
        private readonly float stride, recovery;
        public readonly float ImpactTime;

        public IceKnightRig()
        {
            const string folder = "IceKnightRig/";
            var json = Resources.Load<TextAsset>(folder + "IceKnight");
            var reader = new DataContractJsonSerializer(typeof(Data),
                new DataContractJsonSerializerSettings { UseSimpleDictionaryFormat = true });
            Data data;
            using (var stream = new MemoryStream(json.bytes))
                data = (Data)reader.ReadObject(stream);

            walk = data.animations["walk"];
            attack = data.animations["attack"];
            stride = Duration(walk);
            recovery = Duration(attack);
            ImpactTime = Array.Find(attack.events, marker => marker.name == "impact").time;

            var origins = new Dictionary<string, Vector2>();
            foreach (Bone bone in data.bones)
            {
                var parent = bone.parent == null ? Vector2.zero : origins[bone.parent];
                origins.Add(bone.name, parent + new Vector2(bone.x, -bone.y));
            }

            slots = data.slots;
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

        public void Draw(Rect area, TrialBattle.Unit unit, float elapsed)
        {
            if (Event.current.type != EventType.Repaint) return;
            const float tickTolerance = 0.00001f;
            float age = (elapsed - unit.SwingStart) * unit.SwingSpeed;
            Clip clip = unit.Alive && age < recovery ? attack : unit.Alive && unit.Walking ? walk : null;
            float time = clip == walk ? Mathf.Repeat(elapsed + tickTolerance, stride) : age + tickTolerance;
            float scale = Mathf.Min(area.width / 302, area.height / 320);
            Vector2 origin = area.center + Vector2.up * (151 * scale);
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
