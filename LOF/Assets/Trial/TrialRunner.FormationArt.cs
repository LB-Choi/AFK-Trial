using UnityEngine;

namespace AFKTrial
{
    public sealed partial class TrialRunner
    {
        private Texture2D cardFrame, cardSelection, slotFrame;
        private readonly Texture2D[] deploymentBadges = new Texture2D[3];
        private GUIStyle formationName;
        private static readonly Color selectedSlotColor = new Color(0.43f, 0.86f, 0.85f);

        private void LoadFormationArt()
        {
            cardFrame = LoadTexture("FormationUI/FormationCardFrame");
            cardSelection = LoadTexture("FormationUI/FormationSelectionOutline");
            slotFrame = LoadTexture("FormationUI/FormationSlotFrame");
            for (int i = 0; i < deploymentBadges.Length; i++)
                deploymentBadges[i] = LoadTexture("FormationUI/DeploymentBadge" + (i + 1));
        }
    }
}
