using UnityEngine;

namespace Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Camera))]
    public class BoardCameraFeedback : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Camera _overlayCamera;
        [SerializeField] private GameController _game;
        [Header("Merge punch")]
        [SerializeField, Range(0f, 0.1f)] private float _magnification = 0.03f;
        [SerializeField, Min(0f)] private float _punchInDuration = 0f;
        [SerializeField, Min(0f)] private float _returnDuration = 0.2f;
        [Header("Stabilized handheld")]
        [SerializeField, Min(0f)] private float _translation = 0.03f;
        [SerializeField, Min(0f)] private float _rotationDegrees = 0.4f;
        [SerializeField, Min(0f)] private float _driftFrequency = 0.35f;

        private GridController _grid;
        private Vector3 _defaultPosition;
        private Quaternion _defaultRotation;
        private float _defaultFov;
        private float _defaultSize;
        private float _overlayFov;
        private float _overlaySize;
        private float _pulse;
        private float _pulseFrom;
        private float _phaseTime;
        private float _driftTime;
        private float _driftWeight;
        private bool _punching;
        private bool _returning;
        private bool _captured;

        private void Awake()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            CaptureDefaultState();
        }

        private void OnEnable()
        {
            if (!_captured) CaptureDefaultState();
            _grid = _game != null ? _game.grid : null;
            if (_grid != null)
            {
                _grid.onMergeStarted += PunchMerge;
                _grid.onBoardCleared += ResetFeedback;
            }
        }

        public void CaptureDefaultState()
        {
            if (_camera == null) _camera = GetComponent<Camera>();
            _defaultPosition = transform.localPosition;
            _defaultRotation = transform.localRotation;
            _defaultFov = _camera.fieldOfView;
            _defaultSize = _camera.orthographicSize;
            if (_overlayCamera != null)
            {
                _overlayFov = _overlayCamera.fieldOfView;
                _overlaySize = _overlayCamera.orthographicSize;
            }
            _captured = true;
            _pulse = _pulseFrom = _phaseTime = _driftWeight = 0f;
            _punching = _returning = false;
        }

        private int _pulseStartFrame = -1;

        public void PunchMerge()
        {
            if (!isActiveAndEnabled || !IsGameplayActive()) return;
            _pulse = 1f;
            _pulseFrom = 1f;
            _phaseTime = 0f;
            _pulseStartFrame = Time.frameCount;
            _punching = false;
            _returning = true;
            ApplyLens();
        }

        private bool IsGameplayActive()
        {
            return _game != null && (_game.state == GameController.GameState.play ||
                _game.state == GameController.GameState.powerUp);
        }

        private void LateUpdate()
        {
            if (!_captured) return;
            if (!IsGameplayActive()) { ResetFeedback(); return; }
            float delta = Time.deltaTime;
            _driftTime += delta;
            _phaseTime += Time.frameCount == _pulseStartFrame ? 0f : delta;
            if (_punching)
            {
                float t = _punchInDuration <= 0f ? 1f : Mathf.Clamp01(_phaseTime / _punchInDuration);
                _pulse = Mathf.Lerp(_pulseFrom, 1f, Smooth(t));
                if (t >= 1f) { _punching = false; _returning = true; _phaseTime = 0f; }
            }
            else if (_returning)
            {
                float t = _returnDuration <= 0f ? 1f : Mathf.Clamp01(_phaseTime / _returnDuration);
                _pulse = 1f - Smooth(t);
                if (t >= 1f) { _returning = false; _pulse = 0f; }
            }
            _driftWeight = Mathf.MoveTowards(_driftWeight, 1f - _pulse * 0.7f, delta * 4f);
            float time = _driftTime * _driftFrequency;
            float x = (Mathf.PerlinNoise(17.2f, time) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(59.6f, time) - 0.5f) * 2f;
            transform.localPosition = _defaultPosition +
                _defaultRotation * new Vector3(x, y, 0f) * (_translation * _driftWeight);
            transform.localRotation = _defaultRotation *
                Quaternion.Euler(y * _rotationDegrees * _driftWeight, 0f, x * _rotationDegrees * _driftWeight);
            ApplyLens();
        }

        private void ApplyLens()
        {
            float magnification = 1f + _magnification * _pulse;
            _camera.fieldOfView = 2f * Mathf.Atan(Mathf.Tan(_defaultFov * Mathf.Deg2Rad * 0.5f) /
                magnification) * Mathf.Rad2Deg;
            _camera.orthographicSize = _defaultSize / magnification;
            if (_overlayCamera != null)
            {
                _overlayCamera.fieldOfView = _camera.fieldOfView;
                _overlayCamera.orthographicSize = _camera.orthographicSize;
            }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);

        public void ResetFeedback()
        {
            if (!_captured || _camera == null) return;
            _pulse = _pulseFrom = _phaseTime = _driftWeight = 0f;
            _punching = _returning = false;
            transform.localPosition = _defaultPosition;
            transform.localRotation = _defaultRotation;
            _camera.fieldOfView = _defaultFov;
            _camera.orthographicSize = _defaultSize;
            if (_overlayCamera != null)
            {
                _overlayCamera.fieldOfView = _overlayFov;
                _overlayCamera.orthographicSize = _overlaySize;
            }
        }

        private void OnDisable()
        {
            if (_grid != null)
            {
                _grid.onMergeStarted -= PunchMerge;
                _grid.onBoardCleared -= ResetFeedback;
            }
            _grid = null;
            ResetFeedback();
        }
    }
}
