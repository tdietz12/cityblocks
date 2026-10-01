using System.Collections.Generic;
using UnityEngine;

namespace Data_Persistence
{
    [System.Serializable]
    public class GameSaveData
    {
        public int score;
        public int level;
        public List<Vector3> boardState;
        public int firstInQueue;
        public int nextInQueue;

        public GameSaveData()
        {
            this.score = 0;
            this.level = 0;
            this.boardState = null;
            this.firstInQueue = 0;
            this.nextInQueue = 0;
        }
    }
}
