using Gameplay;
using UnityEngine;

namespace UI
{
    public class PlayButton_StartPanel : MonoBehaviour
    {
        public GameController game;

        public void ButtonPress()
        {
            game.state = GameController.GameState.play;
            LevelRulesUI rulesUI = FindAnyObjectByType<LevelRulesUI>();
            if (rulesUI != null) rulesUI.Refresh();
        }
    }
}
