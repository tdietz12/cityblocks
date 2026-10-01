using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Purchasing;

namespace Gameplay
{
    /// <summary>
    /// Runtime bridge to Apple's App Store and Google Play through Unity IAP 5.
    /// StoreOfferDefinition assets are loaded from Resources/StoreOffers.
    /// </summary>
    public sealed class UnityIapProvider : MonoBehaviour
    {
        private const string OffersResourcesPath = "StoreOffers";
        private static UnityIapProvider instance;

        private readonly Dictionary<string, StoreOfferDefinition> offersById = new Dictionary<string, StoreOfferDefinition>();
        private StoreController controller;
        private bool connected;

        public static UnityIapProvider Instance => instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateProvider()
        {
            if (instance != null) return;
            GameObject providerObject = new GameObject("Unity IAP Provider");
            DontDestroyOnLoad(providerObject);
            instance = providerObject.AddComponent<UnityIapProvider>();
        }

        private async void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            PowerUpStore.PurchaseRequested += Purchase;
            LoadCatalog();
            if (offersById.Count == 0)
            {
                Debug.LogWarning("Unity IAP has no products. Add StoreOfferDefinition assets under Assets/Resources/StoreOffers.");
                return;
            }

            await InitializeStore();
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            PowerUpStore.PurchaseRequested -= Purchase;
            instance = null;
        }

        private void LoadCatalog()
        {
            StoreOfferDefinition[] offers = Resources.LoadAll<StoreOfferDefinition>(OffersResourcesPath);
            CatalogProvider catalog = new CatalogProvider();
            foreach (StoreOfferDefinition offer in offers)
            {
                if (offer == null || string.IsNullOrWhiteSpace(offer.productId))
                {
                    Debug.LogWarning("Skipping a store offer without a product ID.", offer);
                    continue;
                }

                if (offersById.ContainsKey(offer.productId))
                {
                    Debug.LogError("Duplicate Unity IAP product ID: " + offer.productId, offer);
                    continue;
                }

                offersById.Add(offer.productId, offer);
                ProductType type = (offer.noAdsOffer || offer.nonConsumable) && offer.extraMovesOnPurchase == 0
                    ? ProductType.NonConsumable : ProductType.Consumable;
                if (!string.IsNullOrWhiteSpace(offer.appleProductId) || !string.IsNullOrWhiteSpace(offer.googleProductId))
                {
                    StoreSpecificIds storeIds = new StoreSpecificIds();
                    if (!string.IsNullOrWhiteSpace(offer.appleProductId)) storeIds.Add(offer.appleProductId, AppleAppStore.Name);
                    if (!string.IsNullOrWhiteSpace(offer.googleProductId)) storeIds.Add(offer.googleProductId, GooglePlay.Name);
                    catalog.AddProduct(offer.productId, type, storeIds);
                }
                else catalog.AddProduct(offer.productId, type);
            }

            pendingCatalog = catalog;
        }

        private CatalogProvider pendingCatalog;

        private async Task InitializeStore()
        {
            controller = UnityIAPServices.StoreController();
            controller.OnStoreDisconnected += failure => Debug.LogError("Unity IAP store connection failed: " + failure);
            controller.OnProductsFetched += products =>
            {
                connected = true;
                controller.FetchPurchases();
            };
            controller.OnProductsFetchFailed += failure => Debug.LogError("Unity IAP product fetch failed: " + failure);
            controller.OnPurchasesFetched += RestoreEntitlements;
            controller.OnPurchasesFetchFailed += failure => Debug.LogWarning("Unity IAP could not fetch existing purchases: " + failure);
            controller.OnPurchasePending += ProcessPendingPurchase;
            controller.OnPurchaseFailed += ProcessFailedPurchase;

            try
            {
                await controller.Connect();
                pendingCatalog.FetchProducts(products => controller.FetchProducts(products));
            }
            catch (Exception exception)
            {
                Debug.LogError("Unity IAP initialization failed: " + exception);
            }
        }

        public void Purchase(string productId)
        {
            if (controller == null || !connected)
            {
                Debug.LogWarning("Purchase requested before Unity IAP is ready: " + productId);
                return;
            }
            if (!offersById.ContainsKey(productId))
            {
                Debug.LogError("Purchase requested for an unknown product ID: " + productId);
                return;
            }
            controller.PurchaseProduct(productId);
        }

        private void ProcessPendingPurchase(PendingOrder order)
        {
            if (order == null || string.IsNullOrWhiteSpace(order.Info.TransactionID))
            {
                Debug.LogError("Unity IAP returned a pending purchase without a transaction ID; leaving it unconfirmed for retry.");
                return;
            }

            foreach (CartItem cartItem in order.CartOrdered.Items())
            {
                Product product = cartItem.Product;
                if (!offersById.TryGetValue(product.definition.id, out StoreOfferDefinition offer))
                {
                    Debug.LogError("Unity IAP returned an unconfigured product; leaving the order pending.");
                    return;
                }

                double value = product.metadata.localizedPrice > 0 ? (double)product.metadata.localizedPrice : offer.priceValue;
                string currency = string.IsNullOrWhiteSpace(product.metadata.isoCurrencyCode) ? offer.currencyCode : product.metadata.isoCurrencyCode;
                if (!PowerUpStore.FulfillPurchase(offer, order.Info.TransactionID, value, currency)) return;
            }

            controller.ConfirmPurchase(order);
        }

        private void ProcessFailedPurchase(FailedOrder order)
        {
            if (order == null) return;
            foreach (CartItem cartItem in order.CartOrdered.Items())
            {
                LevelAnalytics.PurchaseFailed(cartItem.Product.definition.id);
            }
            Debug.LogWarning("Unity IAP purchase failed: " + order.FailureReason + " - " + order.Details);
        }

        private void RestoreEntitlements(Orders orders)
        {
            if (orders == null) return;
            foreach (ConfirmedOrder order in orders.ConfirmedOrders)
            {
                foreach (CartItem cartItem in order.CartOrdered.Items())
                {
                    Product product = cartItem.Product;
                    if (!offersById.TryGetValue(product.definition.id, out StoreOfferDefinition offer) ||
                        (!offer.nonConsumable && !offer.noAdsOffer))
                    {
                        continue;
                    }

                    if (offer.noAdsOffer) PowerUpStore.RestoreNoAdsEntitlement();
                }
            }
        }
    }
}
