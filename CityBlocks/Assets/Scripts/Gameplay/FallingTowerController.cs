using UnityEngine;

namespace Gameplay
{
    public class FallingTowerController : MonoBehaviour
    {
        public Vector3 mergeTarget;
        [SerializeField, Min(0f)] private float _duration = 0.18f;
        private Vector3 _start;
        private float _elapsed;
        private bool _initialized;
        public bool IsComplete { get; private set; }

        internal void BeginSlide(Vector3 target, float duration)
        {
            mergeTarget = target;
            _duration = Mathf.Max(0f, duration);
            _start = transform.position;
            _elapsed = 0f;
            IsComplete = false;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || IsComplete) return;
            _elapsed += Time.deltaTime;
            float t = _duration <= 0f ? 1f : Mathf.Clamp01(_elapsed / _duration);
            transform.position = Vector3.LerpUnclamped(_start, mergeTarget, t * t * (3f - 2f * t));
            if (t < 1f) return;
            transform.position = mergeTarget;
            IsComplete = true;
            // Hide exactly on arrival; the owning batch performs destruction and logical commit.
            gameObject.SetActive(false);
        }
    }
}
