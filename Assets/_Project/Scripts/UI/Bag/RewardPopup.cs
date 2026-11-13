using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 공용 보상 팝업 (원정 받기, 잔향이 던전 결과, 상점 묶음 · 의상 구매). 보상 칸이 하나씩 튀어나오며 등장한다.
    ///   · 확인 (M20): 보상이 있으면 닫기 전에 칸 자리에서 상단 바로 날린다 (RewardFlyer — 칩 숫자는 닿을 때 오른다). 처음 한 번만 누를 수 있다.
    ///     실패 결과(success = false)는 날리지 않는다 — 얻은 보상이 있는 실패(던전 도전 실패)는 flyOnFail로 날린다.
    ///   · 칸이 다 나온 뒤 재료 칸은 누르면 재료 정보 (M59 — 재화 · 의상은 누를 수 없음).
    ///   · 보조 버튼 (Wave 2 C-04): actionLabel을 주면 확인 왼쪽에 같은 너비의 보조 버튼 (예: 의상 구매 뒤 '바로 입혀 보기'). 이름 ConfirmButton은 그대로.
    ///   · 붙잡아 둔 재화 (M21 §3.12): 보상은 팝업을 열기 전에 이미 들어와 있다. 확인 때 날릴 단추 · 별조각을 HeldOf로 알려
    ///     상단 바(TopBarView)가 그만큼 덜 보여 준다 — 받자마자 칩이 오르고, 확인 때 내려갔다가 아이콘과 함께 다시 오르는 두 번 셈이 없다.
    ///     확인 · 보조 버튼은 날리기 직전에 비우고(날린 양은 RewardFlyer.PendingOf가 이어받는다), 날리지 않고 닫히면 닫을 때 비운다 (HeldChanged).
    ///   · 창 높이 (Wave 3 carry-over): 칸이 한 줄(≤ 5칸)이면 창을 내용에 맞춰 줄인다 — 본문 = 글 92 + 칸 한 줄 216 + 40 + 버튼 줄 128 (창 648).
    ///     두 줄 이상이면 빌드 높이(860) 그대로 (목록이 스크롤된다). 버튼은 본문 아래에 붙어 있어 폭 · 사이(380/380, 24)는 그대로다.
    /// </summary>
    public class RewardPopup : UIPopup
    {
        /// <summary>한 줄에 들어가는 칸 수 (5칸 격자 — 이하이면 창을 줄인다)</summary>
        internal const int CompactMaxCells = UISpace.Grid5PopupColumns;
        /// <summary>창 머리(제목 136) + 아래 여백 36 = 본문 밖</summary>
        internal const float WindowChrome = 172f;
        /// <summary>본문: 글 자리 92 (부제 80 + 12) · 칸 한 줄 216 (위아래 10 + 196) · 칸 아래 40 · 버튼 줄 128 (버튼 108 + 20)</summary>
        internal const float CompactBody = 92f + 216f + 40f + 128f;

        static double s_heldGold;
        static double s_heldGem;

        /// <summary>붙잡아 둔 재화가 바뀌었다 (열 때 · 확인 · 날리지 않고 닫힘) — 상단 바가 숫자를 다시 맞춘다</summary>
        public static event System.Action HeldChanged;

        /// <summary>열린 보상 팝업이 확인 때 상단 바로 날릴 그 재화의 양 (단추 · 별조각만, 아니면 0)</summary>
        public static double HeldOf(CurrencyType currency) => currency switch
        {
            CurrencyType.Gold => s_heldGold,
            CurrencyType.Gem => s_heldGem,
            _ => 0d,
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetHeld()
        {
            s_heldGold = 0d;
            s_heldGem = 0d;
            HeldChanged = null;
        }

        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _subtitleText;
        [SerializeField] RectTransform _slotContent;
        [SerializeField] RewardSlotView _slotPrefab;
        [SerializeField] GameObject _emptyLabel;
        [SerializeField] Button _confirmButton;
        [Tooltip("제목 뒤 발광 (성공이면 금빛, 실패면 차분한 보라)")]
        [SerializeField] Image _glow;
        [SerializeField] float _revealInterval = 0.07f;
        [Header("보조 버튼 (C-04)")]
        [Tooltip("확인 왼쪽 보조 버튼 (actionLabel이 있을 때만)")]
        [SerializeField] Button _actionButton;
        [SerializeField] TMP_Text _actionLabel;
        [Tooltip("확인 하나일 때 / 둘일 때 너비 · 두 버튼 사이")]
        [SerializeField] float _singleWidth = 400f;
        [SerializeField] float _pairWidth = 380f;
        [SerializeField] float _pairGap = 24f;

        readonly List<RewardData> _rewards = new();
        UIListPool<RewardSlotView> _pool;
        Coroutine _reveal;
        CostumeDef _costume;
        bool _fly;
        bool _confirmed;
        System.Action _onAction;
        /// <summary>빌드된 창 높이 (두 줄 이상일 때 — Awake에서 한 번 읽는다, 0 = 창 없음)</summary>
        float _fullHeight;

        /// <summary>
        /// 칸 수에 맞는 창 높이 (테스트용 순수 함수): 한 줄(≤ 5칸, 0칸 포함)이면 머리 + 본문 한 줄 모양(648), 아니면 빌드 높이.
        /// 빌드 높이가 그보다 작으면 줄이지 않는다
        /// </summary>
        internal static float WindowHeightFor(int cells, float fullHeight)
        {
            if (cells > CompactMaxCells || fullHeight <= 0f) return fullHeight;
            return Mathf.Min(fullHeight, WindowChrome + CompactBody);
        }

        protected override void Awake()
        {
            base.Awake();
            if (_window != null) _fullHeight = _window.sizeDelta.y;
            _pool = new UIListPool<RewardSlotView>(_slotPrefab, _slotContent);
            _confirmButton.onClick.AddListener(Confirm);
            if (_actionButton != null) _actionButton.onClick.AddListener(RunAction);
            // 빌드 자리는 '나란히' 모양 (확인 · 보조가 겹치지 않게) — Show 없이 열려도 확인 하나 가운데 모양으로 시작한다
            LayoutButtons(null);
        }

        /// <param name="costume">의상 구매처럼 의상을 얻었으면 첫 칸에 보여준다</param>
        /// <param name="success">false면 실패 결과 (발광을 차분하게, 확인 때 날리지 않는다 — flyOnFail 참고)</param>
        /// <param name="actionLabel">확인 옆 보조 버튼 글 (예: 의상 구매 뒤 '바로 입혀 보기' — 확인과 같은 너비, 보조가 왼쪽). null이면 확인 하나뿐.</param>
        /// <param name="onAction">보조 버튼을 누르면 팝업을 닫고 부른다 (부르는 쪽이 다시 Close해도 괜찮다).</param>
        /// <param name="flyOnFail">실패 결과라도 얻은 보상이 있으면 확인 때 날린다 (던전 도전 실패)</param>
        public void Show(string title, string subtitle, IReadOnlyList<RewardData> rewards, CostumeDef costume = null, bool success = true,
            string actionLabel = null, System.Action onAction = null, bool flyOnFail = false)
        {
            _rewards.Clear();
            if (rewards != null) _rewards.AddRange(RewardService.Merge(rewards));
            _costume = costume;
            _fly = success || flyOnFail;
            _confirmed = false;
            _onAction = string.IsNullOrEmpty(actionLabel) ? null : onAction;

            Open();
            _confirmButton.interactable = true;
            _titleText.text = title;
            _subtitleText.text = subtitle;
            _subtitleText.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            if (_glow != null) _glow.color = success ? new Color(UITheme.Lamp.r, UITheme.Lamp.g, UITheme.Lamp.b, 0.55f) : new Color(UITheme.Lavender.r, UITheme.Lavender.g, UITheme.Lavender.b, 0.25f);
            LayoutButtons(_onAction != null ? actionLabel : null);

            Fill();
            MenuFx.Restart(this, ref _reveal, Reveal());
            HoldCurrencies();
        }

        protected override void OnClose()
        {
            if (_reveal != null) StopCoroutine(_reveal);
            _reveal = null;
            _costume = null;
            _onAction = null;
            // 날리지 않고 닫혔다 (뒤로가기 · 덮개 등): 붙잡아 둔 양을 놓아 상단 바가 바로 세어 오르게
            if (ReleaseHeld()) HeldChanged?.Invoke();
        }

        protected override void OnDisable()
        {
            base.OnDisable();
            // 닫기 처리 없이 꺼지는 길(UIManager.CloseAllImmediate 등)에서도 붙잡은 채로 남지 않게
            if (ReleaseHeld()) HeldChanged?.Invoke();
        }

        /// <summary>확인: 처음 한 번만 — 보상을 날리고 닫는다</summary>
        void Confirm()
        {
            if (_confirmed) return;
            _confirmed = true;
            _confirmButton.interactable = false;
            FlyRewards();
            Close();
        }

        /// <summary>보조 버튼: 보상은 확인과 같이 날리고, 닫은 뒤 동작을 부른다</summary>
        void RunAction()
        {
            if (_confirmed) return;
            _confirmed = true;
            var action = _onAction;
            FlyRewards();
            Close();
            action?.Invoke();
        }

        /// <summary>
        /// 붙잡은 양을 먼저 비우고 날린다 — 날린 양은 RewardFlyer.PendingOf로 넘어가 상단 바 숫자는 그대로다.
        /// 날리지 못했으면(날리기 층 없음) 비운 것을 알려 상단 바가 바로 세어 오른다.
        /// </summary>
        void FlyRewards()
        {
            bool held = ReleaseHeld();
            if (_fly && _rewards.Count > 0) RewardFlyer.Fly(_rewards, _slotContent);
            if (held) HeldChanged?.Invoke();
        }

        /// <summary>확인 때 날릴 단추 · 별조각을 붙잡는다 (날리지 않는 실패 결과는 붙잡지 않는다)</summary>
        void HoldCurrencies()
        {
            double gold = 0d, gem = 0d;
            if (_fly)
            {
                for (int i = 0; i < _rewards.Count; i++)
                {
                    var reward = _rewards[i];
                    if (reward == null || reward.type != RewardType.Currency || !(reward.amount > 0)) continue;
                    if (reward.currency == CurrencyType.Gold) gold += reward.amount;
                    else if (reward.currency == CurrencyType.Gem) gem += reward.amount;
                }
            }
            if (gold == s_heldGold && gem == s_heldGem) return;
            s_heldGold = gold;
            s_heldGem = gem;
            HeldChanged?.Invoke();
        }

        /// <summary>붙잡은 양을 비운다 (알리지 않는다). 무엇이든 붙잡고 있었으면 true</summary>
        static bool ReleaseHeld()
        {
            bool any = s_heldGold != 0d || s_heldGem != 0d;
            s_heldGold = 0d;
            s_heldGem = 0d;
            return any;
        }

        /// <summary>확인 하나 = 가운데 400, 둘 = 같은 너비 380 두 개 (보조 왼쪽, 사이 24 — 확인은 오른쪽)</summary>
        void LayoutButtons(string actionLabel)
        {
            var confirm = (RectTransform)_confirmButton.transform;
            bool pair = actionLabel != null && _actionButton != null;
            if (_actionButton != null)
            {
                _actionButton.gameObject.SetActive(pair);
                _actionButton.interactable = true;
                if (pair && _actionLabel != null) _actionLabel.text = actionLabel;
            }
            float width = pair ? _pairWidth : _singleWidth;
            float offset = pair ? (_pairWidth + _pairGap) * 0.5f : 0f;
            confirm.sizeDelta = new Vector2(width, confirm.sizeDelta.y);
            confirm.anchoredPosition = new Vector2(offset, confirm.anchoredPosition.y);
            if (!pair) return;
            var action = (RectTransform)_actionButton.transform;
            action.sizeDelta = new Vector2(width, confirm.sizeDelta.y);
            action.anchoredPosition = new Vector2(-offset, confirm.anchoredPosition.y);
        }

        void Fill()
        {
            int offset = _costume != null ? 1 : 0;
            _pool.SetCount(_rewards.Count + offset);
            if (_costume != null) _pool[0].SetCostume(_costume, CostumeGrade(_costume));
            for (int i = 0; i < _rewards.Count; i++) _pool[i + offset].Set(_rewards[i]);
            for (int i = 0; i < _pool.Count; i++)
            {
                _pool[i].SetClick(null);
                _pool[i].transform.localScale = Vector3.zero;
            }
            _emptyLabel.SetActive(_pool.Count == 0);
            FitWindow(_pool.Count);
        }

        /// <summary>칸이 한 줄이면 창을 내용 높이로 (가운데 기준이라 위아래가 같이 줄어든다). 바뀔 때만 쓴다</summary>
        void FitWindow(int cells)
        {
            if (_window == null || _fullHeight <= 0f) return;
            float height = WindowHeightFor(cells, _fullHeight);
            var size = _window.sizeDelta;
            if (!Mathf.Approximately(size.y, height)) _window.sizeDelta = new Vector2(size.x, height);
        }

        IEnumerator Reveal()
        {
            // 레이아웃이 자리를 잡은 뒤 하나씩 튀어나오게
            yield return null;
            for (int i = 0; i < _pool.Count; i++)
            {
                StartCoroutine(MenuFx.PopIn(_pool[i].transform, 0.26f));
                yield return new WaitForSecondsRealtime(_revealInterval);
            }
            // 다 나온 뒤에야 누를 수 있다: 재료 칸 = 재료 정보 (재화 · 의상은 그대로 못 누름, M59)
            for (int i = 0; i < _pool.Count; i++) _pool[i].SetItemInfoClick();
            _reveal = null;
        }

        static ItemGrade CostumeGrade(CostumeDef costume)
        {
            var lineage = Game.Characters.Db.GetLineageOfCostume(costume.id);
            return lineage != null ? lineage.grade : ItemGrade.Epic;
        }
    }
}
