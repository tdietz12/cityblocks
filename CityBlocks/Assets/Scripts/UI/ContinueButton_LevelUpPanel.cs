using Gameplay;
using UnityEngine;

namespace UI
{
    public class ContinueButton_LevelUpPanel : MonoBehaviour
    {
        public GameController game;

        public void ButtonPress()
        {
            LevelRulesUI rulesUI = LevelRulesUI.Instance ?? FindAnyObjectByType<LevelRulesUI>();
            if (rulesUI != null)
            {
                rulesUI.ContinueFromLevelUp();
            }
            else
            {
                if (game == null) game = GameController.instance ?? FindAnyObjectByType<GameController>();
                if (game != null) game.state = GameController.GameState.play;
            }
        }
    }
}
