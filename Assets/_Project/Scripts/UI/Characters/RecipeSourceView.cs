using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 진화 레시피 ③ 획득처 한 줄: 재료 → 획득처 + 버튼 (M17 · M39 · M76). 줄 순서는 레시피가 정한다 (갈 수 있는 곳 먼저, 잠긴 곳은 열리는 스테이지 순).
    ///   · 갈 수 있는 줄: 보라 '가기 ›' → 돌아가기 칸 '‹ {형태} 레시피로' → 팝업을 닫고 그곳으로 (스테이지 획득처는 이동 없이 안내 토스트 — CharacterUI.GoToSource)
    ///   · 잠긴 줄: 줄 전체 α0.55 + 아이콘 위 28px 자물쇠 + 획득처 아래 한 줄 '스테이지 2-9 클리어 시' (MetaText.UnlockShort — 스테이지가 아닌 이유면 그 이유),
    ///     버튼은 보조 '잠김'. 누를 수 있고 누르면 '{재료}은(는) {이유}' 토스트 (팝업 그대로).
    /// 상태가 바뀔 때만 모양을 다시 입힌다. 새 직렬화 필드(_group · _lock · _unlockText · _goLabel · 버튼 그림)는 비어 있어도 된다 (다시 빌드하기 전 씬).
    /// </summary>
    public class RecipeSourceView : MonoBehaviour
    {
        /// <summary>잠긴 줄 알파</summary>
        public const float LockedAlpha = 0.55f;
        /// <summary>잠긴 줄에서 획득처 이름을 올리는 양 (아래에 열리는 조건 한 줄)</summary>
        const float LockedSourceLift = 13f;
        const string GoLabel = "가기 ›";
        const string LockedLabel = "잠김";

        [SerializeField] Image _iconBackground;
        [SerializeField] Image _icon;
        [SerializeField] TMP_Text _iconLetter;
        [SerializeField] TMP_Text _materialText;
        [SerializeField] TMP_Text _sourceText;
        [SerializeField] GameObject _needTag;
        [SerializeField] Button _goButton;
        [Tooltip("줄 전체 알파 (잠기면 0.55)")]
        [SerializeField] CanvasGroup _group;
        [Tooltip("아이콘 위 28px 자물쇠 (잠긴 줄만)")]
        [SerializeField] GameObject _lock;
        [Tooltip("획득처 아래 열리는 조건 한 줄 (잠긴 줄만)")]
        [SerializeField] TMP_Text _unlockText;
        [SerializeField] TMP_Text _goLabel;
        [Tooltip("갈 수 있는 줄 버튼 그림 (보라)")]
        [SerializeField] Sprite _primarySprite;
        [Tooltip("잠긴 줄 버튼 그림 (보조)")]
        [SerializeField] Sprite _secondarySprite;

        ItemDef _item;
        ItemSource _source;
        string _formId;
        bool _locked;
        string _reason;
        /// <summary>마지막으로 입힌 모양 (-1 = 아직, 0 = 갈 수 있음, 1 = 잠김)</summary>
        int _shownState = -1;
        float _sourceBaseY;
        bool _sourceBaseKnown;
        Coroutine _punch;

        void Awake() => _goButton.onClick.AddListener(OnGo);

        /// <summary>튕기는 도중 꺼지면(레시피를 바로 닫음) 코루틴의 마지막 줄이 돌지 않는다 — 풀로 다시 쓰이는 줄이 커진 채 남지 않게 크기를 되돌린다</summary>
        void OnDisable()
        {
            _punch = null;
            transform.localScale = Vector3.one;
        }

        /// <param name="check">SourceAvailability.Check 결과 (레시피가 SortForDisplay로 받은 것)</param>
        /// <param name="formId">돌아가기 칸 '‹ {형태} 레시피로'를 만들 형태</param>
        public void Set(ItemDef item, ItemSource source, bool missing, SourceCheck check, string formId)
        {
            _item = item;
            _source = source;
            _formId = formId;
            _locked = !check.Available;
            _reason = check.Reason;
            CharacterUI.SetItemIcon(_icon, _iconBackground, _iconLetter, item);
            _materialText.text = item != null ? item.name : string.Empty;
            _sourceText.text = CharacterUI.SourceLabel(source);
            if (_needTag.activeSelf != missing) _needTag.SetActive(missing);
            if (_locked && _unlockText != null) _unlockText.text = LockedLine(check.RequiredStage, check.Reason);
            ApplyState(_locked);
        }

        /// <summary>
        /// 잠긴 줄 아래 한 줄: 열리는 스테이지를 알면 '스테이지 2-9 클리어 시' (MetaText.UnlockShort), 모르면(품절 · 상점에 없음 등) 이유 그대로.
        /// </summary>
        internal static string LockedLine(int requiredStage, string reason)
        {
            if (requiredStage > 1 && requiredStage < int.MaxValue)
            {
                string unlock = MetaText.UnlockShort(requiredStage);
                if (!string.IsNullOrEmpty(unlock)) return unlock;
            }
            return reason ?? string.Empty;
        }

        void ApplyState(bool locked)
        {
            int state = locked ? 1 : 0;
            if (state == _shownState) return;
            _shownState = state;

            if (_group != null) _group.alpha = locked ? LockedAlpha : 1f;
            if (_lock != null && _lock.activeSelf != locked) _lock.SetActive(locked);
            if (_unlockText != null && _unlockText.gameObject.activeSelf != locked) _unlockText.gameObject.SetActive(locked);
            if (_unlockText != null)
            {
                // 획득처 이름을 위로 올려 아래 한 줄 자리를 낸다 (빌드 위치 기준)
                var rt = _sourceText.rectTransform;
                if (!_sourceBaseKnown)
                {
                    _sourceBaseY = rt.anchoredPosition.y;
                    _sourceBaseKnown = true;
                }
                rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, _sourceBaseY + (locked ? LockedSourceLift : 0f));
            }
            if (_goLabel != null) _goLabel.text = locked ? LockedLabel : GoLabel;

            // 버튼 바탕: 갈 수 있음 = 보라, 잠김 = 보조 (그림이 없으면 단색)
            if (_goButton.targetGraphic is Image image)
            {
                var sprite = locked ? _secondarySprite : _primarySprite;
                if (sprite != null) image.sprite = sprite;
                else if (_primarySprite == null && _secondarySprite == null) image.color = locked ? UITheme.Secondary : UITheme.PrimaryButton;
            }
        }

        /// <summary>레시피의 '획득처 ›'로 찾아왔을 때 한 번 튕긴다 (움직임 줄이기면 없음)</summary>
        public void Punch()
        {
            if (UIMotion.Reduced || !isActiveAndEnabled) return;
            if (_punch != null) StopCoroutine(_punch);
            _punch = StartCoroutine(PunchRoutine());
        }

        System.Collections.IEnumerator PunchRoutine()
        {
            yield return CharacterMotion.Punch(transform, 0.06f, UIMotion.PunchDuration);
            transform.localScale = Vector3.one;
            _punch = null;
        }

        void OnGo()
        {
            if (_source == null) return;
            if (_locked)
            {
                CharacterUI.Toast(CharacterUI.LockedSourceToast(_item != null ? _item.name : null, _reason));
                return;
            }
            CharacterUI.GoToSource(_item, _source, _formId);
        }
    }
}
