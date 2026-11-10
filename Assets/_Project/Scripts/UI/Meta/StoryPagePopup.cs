using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 동화책 한 쪽 980×1400 (CR-05, SPEC4 §8.1): 제목 '동화책 {n}장 · {제목}', 그림 칸 3개 (StoryPanelDef.art — 없으면 안쪽 판 + 장 색 틴트, 글만)를
    /// 위에서부터 하나씩 0.25초 페이드로 보여 주고 (1.2초마다 저절로, 누르면 바로 다음 — StoryIntroView처럼), 칸마다 아래 한 줄 (Body 26, 가운데).
    /// 움직임 줄이기면 한 번에. 아래: 처음 읽기면 보상 칩 + [닫기] Gold, 다시 읽기(도감 이야기 칸)면 [닫기] Secondary (보상 없음 — 같은 버튼의 그림을 바꿔 끼운다).
    /// X · 뒤로 · 어둠은 먼저 남은 칸을 다 보여 주고, 그다음에 닫는다 (M49).
    /// 처음 읽기: 열 때 Story.MarkSeen (도감 story:{n}도 그때), 닫을 때 Story.TryClaimFirstRead → 받은 것이 날아간다 (한 번).
    /// [닫기] 말고 다른 길로 닫혀도 (UIManager.CloseAll · 코드의 Close) 닫힐 때 받는다 — 본 장은 다시 줄에 들어오지 않고 다시 읽기에는 선물이 없다.
    /// 소리: 칸이 나타날 때 StoryPage, 열려 있는 동안 음악 Story (AudioService.PushMusic — 닫으면 PopMusic).
    /// </summary>
    public class StoryPagePopup : UIPopup
    {
        public const int PanelCount = 3;
        public const float FadeSeconds = 0.25f;
        public const float AutoAdvanceSeconds = 1.2f;

        [SerializeField] TMP_Text _titleText;
        [Tooltip("그림 칸 묶음 3개 (CanvasGroup — 하나씩 페이드)")]
        [SerializeField] CanvasGroup[] _panels = Array.Empty<CanvasGroup>();
        [SerializeField] Image[] _panelArt = Array.Empty<Image>();
        [Tooltip("그림이 없을 때 보이는 장 색 판")]
        [SerializeField] Image[] _panelFallback = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _panelText = Array.Empty<TMP_Text>();
        [Tooltip("본문 전체 누름 (다음 칸)")]
        [SerializeField] Button _tapCatcher;
        [SerializeField] GameObject _rewardRoot;
        [SerializeField] MetaRewardChip _rewardChip;
        [Tooltip("[닫기] (처음 읽기 Gold · 다시 읽기 Secondary — 그림을 바꿔 끼운다)")]
        [SerializeField] Button _finishButton;
        [SerializeField] Image _finishImage;
        [SerializeField] TMP_Text _finishLabel;
        [SerializeField] Sprite _goldSprite;
        [SerializeField] Sprite _softSprite;

        /// <summary>장마다 그림이 없을 때의 판 색 (1장부터 돌아가며)</summary>
        static readonly Color[] ChapterTints =
        {
            new(0.42f, 0.36f, 0.62f, 1f), new(0.30f, 0.45f, 0.55f, 1f), new(0.55f, 0.38f, 0.45f, 1f),
            new(0.36f, 0.50f, 0.40f, 1f), new(0.56f, 0.46f, 0.30f, 1f),
        };

        int _chapter;
        bool _firstRead;
        int _shown;
        float _fade;
        float _wait;
        bool _musicPushed;
        readonly List<MetaReward> _rewardsPreview = new(2);

        /// <summary>지금 보여 주는 챕터</summary>
        public int Chapter => _chapter;

        /// <summary>처음 읽기인지 (보상이 있는 쪽)</summary>
        public bool IsFirstRead => _firstRead;

        /// <summary>다 보여 줬는지</summary>
        public bool FullyRevealed => _shown >= VisiblePanelCount && _fade >= 1f;

        int VisiblePanelCount => Mathf.Min(PanelCount, _panels.Length);

        protected override bool CloseButtonUsesDismiss => true;

        protected override void Awake()
        {
            base.Awake();
            if (_tapCatcher != null) _tapCatcher.onClick.AddListener(Advance);
            if (_finishButton != null) _finishButton.onClick.AddListener(Finish);
        }

        /// <summary>챕터 보스를 처음 깬 뒤의 동화책 (발표자). 내용이 없는 챕터는 false (띄우지 않는다)</summary>
        public bool ShowFirstRead(int chapter) => Show(chapter, true);

        /// <summary>다시 읽기 (도감 이야기 칸) — 보상 없음. 내용이 없으면 false</summary>
        public bool ShowReread(int chapter) => Show(chapter, false);

        bool Show(int chapter, bool firstRead)
        {
            var story = MetaUi.IsActive ? Game.Story : null;
            var def = story?.Get(chapter);
            if (def == null || def.panels == null || def.panels.Count == 0) return false;
            _chapter = chapter;
            // 처음 읽기 = 발표자가 띄운 쪽 (StorySystem은 아직 안 본 챕터만 줄에 넣는다). 이미 받은 선물이면 TryClaimFirstRead가 null을 돌려준다
            _firstRead = firstRead;
            if (_titleText != null) _titleText.text = string.IsNullOrEmpty(def.title) ? $"동화책 {chapter}장" : $"동화책 {chapter}장 · {def.title}";
            var tint = ChapterTints[(Math.Max(1, chapter) - 1) % ChapterTints.Length];
            for (int i = 0; i < _panels.Length; i++)
            {
                var panel = i < def.panels.Count ? def.panels[i] : null;
                bool used = panel != null && i < PanelCount;
                if (_panels[i] != null)
                {
                    _panels[i].gameObject.SetActive(used);
                    _panels[i].alpha = 0f;
                }
                if (!used) continue;
                var art = i < _panelArt.Length ? _panelArt[i] : null;
                if (art != null)
                {
                    art.sprite = panel.art;
                    art.enabled = panel.art != null;
                }
                var fallback = i < _panelFallback.Length ? _panelFallback[i] : null;
                if (fallback != null)
                {
                    fallback.enabled = panel.art == null;
                    fallback.color = tint;
                }
                var text = i < _panelText.Length ? _panelText[i] : null;
                if (text != null) text.text = panel.text ?? string.Empty;
            }
            _shown = 0;
            _fade = 1f;
            _wait = 0f;
            _rewardsPreview.Clear();
            if (_firstRead && def.firstReadRewards != null)
            {
                foreach (var reward in def.firstReadRewards)
                {
                    if (reward != null) _rewardsPreview.Add(reward);
                }
            }
            bool hasReward = _rewardsPreview.Count > 0;
            if (_rewardRoot != null) _rewardRoot.SetActive(hasReward);
            if (hasReward && _rewardChip != null) _rewardChip.Set(_rewardsPreview[0]);
            StyleFinish(_firstRead);
            if (_firstRead) story.MarkSeen(chapter);
            Open();
            if (UIMotion.Reduced) RevealAll();
            else Advance();
            return true;
        }

        /// <summary>[닫기] 모양: 처음 읽기 Gold (Ink 글자) · 다시 읽기 Secondary (Text 글자). 그림이 없으면 색만</summary>
        void StyleFinish(bool gold)
        {
            if (_finishImage != null)
            {
                var sprite = gold ? _goldSprite : _softSprite;
                if (sprite != null)
                {
                    _finishImage.sprite = sprite;
                    _finishImage.color = Color.white;
                }
                else _finishImage.color = gold ? UITheme.Gold : UITheme.Secondary;
            }
            if (_finishLabel != null) _finishLabel.color = gold ? UITheme.Ink : UITheme.Text;
        }

        protected override void OnOpen()
        {
            if (_musicPushed) return;
            AudioService.PushMusic(MusicState.Story, GetInstanceID());
            _musicPushed = true;
        }

        protected override void OnClose()
        {
            // [닫기]를 거치지 않고 닫혀도 처음 읽은 선물은 받는다 (TryClaimFirstRead는 rewardedChapters로 한 번만)
            ClaimFirstRead();
            if (!_musicPushed) return;
            _musicPushed = false;
            AudioService.PopMusic(GetInstanceID());
        }

        void Update()
        {
            if (!IsOpen) return;
            float dt = Time.unscaledDeltaTime;
            if (_fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + dt / FadeSeconds);
                int index = _shown - 1;
                if (index >= 0 && index < _panels.Length && _panels[index] != null) _panels[index].alpha = _fade;
                return;
            }
            if (_shown >= VisiblePanelCount) return;
            _wait += dt;
            if (_wait >= AutoAdvanceSeconds) Advance();
        }

        /// <summary>다음 칸 (페이드 중이면 그 칸을 끝낸다). 다 보였으면 아무것도 하지 않는다</summary>
        void Advance()
        {
            if (!IsOpen) return;
            if (_fade < 1f)
            {
                _fade = 1f;
                int index = _shown - 1;
                if (index >= 0 && index < _panels.Length && _panels[index] != null) _panels[index].alpha = 1f;
                return;
            }
            if (_shown >= VisiblePanelCount) return;
            var panel = _shown < _panels.Length ? _panels[_shown] : null;
            _shown++;
            _wait = 0f;
            if (panel == null || !panel.gameObject.activeSelf)
            {
                _shown = VisiblePanelCount;
                return;
            }
            UISfx.Play(UISfxKey.StoryPage);
            _fade = 0f;
            panel.alpha = 0f;
        }

        void RevealAll()
        {
            for (int i = 0; i < _panels.Length; i++)
            {
                if (_panels[i] != null) _panels[i].alpha = 1f;
            }
            _shown = VisiblePanelCount;
            _fade = 1f;
        }

        /// <summary>X · 뒤로 · 어둠 (M49): 남은 칸이 있으면 다 보여 주고, 다 보였으면 닫는다</summary>
        protected override void OnDimOrBack()
        {
            if (!FullyRevealed)
            {
                RevealAll();
                return;
            }
            Finish();
        }

        /// <summary>[닫기]: 처음 읽기면 보상을 받고 닫는다</summary>
        void Finish()
        {
            if (!FullyRevealed)
            {
                RevealAll();
                return;
            }
            ClaimFirstRead();
            Close();
        }

        /// <summary>처음 읽기면 선물을 한 번 받고 날린다 ([닫기] · 다른 길로 닫힐 때). 메타가 꺼져 있으면 받지 않는다</summary>
        void ClaimFirstRead()
        {
            if (!_firstRead) return;
            _firstRead = false;
            var story = MetaUi.IsActive ? Game.Story : null;
            var granted = story?.TryClaimFirstRead(_chapter);
            if (granted == null || granted.Count == 0) return;
            var from = _rewardChip != null ? _rewardChip.Rect : Window;
            MetaUi.FlyRewards(granted, from);
        }
    }
}
