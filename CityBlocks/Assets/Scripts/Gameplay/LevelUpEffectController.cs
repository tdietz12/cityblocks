using UnityEngine;

namespace Gameplay
{
    public class LevelUpEffectController : MonoBehaviour
    {
        void Start()
        {
            Invoke("DestroyLevelUpEffect", .25f);
        }

        void DestroyLevelUpEffect()
        {
            Destroy(this.gameObject);
        }
    }
}
