using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 백업에서 복구 (SAVE-01, SPEC4 §4.2 — 설정 [계정·데이터] '복구'가 연다, 820x1000).
    ///   · 저장소 ListBackups(최근 것부터 — 매일 백업 · 가져오기 전 · 마지막 저장 직전)를 미리 만든 줄(740x120)에 채운다: 이름(SaveBackupInfo.Label,
    ///     '어제' / '그저께' / '가져오기 전' / '마지막 저장 직전') + 요약 '11월 02일 21:14 · 스테이지 3-4 · 수선점 Lv.87' + 보조 '복구'(위험 색 글자).
    ///     줄보다 백업이 많으면 최근 것만, 없으면 '아직 백업이 없어요'.
    ///   · '복구' → 위험 확인 '이 백업으로 되돌릴까요? 지금 진행은 따로 남겨 둬요.' → RestoreBackup(경로) → 성공이면 GameBootstrap.ReloadAfterReplace, 아니면 토스트.
    ///   · 저장소 오류는 예외를 내지 않는다 (경고 한 줄 + 빈 목록 / 토스트).
    /// </summary>
    public class BackupRestorePopup : UIPopup
    {
        internal const string FallbackLabel = "백업";

        [SerializeField] GameObject[] _rows = Array.Empty<GameObject>();
        [SerializeField] TMP_Text[] _rowLabels = Array.Empty<TMP_Text>();
        [SerializeField] TMP_Text[] _rowSummaries = Array.Empty<TMP_Text>();
        [SerializeField] Button[] _rowButtons = Array.Empty<Button>();
        [Tooltip("백업이 없을 때 '아직 백업이 없어요'")]
        [SerializeField] GameObject _emptyLabel;

        /// <summary>줄마다 보여 준 백업 (같은 순서 — '복구'가 고른다)</summary>
        readonly List<SaveBackupInfo> _shown = new();

        protected override void Awake()
        {
            base.Awake();
            for (int i = 0; i < _rowButtons.Length; i++)
            {
                int index = i;
                if (_rowButtons[i] != null) _rowButtons[i].onClick.AddListener(() => ConfirmRestore(index));
            }
        }

        protected override void OnOpen() => Refresh();

        protected override void OnClose() => _shown.Clear();

        /// <summary>목록을 다시 읽어 줄을 채운다 (열 때 한 번 — 파일을 읽는다)</summary>
        void Refresh()
        {
            _shown.Clear();
            var list = SettingsPopup.ListBackups();
            int count = list != null ? list.Count : 0;
            for (int i = 0; i < count && _shown.Count < _rows.Length; i++)
            {
                if (list[i] != null) _shown.Add(list[i]);
            }
            for (int i = 0; i < _rows.Length; i++)
            {
                bool has = i < _shown.Count;
                if (_rows[i] != null && _rows[i].activeSelf != has) _rows[i].SetActive(has);
                if (!has) continue;
                var info = _shown[i];
                if (i < _rowLabels.Length && _rowLabels[i] != null) _rowLabels[i].text = string.IsNullOrEmpty(info.Label) ? FallbackLabel : info.Label;
                if (i < _rowSummaries.Length && _rowSummaries[i] != null) _rowSummaries[i].text = RowSummary(info);
                if (i < _rowButtons.Length && _rowButtons[i] != null) _rowButtons[i].interactable = !string.IsNullOrEmpty(info.Path);
            }
            if (_emptyLabel != null) _emptyLabel.SetActive(_shown.Count == 0);
        }

        /// <summary>줄 요약 '11월 02일 21:14 · 스테이지 3-4 · 수선점 Lv.87' (시각이 없으면 뺀다, 테스트용 순수 함수)</summary>
        internal static string RowSummary(SaveBackupInfo info)
        {
            if (info == null) return string.Empty;
            long unix = info.SavedUnix > 0 ? info.SavedUnix : info.Summary.savedUnix;
            var summary = info.Summary;
            string stage = $"스테이지 {BattleSystem.FormatStage(Math.Max(1, summary.highestStage))} · 수선점 Lv.{Math.Max(1, summary.shopLevel)}";
            return unix > 0 ? SaveImportPopup.ShortTime(unix) + " · " + stage : stage;
        }

        void ConfirmRestore(int index)
        {
            if (index < 0 || index >= _shown.Count) return;
            string path = _shown[index].Path;
            if (string.IsNullOrEmpty(path)) return;
            UIManager.Instance.ShowDangerConfirm("백업에서 복구", "이 백업으로 되돌릴까요? 지금 진행은 따로 남겨 둬요.", "되돌리기", () => Restore(path));
        }

        void Restore(string path)
        {
            var boot = GameBootstrap.Instance;
            var storage = boot != null ? boot.Storage : null;
            bool ok = false;
            if (storage != null)
            {
                try
                {
                    ok = storage.RestoreBackup(path);
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[Backup] 백업을 되돌리지 못했어요: {e.Message}");
                    ok = false;
                }
            }
            if (!ok)
            {
                UIManager.Instance.ShowToast("이 백업은 되돌릴 수 없어요. 지금 진행은 그대로예요", ToastKind.Error);
                Refresh();
                return;
            }
            Close();
            boot.ReloadAfterReplace();
        }
    }
}
