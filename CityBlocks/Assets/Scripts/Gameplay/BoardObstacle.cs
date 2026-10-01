using UnityEngine;

namespace Gameplay
{
    public class BoardObstacle : MonoBehaviour
    {
        [SerializeField] private GameObject[] durabilityPips;

        public BoardObstacleType Type { get; private set; }
        public int Column { get; private set; }
        public int Row { get; private set; }
        public int Durability { get; private set; }
        public bool IsMoving => transform.position != target;

        private Vector3 target;

        public void Initialize(BoardObstacleType type, int column, int row, int durability, Vector3 position)
        {
            Type = type;
            Column = column;
            Row = row;
            Durability = durability;
            target = position;
            transform.position = position;
            RefreshVisual();
        }

        public void MoveTo(int row, Vector3 position)
        {
            Row = row;
            target = position;
        }

        public bool Damage()
        {
            if (Type != BoardObstacleType.Breakable) return false;
            Durability--;
            RefreshVisual();
            return Durability <= 0;
        }

        private void RefreshVisual()
        {
            if (durabilityPips == null) return;
            for (int index = 0; index < durabilityPips.Length; index++)
                if (durabilityPips[index] != null)
                    durabilityPips[index].SetActive(Type == BoardObstacleType.Breakable &&
                                                    index < Durability);
        }

        private void Update()
        {
            if (transform.position != target)
                transform.position = Vector3.MoveTowards(transform.position, target, Time.deltaTime * 8f);
        }
    }
}
