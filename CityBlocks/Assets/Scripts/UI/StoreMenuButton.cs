using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>Connects the prefab-authored main-menu Store button to the store panel.</summary>
    public sealed class StoreMenuButton : MonoBehaviour
    {
        private Button button;
        private void Awake()
        {
            button = GetComponent<Button>();
            if (button != null) button.onClick.AddListener(OpenStore);
        }

        private void Update()
        {
            if (button != null) button.interactable = Gameplay.PowerUpStore.CanAccessStore;
        }

        private static void OpenStore()
        {
            if (!Gameplay.PowerUpStore.CanAccessStore) return;
            StoreOfferWindow window = FindAnyObjectByType<StoreOfferWindow>();
            if (window != null) window.ToggleStore();
            else Debug.LogWarning("No StoreOfferWindow prefab instance is present in the active scene.");
        }
    }
}
