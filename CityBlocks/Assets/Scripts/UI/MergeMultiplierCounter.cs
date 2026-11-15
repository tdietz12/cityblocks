using Gameplay;
using UnityEngine;

namespace UI
{
    public class MergeMultiplierCounter : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text _label;
        [SerializeField] private CanvasGroup _group;
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
        private Vector3 _baseScale;
        private float _duration;
        private float _peak;
        private float _from;
        private float _age;
        private bool _pulsing;
        private bool _hideAfterPulse;
        public bool HasPendingPulse => _pulsing;
        public Vector2 Position { get { Reposition(); return _rect.anchoredPosition; } }

        public void Bind(GameController game, Camera camera, RectTransform layer, float duration, float peak)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (_group == null) _group = GetComponent<CanvasGroup>();
            if (_label == null) _label = GetComponentInChildren<TMPro.TMP_Text>(true);
            _baseScale = Vector3.one;
            _game = game; _camera = camera; _layer = layer;
            _duration = duration; _peak = peak;
            var canvas = layer.GetComponentInParent<Canvas>();
            _uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
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
        public void Reposition()
        {
            if (_game == null || _game.queue == null || _camera == null || _layer == null || _rect == null) return;
            var queue = _game.queue;
            Vector2 maximum = new Vector2(float.MinValue, float.MinValue);
            int visible = Mathf.Min(queue.visibleCount, queue.towerQueue.Count);
            for (int i = 0; i < Mathf.Max(1, visible); i++)
            {
                if (visible > 0 && queue.towerQueue[i] == null) continue;
                Vector3 center = visible == 0 ? new Vector3(2f, 0f, -1.5f) : queue.towerQueue[i].transform.position;
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
