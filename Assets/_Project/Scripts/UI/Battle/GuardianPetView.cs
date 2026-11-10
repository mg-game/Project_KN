using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 전투 중 쓰다듬기 (CR-10, SPEC4 §6.3): 지킴이 몸(그림 사각형 × 0.8)의 투명 'PetHit'를 누르면 0.2초 눌렸다 펴지고, 그 계보의 성격 대사(부록 A.4)를
    /// SkillCalloutView.SayPet으로 1.5초 (CalloutGate를 거치지 않는다 — 스킬 이름이 아니다), 소리는 그 계보 목소리 vox_{계보}_pet(캐릭터 목소리 켜짐 · 클립 있음 · 8초에 한 번) 아니면 pet_squeak.
    /// 하루(DayKey) 계보마다 3번까지 호감도 +5 (AffinitySystem.TryPet) — 그때만 40px 하트(Icons/affinity)가 0.8초에 60px 떠오른다.
    /// 튜토리얼 중 · 잔향이 조준 중 · 잠자기 중에는 쉰다. 비어 있는 받침대의 빈칸 단추(_emptyButton)는 그대로 (PetHit는 채워졌을 때만 입력을 받는다).
    /// 빌더가 만들기 전(사용자 씬)에는 없다
    /// </summary>
    public class GuardianPetView : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] GuardianSlotView _guardian;
        [SerializeField] SkillCalloutView _callouts;
        [SerializeField] SummonAimView _aim;
        [SerializeField] Image _heart;

        public const float HeartRise = 60f;
        public const float HeartTime = 0.8f;
        public const int PetsPerDay = 3;

        float _heartAge = -1f;
        Vector2 _heartBase;

        void Awake()
        {
            if (_heart != null)
            {
                _heartBase = _heart.rectTransform.anchoredPosition;
                _heart.enabled = false;
                _heart.raycastTarget = false;
            }
        }

        /// <summary>지금 쓰다듬을 수 있는지 (튜토리얼 · 조준 · 잠자기 · 빈 받침대가 아니고 메타가 켜져 있을 때)</summary>
        public bool CanPet =>
            _guardian != null && _guardian.State != null && Game.IsReady && Game.IsMetaActive
            && !(Game.Tutorial != null && Game.Tutorial.IsActive) && !(_aim != null && _aim.IsActive) && !AppSignals.IsAsleep;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!CanPet) return;
            var state = _guardian.State;
            _guardian.PlayPet();
            string line = null;
            bool gained = false;
            var affinity = Game.Affinity;
            if (affinity != null)
            {
                try
                {
                    affinity.TryPet(state.LineageId, out line, out gained);
                }
                catch (System.Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            if (string.IsNullOrEmpty(line)) line = FallbackLine(state.LineageId);
            if (gained) StartHeart();
            if (_callouts != null && !string.IsNullOrEmpty(line)) _callouts.SayPet(_guardian, line, BattleUnitArt.MainColor(state.LineageId, state.FormId));
            PlayPetSound(state.LineageId);
        }

        /// <summary>계보 목소리 vox_{계보}_pet 간격 (AUDIO-SPEC §2.10 'CR-10 tap-to-pet ≤ 1 per 8 s, else pet_squeak')</summary>
        public const float PetBarkInterval = 8f;
        /// <summary>마지막 쓰다듬기 목소리 (실제 시간 — 지킴이 다섯이 함께 센다)</summary>
        static float s_lastBark = float.NegativeInfinity;
        string _barkLineage;
        AudioId _bark;

        /// <summary>
        /// 목소리(캐릭터 목소리 설정이 켜져 있고 그 계보의 vox_{계보}_pet 클립이 있고 8초가 지났으면) 아니면 pet_squeak.
        /// 남은 틈: 엔진의 목소리 공통 간격(6초 — 다른 목소리가 막 울렸으면)이 막으면 그 한 번은 조용하다 (AUD에 bool을 돌려주는 재생을 요청함)
        /// </summary>
        void PlayPetSound(string lineageId)
        {
            float now = Time.unscaledTime;
            var bark = BarkId(lineageId);
            if (ShouldBark(AudioService.VoiceBarks && AudioService.IsRunning, HasClip(bark), now, s_lastBark))
            {
                s_lastBark = now;
                AudioService.Play(bark);
                return;
            }
            UISfx.Play(UISfxKey.PetSqueak);
        }

        /// <summary>목소리를 낼지 (순수): 설정 켜짐 · 클립 있음 · 마지막 목소리에서 PetBarkInterval 지남 (시계가 되돌아갔으면 — 새 재생 — 허용)</summary>
        public static bool ShouldBark(bool voiceBarks, bool hasClip, float now, float lastBark) =>
            voiceBarks && hasClip && (now < lastBark || now - lastBark >= PetBarkInterval);

        /// <summary>그 계보의 쓰다듬기 목소리 번호 (계보가 바뀔 때만 한 번 찾는다 — 누를 때만 불린다)</summary>
        AudioId BarkId(string lineageId)
        {
            if (string.IsNullOrEmpty(lineageId)) return AudioId.None;
            if (!string.Equals(lineageId, _barkLineage, System.StringComparison.Ordinal))
            {
                _barkLineage = lineageId;
                _bark = AudioService.Resolve("vox_" + lineageId + "_pet");
            }
            return _bark;
        }

        /// <summary>그 키(별칭이면 대상)에 구운 클립이 있는지 — 목소리 클립이 아직 없으면 pet_squeak으로 (조용한 쓰다듬기가 없게)</summary>
        static bool HasClip(AudioId id)
        {
            if (!id.IsValid) return false;
            var library = UISfx.Library;
            return library != null && (library.Has(id) || library.Has(AudioCatalog.ClipSource(id)));
        }

        /// <summary>콘텐츠의 쓰다듬기 대사 7개 중 하나 (호감도 모듈이 대사를 주지 않았을 때 — 없으면 null)</summary>
        static string FallbackLine(string lineageId)
        {
            var lines = Game.MetaDb != null ? Game.MetaDb.GetAffinityLines(lineageId) : null;
            var pet = lines != null ? lines.pet : null;
            if (pet == null || pet.Length == 0) return null;
            int start = Random.Range(0, pet.Length);
            for (int i = 0; i < pet.Length; i++)
            {
                string line = pet[(start + i) % pet.Length];
                if (!string.IsNullOrEmpty(line)) return line;
            }
            return null;
        }

        void StartHeart()
        {
            if (_heart == null) return;
            _heartAge = 0f;
            _heart.enabled = true;
            _heart.rectTransform.anchoredPosition = _heartBase;
        }

        void Update()
        {
            if (_heartAge < 0f || _heart == null) return;
            _heartAge += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(_heartAge / HeartTime);
            float rise = UIMotion.Reduced ? HeartRise * 0.5f : HeartRise * BattleEase.OutCubic(k);
            _heart.rectTransform.anchoredPosition = _heartBase + new Vector2(0f, rise);
            float pop = k < 0.15f ? BattleEase.OutBack(k / 0.15f, 2f) : 1f;
            _heart.rectTransform.localScale = new Vector3(pop, pop, 1f);
            _heart.color = new Color(1f, 1f, 1f, 1f - Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.6f, 1f, k)));
            if (k < 1f) return;
            _heartAge = -1f;
            _heart.enabled = false;
        }
    }
}
