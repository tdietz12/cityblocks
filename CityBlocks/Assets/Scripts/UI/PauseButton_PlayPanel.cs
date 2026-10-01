using Gameplay;
using UnityEngine;

namespace UI
{
    public class PauseButton_PlayPanel : MonoBehaviour
    {
        public GameController game;
    
        public void ButtonPress()
        {
            game.state = GameController.GameState.start;
        }
    }
}