using UnityEngine;

namespace Gameplay
{
    [CreateAssetMenu(menuName = "CityBlocks/Store Offer", fileName = "StoreOffer")]
    public class StoreOfferDefinition : ScriptableObject
    {
        public string productId;
        [Tooltip("Optional store-specific product IDs. Leave blank to use Product ID on both stores.")]
        public string appleProductId;
        public string googleProductId;
        public string displayName;
        [TextArea] public string description;
        [Min(0)] public int priceInPowerUpUses;
        public PowerUpInventory includedUses = new PowerUpInventory();
        public bool dailySpecial;
        public bool noAdsOffer;
        [Tooltip("No-ads entitlements are non-consumable. Bundles are consumable.")]
        public bool nonConsumable;
        [Min(0)] public int extraMovesOnPurchase;
        [Min(0)] public float priceValue;
        public string currencyCode = "USD";
    }
}
