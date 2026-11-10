using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 패배 분석 고리 (CR-01, SPEC4 §7.2 + 비평 C-05): 실패 배너가 끝나면(1.2 / 1.6초) 0.2초에 나타나 결과 대기(ExtendEndPause 4.8초)가 끝날 때까지 —
    /// 왼쪽 위(StageArea TopLeft 24,−310): 고리 200 (방향 5칸 · 칸마다 72° − 틈 4°, SubText 0.55 · 가장 뚫린 칸 Danger 0.95 + 한 번 1.06 톡,
    /// 가운데 '{n}%' + '!' 그림 — 색만으로 뜻을 싣지 않는다) + 오른쪽 설명 판 280×200 ('{방향}이/가 많이 뚫렸어요' — DefeatAnalyzer.Title ·'{지킴이} 자리' / '빈 자리' · 조언 · 링크 둘).
    /// 고리 · '편성 바꾸기 ›' → 편성 탭 + 그 칸 반짝 (UINav.PulseFormationSlot), '바로 다시 도전 ›' → Game.Battle.SkipEndPause.
    /// 다음 전투가 시작되거나 · 팝업이 열리거나 · 눌리면 사라진다. 빌더가 만들기 전(사용자 씬)에는 BattleView 참조가 비어 아무 일도 없다
    /// </summary>
    public class DefeatAnalysisView : MonoBehaviour
    {
        [SerializeField] CanvasGroup _group;
        [Tooltip("방향 칸 0~4 (Filled Radial360, 칸마다 회전은 빌더가 둔다)")]
        [SerializeField] Image[] _segments;
        [SerializeField] TMP_Text _percent;
        [SerializeField] TMP_Text _title;
        [SerializeField] TMP_Text _slotLine;
        [SerializeField] TMP_Text _advice;
        [SerializeField] Button _ringButton;
        [SerializeField] Button _formationLink;
        [SerializeField] Button _retryLink;

        /// <summary>나타나기 · 톡 시간 (초)</summary>
        public const float FadeIn = 0.2f;
        const float PopTime = 0.35f;
        static readonly Color SegmentColor = new(UITheme.SubText.r, UITheme.SubText.g, UITheme.SubText.b, 0.55f);
        static readonly Color TopColor = new(UITheme.Danger.r, UITheme.Danger.g, UITheme.Danger.b, 0.95f);

        int _slot = -1;
        float _age = -1f;
        bool _showing;
        bool _wired;

        /// <summary>보이는 중인지</summary>
        public bool IsShowing => _showing;

        /// <summary>가장 뚫린 칸 (보이는 동안)</summary>
        public int Slot => _slot;

        void Awake()
        {
            Wire();
            if (!_showing) SetVisible(false);
        }

        void Wire()
        {
            if (_wired) return;
            _wired = true;
            if (_ringButton != null) _ringButton.onClick.AddListener(GoToFormation);
            if (_formationLink != null) _formationLink.onClick.AddListener(GoToFormation);
            if (_retryLink != null) _retryLink.onClick.AddListener(RetryNow);
        }

        /// <summary>분석을 보여 준다 (자료가 없으면 아무것도 하지 않는다). guardianName = 그 칸 지킴이 이름 (없으면 '빈 자리')</summary>
        public void Show(DefeatAnalyzer analyzer, string guardianName)
        {
            if (analyzer == null || !analyzer.HasData) return;
            Wire();
            _slot = analyzer.TopSlot;
            if (_segments != null)
            {
                for (int i = 0; i < _segments.Length; i++)
                {
                    if (_segments[i] == null) continue;
                    _segments[i].color = i == _slot ? TopColor : SegmentColor;
                    _segments[i].rectTransform.localScale = Vector3.one;
                }
            }
            if (_percent != null) _percent.SetText("{0}%", analyzer.TopPercent);
            if (_title != null) _title.text = DefeatAnalyzer.Title(_slot);
            if (_slotLine != null) _slotLine.text = string.IsNullOrEmpty(guardianName) ? "빈 자리" : guardianName + " 자리";
            if (_advice != null) _advice.text = DefeatAnalyzer.Advice(analyzer.TopEnemy(_slot));
            _age = 0f;
            _showing = true;
            SetVisible(true);
            if (_group != null) _group.alpha = UIMotion.Reduced ? 1f : 0f;
            UISfx.Play(UISfxKey.DefeatAdvice);
        }

        /// <summary>숨긴다 (다음 전투 시작 · 팝업)</summary>
        public void Hide()
        {
            if (!_showing) return;
            _showing = false;
            _age = -1f;
            SetVisible(false);
        }

        void SetVisible(bool on)
        {
            if (_group != null)
            {
                _group.alpha = on ? _group.alpha : 0f;
                _group.blocksRaycasts = on;
                _group.interactable = on;
            }
            if (gameObject.activeSelf != on) gameObject.SetActive(on);
        }

        void Update()
        {
            if (!_showing) return;
            if (UIManager.Instance != null && UIManager.Instance.HasOpenPopup)
            {
                Hide();
                return;
            }
            float dt = Time.unscaledDeltaTime;
            _age += dt;
            if (_group != null && _group.alpha < 1f) _group.alpha = UIMotion.Reduced ? 1f : Mathf.Clamp01(_age / FadeIn);
            // 가장 뚫린 칸이 한 번 1.06 톡
            if (_segments != null && _slot >= 0 && _slot < _segments.Length && _segments[_slot] != null && _age <= FadeIn + PopTime + dt)
            {
                float k = Mathf.Clamp01((_age - FadeIn) / PopTime);
                float s = UIMotion.Reduced ? 1f : 1f + 0.06f * Mathf.Sin(k * Mathf.PI);
                _segments[_slot].rectTransform.localScale = new Vector3(s, s, 1f);
            }
        }

        void GoToFormation()
        {
            int slot = _slot;
            Hide();
            UINav.GoToTab(MainTab.Formation);
            if (slot >= 0) UINav.PulseFormationSlot(slot, 2f);
        }

        void RetryNow()
        {
            Hide();
            if (Game.IsReady && Game.Battle != null) Game.Battle.SkipEndPause();
        }
    }
}
