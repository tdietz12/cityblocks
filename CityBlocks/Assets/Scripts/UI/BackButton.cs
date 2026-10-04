using Gameplay;
using UnityEngine;

namespace UI
{
    public class BackButton : MonoBehaviour
    {
        public void ButtonPress()
        {
            LevelRulesUI rulesUI = LevelRulesUI.Instance ?? FindAnyObjectByType<LevelRulesUI>();
            if (rulesUI != null)
            {
                rulesUI.ReturnToMenu();
            }
            else
            {
                LevelFlow.GoToMenu();
            }
        }
    }
}
