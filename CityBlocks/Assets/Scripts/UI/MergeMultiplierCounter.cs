using Gameplay;
using UnityEngine;

namespace UI
{
    public class MergeMultiplierCounter : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text _label;
        [SerializeField] private CanvasGroup _group;
        [SerializeField] private RectTransform _movesPanelAnchor;
        [SerializeField] private RectTransform _scorePanelAnchor;
        [SerializeField, Min(0f)] private float _hudClearance = 6f;
        [SerializeField, Min(0.01f)] private float _displayScale = 1f;
        [SerializeField] private Vector2 _inset = new Vector2(36f, 36f);
        [SerializeField, Min(0f)] private float _holdDuration = 2f;
        [SerializeField, Min(0f)] private float _shrinkDuration = 0.18f;
        private float _shrinkAge;
        private float _holdRemaining;
        private RectTransform _rect;
        private RectTransform _layer;
        private GameController _game;
        private Camera _camera;
        private Camera _uiCamera;
        private float _textHalfWidth;
        private readonly Vector3[] _gridBadgeCorners = new Vector3[4];
        private Vector3 _baseScale;
        private float _duration;
        private float _peak;
        private float _from;
        private float _age;
        private bool _pulsing;
        private bool _hideAfterPulse;
        public bool HasPendingPulse => _pulsing;
        public Color FontColor => _label != null ? _label.color : Color.white;
        public Vector2 Position { get { Reposition(); return _rect.anchoredPosition; } }

        public void Bind(GameController game, Camera camera, RectTransform layer, float duration, float peak)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_label == null) _label = GetComponentInChildren<TMPro.TMP_Text>(true);
            _baseScale = Vector3.one * _displayScale;
            _game = game; _camera = camera; _layer = layer;
            _duration = duration; _peak = peak;
            var canvas = layer.GetComponentInParent<Canvas>();
            _uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            for (int i = 0; i < _gridBadgeCorners.Length; i++)
                _gridBadgeCorners[i] = new Vector3(0f, 0.3f, 0.3f);
            if (_game != null && _game.tower != null)
            {
                RectTransform badge = null;
                foreach (var image in _game.tower.GetComponentsInChildren<UnityEngine.UI.Image>(true))
                    if (badge == null || image.rectTransform.rect.size.sqrMagnitude > badge.rect.size.sqrMagnitude)
                        badge = image.rectTransform;
                if (badge != null)
                {
                    badge.GetWorldCorners(_gridBadgeCorners);
                    for (int i = 0; i < _gridBadgeCorners.Length; i++)
                        _gridBadgeCorners[i] = _game.tower.transform.InverseTransformPoint(_gridBadgeCorners[i]);
                }
            }
            _label.raycastTarget = false;
            _textHalfWidth = _label.GetPreferredValues("x2").x * 0.5f + 8f;
            _group.blocksRaycasts = _group.interactable = false;
            ResetCounter();
        }

        public void ShowMultiplier(int multiplier)
        {
            if (_rect == null) return;
            _label.SetText("x{0}", multiplier);
            _textHalfWidth = _label.GetPreferredValues(_label.text).x * 0.5f + 8f;
            _group.alpha = 1f;
            _from = _rect.localScale.x / _baseScale.x;
            _age = 0f; _pulsing = true; _hideAfterPulse = false;
            _shrinkAge = 0f;
            _holdRemaining = _holdDuration;
            Reposition();
        }

        public void EndTurn()
        {
            _hideAfterPulse = true;
        }

        public void ResetCounter()
        {
            _pulsing = false; _hideAfterPulse = false;
            _holdRemaining = 0f;
            _shrinkAge = 0f;
            if (_rect != null) _rect.localScale = _baseScale;
            if (_group != null) _group.alpha = 0f;
            if (_label != null) _label.text = "";
        }

        public void Advance(float delta)
        {
            delta = Mathf.Max(0f, delta);
            if (_pulsing)
            {
                _age += delta;
                float t = _duration <= 0f ? 1f : Mathf.Clamp01(_age / _duration);
                _rect.localScale = _baseScale * ScoreFeedbackController.EvaluateCounterPulse(t, _from, _peak);
                if (t < 1f) return;
                _pulsing = false; _rect.localScale = _baseScale;
                delta = Mathf.Max(0f, _age - _duration);
                _holdRemaining = _holdDuration;
            }
            float holdTime = Mathf.Min(_holdRemaining, delta);
            _holdRemaining -= holdTime;
            delta -= holdTime;
            if (_hideAfterPulse && _holdRemaining <= 0f && _group != null && _group.alpha > 0f)
            {
                _shrinkAge += delta;
                float t = _shrinkDuration <= 0f ? 1f : Mathf.Clamp01(_shrinkAge / _shrinkDuration);
                float eased = t * t * (3f - 2f * t);
                _rect.localScale = _baseScale * (1f - eased);
                _group.alpha = 1f - eased;
            }
        }

        private void LateUpdate() { Reposition(); }
        private Vector2 ToLayerPoint(Vector3 worldPoint)
        {
            Vector2 point;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer,
                RectTransformUtility.WorldToScreenPoint(_uiCamera, worldPoint), _uiCamera, out point);
            return point;
        }

        public void Reposition()
        {
            if (_scorePanelAnchor != null && _game != null && _game.grid != null &&
                _camera != null && _layer != null && _rect != null)
            {
                var grid = _game.grid;
                float upper = ToLayerPoint(_scorePanelAnchor.TransformPoint(
                    new Vector3(_scorePanelAnchor.rect.center.x, _scorePanelAnchor.rect.yMin, 0f))).y - _hudClearance;
                if (_movesPanelAnchor != null && _movesPanelAnchor.gameObject.activeInHierarchy)
                    upper = Mathf.Min(upper, ToLayerPoint(_movesPanelAnchor.TransformPoint(
                        new Vector3(_movesPanelAnchor.rect.center.x, _movesPanelAnchor.rect.yMin, 0f))).y - _hudClearance);
                float lower = float.MinValue;
                // The last row is the temporary spawn row, not part of the visible tower grid.
                int topRow = Mathf.Max(0, grid.totalRows - 2);
                for (int col = 0; col < grid.totalColumns; col++)
                foreach (Vector3 corner in _gridBadgeCorners)
                {
                    Vector3 screen = _camera.WorldToScreenPoint(grid.WorldOffset + new Vector3(col, 0f, topRow) + corner);
                    Vector2 local;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, _uiCamera, out local);
                    lower = Mathf.Max(lower, local.y + _hudClearance);
                }
                if (upper > lower)
                {
                    Vector3 centerScreen = _camera.WorldToScreenPoint(grid.WorldOffset +
                        new Vector3((grid.totalColumns - 1) * 0.5f, 0f, topRow * 0.5f));
                    Vector2 gridCenter;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, centerScreen, _uiCamera, out gridCenter);
                    float peak = Mathf.Max(1f, _peak);
                    float width = Mathf.Max(_rect.rect.width, _textHalfWidth * 2f);
                    float fit = Mathf.Min(_displayScale, (upper - lower) / (_rect.rect.height * peak),
                        (_layer.rect.width - _hudClearance * 2f) / (width * peak));
                    float pulse = _baseScale.x > 0f ? _rect.localScale.x / _baseScale.x : 1f;
                    _baseScale = Vector3.one * fit;
                    _rect.localScale = _baseScale * pulse;
                    float halfWidth = width * fit * peak * 0.5f;
                    _rect.anchoredPosition = new Vector2(Mathf.Clamp(gridCenter.x,
                        _layer.rect.xMin + halfWidth + _hudClearance,
                        _layer.rect.xMax - halfWidth - _hudClearance), (upper + lower) * 0.5f);
                    return;
                }
            }
            if (_movesPanelAnchor != null && _layer != null && _rect != null)
            {
                Vector3 corner = _movesPanelAnchor.TransformPoint(new Vector3(_movesPanelAnchor.rect.xMax, _movesPanelAnchor.rect.yMin, 0f));
                Vector2 screen = RectTransformUtility.WorldToScreenPoint(_uiCamera, corner);
                Vector2 anchor;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, _uiCamera, out anchor);
                Vector2 halfSize = new Vector2(_textHalfWidth, _rect.rect.height * 0.5f) * Mathf.Max(1f, _peak);
                Vector2 hudPosition = anchor + new Vector2(halfSize.x + _inset.x, -halfSize.y - _inset.y);
                if (!_movesPanelAnchor.gameObject.activeInHierarchy)
                {
                    Vector3 center = _movesPanelAnchor.TransformPoint(_movesPanelAnchor.rect.center);
                    Vector2 centerScreen = RectTransformUtility.WorldToScreenPoint(_uiCamera, center);
                    Vector2 centerInLayer;
                    RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, centerScreen, _uiCamera, out centerInLayer);
                    hudPosition.y = centerInLayer.y;
                }
                Rect layerBounds = _layer.rect;
                hudPosition.x = Mathf.Clamp(hudPosition.x, layerBounds.xMin + halfSize.x, layerBounds.xMax - halfSize.x);
                hudPosition.y = Mathf.Clamp(hudPosition.y, layerBounds.yMin + halfSize.y, layerBounds.yMax - halfSize.y);
                _rect.anchoredPosition = hudPosition;
                return;
            }
            if (_game == null || _game.queue == null || _camera == null || _layer == null || _rect == null) return;
            var queue = _game.queue;
            Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
            int visible = Mathf.Min(queue.visibleCount, queue.towerQueue.Count);
            for (int i = 0; i < Mathf.Max(1, visible); i++)
            {
                if (visible > 0 && queue.towerQueue[i] == null) continue;
                Vector3 center = visible == 0 ? queue.PositionForSlot(0) : queue.towerQueue[i].transform.position;
                float radius = i == 0 ? 0.65f : 0.35f;
                Vector3 point = center + Vector3.up * 0.85f + _camera.transform.right * radius;
                Vector3 screen = _camera.WorldToScreenPoint(point);
                if (screen.z <= 0f) continue;
                Vector2 local;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(_layer, screen, _uiCamera, out local);
                if (local.x > maximum.x) maximum = local;
            }
            if (maximum.x == float.MinValue) return;
            Vector2 half = new Vector2(_textHalfWidth, _rect.rect.height * 0.5f);
            Vector2 position = maximum + new Vector2(half.x + _inset.x, _inset.y);
            Rect bounds = _layer.rect;
            Vector2 pulseHalf = half * Mathf.Max(1f, _peak);
            position.x = Mathf.Clamp(position.x, bounds.xMin + pulseHalf.x, bounds.xMax - pulseHalf.x);
            position.y = Mathf.Clamp(position.y, bounds.yMin + pulseHalf.y, bounds.yMax - pulseHalf.y);
            _rect.anchoredPosition = position;
        }
    }
}
