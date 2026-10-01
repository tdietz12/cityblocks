using UnityEngine;

namespace UI
{
    public class Cell : MonoBehaviour
    {
        public int col;
        public int row;

        public delegate void CellHitEvent(int col, int row);
        public event CellHitEvent onCellHitEvent;

        void Update()
        {
            if (Input.GetMouseButtonDown(0) || (Input.touchCount > 0 && Input.GetTouch(0).phase == TouchPhase.Began))
            {
                RaycastHit hit;
                Ray ray = Camera.main.ScreenPointToRay(GetInputPosition());
                if (Physics.Raycast(ray, out hit))
                {
                    if (hit.collider == GetComponent<BoxCollider>())
                    {
                        if (onCellHitEvent != null)
                        {
                            onCellHitEvent(col, row);
                        }
                    }
                }
            }
        }

        private Vector3 GetInputPosition()
        {
            if (Input.touchCount > 0)
            {
                return Input.GetTouch(0).position;
            }
            else
            {
                return Input.mousePosition;
            }
        }
    }
}