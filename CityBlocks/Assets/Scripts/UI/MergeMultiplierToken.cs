using UnityEngine;

namespace UI
{
    public class MergeMultiplierToken : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text _label;
        [SerializeField, Min(0f)] private float _shrinkDuration = 0.1f;
        private CanvasGroup _group;
        private bool _arrived;
        private float _shrinkAge;
        public bool HasArrived => _arrived;
        private RectTransform _rect;
        private Vector2 _origin;
        private float _age;
        private float _duration;
        private int _lease;
        public MergeScorePopup Target { get; private set; }
        public bool HasValidTarget => Target != null && Target.Lease == _lease && Target.gameObject.activeInHierarchy;

        public void Begin(int multiplier, Vector2 origin, MergeScorePopup target, float duration)
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (_label == null) _label = GetComponent<TMPro.TMP_Text>();
            if (_group == null) _group = GetComponent<CanvasGroup>();
            _group.alpha = 1f;
            _arrived = false; _shrinkAge = 0f;
            _label.SetText("x{0}", multiplier);
            _label.raycastTarget = false;
            _origin = origin;
            _age = 0f;
            _duration = Mathf.Max(0f, duration);
            Target = target;
            _lease = target.Lease;
            _rect.anchoredPosition = origin;
            _rect.localScale = Vector3.one;
            gameObject.SetActive(true);
        }

        public bool Advance(float delta)
        {
            if (!HasValidTarget) return true;
            delta = Mathf.Max(0f, delta);
            if (!_arrived)
            {
                _age += delta;
                float t = _duration <= 0f ? 1f : Mathf.Clamp01(_age / _duration);
                _rect.anchoredPosition = Vector2.LerpUnclamped(_origin, Target.Position, t * t * (3f - 2f * t));
                if (t < 1f) return false;
                _arrived = true;
                delta = Mathf.Max(0f, _age - _duration);
            }
            _rect.anchoredPosition = Target.Position;
            _shrinkAge += delta;
            float shrink = _shrinkDuration <= 0f ? 1f : Mathf.Clamp01(_shrinkAge / _shrinkDuration);
            float scale = 1f - shrink * shrink * (3f - 2f * shrink);
            _rect.localScale = Vector3.one * scale;
            _group.alpha = scale;
            return shrink >= 1f;
        }

        public void Cancel() { Target = null; gameObject.SetActive(false); }
    }
}
