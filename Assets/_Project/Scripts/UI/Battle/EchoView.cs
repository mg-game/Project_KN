using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace IdleGame.UI
{
    /// <summary>
    /// 잔향이 하나 (풀링): 지킴이보다 작고 반투명, 발광 오라, 둥실둥실 떠다님, 발밑에 남은 시간 고리.
    /// 수선점에서 빛이 모여 형태가 잡히며 나타나고, 사라질 때는 몸이 빛으로 풀린다 (빛 줄기는 BattleFx가 그림).
    /// 스킬로 불려 나온 작은 양말 토큰(IsToken)은 더 작고 흐리게, 보호막은 몸을 감싼 거품, 강화/약화는 머리 위 왕관/표식으로 보인다.
    /// 몸 옆 작은 스킬 배지는 고유 스킬 그림 + 쿨타임 고리(준비되면 금빛, 상시 패시브는 늘 찬 고리)를 보여준다.
    /// 상시 패시브 "녹아드는 빛"은 살아 있는 동안 몸이 녹아 작아지고 오라가 촛불빛으로 커지며 촛농이 떨어진다.
    /// 적에게 맞으면 PlayHurt: 반투명한 몸이라 흰 번쩍임은 약하게(50%, UI/WhiteFlash 덧그림) + 살짝 움찔 (피해 숫자는 띄우지 않는다).
    /// </summary>
    public class EchoView : MonoBehaviour
    {
        [SerializeField] RectTransform _body;
        [SerializeField] RectTransform _flip;
        [SerializeField] CanvasGroup _bodyGroup;
        [SerializeField] Image _portrait;
        [Tooltip("맞았을 때 흰 번쩍임 덧그림 (초상화와 같은 그림, UI/WhiteFlash 머티리얼)")]
        [SerializeField] Image _flash;
        [SerializeField] Image _fallback;
        [SerializeField] TMP_Text _fallbackInitial;
        [SerializeField] Image _aura;
        [Tooltip("발밑 시간 고리 (바닥에 눕도록 전장 눌림만큼 세로로 줄인다)")]
        [SerializeField] RectTransform _lifeRing;
        [SerializeField] Image _lifeBack;
        [SerializeField] Image _lifeFill;
        [SerializeField] RectTransform _hpRoot;
        [SerializeField] Image _hpFill;

        [Header("스킬 / 상태")]
        [Tooltip("몸을 감싸는 보호막 거품")]
        [SerializeField] Image _shieldBubble;
        [Tooltip("머리 위 강화(왕관)/약화(표식) 아이콘")]
        [SerializeField] Image _statusIcon;
        [SerializeField] Sprite _buffSprite;
        [SerializeField] Sprite _debuffSprite;
        [SerializeField] Color _buffTint = Color.white;
        [SerializeField] Color _debuffTint = Color.white;
        [Tooltip("스킬 토큰(작은 양말 등)의 크기 배율")]
        [SerializeField] float _tokenScale = 0.68f;

        [Header("고유 스킬 배지")]
        [SerializeField] RectTransform _skillBadge;
        [SerializeField] Image _skillIcon;
        [Tooltip("쿨타임 고리 (Radial360)")]
        [SerializeField] Image _skillRing;
        [SerializeField] SkillVfxLibrary _library;

        [SerializeField] float _alpha = 0.75f;
        [SerializeField] float _height = 80f;
        [SerializeField] float _hover = 14f;
        [SerializeField] float _moteInterval = 0.22f;

        float _age;
        float _dissolveAge = -1f;
        float _phase;
        float _moteTimer;
        float _facing = 1f;
        float _attack;
        float _cast;
        float _heal;
        float _shield;
        float _status;
        int _statusKind;
        float _unitScale = 1f;
        Color _glow;
        bool _hasBadge;
        float _skillReady;
        SkillDef _skill;
        UnitPassive _passive;
        float _melt;
        bool _meltFull;
        float _dripTimer;
        float _hurt;
        float _hurtStrength;
        bool _flashArt;
        // 몸 용수철 (FX-JUICE E8): 공격 = (1.10, 0.90) → (0.95, 1.05) 출렁 / 맞음 = (0.90, 1.10) → 제자리
        float _squashAge = SpringIdle;
        float _squashX;
        float _squashY;
        float _hurtSquashAge = SpringIdle;
        // 전체 정지·느린 화면 동안 자리 붙잡기 (FX-01, EnemyView와 같은 규칙): 연출 시간만 멈추고 시뮬레이터는 계속 걷는다 —
        // 보이는 자리 = 시뮬레이터 자리 + _holdOffset (EnemyFxCatalog.HoldOffset)
        Vector2 _holdOffset;
        /// <summary>지난 갱신 때의 시뮬레이터 자리 (전장 px — 이번 프레임에 시뮬레이터가 움직인 양을 잰다)</summary>
        Vector2 _simField;
        bool _simFieldValid;

        /// <summary>맞았을 때 흰 번쩍임 길이 (초)</summary>
        const float HurtTime = 0.08f;
        const float SpringIdle = 99f;
        /// <summary>근접 공격 눌림 (가로 +0.10 / 세로 −0.11, 5Hz · ζ 0.25 — 한 번 크게 되튄다), 원거리는 0.6배</summary>
        const float AttackSquashX = 0.10f;
        const float AttackSquashY = 0.11f;
        const float AttackSpringHz = 5f;
        const float AttackSpringZeta = 0.25f;
        const float AttackSpringTime = 0.5f;
        /// <summary>맞음 눌림 (가로 −0.10 / 세로 +0.10, 6Hz · ζ 0.45)</summary>
        const float HurtSquash = 0.10f;
        const float HurtSpringHz = 6f;
        const float HurtSpringZeta = 0.45f;
        const float HurtSpringTime = 0.4f;

        public int Id { get; private set; } = -1;
        public EchoState State { get; private set; }
        public RectTransform Rect => (RectTransform)transform;
        public Color GlowColor => _glow;
        /// <summary>몸 가운데 (전장 로컬 좌표)</summary>
        public Vector2 BodyPoint => Rect.anchoredPosition + Vector2.up * ((_hover + _height * 0.45f) * Rect.localScale.y);
        /// <summary>머리 위 (말풍선·표식 높이, 전장 로컬 좌표)</summary>
        public Vector2 HeadPoint => Rect.anchoredPosition + Vector2.up * ((_hover + _height + 16f) * Rect.localScale.y);
        public bool IsToken => State != null && State.IsToken;
        /// <summary>상시 패시브 (토큰은 없음)</summary>
        public UnitPassive Passive => _passive;
        /// <summary>형태 단계 (2차 진화형이면 기본 공격 이펙트 +10%에 금빛 반짝임)</summary>
        public int FormTier { get; private set; }
        /// <summary>신화 계보 (기본 공격 명중에 별가루)</summary>
        public bool IsMythic { get; private set; }
        internal int SeenStamp;

        public void Bind(EchoState state, BattleFieldMapper mapper)
        {
            State = state;
            Id = state.Id;
            _age = 0f;
            _dissolveAge = -1f;
            _phase = Random.Range(0f, Mathf.PI * 2f);
            _moteTimer = Random.Range(0f, _moteInterval);
            _attack = 0f;
            _squashAge = SpringIdle;
            _hurtSquashAge = SpringIdle;
            _cast = 0f;
            _heal = 0f;
            _shield = state.Shield > 0f ? 1f : 0f;
            _status = 0f;
            _statusKind = 0;
            _facing = 1f;
            _unitScale = state.IsToken ? _tokenScale : 1f;
            _glow = Color.Lerp(UITheme.EchoGlow, BattleUnitArt.MainColor(state.LineageId, state.FormId), state.IsToken ? 0.45f : 0.25f);
            if (_shieldBubble != null) _shieldBubble.enabled = false;
            if (_statusIcon != null) _statusIcon.enabled = false;

            BattleUnitArt.Apply(_portrait, _fallback, _fallbackInitial, state.FormId, state.LineageId);
            FormTier = state.IsToken ? 0 : BattleUnitArt.FormTier(state.FormId);
            IsMythic = !state.IsToken && BattleUnitArt.IsMythic(state.LineageId, state.FormId);
            BindFlash();
            BindSkill(state);
            if (_aura != null) _aura.color = BattleEase.WithAlpha(UITheme.EchoGlow, 0f);
            if (_lifeFill != null) _lifeFill.color = UITheme.EchoGlow;
            if (_hpRoot != null) _hpRoot.gameObject.SetActive(false);
            _bodyGroup.alpha = 0f;
            _body.localScale = Vector3.zero;
            _simFieldValid = false;
            _holdOffset = Vector2.zero;
            Place(state.Position, mapper, 1f, 0f);
            gameObject.SetActive(true);
        }

        /// <summary>
        /// 살아 있는 동안 매 프레임 갱신. dt = 연출 시계 (BattleView가 BattleFx.TimeScale을 곱해 넘긴다). 보스 처치 · 불 밝히기 전체 정지와
        /// 느린 화면 동안은 시뮬레이터가 그새 걸은 만큼 몸 자리를 붙잡아 출렁임 · 입자와 함께 멈추고(미끄러지지 않게), 다시 흐르면 0.17초 남짓에
        /// 따라잡는다 (EnemyView와 같은 EnemyFxCatalog.HoldOffset, 상한 96px). fx가 없으면 dt가 0일 때만 멈춘 것으로 본다
        /// </summary>
        /// <param name="alpha">고정 진행 사이 보간 비율 (BattleSimulator.RenderAlpha). 1 = 시뮬레이터 자리 그대로</param>
        /// <param name="holdScale">몸 자리를 붙잡을 배율 (EnemyFxCatalog.HoldScale). 음수 = fx.TimeScale (예전 그대로)</param>
        public void Tick(BattleFieldMapper mapper, float dt, BattleFx fx, float alpha = 1f, float holdScale = -1f)
        {
            if (State == null) return;
            _age += dt;
            // 보간한 자리 (perf PF-2 F1) · 시뮬레이션이 덜 느려진 몫만 붙잡기 (F2)
            var world = alpha >= 1f ? State.Position : Vector2.LerpUnclamped(State.PrevPosition, State.Position, alpha);
            Place(world, mapper, holdScale >= 0f ? holdScale : fx != null ? fx.TimeScale : dt > 0f ? 1f : 0f, dt);

            // 수선점에서 형태가 잡히는 등장 (0.45초)
            float form = Mathf.Clamp01(_age / 0.45f);
            float appear = BattleEase.OutBack(form, 2f);
            float bob = Mathf.Sin(_age * 3.1f + _phase);
            _attack = Mathf.Max(Mathf.Clamp01(State.AttackAnim), _attack - dt * 4f);
            _cast = Mathf.Max(0f, _cast - dt * 2.2f);
            _hurt = Mathf.Max(0f, _hurt - dt);
            _heal = Mathf.Max(0f, _heal - dt * 1.8f);
            float lunge = BattleEase.Punch(1f - _attack) * 14f;
            var heading = new Vector2(Mathf.Cos(State.Heading), Mathf.Sin(State.Heading) * mapper.Squash);

            // 스킬 시전: 통 뛰어오르며 늘어났다가 내려앉는다
            float castHop = BattleEase.Punch(1f - _cast);
            float flinch = _hurt > 0f ? Mathf.Sin(_hurt * 120f) * 2.5f : 0f;
            _body.anchoredPosition = new Vector2(heading.x * lunge + flinch, _hover + bob * 6f + heading.y * lunge + castHop * 22f);
            float breathe = 1f + 0.03f * Mathf.Sin(_age * 2.3f + _phase);
            // 녹아드는 빛: 녹을수록 키가 줄고 옆으로 퍼진다
            float melt = 1f - 0.14f * _melt;
            // 말랑한 몸 용수철: 공격하면 납작해졌다가 길쭉하게 한 번 출렁, 맞으면 길쭉해졌다가 돌아온다
            SpringSquash(dt, out float springX, out float springY);
            _body.localScale = new Vector3(appear * breathe * (1f - 0.08f * castHop) * (2f - melt) * springX,
                appear * (2f - breathe) * (1f + 0.14f * castHop) * melt * springY, 1f);
            _body.localRotation = Quaternion.Euler(0f, 0f, Mathf.Sin(_age * 1.7f + _phase) * 4f);
            _bodyGroup.alpha = _alpha * Mathf.Clamp01(form * 1.6f);

            float cos = Mathf.Cos(State.Heading);
            if (cos > 0.25f) _facing = 1f;
            else if (cos < -0.25f) _facing = -1f;
            if (_flip != null) _flip.localScale = new Vector3(BattleEase.Damp(_flip.localScale.x, _facing, 12f, dt), 1f, 1f);

            if (_aura != null)
            {
                float glow = 0.45f + 0.15f * Mathf.Sin(_age * 4f + _phase) + 0.3f * _attack + 0.45f * _cast + 0.3f * _heal;
                var auraColor = Color.Lerp(_glow, UITheme.Positive, _heal * 0.7f);
                if (State.BuffTimer > 0f) auraColor = Color.Lerp(auraColor, UITheme.Gold, 0.35f);
                if (_melt > 0f) auraColor = Color.Lerp(auraColor, CandleGlow, _melt * 0.75f);
                _aura.color = BattleEase.WithAlpha(auraColor, Mathf.Clamp01(glow + 0.25f * _melt) * Mathf.Clamp01(form * 2f));
                float scale = 0.95f + 0.08f * bob + 0.3f * BattleEase.Punch(1f - _cast) + 0.3f * _melt;
                _aura.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
            TickStatus(dt, form);
            TickSkill(dt, form);
            TickPassive(dt, form, fx);
            DrawHurt();

            float life = State.MaxLife > 0f ? Mathf.Clamp01(State.Life / State.MaxLife) : 1f;
            if (_lifeFill != null)
            {
                _lifeFill.fillAmount = life;
                float blink = life < 0.25f ? 0.55f + 0.45f * Mathf.Sin(_age * 12f) : 1f;
                _lifeFill.color = BattleEase.WithAlpha(UITheme.EchoGlow, 0.85f * blink * form);
            }
            if (_lifeBack != null) _lifeBack.color = BattleEase.WithAlpha(UITheme.Background, 0.35f * form);

            if (_hpRoot != null && State.MaxHp > 0f)
            {
                float hp = Mathf.Clamp01(State.Hp / State.MaxHp);
                bool show = hp < 0.999f;
                if (_hpRoot.gameObject.activeSelf != show) _hpRoot.gameObject.SetActive(show);
                if (show) BattleEase.SetBar(_hpFill, hp);
            }

            _moteTimer -= dt;
            if (_moteTimer <= 0f && fx != null && form >= 1f)
            {
                _moteTimer = _moteInterval;
                fx.Mote(Rect.anchoredPosition + Vector2.up * _hover, _glow);
            }
        }

        static readonly Color CandleGlow = new(1f, 0.72f, 0.42f, 1f);

        void BindSkill(EchoState state)
        {
            _skill = state.IsToken ? null : BattleUnitArt.FormSkill(state.FormId);
            _passive = BattleUnitArt.PassiveOf(_skill);
            _melt = 0f;
            _meltFull = false;
            _dripTimer = 0.8f;
            _skillReady = 0f;
            _hasBadge = _skill != null && _skillBadge != null;
            if (_skillBadge != null) _skillBadge.gameObject.SetActive(_hasBadge);
            if (!_hasBadge) return;
            Sprite icon = null;
            bool isArt = false;
            if (_library != null) _library.TryGetSkillIcon(_skill.vfx, out icon, out isArt);
            if (_skillIcon != null)
            {
                _skillIcon.sprite = icon;
                _skillIcon.enabled = icon != null;
                _skillIcon.color = isArt ? Color.white : SkillVfxLook.Get(_skill.vfx).Main;
            }
            _skillBadge.localScale = Vector3.zero;
        }

        /// <summary>스킬 배지: 쿨타임 고리가 차오르고, 준비되면 금빛으로 맥동. 상시 패시브는 늘 찬 고리 (녹아드는 빛은 촛불빛으로 달아오름)</summary>
        void TickSkill(float dt, float form)
        {
            if (!_hasBadge) return;
            float max = State.SkillCooldownMax;
            bool timed = max > 0.01f;
            float fill = timed ? 1f - Mathf.Clamp01(State.SkillCooldown / max) : 1f;
            bool ready = timed && State.SkillCooldown <= 0.01f;
            _skillReady = BattleEase.Damp(_skillReady, ready ? 1f : 0f, 10f, dt);
            float pulse = 0.5f + 0.5f * Mathf.Sin(_age * 5f + _phase);
            if (_skillRing != null)
            {
                _skillRing.fillAmount = fill;
                var color = timed ? Color.Lerp(UITheme.Lavender, UITheme.Gold, _skillReady) : Color.Lerp(UITheme.Lavender, CandleGlow, _melt);
                _skillRing.color = BattleEase.WithAlpha(Color.Lerp(color, UITheme.Cream, _skillReady * pulse * 0.4f), 0.95f * form);
            }
            if (_skillIcon != null && _skillIcon.enabled)
            {
                var tint = _skillIcon.color;
                tint.a = (timed && !ready ? 0.55f + 0.45f * fill : 1f) * form;
                _skillIcon.color = tint;
            }
            float scale = form * (0.9f + 0.08f * _skillReady * pulse + 0.45f * BattleEase.Punch(1f - _cast));
            _skillBadge.localScale = new Vector3(scale, scale, 1f);
        }

        /// <summary>녹아드는 빛: 살아 있던 시간만큼 오른 공격력 비율로 몸이 녹고 촛농이 떨어지며, 상한에 닿으면 발밑에 웅덩이가 퍼진다</summary>
        void TickPassive(float dt, float form, BattleFx fx)
        {
            if (!_passive.HasRamp) return;
            float target = _passive.RampFraction(Mathf.Max(0f, State.MaxLife - State.Life));
            _melt = BattleEase.Damp(_melt, target, 3f, dt);
            if (fx == null || form < 1f) return;
            _dripTimer -= dt;
            if (_dripTimer <= 0f && target > 0.05f)
            {
                _dripTimer = Mathf.Lerp(1.1f, 0.35f, target);
                fx.WaxDrip(BodyPoint, Rect.anchoredPosition + Vector2.up * 4f, HeadPoint, target, this);
            }
            if (target >= 0.999f && !_meltFull)
            {
                _meltFull = true;
                Sprite art = null;
                bool isArt = false;
                if (_library != null) _library.TryGetVfx(_passive.Vfx, out art, out isArt);
                fx.MeltBloom(Rect.anchoredPosition, BodyPoint, art, isArt, SkillVfxLook.Get(_passive.Vfx), this);
            }
        }

        /// <summary>공격 순간 대상 쪽으로 14px 튀어 나가며 (1.10, 0.90)로 눌렸다가 (0.95, 1.05)로 한 번 출렁인다 (원거리는 0.6배)</summary>
        public void PlayAttack() => PlayAttack(true);

        /// <summary>공격 (melee = 근접 베기: 눌림 그대로, 원거리 쏘기: 0.6배)</summary>
        public void PlayAttack(bool melee)
        {
            _attack = 1f;
            float scale = melee ? 1f : 0.6f;
            _squashX = AttackSquashX * scale;
            _squashY = AttackSquashY * scale;
            _squashAge = 0f;
        }

        /// <summary>공격·맞음 용수철의 지금 가로/세로 배율 (e^(−ζωt)·cos 감쇠, 할당 없음)</summary>
        void SpringSquash(float dt, out float x, out float y)
        {
            x = 1f;
            y = 1f;
            if (_squashAge < SpringIdle)
            {
                _squashAge += dt;
                if (_squashAge >= AttackSpringTime) _squashAge = SpringIdle;
                else
                {
                    float f = JuiceEase.SpringDisplacement(_squashAge, AttackSpringHz, AttackSpringZeta);
                    x += _squashX * f;
                    y -= _squashY * f;
                }
            }
            if (_hurtSquashAge < SpringIdle)
            {
                _hurtSquashAge += dt;
                if (_hurtSquashAge >= HurtSpringTime) _hurtSquashAge = SpringIdle;
                else
                {
                    float f = JuiceEase.SpringDisplacement(_hurtSquashAge, HurtSpringHz, HurtSpringZeta) * _hurtStrength * 2f;
                    x -= HurtSquash * f;
                    y += HurtSquash * f;
                }
            }
        }

        /// <summary>스킬 시전 (통 뛰어오르며 빛이 번진다)</summary>
        public void PlayCast() => _cast = 1f;

        /// <summary>회복을 받음 (오라가 잠깐 연두빛)</summary>
        public void PlayHeal() => _heal = 1f;

        /// <summary>적에게 맞음 (적 공격 연출이 부른다): 흰 번쩍임 strength(기본 50%) 0.08초 + ±2.5px 움찔 + (0.90, 1.10)로 늘었다가 용수철로 제자리</summary>
        public void PlayHurt(float strength = 0.5f)
        {
            _hurt = HurtTime;
            _hurtStrength = Mathf.Clamp01(strength);
            _hurtSquashAge = 0f;
        }

        void BindFlash()
        {
            _hurt = 0f;
            if (_flash == null)
            {
                _flashArt = false;
                return;
            }
            var material = _flash.material;
            _flashArt = _portrait != null && _portrait.sprite != null && material != null && material != Graphic.defaultGraphicMaterial;
            _flash.sprite = _portrait != null ? _portrait.sprite : null;
            _flash.enabled = false;
        }

        void DrawHurt()
        {
            if (_flash == null) return;
            float alpha = _flashArt && _hurt > 0f ? _hurtStrength * (_hurt / HurtTime) : 0f;
            bool show = alpha > 0.01f;
            if (_flash.enabled != show) _flash.enabled = show;
            if (show) _flash.color = new Color(1f, 1f, 1f, alpha);
        }

        void TickStatus(float dt, float form)
        {
            _shield = BattleEase.Damp(_shield, State.Shield > 0.01f ? 1f : 0f, 10f, dt);
            if (_shieldBubble != null)
            {
                float alpha = _shield * (0.42f + 0.1f * Mathf.Sin(_age * 3f + _phase)) * form;
                _shieldBubble.enabled = alpha > 0.01f;
                if (_shieldBubble.enabled)
                {
                    _shieldBubble.color = BattleEase.WithAlpha(_shieldBubble.color, alpha);
                    float scale = BattleEase.OutBack(_shield, 2f) * (1f + 0.04f * Mathf.Sin(_age * 2.2f));
                    _shieldBubble.rectTransform.localScale = new Vector3(scale, scale, 1f);
                }
            }

            // 강화가 약화보다 먼저 보인다
            int kind = State.BuffTimer > 0f ? 1 : State.DebuffTimer > 0f ? 2 : 0;
            if (kind != 0 && kind != _statusKind && _statusIcon != null)
            {
                _statusKind = kind;
                _statusIcon.sprite = kind == 1 ? _buffSprite : _debuffSprite;
            }
            _status = BattleEase.Damp(_status, kind != 0 ? 1f : 0f, 10f, dt);
            if (_statusIcon != null)
            {
                bool show = _status > 0.02f && _statusIcon.sprite != null;
                _statusIcon.enabled = show;
                if (show)
                {
                    var tint = _statusKind == 1 ? _buffTint : _debuffTint;
                    _statusIcon.color = BattleEase.WithAlpha(tint, _status * form);
                    float pop = BattleEase.OutBack(_status, 2.4f);
                    _statusIcon.rectTransform.localScale = new Vector3(pop, pop, 1f);
                    _statusIcon.rectTransform.anchoredPosition = new Vector2(0f, _hover + _height + 14f + Mathf.Sin(_age * 3.2f) * 4f);
                }
            }
        }

        /// <summary>빛으로 풀리기 시작 (몸은 위로 풀리며 투명해진다)</summary>
        public void BeginDissolve()
        {
            if (_dissolveAge >= 0f) return;
            _dissolveAge = 0f;
            if (_hpRoot != null) _hpRoot.gameObject.SetActive(false);
        }

        /// <summary>풀리는 연출. 끝나면 false</summary>
        public bool TickDissolve(float dt)
        {
            _dissolveAge += dt;
            float k = Mathf.Clamp01(_dissolveAge / 0.5f);
            float eased = BattleEase.OutCubic(k);
            _body.localScale = new Vector3(1f - 0.35f * eased, 1f + 0.5f * eased, 1f);
            _body.anchoredPosition += Vector2.up * (dt * 60f);
            _bodyGroup.alpha = _alpha * (1f - k);
            if (_aura != null)
            {
                _aura.color = BattleEase.WithAlpha(_glow, 0.9f * (1f - k));
                float scale = 1f + 0.8f * eased;
                _aura.rectTransform.localScale = new Vector3(scale, scale, 1f);
            }
            if (_lifeFill != null) _lifeFill.color = BattleEase.WithAlpha(UITheme.EchoGlow, 0.8f * (1f - k));
            if (_lifeBack != null) _lifeBack.color = BattleEase.WithAlpha(UITheme.Background, 0.3f * (1f - k));
            if (_shieldBubble != null) _shieldBubble.enabled = false;
            if (_statusIcon != null) _statusIcon.enabled = false;
            if (_flash != null) _flash.enabled = false;
            if (_skillBadge != null && _hasBadge) _skillBadge.localScale = Vector3.one * (1f - k);
            return k < 1f;
        }

        public void Release()
        {
            State = null;
            Id = -1;
            _dissolveAge = -1f;
            gameObject.SetActive(false);
        }

        /// <summary>시뮬레이터 자리(월드)로 — 전체 정지·느린 화면 동안은 그 사이 걸은 만큼을 붙잡았다가 풀리면 따라잡는다 (dt = 연출 시계)</summary>
        void Place(Vector2 world, BattleFieldMapper mapper, float timeScale, float dt)
        {
            var sim = mapper.WorldToField(world);
            _holdOffset = _simFieldValid ? EnemyFxCatalog.HoldOffset(_holdOffset, sim - _simField, timeScale, dt) : Vector2.zero;
            _simField = sim;
            _simFieldValid = true;
            Rect.anchoredPosition = sim + _holdOffset;
            float scale = mapper.ActorScale * _unitScale;
            Rect.localScale = new Vector3(scale, scale, 1f);
            if (_lifeRing != null) _lifeRing.localScale = new Vector3(1f, mapper.Squash, 1f);
        }
    }
}
