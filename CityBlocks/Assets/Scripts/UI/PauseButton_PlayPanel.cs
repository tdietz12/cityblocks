using Gameplay;
using UnityEngine;

namespace UI
{
    public class PauseButton_PlayPanel : MonoBehaviour
    {
        public GameController game;

        public void ButtonPress()
        {
            LevelRulesUI rulesUI = LevelRulesUI.Instance ?? FindAnyObjectByType<LevelRulesUI>();
            if (rulesUI != null)
            {
                rulesUI.PauseGame();
            }
            else
            {
                if (game == null) game = GameController.instance ?? FindAnyObjectByType<GameController>();
                if (game != null) game.state = GameController.GameState.start;
                if (!LevelFlow.IsLevelMode && Data_Persistence.DataPersistenceController.instance != null)
                {
                    Data_Persistence.DataPersistenceController.instance.SaveGame();
                }
            }
        }
    }
}