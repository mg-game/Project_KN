using System;
using UnityEngine;

namespace IdleGame
{
    /// <summary>
    /// 기기별 옵션 (세이브 데이터와 별개로 PlayerPrefs에 저장). 설정 팝업(SET-01) 탭 4개의 값이 모두 여기 있다.
    /// 값을 바꾸면 PlayerPrefs에 저장하고 Apply → Changed를 알린다. 전투 연출(흔들림·번쩍임·숫자·스킬 이름·품질)은
    /// 전투 화면이 읽어 쓴다 (U3, 바뀔 때 Changed를 받아 다시 읽음). 소리 켜짐·배경음/효과음 볼륨은 Apply가 UISfx에 밀어 주고,
    /// 진동은 Haptics.Play가 매번 확인한다 (M18).
    /// </summary>
    public static class GameSettings
    {
        const string SoundKey = "settings.sound";
        const string BgmVolumeKey = "settings.bgm_volume";
        const string SfxVolumeKey = "settings.sfx_volume";
        const string HapticsKey = "settings.haptics";
        const string SleepMinutesKey = "settings.sleep_minutes";
        const string SheetAutoReturnKey = "settings.sheet_auto_return";
        const string FrameRateKey = "settings.frame_rate";
        const string ShakeKey = "settings.shake_percent";
        const string ReduceFlashKey = "settings.reduce_flash";
        const string DamageNumbersKey = "settings.damage_numbers";
        const string SkillNamesKey = "settings.skill_names";
        const string FxQualityKey = "settings.fx_quality";
        const string FxQualityAutoKey = "settings.fx_quality_auto";
        const string NotifyKeyPrefix = "settings.notify.";
        // 4차 (SPEC4 §2.10 · AUDIO-SPEC §8.2) — 알림 종류 이름(NotificationKind)과 겹치지 않는 이름
        const string NotifyPermissionKey = NotifyKeyPrefix + "permission";
        const string NotifySoftAsksKey = NotifyKeyPrefix + "soft_asks";
        const string NotifyLastAskUnixKey = NotifyKeyPrefix + "last_ask_unix";
        const string NotifyMasterKey = NotifyKeyPrefix + "master";
        const string VoiceBarksKey = "settings.voice_barks";

        /// <summary>잠자기 모드 선택지 (분, 0 = 끄기)</summary>
        public static readonly int[] SleepMinuteOptions = { 0, 1, 2, 3, 5 };
        /// <summary>화면 흔들림 선택지 (%)</summary>
        public static readonly int[] ShakeOptions = { 0, 50, 100 };
        /// <summary>프레임 선택지</summary>
        public static readonly int[] FrameRateOptions = { 30, 60 };

        /// <summary>설정 값이 바뀜 (Apply 뒤)</summary>
        public static event Action Changed;

        // ── 일반 ──

        /// <summary>전체 소리 켜기 (끄면 볼륨과 상관없이 무음)</summary>
        public static bool SoundOn
        {
            get => PlayerPrefs.GetInt(SoundKey, 1) == 1;
            set => SetInt(SoundKey, value ? 1 : 0);
        }

        /// <summary>배경음 볼륨 0~1 (기본 0.7)</summary>
        public static float BgmVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(BgmVolumeKey, 0.7f));
            set => SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
        }

        /// <summary>효과음 볼륨 0~1 (기본 0.8)</summary>
        public static float SfxVolume
        {
            get => Mathf.Clamp01(PlayerPrefs.GetFloat(SfxVolumeKey, 0.8f));
            set => SetFloat(SfxVolumeKey, Mathf.Clamp01(value));
        }

        /// <summary>진동 (FX-05, 기본 켜짐). Haptics.Play가 매번 읽는다 — 큰 순간에만 짧게 (불 밝힘·보스 처치·높은 등급 소환·레벨업·막힘)</summary>
        public static bool Haptics
        {
            get => PlayerPrefs.GetInt(HapticsKey, 1) == 1;
            set => SetInt(HapticsKey, value ? 1 : 0);
        }

        /// <summary>잠자기 모드까지 입력 없는 시간 (분, 0 = 끄기, 기본 2) — SleepMinuteOptions 중 하나 (IDLE-06)</summary>
        public static int SleepMinutes
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(SleepMinutesKey, 2));
            set => SetInt(SleepMinutesKey, Mathf.Max(0, value));
        }

        /// <summary>
        /// 전투 화면으로 돌아가기 (UX-SPEC-IA §4.8, 기본 켜짐): 탭 시트를 연 채 UIMotion.SheetAutoReturnSeconds(60초) 동안 손대지 않으면
        /// 시트를 내려 전투를 크게 보여 준다 (SheetController). 설정 줄 '전투 화면으로 돌아가기' / '1분 동안 손대지 않으면 열린 창을 내려요'.
        /// 도구 · 테스트(AutoShot, PlayMode 묶음)는 시작할 때 끄고 끝날 때 되돌린다 — 시간에 따라 장면이 바뀌지 않게 (CR-04).
        /// PlayerPrefs라 매 프레임 읽지 말고 Changed에서 다시 읽어 둔다.
        /// </summary>
        public static bool SheetAutoReturn
        {
            get => PlayerPrefs.GetInt(SheetAutoReturnKey, 1) == 1;
            set => SetInt(SheetAutoReturnKey, value ? 1 : 0);
        }

        /// <summary>목표 프레임 30/60 (기본 60)</summary>
        public static int FrameRate
        {
            get => PlayerPrefs.GetInt(FrameRateKey, 60) <= 30 ? 30 : 60;
            set => SetInt(FrameRateKey, value <= 30 ? 30 : 60);
        }

        // ── 전투 연출 ──

        /// <summary>화면 흔들림 0 / 50 / 100% (FX-02, 기본 100)</summary>
        public static int ShakePercent
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(ShakeKey, 100), 0, 100);
            set => SetInt(ShakeKey, Mathf.Clamp(value, 0, 100));
        }

        /// <summary>흔들림 배율 0~1</summary>
        public static float ShakeScale => ShakePercent / 100f;

        /// <summary>
        /// 번쩍임 줄이기 (설정 이름 그대로, 기본 꺼짐) — 전투·뽑기·축하 연출의 번쩍임과 움직임을 함께 줄이는 눈 보호 옵션 (M64).
        ///   · 화면 덮개 번쩍임 알파 ≤ ReducedFlashMaxAlpha(0.3) — ClampFlashAlpha (전투 연출, 소환 카드 공개 GachaCardView.Flash)
        ///   · 소환 공개 파편 수 절반, 축하 연출(CelebrationFx) 빛줄기 흐리게·회전 없음·숨쉬기 없음·불꽃/색종이 일부만,
        ///     메뉴 번쩍임(MenuFx.Flash) 한 번만·느리게·알파 ≤ 0.3, 팝업 열고 닫기는 크기 변화 없이 짧은 페이드만 (UIMotion.Reduced)
        ///   · 화면 흔들림은 따로 (ShakePercent)
        /// </summary>
        public static bool ReduceFlash
        {
            get => PlayerPrefs.GetInt(ReduceFlashKey, 0) == 1;
            set => SetInt(ReduceFlashKey, value ? 1 : 0);
        }

        /// <summary>번쩍임 줄이기가 켜졌을 때 화면 덮개 알파 상한</summary>
        public const float ReducedFlashMaxAlpha = 0.3f;

        /// <summary>화면 덮개 번쩍임 알파를 설정에 맞게 자른다</summary>
        public static float ClampFlashAlpha(float alpha) => ReduceFlash ? Mathf.Min(alpha, ReducedFlashMaxAlpha) : alpha;

        /// <summary>피해 숫자 모두 / 치명타만 / 끄기 (FX-03, 기본 모두)</summary>
        public static DamageNumberMode DamageNumbers
        {
            get => (DamageNumberMode)Mathf.Clamp(PlayerPrefs.GetInt(DamageNumbersKey, 0), 0, 2);
            set => SetInt(DamageNumbersKey, (int)value);
        }

        /// <summary>스킬 이름 표시 (FX-06, 기본 켜짐)</summary>
        public static bool SkillNames
        {
            get => PlayerPrefs.GetInt(SkillNamesKey, 1) == 1;
            set => SetInt(SkillNamesKey, value ? 1 : 0);
        }

        /// <summary>
        /// 이펙트 품질 (ATK-06). FxQualityAuto면 첫 실행 때 기기 메모리로 정한 값(DetectFxQuality)이고,
        /// 손으로 바꾸면 자동 판정을 끈다 (CR-08).
        /// </summary>
        public static FxQuality FxQuality
        {
            get => PlayerPrefs.HasKey(FxQualityKey)
                ? (FxQuality)Mathf.Clamp(PlayerPrefs.GetInt(FxQualityKey), 0, 2)
                : DetectFxQuality();
            set
            {
                PlayerPrefs.SetInt(FxQualityAutoKey, 0);
                SetInt(FxQualityKey, (int)value);
            }
        }

        /// <summary>이펙트 품질 자동 판정 (기본 켜짐). 실행 중 프레임이 계속 낮으면 한 단계 낮출 수 있다 (LowerFxQualityAuto)</summary>
        public static bool FxQualityAuto
        {
            get => PlayerPrefs.GetInt(FxQualityAutoKey, 1) == 1;
            set => SetInt(FxQualityAutoKey, value ? 1 : 0);
        }

        /// <summary>품질별 동시 파티클 상한 (낮음 120 / 보통 200 / 높음 340)</summary>
        public static int ParticleBudget(FxQuality quality) => quality switch
        {
            FxQuality.Low => 120,
            FxQuality.Medium => 200,
            _ => 340,
        };

        /// <summary>기기 메모리·코어 수로 정한 기본 품질 (3GB 미만 낮음, 6GB 미만 보통, 그 이상 높음, 코어 4개 이하면 한 단계 낮춤)</summary>
        public static FxQuality DetectFxQuality()
        {
            int memory = SystemInfo.systemMemorySize;
            var quality = memory < 3072 ? FxQuality.Low : memory < 6144 ? FxQuality.Medium : FxQuality.High;
            if (SystemInfo.processorCount <= 4 && quality > FxQuality.Low) quality--;
            return quality;
        }

        /// <summary>
        /// 자동 판정 중이면 한 단계 낮춘다 (실행 중 프레임 저하, CR-08). 낮췄으면 true — 부른 쪽이 토스트를 한 번 띄운다
        /// ("화면이 부드럽도록 이펙트를 조금 줄였어요"). 자동으로 다시 올리지는 않는다.
        /// </summary>
        public static bool LowerFxQualityAuto()
        {
            if (!FxQualityAuto) return false;
            var current = FxQuality;
            if (current == FxQuality.Low) return false;
            SetIntNoFlush(FxQualityKey, (int)current - 1);
            return true;
        }

        /// <summary>
        /// PlayerPrefs.Save 없이 바꾼다 (perf PF-6 H5 — 전투 중 자동 판정은 이미 느린 구간이라 디스크 쓰기를 더하지 않는다).
        /// Unity가 종료 때, GameBootstrap이 일시정지 때 PlayerPrefs를 디스크에 쓴다
        /// </summary>
        static void SetIntNoFlush(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            Apply();
            Changed?.Invoke();
        }

        // ── 알림 (NOTI-01 예약) ──

        public static bool GetNotify(NotificationKind kind) => PlayerPrefs.GetInt(NotifyKeyPrefix + kind, 1) == 1;

        public static void SetNotify(NotificationKind kind, bool on) => SetInt(NotifyKeyPrefix + kind, on ? 1 : 0);

        /// <summary>알림 전체 (기본 켜짐). 끄면 종류별 스위치와 상관없이 예약하지 않는다 (NOTI-01 — 실제로 켜진 알림 = NotifyMaster && GetNotify(종류))</summary>
        public static bool NotifyMaster
        {
            get => PlayerPrefs.GetInt(NotifyMasterKey, 1) == 1;
            set => SetInt(NotifyMasterKey, value ? 1 : 0);
        }

        /// <summary>알림 권한 (게임 안 미리 묻기 · 시스템 창 결과, 기본 Unknown). 기기 설정에서 바뀌면 플랫폼 백엔드가 다시 읽어 맞춘다</summary>
        public static NotificationPermission NotifyPermission
        {
            get => (NotificationPermission)Mathf.Clamp(PlayerPrefs.GetInt(NotifyPermissionKey, 0), 0, 3);
            set => SetInt(NotifyPermissionKey, (int)value);
        }

        /// <summary>게임 안 미리 묻기를 '괜찮아요'로 닫은 횟수 (2번까지만 묻는다)</summary>
        public static int NotifySoftAsks
        {
            get => Mathf.Max(0, PlayerPrefs.GetInt(NotifySoftAsksKey, 0));
            set => SetInt(NotifySoftAsksKey, Mathf.Max(0, value));
        }

        /// <summary>마지막으로 알림을 물어본 시각 (유닉스 초, 0 = 없음 — 7일 안에는 다시 묻지 않는다). PlayerPrefs에 long이 없어 문자열로 저장</summary>
        public static long NotifyLastAskUnix
        {
            get => long.TryParse(PlayerPrefs.GetString(NotifyLastAskUnixKey, "0"), System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture, out long value) && value > 0 ? value : 0;
            set
            {
                PlayerPrefs.SetString(NotifyLastAskUnixKey, Math.Max(0, value).ToString(System.Globalization.CultureInfo.InvariantCulture));
                Commit();
            }
        }

        // ── 소리 (AUDIO-SPEC §2.10 · §8.2) ──

        /// <summary>
        /// 캐릭터 목소리 (짧은 말소리, 기본 꺼짐 — 목소리 약관 확인 전까지 조용히, 로드맵 Q12). Apply가 AudioService.VoiceBarks = 소리 켜짐 && 이 값으로 밀어 준다.
        /// 설정 줄 '캐릭터 목소리'는 설정 화면 담당(P4-4)이 붙인다
        /// </summary>
        public static bool VoiceBarks
        {
            get => PlayerPrefs.GetInt(VoiceBarksKey, 0) == 1;
            set => SetInt(VoiceBarksKey, value ? 1 : 0);
        }

        // ── 적용 ──

        /// <summary>소리 / 프레임을 적용한다 (부팅 때 GameBootstrap이 한 번, 값이 바뀔 때마다). 볼륨은 UISfx로, 캐릭터 목소리는 AudioService로 밀어 준다</summary>
        public static void Apply()
        {
            AudioListener.volume = SoundOn ? 1f : 0f;
            // 프레임 페이싱의 주인 (perf PF-1): vSyncCount 0 + 30/60 (모바일은 주사율 / n), 잠자기면 30
            FramePacing.Apply();
            AudioService.VoiceBarks = SoundOn && VoiceBarks;
            UISfx.ApplySettings();
        }

        static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            Commit();
        }

        static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            Commit();
        }

        static void Commit()
        {
            PlayerPrefs.Save();
            Apply();
            Changed?.Invoke();
        }
    }
}
