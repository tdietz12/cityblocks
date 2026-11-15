using UnityEngine;
using UnityEngine.SceneManagement;

namespace UI
{
    [DefaultExecutionOrder(32000)]
    public class SingleEventSystemGuard : MonoBehaviour
    {
        private static SingleEventSystemGuard _instance;
        private UnityEngine.EventSystems.EventSystem _primary;
        private UnityEngine.EventSystems.BaseInputModule _inputModule;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetInstance() { _instance = null; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            if (_instance != null) return;
            var host = new GameObject(nameof(SingleEventSystemGuard));
            DontDestroyOnLoad(host);
            host.AddComponent<SingleEventSystemGuard>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureSingleEventSystem();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) { EnsureSingleEventSystem(); }
        private void LateUpdate() { EnsureSingleEventSystem(); }

        // An EventSystem-only lookup is intentional: SDKs/UI can spawn duplicates after sceneLoaded.
        public void EnsureSingleEventSystem()
        {
            var systems = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
            if (_primary != null && _primary.transform.parent != null && !_primary.transform.parent.gameObject.activeInHierarchy)
            { _primary = null; _inputModule = null; }
            if (_primary == null)
            {
                var current = UnityEngine.EventSystems.EventSystem.current;
                if (current != null && current.gameObject.activeInHierarchy) _primary = current;
                foreach (var system in systems)
                    if (_primary == null && system.gameObject.activeInHierarchy) _primary = system;
                foreach (var system in systems)
                    if (_primary == null && (system.transform.parent == null || system.transform.parent.gameObject.activeInHierarchy)) _primary = system;
                if (_primary == null)
                {
                    var host = new GameObject("EventSystem");
                    _primary = host.AddComponent<UnityEngine.EventSystems.EventSystem>();
                }
                _inputModule = _primary.GetComponent<UnityEngine.EventSystems.BaseInputModule>();
            }
            if (!_primary.gameObject.activeSelf) _primary.gameObject.SetActive(true);
            if (!_primary.enabled) _primary.enabled = true;
            if (_inputModule == null)
                _inputModule = _primary.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (!_inputModule.enabled) _inputModule.enabled = true;
            if (Application.isPlaying && UnityEngine.EventSystems.EventSystem.current != _primary)
                UnityEngine.EventSystems.EventSystem.current = _primary;

            foreach (var duplicate in systems)
            {
                if (duplicate == null || duplicate == _primary) continue;
                // Inspect every component before deciding whether it is safe to remove the whole host.
                bool inputOnlyHost = duplicate.transform.childCount == 0;
                foreach (var component in duplicate.GetComponents<Component>())
                {
                    if (component == null) { inputOnlyHost = false; continue; }
                    if (!(component is Transform) && !(component is UnityEngine.EventSystems.EventSystem) &&
                        !(component is UnityEngine.EventSystems.BaseInputModule) && !(component is UnityEngine.EventSystems.BaseInput))
                        inputOnlyHost = false;
                }
                if (_primary.currentSelectedGameObject == null && duplicate.currentSelectedGameObject != null)
                    _primary.SetSelectedGameObject(duplicate.currentSelectedGameObject);
                duplicate.enabled = false;
                foreach (var module in duplicate.GetComponents<UnityEngine.EventSystems.BaseInputModule>())
                {
                    module.enabled = false;
                    if (!inputOnlyHost) RemoveObject(module);
                }
                if (inputOnlyHost) RemoveObject(duplicate.gameObject);
                else RemoveObject(duplicate); // Keep unrelated scripts, renderers, and children intact.
            }
        }

        private static void RemoveObject(Object target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_instance == this) _instance = null;
        }
    }
}
