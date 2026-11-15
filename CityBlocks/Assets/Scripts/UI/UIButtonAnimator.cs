using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UI
{
    /// <summary>
    /// Provides consistent tactile press squash, elastic release bounce, and optional primary CTA
    /// breathing pulse across all UI buttons.
    /// </summary>
    [DisallowMultipleComponent]
    public class UIButtonAnimator : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler, IPointerClickHandler
    {
        private enum MotionState
        {
            Idle,
            Pressing,
            Releasing
        }

        [Header("Squash & Bounce")]
        [SerializeField, Range(0.75f, 0.98f)] private float _pressScale = 0.92f;
        [SerializeField, Min(0.01f)] private float _pressDuration = 0.07f;
        [SerializeField, Range(1.0f, 1.25f)] private float _releaseOvershoot = 1.08f;
        [SerializeField, Min(0.02f)] private float _releaseDuration = 0.18f;

        [Header("Idle Attention Pulse")]
        [SerializeField] private bool _enableIdlePulse;
        [SerializeField, Range(1.0f, 1.12f)] private float _idlePulseScale = 1.035f;
        [SerializeField, Min(0.2f)] private float _idlePulseFrequency = 1.8f;
        [SerializeField] private RectTransform _arrowGraphic;
        [SerializeField, Min(0f)] private float _arrowNudgeDistance = 5f;

        private Button _button;
        private RectTransform _rect;
        private Vector3 _baseScale = Vector3.one;
        private Vector2 _arrowBasePosition;
        private bool _hasArrowBase;
        private MotionState _state = MotionState.Idle;
        private float _stateTime;
        private float _idleTime;
        private float _fromScale = 1f;
        private float _currentScaleMultiplier = 1f;
        private bool _pointerDown;
        private float _externalScaleMultiplier = 1f;

        public bool EnableIdlePulse
        {
            get => false;
            set => _enableIdlePulse = false;
        }

        public float PressScale => _pressScale;
        public float ReleaseOvershoot => _releaseOvershoot;
        public float CurrentScaleMultiplier => _currentScaleMultiplier;
        public bool IsPressed => _pointerDown;

        internal void SetExternalScaleMultiplier(float multiplier)
        {
            _externalScaleMultiplier = Mathf.Max(0f, multiplier);
            ApplyTransform();
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnEnable()
        {
            CacheReferences();
            ResetState();
        }

        private void OnDisable()
        {
            ResetState();
        }

        private void CacheReferences()
        {
            if (_rect == null) _rect = transform as RectTransform;
            if (_button == null) _button = GetComponent<Button>();
            if (_baseScale == Vector3.zero) _baseScale = Vector3.one;
            if (_arrowGraphic == null)
            {
                Transform arrow = transform.Find("Arrow");
                if (arrow != null) _arrowGraphic = arrow as RectTransform;
            }
            if (_arrowGraphic != null && !_hasArrowBase)
            {
                _arrowBasePosition = _arrowGraphic.anchoredPosition;
                _hasArrowBase = true;
            }
        }

        public void ResetState()
        {
            _pointerDown = false;
            _state = MotionState.Idle;
            _stateTime = 0f;
            _idleTime = 0f;
            _fromScale = 1f;
            _currentScaleMultiplier = 1f;
            _externalScaleMultiplier = 1f;
            ApplyTransform();
        }

        private bool IsInteractable()
        {
            return _button == null || _button.IsInteractable();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            TriggerPress();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_pointerDown)
            {
                TriggerRelease();
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_pointerDown)
            {
                TriggerRelease();
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!IsInteractable()) return;
            if (_state != MotionState.Releasing)
            {
                TriggerRelease();
            }
        }

        public void TriggerPress()
        {
            CacheReferences();
            if (!IsInteractable()) return;
            _pointerDown = true;
            _fromScale = _currentScaleMultiplier;
            _state = MotionState.Pressing;
            _stateTime = 0f;
        }

        public void TriggerRelease()
        {
            CacheReferences();
            _pointerDown = false;
            if (!IsInteractable())
            {
                ResetState();
                return;
            }
            _fromScale = _currentScaleMultiplier;
            _state = MotionState.Releasing;
            _stateTime = 0f;
        }

        private void Update()
        {
            if (_state != MotionState.Idle)
            {
                Advance(Time.unscaledDeltaTime);
            }
        }

        public void Advance(float delta)
        {
            CacheReferences();
            delta = Mathf.Max(0f, delta);

            if (!IsInteractable())
            {
                _pointerDown = false;
                _state = MotionState.Idle;
                _currentScaleMultiplier = 1f;
                ApplyTransform();
                return;
            }

            switch (_state)
            {
                case MotionState.Pressing:
                {
                    _stateTime += delta;
                    float t = _pressDuration <= 0f ? 1f : Mathf.Clamp01(_stateTime / _pressDuration);
                    _currentScaleMultiplier = Mathf.Lerp(_fromScale, _pressScale, Smooth(t));
                    break;
                }
                case MotionState.Releasing:
                {
                    _stateTime += delta;
                    float t = _releaseDuration <= 0f ? 1f : Mathf.Clamp01(_stateTime / _releaseDuration);
                    if (t < 0.45f)
                    {
                        _currentScaleMultiplier = Mathf.Lerp(_fromScale, _releaseOvershoot, Smooth(t / 0.45f));
                    }
                    else
                    {
                        _currentScaleMultiplier = Mathf.Lerp(_releaseOvershoot, 1f, Smooth((t - 0.45f) / 0.55f));
                    }
                    if (t >= 1f)
                    {
                        _currentScaleMultiplier = 1f;
                        _state = MotionState.Idle;
                        _idleTime = 0f;
                    }
                    break;
                }
                case MotionState.Idle:
                {
                    _currentScaleMultiplier = 1f;
                    break;
                }
            }

            ApplyTransform();
        }

        private void ApplyTransform()
        {
            if (_rect != null)
            {
                _rect.localScale = _baseScale * (_currentScaleMultiplier * _externalScaleMultiplier);
            }
            if (_arrowGraphic != null && _hasArrowBase)
            {
                _arrowGraphic.anchoredPosition = _arrowBasePosition;
            }
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }
    }
}
