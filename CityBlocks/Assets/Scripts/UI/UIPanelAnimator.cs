using System.Collections.Generic;
using UnityEngine;

namespace UI
{
    /// <summary>
    /// Animates modal panels and HUD containers on enable with a consistent elastic card pop-in,
    /// CanvasGroup alpha fade, optional defeat shake, and staggered child button entrance.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIPanelAnimator : MonoBehaviour
    {
        [Header("Target References")]
        [SerializeField] private CanvasGroup _canvasGroup;
        [SerializeField] private RectTransform _cardTransform;
        [SerializeField] private List<UIButtonAnimator> _staggeredButtons = new List<UIButtonAnimator>();

        [Header("Entrance & Exit Motion")]
        [SerializeField, Range(0.5f, 1.0f)] private float _startScale = 0.82f;
        [SerializeField, Range(1.0f, 1.2f)] private float _overshootScale = 1.04f;
        [SerializeField, Min(0.05f)] private float _entranceDuration = 0.22f;
        [SerializeField, Min(0.05f)] private float _exitDuration = 0.16f;
        [SerializeField, Min(0.02f)] private float _fadeDuration = 0.16f;
        [SerializeField, Min(0f)] private float _buttonStaggerDelay = 0.04f;

        [Header("Defeat Shake / Banner Pulse")]
        [SerializeField] private bool _shakeOnEnable;
        [SerializeField, Min(0f)] private float _shakeAmplitude = 8f;
        [SerializeField] private bool _gentleBannerPulse;

        private Vector2 _cardBasePosition;
        private bool _hasCardBasePosition;
        private float _elapsed;
        private bool _animating;
        private bool _closing;

        public bool IsAnimating => _animating;
        public bool IsClosing => _animating && _closing;
        public RectTransform CardTransform => _cardTransform;
        public CanvasGroup PanelCanvasGroup => _canvasGroup;
        public float EntranceDuration => _entranceDuration;
        public float ExitDuration => _exitDuration;
        public bool ShakeOnEnable => _shakeOnEnable;

        public static void SetAnimatedActive(GameObject panel, bool active)
        {
            if (panel == null) return;
            UIPanelAnimator anim = panel.GetComponent<UIPanelAnimator>();
            if (active)
            {
                if (!panel.activeSelf)
                {
                    panel.SetActive(true);
                }
                else if (anim != null && anim.IsClosing)
                {
                    anim.PlayEntrance();
                }
            }
            else
            {
                if (!panel.activeSelf) return;
                if (anim != null && Application.isPlaying)
                {
                    anim.PlayExit();
                }
                else
                {
                    panel.SetActive(false);
                }
            }
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            PlayEntrance();
        }

        private void OnDisable()
        {
            SnapToEnd();
        }

        public void CacheReferences()
        {
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_cardTransform == null)
            {
                Transform panelChild = transform.Find("Panel") ?? transform.Find("Card");
                _cardTransform = (panelChild != null ? panelChild : transform) as RectTransform;
            }
            if (_cardTransform != null && !_hasCardBasePosition)
            {
                _cardBasePosition = _cardTransform.anchoredPosition;
                _hasCardBasePosition = true;
            }
            if (_staggeredButtons == null) _staggeredButtons = new List<UIButtonAnimator>();
            if (_staggeredButtons.Count == 0)
            {
                GetComponentsInChildren(true, _staggeredButtons);
            }
        }

        public void PlayEntrance()
        {
            CacheReferences();
            _elapsed = 0f;
            _closing = false;
            _animating = true;
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }
            Evaluate(0f);
        }

        public void PlayExit()
        {
            CacheReferences();
            if (!gameObject.activeSelf) return;
            _elapsed = 0f;
            _closing = true;
            _animating = true;
            if (_canvasGroup != null)
            {
                _canvasGroup.blocksRaycasts = false;
                _canvasGroup.interactable = false;
            }
            EvaluateExit(0f);
        }

        private void Update()
        {
            if (_animating || _gentleBannerPulse)
            {
                Advance(Time.unscaledDeltaTime);
            }
        }

        public void Advance(float delta)
        {
            CacheReferences();
            delta = Mathf.Max(0f, delta);
            _elapsed += delta;

            if (_closing)
            {
                if (_elapsed >= _exitDuration)
                {
                    _animating = false;
                    _closing = false;
                    gameObject.SetActive(false);
                    return;
                }
                EvaluateExit(_elapsed);
                return;
            }

            float totalTime = _entranceDuration + (_staggeredButtons != null ? _staggeredButtons.Count * _buttonStaggerDelay : 0f);
            if (_animating && _elapsed >= totalTime)
            {
                SnapToEnd();
                if (!_gentleBannerPulse) return;
            }

            Evaluate(_elapsed);
        }

        private void EvaluateExit(float time)
        {
            float t = _exitDuration <= 0f ? 1f : Mathf.Clamp01(time / _exitDuration);
            float eased = Smooth(t);
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f - eased;
            }
            float scale = Mathf.Lerp(1f, _startScale, eased);
            if (_cardTransform != null)
            {
                _cardTransform.localScale = Vector3.one * scale;
                if (_hasCardBasePosition)
                {
                    _cardTransform.anchoredPosition = _cardBasePosition;
                }
            }
            if (_staggeredButtons != null)
            {
                for (int i = 0; i < _staggeredButtons.Count; i++)
                {
                    if (_staggeredButtons[i] != null && (_cardTransform == null || _staggeredButtons[i].transform != _cardTransform))
                    {
                        _staggeredButtons[i].SetExternalScaleMultiplier(scale);
                    }
                }
            }
        }

        private void Evaluate(float time)
        {
            if (_canvasGroup != null)
            {
                float fadeT = _fadeDuration <= 0f ? 1f : Mathf.Clamp01(time / _fadeDuration);
                _canvasGroup.alpha = Smooth(fadeT);
            }

            if (_cardTransform != null)
            {
                float popT = _entranceDuration <= 0f ? 1f : Mathf.Clamp01(time / _entranceDuration);
                float scale = EvaluatePopCurve(popT, _startScale, _overshootScale);
                if (!IsAnimating && _gentleBannerPulse)
                {
                    scale = 1f + 0.025f * Mathf.Sin(time * 5.5f);
                }
                _cardTransform.localScale = Vector3.one * scale;

                if (_hasCardBasePosition)
                {
                    float shakeOffset = 0f;
                    if (_shakeOnEnable && time < _entranceDuration)
                    {
                        float decay = 1f - Mathf.Clamp01(time / _entranceDuration);
                        shakeOffset = Mathf.Sin(time * 48f) * _shakeAmplitude * decay;
                    }
                    _cardTransform.anchoredPosition = _cardBasePosition + new Vector2(shakeOffset, 0f);
                }
            }

            if (_staggeredButtons != null)
            {
                for (int i = 0; i < _staggeredButtons.Count; i++)
                {
                    UIButtonAnimator btn = _staggeredButtons[i];
                    if (btn == null || (_cardTransform != null && btn.transform == _cardTransform)) continue;
                    float localTime = time - i * _buttonStaggerDelay;
                    if (localTime <= 0f)
                    {
                        btn.SetExternalScaleMultiplier(_startScale);
                    }
                    else
                    {
                        float btnT = _entranceDuration <= 0f ? 1f : Mathf.Clamp01(localTime / _entranceDuration);
                        btn.SetExternalScaleMultiplier(EvaluatePopCurve(btnT, _startScale, _overshootScale));
                    }
                }
            }
        }

        public void SnapToEnd()
        {
            CacheReferences();
            _animating = false;
            _closing = false;
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = 1f;
                _canvasGroup.blocksRaycasts = true;
                _canvasGroup.interactable = true;
            }
            if (_cardTransform != null)
            {
                _cardTransform.localScale = Vector3.one;
                if (_hasCardBasePosition)
                {
                    _cardTransform.anchoredPosition = _cardBasePosition;
                }
            }
            if (_staggeredButtons != null)
            {
                for (int i = 0; i < _staggeredButtons.Count; i++)
                {
                    if (_staggeredButtons[i] != null)
                    {
                        _staggeredButtons[i].SetExternalScaleMultiplier(1f);
                    }
                }
            }
        }

        private static float EvaluatePopCurve(float t, float startScale, float overshootScale)
        {
            t = Mathf.Clamp01(t);
            if (t < 0.65f)
            {
                return Mathf.Lerp(startScale, overshootScale, Smooth(t / 0.65f));
            }
            return Mathf.Lerp(overshootScale, 1f, Smooth((t - 0.65f) / 0.35f));
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
