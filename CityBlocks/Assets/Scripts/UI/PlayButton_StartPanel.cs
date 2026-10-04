using Gameplay;
using UnityEngine;

namespace UI
{
    public class PlayButton_StartPanel : MonoBehaviour
    {
        public GameController game;

        public void ButtonPress()
        {
            LevelRulesUI rulesUI = LevelRulesUI.Instance ?? FindAnyObjectByType<LevelRulesUI>();
            if (rulesUI != null)
            {
                rulesUI.StartGame();
            }
            else
            {
                if (game == null) game = GameController.instance ?? FindAnyObjectByType<GameController>();
                if (game != null) game.state = GameController.GameState.play;
            }
        }
    }
}
