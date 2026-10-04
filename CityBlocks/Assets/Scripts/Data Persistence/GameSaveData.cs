using System.Collections.Generic;
using UnityEngine;

namespace Data_Persistence
{
    [System.Serializable]
    public class GameSaveData
    {
        public bool hasActiveRun;
        public int score;
        public int level;
        public int recordScore;
        public int recordLevel;
        public bool isLost;
        public List<Vector3> boardState;
        public int firstInQueue;
        public int nextInQueue;
        public int thirdInQueue;
        public int lookAheadTurnsRemaining;
        public bool lookAheadUsed;

        public GameSaveData()
        {
            this.hasActiveRun = false;
            this.score = 0;
            this.level = 0;
            this.recordScore = 0;
            this.recordLevel = 0;
            this.isLost = false;
            this.boardState = new List<Vector3>();
            this.firstInQueue = 0;
            this.nextInQueue = 0;
            this.thirdInQueue = 0;
            this.lookAheadTurnsRemaining = 0;
            this.lookAheadUsed = false;
        }
    }
}
