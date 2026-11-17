using System.Collections.Generic;
using UnityEngine;

namespace Gameplay
{
    public class QueueController : MonoBehaviour
    {
        //Creates a singleton (only one per scene), get the instace publicly, can only be set privately
        public static QueueController instance { get; private set; }

        internal List<GameObject> towerQueue = new List<GameObject>();

        internal int firstInQueue;
        internal int nextInQueue;
        public int visibleCount = 2;

        [Header("Queue Layout")]
        [SerializeField] private float _depth = -1.5f;

        public Vector3 PositionForSlot(int index)
        {
            float x = index > 0 ? (index * -0.5f) + 1.75f : 2f;
            return new Vector3(x, 0f, _depth);
        }

        void Awake()
        {
            if (instance != null)
            {
                Debug.LogError("Found more than one GameObject with this class in the scene.");
            }
            instance = this;
        }
    }
}
