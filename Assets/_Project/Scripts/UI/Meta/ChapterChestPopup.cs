using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 챕터 상자 840×780 (PRG-06 챕터 상자 · P1 core, SPEC4 §8.5): 상자 그림 300 (닫힘 → 받기에서 0.4초 흔들림 + 열림 그림 + 별빛) +
    /// '{n}장 상자' + '{보스 이름}를 물리치고 찾은 상자예요' + 내용 (MetaRewardChip 96) + [받기] Gold → 받은 것이 날아간다 → 0.7초 뒤 닫힘.
    /// 발표자(MetaPopupPresenter)가 ChapterChest 요청을 스테이지 사이에 띄운다 (같은 챕터의 동화책보다 먼저). 이미 열었거나 내용이 없으면 띄우지 않는다.
    /// X · 뒤로 · 어둠 = [받기] (P1 핵심 보상이라 받지 않고 사라지지 않는다 — ChapterChestSystem은 x-10을 다시 깰 때만 다시 줄에 넣는다).
    /// 다른 길로 닫혀도 (UIManager.CloseAll · 코드의 Close) 아직 안 받았으면 닫힐 때 받고, 받았는데 아직 안 날린 것은 그때 날린다.
    /// 메타가 꺼졌거나 시계 되돌림이면 받지 않고 닫는다 — 상자는 남고, 장 지도(ChapterMapPopup)의 상자 표시를 누르면 이 창이 다시 열린다.
    /// 소리: 열 때 MissionChest (`chest_open`, AUDIO-SPEC 22행 — 일지 · 챕터 · 잃어버린 상자, SPEC4 §8.5).
    /// </summary>
    public class ChapterChestPopup : UIPopup
    {
        const float ShakeSeconds = 0.4f;
        const float CloseDelay = 0.7f;

        [SerializeField] TMP_Text _titleText;
        [SerializeField] TMP_Text _lineText;
        [SerializeField] RectTransform _chest;
        [SerializeField] Image _chestImage;
        [SerializeField] Sprite _closedSprite;
        [SerializeField] Sprite _openSprite;
        [SerializeField] RectTransform _rewardRoot;
        [SerializeField] MetaRewardChip _rewardTemplate;
        [SerializeField] Button _claimButton;
        [SerializeField] TMP_Text _claimLabel;
        [SerializeField] MetaSparkleBurst _sparkles;

        MetaViewPool<MetaRewardChip> _chips;
        int _chapter;
        bool _claimed;
        Coroutine _open;
        /// <summary>받았지만 아직 날리지 않은 보상 (흔들림 중에 닫히면 OnClose가 날린다)</summary>
        List<MetaReward> _unflown;

        protected override bool CloseButtonUsesDismiss => true;

        /// <summary>지금 보여 주는 챕터</summary>
        public int Chapter => _chapter;

        protected override void Awake()
        {
            base.Awake();
            EnsurePool();
            if (_claimButton != null) _claimButton.onClick.AddListener(Claim);
        }

        /// <summary>
        /// 내용 칸 풀을 한 번만 만든다. 빌더가 팝업을 꺼 둔 채 저장해서 처음 Show가 불릴 때는 Awake가 아직 돌지 않았다
        /// (Show가 칸을 채운 뒤 Open → SetActive에서 Awake가 돈다 — r1: 처음 열면 내용 칸이 비어 보였다). 그래서 FillChips도 여기서 풀을 만들고,
        /// Awake는 이미 있으면 그대로 쓴다 (새로 만들면 이미 채운 칸이 풀 밖에서 켜진 채 남는다). ReturnPopup.EnsurePool과 같은 규칙
        /// </summary>
        void EnsurePool()
        {
            if (_chips == null && _rewardTemplate != null && _rewardRoot != null) _chips = new MetaViewPool<MetaRewardChip>(_rewardTemplate, _rewardRoot);
        }

        /// <summary>챕터 상자를 띄운다. 이미 열었거나 내용이 없으면 false (띄우지 않는다)</summary>
        public bool Show(int chapter)
        {
            var chests = MetaUi.IsActive ? Game.ChapterChests : null;
            if (chests == null || chapter <= 0 || chests.IsChapterChestOpened(chapter)) return false;
            var contents = chests.PeekChapterChest(chapter);
            if (contents == null || contents.Count == 0) return false;
            _chapter = chapter;
            _claimed = false;
            _unflown = null;
            if (_titleText != null) _titleText.text = $"{chapter}장 상자";
            if (_lineText != null) _lineText.text = LineFor(BossName(chapter));
            if (_chestImage != null && _closedSprite != null) _chestImage.sprite = _closedSprite;
            if (_chest != null)
            {
                _chest.localRotation = Quaternion.identity;
                _chest.localScale = Vector3.one;
            }
            if (_claimButton != null) _claimButton.interactable = true;
            if (_claimLabel != null) _claimLabel.text = "받기";
            FillChips(contents);
            Open();
            return true;
        }

        /// <summary>한 줄 (순수 — 보스 이름이 없으면 '챕터 보스')</summary>
        public static string LineFor(string bossName) =>
            string.IsNullOrEmpty(bossName) ? "챕터 보스를 물리치고 찾은 상자예요" : $"{MetaUi.WithObject(bossName)} 물리치고 찾은 상자예요";

        /// <summary>그 챕터 보스 스테이지(x-10)에 나올 수 있는 보스 중 가장 늦게 나오는 보스의 이름 (없으면 null)</summary>
        static string BossName(int chapter)
        {
            var config = Game.Battle != null ? Game.Battle.Config : null;
            if (config == null || config.enemies == null) return null;
            int stage = chapter * 10;
            EnemyDef best = null;
            foreach (var def in config.enemies)
            {
                if (def == null || !def.isBoss || def.minStage > stage || string.IsNullOrEmpty(def.name)) continue;
                if (best == null || def.minStage > best.minStage) best = def;
            }
            return best?.name;
        }

        void FillChips(IReadOnlyList<MetaReward> rewards)
        {
            EnsurePool();
            if (_chips == null) return;
            int count = 0;
            for (int i = 0; i < rewards.Count; i++)
            {
                if (rewards[i] == null) continue;
                _chips.Get(count++).Set(rewards[i]);
            }
            _chips.Trim(count);
        }

        /// <summary>
        /// X · 뒤로 · 어둠 (M49): 아직 안 받았으면 받는다 (받기 연출 뒤 저절로 닫힘), 받는 중이면 연출이 끝나며 닫힌다.
        /// 메타가 꺼졌거나 시계 되돌림이면 받지 않고 닫는다 (상자는 남는다)
        /// </summary>
        protected override void OnDimOrBack()
        {
            if (_claimed)
            {
                if (_open == null) Close();
                return;
            }
            if (!MetaUi.IsActive || MetaUi.BlockIfTampered())
            {
                Close();
                return;
            }
            Claim();
        }

        protected override void OnClose()
        {
            if (_open != null) StopCoroutine(_open);
            _open = null;
            // 다른 길로 닫혀도 상자가 사라지지 않게: 아직 안 받았으면 여기서 받는다 (메타 꺼짐 · 되돌림이면 그대로 둔다)
            if (!_claimed && _chapter > 0 && MetaUi.IsActive && !(Game.DailyClock != null && Game.DailyClock.IsTampered))
            {
                var granted = Game.ChapterChests?.OpenChapterChest(_chapter);
                if (granted != null)
                {
                    _claimed = true;
                    _unflown = granted;
                    UISfx.Play(UISfxKey.MissionChest);
                }
            }
            FlyUnflown();
        }

        /// <summary>받은 것을 (아직 안 날렸으면) 한 번 날린다</summary>
        void FlyUnflown()
        {
            var fly = _unflown;
            _unflown = null;
            if (fly != null && fly.Count > 0) MetaUi.FlyRewards(fly, _rewardRoot != null ? _rewardRoot : _chest);
        }

        void Claim()
        {
            if (_claimed || !MetaUi.IsActive) return;
            if (MetaUi.BlockIfTampered()) return;
            var chests = Game.ChapterChests;
            var granted = chests?.OpenChapterChest(_chapter);
            if (granted == null)
            {
                MetaUi.Toast("지금은 상자를 열 수 없어요");
                Close();
                return;
            }
            _claimed = true;
            _unflown = granted;
            if (_claimButton != null) _claimButton.interactable = false;
            UISfx.Play(UISfxKey.MissionChest);
            if (isActiveAndEnabled) _open = StartCoroutine(OpenRoutine());
            else Close();
        }

        IEnumerator OpenRoutine()
        {
            if (_chest != null && !UIMotion.Reduced)
            {
                for (float t = 0f; t < ShakeSeconds; t += Time.unscaledDeltaTime)
                {
                    float k = t / ShakeSeconds;
                    _chest.localRotation = Quaternion.Euler(0f, 0f, 9f * Mathf.Sin(k * Mathf.PI * 6f) * (1f - k));
                    yield return null;
                }
                _chest.localRotation = Quaternion.identity;
            }
            if (_chestImage != null && _openSprite != null) _chestImage.sprite = _openSprite;
            if (_sparkles != null && _chest != null) _sparkles.BurstAt(_chest, 14, UITheme.Lamp, 1f);
            FlyUnflown();
            yield return new WaitForSecondsRealtime(CloseDelay);
            _open = null;
            Close();
        }
    }
}
