using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 진화 레시피 ② 진화 조건 한 줄: 체크 / 조건 / 진행 막대 / 진행도 / 기억편 가속.
    /// 조건 글 자리(투명 버튼 + 22px '›', M71)를 누르면 그 조건을 채우는 곳으로 간다 (CharacterUI.GoToCondition — 원정 · 던전 · 전투 · 편성 · 상세).
    /// 다 채운 조건은 '›'를 숨기고 누를 수 없다. 칠한 버튼은 '가속' 하나뿐이다.
    /// 진행 막대는 늘 때만 부드럽게 따라간다 (M62 — CharacterUI.EaseFill, 코루틴은 움직일 때만). 다른 조건에 다시 묶이거나 켜질 때 · 움직임 줄이기면 바로 맞춘다.
    /// 새 직렬화 필드(_goButton · _goChevron)는 비어 있어도 된다 (다시 빌드하기 전 씬).
    /// </summary>
    public class RecipeConditionView : MonoBehaviour
    {
        [SerializeField] Image _checkCircle;
        [SerializeField] GameObject _checkMark;
        [SerializeField] TMP_Text _labelText;
        [SerializeField] TMP_Text _progressText;
        [SerializeField] RectTransform _progressFill;
        [SerializeField] Image _progressFillImage;
        [SerializeField] Button _accelerateButton;
        [SerializeField] TMP_Text _accelerateCostText;
        [Tooltip("조건 글 자리의 투명 버튼 (누르면 채우는 곳으로)")]
        [SerializeField] Button _goButton;
        [Tooltip("조건 글 끝 '›' (다 채우면 숨김)")]
        [SerializeField] GameObject _goChevron;

        string _formId;
        int _index;
        int _cost;
        string _label;
        ProgressType _type;

        // 막대 (M62)
        string _boundForm;
        int _boundIndex = -1;
        float _fillShown = -1f;
        float _fillTarget;
        Coroutine _fillRoutine;

        void Awake()
        {
            _accelerateButton.onClick.AddListener(OnAccelerate);
            if (_goButton != null) _goButton.onClick.AddListener(OnGo);
        }

        void OnEnable()
        {
            // 꺼져 있는 동안 바뀐 값은 다시 채우지 않고 바로 맞춘다
            if (_fillShown >= 0f) SnapFill(_fillTarget);
        }

        void OnDisable() => _fillRoutine = null;

        public void Set(string formId, int index, ConditionStatus status)
        {
            bool rebind = formId != _boundForm || index != _boundIndex;
            _formId = formId;
            _index = index;
            _boundForm = formId;
            _boundIndex = index;
            _label = status.Label;
            _type = status.Condition != null ? status.Condition.type : ProgressType.StageReached;
            int target = Mathf.Max(0, status.Condition != null ? status.Condition.target : 0);
            int progress = Mathf.Min(status.Progress, target);

            _labelText.text = status.Label;
            // 스테이지 조건은 다른 화면과 같은 "챕터-번호" 표기로 보여 준다
            _progressText.text = status.Condition != null && status.Condition.type == ProgressType.StageReached
                ? $"{BattleSystem.FormatStage(Mathf.Max(1, progress))} / {BattleSystem.FormatStage(target)}"
                : $"{progress:N0} / {target:N0}";
            _progressText.color = status.Done ? UITheme.Positive : UITheme.Text;
            SetFillTarget(target > 0 ? (float)progress / target : 1f, rebind);
            _progressFillImage.color = status.Done ? UITheme.Positive : UITheme.Lavender;
            _checkMark.SetActive(status.Done);
            // 체크 고리(ui_ring_stitch, 흰색 틴트용)는 밝게만 물들인다: 완료 = 초록 60%, 진행 중 = 흰색 반투명
            _checkCircle.color = status.Done ? Color.Lerp(Color.white, UITheme.Positive, 0.6f) : CharacterUI.WithAlpha(Color.white, 0.5f);

            _cost = status.Done ? 0 : Game.Evolution.GetAccelerateCost(formId, index);
            _accelerateButton.gameObject.SetActive(_cost > 0);
            _accelerateCostText.text = $"기억편 {_cost:N0}";

            bool canGo = !status.Done;
            if (_goButton != null) _goButton.interactable = canGo;
            if (_goChevron != null && _goChevron.activeSelf != canGo) _goChevron.SetActive(canGo);
        }

        // ───────────── 막대 ─────────────

        void SetFillTarget(float target, bool rebind)
        {
            _fillTarget = Mathf.Clamp01(target);
            if (rebind || _fillShown < 0f || !isActiveAndEnabled || UIMotion.Reduced || _fillTarget <= _fillShown)
            {
                SnapFill(_fillTarget);
                return;
            }
            if (_fillRoutine == null) _fillRoutine = StartCoroutine(FillTween());
        }

        void SnapFill(float value)
        {
            if (_fillRoutine != null) StopCoroutine(_fillRoutine);
            _fillRoutine = null;
            _fillShown = value;
            CharacterUI.SetFill(_progressFill, value);
        }

        IEnumerator FillTween()
        {
            while (_fillShown < _fillTarget)
            {
                _fillShown = CharacterUI.EaseFill(_fillShown, _fillTarget, Time.unscaledDeltaTime);
                CharacterUI.SetFill(_progressFill, _fillShown);
                yield return null;
            }
            _fillRoutine = null;
        }

        // ───────────── 동작 ─────────────

        void OnGo()
        {
            if (!string.IsNullOrEmpty(_formId)) CharacterUI.GoToCondition(_formId, _type);
        }

        void OnAccelerate()
        {
            var lineage = Game.Characters.Db.GetLineageOfForm(_formId);
            if (lineage == null || _cost <= 0) return;
            int shards = Game.Characters.GetShards(lineage.id);
            if (shards < _cost)
            {
                CharacterUI.Toast($"{lineage.name} 기억편이 부족해요. (보유 {shards:N0} / 필요 {_cost:N0})");
                return;
            }

            string formId = _formId;
            int index = _index;
            string label = _label;
            UIManager.Instance.ShowConfirm("진화 가속",
                $"기억편 {_cost:N0}개로\n'{label}' 조건을 바로 채울까요?",
                () =>
                {
                    if (Game.Evolution.TryAccelerate(formId, index)) CharacterUI.Toast($"'{label}' 조건을 채웠어요!", ToastKind.Success);
                    else CharacterUI.Toast("지금은 가속할 수 없어요.");
                }, "가속");
        }
    }
}
