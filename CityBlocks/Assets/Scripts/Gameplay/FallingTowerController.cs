using UnityEngine;

namespace Gameplay
{
    public class FallingTowerController : MonoBehaviour
    {
        public Vector3 mergeTarget;
        [SerializeField, Min(0f)] private float _duration = 0.18f;
        private Vector3 _start;
        private Vector3 _startScale = Vector3.one;
        private Transform _shrinkTarget;
        private Vector3 _shrinkTargetStartScale = Vector3.one;
        private bool _shrinkSelf;
        private bool _hideOnComplete = true;
        private float _elapsed;
        private bool _initialized;
        public bool IsComplete { get; private set; }

        internal void BeginSlide(Vector3 target, float duration, bool shrinkSelf = true, Transform shrinkTarget = null, bool hideOnComplete = true)
        {
            mergeTarget = target;
            _duration = Mathf.Max(0f, duration);
            _start = transform.position;
            _startScale = transform.localScale;
            _shrinkSelf = shrinkSelf;
            _shrinkTarget = shrinkTarget;
            _shrinkTargetStartScale = shrinkTarget != null ? shrinkTarget.localScale : Vector3.one;
            _hideOnComplete = hideOnComplete;
            _elapsed = 0f;
            IsComplete = false;
            _initialized = true;
        }

        private void Update()
        {
            Step(Time.deltaTime);
        }

        internal void Step(float delta)
        {
            if (!_initialized || IsComplete) return;
            _elapsed += Mathf.Max(0f, delta);
            float t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            float eased = t * t * (3f - 2f * t);
            transform.position = Vector3.LerpUnclamped(_start, mergeTarget, eased);
            if (_shrinkSelf)
            {
                transform.localScale = Vector3.LerpUnclamped(_startScale, Vector3.zero, eased);
            }
            if (_shrinkTarget != null)
            {
                _shrinkTarget.localScale = Vector3.LerpUnclamped(_shrinkTargetStartScale, Vector3.zero, eased);
            }
            if (t < 1f) return;
            transform.position = mergeTarget;
            if (_shrinkSelf) transform.localScale = Vector3.zero;
            if (_shrinkTarget != null) _shrinkTarget.localScale = Vector3.zero;
            IsComplete = true;
            // Hide on arrival when sliding a destroyed neighbour proxy; keep visible when driving the survivor.
            if (_hideOnComplete) gameObject.SetActive(false);
        }
    }
}

