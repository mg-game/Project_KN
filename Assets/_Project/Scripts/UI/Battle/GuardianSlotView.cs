using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 방위 링 한 칸: 방위 색 받침대 + 지킴이(크고 불투명, 방위 색 후광, 방패 배지) + 바닥 부채꼴 요격 범위.
    /// 빈칸은 흐린 "+" 받침대로 보여주고, 누르면 편성 탭으로 안내한다.
    /// 몸 왼쪽의 작은 스킬 보석은 고유 스킬 그림 + 쿨타임 고리를 보여주고, 준비되면 금빛으로 반짝인다.
    /// 강화/약화 중이면 머리 위에 왕관/표식이 뜬다.
    /// 상시 패시브 '녹아드는 빛'(공격력 상승)은 전투가 길어질수록 몸이 조금씩 녹아 작아지고, 후광이 촛불빛으로 달아오르며 촛농이 떨어진다.
    /// </summary>
    public class GuardianSlotView : MonoBehaviour
    {
        [SerializeField] BattleFieldMapper _mapper;
        [SerializeField] int _slot;

        [Header("지킴이 (액터 레이어)")]
        [Tooltip("숨쉬기/공격 찌그러짐이 적용되는 몸 (발 기준 pivot)")]
        [SerializeField] RectTransform _body;
        [SerializeField] Image _portrait;
        [SerializeField] Image _fallback;
        [SerializeField] TMP_Text _fallbackInitial;
        [SerializeField] Image _halo;
        [Tooltip("역할 배지 (Icons/style_* 그림을 그대로, 없으면 빌더가 넣은 기본 그림)")]
        [SerializeField] Image _badge;
        [Tooltip("true = 예전 흰 원 배지라 방위 색을 입힌다, false = 아트 배지 (흰색 그대로)")]
        [SerializeField] bool _badgeTinted;

        [Header("받침대 (받침대 레이어)")]
        [SerializeField] RectTransform _pad;
        [SerializeField] Image _padImage;
        [Tooltip("지킴이가 있을 때의 받침대 그림 (Battle/guardian_pad_{방위 색}). 비어 있으면 _padImage 그림 그대로")]
        [SerializeField] Sprite _padFilledSprite;
        [Tooltip("빈칸 받침대 그림 (Battle/guardian_pad_empty). 비어 있으면 _padImage 그림 그대로")]
        [SerializeField] Sprite _padEmptySprite;
        [Tooltip("true = 받침대 그림이 이미 방위 색이라 흰색으로 그린다 (공격 때 크림빛으로만 번쩍임), false = 흰 받침대에 방위 색을 입힌다")]
        [SerializeField] bool _padColored;
        [SerializeField] Image _padGlow;
        [SerializeField] GameObject _emptyMark;
        [SerializeField] Button _emptyButton;
        [Tooltip("쓰다듬기 누름 자리 (CR-10 — 투명, 채워졌을 때만 입력, GuardianPetView). 없으면 쓰다듬기 없음")]
        [SerializeField] Image _petHit;
        [Tooltip("잔향이 드래그 중 가장 가까운 방위 강조")]
        [SerializeField] Image _aimHighlight;

        [Header("고유 스킬 보석")]
        [SerializeField] RectTransform _skillGem;
        [SerializeField] Image _skillIcon;
        [Tooltip("쿨타임 고리 (Radial360)")]
        [SerializeField] Image _skillRing;
        [SerializeField] Image _skillGlow;
        [SerializeField] SkillVfxLibrary _library;

        [Header("강화 / 약화")]
        [SerializeField] Image _statusIcon;
        [SerializeField] Sprite _buffSprite;
        [SerializeField] Sprite _debuffSprite;
        [SerializeField] Color _buffTint = Color.white;
        [SerializeField] Color _debuffTint = Color.white;

        [Header("요격 범위 (바닥 평면)")]
        [SerializeField] Image _fan;
        [SerializeField] float _fanBaseAlpha = 0.14f;
        [SerializeField] float _fanDangerAlpha = 0.3f;

        GuardianState _state;
        Color _slotColor = Color.white;
        int _layoutVersion = -1;
        float _range = 1.6f;
        float _phase;
        float _attackPunch;
        float _leanDirection = 1f;
        float _danger;
        float _highlight;
        bool _highlightOn;
        float _cast;
        float _skillReady;
        float _status;
        int _statusKind;
        bool _hasSkill;
        SkillDef _skill;
        UnitPassive _passive;
        float _melt;
        bool _meltFull;
        float _dripTimer;
        Sprite _badgeDefault;

        // 기본 공격 몸짓 (FX-JUICE E1): 웅크림 W → 놓음(늘어남 + 대상 쪽 기울기) → 반동 6px → 용수철로 제자리
        float _shotAge = Idle;
        float _windup;
        Vector2 _shotDir = Vector2.up;
        // 축하 (E8d): 통 뛰기 + 머리 위 반짝임, 지킴이마다 2.5초에 한 번
        float _celebrateCooldown;
        float _celebrateDelay = -1f;
        float _hopAge = Idle;

        const float Idle = 99f;
        /// <summary>웅크림 꼭대기 (가로 1.10 / 세로 0.88, 대상 반대쪽으로 4°)</summary>
        const float WindupSquashX = 0.10f;
        const float WindupSquashY = 0.12f;
        const float WindupLean = 4f;
        /// <summary>놓는 순간 늘어남 (가로 0.92 / 세로 1.12, 대상 쪽으로 7°) — 용수철 5Hz, ζ 0.5로 220ms쯤 제자리</summary>
        const float ReleaseStretchX = 0.08f;
        const float ReleaseStretchY = 0.12f;
        const float ReleaseLean = 7f;
        const float ReleaseHz = 5f;
        const float ReleaseZeta = 0.5f;
        const float ReleaseSettle = 0.32f;
        /// <summary>반동: 쏘는 반대쪽으로 6px, 40ms에 나가고 (OutExpo) 140ms에 돌아온다 (OutCubic)</summary>
        const float RecoilPx = 6f;
        const float RecoilOut = 0.04f;
        const float RecoilBack = 0.14f;
        /// <summary>웅크리는 동안 후광 +0.2</summary>
        const float WindupHalo = 0.2f;
        public const float CelebrateInterval = 2.5f;
        const float HopTime = 0.16f;
        const float HopPx = 6f;

        /// <summary>빈 받침대가 숨쉬듯 살짝 어두워지는 바닥 밝기 (아트 받침대, 어두운 색을 곱하지 않는다)</summary>
        static readonly Color PadIdleDim = new(0.86f, 0.84f, 0.92f, 1f);

        public int Slot => _slot;
        public GuardianState State => _state;
        public RectTransform Actor => (RectTransform)transform;
        public Color SlotColor => _slotColor;

        /// <summary>투사체가 나가는 위치 (가슴 높이)</summary>
        public Vector2 MuzzlePosition => Actor.anchoredPosition + Vector2.up * (78f * _mapper.ActorScale);
        /// <summary>머리 위 (말풍선·왕관 높이)</summary>
        public Vector2 HeadPoint => Actor.anchoredPosition + Vector2.up * (150f * _mapper.ActorScale);
        /// <summary>발밑 (받침대 가운데)</summary>
        public Vector2 FeetPosition => Actor.anchoredPosition;
        /// <summary>편성된 형태의 고유 스킬 (없으면 null)</summary>
        public SkillDef Skill => _skill;
        /// <summary>상시 패시브 (녹아드는 빛·벨벳 마무리, 없으면 기본값)</summary>
        public UnitPassive Passive => _passive;
        /// <summary>편성된 형태의 단계 (2차 진화형이면 기본 공격 이펙트 +10%에 금빛 반짝임)</summary>
        public int FormTier { get; private set; }
        /// <summary>신화 계보 (기본 공격 명중에 별가루)</summary>
        public bool IsMythic { get; private set; }

        void Awake()
        {
            _slotColor = UITheme.SlotColor(_slot);
            _phase = _slot * 1.37f;
            if (_badge != null) _badgeDefault = _badge.sprite;
            if (_emptyButton != null) _emptyButton.onClick.AddListener(OnEmptyClicked);
        }

        public void Bind(GuardianState state)
        {
            _state = state;
            _slotColor = UITheme.SlotColor(_slot);
            bool filled = state != null;

            _body.gameObject.SetActive(filled);
            if (_petHit != null) _petHit.raycastTarget = filled;
            _pet = 0f;
            if (_emptyMark != null) _emptyMark.SetActive(!filled);
            if (_emptyButton != null) _emptyButton.interactable = !filled;
            if (_padImage != null)
            {
                _padImage.raycastTarget = !filled;
                var padSprite = filled ? _padFilledSprite : _padEmptySprite;
                if (padSprite != null) _padImage.sprite = padSprite;
            }
            if (_badge != null)
            {
                // 역할 배지: 이 지킴이의 역할 그림 (방어·돌격·포획…), 없으면 기본 지킴이 방패
                var styleIcon = filled && !_badgeTinted && _library != null ? _library.StyleIcon(state.Stats.style) : null;
                _badge.sprite = styleIcon != null ? styleIcon : _badgeDefault != null ? _badgeDefault : _badge.sprite;
                _badge.color = _badgeTinted ? _slotColor : Color.white;
            }
            if (_halo != null) _halo.color = BattleEase.WithAlpha(_slotColor, 0.4f);

            FormTier = filled ? BattleUnitArt.FormTier(state.FormId) : 0;
            IsMythic = filled && BattleUnitArt.IsMythic(state.LineageId, state.FormId);
            if (filled)
            {
                BattleUnitArt.Apply(_portrait, _fallback, _fallbackInitial, state.FormId, state.LineageId);
                float arc = state.Stats.arcDegrees > 1f ? Mathf.Min(state.Stats.arcDegrees, 360f) : 72f;
                _range = InterceptRange(state.Stats, Game.IsReady && Game.Battle != null ? Game.Battle.Config : null);
                _fan.fillAmount = arc / 360f;
                // Radial360 + Top + 시계 방향: 12시에서 시계 방향으로 arc만큼 → 중심이 (90 - arc/2)도
                float slotDegrees = BattleGeometry.SlotAngle(_slot) * Mathf.Rad2Deg;
                _fan.rectTransform.localRotation = Quaternion.Euler(0f, 0f, slotDegrees - (90f - arc * 0.5f));
            }
            _fan.enabled = filled;
            _attackPunch = 0f;
            _shotAge = Idle;
            _windup = 0f;
            _hopAge = Idle;
            _celebrateDelay = -1f;
            _celebrateCooldown = 0f;
            _cast = 0f;
            _status = 0f;
            _statusKind = 0;
            if (_statusIcon != null) _statusIcon.enabled = false;
            BindSkill(state);
            _layoutVersion = -1;
        }

        /// <summary>고유 스킬 보석: 스킬 그림(아트 또는 파스텔 대체 모양)과 쿨타임 고리</summary>
        void BindSkill(GuardianState state)
        {
            _skill = state != null ? BattleUnitArt.FormSkill(state.FormId) : null;
            _passive = BattleUnitArt.PassiveOf(_skill);
            _melt = 0f;
            _meltFull = false;
            _dripTimer = 0.6f;
            _hasSkill = _skill != null && _skillGem != null;
            if (_skillGem != null) _skillGem.gameObject.SetActive(_hasSkill);
            _skillReady = 0f;
            if (!_hasSkill) return;
            if (_skillIcon != null)
            {
                Sprite icon = null;
                bool isArt = false;
                if (_library != null) _library.TryGetSkillIcon(_skill.vfx, out icon, out isArt);
                _skillIcon.sprite = icon;
                _skillIcon.enabled = icon != null;
                _skillIcon.color = isArt ? Color.white : SkillVfxLook.Get(_skill.vfx).Main;
            }
        }

        public void Tick(float dt, float danger)
        {
            if (_mapper.Version != _layoutVersion) Layout();

            float time = Time.time;
            _danger = BattleEase.Damp(_danger, Mathf.Clamp01(danger), 6f, dt);
            _highlight = BattleEase.Damp(_highlight, _highlightOn ? 1f : 0f, 12f, dt);
            _attackPunch = Mathf.Max(0f, _attackPunch - dt * 4f);

            if (_aimHighlight != null)
            {
                _aimHighlight.enabled = _highlight > 0.01f;
                float pulse = 0.75f + 0.25f * Mathf.Sin(time * 8f);
                _aimHighlight.color = BattleEase.WithAlpha(_slotColor, _highlight * pulse);
                float scale = 1f + 0.08f * Mathf.Sin(time * 8f);
                _aimHighlight.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }

            if (_state == null)
            {
                float idle = 0.5f + 0.5f * Mathf.Sin(time * 2f + _phase);
                if (_padImage != null)
                {
                    // 아트 빈칸 받침대는 불투명하게, 아주 살짝만 숨쉰다 (반투명 + 어두운 색 곱하기는 탁해진다)
                    _padImage.color = _padColored ? Color.Lerp(PadIdleDim, Color.white, idle)
                        : BattleEase.WithAlpha(Color.Lerp(_slotColor, UITheme.Background, 0.35f), 0.38f + 0.12f * idle);
                }
                if (_padGlow != null) _padGlow.color = BattleEase.WithAlpha(_slotColor, 0.12f + 0.1f * idle);
                return;
            }

            float attack = Mathf.Clamp01(Mathf.Max(_state.AttackAnim, _attackPunch));
            _cast = Mathf.Max(0f, _cast - dt * 1.8f);
            float castHop = BattleEase.Punch(1f - _cast);
            float bob = Mathf.Sin(time * 2.4f + _phase);
            TickCelebrate(dt);
            // 기본 공격 몸짓 (E1): 웅크림 → 놓음 → 반동 → 용수철 (스킬 시전은 위로 쭉 늘어난다)
            ShotPose(dt, out float poseX, out float poseY, out float lean, out var recoil, out float windupGlow);
            float hop = _hopAge < HopTime ? Mathf.Sin(_hopAge / HopTime * Mathf.PI) * HopPx : 0f;
            _body.anchoredPosition = new Vector2(recoil.x, 2f + bob * 3f + castHop * 26f + recoil.y + hop);
            // 녹아드는 빛: 녹을수록 키가 조금 줄고 옆으로 퍼진다
            float melt = 1f - 0.1f * _melt;
            // 쓰다듬기: 눌렸다 펴짐 (sin — 0.2초에 1 → 1.08/0.9 → 1)
            _pet = Mathf.Max(0f, _pet - dt / PetSquashTime);
            float squash = _pet > 0f ? Mathf.Sin((1f - _pet) * Mathf.PI) : 0f;
            _body.localScale = new Vector3((poseX + 0.015f * bob - 0.08f * castHop) * (2f - melt) * (1f + 0.08f * squash),
                (poseY - 0.015f * bob + 0.14f * castHop) * melt * (1f - 0.1f * squash), 1f);
            // lean > 0 = 대상 쪽 (오른쪽 대상이면 시계 방향)
            _body.localRotation = Quaternion.Euler(0f, 0f, -_leanDirection * lean);

            float dangerPulse = _danger > 0.35f ? 0.5f + 0.5f * Mathf.Sin(time * 7f) : 0f;
            _fan.color = BattleEase.WithAlpha(_slotColor, _fanBaseAlpha + _fanDangerAlpha * _danger + 0.08f * dangerPulse * _danger + 0.12f * _highlight);

            if (_halo != null)
            {
                float glow = 0.32f + 0.08f * Mathf.Sin(time * 1.8f + _phase) + 0.3f * attack + 0.2f * _danger + 0.5f * _cast + windupGlow;
                var haloColor = _state.BuffTimer > 0f ? Color.Lerp(_slotColor, UITheme.Gold, 0.45f) : _slotColor;
                if (_melt > 0f) haloColor = Color.Lerp(haloColor, CandleGlow, _melt * 0.7f);
                _halo.color = BattleEase.WithAlpha(Color.Lerp(haloColor, UITheme.Cream, _cast * 0.5f), Mathf.Clamp01(glow + 0.25f * _melt));
                float scale = 1f + 0.25f * castHop + 0.22f * _melt;
                _halo.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
            TickSkill(dt, time);
            TickStatus(dt, time);
            if (_padImage != null)
            {
                // 방위 색 받침대 그림은 흰색 그대로, 공격 순간 크림빛으로만 번쩍인다
                _padImage.color = _padColored ? Color.Lerp(Color.white, UITheme.Cream, 0.6f * attack)
                    : Color.Lerp(_slotColor, Color.white, 0.25f + 0.25f * attack);
            }
            if (_padGlow != null) _padGlow.color = BattleEase.WithAlpha(_slotColor, 0.45f + 0.45f * attack + 0.2f * _danger);
        }

        static readonly Color CandleGlow = new(1f, 0.72f, 0.42f, 1f);

        /// <summary>
        /// 상시 패시브 연출 (BattleView가 Tick 뒤에 부른다). 녹아드는 빛: 전투 시간만큼 오른 공격력 비율로 몸이 녹고 촛농이 떨어지며,
        /// 상한에 닿으면 발밑에 촛농 웅덩이가 퍼진다.
        /// </summary>
        public void TickPassive(float battleSeconds, float dt, BattleFx fx)
        {
            if (_state == null || !_passive.HasRamp)
            {
                _melt = 0f;
                return;
            }
            float target = _passive.RampFraction(battleSeconds);
            _melt = BattleEase.Damp(_melt, target, 3f, dt);
            if (fx == null) return;
            _dripTimer -= dt;
            if (_dripTimer <= 0f && target > 0.05f)
            {
                _dripTimer = Mathf.Lerp(1.2f, 0.4f, target);
                fx.WaxDrip(MuzzlePosition, FeetPosition + Vector2.up * 6f, HeadPoint, target, null);
            }
            if (target >= 0.999f && !_meltFull)
            {
                _meltFull = true;
                Sprite art = null;
                bool isArt = false;
                if (_library != null) _library.TryGetVfx(_passive.Vfx, out art, out isArt);
                fx.MeltBloom(FeetPosition, MuzzlePosition, art, isArt, SkillVfxLook.Get(_passive.Vfx), null);
            }
        }

        /// <summary>공격 연출 (웅크림 없이 바로 놓는다)</summary>
        public void PlayAttack(Vector2 targetFieldPosition) => PlayAttack(targetFieldPosition, 0f);

        /// <summary>
        /// 기본 공격 몸짓 (FX-JUICE E1): windup(W, BattleFx.LastWindup) 동안 대상 반대쪽으로 4° 기울며 (1.10, 0.88)로 웅크리고 후광 +0.2 →
        /// 놓는 순간(총구 불꽃·투사체와 같은 순간) (0.92, 1.12)로 늘어나며 대상 쪽 7° → 쏘는 반대쪽으로 6px 반동(40ms 나감 · 140ms 돌아옴) →
        /// 감쇠 용수철(5Hz, ζ 0.5)로 220ms쯤 제자리. 받침대 번쩍임은 그대로 (+0.45 × 공격)
        /// </summary>
        public void PlayAttack(Vector2 targetFieldPosition, float windup)
        {
            _attackPunch = 1f;
            float dx = targetFieldPosition.x - Actor.anchoredPosition.x;
            _leanDirection = Mathf.Abs(dx) < 4f ? 0f : Mathf.Sign(dx);
            var delta = targetFieldPosition - MuzzlePosition;
            _shotDir = delta.sqrMagnitude > 1f ? delta.normalized : Vector2.up;
            _windup = Mathf.Clamp(windup, 0f, 0.1f);
            _shotAge = 0f;
        }

        /// <summary>
        /// 축하 (FX-JUICE E8d): 기본 공격이 처치했거나 치명타를 냈을 때 delay(그 순간이 보일 때까지) 뒤 6px 통 뛴다 (160ms).
        /// 지킴이마다 2.5초에 한 번 — 이번에 뛰면 true (부르는 쪽이 머리 위 반짝임 BattleFx.CelebrationGlint를 붙인다)
        /// </summary>
        public bool Celebrate(float delay = 0f)
        {
            if (_state == null || _celebrateCooldown > 0f) return false;
            _celebrateCooldown = CelebrateInterval;
            _celebrateDelay = Mathf.Max(0f, delay);
            return true;
        }

        void TickCelebrate(float dt)
        {
            _celebrateCooldown = Mathf.Max(0f, _celebrateCooldown - dt);
            if (_hopAge < Idle) _hopAge += dt;
            if (_celebrateDelay < 0f) return;
            _celebrateDelay -= dt;
            if (_celebrateDelay > 0f) return;
            _celebrateDelay = -1f;
            _hopAge = 0f;
        }

        /// <summary>
        /// 지금 공격 몸짓: 가로/세로 배율, 대상 쪽 기울기(도, 음수 = 반대쪽), 반동(몸 로컬 px — 배율이 곱해져 × U), 웅크림 후광 더하기
        /// </summary>
        void ShotPose(float dt, out float scaleX, out float scaleY, out float lean, out Vector2 recoil, out float windupGlow)
        {
            scaleX = 1f;
            scaleY = 1f;
            lean = 0f;
            recoil = Vector2.zero;
            windupGlow = 0f;
            if (_shotAge >= Idle) return;
            _shotAge += dt;
            if (_shotAge < _windup)
            {
                float u = JuiceEase.OutQuad(_shotAge / Mathf.Max(0.001f, _windup));
                scaleX = 1f + WindupSquashX * u;
                scaleY = 1f - WindupSquashY * u;
                lean = -WindupLean * u;
                windupGlow = WindupHalo * u;
                return;
            }
            float t = _shotAge - _windup;
            if (t >= ReleaseSettle)
            {
                _shotAge = Idle;
                return;
            }
            float f = JuiceEase.SpringDisplacement(t, ReleaseHz, ReleaseZeta);
            scaleX = 1f - ReleaseStretchX * f;
            scaleY = 1f + ReleaseStretchY * f;
            lean = ReleaseLean * f;
            float kick = t < RecoilOut ? JuiceEase.OutExpo(t / RecoilOut) : 1f - JuiceEase.OutCubic((t - RecoilOut) / RecoilBack);
            recoil = -_shotDir * (RecoilPx * Mathf.Clamp01(kick));
        }

        public void SetAimHighlight(bool on) => _highlightOn = on;

        /// <summary>스킬 시전 (위로 통 늘어나며 후광이 번지고 스킬 보석이 톡 튄다)</summary>
        public void PlayCast() => _cast = 1f;

        /// <summary>쓰다듬기 (CR-10): 0.2초 동안 옆으로 1.08 · 위아래 0.9로 눌렸다 펴진다</summary>
        public void PlayPet() => _pet = 1f;

        /// <summary>쓰다듬기 눌림 남은 비율 (1 → 0, 0.2초)</summary>
        float _pet;
        public const float PetSquashTime = 0.2f;

        /// <summary>지원·힐 맥박 (시전보다 작게: 살짝 통 뛰며 후광이 번진다)</summary>
        public void PlayPulse() => _cast = Mathf.Max(_cast, 0.45f);

        void TickSkill(float dt, float time)
        {
            if (!_hasSkill) return;
            float max = _state.SkillCooldownMax;
            float cooldown = _state.SkillCooldown;
            bool timed = max > 0.01f;
            float fill = timed ? 1f - Mathf.Clamp01(cooldown / max) : 1f;
            bool ready = timed && cooldown <= 0.01f;
            _skillReady = BattleEase.Damp(_skillReady, ready ? 1f : 0f, 10f, dt);
            float pulse = 0.5f + 0.5f * Mathf.Sin(time * 5f + _phase);

            if (_skillRing != null)
            {
                _skillRing.fillAmount = fill;
                var color = timed ? Color.Lerp(UITheme.Lavender, UITheme.Gold, _skillReady) : BattleEase.WithAlpha(UITheme.Lavender, 0.75f);
                _skillRing.color = Color.Lerp(color, UITheme.Cream, _skillReady * pulse * 0.4f);
            }
            if (_skillGlow != null)
            {
                float alpha = _skillReady * (0.35f + 0.35f * pulse) + _cast * 0.9f;
                _skillGlow.enabled = alpha > 0.01f;
                if (_skillGlow.enabled) _skillGlow.color = BattleEase.WithAlpha(Color.Lerp(UITheme.Gold, UITheme.Cream, _cast), Mathf.Clamp01(alpha));
            }
            if (_skillIcon != null && _skillIcon.enabled)
            {
                var tint = _skillIcon.color;
                tint.a = timed && !ready ? 0.55f + 0.45f * fill : 1f;
                _skillIcon.color = tint;
            }
            float scale = 1f + 0.08f * _skillReady * pulse + 0.4f * BattleEase.Punch(1f - _cast);
            _skillGem.localScale = new Vector3(scale, scale, 1f);
        }

        void TickStatus(float dt, float time)
        {
            if (_statusIcon == null) return;
            int kind = _state.BuffTimer > 0f ? 1 : _state.DebuffTimer > 0f ? 2 : 0;
            if (kind != 0 && kind != _statusKind)
            {
                _statusKind = kind;
                _statusIcon.sprite = kind == 1 ? _buffSprite : _debuffSprite;
            }
            _status = BattleEase.Damp(_status, kind != 0 ? 1f : 0f, 10f, dt);
            bool show = _status > 0.02f && _statusIcon.sprite != null;
            _statusIcon.enabled = show;
            if (!show) return;
            _statusIcon.color = BattleEase.WithAlpha(_statusKind == 1 ? _buffTint : _debuffTint, _status);
            float pop = BattleEase.OutBack(_status, 2.4f);
            _statusIcon.rectTransform.localScale = new Vector3(pop, pop, 1f);
            var rt = _statusIcon.rectTransform;
            rt.anchoredPosition = new Vector2(rt.anchoredPosition.x, 168f + Mathf.Sin(time * 3f + _phase) * 4f);
        }

        /// <summary>
        /// 실제 요격 반경 (BattleSimulator.ApplyGuardianStats와 같은 계산): 돌격은 짧게, 저격은 길게 배율을 곱하고
        /// 수선점 근접 반경 + 0.1보다 작아지지 않는다. 부채꼴 미리보기가 실제로 공격하는 범위와 같아야 편성 판단이 맞는다.
        /// </summary>
        public static float InterceptRange(CharacterStats stats, BattleConfig config)
        {
            float range = float.IsNaN(stats.range) || float.IsInfinity(stats.range) || stats.range <= 0f ? 1f : Mathf.Max(0.1f, stats.range);
            if (config == null) return range;
            switch (stats.style)
            {
                case CombatStyle.Assault:
                    range *= config.assaultRangeScale;
                    break;
                case CombatStyle.Snipe:
                    range *= config.snipeRangeScale;
                    break;
            }
            return Mathf.Max(range, config.shopGuardRadius + 0.1f);
        }

        void Layout()
        {
            _layoutVersion = _mapper.Version;
            var position = _mapper.WorldToField(BattleGeometry.GuardianPosition(_slot));
            float scale = _mapper.ActorScale;
            Actor.anchoredPosition = position;
            Actor.localScale = new Vector3(scale, scale, 1f);
            if (_pad != null)
            {
                _pad.anchoredPosition = position;
                _pad.localScale = new Vector3(scale, scale, 1f);
            }
            // 바닥에 누운 고리는 전장 눌림만큼 납작하게
            FlattenOnGround(_padGlow);
            FlattenOnGround(_aimHighlight);
            float diameter = _range * 2f * _mapper.Ppu;
            _fan.rectTransform.anchoredPosition = Vector2.zero;
            _fan.rectTransform.sizeDelta = new Vector2(diameter, diameter);
        }

        void FlattenOnGround(Image ring)
        {
            if (ring == null) return;
            var rt = ring.rectTransform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, rt.sizeDelta.x * _mapper.Squash);
        }

        void OnEmptyClicked()
        {
            if (_state != null) return;
            if (UIManager.Instance != null) UIManager.Instance.ShowToast("편성 탭에서 지킴이를 배치해 주세요.");
            UINav.GoToTab(MainTab.Formation);
        }
    }
}
