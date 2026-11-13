using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 수선점 둘레의 금빛 방위 링(바닥 평면, 반지름 1)과 가장자리 위험 표시들.
    /// 링은 숨 쉬듯 밝아지고, 방향별 위협도는 DangerArrowView로 전달한다.
    /// 적이 수선점을 때리면 그 적이 들어온 방향의 링 칸(72° 호)이 0.6초 동안 빨갛게 번쩍이고 "!" 표시가 톡 튄다 (ATK-03 / ATK-17 색 접근성:
    /// 5방향 디펜스에서 "어디가 뚫렸나"를 색만이 아니라 모양으로도 바로 보여준다). 보스 공격은 더 크고 진하게. 번쩍임 줄이기면 처음 깜박임 없이 스민다.
    /// </summary>
    public class DirectionRingView : MonoBehaviour
    {
        [SerializeField] BattleFieldMapper _mapper;
        [SerializeField] Image _ring;
        [SerializeField] Image _ringHalo;
        [Tooltip("링 스프라이트에서 고리가 차지하는 반지름 비율 (텍스처 반폭 = 1)")]
        [SerializeField] float _ringSpriteRadius = 0.84f;
        [SerializeField] DangerArrowView[] _arrows;

        [Header("뚫린 방향 번쩍임")]
        [Tooltip("방위 칸별 붉은 호 (바닥 평면, Radial360 Top 시계 방향, fillAmount 0.2)")]
        [SerializeField] Image[] _slotFlash;
        [Tooltip("방위 칸별 \"!\" 표시 묶음 (전장 로컬, 수선점과 지킴이 사이 위쪽 — 번쩍일 때만 켠다)")]
        [SerializeField] RectTransform[] _alerts;
        [Tooltip("\"!\" 바탕 붉은 원")]
        [SerializeField] Image[] _alertBadges;
        [Tooltip("\"!\" 배지 색. 빨간 배지 그림(ui_badge_red)이면 흰색, 예전 흰 원이면 Danger")]
        [SerializeField] Color _alertBadgeTint = UITheme.Danger;
        [Tooltip("\"!\" 글자 (흰색 모양)")]
        [SerializeField] Image[] _alertMarks;

        int _layoutVersion = -1;
        readonly float[] _flash = new float[BattleGeometry.SlotCount];
        readonly float[] _flashPeak = new float[BattleGeometry.SlotCount];

        /// <param name="danger">방위 슬롯별 위협도 0~1 (길이 = BattleGeometry.SlotCount)</param>
        public void Tick(BattleSimulator sim, float[] danger, float dt)
        {
            if (_mapper.Version != _layoutVersion) Layout();

            float time = Time.time;
            float breathe = 0.5f + 0.5f * Mathf.Sin(time * 1.4f);
            float lamp = sim != null ? Mathf.Clamp01(sim.LampGauge) : 0f;
            if (_ring != null) _ring.color = BattleEase.WithAlpha(UITheme.Lamp, 0.55f + 0.25f * breathe + 0.2f * lamp);
            if (_ringHalo != null) _ringHalo.color = BattleEase.WithAlpha(UITheme.GoldAccent, 0.16f + 0.12f * breathe + 0.15f * lamp);

            for (int i = 0; i < _arrows.Length; i++)
            {
                _arrows[i].Tick(danger != null && i < danger.Length ? danger[i] : 0f, dt);
            }
            TickFlash(dt, time);
        }

        /// <summary>적이 이 방향으로 수선점을 때렸다: 링 칸이 0.6초 빨갛게 번쩍이고 "!"가 튄다 (보스는 더 진하게)</summary>
        public void FlashSlot(int slot, bool boss)
        {
            if (slot < 0 || slot >= _flash.Length) return;
            // 아직 진하게 번쩍이는 중이면 (보스 번쩍임 위로 일반 피격이 와도) 더 진한 쪽을 남긴다
            float previous = _flash[slot] > 0.5f ? _flashPeak[slot] : 0f;
            _flash[slot] = 1f;
            _flashPeak[slot] = Mathf.Max(boss ? 1f : 0.75f, previous);
        }

        void TickFlash(float dt, float time)
        {
            float decay = dt / EnemyFxCatalog.RingFlashTime;
            for (int i = 0; i < _flash.Length; i++)
            {
                float k = _flash[i];
                if (k <= 0f && !Visible(i)) continue;
                _flash[i] = k = Mathf.Max(0f, k - decay);
                if (k <= 0f) _flashPeak[i] = 0f;
                // 처음 0.15초는 빠르게 두 번 깜박이고 천천히 스민다 (8Hz 붉은 깜박임 — 번쩍임 줄이기면 깜박이지 않고 스미기만, "!"가 모양으로 알린다)
                float blink = k > 0.75f && !BattleFx.ReduceFlashNow ? 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(time * 26f)) : 1f;
                float alpha = _flashPeak[i] * k * blink;
                var arc = Pick(_slotFlash, i);
                if (arc != null)
                {
                    bool show = alpha > 0.01f;
                    if (arc.enabled != show) arc.enabled = show;
                    if (show) arc.color = BattleEase.WithAlpha(UITheme.Danger, alpha);
                }
                var rect = _alerts != null && i < _alerts.Length ? _alerts[i] : null;
                if (rect == null) continue;
                bool showIcon = k > 0.01f;
                if (rect.gameObject.activeSelf != showIcon) rect.gameObject.SetActive(showIcon);
                if (!showIcon) continue;
                float age = 1f - k;
                float pop = age < 0.25f ? BattleEase.OutBack(age / 0.25f, 2.6f) : 1f;
                float scale = pop * (1f + 0.08f * Mathf.Sin(time * 14f)) * (_flashPeak[i] >= 1f ? 1.25f : 1f);
                rect.localScale = new Vector3(scale, scale, 1f);
                float fade = Mathf.Clamp01(k * 2.2f);
                var badge = Pick(_alertBadges, i);
                if (badge != null) badge.color = BattleEase.WithAlpha(_alertBadgeTint, fade);
                var mark = Pick(_alertMarks, i);
                if (mark != null) mark.color = BattleEase.WithAlpha(UITheme.Cream, fade);
            }
        }

        bool Visible(int slot)
        {
            var arc = Pick(_slotFlash, slot);
            var alert = _alerts != null && slot < _alerts.Length ? _alerts[slot] : null;
            return (arc != null && arc.enabled) || (alert != null && alert.gameObject.activeSelf);
        }

        void Layout()
        {
            _layoutVersion = _mapper.Version;
            float diameter = 2f * BattleGeometry.RingRadius * _mapper.Ppu / Mathf.Max(0.1f, _ringSpriteRadius);
            if (_ring != null) _ring.rectTransform.sizeDelta = new Vector2(diameter, diameter);
            if (_ringHalo != null) _ringHalo.rectTransform.sizeDelta = new Vector2(diameter * 1.08f, diameter * 1.08f);
            for (int i = 0; i < BattleGeometry.SlotCount; i++)
            {
                var arc = Pick(_slotFlash, i);
                if (arc != null)
                {
                    // Radial360 + Top + 시계 방향: 12시에서 시계 방향으로 72° → 칸 가운데가 (90 - 36)도. 칸 방향으로 돌린다 (GuardianSlotView 부채꼴과 같은 계산)
                    const float arcDegrees = 360f / BattleGeometry.SlotCount;
                    float slotDegrees = BattleGeometry.SlotAngle(i) * Mathf.Rad2Deg;
                    arc.fillAmount = arcDegrees / 360f;
                    arc.rectTransform.sizeDelta = new Vector2(diameter * 1.02f, diameter * 1.02f);
                    arc.rectTransform.localRotation = Quaternion.Euler(0f, 0f, slotDegrees - (90f - arcDegrees * 0.5f));
                }
                if (_alerts != null && i < _alerts.Length && _alerts[i] != null)
                {
                    // 수선점과 방위 링 사이, 바닥보다 조금 위 (지킴이에 가리지 않게 링 안쪽)
                    var world = BattleGeometry.FromAngle(BattleGeometry.SlotAngle(i), 0.62f);
                    _alerts[i].anchoredPosition = _mapper.WorldToField(world) + Vector2.up * (44f * _mapper.ActorScale);
                }
            }
        }

        static Image Pick(Image[] images, int index) => images != null && index >= 0 && index < images.Length ? images[index] : null;
    }
}
