using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전장 오른쪽의 작은 알림 띠 (아이콘 + 제목 + 한 줄 설명).
    /// 큰 스킬 이름("마지막 음"), 스테이지 기믹("비 오는 밤"), 보스 분노 같은 순간을 짧게 알린다.
    /// 자리는 적이 걸어오는 다섯 레인 사이의 빈 곳(수선점 오른쪽 아래, 동쪽 레인과 남동쪽 레인 사이)이라 적·지킴이·말풍선을 가리지 않는다.
    /// 수선점 위치는 화면 비율마다 달라지므로 BattleFieldMapper로 수선점 높이에 맞춰 놓는다.
    /// 띠는 위에서부터 칸을 채워 쌓이고(최대 칸 수 = 만들어 둔 줄 수), 칸이 모자라면 가장 오래된 띠를 바꿔 쓴다. 같은 제목이 이미 떠 있으면 시간만 늘린다.
    /// <para>
    /// 설명 줄 (M23 — 18, 자동 하한 16): 하한 16에서도 한 줄(칸 폭 262)에 들지 않으면 그 띠만 _tallRowHeight로 키우고 설명을 두 줄로 접는다
    /// (스테이지 기믹 '그림자가 숨어요 · 빛 특성이 크게 강해져요', 좀나방 안내 등). 띠 높이는 Show에서 한 번 정하고,
    /// 아래 칸들은 위 띠가 키운 만큼 내려가 앉는다 (위 띠가 사라지면 부드럽게 올라온다 — 줄이기면 바로).
    /// </para>
    /// </summary>
    public class BattleNoticeView : MonoBehaviour
    {
        [SerializeField] RectTransform[] _rows;
        [SerializeField] CanvasGroup[] _groups;
        [SerializeField] Image[] _icons;
        [SerializeField] Image[] _iconGlows;
        [SerializeField] TMP_Text[] _titles;
        [SerializeField] TMP_Text[] _subtitles;
        [SerializeField] Image[] _edges;
        [Tooltip("칸 간격 (한 줄 설명 띠 기준 — 위 띠가 두 줄이면 그만큼 더 내려간다)")]
        [SerializeField] float _rowSpacing = 64f;
        [Tooltip("한 줄 설명 띠 높이 (빌더의 Notice 사각형 높이)")]
        [SerializeField] float _rowHeight = 58f;
        [Tooltip("설명이 하한 크기에서도 한 줄에 들지 않을 때의 띠 높이 (설명 두 줄)")]
        [SerializeField] float _tallRowHeight = 84f;
        [Tooltip("전장 좌표 변환 (없으면 _x/_top 고정 위치)")]
        [SerializeField] BattleFieldMapper _mapper;
        [Tooltip("첫 줄 윗변의 월드 y (수선점 = 0, 음수 = 수선점보다 아래)")]
        [SerializeField] float _worldTop = -0.2f;
        [Tooltip("전장 오른쪽 끝에서 띄우는 여백 (줄은 가운데 앵커 + 오른쪽 위 pivot)")]
        [SerializeField] float _rightMargin = 14f;
        [Tooltip("나타날 때 미끄러져 들어오는 방향/거리 (오른쪽에서 들어온다)")]
        [SerializeField] Vector2 _slide = new(40f, 0f);
        [Tooltip("_mapper가 없을 때의 고정 위치")]
        [SerializeField] float _top = -16f;
        [SerializeField] float _x;

        struct Row
        {
            public bool Alive;
            public float Age;
            public float Hold;
            public string Title;
            /// <summary>설명 두 줄 (띠 높이 = _tallRowHeight)</summary>
            public bool Tall;
            /// <summary>첫 칸 윗변에서 이 띠 윗변까지 (부드럽게 따라감) · 이미 자리를 잡았는지</summary>
            public float Slot;
            public bool Placed;
        }

        const float InTime = 0.3f;
        const float OutTime = 0.35f;
        /// <summary>위 띠 높이가 바뀌어 아래 띠가 옮겨 앉는 빠르기 (BattleEase.Damp)</summary>
        const float SlotSharpness = 14f;

        Row[] _state;
        bool _ready;

        void Awake() => Prepare();

        void Prepare()
        {
            if (_ready) return;
            _ready = true;
            int count = _rows != null ? _rows.Length : 0;
            _state = new Row[count];
            for (int i = 0; i < count; i++)
            {
                if (_rows[i] != null) _rows[i].gameObject.SetActive(false);
            }
        }

        /// <param name="icon">null이면 아이콘 칸에 빛만 남긴다</param>
        /// <param name="iconTint">아트 아이콘이면 흰색, 흰색 대체 모양이면 색</param>
        public void Show(string title, string subtitle, Sprite icon, Color iconTint, Color accent, float hold = 1.8f)
        {
            if (string.IsNullOrEmpty(title)) return;
            Prepare();
            if (_state.Length == 0) return;

            // 같은 제목이 떠 있으면 연장만
            for (int i = 0; i < _state.Length; i++)
            {
                if (!_state[i].Alive || _state[i].Title != title) continue;
                _state[i].Hold = Mathf.Max(_state[i].Hold, _state[i].Age - InTime + hold);
                return;
            }

            int index = Take();
            ref var row = ref _state[index];
            row.Alive = true;
            row.Age = 0f;
            row.Hold = hold;
            row.Title = title;
            row.Tall = false;
            row.Placed = false;

            _rows[index].gameObject.SetActive(true);
            if (index < _titles.Length && _titles[index] != null) _titles[index].text = title;
            if (index < _subtitles.Length && _subtitles[index] != null)
            {
                var label = _subtitles[index];
                bool hasSubtitle = !string.IsNullOrEmpty(subtitle);
                label.gameObject.SetActive(hasSubtitle);
                if (hasSubtitle)
                {
                    label.text = subtitle;
                    row.Tall = NeedsTwoLines(label, subtitle);
                }
            }
            var rect = _rows[index];
            float height = row.Tall ? _tallRowHeight : _rowHeight;
            if (!Mathf.Approximately(rect.sizeDelta.y, height)) rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            if (index < _icons.Length && _icons[index] != null)
            {
                _icons[index].sprite = icon;
                _icons[index].enabled = icon != null;
                _icons[index].color = iconTint;
            }
            if (index < _iconGlows.Length && _iconGlows[index] != null) _iconGlows[index].color = BattleEase.WithAlpha(accent, 0.6f);
            if (index < _edges.Length && _edges[index] != null) _edges[index].color = BattleEase.WithAlpha(accent, 0.95f);
            Apply(index, 0f, 0f);
        }

        /// <summary>설명이 하한 크기(자동 크기 최소)에서도 칸 폭 한 줄에 들지 않는지 — 그러면 띠를 키워 두 줄로 접는다. Show마다 한 번 잰다</summary>
        static bool NeedsTwoLines(TMP_Text label, string text)
        {
            float width = label.rectTransform.rect.width;
            if (width <= 0f) return false;
            float max = label.enableAutoSizing ? label.fontSizeMax : label.fontSize;
            float floor = label.enableAutoSizing ? label.fontSizeMin : max;
            // 자동 크기면 최대 크기에서 잰 한 줄 폭 (줄바꿈 없이)
            return NeedsTwoLines(label.GetPreferredValues(text).x, max, floor, width);
        }

        /// <summary>최대 크기에서 잰 한 줄 폭을 하한 크기로 줄여도 칸 폭(+0.5 여유)을 넘는지. 순수 계산</summary>
        internal static bool NeedsTwoLines(float oneLineWidthAtMax, float max, float floor, float width)
        {
            if (oneLineWidthAtMax <= 0f || max <= 0f) return false;
            return oneLineWidthAtMax * (Mathf.Min(floor, max) / max) > width + 0.5f;
        }

        public void Clear()
        {
            if (!_ready) return;
            for (int i = 0; i < _state.Length; i++)
            {
                _state[i].Alive = false;
                _state[i].Title = null;
                _state[i].Tall = false;
                _state[i].Placed = false;
                if (_rows[i] != null) _rows[i].gameObject.SetActive(false);
            }
        }

        int Take()
        {
            int oldest = 0;
            float oldestAge = -1f;
            for (int i = 0; i < _state.Length; i++)
            {
                if (!_state[i].Alive) return i;
                if (_state[i].Age > oldestAge)
                {
                    oldestAge = _state[i].Age;
                    oldest = i;
                }
            }
            return oldest;
        }

        /// <summary>
        /// 나이 · 사라짐 · 자리 (LateUpdate): 전투 올림(Field/Camera)과 이 띠의 아래 여백은 SheetController.Update 안에서
        /// (UIFrame.SheetProgressChanged → BattleFrameDriver) 바뀐다. 기본 순서 Update끼리는 순서가 정해져 있지 않아서 Update로 놓으면
        /// 시트가 미끄러지는 동안 지난 프레임의 올림 · 여백으로 놓여 전장보다 한 프레임 늦게 따라간다 — 모든 Update 뒤에 놓는다 (UX-SPEC-IA §6.4)
        /// </summary>
        void LateUpdate()
        {
            if (!_ready) return;
            float dt = Time.unscaledDeltaTime;
            for (int i = 0; i < _state.Length; i++)
            {
                ref var row = ref _state[i];
                if (!row.Alive) continue;
                row.Age += dt;
                if (row.Age >= InTime + row.Hold + OutTime)
                {
                    row.Alive = false;
                    row.Title = null;
                    row.Tall = false;
                    row.Placed = false;
                    _rows[i].gameObject.SetActive(false);
                    continue;
                }
            }
            // 나이를 다 센 뒤 놓는다 (위 띠가 이번 프레임에 사라졌으면 아래 띠가 바로 올라오기 시작한다)
            for (int i = 0; i < _state.Length; i++)
            {
                if (_state[i].Alive) Apply(i, _state[i].Age, dt);
            }
        }

        /// <summary>첫 칸 윗변에서 index 칸 윗변까지: 위 칸마다 _rowSpacing + (살아 있는 두 줄 띠면 키운 높이만큼 더)</summary>
        float SlotOffset(int index)
        {
            float offset = 0f;
            float extra = Mathf.Max(0f, _tallRowHeight - _rowHeight);
            for (int j = 0; j < index && j < _state.Length; j++)
            {
                offset += _rowSpacing;
                if (_state[j].Alive && _state[j].Tall) offset += extra;
            }
            return offset;
        }

        void Apply(int index, float age, float dt)
        {
            ref var row = ref _state[index];
            float target = SlotOffset(index);
            if (!row.Placed || UIMotion.Reduced)
            {
                row.Slot = target;
                row.Placed = true;
            }
            else if (!Mathf.Approximately(row.Slot, target)) row.Slot = BattleEase.Damp(row.Slot, target, SlotSharpness, dt);
            float x = _x;
            float top = _top;
            if (_mapper != null)
            {
                var area = (RectTransform)transform;
                x = area.rect.width * 0.5f - _rightMargin;
                // 월드 공간을 거쳐 이 띠 기준으로 — 시트가 열리면 띠 아래 여백(BattleFrameDriver)과 전투 올림이 달라서 띠 가운데 ≠ 전장 가운데 (UX-SPEC-IA §6.4)
                top = area.InverseTransformPoint(_mapper.FieldToCanvasWorld(_mapper.WorldToField(new Vector2(0f, _worldTop)))).y - area.rect.center.y;
            }
            float slotY = top - row.Slot;
            float alpha;
            float scale;
            float offset;
            if (age < InTime)
            {
                float k = age / InTime;
                alpha = Mathf.Clamp01(k * 2f);
                scale = Mathf.LerpUnclamped(0.7f, 1f, BattleEase.OutBack(k, 2.2f));
                offset = 1f - BattleEase.OutCubic(k);
            }
            else if (age < InTime + row.Hold)
            {
                alpha = 1f;
                scale = 1f;
                offset = 0f;
            }
            else
            {
                float k = (age - InTime - row.Hold) / OutTime;
                alpha = 1f - k;
                scale = 1f - 0.05f * k;
                offset = 0.66f * BattleEase.OutCubic(k);
            }
            var rect = _rows[index];
            rect.anchoredPosition = new Vector2(x + _slide.x * offset, slotY + _slide.y * offset);
            rect.localScale = new Vector3(scale, scale, 1f);
            if (index < _groups.Length && _groups[index] != null) _groups[index].alpha = alpha;
            if (index < _iconGlows.Length && _iconGlows[index] != null)
            {
                float pulse = 0.85f + 0.15f * Mathf.Sin(age * 5f);
                _iconGlows[index].rectTransform.localScale = new Vector3(pulse, pulse, 1f);
            }
        }
    }
}
