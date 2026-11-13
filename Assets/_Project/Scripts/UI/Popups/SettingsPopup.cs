using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 설정 (SET-01). 탭 [일반][전투 연출][알림][계정·데이터] (+ 에디터/Development Build는 [개발]) — 쪽 탭 모양 (M53, TabStyle Page).
    /// 값은 전부 GameSettings (기기별 PlayerPrefs)에 저장되고, 바뀌면 GameSettings.Changed로 전투 화면 등이 다시 읽는다.
    ///   · 일반: 배경음/효과음 볼륨(소리가 없는 빌드면 흐리게 + '준비 중', 있으면 끄는 동안 틱 소리 — M18), 소리, 캐릭터 목소리(GameSettings.VoiceBarks —
    ///     목소리 클립 vox_*가 없으면 흐리게 + '준비 중'), 진동, 잠자기 모드(끄기/1/2/3/5분), 전투 화면으로 돌아가기(UX-SPEC-IA §4.8, GameSettings.SheetAutoReturn),
    ///     프레임 30/60, 공지사항, 튜토리얼 건너뛰기(위험 확인, M74), 처음 이야기 다시 보기(UINav.ReplayIntro — 튜토리얼 중에는 누를 수 없다, SPEC4 §4.1)
    ///   · 전투 연출: 화면 흔들림 0/50/100%, 번쩍임 줄이기, 피해 숫자(모두/치명타만/끄기), 스킬 이름, 이펙트 품질(자동/낮음/보통/높음)
    ///   · 알림: 권한 상태 줄('알림이 켜져 있어요' / '기기 설정에서 알림이 꺼져 있어요' + '설정 열기 ›' / '아직 알림을 허용하지 않았어요' + '허용하기', NOTI-01) +
    ///     바구니 가득 / 원정 완료 / 던전 입장권 / 출석 도장 + 밤 조용한 시간 안내
    ///   · 계정·데이터: 계정 연동, 세이브 코드 내보내기(클립보드) · 공유(Android) · 가져오기(SaveImportPopup), 백업에서 복구(BackupRestorePopup),
    ///     쿠폰 입력(CouponPopup), 데이터 초기화. 내보내기 · 공유는 백업 알림 시계를 되돌린다 (BackupReminder.MarkBackedUp, SB-1)
    ///   SPEC4 줄의 참조는 다시 빌드한 씬에만 있다 — 없으면 그 줄만 건너뛴다 (예전 씬은 예전 그대로).
    ///   · 개발: 재화·우편, 오프라인 1시간(메타면 바구니 + 복귀 화면), 하루 넘기기, 바구니 가득, 기능 모두 열기, 수선권
    /// 스위치 · 칸 고르기 줄은 줄 어디를 눌러도 된다 (M22 — 스위치는 바뀌고, 칸 고르기는 다음 칸). 스위치를 바꾸면 ToggleOn / ToggleOff 소리.
    /// </summary>
    public class SettingsPopup : UIPopup
    {
        /// <summary>볼륨 슬라이더를 놓은 뒤 이 시간이 지나면 저장한다 (끄는 동안 디스크에 계속 쓰지 않게)</summary>
        const float VolumeCommitDelay = 0.35f;
        /// <summary>소리가 없는 빌드의 볼륨 슬라이더 흐림 (M18)</summary>
        const float AudioUnavailableAlpha = 0.5f;
        const string TutorialDescActive = "남은 안내 말풍선을 꺼요";
        const string TutorialDescDone = "처음 안내를 모두 마쳤어요";
        /// <summary>첫 만남 소환 경고를 붙이는 마지막 튜토리얼 단계 (그 뒤면 이미 소환을 지났다)</summary>
        const int FirstMeetingStepLimit = 10;
        /// <summary>캐릭터 목소리 클립 키 앞머리 (AUDIO-SPEC §2.10)</summary>
        internal const string VoiceKeyPrefix = "vox_";
        internal const string NotifyGrantedText = "알림이 켜져 있어요";
        internal const string NotifyDeniedText = "기기 설정에서 알림이 꺼져 있어요";
        internal const string NotifyAskText = "아직 알림을 허용하지 않았어요";
        internal const string NoBackupText = "아직 백업이 없어요";
        const string ShareTitle = "LOST & LOVELY 세이브 코드";

        [Header("탭")]
        [SerializeField] Button[] _tabButtons = Array.Empty<Button>();
        [SerializeField] Image[] _tabImages = Array.Empty<Image>();
        [SerializeField] TMP_Text[] _tabLabels = Array.Empty<TMP_Text>();
        [SerializeField] GameObject[] _pages = Array.Empty<GameObject>();

        [Header("일반")]
        [SerializeField] Slider _bgmSlider;
        [SerializeField] TMP_Text _bgmValue;
        [SerializeField] Slider _sfxSlider;
        [SerializeField] TMP_Text _sfxValue;
        [Tooltip("볼륨 슬라이더 흐림 (소리가 없는 빌드면 α0.5)")]
        [SerializeField] CanvasGroup _bgmGroup;
        [SerializeField] CanvasGroup _sfxGroup;
        [Tooltip("'준비 중' 알약 (소리가 없는 빌드만)")]
        [SerializeField] GameObject _bgmReady;
        [SerializeField] GameObject _sfxReady;
        [SerializeField] MetaSwitch _soundSwitch;
        [SerializeField] MetaSwitch _hapticsSwitch;
        [SerializeField] MetaSegmentedControl _sleepSegment;
        [Tooltip("전투 화면으로 돌아가기 (GameSettings.SheetAutoReturn — 1분 동안 손대지 않으면 시트를 내린다)")]
        [SerializeField] MetaSwitch _sheetAutoReturnSwitch;
        [SerializeField] MetaSegmentedControl _frameSegment;
        [SerializeField] Button _noticeButton;
        [SerializeField] Button _tutorialButton;
        [SerializeField] TMP_Text _tutorialLabel;
        [Tooltip("TutorialRow/Desc ('남은 안내 말풍선을 꺼요' / '처음 안내를 모두 마쳤어요')")]
        [SerializeField] TMP_Text _tutorialDesc;
        [SerializeField] TMP_Text _versionText;
        [Tooltip("캐릭터 목소리 스위치 (GameSettings.VoiceBarks, SPEC4 §4.1 — 다시 빌드한 씬부터)")]
        [SerializeField] MetaSwitch _voiceSwitch;
        [Tooltip("목소리 스위치 흐림 (목소리 클립이 없으면 α0.5 · 누를 수 없음)")]
        [SerializeField] CanvasGroup _voiceGroup;
        [Tooltip("'준비 중' 알약 (목소리 클립이 없을 때만)")]
        [SerializeField] GameObject _voiceReady;
        [Tooltip("VoiceRow 줄 전체 누르기 (목소리를 쓸 수 있을 때만 스위치로)")]
        [SerializeField] Button _voiceRow;
        [Tooltip("처음 이야기 다시 보기 '보기' (UINav.ReplayIntro)")]
        [SerializeField] Button _introButton;

        [Header("전투 연출")]
        [SerializeField] MetaSegmentedControl _shakeSegment;
        [SerializeField] MetaSwitch _reduceFlashSwitch;
        [SerializeField] MetaSegmentedControl _damageSegment;
        [SerializeField] MetaSwitch _skillNamesSwitch;
        [SerializeField] MetaSegmentedControl _qualitySegment;
        [SerializeField] TMP_Text _qualityHint;

        [Header("알림 (NotificationKind 순서)")]
        [SerializeField] MetaSwitch[] _notifySwitches = Array.Empty<MetaSwitch>();
        [Tooltip("권한 상태 줄 NotifyStatus (NOTI-01)")]
        [SerializeField] TMP_Text _notifyStatusText;
        [Tooltip("'허용하기' / '설정 열기 ›' (켜져 있으면 숨김)")]
        [SerializeField] Button _notifyStatusButton;
        [SerializeField] TMP_Text _notifyStatusLabel;

        [Header("줄 전체 누르기 (M22 — 같은 순서로 짝)")]
        [SerializeField] Button[] _switchRows = Array.Empty<Button>();
        [SerializeField] MetaSwitch[] _rowSwitches = Array.Empty<MetaSwitch>();
        [SerializeField] Button[] _segmentRows = Array.Empty<Button>();
        [SerializeField] MetaSegmentedControl[] _rowSegments = Array.Empty<MetaSegmentedControl>();

        [Header("계정·데이터")]
        [SerializeField] TMP_Text _accountText;
        [SerializeField] GameObject _guestWarning;
        [SerializeField] AccountLinkRow[] _accountRows = Array.Empty<AccountLinkRow>();
        [SerializeField] Button _exportButton;
        [Tooltip("'공유' (Android만 — ShareSheet.CanShare가 아니면 숨긴다)")]
        [SerializeField] Button _shareButton;
        [SerializeField] Button _importButton;
        [Tooltip("BackupRow '복구' (백업이 없으면 누를 수 없음)")]
        [SerializeField] Button _backupButton;
        [Tooltip("BackupRow/Desc '최근 백업: 11월 02일 21:14 · 스테이지 3-4' / '아직 백업이 없어요'")]
        [SerializeField] TMP_Text _backupDesc;
        [Tooltip("CouponRow '입력' (CouponPopup)")]
        [SerializeField] Button _couponButton;
        [SerializeField] Button _resetButton;

        [Header("개발자 메뉴 (에디터 / Development Build 전용)")]
        [SerializeField] GameObject _devTab;
        [SerializeField] Button _addGoldButton;
        [SerializeField] Button _addGemButton;
        [SerializeField] Button _testMailButton;
        [SerializeField] Button _offlineTestButton;
        [SerializeField] Button _nextDayButton;
        [SerializeField] Button _fillChestButton;
        [SerializeField] Button _unlockAllButton;
        [SerializeField] Button _ticketsButton;
        [Tooltip("[계정 · 데이터] '플레이 기록 보내기' (D-58 #1 — 출시 빌드에도 있음). 최근 24시간 요약 + [내보내기]. 다시 빌드하기 전 장면에서는 개발 탭 버튼을 가리킨다")]
        [SerializeField] Button _playLogButton;

        static readonly string[] QualityNames = { "낮음", "보통", "높음" };

        int _tab;
        float _volumeTimer = -1f;
        bool _syncing;
        /// <summary>목소리 클립이 있는지 (열 때 한 번 센다)</summary>
        bool _voiceLive;
        /// <summary>권한을 묻는 중 (두 번 묻지 않게)</summary>
        bool _asking;

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < _tabButtons.Length; i++)
            {
                int index = i;
                _tabButtons[i].onClick.AddListener(() => SelectTab(index));
            }

            _bgmSlider.onValueChanged.AddListener(OnVolumeChanged);
            _sfxSlider.onValueChanged.AddListener(OnSfxVolumeChanged);
            _soundSwitch.Changed += on => GameSettings.SoundOn = on;
            _hapticsSwitch.Changed += on => GameSettings.Haptics = on;
            _sleepSegment.Changed += index => GameSettings.SleepMinutes = GameSettings.SleepMinuteOptions[index];
            if (_sheetAutoReturnSwitch != null) _sheetAutoReturnSwitch.Changed += on => GameSettings.SheetAutoReturn = on;
            _frameSegment.Changed += index => GameSettings.FrameRate = GameSettings.FrameRateOptions[index];
            _noticeButton.onClick.AddListener(OpenNotices);
            _tutorialButton.onClick.AddListener(ConfirmSkipTutorial);
            if (_voiceSwitch != null)
            {
                _voiceSwitch.Changed += on => GameSettings.VoiceBarks = on;
                _voiceSwitch.Changed += PlayToggle;
                var voiceControl = _voiceSwitch.GetComponent<Button>();
                // 줄을 누르면 스위치 (목소리를 쓸 수 없으면 아무것도 하지 않는다)
                if (_voiceRow != null && voiceControl != null) _voiceRow.onClick.AddListener(() => { if (_voiceLive) voiceControl.onClick.Invoke(); });
            }
            if (_introButton != null) _introButton.onClick.AddListener(ReplayIntro);
            if (_notifyStatusButton != null) _notifyStatusButton.onClick.AddListener(OnNotifyStatusButton);

            _shakeSegment.Changed += index => GameSettings.ShakePercent = GameSettings.ShakeOptions[index];
            _reduceFlashSwitch.Changed += on => GameSettings.ReduceFlash = on;
            _damageSegment.Changed += index => GameSettings.DamageNumbers = (DamageNumberMode)index;
            _skillNamesSwitch.Changed += on => GameSettings.SkillNames = on;
            _qualitySegment.Changed += OnQualityChanged;

            for (int i = 0; i < _notifySwitches.Length; i++)
            {
                var kind = (NotificationKind)i;
                _notifySwitches[i].Changed += on => GameSettings.SetNotify(kind, on);
            }
            WireRows();

            _exportButton.onClick.AddListener(ExportSave);
            if (_shareButton != null) _shareButton.onClick.AddListener(ShareSave);
            _importButton.onClick.AddListener(ImportSave);
            if (_backupButton != null) _backupButton.onClick.AddListener(OpenBackups);
            if (_couponButton != null) _couponButton.onClick.AddListener(OpenCoupon);
            _resetButton.onClick.AddListener(ConfirmReset);
            _versionText.text = $"버전 {Application.version}";

            // 플레이 기록 보내기 (D-58 #1): 외부 테스터 빌드(Development Build 끔)에서도 쓴다 — 치트와 달리 #if 밖
            if (_playLogButton != null) _playLogButton.onClick.AddListener(ShowPlayLog);
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            _devTab.SetActive(true);
            _addGoldButton.onClick.AddListener(AddGold);
            _addGemButton.onClick.AddListener(AddGem);
            _testMailButton.onClick.AddListener(SendTestMail);
            _offlineTestButton.onClick.AddListener(TestOfflineReward);
            _nextDayButton.onClick.AddListener(SkipDay);
            _fillChestButton.onClick.AddListener(FillChest);
            _unlockAllButton.onClick.AddListener(UnlockAllFeatures);
            _ticketsButton.onClick.AddListener(AddTickets);
#else
            _devTab.SetActive(false);
#endif
        }

        /// <summary>줄 전체 누르기와 스위치 소리를 잇는다 (Awake 한 번)</summary>
        void WireRows()
        {
            int switches = Mathf.Min(_switchRows.Length, _rowSwitches.Length);
            for (int i = 0; i < switches; i++)
            {
                var view = _rowSwitches[i];
                if (_switchRows[i] == null || view == null) continue;
                var control = view.GetComponent<Button>();
                // 줄을 누르면 스위치를 누른 것과 같다 (스위치 위를 누르면 스위치가 먼저 받는다)
                if (control != null) _switchRows[i].onClick.AddListener(control.onClick.Invoke);
                view.Changed += PlayToggle;
            }
            int segments = Mathf.Min(_segmentRows.Length, _rowSegments.Length);
            for (int i = 0; i < segments; i++)
            {
                var view = _rowSegments[i];
                if (_segmentRows[i] == null || view == null) continue;
                var options = view.GetComponentsInChildren<Button>(true);
                // 줄을 누르면 다음 칸 (끝에서는 처음으로)
                _segmentRows[i].onClick.AddListener(() =>
                {
                    if (options.Length == 0) return;
                    int next = view.Index < 0 ? 0 : (view.Index + 1) % options.Length;
                    options[next].onClick.Invoke();
                });
            }
        }

        static void PlayToggle(bool on) => UISfx.Play(on ? UISfxKey.ToggleOn : UISfxKey.ToggleOff);

        protected override void OnOpen()
        {
            Game.Account.Changed -= RefreshAccount;
            Game.Account.Changed += RefreshAccount;
            SyncFromSettings();
            RefreshAudio();
            RefreshAccount();
            RefreshTutorial();
            RefreshNotifyStatus();
            RefreshSaveRows();
            SelectTab(Mathf.Clamp(_tab, 0, _pages.Length - 1));
        }

        protected override void OnClose()
        {
            CommitVolume();
            if (Game.IsReady) Game.Account.Changed -= RefreshAccount;
        }

        void Update()
        {
            if (_volumeTimer < 0f) return;
            _volumeTimer -= Time.unscaledDeltaTime;
            if (_volumeTimer < 0f) CommitVolume();
        }

        void SelectTab(int index)
        {
            _tab = index;
            for (int i = 0; i < _pages.Length; i++)
            {
                bool on = i == index;
                _pages[i].SetActive(on);
                // 쪽 탭 (M53 Page): 고른 탭 = 양피지 + Ink 굵게, 아니면 TabNormal + SubText
                TabStyle.Apply(i < _tabImages.Length ? _tabImages[i] : null, i < _tabLabels.Length ? _tabLabels[i] : null, on, TabVariant.Page);
            }
        }

        /// <summary>화면 값을 GameSettings에 맞춘다 (알림 없이)</summary>
        void SyncFromSettings()
        {
            _syncing = true;
            _bgmSlider.SetValueWithoutNotify(Mathf.Round(GameSettings.BgmVolume * 100f));
            _sfxSlider.SetValueWithoutNotify(Mathf.Round(GameSettings.SfxVolume * 100f));
            WriteVolumeLabels();
            _soundSwitch.SetOn(GameSettings.SoundOn);
            _hapticsSwitch.SetOn(GameSettings.Haptics);
            _sleepSegment.SetIndex(IndexOf(GameSettings.SleepMinuteOptions, GameSettings.SleepMinutes));
            if (_sheetAutoReturnSwitch != null) _sheetAutoReturnSwitch.SetOn(GameSettings.SheetAutoReturn);
            _frameSegment.SetIndex(IndexOf(GameSettings.FrameRateOptions, GameSettings.FrameRate));
            _shakeSegment.SetIndex(IndexOf(GameSettings.ShakeOptions, GameSettings.ShakePercent));
            _reduceFlashSwitch.SetOn(GameSettings.ReduceFlash);
            _damageSegment.SetIndex((int)GameSettings.DamageNumbers);
            _skillNamesSwitch.SetOn(GameSettings.SkillNames);
            if (_voiceSwitch != null) _voiceSwitch.SetOn(GameSettings.VoiceBarks);
            RefreshQuality();
            for (int i = 0; i < _notifySwitches.Length; i++) _notifySwitches[i].SetOn(GameSettings.GetNotify((NotificationKind)i));
            _syncing = false;
        }

        /// <summary>선택지에서 값의 위치 (없으면 가장 가까운 칸)</summary>
        static int IndexOf(int[] options, int value)
        {
            int best = 0;
            for (int i = 0; i < options.Length; i++)
            {
                if (Math.Abs(options[i] - value) < Math.Abs(options[best] - value)) best = i;
            }
            return best;
        }

        // ───────────── 일반 ─────────────

        /// <summary>배경음 슬라이더: 끄는 동안 틱 소리 (효과음 크기 그대로 — 배경음 크기는 0.35초 뒤 저장할 때 바뀐다)</summary>
        void OnVolumeChanged(float value)
        {
            if (_syncing) return;
            WriteVolumeLabels();
            _volumeTimer = VolumeCommitDelay;
            // 끄는 동안 틱 소리로 크기를 들려 준다 (UISfxGate가 150ms에 하나로 거른다)
            UISfx.Play(UISfxKey.SliderTick);
        }

        /// <summary>
        /// 효과음 슬라이더 (4.67): 틱을 끄는 중인 크기로 들려 준다 — 저장은 그대로 0.35초 뒤 한 번 (끄는 동안 디스크에 쓰지 않게).
        /// 효과음 버스는 아직 저장된 크기라 틱 배율 = 끄는 크기 ÷ 저장된 크기 (0으로 끌면 틱 없음).
        /// 저장된 크기가 0이면 버스가 막혀 배율로는 들리지 않으므로 그 자리에서 한 번 저장해 연다 (그다음 칸부터는 배율).
        /// </summary>
        void OnSfxVolumeChanged(float value)
        {
            if (_syncing) return;
            WriteVolumeLabels();
            _volumeTimer = VolumeCommitDelay;
            float dragged = Mathf.Clamp01(value / 100f);
            if (!(dragged > 0f)) return;
            float committed = GameSettings.SfxVolume;
            if (!(committed > 0f))
            {
                GameSettings.SfxVolume = dragged;
                committed = dragged;
            }
            UISfx.Play(UISfxKey.SliderTick, dragged / committed);
        }

        /// <summary>
        /// 볼륨 슬라이더 (M18 · AUDIO-SPEC §8.2): 소리가 있는 빌드(UISfx.HasClips)면 그대로, 없으면 α0.5 · 누를 수 없음 + '준비 중' 알약.
        /// 소리 · 진동 스위치는 늘 쓸 수 있다. 줄은 숨기지 않는다.
        /// </summary>
        void RefreshAudio()
        {
            bool live = UISfx.HasClips;
            SetVolumeLive(_bgmSlider, _bgmGroup, _bgmReady, live);
            SetVolumeLive(_sfxSlider, _sfxGroup, _sfxReady, live);
            RefreshVoice(live);
        }

        /// <summary>캐릭터 목소리 줄: 목소리 클립(vox_*)이 하나라도 있으면 그대로, 없으면 스위치 α0.5 · 누를 수 없음 + '준비 중' (M18과 같은 모양)</summary>
        void RefreshVoice(bool anyClips)
        {
            if (_voiceSwitch == null) return;
            _voiceLive = anyClips && HasVoiceClips(UISfx.Library);
            if (_voiceGroup != null)
            {
                _voiceGroup.alpha = _voiceLive ? 1f : AudioUnavailableAlpha;
                _voiceGroup.interactable = _voiceLive;
                _voiceGroup.blocksRaycasts = _voiceLive;
            }
            if (_voiceReady != null) _voiceReady.SetActive(!_voiceLive);
        }

        /// <summary>라이브러리에 캐릭터 목소리 클립(vox_*)이 하나라도 있는지 (테스트용 — 열 때 한 번, 키 수만큼 돈다)</summary>
        internal static bool HasVoiceClips(AudioLibrary library)
        {
            if (library == null || library.entries == null) return false;
            foreach (var entry in library.entries)
            {
                if (entry != null && entry.key != null && entry.key.StartsWith(VoiceKeyPrefix, StringComparison.Ordinal) && entry.HasClips) return true;
            }
            return false;
        }

        static void SetVolumeLive(Slider slider, CanvasGroup group, GameObject ready, bool live)
        {
            if (slider != null) slider.interactable = live;
            if (group != null) group.alpha = live ? 1f : AudioUnavailableAlpha;
            if (ready != null) ready.SetActive(!live);
        }

        void WriteVolumeLabels()
        {
            _bgmValue.text = Mathf.RoundToInt(_bgmSlider.value).ToString();
            _sfxValue.text = Mathf.RoundToInt(_sfxSlider.value).ToString();
        }

        void CommitVolume()
        {
            _volumeTimer = -1f;
            float bgm = _bgmSlider.value / 100f;
            float sfx = _sfxSlider.value / 100f;
            if (!Mathf.Approximately(GameSettings.BgmVolume, bgm)) GameSettings.BgmVolume = bgm;
            if (!Mathf.Approximately(GameSettings.SfxVolume, sfx)) GameSettings.SfxVolume = sfx;
        }

        static void OpenNotices()
        {
            UIManager.Instance.Get<NoticePopup>().OpenList();
        }

        void RefreshTutorial()
        {
            bool canSkip = Game.IsMetaActive && Game.Tutorial != null && SafeTutorialActive();
            _tutorialButton.interactable = canSkip;
            _tutorialLabel.text = "건너뛰기";
            if (_tutorialDesc != null) _tutorialDesc.text = canSkip ? TutorialDescActive : TutorialDescDone;
            // 처음 이야기 다시 보기는 튜토리얼이 끝난 뒤에만 (인트로가 튜토리얼의 일부라 그동안은 누를 수 없다)
            if (_introButton != null) _introButton.interactable = Game.IsMetaActive && Game.Tutorial != null && !SafeTutorialActive();
        }

        /// <summary>처음 이야기 다시 보기: 설정을 닫고 StoryIntroView 다시 보기 (보상 · 깃발 없음 — 받는 쪽 P4-6)</summary>
        void ReplayIntro()
        {
            if (!Game.IsMetaActive || Game.Tutorial == null || SafeTutorialActive()) return;
            Close();
            UINav.ReplayIntro();
        }

        static bool SafeTutorialActive()
        {
            try
            {
                return Game.Tutorial.IsActive;
            }
            catch (NotImplementedException)
            {
                return false;
            }
        }

        /// <summary>튜토리얼 건너뛰기 (M74): 되돌릴 수 없어 위험 확인 ('건너뛰기' / '계속 배우기', 0.6초 잠금)</summary>
        void ConfirmSkipTutorial()
        {
            if (!Game.IsMetaActive || !SafeTutorialActive()) return;
            var tutorial = Game.Tutorial;
            bool beforeFirstMeeting = tutorial.StepIndex <= FirstMeetingStepLimit
                && (Game.Progress == null || !Game.Progress.HasFlag(MetaFlags.FirstMeetingSummoned));
            UIManager.Instance.ShowDangerConfirm("튜토리얼 건너뛰기",
                SkipTutorialBody(beforeFirstMeeting, FtueDirector.FirstMeetingKeptAfterSkip, tutorial.GraduationGiftText),
                "건너뛰기",
                () =>
                {
                    Game.Tutorial.Skip();
                    RefreshTutorial();
                    string message = Game.Tutorial.LastSkipMessage;
                    UIManager.Instance.ShowToast(string.IsNullOrEmpty(message) ? "튜토리얼을 건너뛰었어요" : message, ToastKind.Success);
                },
                "계속 배우기");
        }

        /// <summary>
        /// 건너뛰기 확인 글 (최대 6줄, 테스트용 순수 함수): 남은 말풍선 · 열리는 기능 · (첫 만남 전이면) 첫 만남 소환 안내 · 졸업 선물 · 묻기.
        /// 첫 만남 소환이 건너뛰어도 남으면(FtueDirector.FirstMeetingKeptAfterSkip) 안심 문장, 아니면 주황 경고.
        /// </summary>
        internal static string SkipTutorialBody(bool beforeFirstMeeting, bool firstMeetingKept, string graduationGift)
        {
            var body = new System.Text.StringBuilder(160);
            body.Append("남은 안내 말풍선이 더 나오지 않아요.\n");
            body.Append("편성·시너지는 바로 열려요. 다른 기능은 스테이지를 깨면 열려요.\n");
            if (beforeFirstMeeting)
            {
                body.Append(firstMeetingKept
                    ? "첫 만남 소환은 소환이 열리면 그대로 받을 수 있어요\n"
                    : "<color=#FFB86B>첫 만남 소환(전설 우두커니 확정)을 받을 수 없어요.</color>\n");
            }
            if (!string.IsNullOrEmpty(graduationGift)) body.Append("졸업 선물(").Append(graduationGift).Append(")은 그대로 받아요\n");
            body.Append("건너뛸까요?");
            return body.ToString();
        }

        // ───────────── 전투 연출 ─────────────

        void OnQualityChanged(int index)
        {
            if (index == 0)
            {
                GameSettings.FxQuality = GameSettings.DetectFxQuality();
                GameSettings.FxQualityAuto = true;
            }
            else
            {
                GameSettings.FxQuality = (FxQuality)(index - 1);
            }
            RefreshQuality();
        }

        void RefreshQuality()
        {
            bool auto = GameSettings.FxQualityAuto;
            var quality = GameSettings.FxQuality;
            _qualitySegment.SetIndex(auto ? 0 : 1 + (int)quality);
            string name = QualityNames[Mathf.Clamp((int)quality, 0, QualityNames.Length - 1)];
            _qualityHint.text = auto
                ? $"자동: 지금 {name} (기기에 맞춰 정하고, 화면이 버벅이면 한 단계 낮춰요)"
                : $"직접 고름: {name} · 동시에 보이는 반짝임 {GameSettings.ParticleBudget(quality)}개";
        }

        // ───────────── 알림 권한 (NOTI-01) ─────────────

        /// <summary>
        /// 지금 권한 = 예약 계획(NotificationContext.Capture)과 같은 한 규칙 LocalNotifications.EffectivePermission (P4-7 R2):
        /// 기기가 꺼 두면 Denied, 저장 Denied인데 기기가 켜면 Granted, 나머지는 저장된 값 — 기기가 허용해도(Android 12 이하 · 기기 설정에서 먼저 켬)
        /// 게임 안에서 허락받기 전(Unknown · 미리 묻기 '괜찮아요' = SoftDeclined)이면 '허용하기' (누르면 RequestPermission이 바로 Granted를 저장한다)
        /// </summary>
        internal static NotificationPermission EffectivePermission(NotificationPermission device, NotificationPermission saved) =>
            LocalNotifications.EffectivePermission(device, saved);

        /// <summary>상태 줄 글 (SPEC4 §4.1)</summary>
        internal static string NotifyStatusText(NotificationPermission permission) => permission switch
        {
            NotificationPermission.Granted => NotifyGrantedText,
            NotificationPermission.Denied => NotifyDeniedText,
            _ => NotifyAskText,
        };

        /// <summary>상태 줄 버튼 이름 (켜져 있으면 null = 숨김)</summary>
        internal static string NotifyStatusButtonText(NotificationPermission permission) => permission switch
        {
            NotificationPermission.Granted => null,
            NotificationPermission.Denied => "설정 열기 ›",
            _ => "허용하기",
        };

        NotificationPermission CurrentPermission() => EffectivePermission(LocalNotifications.Permission, GameSettings.NotifyPermission);

        void RefreshNotifyStatus()
        {
            if (_notifyStatusText == null) return;
            var permission = CurrentPermission();
            _notifyStatusText.text = NotifyStatusText(permission);
            _notifyStatusText.color = permission == NotificationPermission.Denied ? UITheme.Lamp : UITheme.SubText;
            if (_notifyStatusButton == null) return;
            string label = NotifyStatusButtonText(permission);
            _notifyStatusButton.gameObject.SetActive(label != null);
            if (label != null && _notifyStatusLabel != null) _notifyStatusLabel.text = label;
            _notifyStatusButton.interactable = !_asking;
        }

        /// <summary>'설정 열기 ›' = 기기 알림 설정, '허용하기' = 시스템 권한 창 (§4.3 5번). 기기 알림이 없는 곳(에디터 · PC)이면 토스트만</summary>
        void OnNotifyStatusButton()
        {
            var permission = CurrentPermission();
            if (permission == NotificationPermission.Granted) return;
            if (permission == NotificationPermission.Denied)
            {
                LocalNotifications.OpenSystemSettings();
                return;
            }
            if (LocalNotifications.Backend == null)
            {
                UIManager.Instance.ShowToast("이 기기에서는 아직 알림을 보낼 수 없어요");
                return;
            }
            if (_asking) return;
            _asking = true;
            RefreshNotifyStatus();
            // 설정 창이 닫혀도 결과는 받게 UIManager에서 돌린다 (LocalNotifications.RequestPermission은 예외를 내지 않고 done을 꼭 한 번 부른다)
            UIManager.Instance.StartCoroutine(LocalNotifications.RequestPermission(OnPermissionResult));
        }

        void OnPermissionResult(NotificationPermission result)
        {
            _asking = false;
            if (result == NotificationPermission.Granted || result == NotificationPermission.Denied) GameSettings.NotifyPermission = result;
            if (result == NotificationPermission.Granted) UIManager.Instance.ShowToast("알림을 켰어요", ToastKind.Success);
            if (this != null && IsOpen) RefreshNotifyStatus();
        }

        // ───────────── 계정·데이터 ─────────────

        void RefreshAccount()
        {
            var account = Game.Account;
            string id = account.AccountId ?? string.Empty;
            _accountText.text = $"{(account.IsGuest ? "게스트 계정" : "연동 계정")} · ID {(id.Length > 8 ? id.Substring(0, 8) : id)}";
            _guestWarning.SetActive(account.IsGuest);
            foreach (var row in _accountRows) row.Refresh();
        }

        /// <summary>공유 버튼 (Android만) · 최근 백업 줄 — 열 때 한 번 (백업 목록은 파일을 읽는다)</summary>
        void RefreshSaveRows()
        {
            if (_shareButton != null) _shareButton.gameObject.SetActive(ShareSheet.CanShare);
            if (_backupDesc == null && _backupButton == null) return;
            var newest = NewestBackup();
            if (_backupDesc != null) _backupDesc.text = BackupLine(newest);
            if (_backupButton != null) _backupButton.interactable = newest != null;
        }

        /// <summary>가장 최근 백업 (없거나 읽지 못하면 null — 목록은 최근 것부터, P4-7)</summary>
        static SaveBackupInfo NewestBackup()
        {
            var list = ListBackups();
            return list != null && list.Count > 0 ? list[0] : null;
        }

        /// <summary>저장소의 백업 목록 (없거나 오류면 null — 예외를 내지 않는다)</summary>
        internal static IReadOnlyList<SaveBackupInfo> ListBackups()
        {
            var storage = GameBootstrap.Instance != null ? GameBootstrap.Instance.Storage : null;
            if (storage == null) return null;
            try
            {
                return storage.ListBackups();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] 백업 목록을 읽지 못했어요: {e.Message}");
                return null;
            }
        }

        /// <summary>백업 줄 설명 '최근 백업: 11월 02일 21:14 · 스테이지 3-4' (없으면 '아직 백업이 없어요', 테스트용 순수 함수)</summary>
        internal static string BackupLine(SaveBackupInfo info)
        {
            if (info == null) return NoBackupText;
            long unix = info.SavedUnix > 0 ? info.SavedUnix : info.Summary.savedUnix;
            string stage = info.Summary.highestStage > 0 ? " · 스테이지 " + BattleSystem.FormatStage(info.Summary.highestStage) : string.Empty;
            return unix > 0 ? $"최근 백업: {SaveImportPopup.ShortTime(unix)}{stage}" : "최근 백업" + stage;
        }

        static void ExportSave()
        {
            if (!Game.IsReady) return;
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.Save();
            string code = SaveCode.Export(Game.Data);
            if (string.IsNullOrEmpty(code)) return;
            GUIUtility.systemCopyBuffer = code;
            MarkBackedUp();
            ShowCodeCopied(code.Length);
        }

        /// <summary>'세이브 코드를 복사했어요' 안내 (내보내기 · 공유 시트를 띄우지 못해 복사로 대신했을 때)</summary>
        static void ShowCodeCopied(int length)
        {
            UIManager.Instance.ShowMessage("세이브 코드를 복사했어요",
                $"지금까지의 진행을 담은 코드({length:N0}자)를 클립보드에 복사했어요.\n메모장 등에 안전하게 보관해 주세요. 다른 사람에게 보여 주면 진행을 가져갈 수 있어요.");
        }

        /// <summary>
        /// 공유 (Android 공유 시트, SPEC4 §4.2) — 공유할 수 없는 곳이면 내보내기(복사)와 같다. 공유 시트를 띄우지 못하면(아주 긴 코드 · 받을 앱 없음)
        /// ShareSheet.TryShare가 복사로 대신하므로 복사 안내를 보인다 (P4-7 요청 2)
        /// </summary>
        static void ShareSave()
        {
            if (!ShareSheet.CanShare)
            {
                ExportSave();
                return;
            }
            if (!Game.IsReady) return;
            if (GameBootstrap.Instance != null) GameBootstrap.Instance.Save();
            string code = SaveCode.Export(Game.Data);
            if (string.IsNullOrEmpty(code)) return;
            bool shared = ShareSheet.TryShare(ShareTitle, code);
            MarkBackedUp();
            if (!shared) ShowCodeCopied(code.Length);
        }

        /// <summary>플레이어가 직접 백업했다 → 백업 알림 시계를 되돌린다 (SB-1, 예외는 기록만)</summary>
        static void MarkBackedUp()
        {
            if (!Game.IsMetaActive || Game.BackupReminder == null) return;
            try
            {
                Game.BackupReminder.MarkBackedUp();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] 백업 표시 실패: {e.Message}");
            }
        }

        /// <summary>가져오기: 다시 빌드한 씬이면 세이브 코드 가져오기 팝업, 아니면 예전 안내</summary>
        static void ImportSave()
        {
            if (UIManager.Instance.TryGet<SaveImportPopup>(out var popup))
            {
                popup.Open();
                return;
            }
            string clipboard = GUIUtility.systemCopyBuffer;
            bool valid = SaveCode.TryDecode(clipboard, out _);
            UIManager.Instance.ShowMessage("세이브 코드 가져오기",
                valid
                    ? "클립보드에 올바른 세이브 코드가 있어요.\n코드로 진행을 옮기는 기능은 곧 열려요. 코드는 그대로 보관해 주세요."
                    : "다른 기기에서 복사한 세이브 코드로 진행을 옮기는 기능은 곧 열려요.\n지금은 [내보내기]로 코드를 미리 보관해 두세요.");
        }

        /// <summary>백업에서 복구 팝업 (백업이 없으면 버튼이 꺼져 있다)</summary>
        static void OpenBackups()
        {
            if (UIManager.Instance.TryGet<BackupRestorePopup>(out var popup)) popup.Open();
            else UIManager.Instance.ShowToast(NoBackupText);
        }

        /// <summary>쿠폰 입력 팝업 (EVT-02)</summary>
        static void OpenCoupon()
        {
            if (UIManager.Instance.TryGet<CouponPopup>(out var popup)) popup.Open();
            else UIManager.Instance.ShowToast("쿠폰 입력은 다음 업데이트에서 열려요");
        }

        /// <summary>데이터 초기화 (M55): 위험 확인 — 확인 버튼 '초기화' (DangerButton, 열린 뒤 0.6초 잠금)</summary>
        static void ConfirmReset()
        {
            UIManager.Instance.ShowDangerConfirm("데이터 초기화", "모든 진행 상황이 삭제되고 처음부터 시작해요.\n되돌릴 수 없어요. 초기화할까요?",
                "초기화", () => GameBootstrap.Instance.ResetData());
        }

        // ───────────── 플레이 기록 보내기 ([계정 · 데이터], D-58 #1) ─────────────

        /// <summary>플레이 기록 팝업 제목 · 기록이 없을 때 본문 · 내보내기 (CR-09 §4.4)</summary>
        internal const string PlayLogTitle = "플레이 기록";
        internal const string PlayLogEmptyText = "아직 기록이 없어요";
        internal const string PlayLogExportLabel = "내보내기";
        internal const string PlayLogCopiedText = "플레이 기록을 복사했어요";

        /// <summary>플레이 기록 팝업 본문 (테스트용 순수 함수): 요약이 비었으면 '아직 기록이 없어요'</summary>
        internal static string PlayLogBody(string summary) => string.IsNullOrWhiteSpace(summary) ? PlayLogEmptyText : summary;

        /// <summary>
        /// '플레이 기록 보내기' (CR-09, SPEC4 §4.4 → D-58 #1: 개발 탭에서 [계정 · 데이터] 탭으로 옮김 — 출시 빌드에도 있음):
        /// PlayLog.Summary(24) → 확인 창 (요약 + 튜토리얼 단계 표). 기록이 있으면 [내보내기] =
        /// 마지막 500줄을 공유 시트로 (공유할 수 없으면 복사 — '플레이 기록을 복사했어요'). 자동으로 올리는 일은 없다. 읽기 실패는 기록만
        /// </summary>
        static void ShowPlayLog()
        {
            string summary = null;
            try
            {
                summary = PlayLog.Summary(24);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] 플레이 기록을 읽지 못했어요: {e.Message}");
            }
            string body = PlayLogBody(summary);
            if (string.IsNullOrWhiteSpace(summary))
            {
                UIManager.Instance.ShowMessage(PlayLogTitle, body);
                return;
            }
            UIManager.Instance.ShowConfirm(PlayLogTitle, body, ExportPlayLog, PlayLogExportLabel, "닫기");
        }

        /// <summary>[내보내기]: 마지막 500줄 → 공유 시트 (못 띄우면 복사 + 토스트)</summary>
        static void ExportPlayLog()
        {
            string tail = null;
            try
            {
                tail = PlayLog.Tail();
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Settings] 플레이 기록을 읽지 못했어요: {e.Message}");
            }
            if (string.IsNullOrEmpty(tail))
            {
                UIManager.Instance.ShowToast(PlayLogEmptyText);
                return;
            }
            if (!ShareSheet.TryShare(PlayLogTitle, tail)) UIManager.Instance.ShowToast(PlayLogCopiedText, ToastKind.Success);
        }

        // ───────────── 개발자 메뉴 (치트 — 에디터 / Development Build 전용) ─────────────
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        static void AddGold()
        {
            double gold = Game.Growth.GoldPerSecond * 600;
            Game.Currency.Add(CurrencyType.Gold, gold);
            UIManager.Instance.ShowToast($"{RewardService.CurrencyName(CurrencyType.Gold)} {RewardService.FormatAmount(CurrencyType.Gold, gold)} 지급");
        }

        static void AddGem()
        {
            const double gem = 1000;
            Game.Currency.Add(CurrencyType.Gem, gem);
            UIManager.Instance.ShowToast($"{RewardService.CurrencyName(CurrencyType.Gem)} {RewardService.FormatAmount(CurrencyType.Gem, gem)} 지급");
        }

        static void SendTestMail()
        {
            Game.Mail.Send("테스트 우편", "개발자 메뉴에서 보낸 우편입니다.", new[]
            {
                RewardData.Currency(CurrencyType.Gem, 100),
                RewardData.Currency(CurrencyType.Gold, Game.Growth.GoldPerSecond * 60),
            });
            UIManager.Instance.ShowToast("테스트 우편을 보냈어요.");
        }

        /// <summary>메타가 켜져 있으면 바구니에 1시간을 더하고 복귀 화면을 요청, 아니면 예전 오프라인 보상</summary>
        void TestOfflineReward()
        {
            Close();
            const long seconds = 3600;
            if (MetaUi.IsActive)
            {
                if (!Game.IdleChest.IsUnlocked)
                {
                    UIManager.Instance.ShowToast("수선 바구니가 아직 잠겨 있어요 ([기능 모두 열기]를 먼저)");
                    return;
                }
                // 재료 줄은 마지막 받기부터 쌓이므로 그만큼 앞당기고, 단추 줄은 꺼 둔 시간으로 더한다
                var chest = Game.Data.meta.idleChest;
                if (chest.lastClaimUnix > 0) chest.lastClaimUnix -= seconds;
                Game.IdleChest.AddOfflineTime(seconds);
                Game.Popups.Enqueue(MetaPopupRequest.Return(seconds));
                return;
            }
            Game.OfflineReward.Calculate(TimeUtil.ToUnix(Game.Clock.UtcNow) - seconds);
            UIManager.Instance.Get<OfflineRewardPopup>().Open();
        }

        /// <summary>
        /// 하루 넘기기: "오늘" 기록들의 날 키를 지난날로 돌려 두고 시계를 다시 읽는다 → 날 바뀜 처리(일일 미션·출석·빠른 수선·두 배 초기화).
        /// 기기 시계는 그대로라 되돌림으로 잡히지 않는다.
        /// </summary>
        static void SkipDay()
        {
            if (!MetaUi.IsActive) return;
            const string past = "2000-01-03";
            var meta = Game.Data.meta;
            meta.clock.lastDayKey = past;
            meta.counters.dayKey = past;
            meta.missions.daily.periodKey = past;
            meta.attendance.lastCheckInDayKey = past;
            meta.idleChest.quickDayKey = past;
            meta.returnScreen.doubleClaimedDayKey = past;
            Game.DailyClock.Refresh();
            Game.Progress.MarkDirty();
            Game.RedDots.MarkDirty();
            if (Game.Attendance.CanCheckIn) Game.Popups.Enqueue(MetaPopupRequest.Attendance());
            UIManager.Instance.ShowToast("하루를 넘겼어요 (오늘 기록 초기화)");
        }

        static void FillChest()
        {
            if (!MetaUi.IsActive) return;
            if (!Game.IdleChest.IsUnlocked) Game.Features.ForceUnlock(FeatureId.IdleChest, false);
            long cap = (long)Game.IdleChest.Cap.TotalSeconds;
            Game.Data.meta.idleChest.lastClaimUnix = Game.DailyClock.NowUnix - cap;
            Game.IdleChest.AddOfflineTime(cap);
            UIManager.Instance.ShowToast("바구니를 가득 채웠어요");
        }

        static void UnlockAllFeatures()
        {
            if (!MetaUi.IsActive) return;
            foreach (FeatureId feature in Enum.GetValues(typeof(FeatureId)))
            {
                if (!Game.Features.IsUnlocked(feature)) Game.Features.ForceUnlock(feature, false);
            }
            UIManager.Instance.ShowToast("모든 기능을 열었어요");
        }

        static void AddTickets()
        {
            if (!MetaUi.IsActive) return;
            Game.MetaRewards.Grant(new[] { MetaReward.QuickIdleTicket(5), MetaReward.DoubleTicket(2), MetaReward.SelectShards(20) });
            UIManager.Instance.ShowToast("빠른 수선권 5 · 두 배 수선권 2 · 선택 기억편 20");
        }
#endif
    }
}
