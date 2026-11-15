using System.Collections.Generic;
using Gameplay;
using UnityEngine;

namespace UI
{
    public class ScoreFeedbackController : MonoBehaviour
    {
        [Header("Presentation")]
        [SerializeField] private RectTransform _popupLayer;
        [SerializeField] private RectTransform _scoreTarget;
        [SerializeField] private RectTransform _pulseTarget;
        [SerializeField] private MergeScorePopup _popupPrefab;
        [SerializeField] private Camera _gameplayCamera;
        [SerializeField] private MergeMultiplierCounter _multiplierCounter;
        [SerializeField] private MergeMultiplierToken _multiplierTokenPrefab;
        [SerializeField] private ComboCelebrationController _comboCelebration;
        [SerializeField, Min(0f)] private float _multiplierFlightDuration = 0.35f;
        private readonly List<MergeMultiplierToken> _tokens = new List<MergeMultiplierToken>();
        private readonly Stack<MergeMultiplierToken> _tokenPool = new Stack<MergeMultiplierToken>();
        [Header("Timing")]
        [SerializeField, Min(0f)] private float _riseDuration = 0.3f;
        [SerializeField, Min(0f)] private float _multipliedRiseDuration = 0.6f;
        [SerializeField, Min(0f)] private float _flightDuration = 0.15f;
        [SerializeField, Min(0f)] private float _riseDistance = 45f;
        [SerializeField, Min(0f)] private float _worldHeightOffset = 0.7f;
        [SerializeField, Min(0f)] private float _pulseDuration = 0.15f;
        [SerializeField, Range(1f, 3f)] private float _pulseScale = 2.0f;
        private readonly List<MergeScorePopup> _active = new List<MergeScorePopup>();
        private readonly Stack<MergeScorePopup> _pool = new Stack<MergeScorePopup>();
        private GameController _game;
        private GridController _grid;
        private Canvas _canvas;
        private int _pendingPoints;
        private int _recordBaseline;
        private Vector3 _baseScale = Vector3.one;
        private float _pulseAge;
        private float _pulseFrom = 1f;
        private bool _pulsing;
        public event System.Action onDisplayedScoreChanged;
        public int DisplayedScore => _game == null ? 0 : Mathf.Max(0, _game.score - _pendingPoints);
        public int DisplayedRecordScore => Mathf.Max(_recordBaseline, HasPendingFeedback || _game == null ? DisplayedScore : _game.recordScore);
        public bool HasPendingComboCelebration => _comboCelebration != null && _comboCelebration.IsCelebrating;
        public ComboCelebrationController ComboCelebration => _comboCelebration;
        public bool HasPendingFeedback => _active.Count > 0 || _tokens.Count > 0 || _pulsing ||
            (_multiplierCounter != null && _multiplierCounter.HasPendingPulse) || HasPendingComboCelebration;
        internal float CounterPulseDuration => _pulseDuration;
        internal float CounterPulseScale => _pulseScale;
        internal static float EvaluateCounterPulse(float t, float from, float peak)
        {
            return t < 0.35f ? Mathf.Lerp(from, peak, Smooth(t / 0.35f))
                : Mathf.Lerp(peak, 1f, Smooth((t - 0.35f) / 0.65f));
        }

        public void CaptureRecordBaseline()
        {
            if (!HasPendingFeedback && _game != null) _recordBaseline = _game.recordScore;
        }

        public void Bind(GameController game, Camera gameplayCamera)
        {
            if (_comboCelebration == null && transform.parent != null)
            {
                _comboCelebration = transform.parent.GetComponentInChildren<ComboCelebrationController>(true);
            }
            if (_game == game && (gameplayCamera == null || _gameplayCamera == gameplayCamera))
            {
                if (_comboCelebration != null) _comboCelebration.Bind(_game);
                return;
            }
            ClearAndSynchronize();
            if (_grid != null) _grid.onBoardCleared -= ClearAndSynchronize;
            UnsubscribeGame();
            _game = game;
            if (_game != null)
            {
                _game.onMergeScoreAwarded += EnqueueAward;
                _game.onComboTurnStarted += BeginTurn;
                _game.onComboTurnEnded += EndTurn;
            }
            if (gameplayCamera != null) _gameplayCamera = gameplayCamera;
            _grid = game != null ? game.grid : null;
            if (_grid != null) _grid.onBoardCleared += ClearAndSynchronize;
            _recordBaseline = game != null ? game.recordScore : 0;
            _canvas = _popupLayer != null ? _popupLayer.GetComponentInParent<Canvas>() : null;
            if (_pulseTarget != null) _baseScale = _pulseTarget.localScale;
            if (_multiplierCounter != null) _multiplierCounter.Bind(_game, _gameplayCamera, _popupLayer, _pulseDuration, _pulseScale);
            if (_comboCelebration != null) _comboCelebration.Bind(_game);
            onDisplayedScoreChanged?.Invoke();
        }

        private void UnsubscribeGame()
        {
            if (_game == null) return;
            _game.onMergeScoreAwarded -= EnqueueAward;
            _game.onComboTurnStarted -= BeginTurn;
            _game.onComboTurnEnded -= EndTurn;
        }

        private void BeginTurn() { if (_multiplierCounter != null) _multiplierCounter.EndTurn(); }
        private void EndTurn() { if (_multiplierCounter != null) _multiplierCounter.EndTurn(); }

        public void Enqueue(int points, Vector3 mergeWorldPosition)
        {
            EnqueueAward(points, 1, points, mergeWorldPosition, _game != null ? _game.ComboTurnId : 0);
        }

        private void EnqueueAward(int basePoints, int multiplier, int points, Vector3 mergeWorldPosition, int turnId)
        {
            if (_game == null || !isActiveAndEnabled || _popupLayer == null || _scoreTarget == null ||
                !_scoreTarget.gameObject.activeInHierarchy || _popupPrefab == null || _gameplayCamera == null)
            {
                ClearAndSynchronize();
                return;
            }
            if (_canvas == null) _canvas = _popupLayer.GetComponentInParent<Canvas>();
            Vector3 screen = _gameplayCamera.WorldToScreenPoint(mergeWorldPosition + Vector3.up * _worldHeightOffset);
            if (screen.z <= 0f) { onDisplayedScoreChanged?.Invoke(); return; }
            Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
            Vector2 origin;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_popupLayer, screen, uiCamera, out origin);
            // Keep the entire number within the screen-space feedback layer.
            Rect bounds = _popupLayer.rect;
            float halfWidth = _popupPrefab.HalfTextWidth(points);
            origin.x = Mathf.Clamp(origin.x, bounds.xMin + halfWidth, bounds.xMax - halfWidth);
            origin.y = Mathf.Clamp(origin.y, bounds.yMin + 30f, bounds.yMax - _riseDistance - 30f);
            MergeScorePopup popup = null;
            while (_pool.Count > 0 && popup == null) popup = _pool.Pop();
            if (popup == null) popup = Instantiate(_popupPrefab, _popupLayer);
            _pendingPoints += points;
            _active.Add(popup);
            popup.Begin(points, origin, multiplier >= 2 ? _multipliedRiseDuration : _riseDuration, _flightDuration, _riseDistance);
            if (multiplier >= 2)
            {
                if (_multiplierCounter != null) _multiplierCounter.ShowMultiplier(multiplier);
                popup.PrepareMultiplier(basePoints);
                if (_multiplierCounter != null && _multiplierTokenPrefab != null)
                {
                    MergeMultiplierToken token = null;
                    while (_tokenPool.Count > 0 && token == null) token = _tokenPool.Pop();
                    if (token == null) token = Instantiate(_multiplierTokenPrefab, _popupLayer);
                    token.Begin(multiplier, _multiplierCounter.Position, popup, _multiplierFlightDuration);
                    _tokens.Add(token);
                }
                else popup.ApplyMultiplier();
            }
        }

        private void Update(){ Advance(Time.unscaledDeltaTime); }

        public void Advance(float delta)
        {
            if (_multiplierCounter != null) _multiplierCounter.Advance(delta);
            if (_comboCelebration != null) _comboCelebration.Advance(delta);
            if (_active.Count > 0)
            {
                if (_scoreTarget == null || !_scoreTarget.gameObject.activeInHierarchy || _popupLayer == null)
                { ClearAndSynchronize(); return; }
                Camera uiCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(uiCamera, _scoreTarget.TransformPoint(_scoreTarget.rect.center));
                Vector2 destination;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_popupLayer, screen, uiCamera, out destination);
                bool collected = false;
                for (int i = _active.Count - 1; i >= 0; i--)
                {
                    MergeScorePopup popup = _active[i];
                    if (popup != null && !popup.Advance(delta, destination)) continue;
                    if (popup == null)
                    { ClearAndSynchronize(); return; }
                    _pendingPoints -= popup.Points;
                    _active.RemoveAt(i);
                    _pool.Push(popup);
                    collected = true;
                }
                if (collected)
                {
                    _recordBaseline = Mathf.Max(_recordBaseline, DisplayedScore);
                    BeginPulse();
                    onDisplayedScoreChanged?.Invoke();
                }
            }
            for (int i = _tokens.Count - 1; i >= 0; i--)
            {
                var token = _tokens[i];
                bool complete = token == null || token.Advance(delta);
                if (token != null && token.HasArrived && token.HasValidTarget) token.Target.ApplyMultiplier();
                if (!complete) continue;
                if (token != null)
                {
                    token.Cancel(); _tokenPool.Push(token);
                }
                _tokens.RemoveAt(i);
            }
            if (!_pulsing || _pulseTarget == null) return;
            _pulseAge += Mathf.Max(0f, delta);
            float t = _pulseDuration <= 0f ? 1f : Mathf.Clamp01(_pulseAge / _pulseDuration);
            float scale = EvaluateCounterPulse(t, _pulseFrom, _pulseScale);
            _pulseTarget.localScale = _baseScale * scale;
            if (t >= 1f) { _pulsing = false; _pulseTarget.localScale = _baseScale; }
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
        private void BeginPulse()
        {
            _pulseFrom = _pulseTarget != null && _baseScale.x != 0f ? _pulseTarget.localScale.x / _baseScale.x : 1f;
            _pulseAge = 0f;
            _pulsing = _pulseTarget != null;
        }

        public void ClearAndSynchronize()
        {
            foreach (MergeScorePopup popup in _active)
                if (popup != null) { popup.Cancel(); _pool.Push(popup); }
            _active.Clear();
            foreach (var token in _tokens)
                if (token != null) { token.Cancel(); _tokenPool.Push(token); }
            _tokens.Clear();
            if (_multiplierCounter != null) _multiplierCounter.ResetCounter();
            if (_comboCelebration != null) _comboCelebration.ClearImmediate();
            _pendingPoints = 0;
            _pulsing = false;
            if (_pulseTarget != null) _pulseTarget.localScale = _baseScale;
            _recordBaseline = _game != null ? _game.recordScore : 0;
            onDisplayedScoreChanged?.Invoke();
        }

        private void OnDisable() { ClearAndSynchronize(); }
        private void OnApplicationPause(bool paused) { if (paused) ClearAndSynchronize(); }
        private void OnDestroy()
        {
            UnsubscribeGame();
            if (_grid != null) _grid.onBoardCleared -= ClearAndSynchronize;
        }
    }
}
