using CloudServices;
using TMPro;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Shows the player's cloud coin balance (PlayerWallet) in a coin chip. Shows the cached value instantly and
    /// updates when fresh balances arrive; shows a dash until any value is known.
    /// </summary>
    public class CoinChipUI : MonoBehaviour
    {
        [Tooltip("Text that displays the balance.")]
        [SerializeField] private TMP_Text balanceLabel;
        [Tooltip("Shown before any balance is known (first launch while offline / before the first cloud call).")]
        [SerializeField] private string unknownText = "–";

        private void OnEnable()
        {
            PlayerWallet.Changed += Refresh;
            AccountService.KindChanged += OnAccountChanged;
            Refresh();
        }

        private void OnDisable()
        {
            PlayerWallet.Changed -= Refresh;
            AccountService.KindChanged -= OnAccountChanged;
        }

        private void OnAccountChanged(string kind) => Refresh();

        private void Refresh()
        {
            if (balanceLabel == null) return;
            balanceLabel.text = PlayerWallet.HasValue ? PlayerWallet.Coins.ToString("N0") : unknownText;
        }
    }
}
