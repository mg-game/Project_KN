using TMPro;
using UnityEngine;

namespace IdleGame.UI
{
    /// <summary>
    /// 튜토리얼 단계 → 손가락을 실제로 댈 화면 대상. 콘텐츠의 coachTarget(보통 탭 버튼)을 기본으로 하고,
    /// 그 탭 화면이 이미 열려 있으면 완료 조건에 맞는 안쪽 버튼으로 손가락을 옮긴다 (예: 강화 탭 → "강화 x1" 버튼).
    /// 팝업이 열려 있으면 맨 위 팝업 안에서만 찾는다 (예: 바구니 팝업의 받기 버튼). 못 찾으면 null → 코치마크를 잠깐 숨긴다.
    /// 맨 위 팝업은 UIManager.TopPopup이다 (WP8-COACH-TOPPOPUP): 열린(IsOpen) 팝업만, 위 층(SystemPopups — 확인 · 부족 창)이 먼저 —
    /// 닫히는 연출 중(0.12초 동안 켜져 있음)인 팝업을 가리키거나, 열린 확인 창 뒤 버튼을 가리키지 않는다.
    /// 다른 화면이 만든 이름(빌더 이름)에 기대므로 이름이 바뀌면 여기만 고친다. 이름 비교는 문자열을 만들기 때문에
    /// FtueDirector가 화면이 바뀔 때(탭 전환·팝업 열고 닫기·시트 열림/닫힘·단계 시작)와 1초마다만 부른다.
    /// <para>
    /// 화면 틀 (UX-SPEC-IA §5.7): 탭 화면은 시트 안에 있다. 시트 목표 상태(UIFrame.SheetOpen)를 기준으로 본다 —
    /// 닫기를 누른 그 프레임부터(패널은 다 내려간 뒤에 꺼진다) 패널 안 대상 대신 그 탭 버튼, 열기를 누른 그 프레임부터 안쪽 버튼 (올라오는 동안 손가락이 따라간다).
    /// 마지막으로 고른 대상(끌어 놓을 곳 포함)이 열린 시트 밑에 가리거나(UIFrame.IsUnderSheet — 덱 · 램프 등 전투 아래쪽)
    /// 시트가 열리면 사라지는 HUD(SheetFade 0 — 빠른 아이콘 줄 등) 안이면 사냥 버튼(CoachTargets.TabMain)으로 바꾸고
    /// RedirectedToMain을 켠다. 사냥을 누르면 MAIN이 되고, 그때 다시 찾으면 원래 대상이 나온다.
    /// </para>
    /// </summary>
    public sealed class CoachTargetResolver
    {
        static readonly string[] GuardianSlotNames =
        {
            "GuardianSlot_0", "GuardianSlot_1", "GuardianSlot_2", "GuardianSlot_3", "GuardianSlot_4",
        };

        readonly RectTransform _canvas;
        readonly RectTransform _popupRoot;

        /// <param name="canvas">Canvas 루트 (CoachTargets 경로 기준)</param>
        /// <param name="popupRoot">팝업 루트 (맨 위 팝업 찾기)</param>
        public CoachTargetResolver(RectTransform canvas, RectTransform popupRoot)
        {
            _canvas = canvas;
            _popupRoot = popupRoot;
        }

        /// <summary>첫 만남 카드의 [문 열어 주기] 버튼 (FtueDirector가 넣는다)</summary>
        public RectTransform FirstMeetingButton { get; set; }

        /// <summary>FTUE에서 처음 만나는 친구 (편성 끌어 놓기 출처 카드 찾기)</summary>
        public string FirstMeetingLineage { get; set; }

        /// <summary>
        /// 마지막 Resolve가 시트 때문에 안 보이는 대상(UIFrame.IsUnderSheet · 사라지는 HUD)을 사냥 버튼(CoachTargets.TabMain)으로 바꿨는지 (UX-SPEC-IA §5.7).
        /// Resolve를 시작할 때마다 지운다 — FtueDirector가 단계 시작의 첫 Resolve 뒤에 보고 한 번 UINav.GoToMain().
        /// 사냥 버튼을 찾지 못해도(옛 6칸 탭 바) 켠다 — 대상이 가렸다는 뜻이고, 그때 돌려주는 값은 null (코치마크를 잠깐 숨긴다).
        /// </summary>
        public bool RedirectedToMain { get; private set; }

        /// <summary>
        /// 대상이 맨 위에 열린 팝업 안에 있는지 (팝업이 열려 있어도 코치마크를 보여도 되는지). 그 팝업 위에 확인 창 같은 다른 팝업이
        /// 열려 있으면 false — 손가락 · 어둠이 위 창 밑 버튼을 가리키지 않게 (코치마크 층은 SystemPopups 아래라 위 창에 가린다).
        /// 열린 팝업이 없으면 팝업 층 아래인지만 본다. 매 프레임 불러도 된다 (할당 없음).
        /// </summary>
        public bool IsInsidePopup(RectTransform target)
        {
            if (target == null) return false;
            var top = TopPopup();
            if (top != null) return target.IsChildOf(top);
            return _popupRoot != null && target.IsChildOf(_popupRoot);
        }

        /// <summary>
        /// 손가락 대상. 끌어 놓기 단계면 dragTo도 채운다. popupOpen이면 맨 위 팝업 안에서만 찾는다.
        /// 대상(또는 dragTo)이 시트 때문에 안 보이면(HiddenBySheet) 사냥 버튼을 돌려주고(dragTo는 비운다) RedirectedToMain을 켠다.
        /// </summary>
        public RectTransform Resolve(FtueStepDef step, bool popupOpen, out RectTransform dragTo)
        {
            RedirectedToMain = false;
            var target = ResolveCore(step, popupOpen, out dragTo);
            if (target == null || (!HiddenBySheet(target) && (dragTo == null || !HiddenBySheet(dragTo)))) return target;

            // 시트 밑(덱 · 램프 등)은 누를 수 없다 — 손가락은 사냥 버튼으로 (누르면 MAIN, 다시 찾으면 원래 대상)
            RedirectedToMain = true;
            dragTo = null;
            return UIFind.Path(_canvas, CoachTargets.TabMain);
        }

        /// <summary>
        /// 시트가 열려(목표 상태) 대상을 가리킬 수 없다: 열린 시트 밑(UIFrame.IsUnderSheet — 덱 · 램프 · 아래쪽 빠른 아이콘)이거나,
        /// 시트가 열리면 사라지는 HUD(가장 가까운 SheetFade의 다 열린 알파 &lt; 0.05 — 빠른 아이콘 줄 · 권장 전투력 · 시너지 줄) 안이다.
        /// 지금 알파가 아니라 다 열린 알파를 봐서 IsUnderSheet처럼 열기를 부른 그 프레임에 바뀐다 (MetaNavigator.FeatureSlot과 같은 규칙).
        /// 탭 바 · 상단 바 · 팝업 · 시트 안 대상은 false. Resolve마다 한 번 (GetComponentInParent).
        /// </summary>
        static bool HiddenBySheet(RectTransform target)
        {
            if (!UIFrame.SheetOpen) return false;
            if (UIFrame.IsUnderSheet(target)) return true;
            var stage = UIFrame.StageRect;
            if (stage == null || !target.IsChildOf(stage)) return false;
            var fade = target.GetComponentInParent<SheetFade>(true);
            return fade != null && fade.SheetAlpha < SheetFade.HiddenBelow;
        }

        RectTransform ResolveCore(FtueStepDef step, bool popupOpen, out RectTransform dragTo)
        {
            dragTo = null;
            if (step == null) return null;
            var condition = step.complete;

            if (popupOpen) return ResolveInPopup(condition);

            var primary = UIFind.Path(_canvas, step.coachTarget);
            var panelPath = PanelFor(condition);
            if (panelPath == null) return primary;

            var panel = UIFind.Path(_canvas, panelPath);
            if (panel == null || !panel.gameObject.activeInHierarchy || InClosingSheet(panel))
            {
                // 탭 화면이 아직 안 열렸거나 닫히는 중이면 그 탭 버튼 (콘텐츠에 대상이 없을 때도)
                return primary != null && UIFind.IsShown(primary) && !InClosingSheet(primary)
                    ? primary
                    : UIFind.Path(_canvas, TabFor(panelPath));
            }

            var deep = ResolveInPanel(condition, panel, out dragTo);
            return deep != null ? deep : primary != null && !IsTabButton(step.coachTarget) ? primary : null;
        }

        /// <summary>
        /// 대상이 닫히는 시트 안에 있다: 목표는 닫힘(UIFrame.SheetOpen false)인데 아직 보인다(SheetProgress &gt; 0).
        /// 패널은 시트가 다 내려간 뒤에 꺼지므로, 그 사이 안쪽 버튼을 가리키지 않게 한다. 시트가 없는 장면(틀을 잇지 않음)에서는 늘 false.
        /// </summary>
        static bool InClosingSheet(RectTransform target)
        {
            var sheet = UIFrame.SheetRect;
            return sheet != null && !UIFrame.SheetOpen && UIFrame.SheetProgress > 0f && target.IsChildOf(sheet);
        }

        RectTransform ResolveInPopup(MetaCondition condition)
        {
            var top = TopPopup();
            if (top == null || condition == null) return null;
            if (condition.type != MetaConditionType.Counter && condition.type != MetaConditionType.DailyCounter) return null;
            switch (condition.counter)
            {
                case CounterKey.ChestClaim:
                    return UIFind.Deep(top, "ClaimButton");
                case CounterKey.QuickIdle:
                {
                    var quick = UIFind.Deep(top, "QuickButton");
                    return quick != null ? quick : UIFind.DeepContains(top, "Quick");
                }
                case CounterKey.ExpeditionStart:
                    return UIFind.Deep(top, "StartButton");
                case CounterKey.ExpeditionClaim:
                    return UIFind.Deep(top, "ClaimButton");
                default:
                    return null;
            }
        }

        /// <summary>
        /// 맨 위에 열린 팝업: UIManager.TopPopup (열린 팝업 스택 — 닫히는 중인 팝업은 빠져 있고, 위 층 SystemPopups가 먼저).
        /// UIManager가 없으면(도구 · 테스트) 팝업 층의 마지막 켜진 자식 중 UIPopup이 열려 있는 것 (열면 SetAsLastSibling). 할당 없음.
        /// </summary>
        RectTransform TopPopup()
        {
            var ui = UIManager.Instance;
            if (ui != null)
            {
                var top = ui.TopPopup;
                return top != null ? top.transform as RectTransform : null;
            }
            if (_popupRoot == null) return null;
            for (int i = _popupRoot.childCount - 1; i >= 0; i--)
            {
                var child = _popupRoot.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                // 닫히는 연출 중(IsOpen false인데 아직 켜져 있음)인 팝업은 건너뛴다
                if (child.TryGetComponent<UIPopup>(out var popup) && !popup.IsOpen) continue;
                return child as RectTransform;
            }
            return null;
        }

        static string PanelFor(MetaCondition condition)
        {
            if (condition == null) return null;
            switch (condition.type)
            {
                case MetaConditionType.ShopLevel:
                case MetaConditionType.LineageLevel:
                case MetaConditionType.LineageLevelSum:
                    return CoachTargets.GrowthPanel;
                case MetaConditionType.GuardiansPlaced:
                case MetaConditionType.ActiveSynergies:
                    return CoachTargets.FormationPanel;
                case MetaConditionType.Flag when condition.targetId == MetaFlags.SynergyDetailViewed:
                    return CoachTargets.FormationPanel;
                case MetaConditionType.Counter:
                case MetaConditionType.DailyCounter:
                    switch (condition.counter)
                    {
                        case CounterKey.ShopLevelUp:
                        case CounterKey.LineageLevelUp:
                        case CounterKey.AnyUpgrade:
                            return CoachTargets.GrowthPanel;
                        case CounterKey.GachaPull:
                            return CoachTargets.GachaPanel;
                        case CounterKey.ExpeditionStart:
                        case CounterKey.ExpeditionClaim:
                            return CoachTargets.ExpeditionPanel;
                    }
                    return null;
                default:
                    return null;
            }
        }

        static string TabFor(string panelPath)
        {
            if (panelPath == CoachTargets.GrowthPanel) return CoachTargets.TabGrowth;
            if (panelPath == CoachTargets.FormationPanel) return CoachTargets.TabFormation;
            if (panelPath == CoachTargets.GachaPanel) return CoachTargets.TabGacha;
            if (panelPath == CoachTargets.ExpeditionPanel) return CoachTargets.TabExpedition;
            if (panelPath == CoachTargets.CharacterPanel) return CoachTargets.TabCharacters;
            return CoachTargets.TabShop;
        }

        static bool IsTabButton(string path) => !string.IsNullOrEmpty(path) && path.StartsWith("SafeArea/TabBar/", System.StringComparison.Ordinal);

        RectTransform ResolveInPanel(MetaCondition condition, RectTransform panel, out RectTransform dragTo)
        {
            dragTo = null;
            switch (condition.type)
            {
                case MetaConditionType.LineageLevel:
                {
                    var card = FindCardByLineage(panel, "LineageList", condition.targetId);
                    var button = card != null ? UIFind.Deep(card, "LevelUpButton") : null;
                    return button != null ? button : UIFind.Deep(UIFind.Deep(panel, "LineageList"), "LevelUpButton");
                }
                case MetaConditionType.LineageLevelSum:
                    return UIFind.Deep(UIFind.Deep(panel, "LineageList"), "LevelUpButton");
                case MetaConditionType.ShopLevel:
                    return UIFind.Deep(panel, "LevelUp1Button");
                case MetaConditionType.GuardiansPlaced:
                {
                    var source = FindCardByLineage(panel, "CardList", FirstMeetingLineage);
                    if (source == null) source = FirstUnplacedCard(panel);
                    dragTo = FreeGuardianSlot(panel);
                    if (dragTo == null) dragTo = UIFind.Deep(panel, "Field");
                    return source;
                }
                case MetaConditionType.ActiveSynergies:
                case MetaConditionType.Flag:
                {
                    var list = UIFind.Deep(panel, "SynergyList");
                    return list != null ? FirstActiveRow(list) : null;
                }
            }

            switch (condition.counter)
            {
                case CounterKey.ShopLevelUp:
                case CounterKey.AnyUpgrade:
                    return UIFind.Deep(panel, "LevelUp1Button");
                case CounterKey.LineageLevelUp:
                    return UIFind.Deep(UIFind.Deep(panel, "LineageList"), "LevelUpButton");
                case CounterKey.GachaPull:
                    return UIFind.IsShown(FirstMeetingButton) ? FirstMeetingButton : UIFind.Deep(panel, "Pull1Button");
                case CounterKey.ExpeditionStart:
                    return UIFind.Deep(UIFind.Deep(panel, "ZoneList"), "SendButton");
                case CounterKey.ExpeditionClaim:
                {
                    var claim = UIFind.Deep(UIFind.Deep(panel, "ZoneList"), "ClaimButton");
                    return claim != null ? claim : UIFind.Deep(panel, "ClaimAllButton");
                }
                default:
                    return null;
            }
        }

        /// <summary>목록 안에서 이름 글자가 계보(또는 기본 형태) 이름과 같은 카드</summary>
        static RectTransform FindCardByLineage(RectTransform panel, string listName, string lineageId)
        {
            if (string.IsNullOrEmpty(lineageId) || !Game.IsReady) return null;
            var db = Game.Content?.Characters;
            var lineage = db?.GetLineage(lineageId);
            if (lineage == null) return null;
            string formName = db.GetForm(lineage.baseFormId)?.name;
            var list = UIFind.Deep(panel, listName);
            return list != null ? FindCardWithName(list, lineage.name, formName) : null;
        }

        static RectTransform FindCardWithName(Transform root, string name, string altName)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                var label = child.Find("NameText");
                if (label == null) label = child.Find("Body/NameText");
                if (label != null && label.TryGetComponent<TMP_Text>(out var text) && (text.text == name || text.text == altName))
                    return child as RectTransform;
                var found = FindCardWithName(child, name, altName);
                if (found != null) return found;
            }
            return null;
        }

        static RectTransform FirstUnplacedCard(RectTransform panel)
        {
            var list = UIFind.Deep(panel, "CardList");
            var content = list != null ? UIFind.Deep(list, "Content") : null;
            if (content == null) return null;
            for (int i = 0; i < content.childCount; i++)
            {
                var child = content.GetChild(i);
                if (!child.gameObject.activeSelf) continue;
                // 배치된 카드에는 상태 띠("StateBand")가 켜져 있다
                var band = child.Find("Body/StateBand");
                if (band == null || !band.gameObject.activeSelf) return child as RectTransform;
            }
            return content.childCount > 0 ? content.GetChild(0) as RectTransform : null;
        }

        static RectTransform FreeGuardianSlot(RectTransform panel)
        {
            if (!Game.IsReady || Game.Formation == null) return null;
            var field = UIFind.Deep(panel, "Field");
            if (field == null) return null;
            for (int i = 0; i < GuardianSlotNames.Length; i++)
            {
                if (Game.Formation.GetGuardian(i) != null) continue;
                var slot = UIFind.Deep(field, GuardianSlotNames[i]);
                if (slot != null) return slot;
            }
            return null;
        }

        static RectTransform FirstActiveRow(RectTransform list)
        {
            var content = UIFind.Deep(list, "Content");
            var root = content != null ? content : list;
            for (int i = 0; i < root.childCount; i++)
            {
                var child = root.GetChild(i);
                if (child.gameObject.activeSelf && child is RectTransform rt && rt.rect.height > 1f) return rt;
            }
            return null;
        }
    }
}
