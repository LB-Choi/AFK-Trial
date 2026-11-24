using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    public sealed class SemiSDImporter : AssetPostprocessor
    {
        private const string AtlasDirectory = "Assets/Resources/SemiSD/";
        internal const string HitDirectory = "Assets/Editor/SemiSDHit/";
        // Top-left coordinates of background islands in the remaining original artwork.
        private static readonly Dictionary<string, int[]> BackgroundSeeds = new Dictionary<string, int[]>
        {
            { "axe_warrior/death.png", new[] { 113, 145, 433, 147, 754, 185, 1186, 225, 506, 226, 800, 291 } },
            { "axe_warrior/hit.png", new[] { 114, 146, 433, 148, 749, 154, 1062, 160, 872, 192, 1179, 195, 423, 463, 1065, 463, 746, 466, 114, 471 } },
            { "axe_warrior/skill0.png", new[] { 433, 223, 205, 248, 1079, 540 } },
            { "axe_warrior/skill1.png", new[] { 115, 144, 545, 185, 1076, 465 } },
            { "bandit_captain/death.png", new[] { 141, 166, 1065, 200, 1137, 205, 1204, 221, 174, 234, 479, 234, 1137, 291, 119, 524, 438, 540, 72, 559 } },
            { "bandit_captain/hit.png", new[] { 1166, 159, 140, 161, 480, 230, 790, 232, 1121, 234, 531, 474, 213, 475, 777, 486, 1099, 487, 143, 488, 456, 489, 814, 553, 1136, 553, 176, 554, 497, 554 } },
            { "bandit_captain/special.png", new[] { 233, 156, 1194, 475 } },
            { "bandit_chief/death.png", new[] { 383, 550, 72, 595 } },
            { "banner_paladin/death.png", new[] { 126, 112 } },
            { "banner_paladin/hit.png", new[] { 127, 111 } },
            { "banner_paladin/skill0.png", new[] { 1129, 626 } },
            { "hammer_maid/death.png", new[] { 860, 596, 1182, 596 } },
            { "ice_knight/death.png", new[] { 358, 162, 53, 174, 370, 179, 64, 191, 696, 203, 93, 208, 763, 279 } },
            { "ice_knight/hit.png", new[] { 51, 175, 65, 190, 383, 194, 93, 208, 690, 489, 1010, 495, 385, 505, 702, 507, 1025, 508, 1053, 528 } },
            { "ice_knight/skill0.png", new[] { 75, 240, 1035, 557, 703, 561 } },
            { "ice_knight/skill1.png", new[] { 74, 240 } },
            { "ice_knight/skill2.png", new[] { 386, 236, 71, 241, 1029, 557 } },
            { "katana_knight/death.png", new[] { 191, 474 } },
            { "katana_knight/hit.png", new[] { 147, 91, 777, 409, 1106, 409 } },
            { "katana_knight/skill1.png", new[] { 164, 305, 1127, 625 } },
            { "katana_knight/skill2.png", new[] { 450, 303, 168, 305, 777, 624, 1129, 625 } },
            { "lion_guardian/death.png", new[] { 798, 280 } },
            { "royal_knight/death.png", new[] { 1058, 207, 156, 282 } },
            { "royal_knight/hit.png", new[] { 1135, 601 } },
            { "royal_knight/skill0.png", new[] { 460, 302, 156, 305, 1110, 623 } },
            { "royal_knight/skill1.png", new[] { 154, 304, 1115, 624 } },
            { "scythe_knight/death.png", new[] { 115, 168, 443, 173, 1038, 216, 420, 229, 366, 247, 1002, 272, 1067, 291, 119, 465, 482, 471, 82, 531, 41, 565, 992, 595, 101, 598 } },
            { "scythe_knight/hit.png", new[] { 693, 118, 114, 169, 441, 178, 737, 183, 1071, 190, 433, 222, 1067, 231, 375, 245, 1074, 489, 761, 492, 122, 494, 440, 496, 122, 543, 439, 543, 757, 544 } },
            { "scythe_knight/skill0.png", new[] { 118, 223, 423, 223, 373, 256, 374, 279 } },
            { "scythe_knight/skill2.png", new[] { 260, 239, 1220, 559 } },
            { "sun_paladin/skill0.png", new[] { 137, 304 } },
            { "tower_knight/death.png", new[] { 184, 150, 825, 174, 1128, 175, 161, 498 } },
            { "tower_knight/hit.png", new[] { 697, 77, 504, 146, 184, 149, 1137, 154, 827, 159, 720, 447, 410, 451, 1145, 469, 823, 470, 508, 473, 195, 481 } },
            { "tower_knight/skill0.png", new[] { 499, 217 } },
            { "tower_knight/skill1.png", new[] { 499, 221 } },
        };

        public override uint GetVersion() => 3;

        private string AtlasKey()
        {
            if (assetPath.StartsWith(AtlasDirectory, System.StringComparison.Ordinal))
                return assetPath.Substring(AtlasDirectory.Length);
            if (assetPath.StartsWith(HitDirectory, System.StringComparison.Ordinal))
                return System.IO.Path.GetFileNameWithoutExtension(assetPath) + "/hit.png";
            return null;
        }

        void OnPostprocessTexture(Texture2D texture)
        {
            string key = AtlasKey();
            if (key == null || texture.width != 1280 || texture.height != 640 ||
                !BackgroundSeeds.TryGetValue(key, out var seeds)) return;

            var pixels = texture.GetPixels32();
            var pending = new Stack<int>();
            for (int i = 0; i < seeds.Length; i += 2)
            {
                int x = seeds[i], y = texture.height - 1 - seeds[i + 1];
                int left = x / 320 * 320, bottom = y / 320 * 320;
                pending.Push(y * texture.width + x);
                while (pending.Count > 0)
                {
                    int index = pending.Pop();
                    Color32 pixel = pixels[index];
                    int low = System.Math.Min(pixel.r, System.Math.Min(pixel.g, pixel.b));
                    int high = System.Math.Max(pixel.r, System.Math.Max(pixel.g, pixel.b));
                    if (pixel.a == 0 || low < 180 || high > 254 || high - low > 16) continue;
                    pixels[index].a = 0;
                    int px = index % texture.width, py = index / texture.width;
                    if (px > left) pending.Push(index - 1);
                    if (px < left + 319) pending.Push(index + 1);
                    if (py > bottom) pending.Push(index - texture.width);
                    if (py < bottom + 319) pending.Push(index + texture.width);
                }
            }
            texture.SetPixels32(pixels);
            texture.Apply(false);
        }

        void OnPreprocessTexture()
        {
            if (AtlasKey() == null) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.alphaSource = TextureImporterAlphaSource.FromInput;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.isReadable = false;
            importer.npotScale = TextureImporterNPOTScale.None;
        }
    }
}
