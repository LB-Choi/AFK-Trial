using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private readonly Texture2D[] deploymentBadges = new Texture2D[3];
        private GUIStyle formationName;
        private static readonly Color selectedSlotColor = new Color(0.43f, 0.86f, 0.85f);

        private static Rect FormationSlotRect(Rect area, int slot)
        {
            float gap = Mathf.Min(50f, area.width * .07f);
            float w = (area.width - gap) * .5f;
            float h = (area.height - 6f) * .5f;
            float x = slot == 1 ? area.xMax - w : area.x;
            float y = slot == 2 ? area.y : slot == 1 ? area.center.y - h * .5f : area.yMax - h;
            return new Rect(x, y, w, h);
        }

        private void LoadFormationArt()
        {
            // Slot order on the ground is lower rear, center front, upper rear.
            int[] badgeNumbers = { 3, 1, 2 };
            for (int i = 0; i < deploymentBadges.Length; i++)
                deploymentBadges[i] = LoadTexture("FormationUI/DeploymentBadge" + badgeNumbers[i]);
        }
    }
}
