using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private enum SystemMenu { Closed, Settings, Help, Guides, Information, Reset, Quit, Goals, BossInfo, Audio }
        [Serializable]
        private sealed class DisplaySettings
        {
            public int version = 2;
            public bool showDamageNumbers = true;
            public float musicVolume = .3f, effectsVolume = .65f;
        }

        private SystemMenu systemMenu;
        private bool showDamageNumbers = true;
        private string settingsPath, settingsError, resetConfirmation = "";
        private int helpStep;
        private GUIStyle settingsBody;
        private bool SystemMenuOpen => systemMenu != SystemMenu.Closed;
        private bool CanOpenSystemMenu => !GuideOpen && !showRecruit && !HasPendingIdleReward && !HasDungeonResult && !HasBossReview;

        private void LoadDisplaySettings()
        {
            if (string.IsNullOrEmpty(settingsPath))
                settingsPath = Path.Combine(ProfileFolder, "settings.json");
            showDamageNumbers = true;
            musicVolume = .3f; effectsVolume = .65f;
            settingsError = null;
            try
            {
                if (!File.Exists(settingsPath)) return;
                var saved = JsonUtility.FromJson<DisplaySettings>(File.ReadAllText(settingsPath));
                if (saved == null || saved.version < 1 || saved.version > 2) throw new InvalidDataException("Unsupported display settings.");
                showDamageNumbers = saved.showDamageNumbers;
                if (saved.version >= 2)
                {
                    if (float.IsNaN(saved.musicVolume) || float.IsInfinity(saved.musicVolume) ||
                        float.IsNaN(saved.effectsVolume) || float.IsInfinity(saved.effectsVolume)) throw new InvalidDataException("Invalid volume.");
                    musicVolume = Mathf.Clamp01(saved.musicVolume); effectsVolume = Mathf.Clamp01(saved.effectsVolume);
                }
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
            {
                settingsError = "표시 설정을 불러오지 못했습니다. 피해 숫자는 기본값인 켜짐으로 표시합니다.";
                Debug.LogWarning(settingsError);
            }
        }

        private bool SetDamageNumbers(bool enabled) => SaveDisplaySettings(enabled, musicVolume, effectsVolume);

        private bool SaveDisplaySettings(bool enabled, float music, float effects)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(settingsPath));
                WriteAtomically(settingsPath, Encoding.UTF8.GetBytes(JsonUtility.ToJson(new DisplaySettings { showDamageNumbers = enabled, musicVolume = music, effectsVolume = effects })));
                showDamageNumbers = enabled;
                musicVolume = music; effectsVolume = effects;
                settingsError = null;
                return true;
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                settingsError = "설정을 저장하지 못했습니다. 저장 공간을 확인한 뒤 다시 시도해 주세요.";
                return false;
            }
        }

        private void OpenSystemMenu()
        {
            if (!CanOpenSystemMenu) return;
            systemMenu = SystemMenu.Settings;
            resetConfirmation = "";
        }

        private void BackFromSystemMenu()
        {
            if (systemMenu == SystemMenu.Audio && uiAudio != null) uiAudio.Stop();
            systemMenu = (systemMenu == SystemMenu.Settings || systemMenu == SystemMenu.Goals || systemMenu == SystemMenu.BossInfo) ? SystemMenu.Closed : SystemMenu.Settings;
            resetConfirmation = "";
            GUI.FocusControl(null);
        }

        private bool TryResetProgress()
        {
            if (systemMenu != SystemMenu.Reset || resetConfirmation != "초기화" || progressLock == null || suspended) return false;
            var fresh = new TrialBattle(battle.Data);
            long oldSeen = lastSeenUtc;
            try
            {
                // Remove the recovery copy first so an old victory can never resurrect a reset profile.
                // If the main write fails, the existing main save and live battle remain intact.
                if (File.Exists(victoryBackupPath)) File.Delete(victoryBackupPath);
                lastSeenUtc = UtcNow;
                WriteProgress(fresh.CaptureProgress(), 0);
            }
            catch (Exception error) when (error is IOException || error is UnauthorizedAccessException)
            {
                lastSeenUtc = oldSeen;
                settingsError = "초기화 저장에 실패했습니다. 현재 진행은 유지됩니다. 다시 시도해 주세요.";
                return false;
            }
            battle.SkillApplied -= PlaySkillEffect;
            battle = fresh;
            ResetGuideUI();
            restartAfterAway = applicationPaused = false;
            battle.SkillApplied += PlaySkillEffect;
            playingSkills.Clear();
            pendingSeconds = equipmentSeed = 0;
            PendingIdleReward = default;
            idleSaveError = settingsError = null;
            resultDungeon = null;
            bossReview = bossPrevious = null;
            bossResultPending = false;
            showRecruit = editing = choosingRepeat = false;
            featurePage = -1;
            heroView = HeroView.Menu;
            selectedDungeon = null;
            selectedGear = pendingSale = null;

            selectedSkill = -1;
            featureHero = selectedSlot = dungeonDifficulty = inventoryPage = 0;
            gearFilter = -1;
            accumulator = restartDelay = 0;
            animationStart = float.NegativeInfinity;
            progressLock.Dispose();
            progressLock = null;
            atTitle = true;
            systemMenu = SystemMenu.Closed;
            resetConfirmation = "";
            return true;
        }

        private bool SaveBeforeQuit()
        {
            if (progressLock == null) return true;
            CheckpointProgress();
            if (string.IsNullOrEmpty(idleSaveError)) return true;
            settingsError = "저장 실패로 종료를 취소했습니다. 저장 공간을 확인한 뒤 다시 시도해 주세요.";
            return false;
        }

        private void QuitGame()
        {
            if (!SaveBeforeQuit()) return;
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        private void DrawSystemMenu(int width, int height)
        {
            if (systemMenu == SystemMenu.Audio) { DrawAudioSettings(width, height); return; }
            if (systemMenu == SystemMenu.BossInfo) { DrawBossInformation(width, height); return; }
            if (systemMenu == SystemMenu.Goals) { DrawGoals(width, height); return; }
            if (settingsBody == null)
                settingsBody = new GUIStyle(small) { fontSize = 17, wordWrap = true, alignment = TextAnchor.UpperLeft };
            if (systemMenu == SystemMenu.Help) { DrawSpotlightHelp(width, height); return; }
            Fill(new Rect(0, 0, width, height), new Color(0.015f, 0.025f, 0.04f, .82f));
            const float panelWidth = 464, panelHeight = 490;
            GUI.BeginGroup(new Rect((width - panelWidth) / 2, (height - panelHeight) / 2, panelWidth, panelHeight));
            DrawKnightPanel(new Rect(0, 0, panelWidth, panelHeight));
            string heading = systemMenu == SystemMenu.Settings ? "설정" : systemMenu == SystemMenu.Information ? "게임 정보" : systemMenu == SystemMenu.Guides ? "상황별 안내 다시 보기" :
                systemMenu == SystemMenu.Reset ? "진행을 초기화할까요?" : "게임을 종료할까요?";
            CourtHeading(new Rect(24, 14, 360, 42), heading, 29, TextAnchor.MiddleLeft);
            ArcadeText(new Rect(25, 58, 410, 23), atTitle ? Application.productName : "전투 일시정지", 13, HallAmber, TextAnchor.MiddleLeft);
            CourtRule(new Rect(24, 87, 416, 1), HallLine);
            if (systemMenu == SystemMenu.Settings)
            {
                GUI.Label(new Rect(26, 108, 275, 31), "피해 숫자 표시", label);
                if (GUI.Button(new Rect(324, 104, 112, 35), showDamageNumbers ? "켜짐" : "꺼짐", button)) SetDamageNumbers(!showDamageNumbers);
                ArcadeText(new Rect(26, 144, 410, 24), "떠오르는 피해 숫자만 표시하거나 숨깁니다.", 14, BodyInk, TextAnchor.MiddleLeft);
                GUI.enabled = !atTitle;
                if (GUI.Button(new Rect(26, 184, 200, 43), "도움말 / 화면 안내", button)) { helpStep = 0; systemMenu = SystemMenu.Help; }
                GUI.enabled = true;
                if (GUI.Button(new Rect(238, 184, 200, 43), "게임 정보", button)) systemMenu = SystemMenu.Information;
                GUI.enabled = progressLock != null && !suspended;
                if (GUI.Button(new Rect(26, 243, 200, 43), "진행 초기화", button)) { systemMenu = SystemMenu.Reset; resetConfirmation = ""; settingsError = null; }
                GUI.enabled = true;
                if (GUI.Button(new Rect(238, 243, 200, 43), "게임 종료", button)) { systemMenu = SystemMenu.Quit; settingsError = null; }
                GUI.enabled = !atTitle;
                if (GUI.Button(new Rect(26, 300, 412, 43), "도움말 / 상황별 최초 안내", button)) systemMenu = SystemMenu.Guides;
                GUI.enabled = true;
                if (GUI.Button(new Rect(26, 354, 412, 35), "소리 설정 / 배경음·효과음", button))
                { draftMusic = musicVolume; draftEffects = effectsVolume; audioSaveNotice = null; systemMenu = SystemMenu.Audio; }
            }
            else if (systemMenu == SystemMenu.Guides) DrawGuideList();
            else if (systemMenu == SystemMenu.Information)
            {
                GUI.Label(new Rect(26, 109, 412, 275), Application.productName + "  /  " + Application.version +
                    "\n\n로컬 싱글 플레이 자동 전투 게임입니다.\n기사 10종을 수집하고 1~3인 편성을 구성하여 메인 스테이지와 재화 던전을 공략하세요." +
                    "\n\n진행은 이 컴퓨터에 저장됩니다. 표시 설정은 별도로 저장되며 진행 초기화 후에도 유지됩니다." +
                    "\n\n유니티로 제작했습니다. 기획·아트·구현에 인공지능을 활용했습니다.", settingsBody);
            }
            else if (systemMenu == SystemMenu.Reset)
            {
                GUI.Label(new Rect(26, 108, 412, 155), "이 컴퓨터의 모든 게임 진행을 초기화합니다.\n기사·레벨·스킬·장비·재화·소환·스테이지·던전 최초 클리어 보상이 초기 상태로 돌아갑니다.\n\n방치 보상과 복구 저장도 삭제됩니다.\n초기화 후에는 되돌릴 수 없습니다.", settingsBody);
                GUI.Label(new Rect(26, 270, 412, 27), "확인하려면 '초기화'를 입력하세요:", label);
                GUI.SetNextControlName("ResetConfirmation");
                resetConfirmation = GUI.TextField(new Rect(26, 303, 412, 32), resetConfirmation, 5);
                GUI.enabled = resetConfirmation == "초기화" && progressLock != null && !suspended;
                if (GUI.Button(new Rect(26, 345, 412, 40), "진행 삭제 후 제목 화면으로", button)) TryResetProgress();
                GUI.enabled = true;
            }
            else
            {
                GUI.Label(new Rect(26, 111, 412, 115), "현재 진행을 저장한 뒤 종료합니다.\n\n다시 접속하면 현재 메인 스테이지를 처음부터 시작하며 부재 시간에 따른 방치 보상을 계산합니다.", settingsBody);
                if (GUI.Button(new Rect(26, 262, 412, 45), "저장하고 종료", button)) QuitGame();
            }
            if (!string.IsNullOrEmpty(settingsError)) GUI.Label(new Rect(26, 390, 412, 46), settingsError, small);
            if (GUI.Button(new Rect(130, 445, 204, 32), systemMenu == SystemMenu.Settings ? "닫기 / 재개" : "취소 / 뒤로", button)) BackFromSystemMenu();
            GUI.EndGroup();
        }

        private Rect HelpFocus(int width, int height)
        {
            float tray = sideBattle ? 420 : 696;
            switch (helpStep)
            {
                case 0: return new Rect(6, 4, width - 12, 106 + StageHeaderOffset);
                case 1: return sideBattle ? new Rect(12, tray + 6, 488, 107) : new Rect(12, tray + 9, width - 24, 164);
                case 2: return sideBattle ? new Rect(509, tray + 5, width - 515, 110) : new Rect(4, 875, width - 8, 80);
                default: return new Rect(10, tray - 47, width - 20, 45);
            }
        }

        private static Rect[] SpotlightDimAreas(int width, int height, Rect focus)
        {
            return new[] {
                new Rect(0, 0, width, focus.yMin), new Rect(0, focus.yMax, width, height - focus.yMax),
                new Rect(0, focus.yMin, focus.xMin, focus.height), new Rect(focus.xMax, focus.yMin, width - focus.xMax, focus.height)
            };
        }

        private void DrawSpotlightHelp(int width, int height)
        {
            Rect focus = HelpFocus(width, height);
            foreach (var area in SpotlightDimAreas(width, height, focus)) Fill(area, new Color(0.01f, .015f, .025f, .86f));
            CourtRule(new Rect(focus.x, focus.y, focus.width, 2), HallAmber);
            CourtRule(new Rect(focus.x, focus.yMax - 2, focus.width, 2), HallAmber);
            Fill(new Rect(focus.x, focus.y, 2, focus.height), HallAmber);
            Fill(new Rect(focus.xMax - 2, focus.y, 2, focus.height), HallAmber);
            bool dungeon = battle.ActiveDungeon != null;
            string[] headings = { "스테이지와 전투 시간", "출전 중인 기사", dungeon ? "던전 보상" : "성장과 콘텐츠", dungeon ? "던전 전투 결과" : "패배·파밍·재도전" };
            string[] text = {
                "전투는 자동으로 진행됩니다. 각 회차의 5·10스테이지에서 보스가 등장하며 상단에는 남은 전투 시간이 표시됩니다.\n\n오른쪽 위 설정을 열면 전투가 멈춥니다. 던전에서 퇴각하면 보상 없이 돌아옵니다.",
                "현재 전투에 출전한 기사와 남은 체력입니다.\n\n기사는 1~3명까지 편성할 수 있습니다. 1번은 전열이며 주는 피해·받는 피해가 20% 증가합니다. 성장·장비·편성 변경은 다음 전투부터 적용됩니다.",
                dungeon ? "승리하면 표시된 던전 재화를 획득합니다. 각 던전의 난이도별 최초 승리에는 소환권 1장도 지급됩니다.\n\n패배·퇴각 시에는 보상이 없습니다. 결과창을 닫으면 메인 스테이지로 돌아갑니다." :
                    "영웅에서 레벨·스킬·장비를 성장시키고 편성에서 출전 기사를 선택하세요. 소환에서는 소환권으로 기사를 영입합니다.\n\n홀에서 장비·던전·스테이지 반복·방치 보상을 이용할 수 있습니다. 던전은 첫 5스테이지 보스 처치 후 해금되며 방치 보상은 최대 20시간 누적됩니다.",
                dungeon ? "현재 전투 결과가 표시되는 곳입니다. 패배해도 최초 클리어 보상 기회는 사라지지 않습니다.\n\n던전별로 쉬움·보통·어려움 순서로 해금됩니다. 클리어한 난이도를 반복하면 재화는 얻지만 최초 소환권은 다시 지급되지 않습니다." :
                    "패배하면 낮은 스테이지에서 자동으로 파밍합니다. 기사를 성장시키거나 편성을 바꾼 뒤 이곳의 재도전 버튼을 누르세요.\n\n직접 지정한 반복 스테이지는 홀에서 중지할 때까지 유지됩니다. 진행은 자동 저장되며 설정을 닫으면 같은 전투가 이어집니다."
            };
            const float boxWidth = 464, boxHeight = 270;
            float top = helpStep == 0 ? (sideBattle ? 167 : 230) : (sideBattle ? 96 : 210);
            GUI.BeginGroup(new Rect((width - boxWidth) / 2, top, boxWidth, boxHeight));
            DrawKnightPanel(new Rect(0, 0, boxWidth, boxHeight));
            CourtHeading(new Rect(20, 12, 424, 34), headings[helpStep], 23, TextAnchor.MiddleLeft);
            GUI.Label(new Rect(20, 56, 424, 160), text[helpStep], settingsBody);
            GUI.enabled = helpStep > 0;
            if (GUI.Button(new Rect(20, 226, 92, 32), "이전", button)) helpStep--;
            GUI.enabled = true;
            if (GUI.Button(new Rect(123, 226, 104, 32), "닫기", button)) systemMenu = SystemMenu.Settings;
            ArcadeText(new Rect(239, 226, 78, 32), (helpStep + 1) + " / 4", 14, HallAmber, TextAnchor.MiddleCenter);
            if (GUI.Button(new Rect(329, 226, 115, 32), helpStep == 3 ? "완료" : "다음", button))
            {
                if (helpStep == 3) systemMenu = SystemMenu.Settings;
                else helpStep++;
            }
            GUI.EndGroup();
        }
    }
}
