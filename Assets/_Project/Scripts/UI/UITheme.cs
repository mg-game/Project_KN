using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// UI 공통 색상/표기. 씬 빌더와 런타임 뷰가 같이 사용한다.
    /// LOST &amp; LOVELY 아트 가이드: 포근한 밤거리 — 인디고/딥 퍼플 바탕, 파스텔 보라·라벤더·더스티 핑크·소프트 블루,
    /// 크림·아이보리 카드, 금색은 포인트로만. 글자·간격·크기·움직임 토큰은 UITokens.cs (UIType / UISpace / UISize / UIMotion).
    /// <para>
    /// 색 쓰는 규칙 (UX-SPEC §1.2, M29 · M82):
    ///   · Positive · Negative · Gold · Lavender 글자는 어두운 바탕에서만 쓴다.
    ///   · 양피지(Parchment · ParchmentDark) 위 글자는 Ink · InkSub · PositiveInk · NegativeInk만. 양피지 위 숫자는 Ink (Gold 금지).
    ///     글자가 아닌 선 · 고리 · 실루엣 · 구분선은 InkLine.
    ///   · 라벤더 칠 위 글자는 Ink 또는 InkSub (Text · Cream 금지). 금색 칠 위 글자는 Ink.
    ///   · 글자가 있는 단색 보라 버튼은 PrimaryButton (Primary는 토글 · 막대 · 채움용), 지우기 · 초기화 같은 파괴 버튼만 DangerButton
    ///     (전투 연출은 Danger 그대로).
    ///   · 빨간 점(RedDot) 위 숫자 · NEW는 흰색 굵게 + BadgeTextOutline 외곽선 0.2.
    ///   · 대비 목표: 24px 이하 글자 4.5:1 이상, 28px 이상 굵은 글자 3:1 이상.
    ///   · 아트(힉스필드 그림)는 흰색 그대로 쓴다 — 이 색들은 아트가 없을 때의 단색 자리표시와 흰색 틴트용 조각(탭 · 알약 · 막대)에만 입힌다.
    /// GradeColor · TraitColor · StyleColor는 바꾸지 않는다 (M83).
    /// </para>
    /// </summary>
    public static class UITheme
    {
        // ── 밤 바탕 ──
        public static readonly Color Background = Hex("17122E");
        public static readonly Color Stage = Hex("221A44");
        public static readonly Color Ground = Hex("2D2458");
        public static readonly Color Panel = Hex("261D4C");
        public static readonly Color PanelLight = Hex("362A68");
        public static readonly Color Divider = Hex("4B3D85");
        public static readonly Color Dim = new Color(0.04f, 0.02f, 0.1f, 0.72f);

        // ── 동화책 카드 ──
        public static readonly Color Parchment = Hex("F6EDDA");
        public static readonly Color ParchmentDark = Hex("E7D6B6");
        /// <summary>크림 카드 위 글자색</summary>
        public static readonly Color Ink = Hex("3D2B5E");

        /// <summary>
        /// InkSub가 M29의 진한 값(#5E4C7B, 양피지 6.45:1)을 쓰는지. 지금은 false = 예전 값 #7A6894 (UX-SPEC §4.3 WP-1 위험 3의 권장안):
        /// 글자가 아닌 InkSub(선·고리·칩 바탕)를 각 화면 담당이 InkLine으로 옮긴 뒤, Wave 2 마지막 커밋에서 true로 바꾼다.
        /// 바꾸기 전 조건 (이 한 줄만 바꾼다): WP-4 MenuSceneBuilder의 선 InkSub → InkLine, WP-6 CharacterSceneBuilder · EvolutionNodeView ·
        /// EvolutionTreeView · FriendPortrait · FormSkillRowView · CostumeItemView의 글자가 아닌 InkSub(실루엣 · 고리 · 칩 바탕) → InkLine.
        /// </summary>
        public static readonly bool InkSubDarkened = false;
        /// <summary>
        /// 크림 카드 위 보조 글자 (캡션). 목표값 #5E4C7B (양피지 6.45:1 / ParchmentDark 5.26:1) — InkSubDarkened 참고.
        /// 글자가 아닌 선·고리에는 InkLine을 쓴다
        /// </summary>
        public static readonly Color InkSub = InkSubDarkened ? Hex("5E4C7B") : Hex("7A6894");
        /// <summary>양피지 위 글자가 아닌 선 · 고리 · 실루엣 · 구분선 (예전 InkSub 값 #7A6894)</summary>
        public static readonly Color InkLine = Hex("7A6894");
        /// <summary>양피지 위 좋은 값 · 늘어난 값 (6.08:1 / ParchmentDark 4.95:1)</summary>
        public static readonly Color PositiveInk = Hex("23643F");
        /// <summary>양피지 위 나쁜 값 · 모자란 값 (5.60:1 / ParchmentDark 4.56:1)</summary>
        public static readonly Color NegativeInk = Hex("A8324B");

        // ── 글자 ──
        public static readonly Color Text = Hex("FBF6EE");
        public static readonly Color SubText = Hex("B8ADD6");
        public static readonly Color Positive = Hex("8FE3B0");
        public static readonly Color Negative = Hex("FF8A9E");

        // ── 파스텔 / 포인트 ──
        public static readonly Color Lavender = Hex("C4B2F2");
        public static readonly Color DustyPink = Hex("E9A6C6");
        public static readonly Color SoftBlue = Hex("93C8F0");
        public static readonly Color Cream = Hex("FFF4DC");
        public static readonly Color GoldAccent = Hex("E8BE6A");

        // ── 재화 ──
        /// <summary>단추 (골드)</summary>
        public static readonly Color Gold = Hex("F2C46B");
        /// <summary>별조각 (젬)</summary>
        public static readonly Color Gem = Hex("9FD3FF");
        /// <summary>별빛 사탕 (이벤트)</summary>
        public static readonly Color EventToken = Hex("FFB3D6");
        /// <summary>실타래 (유실자 레벨업 전용, T2 산호). 임시 그림(VFX/motif_yarn, 연보라)에 곱해 재료 "기억의 실"(보라)과 가른다</summary>
        public static readonly Color LineageXp = Hex("FFA27A");

        // ── 버튼 ──
        /// <summary>토글 · 막대 · 채움 보라 (글자가 있는 단색 버튼은 PrimaryButton)</summary>
        public static readonly Color Primary = Hex("8A6BE0");
        public static readonly Color Secondary = Hex("4A3B7C");
        /// <summary>전투 연출 빨강 (버튼은 DangerButton)</summary>
        public static readonly Color Danger = Hex("D9566E");
        public static readonly Color RedDot = Hex("FF5C7E");
        /// <summary>글자가 있는 단색 보라 버튼 칠 (흰 글자 5.01:1, Text 4.65:1)</summary>
        public static readonly Color PrimaryButton = Hex("7A58D6");
        /// <summary>파괴 버튼 칠 (데이터 초기화 · 계정 해제 등, Text 4.67:1)</summary>
        public static readonly Color DangerButton = Hex("C2405C");
        /// <summary>쓸 수 없는 비용 버튼 (최대 레벨 · 품절 · 보유 중) 칠 — 아트가 없을 때</summary>
        public static readonly Color Disabled = Hex("3B3460");
        /// <summary>Disabled 칠 위 글자</summary>
        public static readonly Color DisabledText = Hex("9A90BE");
        /// <summary>모자란 비용 뒤에 까는 어두운 알약 (M07)</summary>
        public static readonly Color CostPillBack = new Color(0.09f, 0.07f, 0.18f, 0.72f);
        /// <summary>어두운 알약 위 모자란 비용 글자 (5:1 이상)</summary>
        public static readonly Color CostShortText = Hex("FFC2CC");
        /// <summary>빨간 점 위 흰 숫자 · NEW 글자의 외곽선 (0.2)</summary>
        public static readonly Color BadgeTextOutline = Hex("8A1F3A");

        public static readonly Color TabNormal = Hex("1D1640");
        /// <summary>하단 탭 선택 (M52: #3A2D6E → #4A3A8A)</summary>
        public static readonly Color TabSelected = Hex("4A3A8A");

        // ── 덮개 · 토스트 · 안내 (M38 · M14 · M59) ──
        /// <summary>HUD · 탭 잠금 덮개: TabNormal 불투명 (아래 글자가 비치지 않게)</summary>
        public static readonly Color LockCover = WithAlpha(TabNormal, 1f);
        /// <summary>잠금 덮개 안쪽 2px 테두리 (Gold@0.35)</summary>
        public static readonly Color LockCoverEdge = WithAlpha(Gold, 0.35f);
        /// <summary>토스트 바탕 (Panel@0.95)</summary>
        public static readonly Color ToastBack = WithAlpha(Panel, 0.95f);
        /// <summary>토스트 2px 테두리 (GoldAccent@0.5)</summary>
        public static readonly Color ToastEdge = WithAlpha(GoldAccent, 0.5f);
        /// <summary>안내 말풍선 바탕 (Panel@0.96)</summary>
        public static readonly Color TooltipBack = WithAlpha(Panel, 0.96f);

        // ── 어두운 덮개 · 바탕 (C-23, 손대는 곳부터 날 색 대신) ──
        /// <summary>Background@0.72</summary>
        public static readonly Color Scrim = WithAlpha(Background, 0.72f);
        /// <summary>Background@0.88</summary>
        public static readonly Color ScrimStrong = WithAlpha(Background, 0.88f);
        /// <summary>Background@0.82 (유리 알약)</summary>
        public static readonly Color Glass = WithAlpha(Background, 0.82f);
        /// <summary>패널 안쪽 칸</summary>
        public static readonly Color Inset = Hex("1F173D");
        /// <summary>스위치 · 고르기 칸 꺼짐</summary>
        public static readonly Color ControlOff = Hex("2B2157");

        /// <summary>
        /// 모자란 비용 글자 색 (M07): 어두운 버튼 · 금 버튼 = CostShortText(#FFC2CC, 어두운 CostPill 위), 양피지 = NegativeInk (알약 없음)
        /// </summary>
        public static Color CostShort(CostSurface surface) => surface == CostSurface.Parchment ? NegativeInk : CostShortText;

        /// <summary>넉넉한 비용 글자 색: 어두운 버튼 = Text, 금 버튼 · 양피지 = Ink</summary>
        public static Color CostAffordable(CostSurface surface) => surface == CostSurface.Dark ? Text : Ink;

        /// <summary>색의 알파만 바꾼 값</summary>
        public static Color WithAlpha(Color color, float alpha)
        {
            color.a = alpha;
            return color;
        }

        // ── 전투 ──
        /// <summary>지킴이 (크고 불투명, 방위 색 표시)</summary>
        public static readonly Color Guardian = Hex("F2C46B");
        /// <summary>잔향이 (작고 반투명, 발광)</summary>
        public static readonly Color EchoGlow = Hex("C9A6FF");
        /// <summary>잔향 자원</summary>
        public static readonly Color Resonance = Hex("B48CFF");
        /// <summary>불빛 게이지 / 불 밝히기</summary>
        public static readonly Color Lamp = Hex("FFD98A");
        public static readonly Color Enemy = Hex("6B5A8E");

        /// <summary>방위 슬롯 색 (0 위 → 반시계: 위, 왼쪽 위, 왼쪽 아래, 오른쪽 아래, 오른쪽 위)</summary>
        public static Color SlotColor(int slot) => (slot % 5) switch
        {
            0 => Hex("F28CA8"),
            1 => Hex("8FD6A0"),
            2 => Hex("B48CFF"),
            3 => Hex("F2C46B"),
            _ => Hex("8EC5F0"),
        };

        public static Color GradeColor(ItemGrade grade) => grade switch
        {
            ItemGrade.Common => Hex("B9B3CC"),
            ItemGrade.Rare => Hex("8EC5F0"),
            ItemGrade.Epic => Hex("C3A0FF"),
            ItemGrade.Legendary => Hex("F2C46B"),
            ItemGrade.Mythic => Hex("FF9FC8"),
            _ => Color.white,
        };

        public static string GradeName(ItemGrade grade) => grade switch
        {
            ItemGrade.Common => "일반",
            ItemGrade.Rare => "희귀",
            ItemGrade.Epic => "영웅",
            ItemGrade.Legendary => "전설",
            ItemGrade.Mythic => "신화",
            _ => grade.ToString(),
        };

        /// <summary>글자 속 별 그림 태그 (TMP 기본 스프라이트 에셋 Generated/UiInlineSprites의 "star" = Icons/star)</summary>
        public const string StarSprite = "<sprite name=\"star\">";
        /// <summary>글자 속 호감도 하트 그림 태그 ("affinity" = Icons/affinity)</summary>
        public const string AffinitySprite = "<sprite name=\"affinity\">";

        static readonly string[] StarStrings = new string[8];

        /// <summary>등급 별 (Common=별 1개, Mythic=별 5개). 힉스필드 별 그림(글자 속 스프라이트)이라 &lt;color&gt;로는 색이 바뀌지 않는다</summary>
        public static string Stars(ItemGrade grade) => Stars((int)grade + 1);

        /// <summary>별 그림 n개 (글자 속 스프라이트 태그를 이어 붙인 문자열, 0~7개는 미리 만들어 둔 것을 쓴다)</summary>
        public static string Stars(int count)
        {
            if (count <= 0) return string.Empty;
            if (count >= StarStrings.Length) return string.Concat(System.Linq.Enumerable.Repeat(StarSprite, count));
            return StarStrings[count] ??= string.Concat(System.Linq.Enumerable.Repeat(StarSprite, count));
        }

        /// <summary>예전 글자 별 (★ 글리프). 별 색을 &lt;color&gt;로 바꿔야 하는 곳에서만 쓴다</summary>
        public static string StarsGlyph(ItemGrade grade) => new string('★', (int)grade + 1);

        /// <summary>예전 글자 별 n개</summary>
        public static string StarsGlyph(int count) => count > 0 ? new string('★', count) : string.Empty;

        public static string RoleName(CharacterRole role) => role == CharacterRole.Guardian ? "지킴이" : "잔향이";

        public static string StyleName(CombatStyle style) => style switch
        {
            CombatStyle.Defense => "방어",
            CombatStyle.Assault => "돌격",
            CombatStyle.Capture => "포획",
            CombatStyle.Snipe => "저격",
            CombatStyle.Support => "지원",
            CombatStyle.Heal => "힐",
            _ => style.ToString(),
        };

        public static Color StyleColor(CombatStyle style) => style switch
        {
            CombatStyle.Defense => Hex("93C8F0"),
            CombatStyle.Assault => Hex("FF9FA8"),
            CombatStyle.Capture => Hex("C4B2F2"),
            CombatStyle.Snipe => Hex("F2C46B"),
            CombatStyle.Support => Hex("8FE3B0"),
            CombatStyle.Heal => Hex("FFB3C8"),
            _ => Color.white,
        };

        public static string TraitName(TraitType trait) => trait switch
        {
            TraitType.Fabric => "천",
            TraitType.Metal => "금속",
            TraitType.Light => "빛",
            TraitType.Sound => "소리",
            TraitType.Night => "밤",
            TraitType.Rain => "비",
            TraitType.Charm => "치장",
            _ => trait.ToString(),
        };

        public static Color TraitColor(TraitType trait) => trait switch
        {
            TraitType.Fabric => Hex("E9C9A8"),
            TraitType.Metal => Hex("C9D3E6"),
            TraitType.Light => Hex("FFD98A"),
            TraitType.Sound => Hex("C4B2F2"),
            TraitType.Night => Hex("8A9BE0"),
            TraitType.Rain => Hex("93C8F0"),
            TraitType.Charm => Hex("F2A6C6"),
            _ => Color.white,
        };

        public static string StageModifierName(StageModifierType modifier) => modifier switch
        {
            StageModifierType.Rain => "비 오는 밤",
            StageModifierType.Blackout => "정전",
            StageModifierType.FullMoon => "보름달",
            StageModifierType.Wind => "바람 부는 밤",
            StageModifierType.DustStorm => "먼지 폭풍",
            StageModifierType.Festival => "축제의 밤",
            _ => string.Empty,
        };

        public static Color StageModifierColor(StageModifierType modifier) => modifier switch
        {
            StageModifierType.Rain => Hex("93C8F0"),
            StageModifierType.Blackout => Hex("5A4A8A"),
            StageModifierType.FullMoon => Hex("FFE7A8"),
            StageModifierType.Wind => Hex("A8E6D8"),
            StageModifierType.DustStorm => Hex("C8B49A"),
            StageModifierType.Festival => Hex("FF9FC8"),
            _ => Color.white,
        };

        public static string SynergyKindName(SynergyKind kind) => kind switch
        {
            SynergyKind.Trait => "특성",
            SynergyKind.Style => "역할",
            SynergyKind.Combo => "조합",
            _ => kind.ToString(),
        };

        public static string EvolutionStateName(EvolutionState state) => state switch
        {
            EvolutionState.Undiscovered => "미발견",
            EvolutionState.ClueFound => "단서 발견",
            EvolutionState.Unlocked => "해금",
            _ => state.ToString(),
        };

        public static Color CurrencyColor(CurrencyType type) => type switch
        {
            CurrencyType.Gold => Gold,
            CurrencyType.Gem => Gem,
            CurrencyType.EventToken => EventToken,
            CurrencyType.LineageXp => LineageXp,
            _ => Color.white,
        };

        /// <summary>
        /// 재화 그림에 곱할 색 (T2). 실타래는 임시 그림(VFX/motif_yarn)이 흰 모양 재료라 산호를 곱하고, 나머지 재화는 채색 그림이라 흰색.
        /// 아트 큐의 실타래 아이콘(Icons/currency_yarn)이 들어오면 흰색으로 바꾼다
        /// </summary>
        public static Color CurrencyIconTint(CurrencyType type) => type == CurrencyType.LineageXp ? LineageXp : Color.white;

        public static string ColorTag(Color color) => "#" + ColorUtility.ToHtmlStringRGB(color);

        static Color Hex(string hex)
        {
            ColorUtility.TryParseHtmlString("#" + hex, out var color);
            return color;
        }
    }

    /// <summary>
    /// 팝업 안 탭 · 고르기 칸의 선택 모양을 하나로 (M53). 색과 글자만 바꾼다 — 탭 크기 · 이름 · 연결은 그대로.
    ///   · Page (쪽 탭: 친구 상세 · 수선 일지 · 출석부 · 설정 · 상점 · 시너지 도감): 선택 = 양피지 칠 + Ink 굵게 / 아님 = TabNormal + SubText 보통
    ///   · Option (고르기 칸: 설정 줄 · 목록 거르기): 선택 = PrimaryButton + Cream 굵게 / 아님 = Secondary + SubText 보통.
    ///     바탕에 UnityEngine.UI.Outline이 붙어 있으면 선택일 때만 켠다 (2px Cream@0.6 — 색만으로 고르지 않게).
    ///   · accent가 있으면 선택일 때만 켜고 accentColor(없으면 Gold)를 입힌다 (도감 종류 색 밑줄 · 이벤트 상점 분홍 막대 6px).
    /// 바탕(bg)은 흰색 틴트용 그림(ui_tab_9s · ui_pill_white_9s)이나 단색이어야 한다 — 색이 그려진 아트에 쓰지 않는다.
    ///   · 예외 (SX-2, ART-UPGRADE §4.3 #9): Page 탭 바탕 Image에 쓸 수 있는 TabSkin(켜짐 · 꺼짐 전용 그림 한 쌍)이 붙어 있으면 색 대신 그 그림으로 바꾸고 흰색으로 둔다.
    ///     TabSkin이 없거나 한 쌍이 다 있지 않으면 지금처럼 색으로 칠한다 (빌더는 전용 아트가 있을 때만 붙인다 — UiArt.HasTabSkin). 글자 · accent 규칙은 그대로.
    /// 누를 때만 부른다 (색 · 굵기는 바뀔 때만 다시 그려진다).
    /// </summary>
    public static class TabStyle
    {
        static readonly Color OptionEdge = UITheme.WithAlpha(UITheme.Cream, 0.6f);
        static readonly Vector2 OptionEdgeDistance = new(2f, -2f);

        public static void Apply(Graphic bg, TMP_Text label, bool selected, TabVariant variant, Graphic accent = null, Color? accentColor = null)
        {
            bool page = variant == TabVariant.Page;
            if (bg != null)
            {
                if (!(page && ApplySkin(bg, selected)))
                    bg.color = page
                        ? selected ? UITheme.Parchment : UITheme.TabNormal
                        : selected ? UITheme.PrimaryButton : UITheme.Secondary;
                if (!page && bg.TryGetComponent<Outline>(out var edge))
                {
                    edge.effectColor = OptionEdge;
                    edge.effectDistance = OptionEdgeDistance;
                    edge.enabled = selected;
                }
            }

            if (label != null)
            {
                label.color = page
                    ? selected ? UITheme.Ink : UITheme.SubText
                    : selected ? UITheme.Cream : UITheme.SubText;
                var style = selected ? label.fontStyle | FontStyles.Bold : label.fontStyle & ~FontStyles.Bold;
                if (label.fontStyle != style) label.fontStyle = style;
            }

            if (accent != null)
            {
                accent.enabled = selected;
                if (selected) accent.color = accentColor ?? UITheme.Gold;
            }
        }

        /// <summary>쓸 수 있는 TabSkin이 있으면 켜짐 · 꺼짐 그림으로 바꾸고 흰색으로 둔다 (바꿨으면 true). 할당 없음</summary>
        static bool ApplySkin(Graphic bg, bool selected)
        {
            if (!(bg is Image image) || !image.TryGetComponent<TabSkin>(out var skin) || !skin.IsUsable) return false;
            var sprite = selected ? skin.On : skin.Off;
            if (image.sprite != sprite) image.sprite = sprite;
            image.color = Color.white;
            return true;
        }
    }
}
