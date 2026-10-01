using Gameplay;
using UnityEngine;

namespace UI
{
    public class BackButton : MonoBehaviour
    {
        public void ButtonPress()
        {
            LevelFlow.GoToMenu();
        }
    }
}
