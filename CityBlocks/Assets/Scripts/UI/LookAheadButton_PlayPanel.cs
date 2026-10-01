using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    public class LookAheadButton_PlayPanel : MonoBehaviour
    {
        public GameController game;
        private Button button;
        private TMP_Text label;

        private void Awake()
        {
            button = GetComponent<Button>();
            label = GetComponentInChildren<TMP_Text>();
        }

        private void OnEnable() => Refresh();

        private void Refresh()
        {
            int uses = PowerUpStore.Uses(GameController.PowerUpType.lookAhead);
            if (button != null) button.interactable = uses > 0;
            if (label != null) label.text = "LOOK AHEAD (" + uses + ")";
        }

        public void ButtonPress()
        {
            if (game != null) game.TryBeginPowerUp(GameController.PowerUpType.lookAhead);
        }
    }
}
