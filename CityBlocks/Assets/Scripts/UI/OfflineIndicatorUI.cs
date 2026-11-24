using CloudServices;
using UnityEngine;

namespace UI
{
    /// <summary>Shows the offline badge while ConnectivityService reports offline; hidden while online.</summary>
    public class OfflineIndicatorUI : MonoBehaviour
    {
        [Tooltip("The badge object to show while offline (a child, so this component keeps listening while it's hidden).")]
        [SerializeField] private GameObject badge;

        private void OnEnable()
        {
            ConnectivityService.OnlineChanged += OnOnlineChanged;
            Apply(ConnectivityService.IsOnline);
        }

        private void OnDisable() => ConnectivityService.OnlineChanged -= OnOnlineChanged;

        private void OnOnlineChanged(bool online) => Apply(online);

        private void Apply(bool online)
        {
            if (badge != null && badge.activeSelf == online) badge.SetActive(!online);
        }
    }
}
