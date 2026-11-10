using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using IdleGame.UI;
using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 사운드 시험장 (AUDIO-SPEC §6) — 사용자가 귀로 테이크를 고르는 창. 편집 · 재생 모드 모두 된다.
    ///   · 왼쪽: 모든 카탈로그 키 (§2 묶음), 상태 점 (● 됨 · ◐ 테이크 모자람 · ○ 없음 · ⚠ QC 실패), 켜 둔/전체 테이크 수, 평균 별점
    ///   · 오른쪽: 설명 · 거는 곳 · 버스/등급/음량 단계 · 길이/목표 음량 · 게이트 · 사다리, 볼륨 손질(−12…0 dB), QC 추천으로 되돌리기, 메모,
    ///     테이크 줄 (▶ ■ · 켜기 · ★1~5 · 파형 · QC 한 줄 · 만든 경로)
    ///   · 재생: 숨은 미리 듣기 소스 (런타임과 같은 볼륨 단계 · 손질 · 흔들기 · 섞어 뽑기 · 사다리), 런타임 규칙으로 재생, 사다리 ×8, 연타 테스트,
    ///     루프 이음매, 전투 미리 듣기 10초 (×2), 블라인드 A/B, 재생 모드면 게임 안에서 재생
    /// 쓰는 것은 Assets/_Project/Audio/AudioSelection.asset (그리고 빌더로 라이브러리)뿐이다.
    /// </summary>
    public sealed class AudioAuditionWindow : EditorWindow
    {
        static readonly string[] GroupNames =
        {
            "전체", "UI", "아군 공격", "적·보스", "수선점·보호막·잔향이·불빛", "스킬", "상태", "흐름", "환경음", "배경음", "징글", "목소리", "예약", "별칭",
        };

        static readonly string[] StageNames = { "전체", "P0", "P1", "P2", "P3" };

        static AudioAuditionWindow _open;

        string _search = string.Empty;
        int _group;
        int _stage;
        bool _problemsOnly;
        float _previewVolume = 1f;
        bool _bed;
        bool _fast;
        int _selected = -1;
        Vector2 _listScroll;
        Vector2 _detailScroll;

        Dictionary<string, List<AudioDataBuilder.FoundTake>> _found = new();
        AudioQcData _qc = new();
        AudioSelection _selection;
        AudioDataBuilder.Report _report;
        bool _needsReload = true;
        double _rebuildAt = -1;

        PreviewPlayer _player;
        int _abA = -1;
        int _abB = -1;
        int _abRounds;
        string _status = string.Empty;

        GUIStyle _dot;
        GUIStyle _wrap;

        [MenuItem("IdleGame/Audio/사운드 시험장", priority = 0)]
        public static void Open()
        {
            var window = GetWindow<AudioAuditionWindow>("사운드 시험장");
            window.minSize = new Vector2(820f, 480f);
            window.Show();
        }

        /// <summary>자동 다시 만들기가 끝났을 때 (열려 있으면 목록을 다시 읽는다)</summary>
        public static void NotifyRebuilt()
        {
            if (_open == null) return;
            _open._needsReload = true;
            _open.Repaint();
        }

        void OnEnable()
        {
            _open = this;
            _needsReload = true;
            EditorApplication.update += OnEditorUpdate;
        }

        void OnDisable()
        {
            EditorApplication.update -= OnEditorUpdate;
            if (_player != null)
            {
                _player.Dispose();
                _player = null;
            }
            SaveSelection();
            if (_open == this) _open = null;
        }

        void OnEditorUpdate()
        {
            if (_player != null && _player.Tick()) Repaint();
            if (_rebuildAt > 0 && EditorApplication.timeSinceStartup >= _rebuildAt && !EditorApplication.isCompiling && !EditorApplication.isUpdating)
            {
                _rebuildAt = -1;
                Rebuild(false);
            }
        }

        PreviewPlayer Player => _player ??= new PreviewPlayer();

        void Reload()
        {
            _needsReload = false;
            _report = new AudioDataBuilder.Report();
            _found = AudioDataBuilder.FindTakes(_report);
            _qc = AudioQcData.Load(AudioDataBuilder.AudioRoot);
            _selection = AudioSelection.Load(false);
            Player.SetLibrary(UISfx.Library ?? AssetDatabase.LoadAssetAtPath<AudioLibrary>(AudioDataBuilder.LibraryPath));
        }

        void Rebuild(bool log)
        {
            SaveSelection();
            var library = AudioDataBuilder.Build();
            AssetDatabase.SaveAssets();
            if (EditorApplication.isPlaying) AudioService.ReloadLibrary();
            if (log && AudioDataBuilder.LastReport != null) Debug.Log(AudioDataBuilder.LastReport.ToText());
            _needsReload = true;
            Player.SetLibrary(library);
            _status = "라이브러리를 다시 만들었어요";
        }

        void QueueRebuild() => _rebuildAt = EditorApplication.timeSinceStartup + 0.4;

        void SaveSelection()
        {
            if (_selection == null || !EditorUtility.IsDirty(_selection)) return;
            AssetDatabase.SaveAssetIfDirty(_selection);
        }

        AudioSelection.KeyChoice Choice(string key, bool create)
        {
            if (_selection == null && create) _selection = AudioSelection.Load(true);
            if (_selection == null) return null;
            return create ? _selection.GetOrAdd(key) : _selection.Find(key);
        }

        void MarkSelectionDirty()
        {
            if (_selection == null) return;
            EditorUtility.SetDirty(_selection);
            QueueRebuild();
        }

        // ───────────── 그리기 ─────────────

        void OnGUI()
        {
            if (_needsReload) Reload();
            _dot ??= new GUIStyle(EditorStyles.label) { fontSize = 12, alignment = TextAnchor.MiddleCenter, fixedWidth = 18f };
            _wrap ??= new GUIStyle(EditorStyles.label) { wordWrap = true };
            DrawToolbar();
            // 미리 듣기는 게임 설정 볼륨(소리 켜기 · 효과음 · 배경음)을 따른다 — 꺼져 있으면 조용한 이유를 알려 준다
            if (UISfx.SfxGain <= 0f || UISfx.BgmGain <= 0f)
                EditorGUILayout.HelpBox(GameSettings.SoundOn
                        ? "게임 설정의 효과음 또는 배경음 볼륨이 0이에요 — 그 소리는 미리 듣기도 조용해요 (설정 팝업에서 올려 주세요)."
                        : "게임 설정에서 소리가 꺼져 있어요 — 미리 듣기도 조용해요 (설정 팝업에서 소리를 켜 주세요).", MessageType.Warning);
            EditorGUILayout.BeginHorizontal();
            DrawList();
            DrawDetail();
            EditorGUILayout.EndHorizontal();
            if (!string.IsNullOrEmpty(_status)) EditorGUILayout.LabelField(_status, EditorStyles.miniLabel);
        }

        void DrawToolbar()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            _search = EditorGUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.Width(170f));
            _group = EditorGUILayout.Popup(_group, GroupNames, EditorStyles.toolbarPopup, GUILayout.Width(150f));
            _stage = EditorGUILayout.Popup(_stage, StageNames, EditorStyles.toolbarPopup, GUILayout.Width(52f));
            _problemsOnly = GUILayout.Toggle(_problemsOnly, "문제만", EditorStyles.toolbarButton, GUILayout.Width(50f));
            GUILayout.Label("미리 듣기", GUILayout.Width(52f));
            _previewVolume = GUILayout.HorizontalSlider(_previewVolume, 0f, 1f, GUILayout.Width(70f));
            bool bed = GUILayout.Toggle(_bed, "배경음 깔기", EditorStyles.toolbarButton, GUILayout.Width(74f));
            if (bed != _bed)
            {
                _bed = bed;
                Player.SetBed(bed, _previewVolume);
            }
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("라이브러리 다시 만들기", EditorStyles.toolbarButton))
            {
                Rebuild(true);
                // 저장 · 임포트가 끝난 뒤 이 이벤트의 레이아웃을 이어 그리지 않는다
                GUIUtility.ExitGUI();
            }
            if (GUILayout.Button("폴더 열기", EditorStyles.toolbarButton)) EditorUtility.RevealInFinder(AudioDataBuilder.AudioRoot);
            if (GUILayout.Button("보고서 복사", EditorStyles.toolbarButton))
            {
                EditorGUIUtility.systemCopyBuffer = BuildReport();
                _status = "보고서를 클립보드에 복사했어요";
            }
            if (GUILayout.Button("■ 모두 멈춤", EditorStyles.toolbarButton)) Player.StopAll();
            EditorGUILayout.EndHorizontal();
        }

        static int GroupFilterIndex(AudioGroup group) => group switch
        {
            AudioGroup.Ui => 1,
            AudioGroup.Ally => 2,
            AudioGroup.Enemy => 3,
            AudioGroup.Defense => 4,
            AudioGroup.Skill => 5,
            AudioGroup.Status => 6,
            AudioGroup.Flow => 7,
            AudioGroup.Amb => 8,
            AudioGroup.Emit => 8,
            AudioGroup.Bgm => 9,
            AudioGroup.Jingle => 10,
            AudioGroup.Vox => 11,
            AudioGroup.Reserved => 12,
            _ => 13,
        };

        int FoundCount(in AudioKeyDef def)
        {
            var key = def.IsAlias ? AudioCatalog.Keys[def.AliasOf].Key : def.Key;
            return _found.TryGetValue(key, out var list) ? list.Count : 0;
        }

        int ActiveCount(in AudioKeyDef def)
        {
            var key = def.IsAlias ? AudioCatalog.Keys[def.AliasOf].Key : def.Key;
            if (!_found.TryGetValue(key, out var list)) return 0;
            int n = 0;
            for (int i = 0; i < list.Count; i++)
            {
                if (IsTakeActive(key, list, list[i].Number)) n++;
            }
            return n;
        }

        bool HasQcFail(string key)
        {
            if (!_found.TryGetValue(key, out var list)) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var qc = _qc.Find(list[i].RelativePath);
                if (qc != null && qc.HasPasses && !qc.Passes) return true;
            }
            return false;
        }

        /// <summary>상태 점: ● 됨 · ◐ 모자람 · ○ 없음 · ⚠ QC 실패</summary>
        string StatusDot(in AudioKeyDef def, out Color color)
        {
            int found = FoundCount(in def);
            if (found == 0)
            {
                color = new Color(0.6f, 0.6f, 0.6f);
                return "○";
            }
            if (!def.IsAlias && HasQcFail(def.Key))
            {
                color = new Color(1f, 0.55f, 0.2f);
                return "⚠";
            }
            if (!def.IsAlias && ActiveCount(in def) < def.Takes)
            {
                color = new Color(0.95f, 0.8f, 0.3f);
                return "◐";
            }
            color = new Color(0.45f, 0.85f, 0.5f);
            return "●";
        }

        bool Visible(in AudioKeyDef def)
        {
            if (_group > 0 && GroupFilterIndex(def.Group) != _group) return false;
            if (_stage > 0 && def.Stage != _stage - 1) return false;
            if (!string.IsNullOrEmpty(_search))
            {
                var info = AudioCatalogText.Get(def.Key);
                bool hit = def.Key.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0 ||
                    (info.Sound != null && info.Sound.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0) ||
                    (info.Hook != null && info.Hook.IndexOf(_search, StringComparison.OrdinalIgnoreCase) >= 0);
                if (!hit) return false;
            }
            if (_problemsOnly)
            {
                string dot = StatusDot(in def, out _);
                if (dot == "●") return false;
            }
            return true;
        }

        void DrawList()
        {
            EditorGUILayout.BeginVertical(GUILayout.Width(330f));
            _listScroll = EditorGUILayout.BeginScrollView(_listScroll);
            var keys = AudioCatalog.Keys;
            var lastGroup = (AudioGroup)255;
            for (int i = 0; i < keys.Length; i++)
            {
                var def = keys[i];
                if (!Visible(in def)) continue;
                if (def.Group != lastGroup && _group == 0)
                {
                    lastGroup = def.Group;
                    EditorGUILayout.LabelField(GroupNames[GroupFilterIndex(def.Group)], EditorStyles.boldLabel);
                }
                string dot = StatusDot(in def, out var color);
                int active = ActiveCount(in def);
                int found = FoundCount(in def);
                var choice = Choice(def.Key, false);
                float rating = choice != null ? choice.AverageRating : 0f;
                EditorGUILayout.BeginHorizontal();
                var old = GUI.color;
                GUI.color = color;
                GUILayout.Label(dot, _dot);
                GUI.color = old;
                string label = def.Key + "  " + active + "/" + found + (rating > 0f ? "  ★" + rating.ToString("0.0") : string.Empty) +
                    "  P" + def.Stage;
                bool selected = i == _selected;
                if (GUILayout.Toggle(selected, label, EditorStyles.label) && !selected)
                {
                    _selected = i;
                    _abA = _abB = -1;
                    _detailScroll = Vector2.zero;
                }
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawDetail()
        {
            EditorGUILayout.BeginVertical();
            _detailScroll = EditorGUILayout.BeginScrollView(_detailScroll);
            if (_selected < 0 || _selected >= AudioCatalog.Count)
            {
                EditorGUILayout.HelpBox("왼쪽에서 소리를 고르세요. ▶로 들어 보고, 켜기로 실제 게임에 들어갈 테이크를 고르고, ★로 점수를 매겨요.\n" +
                    "고른 내용은 AudioSelection에 저장되고 라이브러리를 다시 만들 때 반영돼요.", MessageType.Info);
                DrawReport();
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                return;
            }
            var def = AudioCatalog.Keys[_selected];
            var id = new AudioId(_selected);
            var info = AudioCatalogText.Get(def.Key);
            EditorGUILayout.LabelField(def.Key, EditorStyles.largeLabel);
            if (!string.IsNullOrEmpty(info.Sound)) EditorGUILayout.LabelField("소리: " + info.Sound, _wrap);
            if (!string.IsNullOrEmpty(info.Hook)) EditorGUILayout.LabelField("거는 곳: " + info.Hook, _wrap);
            if (!string.IsNullOrEmpty(info.Brief)) EditorGUILayout.LabelField("목표: " + info.Brief, EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.LabelField(
                $"버스 {def.Bus} · 등급 {def.Class} · 단계 {def.Tier} ({def.TargetLufs} LUFS) · 길이 {def.Length:0.##}s · 테이크 목표 {def.Takes} · P{def.Stage}" +
                (string.IsNullOrEmpty(info.Route) ? string.Empty : " · " + info.Route), EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                $"게이트 {def.MinInterval * 1000f:0}ms · 동시 {def.MaxVoices} · 음높이 ±{def.PitchJitter * 100f:0}% · 볼륨 ±{def.VolumeJitterDb:0.#}dB" +
                (def.Ladder > 0 ? " · 사다리 " + AudioCatalog.Ladders[def.Ladder].Name : string.Empty) +
                (def.IsTuned ? $" · 조율 기준 {def.TargetRootHz:0.#}Hz" : string.Empty) +
                (def.IsAlias ? " · 별칭 → " + AudioCatalog.Keys[def.AliasOf].Key : string.Empty), EditorStyles.miniLabel);

            var choice = Choice(def.Key, false);
            EditorGUI.BeginChangeCheck();
            float trim = EditorGUILayout.Slider("볼륨 손질 (dB)", choice != null ? choice.trimDb : 0f, AudioSelection.MinTrimDb, 0f);
            if (EditorGUI.EndChangeCheck())
            {
                Choice(def.Key, true).trimDb = AudioLibraryRules.ClampTrim(trim);
                MarkSelectionDirty();
            }
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(choice == null || !choice.custom))
            {
                if (GUILayout.Button("QC 추천으로 되돌리기", GUILayout.Width(150f)))
                {
                    choice.custom = false;
                    choice.activeTakes = Array.Empty<byte>();
                    MarkSelectionDirty();
                }
            }
            GUILayout.Label(choice != null && choice.custom ? "귀로 고름" : "QC 추천을 따름", EditorStyles.miniLabel);
            EditorGUILayout.EndHorizontal();
            EditorGUI.BeginChangeCheck();
            string note = EditorGUILayout.TextField("메모", choice != null ? choice.note : string.Empty);
            if (EditorGUI.EndChangeCheck())
            {
                Choice(def.Key, true).note = note;
                MarkSelectionDirty();
            }

            EditorGUILayout.Space(4f);
            DrawActions(id, in def);
            EditorGUILayout.Space(4f);
            DrawTakes(in def);
            EditorGUILayout.EndScrollView();
            EditorGUILayout.EndVertical();
        }

        void DrawActions(AudioId id, in AudioKeyDef def)
        {
            var takes = TakesOf(in def);
            EditorGUILayout.BeginHorizontal();
            using (new EditorGUI.DisabledScope(takes == null || takes.Count == 0))
            {
                if (GUILayout.Button("▶ 런타임 규칙으로 재생")) PlayRuntime(in def, 0);
                if (def.Ladder > 0 && GUILayout.Button("사다리 ×8")) PlayLadder(in def);
                if (GUILayout.Button("연타 테스트")) PlayStress(in def);
                if (def.Has(AudioKeyFlags.Loop) && GUILayout.Button("루프 이음매")) PlaySeam(in def);
                if (EditorApplication.isPlaying && GUILayout.Button("게임 안에서 재생")) AudioService.Play(id);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("전투 미리 듣기 (10초)")) PlayBattlePreview();
            _fast = GUILayout.Toggle(_fast, "×2", GUILayout.Width(40f));
            using (new EditorGUI.DisabledScope(takes == null || takes.Count < 2))
            {
                if (GUILayout.Button("블라인드 A/B")) StartAb(in def);
            }
            EditorGUILayout.EndHorizontal();
            if (_abA >= 0 && _abB >= 0 && takes != null && _abA < takes.Count && _abB < takes.Count)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.Label($"블라인드 A/B ({_abRounds}번째)", GUILayout.Width(130f));
                if (GUILayout.Button("▶ A")) PlayTake(in def, takes[_abA]);
                if (GUILayout.Button("▶ B")) PlayTake(in def, takes[_abB]);
                if (GUILayout.Button("A가 좋아요")) PickAb(in def, takes, _abA);
                if (GUILayout.Button("B가 좋아요")) PickAb(in def, takes, _abB);
                EditorGUILayout.EndHorizontal();
            }
        }

        List<AudioDataBuilder.FoundTake> TakesOf(in AudioKeyDef def)
        {
            var key = def.IsAlias ? AudioCatalog.Keys[def.AliasOf].Key : def.Key;
            return _found.TryGetValue(key, out var list) ? list : null;
        }

        bool IsTakeActive(string key, List<AudioDataBuilder.FoundTake> list, int number)
        {
            var choice = Choice(key, false);
            if (choice != null && choice.custom) return choice.IsActive(number);
            // QC 추천 → 통과 → 전부 (빌더와 같은 규칙)
            bool anyRecommended = false;
            bool anyPass = false;
            for (int i = 0; i < list.Count; i++)
            {
                var qc = _qc.Find(list[i].RelativePath);
                if (qc == null) continue;
                anyRecommended |= qc.HasRecommended && qc.Recommended;
                anyPass |= qc.Passes;
            }
            AudioQcData.FileQc self = null;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i].Number == number) self = _qc.Find(list[i].RelativePath);
            }
            if (anyRecommended) return self != null && self.HasRecommended && self.Recommended;
            if (anyPass) return self != null && self.Passes;
            return true;
        }

        void DrawTakes(in AudioKeyDef def)
        {
            var takes = TakesOf(in def);
            if (takes == null || takes.Count == 0)
            {
                EditorGUILayout.HelpBox("아직 파일이 없어요 — 조용히 지나가요 (정상). 파일 이름: " + def.Key + "_01.wav", MessageType.None);
                return;
            }
            string key = def.IsAlias ? AudioCatalog.Keys[def.AliasOf].Key : def.Key;
            var choice = Choice(key, false);
            for (int i = 0; i < takes.Count; i++)
            {
                var take = takes[i];
                var qc = _qc.Find(take.RelativePath);
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                EditorGUILayout.BeginHorizontal();
                if (GUILayout.Button("▶", GUILayout.Width(26f))) PlayTake(in def, take);
                if (GUILayout.Button("■", GUILayout.Width(26f))) Player.StopAll();
                bool active = IsTakeActive(key, takes, take.Number);
                using (new EditorGUI.DisabledScope(def.IsAlias))
                {
                    bool on = GUILayout.Toggle(active, "켜기", GUILayout.Width(46f));
                    if (on != active) SetActive(key, takes, take.Number, on);
                }
                GUILayout.Label(Path.GetFileName(take.Path), GUILayout.Width(190f));
                int stars = choice != null ? choice.Rating(take.Number) : 0;
                for (int s = 1; s <= 5; s++)
                {
                    if (GUILayout.Button(s <= stars ? "★" : "☆", EditorStyles.label, GUILayout.Width(14f)))
                    {
                        Choice(key, true).SetRating(take.Number, s == stars ? 0 : s);
                        MarkSelectionDirty();
                    }
                }
                var preview = AssetPreview.GetAssetPreview(take.Clip);
                var rect = GUILayoutUtility.GetRect(140f, 28f, GUILayout.Width(140f));
                if (preview != null) GUI.DrawTexture(rect, preview, ScaleMode.StretchToFill);
                else if (AssetPreview.IsLoadingAssetPreview(take.Clip.GetInstanceID())) Repaint();
                if (GUILayout.Button("파일", GUILayout.Width(40f))) EditorGUIUtility.PingObject(take.Clip);
                EditorGUILayout.EndHorizontal();
                if (qc != null)
                {
                    EditorGUILayout.LabelField(qc.Summary(), EditorStyles.miniLabel);
                    string made = string.Join(" · ", NonEmpty(qc.Source, qc.Recipe, qc.Variation));
                    if (!string.IsNullOrEmpty(made)) EditorGUILayout.LabelField("만든 경로: " + made, EditorStyles.wordWrappedMiniLabel);
                    if (!string.IsNullOrEmpty(qc.Master) && GUILayout.Button("마스터 파일 보기", GUILayout.Width(110f))) EditorUtility.RevealInFinder(qc.Master);
                }
                else EditorGUILayout.LabelField($"{take.Clip.length:0.00}s · QC 없음", EditorStyles.miniLabel);
                EditorGUILayout.EndVertical();
            }
        }

        static IEnumerable<string> NonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrEmpty(values[i])) yield return values[i];
            }
        }

        void SetActive(string key, List<AudioDataBuilder.FoundTake> takes, int number, bool on)
        {
            var choice = Choice(key, true);
            if (!choice.custom)
            {
                // 지금 보이는 상태(추천)를 그대로 옮겨 적고 귀로 고름으로 바꾼다
                var list = new List<byte>();
                for (int i = 0; i < takes.Count; i++)
                {
                    if (IsTakeActive(key, takes, takes[i].Number)) list.Add((byte)takes[i].Number);
                }
                choice.activeTakes = list.ToArray();
                choice.custom = true;
            }
            choice.SetActive(number, on);
            MarkSelectionDirty();
        }

        void DrawReport()
        {
            var report = AudioDataBuilder.LastReport ?? _report;
            if (report == null) return;
            EditorGUILayout.LabelField(report.ToText(), EditorStyles.wordWrappedMiniLabel);
        }

        string BuildReport()
        {
            var sb = new StringBuilder();
            sb.AppendLine("# 사운드 시험장 보고 (" + DateTime.Now.ToString("yyyy-MM-dd HH:mm") + ")");
            sb.AppendLine();
            sb.AppendLine("| 키 | P | 켜 둔/전체 | 귀로 고름 | 손질 dB | ★ | 메모 |");
            sb.AppendLine("|---|---|---|---|---|---|---|");
            var keys = AudioCatalog.Keys;
            int missing = 0;
            for (int i = 0; i < keys.Length; i++)
            {
                var def = keys[i];
                int found = FoundCount(in def);
                if (found == 0)
                {
                    missing++;
                    continue;
                }
                var choice = Choice(def.Key, false);
                sb.Append("| ").Append(def.Key).Append(" | P").Append(def.Stage).Append(" | ").Append(ActiveCount(in def)).Append('/').Append(found)
                    .Append(" | ").Append(choice != null && choice.custom ? "예" : "-").Append(" | ").Append(choice != null ? choice.trimDb.ToString("0.#") : "0")
                    .Append(" | ").Append(choice != null && choice.AverageRating > 0f ? choice.AverageRating.ToString("0.0") : "-").Append(" | ")
                    .Append(choice != null ? choice.note : string.Empty).AppendLine(" |");
            }
            sb.AppendLine();
            sb.AppendLine("파일이 없는 키: " + missing + "개");
            return sb.ToString();
        }

        // ───────────── 재생 ─────────────

        AudioLibrary.Take TakeOf(in AudioKeyDef def, AudioDataBuilder.FoundTake take)
        {
            var qc = _qc.Find(take.RelativePath);
            return new AudioLibrary.Take
            {
                clip = take.Clip,
                number = (byte)take.Number,
                rootHz = def.IsTuned && qc != null && qc.RootHz.HasValue ? qc.RootHz.Value : 0f,
                gainDb = qc != null && qc.GainDb.HasValue ? AudioLibraryRules.ClampTakeGain(qc.GainDb.Value) : 0f,
            };
        }

        float TrimOf(in AudioKeyDef def)
        {
            var choice = Choice(def.Key, false);
            return choice != null ? choice.trimDb : 0f;
        }

        float BusGain(in AudioKeyDef def) => def.Bus switch
        {
            AudioBus.Bgm => UISfx.BgmGain,
            AudioBus.Amb => UISfx.BgmGain * 0.9f,
            _ => UISfx.SfxGain,
        };

        /// <summary>테이크 하나를 그대로 (흔들기 없음, 조율 키는 기준 음으로)</summary>
        void PlayTake(in AudioKeyDef def, AudioDataBuilder.FoundTake found)
        {
            var take = TakeOf(in def, found);
            const float pitch = 1f;
            float volume = _previewVolume * BusGain(in def) * AudioGate.DbToGain(TrimOf(in def) + take.gainDb);
            Player.Play(take.clip, volume, pitch, 0f, 0, def.Has(AudioKeyFlags.Loop) ? Math.Min(take.clip.length, 12.0) : 0, def.Has(AudioKeyFlags.Loop));
            _status = $"{Path.GetFileName(found.Path)} 재생 ({take.clip.length:0.00}s)";
        }

        /// <summary>런타임처럼: 켜 둔 테이크 섞어 뽑기 + 흔들기 + 게이트 (+ 조율 키는 targetHz)</summary>
        bool PlayRuntime(in AudioKeyDef def, double delay, float targetHz = 0f, float gainDb = 0f, float pitchScale = 1f, float pan = 0f)
        {
            var takes = TakesOf(in def);
            if (takes == null || takes.Count == 0) return false;
            string key = def.IsAlias ? AudioCatalog.Keys[def.AliasOf].Key : def.Key;
            var active = new List<AudioDataBuilder.FoundTake>(takes.Count);
            for (int i = 0; i < takes.Count; i++)
            {
                if (IsTakeActive(key, takes, takes[i].Number)) active.Add(takes[i]);
            }
            if (active.Count == 0) return false;
            int index;
            float pitch = pitchScale;
            int keyIndex = AudioCatalog.Find(def.Key).Index;
            if (def.IsTuned)
            {
                var roots = new float[active.Count];
                for (int i = 0; i < active.Count; i++)
                {
                    var t = TakeOf(in def, active[i]);
                    roots[i] = t.rootHz > 0f ? t.rootHz : def.TargetRootHz;
                }
                float target = targetHz > 0f ? targetHz : def.Ladder > 0 ? AudioCatalog.LadderHz(def.Ladder, 0) : def.TargetRootHz;
                index = PitchLadder.NearestTake(target, roots, roots.Length, UnityEngine.Random.value);
                if (index < 0) index = 0;
                pitch *= PitchLadder.PitchFor(target, roots[index]);
            }
            else
            {
                index = Player.Shuffle(keyIndex, active.Count);
                pitch *= AudioGate.PitchJitter(def.PitchJitter, UnityEngine.Random.value);
            }
            var take = TakeOf(in def, active[index]);
            if (!Player.Gate(keyIndex, def.MinInterval, def.MaxVoices, delay, take.clip.length / Mathf.Max(0.1f, pitch))) return false;
            float db = TrimOf(in def) + take.gainDb + gainDb + AudioGate.GainJitterDb(def.VolumeJitterDb, UnityEngine.Random.value);
            float volume = _previewVolume * BusGain(in def) * AudioGate.DbToGain(db);
            Player.Play(take.clip, volume, pitch, pan, delay, def.Has(AudioKeyFlags.Loop) ? 6.0 : 0, def.Has(AudioKeyFlags.Loop));
            return true;
        }

        void PlayLadder(in AudioKeyDef def)
        {
            var ladder = AudioCatalog.Ladders[def.Ladder];
            for (int i = 0; i < 8; i++)
            {
                int step = i;
                if (def.Ladder == AudioCatalog.LadderLullaby && i % 7 == 6)
                {
                    // 자장가의 7번째는 쉼표 (피날레 형태만 마지막 음)
                    continue;
                }
                if (def.Ladder == AudioCatalog.LadderLullaby) step = i % 7;
                float hz = ladder.StepHz(Mathf.Min(step, ladder.Count - 1));
                PlayRuntime(in def, i * 0.28, hz);
            }
            _status = "사다리 " + ladder.Name + " ×8";
        }

        void PlayStress(in AudioKeyDef def)
        {
            float interval = Mathf.Max(def.MinInterval, def.Group == AudioGroup.Ally ? 1f / 14f : 0.07f);
            int played = 0;
            for (int i = 0; i < 12; i++)
            {
                if (PlayRuntime(in def, i * interval, def.IsTuned ? AudioCatalog.LadderHz(def.Ladder, i) : 0f)) played++;
            }
            _status = $"연타 테스트: 12번 중 {played}번 울림 ({interval * 1000f:0}ms 간격, 게이트 · 섞어 뽑기 적용)";
        }

        void PlaySeam(in AudioKeyDef def)
        {
            var takes = TakesOf(in def);
            if (takes == null || takes.Count == 0) return;
            var take = TakeOf(in def, takes[0]);
            float volume = _previewVolume * BusGain(in def) * AudioGate.DbToGain(TrimOf(in def) + take.gainDb);
            Player.PlaySeam(take.clip, volume, 2.0);
            _status = "루프 이음매: 끝 2초 → 처음 2초";
        }

        void StartAb(in AudioKeyDef def)
        {
            var takes = TakesOf(in def);
            if (takes == null || takes.Count < 2) return;
            _abA = UnityEngine.Random.Range(0, takes.Count);
            do _abB = UnityEngine.Random.Range(0, takes.Count);
            while (_abB == _abA);
            _abRounds++;
            PlayTake(in def, takes[_abA]);
        }

        void PickAb(in AudioKeyDef def, List<AudioDataBuilder.FoundTake> takes, int winner)
        {
            string key = def.IsAlias ? AudioCatalog.Keys[def.AliasOf].Key : def.Key;
            var choice = Choice(key, true);
            int number = takes[winner].Number;
            choice.SetRating(number, Mathf.Min(5, Mathf.Max(1, choice.Rating(number)) + 1));
            MarkSelectionDirty();
            _status = $"A/B: {Path.GetFileName(takes[winner].Path)} ★+1 — 다음 판";
            StartAb(in def);
        }

        // ───────────── 전투 미리 듣기 (10초) ─────────────

        void PlayBattlePreview()
        {
            Player.StopAll();
            Player.ResetRules();
            Player.SetBed(true, _previewVolume);
            int speed = _fast ? 2 : 1;
            float rate = speed;
            string[] lineages = { "jjakjjak", "ttokdan", "chalkak", "mongdang", "lalabell" };
            var random = new System.Random(7);
            int lullaby = 0;
            // 지킴이 다섯: 저마다 1.1~1.4초마다 쏘고, 비행 시간 뒤에 맞는다
            for (int g = 0; g < lineages.Length; g++)
            {
                var look = AttackFxCatalog.Look(lineages[g]);
                var mod = AttackFxCatalog.Style(CombatStyle.Defense);
                float interval = (1.1f + 0.07f * g) / rate;
                bool skip = false;
                for (double t = 0.2 + g * 0.17; t < 10.0; t += interval)
                {
                    float flight = AttackFxCatalog.FlightTime(look, mod, 220f + 40f * (float)random.NextDouble(), speed, 1f);
                    float windup = AttackFxCatalog.Windup(flight, speed);
                    bool crit = g == 2 && t > 2.0 && t < 3.4;
                    skip = speed >= 2 && !skip;
                    if (!skip) Cue(AudioCatalog.FireKeyOf(look.Sfx), t + windup, 0f, 1f, Pan(g));
                    if (look.Extra == AttackExtra.SoundRing)
                    {
                        int index = lullaby++ % 7;
                        Cue("atk_musicbox_ring", t + flight, 0f, 1f, Pan(g));
                        if (index < 6) Cue("atk_musicbox", t + flight, 0f, 1f, Pan(g), AudioCatalog.LadderHz(AudioCatalog.LadderLullaby, index));
                        continue;
                    }
                    if (crit)
                    {
                        Cue(AudioCatalog.CritKeyOf(look.Sfx), t + flight, 0f, 1f, Pan(g));
                        Cue("hit_crit", t + flight, 0f, 1f, Pan(g));
                    }
                    else Cue(look.Sfx, t + flight, 0f, 1f, Pan(g));
                    if (random.NextDouble() < (speed >= 2 ? 0.3 : 0.5)) Cue("hurt_dust", t + flight, -6f, 1f, Pan(g));
                }
            }
            // 잔향이 둘 (근접 베기)
            for (double t = 0.5; t < 10.0; t += 0.8 / rate)
            {
                Cue("echo_slash", t, 0f, 1f, -0.2f);
                Cue("atk_kiss", t + BattleAudioTiming.EchoMeleeHit(speed), -3f, AudioCatalog.SemitoneRatio(2f), -0.2f);
            }
            for (double t = 0.9; t < 10.0; t += 0.8 / rate) Cue("atk_rain", t + BattleAudioTiming.EchoMeleeHit(speed), -3f, AudioCatalog.SemitoneRatio(2f), 0.25f);
            // 적의 공격
            for (double t = 1.0; t < 10.0; t += 1.0 / rate) Cue(t % 2.0 < 1.0 ? "enemy_puff" : "enemy_flutter", t, -3f, 1f, 0.1f);
            Cue("shop_hit", 6.2, 0f, 1f, 0f);
            Cue("enemy_spawn", 0.3, -4f, 1f, 0.3f);
            // 처치 여섯 (연속 처치 사다리 · 단추)
            double[] kills = { 1.5, 3.0, 3.35, 3.7, 6.6, 8.1 };
            string[] deaths = { "death_dust", "death_moth", "death_dust", "death_metal", "death_shadow", "death_dust" };
            int streak = 0;
            double last = -9;
            for (int k = 0; k < kills.Length; k++)
            {
                streak = kills[k] - last <= 0.8 ? streak + 1 : 1;
                last = kills[k];
                Cue(deaths[k], kills[k], 0f, 1f, 0.15f * (k % 3 - 1));
                if (streak >= 2) Cue("kill_chain", kills[k], 0f, 1f, 0f, AudioCatalog.LadderHz(AudioCatalog.LadderKill, streak - 2));
                if (k == 4)
                {
                    Cue("coin_pop", kills[k] + 0.08, 0f, 1f, 0f);
                    Cue("ui_coin_land", kills[k] + 0.78, 0f, 1f, 0f);
                }
            }
            // 불 밝히기 한 번: 점화 → 물결 → 닿는 순서대로 C5 F5 A5 C6 F6 A6 C7
            Cue("lamp_lit", 5.0, 0f, 1f, 0f);
            Cue("lamp_wave", 5.0 + LampWave.Start, 0f, 1f, 0f);
            for (int n = 0; n < 7; n++)
                Cue("hit_lamp", 5.0 + LampWave.Start + LampWave.Delay(0.6f + 0.3f * n), 0f, 1f, 0.3f * ((n % 3) - 1), AudioCatalog.LadderHz(AudioCatalog.LadderLamp, n));
            _status = speed >= 2 ? "전투 미리 듣기 ×2 (10초)" : "전투 미리 듣기 (10초)";
        }

        static float Pan(int slot) => BattleAudioTiming.Pan(BattleGeometry.GuardianPosition(slot).x);

        void Cue(string key, double at, float gainDb, float pitch, float pan, float targetHz = 0f)
        {
            var id = AudioCatalog.Find(key);
            if (!id.IsValid) return;
            var def = AudioCatalog.Keys[id.Index];
            PlayRuntime(in def, at, targetHz, gainDb, pitch, pan);
        }

        // ───────────── 미리 듣기 소스 ─────────────

        /// <summary>숨은 미리 듣기 소스 (편집 · 재생 모드) — 예약은 0.15초 앞까지 PlayScheduled로 넘긴다</summary>
        sealed class PreviewPlayer
        {
            const int Voices = 24;

            struct Cue
            {
                public double At;
                public AudioClip Clip;
                public float Volume;
                public float Pitch;
                public float Pan;
                public double Length;
                public bool Loop;
                public int StartSample;
            }

            readonly GameObject _go;
            readonly AudioSource[] _sources = new AudioSource[Voices];
            readonly double[] _busyUntil = new double[Voices];
            readonly AudioSource _bedMusic;
            readonly AudioSource _bedAmb;
            readonly List<Cue> _queue = new();
            readonly ShuffleState[] _shuffle = new ShuffleState[AudioCatalog.Count];
            AudioGate _gate = new(AudioCatalog.Count);
            double _origin;
            AudioLibrary _library;

            public PreviewPlayer()
            {
                _go = new GameObject("[AudioAudition]") { hideFlags = HideFlags.HideAndDontSave };
                for (int i = 0; i < Voices; i++) _sources[i] = NewSource();
                _bedMusic = NewSource();
                _bedAmb = NewSource();
            }

            AudioSource NewSource()
            {
                var source = _go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.dopplerLevel = 0f;
                source.bypassReverbZones = true;
                return source;
            }

            public void SetLibrary(AudioLibrary library) => _library = library;

            public void ResetRules()
            {
                _gate = new AudioGate(AudioCatalog.Count);
                _origin = AudioSettings.dspTime + 0.1;
            }

            public int Shuffle(int key, int count) => key >= 0 && key < _shuffle.Length ? ShuffleBag.Next(ref _shuffle[key], count, UnityEngine.Random.value) : 0;

            /// <summary>런타임 게이트 (DSP 시계, delay초 뒤)</summary>
            public bool Gate(int key, float minInterval, int maxVoices, double delay, float duration)
            {
                double now = AudioSettings.dspTime;
                if (_origin < now) _origin = now + 0.05;
                return _gate.TryPass(key, minInterval, maxVoices, _origin + delay, duration);
            }

            public void Play(AudioClip clip, float volume, float pitch, float pan, double delay, double length, bool loop)
            {
                if (clip == null) return;
                double now = AudioSettings.dspTime;
                if (_origin < now) _origin = now + 0.05;
                _queue.Add(new Cue
                {
                    At = delay > 0 ? _origin + delay : now + 0.02,
                    Clip = clip,
                    Volume = Mathf.Clamp01(volume),
                    Pitch = pitch,
                    Pan = pan,
                    Length = length,
                    Loop = loop,
                });
                Tick();
            }

            /// <summary>반복 이음매: 끝 seconds초 → (경계에서) 처음 seconds초</summary>
            public void PlaySeam(AudioClip clip, float volume, double seconds)
            {
                if (clip == null || clip.frequency <= 0) return;
                StopAll();
                double now = AudioSettings.dspTime + 0.1;
                int tail = Mathf.Max(0, clip.samples - (int)(seconds * clip.frequency));
                _queue.Add(new Cue { At = now, Clip = clip, Volume = volume, Pitch = 1f, Length = seconds, StartSample = tail });
                _queue.Add(new Cue { At = now + (double)(clip.samples - tail) / clip.frequency, Clip = clip, Volume = volume, Pitch = 1f, Length = seconds });
                Tick();
            }

            public void SetBed(bool on, float volume)
            {
                _bedMusic.Stop();
                _bedAmb.Stop();
                if (!on || _library == null) return;
                StartBed(_bedMusic, AudioCatalog.Find("bgm_battle_night"), volume * UISfx.BgmGain);
                StartBed(_bedAmb, AudioCatalog.Find("amb_night"), volume * UISfx.BgmGain * 0.9f);
            }

            void StartBed(AudioSource source, AudioId id, float volume)
            {
                var entry = _library.Get(id);
                if (entry == null || entry.takes == null || entry.takes.Length == 0 || entry.takes[0].clip == null) return;
                source.clip = entry.takes[0].clip;
                source.loop = true;
                source.volume = Mathf.Clamp01(volume * AudioGate.DbToGain(entry.trimDb + entry.takes[0].gainDb));
                source.Play();
            }

            /// <summary>0.15초 안에 울릴 예약을 소스에 넘긴다. 아직 할 일이 있으면 true</summary>
            public bool Tick()
            {
                double now = AudioSettings.dspTime;
                for (int q = _queue.Count - 1; q >= 0; q--)
                {
                    var cue = _queue[q];
                    if (cue.At > now + 0.15) continue;
                    int voice = FreeVoice(now);
                    if (voice < 0) continue;
                    var source = _sources[voice];
                    source.Stop();
                    source.clip = cue.Clip;
                    source.volume = cue.Volume;
                    source.pitch = cue.Pitch;
                    source.panStereo = Mathf.Clamp(cue.Pan, -1f, 1f);
                    source.loop = cue.Loop;
                    source.timeSamples = Mathf.Clamp(cue.StartSample, 0, Mathf.Max(0, cue.Clip.samples - 1));
                    double at = Math.Max(cue.At, now + 0.005);
                    source.PlayScheduled(at);
                    double length = cue.Length > 0 ? cue.Length : cue.Clip.length / Mathf.Max(0.1f, Mathf.Abs(cue.Pitch));
                    if (cue.Length > 0) source.SetScheduledEndTime(at + cue.Length);
                    _busyUntil[voice] = at + length;
                    _queue.RemoveAt(q);
                }
                return _queue.Count > 0;
            }

            int FreeVoice(double now)
            {
                int oldest = 0;
                for (int i = 0; i < Voices; i++)
                {
                    if (_busyUntil[i] <= now && !_sources[i].isPlaying) return i;
                    if (_busyUntil[i] < _busyUntil[oldest]) oldest = i;
                }
                return oldest;
            }

            public void StopAll()
            {
                _queue.Clear();
                for (int i = 0; i < Voices; i++)
                {
                    _sources[i].Stop();
                    _busyUntil[i] = 0;
                }
                _bedMusic.Stop();
                _bedAmb.Stop();
                _origin = 0;
            }

            public void Dispose()
            {
                StopAll();
                if (_go != null) DestroyImmediate(_go);
            }
        }
    }
}
