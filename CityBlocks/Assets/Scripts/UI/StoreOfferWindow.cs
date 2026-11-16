using Gameplay;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UI
{
    /// <summary>Presenter for prefab-authored daily special and no-ads purchase windows.</summary>
    public class StoreOfferWindow : MonoBehaviour
    {
        [SerializeField] private StoreOfferDefinition dailySpecial;
        [SerializeField] private StoreOfferDefinition noAdsOffer;
        [SerializeField] private GameObject dailySpecialWindow;
        [SerializeField] private GameObject noAdsWindow;
        [SerializeField] private Button closeStoreButton;
        [SerializeField] private TMP_Text dailySpecialTitle;
        [SerializeField] private TMP_Text dailySpecialDescription;
        [SerializeField] private TMP_Text noAdsTitle;
        [SerializeField] private Button dailySpecialPurchaseButton;
        [SerializeField] private Button noAdsPurchaseButton;

        private void Awake()
        {
            if (dailySpecialPurchaseButton != null) dailySpecialPurchaseButton.onClick.AddListener(BuyDailySpecial);
            if (noAdsPurchaseButton != null) noAdsPurchaseButton.onClick.AddListener(BuyNoAds);
            if (closeStoreButton != null)
            {
                closeStoreButton.onClick.AddListener(CloseDailySpecial);
                TMP_Text closeLabel = closeStoreButton.GetComponentInChildren<TMP_Text>();
                if (closeLabel != null) closeLabel.text = "CLOSE";
                bool insidePanel = (dailySpecialWindow != null && closeStoreButton.transform.IsChildOf(dailySpecialWindow.transform)) ||
                                   (noAdsWindow != null && closeStoreButton.transform.IsChildOf(noAdsWindow.transform));
                closeStoreButton.gameObject.SetActive(insidePanel);
            }
            if (dailySpecialWindow != null) dailySpecialWindow.SetActive(false);
            if (noAdsWindow != null) noAdsWindow.SetActive(false);
        }

        public void OpenDailySpecial()
        {
            if (dailySpecial == null || !dailySpecial.dailySpecial) return;
            LevelAnalytics.StoreOpened("daily_special", CurrentLevelName());
            LevelAnalytics.ItemListViewed("daily_special");
            LevelAnalytics.ItemViewed(dailySpecial.productId, "daily_special");
            ShowCombinedOfferPanel();
        }

        public void OpenNoAdsOffer()
        {
            if (noAdsOffer == null || !noAdsOffer.noAdsOffer || PowerUpStore.AdsRemoved) return;
            LevelAnalytics.StoreOpened("no_ads_offer", CurrentLevelName());
            LevelAnalytics.ItemListViewed("no_ads");
            LevelAnalytics.ItemViewed(noAdsOffer.productId, "no_ads");
            if (dailySpecialWindow == noAdsWindow && dailySpecialWindow != null)
            {
                ShowCombinedOfferPanel();
                return;
            }
            if (noAdsTitle != null) noAdsTitle.text = noAdsOffer.displayName;
            if (closeStoreButton != null) closeStoreButton.gameObject.SetActive(true);
            UIPanelAnimator.SetAnimatedActive(noAdsWindow, true);
        }

        private void ShowCombinedOfferPanel()
        {
            if (dailySpecialTitle != null) dailySpecialTitle.text = "STORE";
            if (dailySpecialDescription != null)
            {
                string special = dailySpecial != null
                    ? "<color=#D4A424><b>" + dailySpecial.displayName.ToUpperInvariant() + "</b></color>\n" +
                      dailySpecial.description +
                      "\n<color=#B8C4D0>Includes: " + FormatUses(dailySpecial.includedUses) + "</color>" +
                      (dailySpecial.priceInPowerUpUses > 0 ? "\nPrice: " + dailySpecial.priceInPowerUpUses + " power-up uses" : string.Empty)
                    : string.Empty;
                string noAds = noAdsOffer != null && noAdsOffer.noAdsOffer
                    ? "\n\n<color=#D4A424><b>" + noAdsOffer.displayName.ToUpperInvariant() + "</b></color>\n" + noAdsOffer.description
                    : string.Empty;
                dailySpecialDescription.text = special + noAds;
            }
            if (dailySpecialPurchaseButton != null)
                dailySpecialPurchaseButton.interactable = dailySpecial != null && !string.IsNullOrWhiteSpace(dailySpecial.productId);
            if (noAdsPurchaseButton != null)
                noAdsPurchaseButton.interactable = noAdsOffer != null && !string.IsNullOrWhiteSpace(noAdsOffer.productId) && !PowerUpStore.AdsRemoved;
            if (closeStoreButton != null) closeStoreButton.gameObject.SetActive(true);
            UIPanelAnimator.SetAnimatedActive(dailySpecialWindow, true);
        }

        public void ToggleStore()
        {
            GameObject panel = dailySpecialWindow != null ? dailySpecialWindow : noAdsWindow;
            if (panel == null) return;
            UIPanelAnimator anim = panel.GetComponent<UIPanelAnimator>();
            bool isOpen = panel.activeSelf && (anim == null || !anim.IsClosing);
            if (isOpen) CloseDailySpecial();
            else OpenDailySpecial();
        }

        public void CloseDailySpecial()
        {
            bool closeInsidePanel = closeStoreButton != null &&
                                    ((dailySpecialWindow != null && closeStoreButton.transform.IsChildOf(dailySpecialWindow.transform)) ||
                                     (noAdsWindow != null && closeStoreButton.transform.IsChildOf(noAdsWindow.transform)));
            UIPanelAnimator.SetAnimatedActive(dailySpecialWindow, false);
            if (noAdsWindow != null && noAdsWindow != dailySpecialWindow) UIPanelAnimator.SetAnimatedActive(noAdsWindow, false);
            if (closeStoreButton != null && !closeInsidePanel) closeStoreButton.gameObject.SetActive(false);
        }

        public void CloseNoAdsOffer()
        {
            bool closeInsidePanel = closeStoreButton != null &&
                                    ((dailySpecialWindow != null && closeStoreButton.transform.IsChildOf(dailySpecialWindow.transform)) ||
                                     (noAdsWindow != null && closeStoreButton.transform.IsChildOf(noAdsWindow.transform)));
            UIPanelAnimator.SetAnimatedActive(noAdsWindow, false);
            if (dailySpecialWindow != null && dailySpecialWindow != noAdsWindow) UIPanelAnimator.SetAnimatedActive(dailySpecialWindow, false);
            if (closeStoreButton != null && !closeInsidePanel) closeStoreButton.gameObject.SetActive(false);
        }

        private void BuyDailySpecial()
        {
            if (dailySpecial != null)
            {
                LevelAnalytics.CheckoutStarted(dailySpecial.productId, dailySpecial.priceValue, dailySpecial.currencyCode);
                PowerUpStore.RequestPurchase(dailySpecial.productId);
            }
        }

        private void BuyNoAds()
        {
            if (noAdsOffer != null)
            {
                LevelAnalytics.CheckoutStarted(noAdsOffer.productId, noAdsOffer.priceValue, noAdsOffer.currencyCode);
                PowerUpStore.RequestPurchase(noAdsOffer.productId);
            }
        }

        private static string FormatUses(PowerUpInventory uses)
        {
            if (uses == null) return "None";
            return "Delete " + uses.deleteUses + "  |  Delete row " + uses.deleteRowUses +
                   "  |  Level up " + uses.levelUpUses + "  |  Extra turns " + uses.extraTurnsUses;
        }

        private static string CurrentLevelName()
        {
            LevelSession session = LevelFlow.ActiveSession;
            return session == null ? string.Empty : "Level " + session.Definition.levelNumber;
        }
    }
}
