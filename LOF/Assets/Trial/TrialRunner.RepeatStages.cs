using System;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private const int RepeatColumns = 5;
        private bool choosingRepeat;
        private int repeatPage, selectedRepeat;
        private Vector2 repeatScroll;

        private void OpenRepeatStages()
        {
            selectedRepeat = battle.RepeatStage > 0 ? battle.RepeatStage : battle.Highest;
            repeatPage = Math.Max(0, selectedRepeat - 1) / battle.Data.RoundLength;
            repeatScroll = Vector2.zero;
            choosingRepeat = true;
        }

        private void SaveRepeatStage(int stage)
        {
            if (progressLock == null || suspended || HasPendingIdleReward) return;
            lastSeenUtc = Math.Max(lastSeenUtc, UtcNow);
            if (battle.SetRepeatStage(stage, progress => TrySaveProgress(progress, pendingSeconds)))
                choosingRepeat = false;
        }

        private void DrawRepeatStages(int width, int height)
        {
            bool portrait = height > width;
            if (DrawRosterHeader(width, "Repeat stage", "Knight Hall"))
            {
                choosingRepeat = false;
                return;
            }
            int length = battle.Data.RoundLength;
            int lastPage = Math.Max(0, battle.Highest - 1) / length;
            float sheetWidth = portrait ? width - 36 : 740;
            float bannerHeight = portrait ? 155 : 91;
            float rowHeight = portrait ? 88 : 60;
            float gridTop = bannerHeight + 19;
            float gridHeight = 2 * rowHeight + 8;
            float summaryTop = gridTop + gridHeight + 14;
            float sheetHeight = summaryTop + 119;
            var sheet = new Rect((width - sheetWidth) * 0.5f, 62 + (height - 72 - sheetHeight) * 0.5f,
                sheetWidth, sheetHeight);

            GUI.BeginGroup(sheet);
            DrawKnightPanel(new Rect(0, 0, sheet.width, sheet.height));
            var banner = new Rect(4, 4, sheetWidth - 8, bannerHeight - 4);
            // Keep the castle and horizon in view even in the shallow landscape banner.
            float bannerAspect = banner.width / banner.height * courtyard.height / courtyard.width;
            float cropHeight = Mathf.Min(1, 1 / bannerAspect);
            float cropWidth = Mathf.Min(1, bannerAspect);
            GUI.DrawTextureWithTexCoords(banner, courtyard, new Rect(0, 1 - cropHeight, cropWidth, cropHeight));
            ManuscriptFade(banner, new Color32(10, 19, 32, 111), new Color32(10, 19, 32, 239));
            CourtHeading(new Rect(74, 10, sheetWidth - 148, bannerHeight - 33),
                "Round " + (repeatPage + 1), portrait ? 40 : 32, TextAnchor.MiddleCenter);
            int roundBase = repeatPage * length;
            int cleared = Math.Min(length, Math.Max(0, battle.Highest - roundBase));
            ArcadeText(new Rect(70, bannerHeight - 34, sheetWidth - 140, 25),
                cleared + " / " + length + " stages cleared", 14, Parchment, TextAnchor.MiddleCenter);
            CourtRule(new Rect(7, bannerHeight, sheetWidth - 14, 1), FrameGold);
            GUI.enabled = repeatPage > 0;
            bool previous = GUI.Button(new Rect(17, (bannerHeight - 42) * 0.5f, 54, 42), "‹", button);
            GUI.enabled = repeatPage < lastPage;
            bool next = GUI.Button(new Rect(sheetWidth - 71, (bannerHeight - 42) * 0.5f, 54, 42), "›", button);
            GUI.enabled = true;

            DrawRepeatGrid(new Rect(15, gridTop, sheetWidth - 30, gridHeight), rowHeight, roundBase, length);

            CourtRule(new Rect(17, summaryTop, sheetWidth - 34, 1), FrameGold);
            string target = battle.RepeatStage == 0 ? "Off" : battle.StageName(battle.RepeatStage);
            ArcadeText(new Rect(17, summaryTop + 4, sheetWidth - 34, 25),
                "Current repeat   /   " + target, 15, Parchment, TextAnchor.MiddleLeft);
            string hint = battle.Highest == 0 ? "Clear a stage to unlock it for repeat."
                : selectedRepeat > 0 ? "Selected: " + battle.StageName(selectedRepeat) + "   ·   Applies next battle"
                : "Select a cleared stage.";
            GUI.Label(new Rect(17, summaryTop + 30, sheetWidth - 34, 29), idleSaveError ?? hint, small);
            GUI.enabled = battle.RepeatStage > 0;
            if (GUI.Button(new Rect(16, summaryTop + 62, 156, 43), "Stop repeating", button)) SaveRepeatStage(0);
            GUI.enabled = selectedRepeat > roundBase && selectedRepeat <= battle.Highest &&
                (long)selectedRepeat <= (long)roundBase + length && selectedRepeat != battle.RepeatStage;
            if (GUI.Button(new Rect(sheetWidth - 174, summaryTop + 62, 158, 43), "Set repeat", knightSelectedButton))
                SaveRepeatStage(selectedRepeat);
            GUI.enabled = true;
            GUI.EndGroup();
            if (previous || next)
            {
                repeatPage += previous ? -1 : 1;
                selectedRepeat = battle.RepeatStage > 0 && (battle.RepeatStage - 1) / length == repeatPage
                    ? battle.RepeatStage : 0;
                repeatScroll = Vector2.zero;
            }
        }

        private void DrawRepeatGrid(Rect region, float rowHeight, int roundBase, int length)
        {
            const int gap = 8;
            int count = Math.Min(length, int.MaxValue - roundBase);
            int rows = 1 + (count - 1) / RepeatColumns;
            float stride = rowHeight + gap;
            bool scroll = rows > 2;
            float usableWidth = region.width - (scroll ? 20 : 0);
            if (scroll)
                repeatScroll = GUI.BeginScrollView(region, repeatScroll, new Rect(0, 0, usableWidth, rows * stride - gap));
            else
                GUI.BeginGroup(region);
            float tileWidth = (usableWidth - gap * (RepeatColumns - 1)) / RepeatColumns;
            int firstRow = scroll ? Mathf.Clamp(Mathf.FloorToInt(repeatScroll.y / stride), 0, rows - 1) : 0;
            int endRow = Math.Min(rows, firstRow + 3);
            for (int row = firstRow; row < endRow; row++)
                for (int col = 0; col < RepeatColumns; col++)
                {
                    long index = (long)row * RepeatColumns + col;
                    if (index >= count) break;
                    var tile = new Rect(col * (tileWidth + gap), row * stride, tileWidth, rowHeight);
                    DrawRepeatTile(tile, roundBase + (int)index + 1, (int)index + 1);
                }
            if (scroll) GUI.EndScrollView();
            else GUI.EndGroup();
        }

        private void DrawRepeatTile(Rect tile, int stage, int ordinal)
        {
            bool cleared = stage <= battle.Highest;
            bool boss = stage % battle.Data.BossEvery == 0;
            bool selected = stage == selectedRepeat;
            Color gold = selected ? FrameLight : FrameGold;
            if (cleared && GUI.Button(tile, GUIContent.none, GUIStyle.none)) selectedRepeat = stage;
            bool highlighted = selected || cleared && tile.Contains(Event.current.mousePosition);
            Fill(tile, highlighted ? gold : new Color32(67, 71, 78, 255));
            var inside = new Rect(tile.x + 1, tile.y + 1, tile.width - 2, tile.height - 2);
            ManuscriptFade(inside, highlighted ? new Color32(81, 68, 52, 255) : new Color32(37, 44, 57, 255),
                new Color32(17, 25, 36, 255));
            string number = ordinal.ToString("00");
            var numeral = new Rect(tile.x + 3, tile.center.y - 23, tile.width - 6, 31);
            if (cleared) CourtHeading(numeral, number, 26, TextAnchor.MiddleCenter);
            else ArcadeText(numeral, number, 21, new Color32(161, 168, 177, 255), TextAnchor.MiddleCenter);
            if (boss) GUI.DrawTexture(new Rect(tile.xMax - 23, tile.y + 3, 19, 19), bossMark, ScaleMode.ScaleToFit);
            string state = !cleared ? "Locked" : stage == battle.RepeatStage ? "Repeating" : boss ? "Boss" : "Cleared";
            ArcadeText(new Rect(tile.x, tile.center.y + 6, tile.width, 20), state,
                12, cleared ? FrameLight : new Color32(164, 173, 186, 255), TextAnchor.MiddleCenter);
            if (selected) CourtRule(new Rect(tile.x + 9, tile.yMax - 4, tile.width - 18, 1), gold);
        }
    }
}
