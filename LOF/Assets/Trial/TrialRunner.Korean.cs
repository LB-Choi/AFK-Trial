namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        // Equipment names remain stable save keys and resource names; translate only their display.
        private static string GearNameKorean(string name)
        {
            switch (name)
            {
                case "Iron Sword": return "철검";
                case "Knight Blade": return "기사의 검";
                case "Leather Armor": return "가죽 갑옷";
                case "Steel Armor": return "강철 갑옷";
                case "Amber Necklace": return "호박 목걸이";
                case "Jade Necklace": return "비취 목걸이";
                default: return name;
            }
        }

        private static string RoleKorean(TrialData.Role role) => role == TrialData.Role.Attack ? "공격" :
            role == TrialData.Role.Defense ? "방어" : role == TrialData.Role.Support ? "지원" : "없음";
        private static string SynergyKorean(TrialData.Role role) => role == TrialData.Role.Attack ? "맹공" :
            role == TrialData.Role.Defense ? "수호" : role == TrialData.Role.Support ? "강화" : "없음";
        private string StageLabel(int stage) => stage == 0 ? "연습 전투" :
            ((stage - 1) / battle.Data.RoundLength + 1) + "회차 / " + ((stage - 1) % battle.Data.RoundLength + 1) + "스테이지";
    }
}
