using Gameplay;
using UnityEngine;

namespace UI
{
    public class ContinueButton_LevelUpPanel : MonoBehaviour
    {
        public GameController game;

        public void ButtonPress()
        {
            game.state = GameController.GameState.play;
        }
    }
}
