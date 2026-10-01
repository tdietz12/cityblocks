using System;
using UnityEngine;
using UnityEngine.Advertisements;

namespace Gameplay
{
    /// <summary>Unity Ads interstitial integration. Dashboard identifiers are read from Resources/UnityAdsSettings.</summary>
    public sealed class UnityAdsProvider : MonoBehaviour,
        IUnityAdsInitializationListener, IUnityAdsLoadListener, IUnityAdsShowListener
    {
        private static UnityAdsProvider instance;
        private UnityAdsSettings settings;
        private string gameId;
        private string interstitialAdUnitId;
        private int requestedLevel;
        private bool initialized;
        private bool adLoaded;
        private bool loading;
        private bool showWhenLoaded;
        private Action afterAdAction;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void CreateProvider()
        {
            if (instance != null) return;
            GameObject providerObject = new GameObject("Unity Ads Provider");
            DontDestroyOnLoad(providerObject);
            instance = providerObject.AddComponent<UnityAdsProvider>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            PowerUpStore.InterstitialRequested += ShowInterstitial;
            settings = Resources.Load<UnityAdsSettings>("UnityAdsSettings");
            if (settings == null)
            {
                Debug.LogWarning("Unity Ads is disabled: create Assets/Resources/UnityAdsSettings.asset.");
                return;
            }

            SelectPlatformSettings();
            if (string.IsNullOrWhiteSpace(gameId) || string.IsNullOrWhiteSpace(interstitialAdUnitId))
            {
                Debug.Log("Unity Ads is waiting for the platform Game ID and interstitial Ad Unit ID in UnityAdsSettings.");
                return;
            }

            Advertisement.Initialize(gameId, settings.testMode, this);
        }

        private void OnDestroy()
        {
            if (instance != this) return;
            PowerUpStore.InterstitialRequested -= ShowInterstitial;
            instance = null;
        }

        private void SelectPlatformSettings()
        {
            switch (Application.platform)
            {
                case RuntimePlatform.IPhonePlayer:
                    gameId = settings.iOSGameId;
                    interstitialAdUnitId = settings.iOSInterstitialAdUnitId;
                    break;
                case RuntimePlatform.Android:
                    gameId = settings.androidGameId;
                    interstitialAdUnitId = settings.androidInterstitialAdUnitId;
                    break;
            #if UNITY_EDITOR
                case RuntimePlatform.OSXEditor:
                case RuntimePlatform.WindowsEditor:
                case RuntimePlatform.LinuxEditor:
                    gameId = settings.androidGameId;
                    interstitialAdUnitId = settings.androidInterstitialAdUnitId;
                    break;
            #endif
                default:
                    gameId = string.Empty;
                    interstitialAdUnitId = string.Empty;
                    break;
            }
        }

        private void ShowInterstitial(int levelNumber)
        {
            requestedLevel = levelNumber;
            if (!initialized || string.IsNullOrWhiteSpace(interstitialAdUnitId)) return;
            if (adLoaded)
            {
                adLoaded = false;
                Advertisement.Show(interstitialAdUnitId, this);
            }
            else
            {
                showWhenLoaded = true;
                LoadInterstitial();
            }
        }

        private void LoadInterstitial()
        {
            if (!initialized || loading || adLoaded || string.IsNullOrWhiteSpace(interstitialAdUnitId)) return;
            loading = true;
            Advertisement.Load(interstitialAdUnitId, this);
        }

        public void OnInitializationComplete()
        {
            initialized = true;
            LoadInterstitial();
        }

        public void OnInitializationFailed(UnityAdsInitializationError error, string message)
        {
            Debug.LogError("Unity Ads initialization failed: " + error + " - " + message);
        }

        public void OnUnityAdsAdLoaded(string adUnitId)
        {
            loading = false;
            if (adUnitId != interstitialAdUnitId) return;
            adLoaded = true;
            if (showWhenLoaded)
            {
                showWhenLoaded = false;
                adLoaded = false;
                Advertisement.Show(interstitialAdUnitId, this);
            }
        }

        public void OnUnityAdsFailedToLoad(string adUnitId, UnityAdsLoadError error, string message)
        {
            loading = false;
            adLoaded = false;
            LevelAnalytics.AdLoadFailed("interstitial", error + ":" + message);
            Debug.LogWarning("Unity Ads failed to load " + adUnitId + ": " + error + " - " + message);
            CompleteDeferredAction();
        }

        public void OnUnityAdsShowFailure(string adUnitId, UnityAdsShowError error, string message)
        {
            adLoaded = false;
            LevelAnalytics.AdLoadFailed("interstitial", error + ":" + message);
            Debug.LogWarning("Unity Ads failed to show " + adUnitId + ": " + error + " - " + message);
            CompleteDeferredAction();
            LoadInterstitial();
        }

        public void OnUnityAdsShowStart(string adUnitId)
        {
            bool endless = LevelFlow.ActiveSession == null;
            LevelAnalytics.AdImpression("unity_ads", "interstitial", requestedLevel, endless);
        }

        public void OnUnityAdsShowClick(string adUnitId) { }

        public void OnUnityAdsShowComplete(string adUnitId, UnityAdsShowCompletionState showCompletionState)
        {
            CompleteDeferredAction();
            LoadInterstitial();
        }

        public static void ShowBeforeReturningToMenu(int levelNumber, Action continuation)
        {
            if (PowerUpStore.AdsRemoved || instance == null || !instance.initialized ||
                string.IsNullOrWhiteSpace(instance.interstitialAdUnitId) || !instance.adLoaded)
            {
                continuation?.Invoke();
                return;
            }

            instance.requestedLevel = levelNumber;
            instance.afterAdAction = continuation;
            if (instance.adLoaded)
            {
                instance.adLoaded = false;
                try
                {
                    Advertisement.Show(instance.interstitialAdUnitId, instance);
                }
                catch (Exception exception)
                {
                    Debug.LogWarning("Unity Ads could not show the interstitial: " + exception.Message);
                    instance.CompleteDeferredAction();
                    instance.LoadInterstitial();
                }
            }
        }

        private void CompleteDeferredAction()
        {
            Action continuation = afterAdAction;
            afterAdAction = null;
            showWhenLoaded = false;
            continuation?.Invoke();
        }
    }
}
