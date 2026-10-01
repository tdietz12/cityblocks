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
