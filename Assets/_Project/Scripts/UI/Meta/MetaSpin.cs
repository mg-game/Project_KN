using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>천천히 도는 빛살 (개방 카드, 복귀 화면 머리)</summary>
    public class MetaSpin : MonoBehaviour
    {
        [SerializeField] float _degreesPerSecond = 18f;

        void Update() => transform.Rotate(0f, 0f, -_degreesPerSecond * Time.unscaledDeltaTime);
    }
}
