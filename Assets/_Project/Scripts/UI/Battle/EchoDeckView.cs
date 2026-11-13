using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 하단 잔향이 덱 띠 (5칸 + 잠긴 1칸).
    /// ① 탭: 수선점에서 형태가 잡혀 가장 위험한 방향으로 자동 이동 (TrySummon(slot, null))
    /// ② 홀드 → 드래그: 부채꼴 미리보기로 방향 지정 → 전장 위에서 놓으면 그 방향으로 소환, 덱 위에서 놓으면 취소 (잔향 소모 없음)
    /// <para>
    /// M36 (UX-SPEC §2.1.11):
    ///   · 빈칸: 넣을 수 있는 잔향이(해금된 형태 중 CanPlace(Echo, 칸, 형태) == Ok)가 있으면 카드의 '+'가 숨쉬고, 누르면 '이 칸에 넣을 잔향이가 있어요' + 편성 탭.
    ///     없으면 '+'가 흐리게 멈추고, 누르면 '새 친구를 만나면 잔향이를 더 넣을 수 있어요'만 (이동 없음 — 소환 탭이 열렸으면 손가락으로 소환 탭을 가리킨다).
    ///     편성 · 친구가 바뀔 때만 다시 잰다 (최대 0.5초에 한 번). 튜토리얼 단계 중에는 빈칸을 눌러도 토스트 · 이동이 없다.
    ///   · 잠긴 칸: 토스트는 문장 FormationSystem.EchoSlotUnlockSentence ('스테이지 2-9를 클리어하면 열려요').
    ///   · 덱 이름표(DeckTab): 잔향이를 10번 넘게 불렀고(CounterKey.EchoSummon) 끌어서 방향 소환에 성공해 봤으면(MetaFlags.DeckDragHint) 안내
    ///     '탭 = 자동 소환 · 끌기 = 방향 지정'이 0.4초에 걷히고 '잔향이 N/M'(채운 칸 / 열린 칸)으로 바뀐다 — 그때부터 이름표(160x48)를 누르면 편성 탭.
    ///     끌어서 방향 소환에 성공하면 이 덱이 MetaFlags.DeckDragHint와 MetaFlags.DirectionalSummon을 세운다 (성공을 아는 곳이 여기라서).
    /// </para>
    /// <para>
    /// 시트가 올라와 SummonAimView가 조준을 거두면 들린 카드도 바로 내려놓는다 (손을 뗄 때까지 들린 채 남아 있다가 '취소했어요'가 뜨지 않게 — IA 전투 편차 4).
    /// </para>
    /// </summary>
    public class EchoDeckView : MonoBehaviour
    {
        [SerializeField] EchoCardView[] _cards;
        [SerializeField] SummonAimView _aim;

        [Header("덱 이름표 (M36)")]
        [Tooltip("Strip/DeckTab 알약 (안내 → 잔향이 N/M)")]
        [SerializeField] RectTransform _tab;
        [SerializeField] CanvasGroup _tabGroup;
        [SerializeField] TMP_Text _tabLabel;
        [Tooltip("이름표 누르기 (잔향이 N/M 때만 — 편성 탭)")]
        [SerializeField] Button _tabButton;
        [SerializeField] Vector2 _tabHintSize = new(440f, 32f);
        [SerializeField] Vector2 _tabCountSize = new(160f, 32f);

        const string CancelMessage = "소환을 취소했어요. 잔향은 그대로예요.";
        const string CandidateMessage = "이 칸에 넣을 잔향이가 있어요";
        const string NoCandidateMessage = "새 친구를 만나면 잔향이를 더 넣을 수 있어요";
        const string TabHintText = "탭 = 자동 소환 · 끌기 = 방향 지정";
        /// <summary>안내 줄을 거두는 조건: 잔향이를 이만큼 불렀고 끌어서 방향 소환을 해 봄</summary>
        internal const int HintSummonCount = 10;
        /// <summary>안내 줄이 걷히는 시간 / 잔향이 N/M이 나타나는 시간</summary>
        const float TabFadeOut = 0.4f;
        const float TabFadeIn = 0.2f;
        /// <summary>빈칸 후보 · 안내 조건을 다시 재는 최소 간격 (초) — 강화 길게 누르기 중 친구 변경이 쏟아져도 프레임마다 재지 않게</summary>
        const float RecheckInterval = 0.5f;

        BattleSimulator _sim;
        /// <summary>방향 지정 중인 카드 (조준 상태의 유일한 기준, 카드의 조준 표시는 이 값을 따른다)</summary>
        int _aimIndex = -1;

        readonly bool[] _candidates = new bool[FormationSystem.EchoSlotCount];
        bool _candidatesDirty = true;
        float _nextCandidateCheck;
        bool _listening;
        FormationSystem _formation;
        CharacterSystem _characters;

        enum TabMode
        {
            Unknown,
            Hint,
            FadingOut,
            Count,
        }

        TabMode _tabMode = TabMode.Unknown;
        float _tabTime;
        float _nextHintCheck;
        int _tabFilled = -1;
        int _tabUnlocked = -1;

        void Awake()
        {
            if (_tabButton != null) _tabButton.onClick.AddListener(OnTabClicked);
        }

        void OnDestroy()
        {
            if (!_listening) return;
            if (_formation != null) _formation.Changed -= MarkCandidates;
            if (_characters != null) _characters.Changed -= MarkCandidates;
            _listening = false;
        }

        public void Bind(BattleSimulator sim)
        {
            _sim = sim;
            // 새 전투(스테이지 전환·편성 변경)가 시작되면 진행 중이던 방향 지정은 취소하고 알려 준다
            StopAim(true);
            _candidatesDirty = true;
        }

        public void Tick(BattleSimulator sim, float dt)
        {
            _sim = sim;
            Listen();
            int unlocked = Game.IsReady && Game.Formation != null ? Game.Formation.UnlockedEchoSlots : _cards.Length;
            float resonance = sim != null ? sim.Resonance : 0f;
            float now = Time.unscaledTime;
            if (_candidatesDirty && now >= _nextCandidateCheck) RefreshCandidates(unlocked, now);
            int filled = 0;
            for (int i = 0; i < _cards.Length; i++)
            {
                var slot = GetSlot(i);
                if (slot != null && i < unlocked) filled++;
                _cards[i].Refresh(slot, i >= unlocked, resonance, dt);
            }
            // 시트가 올라와 미리보기가 조준을 거뒀으면 들린 카드도 바로 내려놓는다 (조용히 — 취소 알림 없음)
            if (_aimIndex >= 0 && _aim != null && !_aim.IsActive) StopAim(false);
            if (_aimIndex >= 0) RefreshAim(resonance);
            TickTab(filled, Mathf.Min(unlocked, _cards.Length), now, Time.unscaledDeltaTime);
        }

        /// <summary>방향 지정 중인 슬롯의 잔향/재소환 대기 상태를 미리보기에 알려 준다 (자동 소환이 잔향을 써도 바로 반영)</summary>
        void RefreshAim(float resonance)
        {
            var slot = GetSlot(_aimIndex);
            if (slot == null || IsLocked(_aimIndex))
            {
                StopAim(true);
                return;
            }
            _aim.SetState(IsAffordable(slot, resonance), IsReady(slot));
        }

        /// <summary>소환 성공 (수동/자동 모두) — 해당 카드가 톡 튄다</summary>
        public void OnSummoned(int deckSlot)
        {
            if (deckSlot >= 0 && deckSlot < _cards.Length) _cards[deckSlot].Pop();
        }

        // ───────────── 빈칸 후보 (M36) ─────────────

        /// <summary>편성 · 친구 변화를 듣는다 (게임이 준비된 뒤 한 번)</summary>
        void Listen()
        {
            if (_listening || !Game.IsReady) return;
            _formation = Game.Formation;
            _characters = Game.Characters;
            if (_formation != null) _formation.Changed += MarkCandidates;
            if (_characters != null) _characters.Changed += MarkCandidates;
            _listening = true;
            _candidatesDirty = true;
        }

        void MarkCandidates() => _candidatesDirty = true;

        /// <summary>빈칸마다 넣을 수 있는 잔향이가 있는지 다시 잰다 (바뀔 때만, 최대 RecheckInterval에 한 번 — 형태 목록은 그때 한 번 만든다)</summary>
        void RefreshCandidates(int unlocked, float now)
        {
            _candidatesDirty = false;
            _nextCandidateCheck = now + RecheckInterval;
            var formation = Game.IsReady ? Game.Formation : null;
            List<CharacterFormDef> forms = null;
            for (int i = 0; i < _cards.Length; i++)
            {
                bool candidate = false;
                if (formation != null && i < unlocked && i < _candidates.Length && string.IsNullOrEmpty(formation.GetEcho(i)))
                {
                    forms ??= Game.Characters != null ? Game.Characters.GetUnlockedForms() : null;
                    candidate = HasCandidate(formation, i, forms);
                }
                if (i < _candidates.Length) _candidates[i] = candidate;
                _cards[i].SetCandidate(candidate);
            }
        }

        /// <summary>해금된 형태 중 이 잔향이 칸에 놓을 수 있는 것이 하나라도 있는지</summary>
        static bool HasCandidate(FormationSystem formation, int slot, List<CharacterFormDef> forms)
        {
            if (forms == null) return false;
            for (int f = 0; f < forms.Count; f++)
            {
                var form = forms[f];
                if (form != null && formation.CanPlace(CharacterRole.Echo, slot, form.id) == PlaceResult.Ok) return true;
            }
            return false;
        }

        // ───────────── 덱 이름표 (M36) ─────────────

        /// <summary>안내 줄을 거둘 때가 됐는지: 잔향이를 HintSummonCount번 이상 불렀고 끌어서 방향 소환을 해 봄</summary>
        internal static bool ShouldRetireHint(long summons, bool dragged) => summons >= HintSummonCount && dragged;

        static bool HintRetired()
        {
            var progress = Game.IsReady ? Game.Progress : null;
            return progress != null && ShouldRetireHint(progress.GetLifetime(CounterKey.EchoSummon), progress.HasFlag(MetaFlags.DeckDragHint));
        }

        void TickTab(int filled, int unlocked, float now, float dt)
        {
            if (_tab == null || _tabLabel == null) return;
            switch (_tabMode)
            {
                case TabMode.Unknown:
                    if (!Game.IsReady) return;
                    // 처음: 이미 거둘 때가 지났으면 바로 잔향이 N/M (걷히는 연출 없이)
                    if (HintRetired()) EnterCount(filled, unlocked, true);
                    else EnterHint();
                    return;
                case TabMode.Hint:
                    if (now < _nextHintCheck) return;
                    _nextHintCheck = now + RecheckInterval;
                    if (!HintRetired()) return;
                    if (UIMotion.Reduced)
                    {
                        EnterCount(filled, unlocked, true);
                        return;
                    }
                    _tabMode = TabMode.FadingOut;
                    _tabTime = 0f;
                    return;
                case TabMode.FadingOut:
                    _tabTime += dt;
                    SetTabAlpha(1f - Mathf.Clamp01(_tabTime / TabFadeOut));
                    if (_tabTime >= TabFadeOut) EnterCount(filled, unlocked, false);
                    return;
                default:
                    if (_tabTime < TabFadeIn)
                    {
                        _tabTime += dt;
                        SetTabAlpha(Mathf.Clamp01(_tabTime / TabFadeIn));
                    }
                    WriteCount(filled, unlocked);
                    return;
            }
        }

        void EnterHint()
        {
            _tabMode = TabMode.Hint;
            _tab.sizeDelta = _tabHintSize;
            _tabLabel.text = TabHintText;
            // 빌더가 자동 크기(하한 16)를 켜 두므로 최대 크기로 정한다
            _tabLabel.fontSizeMax = UIType.Hint;
            _tabLabel.fontSize = UIType.Hint;
            _tabLabel.color = UITheme.Cream;
            SetTabAlpha(1f);
            if (_tabButton != null) _tabButton.interactable = false;
        }

        void EnterCount(int filled, int unlocked, bool snap)
        {
            _tabMode = TabMode.Count;
            _tabTime = snap ? TabFadeIn : 0f;
            _tab.sizeDelta = _tabCountSize;
            _tabLabel.fontSizeMax = UIType.Micro;
            _tabLabel.fontSize = UIType.Micro;
            _tabLabel.color = UITheme.SubText;
            _tabFilled = -1;
            WriteCount(filled, unlocked);
            SetTabAlpha(snap ? 1f : 0f);
            if (_tabButton != null) _tabButton.interactable = true;
        }

        /// <summary>'잔향이 N/M' (바뀔 때만 — SetText 서식은 할당 없음)</summary>
        void WriteCount(int filled, int unlocked)
        {
            if (filled == _tabFilled && unlocked == _tabUnlocked) return;
            _tabFilled = filled;
            _tabUnlocked = unlocked;
            _tabLabel.SetText("잔향이 {0}/{1}", filled, unlocked);
        }

        void SetTabAlpha(float alpha)
        {
            if (_tabGroup != null) _tabGroup.alpha = alpha;
        }

        void OnTabClicked()
        {
            if (_tabMode != TabMode.Count || FtueActive) return;
            UINav.GoToTab(MainTab.Formation);
        }

        // ───────────── 카드 입력 ─────────────

        public void Tap(int index)
        {
            if (IsLocked(index))
            {
                Toast(Game.IsReady && Game.Formation != null ? Game.Formation.EchoSlotUnlockSentence : "아직 잠겨 있는 칸이에요.");
                return;
            }
            if (GetSlot(index) == null)
            {
                TapEmpty(index);
                return;
            }
            Summon(index, null);
        }

        /// <summary>빈칸 누르기 (M36): 넣을 잔향이가 있으면 편성 탭으로, 없으면 안내만 (소환 탭이 열렸으면 손가락으로 가리킨다). 튜토리얼 중에는 조용히</summary>
        void TapEmpty(int index)
        {
            if (FtueActive) return;
            bool candidate = index >= 0 && index < _candidates.Length && _candidates[index];
            if (candidate)
            {
                Toast(CandidateMessage);
                UINav.GoToTab(MainTab.Formation);
                return;
            }
            Toast(NoCandidateMessage);
            if (Game.IsReady && Game.Features != null && Game.Features.IsUnlocked(FeatureId.Gacha)) UINav.FocusTarget(CoachTargets.TabGacha);
        }

        static bool FtueActive => Game.IsReady && Game.Tutorial != null && Game.Tutorial.IsActive;

        public bool CanAim(int index) => _sim != null && !IsLocked(index) && GetSlot(index) != null && _aimIndex < 0;

        /// <summary>방향 지정 시작. 시작하지 못하면 false (카드는 조준 상태로 들어가지 않는다)</summary>
        public bool BeginAim(int index, Vector2 screen, Camera eventCamera)
        {
            var slot = GetSlot(index);
            if (slot == null || _aim == null || _aimIndex >= 0) return false;
            _aimIndex = index;
            float startAngle = _sim != null ? _sim.GetDangerAngle() : Mathf.PI * 0.5f;
            _aim.Begin(slot.FormId, slot.LineageId, IsAffordable(slot, _sim.Resonance), IsReady(slot), startAngle);
            _aim.Move(screen, eventCamera);
            return true;
        }

        public void UpdateAim(Vector2 screen, Camera eventCamera)
        {
            if (_aimIndex >= 0) _aim.Move(screen, eventCamera);
        }

        public void EndAim(int index, Vector2 screen, Camera eventCamera)
        {
            if (_aimIndex != index)
            {
                // 이미 취소된 조준 (카드는 AbortAim으로 알림을 받으므로 보통 오지 않는다)
                Toast(CancelMessage);
                return;
            }
            _aimIndex = -1;
            if (_aim.End(screen, eventCamera, out float angle))
            {
                // 끌어서 방향 소환에 성공: 덱 안내 줄을 거둘 조건 하나 + 가이드 '원하는 방향으로 보내기'
                if (Summon(index, angle) && Game.IsReady && Game.Progress != null)
                {
                    Game.Progress.SetFlag(MetaFlags.DeckDragHint);
                    Game.Progress.SetFlag(MetaFlags.DirectionalSummon);
                }
            }
            else Toast(CancelMessage);
        }

        /// <summary>조용히 조준 취소 (카드가 꺼질 때 등)</summary>
        public void CancelAim() => StopAim(false);

        /// <summary>
        /// 창 포커스를 잃으면 방향 지정을 조용히 거둔다 (M01): 알트탭 중에는 EventSystem이 떼기를 놓쳐 미리보기가 남을 수 있다.
        /// 카드도 AbortAim으로 들림·드래그 상태를 푼다. 전투는 그대로 진행된다
        /// </summary>
        void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelAim();
        }

        /// <summary>조준 취소: 미리보기를 거두고 조준 중이던 카드에도 알려 들림/드래그 상태를 함께 푼다</summary>
        void StopAim(bool notify)
        {
            int index = _aimIndex;
            _aimIndex = -1;
            if (_aim != null) _aim.Cancel();
            if (index < 0) return;
            if (index < _cards.Length && _cards[index] != null) _cards[index].AbortAim();
            if (notify) Toast(CancelMessage);
        }

        // ───────────── 소환 ─────────────

        /// <summary>소환을 시도한다. 성공하면 true (실패면 카드가 흔들리고 까닭을 토스트로)</summary>
        bool Summon(int index, float? angle)
        {
            var slot = GetSlot(index);
            if (_sim == null || slot == null || !Game.IsReady) return false;

            string reason = null;
            if (!IsReady(slot)) reason = "다시 부를 준비를 하고 있어요.";
            else if (!IsAffordable(slot, _sim.Resonance)) reason = "잔향이 부족해요";
            else if (!Game.Battle.TrySummon(index, angle))
            {
                reason = Game.Battle.IsBetweenStages ? "다음 전투를 준비하고 있어요." : "지금은 잔향이를 부를 수 없어요.";
            }

            if (reason == null) return true;
            _cards[index].Deny();
            Toast(reason);
            return false;
        }

        DeckSlotState GetSlot(int index)
        {
            if (_sim == null) return null;
            var deck = _sim.Deck;
            if (deck == null || index < 0 || index >= deck.Count) return null;
            var slot = deck[index];
            return slot != null && !string.IsNullOrEmpty(slot.FormId) ? slot : null;
        }

        static bool IsAffordable(DeckSlotState slot, float resonance) => resonance + 0.001f >= slot.Cost;

        /// <summary>재소환 대기(쿨다운)가 끝났는지</summary>
        static bool IsReady(DeckSlotState slot) => slot.Cooldown <= 0.01f;

        bool IsLocked(int index)
        {
            int unlocked = Game.IsReady && Game.Formation != null ? Game.Formation.UnlockedEchoSlots : _cards.Length;
            return index >= unlocked;
        }

        static void Toast(string message)
        {
            if (UIManager.Instance != null) UIManager.Instance.ShowToast(message);
        }
    }
}
