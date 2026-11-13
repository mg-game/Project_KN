using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 기록 창 (CR-03, SPEC4 §7.4 — 900×900 '전투 기록', BattleSceneBuilder가 만든다): [지난 60초][이번 스테이지] 두 칸,
    /// 피해 많은 순 줄 최대 11 (초상 56 · 이름 + 지/잔 칩 · 계보 메인 색 피해 막대 360×18 + 짧은 숫자 · 회복(Positive) · 보호막(SoftBlue) 작은 막대 — 막대마다 그 칸에서 가장 큰 친구 기준 · '처치 {n}'),
    /// 맨 위 친구는 왕관. 열려 있는 동안 1초에 한 번 다시 채우고 글자는 바뀔 때만 쓴다. 기록이 없으면 '아직 기록이 없어요. 전투를 조금 지켜봐 주세요'.
    /// 여는 곳: UINav.OpenBattleStats → BattleView (R1 칸 StatsButton은 P4-6)
    /// </summary>
    public class BattleStatsPopup : UIPopup
    {
        public const int Rows = 11;
        public const string EmptyText = "아직 기록이 없어요. 전투를 조금 지켜봐 주세요";

        [Header("전투 기록")]
        [SerializeField] Button _windowTab;
        [SerializeField] Button _stageTab;
        [SerializeField] Image _windowTabBack;
        [SerializeField] Image _stageTabBack;
        [SerializeField] RectTransform[] _rows;
        [SerializeField] Image[] _portraits;
        [SerializeField] Image[] _portraitFrames;
        [SerializeField] TMP_Text[] _names;
        [SerializeField] TMP_Text[] _roles;
        [SerializeField] Image[] _damageBars;
        [SerializeField] TMP_Text[] _damageTexts;
        [SerializeField] Image[] _healBars;
        [SerializeField] Image[] _shieldBars;
        [SerializeField] TMP_Text[] _kills;
        [SerializeField] Image _crown;
        [SerializeField] TMP_Text _empty;

        /// <summary>지금 열린 전투 화면의 기록 (BattleView가 넣는다)</summary>
        public static BattleStatsRecorder Source { get; set; }

        readonly List<BattleStatsRecorder.UnitTotals> _list = new();
        readonly string[] _shownForm = new string[Rows];
        readonly double[] _shownDamage = new double[Rows];
        readonly int[] _shownKills = new int[Rows];
        bool _lastMinute = true;
        float _next;
        int _shownCount = -1;

        /// <summary>창을 연다 (씬에 없으면 false — 빌더를 다시 돌리기 전)</summary>
        public static bool TryOpen()
        {
            var ui = UIManager.Instance;
            if (ui == null || !ui.TryGet<BattleStatsPopup>(out var popup)) return false;
            popup.Open();
            return popup.IsOpen;
        }

        protected override void Awake()
        {
            base.Awake();
            if (_windowTab != null) _windowTab.onClick.AddListener(() => SetSegment(true));
            if (_stageTab != null) _stageTab.onClick.AddListener(() => SetSegment(false));
        }

        protected override void OnOpen()
        {
            _shownCount = -1;
            for (int i = 0; i < Rows; i++) _shownForm[i] = null;
            ApplySegment();
            Refresh();
        }

        void SetSegment(bool window)
        {
            if (_lastMinute == window) return;
            _lastMinute = window;
            _shownCount = -1;
            for (int i = 0; i < Rows; i++) _shownForm[i] = null;
            ApplySegment();
            Refresh();
        }

        void ApplySegment()
        {
            if (_windowTabBack != null) _windowTabBack.color = _lastMinute ? UITheme.TabSelected : UITheme.TabNormal;
            if (_stageTabBack != null) _stageTabBack.color = _lastMinute ? UITheme.TabNormal : UITheme.TabSelected;
        }

        void Update()
        {
            if (!IsOpen || Time.unscaledTime < _next) return;
            Refresh();
        }

        void Refresh()
        {
            _next = Time.unscaledTime + 1f;
            var source = Source;
            int count = source != null ? source.Fill(_list, _lastMinute, Time.unscaledTime, Rows) : 0;
            if (count != _shownCount)
            {
                _shownCount = count;
                if (_empty != null) _empty.gameObject.SetActive(count == 0);
                if (_crown != null) _crown.gameObject.SetActive(count > 0);
            }
            double top = count > 0 ? System.Math.Max(1d, _list[0].Damage) : 1d;
            // 회복 · 보호막 작은 막대는 그 칸에서 가장 큰 친구 기준 (줄마다 따로 재면 회복 3인 친구도 꽉 찬 막대가 된다)
            ColumnTops(_list, count, out double healTop, out double shieldTop);
            for (int i = 0; _rows != null && i < _rows.Length && i < Rows; i++)
            {
                var row = _rows[i];
                if (row == null) continue;
                bool on = i < count;
                if (row.gameObject.activeSelf != on) row.gameObject.SetActive(on);
                if (!on) continue;
                var t = _list[i];
                if (!string.Equals(_shownForm[i], t.FormId))
                {
                    _shownForm[i] = t.FormId;
                    _shownDamage[i] = -1d;
                    _shownKills[i] = -1;
                    var main = BattleUnitArt.MainColor(null, t.FormId);
                    if (At(_portraits, i) is Image portrait)
                    {
                        portrait.sprite = BattleUnitArt.Portrait(t.FormId);
                        portrait.enabled = portrait.sprite != null;
                    }
                    if (At(_portraitFrames, i) is Image frame) frame.color = main;
                    if (At(_names, i) is TMP_Text name) name.text = BattleUnitArt.DisplayName(t.FormId);
                    if (At(_roles, i) is TMP_Text role) role.text = t.IsEcho ? "잔" : "지";
                    if (At(_damageBars, i) is Image bar) bar.color = main;
                }
                if (t.Damage != _shownDamage[i])
                {
                    _shownDamage[i] = t.Damage;
                    if (At(_damageTexts, i) is TMP_Text value) value.text = NumberFormatter.FormatCompact(t.Damage);
                }
                if (t.Kills != _shownKills[i])
                {
                    _shownKills[i] = t.Kills;
                    if (At(_kills, i) is TMP_Text kills) kills.SetText("처치 {0}", t.Kills);
                }
                if (At(_damageBars, i) is Image damage) damage.fillAmount = (float)(t.Damage / top);
                if (At(_healBars, i) is Image heal) heal.fillAmount = MiniBarFill(t.Heal, healTop);
                if (At(_shieldBars, i) is Image shield) shield.fillAmount = MiniBarFill(t.Shield, shieldTop);
            }
        }

        /// <summary>앞 count줄의 가장 큰 회복 · 보호막 (순수, 할당 없음)</summary>
        public static void ColumnTops(List<BattleStatsRecorder.UnitTotals> list, int count, out double healTop, out double shieldTop)
        {
            healTop = 0d;
            shieldTop = 0d;
            int n = list != null ? System.Math.Min(count, list.Count) : 0;
            for (int i = 0; i < n; i++)
            {
                var t = list[i];
                if (t.Heal > healTop) healTop = t.Heal;
                if (t.Shield > shieldTop) shieldTop = t.Shield;
            }
        }

        /// <summary>작은 막대 채움 = 값 / 그 칸의 가장 큰 값 (최소 1로 나눔, 0 이하는 빈 막대 — 순수)</summary>
        public static float MiniBarFill(double value, double columnTop) =>
            value > 0d ? (float)System.Math.Min(1d, value / System.Math.Max(1d, columnTop)) : 0f;

        static Object At<T>(T[] array, int index) where T : Object => array != null && index < array.Length ? array[index] : null;
    }
}
