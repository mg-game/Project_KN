using System;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 세이브 코드 가져오기 (SAVE-01, SPEC4 §4.2 — 설정 [계정·데이터] '가져오기'가 연다, 820x900).
    ///   · [클립보드에서 붙여 넣기] → SaveCode.TryParse(코드, …, 요약) → 미리보기 '스테이지 3-4 · 수선점 Lv.87 · 친구 6명' / '저장 2026-11-02 21:14 · ID 1a2b3c4d'.
    ///     읽지 못하면 이유 한 줄 (Negative 24 — 잘림 · 바뀜 / 새 버전 / 읽을 수 없음). 큰 입력 칸은 두지 않는다 (코드는 100KB까지 된다).
    ///   · [이 진행으로 바꾸기] (위험, 읽은 뒤에만) → 위험 확인 '진행을 바꿀까요?' → 저장소 Replace(지금 진행은 백업으로) → GameBootstrap.ReloadAfterReplace.
    ///   · 저장소 · 읽기 오류는 예외를 내지 않고 토스트 · 경고 한 줄로 끝낸다 (킬 스위치 — 새 코드는 던지지 않는다). 읽은 진행은 창이 닫히면 잊는다.
    /// </summary>
    public class SaveImportPopup : UIPopup
    {
        public const string HintEmpty = "코드를 붙여 넣으면 여기에 진행이 보여요";
        internal const string ErrorEmpty = "클립보드가 비어 있어요. 코드를 먼저 복사해 주세요";
        internal const string ErrorBroken = "코드가 잘렸거나 바뀌었어요. 다시 복사해 주세요";
        internal const string ErrorNewer = "더 새 버전에서 만든 코드예요. 앱을 업데이트해 주세요";
        internal const string ErrorUnreadable = "이 코드는 읽을 수 없어요";
        /// <summary>바꾸기 실패 (저장소 Replace가 false — 지금 진행을 백업하지 못해 아무것도 바뀌지 않음)</summary>
        internal const string ReplaceFailedText = "진행을 바꾸지 못했어요. 지금 진행은 그대로예요";
        /// <summary>저장소에 남기는 백업 이름 (player.json.import-{unix}.bak, P4-7)</summary>
        const string ReplaceReason = "import";

        [SerializeField] Button _pasteButton;
        [Tooltip("미리보기 두 줄 (요약 · 저장 시각) — 읽은 뒤에만")]
        [SerializeField] GameObject _previewGroup;
        [SerializeField] TMP_Text _summaryText;
        [SerializeField] TMP_Text _savedText;
        [Tooltip("안내 · 오류 한 줄 (미리보기 판 가운데)")]
        [SerializeField] TMP_Text _hintText;
        [Tooltip("[이 진행으로 바꾸기] (위험 — 읽은 뒤에만 누를 수 있다)")]
        [SerializeField] Button _replaceButton;

        PlayerData _parsed;

        protected override void Awake()
        {
            base.Awake();
            if (_pasteButton != null) _pasteButton.onClick.AddListener(Paste);
            if (_replaceButton != null) _replaceButton.onClick.AddListener(ConfirmReplace);
        }

        protected override void OnOpen()
        {
            _parsed = null;
            ShowHint(HintEmpty, false);
        }

        protected override void OnClose() => _parsed = null;

        /// <summary>오류 한 줄 (SPEC4 §4.2, 테스트용 순수 함수)</summary>
        internal static string ErrorText(SaveCodeError error) => error switch
        {
            SaveCodeError.Empty => ErrorEmpty,
            SaveCodeError.NewerVersion => ErrorNewer,
            SaveCodeError.TooLarge => ErrorUnreadable,
            SaveCodeError.Corrupt => ErrorUnreadable,
            _ => ErrorBroken,
        };

        /// <summary>미리보기 첫 줄 '스테이지 3-4 · 수선점 Lv.87 · 친구 6명' (테스트용 순수 함수)</summary>
        internal static string SummaryLine(SaveSummary summary) =>
            $"스테이지 {BattleSystem.FormatStage(Math.Max(1, summary.highestStage))} · 수선점 Lv.{Math.Max(1, summary.shopLevel)} · 친구 {Math.Max(0, summary.lineages)}명";

        /// <summary>미리보기 둘째 줄 '저장 2026-11-02 21:14 · ID 1a2b3c4d' (시각 · ID가 없으면 그 부분을 뺀다, 테스트용)</summary>
        internal static string SavedLine(SaveSummary summary)
        {
            bool hasTime = summary.savedUnix > 0;
            bool hasId = !string.IsNullOrEmpty(summary.accountIdShort);
            string time = hasTime ? "저장 " + LongTime(summary.savedUnix) : string.Empty;
            if (!hasId) return time;
            return hasTime ? $"{time} · ID {summary.accountIdShort}" : "ID " + summary.accountIdShort;
        }

        /// <summary>'2026-11-02 21:14' (기기 현지 시각)</summary>
        internal static string LongTime(long unix) =>
            DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime().ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture);

        /// <summary>'11월 02일 21:14' (기기 현지 시각 — 설정 백업 줄 · 백업 목록)</summary>
        internal static string ShortTime(long unix)
        {
            var t = DateTimeOffset.FromUnixTimeSeconds(unix).ToLocalTime();
            return t.ToString("MM", CultureInfo.InvariantCulture) + "월 " + t.ToString("dd", CultureInfo.InvariantCulture) + "일 "
                + t.ToString("HH:mm", CultureInfo.InvariantCulture);
        }

        void Paste()
        {
            string code = GUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(code))
            {
                Fail(SaveCodeError.Empty);
                return;
            }
            bool ok;
            PlayerData data;
            SaveCodeError error;
            SaveSummary summary;
            try
            {
                ok = SaveCode.TryParse(code, out data, out error, out summary);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveImport] 세이브 코드를 읽지 못했어요: {e.Message}");
                ok = false;
                data = null;
                error = SaveCodeError.Corrupt;
                summary = default;
            }
            if (!ok || data == null)
            {
                Fail(error);
                return;
            }
            _parsed = data;
            if (_hintText != null) _hintText.gameObject.SetActive(false);
            if (_previewGroup != null) _previewGroup.SetActive(true);
            if (_summaryText != null) _summaryText.text = SummaryLine(summary);
            if (_savedText != null) _savedText.text = SavedLine(summary);
            if (_replaceButton != null) _replaceButton.interactable = true;
            UISfx.Play(UISfxKey.Confirm);
        }

        void Fail(SaveCodeError error)
        {
            _parsed = null;
            ShowHint(ErrorText(error), true);
            UISfx.Play(UISfxKey.Deny);
        }

        void ShowHint(string text, bool error)
        {
            if (_previewGroup != null) _previewGroup.SetActive(false);
            if (_hintText != null)
            {
                _hintText.gameObject.SetActive(true);
                _hintText.text = text;
                _hintText.color = error ? UITheme.Negative : UITheme.SubText;
            }
            if (_replaceButton != null) _replaceButton.interactable = false;
        }

        void ConfirmReplace()
        {
            if (_parsed == null) return;
            var data = _parsed;
            UIManager.Instance.ShowDangerConfirm("진행을 바꿀까요?",
                "지금 진행은 백업으로 남겨 두고 코드의 진행으로 바꿔요.\n바꾼 뒤에는 게임이 다시 시작돼요.",
                "바꾸기", () => Replace(data));
        }

        /// <summary>
        /// 저장소에 바꿔 쓰고(지금 진행은 import 백업으로) 바꿨을 때만 onReplaced(창 닫기 · 다시 시작)를 부른다 (테스트용 — P4-7 요청 1).
        /// 저장소 · 진행이 없거나, Replace가 false(지금 진행을 백업하지 못해 아무것도 바뀌지 않음)거나 예외를 내면 false — onReplaced는 부르지 않는다.
        /// 예외를 밖으로 내지 않는다 (경고 한 줄)
        /// </summary>
        internal static bool TryReplace(ISaveStorage storage, PlayerData data, Action onReplaced = null)
        {
            if (storage == null || data == null) return false;
            bool replaced;
            try
            {
                replaced = storage.Replace(data, ReplaceReason);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveImport] 진행을 바꾸지 못했어요: {e.Message}");
                replaced = false;
            }
            if (replaced) onReplaced?.Invoke();
            return replaced;
        }

        /// <summary>
        /// 저장소에 바꿔 쓰고 다시 시작한다. 저장소가 없거나 실패하면 토스트만 —
        /// Replace가 false면 지금 진행을 백업하지 못해 아무것도 바뀌지 않았으므로 다시 시작하지 않는다 (TryReplace)
        /// </summary>
        void Replace(PlayerData data)
        {
            var boot = GameBootstrap.Instance;
            var storage = boot != null ? boot.Storage : null;
            if (storage == null)
            {
                UIManager.Instance.ShowToast("지금은 진행을 바꿀 수 없어요", ToastKind.Error);
                return;
            }
            if (TryReplace(storage, data, () =>
                {
                    _parsed = null;
                    Close();
                    boot.ReloadAfterReplace();
                })) return;
            UIManager.Instance.ShowToast(ReplaceFailedText, ToastKind.Error);
        }
    }
}
