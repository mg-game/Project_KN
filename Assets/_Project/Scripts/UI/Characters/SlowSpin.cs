using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>천천히 도는 빛 고리 (해금 완료, 소환 카드 뒷면)</summary>
    public class SlowSpin : MonoBehaviour
    {
        [SerializeField] float _degreesPerSecond = 24f;

        void Update() => transform.Rotate(0f, 0f, -_degreesPerSecond * Time.unscaledDeltaTime);
    }
}
