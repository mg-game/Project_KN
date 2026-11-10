using System;
using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 소환 결과 카드. 뒷면으로 등장 → 뒤집혀 앞면 공개, 등급 색 빛.
    /// ★4 이상은 뒷면이 금빛으로 떨리다가 별빛이 크게 터진다 (강한 진동 한 번 — Haptics가 400ms에 한 번으로 묶는다).
    /// 앞면 배경은 등급별 카드 그림(card_bg_grade1~5)을 색 없이(흰색) 깐다.
    /// 번쩍임 정점은 GameSettings.ClampFlashAlpha(0.75) (번쩍임 줄이기면 0.3), 움직임 줄이기면 별빛 수가 절반이고 떨림이 없다 (M64).
    /// 소리 (AUDIO-SPEC §8.1): 등장 GachaCardAppear, ★4 이상 예고 GachaAnticipate, 공개는 등급별 (★1 Common · ★2 Rare · ★3 Epic · ★4 GachaRare · ★5 Mythic).
    /// 한 번에 공개(건너뛰기)할 때는 카드마다 소리를 내지 않는다 (결과 창이 GachaSkip 하나만).
    /// 분실물 꾸러미 공개 (CR-04): 결과 창이 PlayAppear(조용히 튀어나옴)와 PlayQuickFlip(0.08초 뒤집기 — 소리는 결과 창이 고른다)로 박자를 맞춘다.
    /// 뒷면 그림은 빌더가 꾸러미 그림(UiArt.GachaBundle 묶임 — 없으면 카드 뒷면)으로 굽는다. 움직임 줄이기면 뒤집기 대신 바로 바뀐다(알파만).
    /// </summary>
    public class GachaCardView : MonoBehaviour
    {
        [SerializeField] RectTransform _flip;
        [SerializeField] GameObject _back;
        [SerializeField] Image _backGlow;
        [SerializeField] GameObject _front;
        /// <summary>앞면 배경 그림 (등급에 맞춰 스프라이트를 바꾼다)</summary>
        [SerializeField] Image _gradeBack;
        /// <summary>등급별 앞면 배경 (ItemGrade 순서: 일반 → 신화)</summary>
        [SerializeField] Sprite[] _gradeBacks;
        [SerializeField] Image _glow;
        [SerializeField] Image _gradeLine;
        [SerializeField] FriendPortrait _portrait;
        [SerializeField] TMP_Text _nameText;
        [SerializeField] TMP_Text _starsText;
        [SerializeField] GameObject _newBadge;
        [SerializeField] GameObject _newChip;
        [SerializeField] GameObject _shardChip;
        [SerializeField] TMP_Text _shardText;
        [SerializeField] GameObject _pityTag;
        [SerializeField] Image _flash;
        [SerializeField] SparkleBurst _burst;

        GachaResult _result;
        Action _frontWithSound;
        Action _frontSilent;

        public bool Revealed { get; private set; }

        bool IsHighGrade => _result != null && _result.Lineage.grade >= ItemGrade.Legendary;

        public void Set(GachaResult result)
        {
            StopAllCoroutines();
            _result = result;
            Revealed = false;
            var lineage = result.Lineage;
            var gradeColor = UITheme.GradeColor(lineage.grade);

            _portrait.Set(CharacterUI.Portrait(CharacterUI.BaseForm(lineage)), lineage, lineage.name, PortraitTone.Normal);
            SetGradeBack(lineage.grade);
            _nameText.text = lineage.name;
            _starsText.text = UITheme.Stars(lineage.grade);
            _starsText.color = gradeColor;
            _gradeLine.color = gradeColor;
            _glow.color = CharacterUI.WithAlpha(gradeColor, _glow.color.a);
            _backGlow.color = CharacterUI.WithAlpha(IsHighGrade ? UITheme.Gold : UITheme.Lavender, 0.45f);

            _newBadge.SetActive(result.IsNew);
            _newChip.SetActive(result.IsNew);
            _shardChip.SetActive(!result.IsNew);
            _shardText.text = $"기억편 +{result.ShardsGained}";
            _pityTag.SetActive(result.IsPity);

            _back.SetActive(true);
            _front.SetActive(false);
            _glow.gameObject.SetActive(false);
            _flash.color = CharacterUI.WithAlpha(Color.white, 0f);
            _flip.localScale = Vector3.one;
            transform.localScale = Vector3.zero;
        }

        /// <summary>등급 배경 그림으로 바꾼다. 그림이 없으면 빌더가 깔아 둔 기본 바탕을 그대로 둔다.</summary>
        void SetGradeBack(ItemGrade grade)
        {
            if (_gradeBack == null || _gradeBacks == null) return;
            int index = Mathf.Clamp((int)grade, 0, _gradeBacks.Length - 1);
            if (index < 0 || _gradeBacks[index] == null) return;
            _gradeBack.sprite = _gradeBacks[index];
            _gradeBack.color = Color.white;
        }

        /// <summary>공개 연출에 걸리는 대략적인 시간 (다음 카드 순서 맞추기용)</summary>
        public float RevealDuration => IsHighGrade ? 1.05f : 0.5f;

        /// <summary>등장 → (고등급 예고) → 뒤집기 → 빛. 카드 자신이 코루틴을 돌린다.</summary>
        public void PlayReveal()
        {
            if (!isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(Reveal());
        }

        IEnumerator Reveal()
        {
            UISfx.Play(UISfxKey.GachaCardAppear);
            yield return CharacterMotion.Pop(transform, 0.2f, 0f);
            if (IsHighGrade) yield return Anticipate();
            yield return CharacterMotion.Flip(_flip, 0.26f, ShowFrontWithSound);
            yield return Flash();
        }

        /// <summary>연출 없이 바로 앞면 (소리 없음 — 건너뛰기는 결과 창이 소리 하나로 알린다)</summary>
        public void RevealInstant()
        {
            StopAllCoroutines();
            transform.localScale = Vector3.one;
            _flip.localScale = Vector3.one;
            _flip.localRotation = Quaternion.identity;
            if (!Revealed) ShowFront(false);
            _flash.color = CharacterUI.WithAlpha(Color.white, 0f);
        }

        /// <summary>뒷면(꾸러미)으로 조용히 튀어나온다 (10회 공개 — 소리는 결과 창이 한 번만). 꺼져 있으면 바로 제 크기</summary>
        public void PlayAppear(float duration)
        {
            if (!isActiveAndEnabled)
            {
                transform.localScale = Vector3.one;
                return;
            }
            StopAllCoroutines();
            StartCoroutine(CharacterMotion.Pop(transform, Mathf.Max(0.01f, duration), 0f));
        }

        /// <summary>빠른 뒤집기 (10회 공개: duration 0.08초, 끝나면 번쩍임). sound = 등급 공개 소리 (크게 보여 준 장은 결과 창이 이미 냈다)</summary>
        public void PlayQuickFlip(float duration, bool sound)
        {
            if (!isActiveAndEnabled)
            {
                RevealInstant();
                return;
            }
            StopAllCoroutines();
            transform.localScale = Vector3.one;
            StartCoroutine(QuickFlip(duration, sound));
        }

        IEnumerator QuickFlip(float duration, bool sound)
        {
            if (UIMotion.Reduced)
            {
                // 움직임 줄이기: 뒤집지 않고 바로 앞면 (알파만 — 번쩍임은 ClampFlashAlpha가 줄인다)
                ShowFront(sound);
                yield return Flash();
                yield break;
            }
            _frontWithSound ??= ShowFrontWithSound;
            _frontSilent ??= ShowFrontSilent;
            yield return CharacterMotion.Flip(_flip, Mathf.Max(0.02f, duration), sound ? _frontWithSound : _frontSilent);
            yield return Flash();
        }

        void ShowFrontWithSound() => ShowFront(true);

        void ShowFrontSilent() => ShowFront(false);

        void ShowFront(bool sound)
        {
            Revealed = true;
            _back.SetActive(false);
            _front.SetActive(true);
            _glow.gameObject.SetActive(_result.Lineage.grade >= ItemGrade.Rare);
            _flip.localRotation = Quaternion.identity;
            if (!sound) return;
            UISfx.Play(RevealKey(_result.Lineage.grade));
            if (IsHighGrade) Haptics.Play(HapticKind.Heavy);
        }

        /// <summary>등급별 공개 소리 (★1 일반 → ★5 신화)</summary>
        internal static UISfxKey RevealKey(ItemGrade grade) => grade switch
        {
            ItemGrade.Mythic => UISfxKey.GachaRevealMythic,
            ItemGrade.Legendary => UISfxKey.GachaRare,
            ItemGrade.Epic => UISfxKey.GachaRevealEpic,
            ItemGrade.Rare => UISfxKey.GachaRevealRare,
            _ => UISfxKey.GachaRevealCommon,
        };

        /// <summary>★4 이상: 뒷면이 금빛으로 밝아지며 떨린다 (움직임 줄이기면 빛만)</summary>
        IEnumerator Anticipate()
        {
            const float duration = 0.45f;
            bool shake = !UIMotion.Reduced;
            UISfx.Play(UISfxKey.GachaAnticipate);
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                float k = t / duration;
                _backGlow.color = CharacterUI.WithAlpha(UITheme.Gold, Mathf.Lerp(0.45f, 1f, k));
                if (shake) _flip.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(t * 70f) * 4f * k);
                yield return null;
            }
            _flip.localRotation = Quaternion.identity;
        }

        IEnumerator Flash()
        {
            var gradeColor = UITheme.GradeColor(_result.Lineage.grade);
            if (IsHighGrade)
            {
                _burst.Play(gradeColor, CharacterMotion.BurstCount(18), 1f);
                _burst.Play(UITheme.Cream, CharacterMotion.BurstCount(10), 0.8f);
            }
            else if (_result.Lineage.grade >= ItemGrade.Epic)
            {
                _burst.Play(gradeColor, CharacterMotion.BurstCount(8), 0.7f);
            }

            const float duration = 0.3f;
            float peak = GameSettings.ClampFlashAlpha(0.75f);
            for (float t = 0; t < duration; t += Time.unscaledDeltaTime)
            {
                _flash.color = CharacterUI.WithAlpha(Color.white, peak * (1f - t / duration));
                yield return null;
            }
            _flash.color = CharacterUI.WithAlpha(Color.white, 0f);
        }
    }
}
