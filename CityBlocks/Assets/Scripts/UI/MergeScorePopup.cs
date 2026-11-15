using UnityEngine;

namespace UI
{
    public class MergeScorePopup : MonoBehaviour
    {
        [SerializeField] private TMPro.TMP_Text _label;
        [SerializeField] private CanvasGroup _group;
        [SerializeField, Min(0f)] private float _multiplierBurstDuration = 0.15f;
        [SerializeField, Range(1f, 3f)] private float _multiplierBurstScale = 2.0f;
        private bool _bursting;
        private float _burstAge;
        private float _burstFrom;
        private RectTransform _rect;
        private Vector2 _origin;
        private float _age;
        private float _riseDuration;
        private float _flightDuration;
        private float _riseDistance;
        private bool _running;
        public int Points { get; private set; }
        public int Lease { get; private set; }
        public Vector2 Position => _rect.anchoredPosition;
        public bool IsComplete { get; private set; }
        private bool _multiplierApplied = true;

        public void PrepareMultiplier(int basePoints)
        {
            _multiplierApplied = false;
            _label.SetText("+{0}", basePoints);
        }

        public void ApplyMultiplier()
        {
            if (_multiplierApplied || !_running) return;
            _multiplierApplied = true;
            _label.SetText("+{0}", Points);
            _burstAge = 0f;
            _burstFrom = _rect.localScale.x;
            _bursting = true;
        }

        private void Awake() { Cache(); }
        private void Cache()
        {
            if (_rect == null) _rect = (RectTransform)transform;
            if (_label == null) _label = GetComponent<TMPro.TMP_Text>();
            if (_group == null) _group = GetComponent<CanvasGroup>();
        }

        public float HalfTextWidth(int points)
        {
            Cache();
            return _label.GetPreferredValues("+" + points).x * 0.5f + 8f;
        }

        public void Begin(int points, Vector2 origin, float riseDuration, float flightDuration, float riseDistance)
        {
            Cache();
            Lease++;
            _multiplierApplied = true;
            _bursting = false;
            _burstAge = 0f;
            Points = points;
            _origin = origin;
            _age = 0f;
            _riseDuration = Mathf.Max(0f, riseDuration);
            _flightDuration = Mathf.Max(0f, flightDuration);
            _riseDistance = riseDistance;
            _running = true;
            IsComplete = false;
            _rect.anchoredPosition = origin;
            _rect.localScale = Vector3.one * 0.8f;
            _label.SetText("+{0}", points);
            _label.raycastTarget = false;
            _group.alpha = 1f;
            _group.interactable = _group.blocksRaycasts = false;
            gameObject.SetActive(true);
        }

        public bool Advance(float delta, Vector2 destination)
        {
            bool complete = AdvanceMotion(delta, destination);
            if (!complete && _running && _bursting)
            {
                _burstAge += Mathf.Max(0f, delta);
                float t = _multiplierBurstDuration <= 0f ? 1f : Mathf.Clamp01(_burstAge / _multiplierBurstDuration);
                _rect.localScale = Vector3.one * ScoreFeedbackController.EvaluateCounterPulse(t, _burstFrom, _multiplierBurstScale);
                if (t >= 1f) _bursting = false;
            }
            return complete;
        }

        private bool AdvanceMotion(float delta, Vector2 destination)
        {
            if (!_running) return false;
            _age += Mathf.Max(0f, delta);
            Vector2 risen = _origin + Vector2.up * _riseDistance;
            if ((!_multiplierApplied || _bursting) && _age >= _riseDuration)
            {
                _age = _riseDuration;
                _rect.anchoredPosition = risen;
                _rect.localScale = Vector3.one;
                return false;
            }
            if (_age < _riseDuration)
            {
                _rect.anchoredPosition = Vector2.LerpUnclamped(_origin, risen, _age / _riseDuration);
                float pop = Mathf.Clamp01(_age / 0.12f);
                _rect.localScale = Vector3.one * (pop < 0.5f
                    ? Mathf.Lerp(0.8f, 1.12f, pop * 2f)
                    : Mathf.Lerp(1.12f, 1f, (pop - 0.5f) * 2f));
                return false;
            }
            float t = _flightDuration <= 0f ? 1f : Mathf.Clamp01((_age - _riseDuration) / _flightDuration);
            // Accelerating collection flight, with an exact target endpoint.
            _rect.anchoredPosition = Vector2.LerpUnclamped(risen, destination, t * t);
            _rect.localScale = Vector3.one * Mathf.Lerp(1f, 0.8f, t);
            if (t < 1f) return false;
            _rect.anchoredPosition = destination;
            _running = false;
            IsComplete = true;
            _group.alpha = 0f;
            gameObject.SetActive(false);
            return true;
        }

        public void Cancel()
        {
            Lease++;
            _running = false;
            _bursting = false;
            IsComplete = false;
            gameObject.SetActive(false);
        }
    }
}
