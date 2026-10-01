using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>Connects the prefab-authored main-menu Store button to the store panel.</summary>
    public sealed class StoreMenuButton : MonoBehaviour
    {
        private void Awake()
        {
            Button button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OpenStore);
        }

        private static void OpenStore()
        {
            StoreOfferWindow window = FindAnyObjectByType<StoreOfferWindow>();
            if (window != null) window.ToggleStore();
            else Debug.LogWarning("No StoreOfferWindow prefab instance is present in the active scene.");
        }
    }
}
