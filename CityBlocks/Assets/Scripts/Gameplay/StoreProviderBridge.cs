using UnityEngine;
using UnityEngine.Events;

namespace Gameplay
{
    /// <summary>Connect this to a configured platform IAP/ad SDK. It never grants content on its own.</summary>
    public class StoreProviderBridge : MonoBehaviour
    {
        [SerializeField] private StoreOfferDefinition[] offers;
        [SerializeField] private UnityEvent<string> onPurchaseRequest = new UnityEvent<string>();
        [SerializeField] private UnityEvent<int> onInterstitialRequest = new UnityEvent<int>();

        private void OnEnable()
        {
            PowerUpStore.PurchaseRequested += HandlePurchaseRequest;
            PowerUpStore.InterstitialRequested += HandleInterstitialRequest;
        }

        private void OnDisable()
        {
            PowerUpStore.PurchaseRequested -= HandlePurchaseRequest;
            PowerUpStore.InterstitialRequested -= HandleInterstitialRequest;
        }

        private void HandlePurchaseRequest(string productId) => onPurchaseRequest.Invoke(productId);
        private void HandleInterstitialRequest(int levelNumber) => onInterstitialRequest.Invoke(levelNumber);

        public void ReportPurchase(string transactionId, double value, string currency, string commaSeparatedItems)
        {
            LevelProgressData progress = LevelProgressStore.Load();
            bool firstPurchase = !progress.hasMadePurchase;
            progress.hasMadePurchase = true;
            LevelProgressStore.Save(progress);
            LevelAnalytics.PurchaseCompleted(transactionId, value, currency, commaSeparatedItems,
                firstPurchase, progress.completedLevels != null ? progress.completedLevels.Count : 0);
        }

        /// <summary>Call from the IAP provider only after the platform confirms the transaction.</summary>
        public void ConfirmPurchase(string productId, string transactionId)
        {
            if (offers == null || string.IsNullOrWhiteSpace(transactionId)) return;
            foreach (StoreOfferDefinition offer in offers)
            {
                if (offer == null || offer.productId != productId) continue;
                PowerUpStore.FulfillPurchase(offer, transactionId, offer.priceValue, offer.currencyCode);
                return;
            }
        }

        public void ReportPurchaseFailed(string productId) => LevelAnalytics.PurchaseFailed(productId);
        public void ReportAdImpression(string platform, string format, int levelNumber, bool endless) =>
            LevelAnalytics.AdImpression(platform, format, levelNumber, endless);
        public void ReportAdLoadFailed(string format, string errorCode) =>
            LevelAnalytics.AdLoadFailed(format, errorCode);
    }
}
