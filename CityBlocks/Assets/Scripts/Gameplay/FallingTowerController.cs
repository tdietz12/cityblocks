using UnityEngine;
using UnityEngine.Events;

namespace Gameplay
{
    public class FallingTowerController : MonoBehaviour
    {
        public Vector3 mergeTarget;

        internal UnityEvent DestroyTower;

        void Start()
        {
            Invoke("DestroyFallingTower", .15f);
        }

        void Update()
        {
            if (Vector3.Distance(transform.position, mergeTarget) < .25f)
            {
                transform.position = mergeTarget;
                transform.localScale = Vector3.one * .5f;
            }
            else
            {
                transform.position = Vector3.Lerp(transform.position, mergeTarget, .1f);
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * .5f, .1f);
            }
        }

        void DestroyFallingTower()
        {
            Destroy(this.gameObject);
        }
    }
}