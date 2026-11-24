using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace AFKTrial.Editor
{
    public sealed class SemiSDImporter : AssetPostprocessor
    {
        private const string AtlasDirectory = "Assets/Resources/SemiSD/";
        // Top-left PNG coordinates inside reviewed background islands between pieces of artwork.
        private static readonly Dictionary<string, int[]> BackgroundSeeds = new Dictionary<string, int[]>
        {
            { "axe_warrior/attack.png", new[] { 850, 120, 497, 145, 112, 146, 222, 186, 185, 401, 1074, 466, 812, 474, 1182, 507 } },
            { "axe_warrior/death.png", new[] { 113, 145, 433, 147, 754, 185, 1186, 225, 506, 226, 800, 291 } },
            { "axe_warrior/hit.png", new[] { 114, 146, 433, 148, 749, 154, 1062, 160, 872, 192, 1179, 195, 423, 463, 1065, 463, 746, 466, 114, 471 } },
            { "axe_warrior/skill0.png", new[] { 433, 223, 1158, 236, 205, 248, 204, 524, 1079, 540 } },
            { "axe_warrior/skill1.png", new[] { 115, 144, 545, 185, 1076, 465 } },
            { "axe_warrior/skill2.png", new[] { 868, 229, 115, 476 } },
            { "axe_warrior/walk.png", new[] { 748, 137, 432, 141, 114, 146, 1077, 156, 1073, 464, 205, 495, 851, 508 } },
            { "bandit/attack.png", new[] { 84, 432 } },
            { "bandit_captain/attack.png", new[] { 1111, 56, 536, 147, 140, 157, 485, 234, 873, 471, 1099, 482 } },
            { "bandit_captain/death.png", new[] { 141, 166, 1065, 200, 1137, 205, 1204, 221, 174, 234, 479, 234, 1137, 291, 119, 524, 438, 540, 72, 559 } },
            { "bandit_captain/hit.png", new[] { 1166, 159, 140, 161, 480, 230, 790, 232, 1121, 234, 531, 474, 213, 475, 777, 486, 1099, 487, 143, 488, 456, 489, 814, 553, 1136, 553, 176, 554, 497, 554 } },
            { "bandit_captain/idle.png", new[] { 228, 155, 548, 155, 868, 155, 1188, 156, 104, 371, 228, 475, 548, 475, 868, 475, 1188, 475, 1112, 480 } },
            { "bandit_captain/special.png", new[] { 813, 50, 233, 156, 868, 166, 1105, 168, 1194, 475, 156, 495 } },
            { "bandit_captain/walk.png", new[] { 140, 157, 456, 166, 496, 229, 176, 232, 802, 233, 534, 473, 213, 474, 1097, 476, 849, 478, 1138, 562 } },
            { "bandit_chief/death.png", new[] { 383, 550, 72, 595 } },
            { "bandit_chief/walk.png", new[] { 433, 249, 1070, 253 } },
            { "banner_paladin/attack.png", new[] { 167, 110 } },
            { "banner_paladin/death.png", new[] { 126, 112 } },
            { "banner_paladin/hit.png", new[] { 127, 111 } },
            { "banner_paladin/skill0.png", new[] { 1129, 626 } },
            { "banner_paladin/skill1.png", new[] { 1105, 305, 450, 503 } },
            { "banner_paladin/walk.png", new[] { 126, 112, 1087, 429 } },
            { "hammer_maid/death.png", new[] { 860, 596, 1182, 596 } },
            { "hammer_maid/skill1.png", new[] { 826, 163, 1137, 166, 496, 483, 177, 484 } },
            { "ice_knight/attack.png", new[] { 698, 176, 376, 177, 1016, 177, 61, 181, 1032, 191, 702, 467, 52, 500, 1016, 500, 699, 501, 399, 504, 729, 507, 70, 512, 1031, 518 } },
            { "ice_knight/death.png", new[] { 358, 162, 53, 174, 370, 179, 64, 191, 696, 203, 93, 208, 763, 279 } },
            { "ice_knight/hit.png", new[] { 51, 175, 65, 190, 383, 194, 93, 208, 690, 489, 1010, 495, 385, 505, 702, 507, 1025, 508, 1053, 528 } },
            { "ice_knight/skill0.png", new[] { 75, 240, 1035, 557, 703, 561 } },
            { "ice_knight/skill1.png", new[] { 696, 239, 74, 240, 1013, 241, 52, 558, 410, 562 } },
            { "ice_knight/skill2.png", new[] { 386, 236, 708, 239, 71, 241, 1030, 241, 1029, 557, 384, 561 } },
            { "ice_knight/walk.png", new[] { 716, 186, 63, 191, 94, 208, 734, 492, 1013, 500, 420, 512 } },
            { "katana_knight/attack.png", new[] { 147, 90, 1107, 411 } },
            { "katana_knight/death.png", new[] { 191, 474 } },
            { "katana_knight/hit.png", new[] { 147, 91, 777, 409, 1106, 409 } },
            { "katana_knight/skill1.png", new[] { 773, 303, 1090, 303, 164, 305, 463, 306, 130, 623, 449, 623, 770, 623, 1127, 625 } },
            { "katana_knight/skill2.png", new[] { 450, 303, 772, 303, 1090, 303, 168, 305, 777, 624, 1129, 625 } },
            { "katana_knight/walk.png", new[] { 147, 91, 1109, 91, 1108, 410 } },
            { "lion_guardian/death.png", new[] { 798, 280 } },
            { "royal_knight/attack.png", new[] { 110, 399, 78, 402 } },
            { "royal_knight/death.png", new[] { 1058, 207, 156, 282 } },
            { "royal_knight/hit.png", new[] { 1135, 601 } },
            { "royal_knight/skill0.png", new[] { 460, 302, 156, 305, 1110, 623 } },
            { "royal_knight/skill1.png", new[] { 1052, 299, 786, 301, 154, 304, 761, 304, 433, 619, 1115, 624 } },
            { "scythe_knight/attack.png", new[] { 1076, 41, 125, 175, 1076, 175, 436, 178, 772, 183, 110, 222, 402, 226, 461, 415, 361, 434, 748, 489, 42, 497, 1084, 497, 507, 506, 756, 524, 1078, 533 } },
            { "scythe_knight/death.png", new[] { 115, 168, 443, 173, 1038, 216, 420, 229, 366, 247, 1002, 272, 1067, 291, 119, 465, 482, 471, 82, 531, 41, 565, 992, 595, 101, 598 } },
            { "scythe_knight/hit.png", new[] { 693, 118, 114, 169, 441, 178, 737, 183, 1071, 190, 433, 222, 1067, 231, 375, 245, 1074, 489, 761, 492, 122, 494, 440, 496, 122, 543, 439, 543, 757, 544 } },
            { "scythe_knight/skill0.png", new[] { 118, 223, 423, 223, 373, 256, 374, 279, 706, 281, 581, 561, 855, 564, 886, 575 } },
            { "scythe_knight/skill2.png", new[] { 260, 239, 1220, 559 } },
            { "scythe_knight/walk.png", new[] { 1082, 160, 765, 169, 437, 171, 122, 174, 1009, 231, 393, 435, 443, 484, 765, 485, 125, 486, 1085, 493, 50, 551, 371, 553 } },
            { "sun_paladin/attack.png", new[] { 1163, 49 } },
            { "sun_paladin/skill0.png", new[] { 137, 304 } },
            { "tower_knight/attack.png", new[] { 729, 52, 1146, 152, 498, 153, 88, 376, 761, 460, 825, 468, 514, 471 } },
            { "tower_knight/death.png", new[] { 184, 150, 825, 174, 1128, 175, 161, 498 } },
            { "tower_knight/hit.png", new[] { 697, 77, 504, 146, 184, 149, 1137, 154, 827, 159, 720, 447, 410, 451, 1145, 469, 823, 470, 508, 473, 195, 481 } },
            { "tower_knight/skill0.png", new[] { 499, 217, 820, 223 } },
            { "tower_knight/skill1.png", new[] { 499, 221 } },
            { "tower_knight/skill2.png", new[] { 1101, 296 } },
            { "tower_knight/walk.png", new[] { 1050, 51, 1146, 160, 747, 164, 823, 465, 186, 478 } },
        };

        public override uint GetVersion() => 1;

        void OnPostprocessTexture(Texture2D texture)
        {
            if (!assetPath.StartsWith(AtlasDirectory) || texture.width != 1280 || texture.height != 640 ||
                !BackgroundSeeds.TryGetValue(assetPath.Substring(AtlasDirectory.Length), out var seeds)) return;

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
            if (!assetPath.StartsWith(AtlasDirectory)) return;
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
