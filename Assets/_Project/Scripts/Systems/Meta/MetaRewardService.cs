using System;
using System.Collections.Generic;
using System.Text;

namespace IdleGame
{
    /// <summary>
    /// 메타 보상 지급 창구. MetaReward를 풀어서 지급하고, 실제로 받은 목록(풀린 값, 같은 종류 합침)을 돌려준다.
    ///   · Currency / Item → RewardService.Grant
    ///   · Currency 단추(고정 양) · GoldMinutes는 기준 양 B로 보고 단추 · 실타래로 나눠 준다 (결정 2 안 D, IncomeSplit, 몫마다 내림 정수).
    ///     GoldMinutes의 B = 받는 순간의 Growth.IncomePerSecond(나누기 전) × 60 × 분. 이미 나뉜 줄(바구니)은 splitIncome = false로 넘긴다
    ///   · QuickIdleTicket / DoubleTicket / SelectShards → meta.wallet (빠른 수선권은 상한까지만, 넘친 양은 별조각으로 바꾸지 않고 버린다)
    ///   · Lineage → Characters.Grant (이미 있으면 기억편으로 변환됨, 결과는 Lineage 그대로 + 변환 기억편은 SelectShards가 아니라 계보 기억편)
    ///   · Costume → Costumes.Unlock
    /// 돌려주는 목록에는 GoldMinutes가 없다 (Currency 단추 · 실타래로 풀림). RewardFlyer / 보상 팝업은 이 목록을 그대로 쓴다.
    /// <para>
    /// 재화·재료 양은 정수로 내린다 (배수를 곱한 뒤). 0이 된 보상과 상한 때문에 하나도 못 받은 수선권은 결과에서 빠진다.
    /// 빠른 수선권이 상한에 걸려 일부만 들어가면 결과에는 실제로 들어간 장수만 적힌다.
    /// </para>
    /// </summary>
    public class MetaRewardService
    {
        /// <summary>지급 직후 (풀린 목록). 보상 날아가기(RewardFlyer)가 출처 위치를 모를 때 공용으로 쓴다</summary>
        public event Action<IReadOnlyList<MetaReward>> Granted;
        /// <summary>빠른 수선권 / 두 배 수선권 / 선택 기억편 보유량이 바뀜</summary>
        public event Action WalletChanged;

        /// <summary>IdleChestConfig가 없을 때 빠른 수선권 상한</summary>
        public const int DefaultMaxQuickIdleTickets = 20;

        readonly PlayerData _data;
        readonly MetaDatabase _db;
        readonly RewardService _rewards;
        readonly ItemDatabase _items;
        readonly CharacterSystem _characters;
        readonly CostumeSystem _costumes;
        readonly GrowthSystem _growth;

        public MetaRewardService(PlayerData data, MetaDatabase db, RewardService rewards, ItemDatabase items, CharacterSystem characters,
            CostumeSystem costumes, GrowthSystem growth)
        {
            _data = data;
            _db = db;
            _rewards = rewards;
            _items = items;
            _characters = characters;
            _costumes = costumes;
            _growth = growth;
        }

        MetaWalletSave Wallet => (_data.meta ??= new MetaSave()).wallet ??= new MetaWalletSave();

        /// <summary>
        /// 보상을 지급하고 받은 목록을 돌려준다. multiplier는 재화/재료/단추 N분치에만 곱한다
        /// (excludeGemFromMultiplier면 별조각은 1배, 수선권·친구·의상은 항상 1배). 빈/null 목록이면 빈 목록.
        /// splitIncome이면 Currency 단추 줄을 기준 양 B로 보고 단추 · 실타래로 나눈다 (안 D). 이미 나뉜 단추 · 실타래 줄을 넘기는 쪽(바구니)만 false
        /// </summary>
        public List<MetaReward> Grant(IReadOnlyList<MetaReward> rewards, double multiplier = 1, bool excludeGemFromMultiplier = false,
            bool splitIncome = true)
        {
            var granted = new List<MetaReward>();
            if (rewards == null || rewards.Count == 0) return granted;
            if (!(multiplier > 0) || double.IsInfinity(multiplier)) multiplier = 1;

            bool walletChanged = false;
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null) continue;
                switch (reward.type)
                {
                    case MetaRewardType.Currency:
                    {
                        double scale = excludeGemFromMultiplier && reward.currency == CurrencyType.Gem ? 1 : multiplier;
                        if (splitIncome && reward.currency == CurrencyType.Gold)
                        {
                            GrantIncome(IncomeOf(reward) * scale, granted);
                            break;
                        }
                        double amount = Whole(reward.amount * scale);
                        if (amount <= 0) break;
                        _rewards?.Grant(RewardData.Currency(reward.currency, amount));
                        granted.Add(MetaReward.Currency(reward.currency, amount));
                        break;
                    }
                    case MetaRewardType.Item:
                    {
                        int count = (int)Math.Min(int.MaxValue, Whole(reward.amount * multiplier));
                        if (count <= 0 || string.IsNullOrEmpty(reward.itemId)) break;
                        _rewards?.Grant(RewardData.Item(reward.itemId, count));
                        granted.Add(MetaReward.Item(reward.itemId, count));
                        break;
                    }
                    case MetaRewardType.GoldMinutes:
                    {
                        GrantIncome(IncomeOf(reward) * multiplier, granted);
                        break;
                    }
                    case MetaRewardType.QuickIdleTicket:
                    {
                        var wallet = Wallet;
                        int room = Math.Max(0, MaxQuickIdleTickets - wallet.quickIdleTickets);
                        int add = Math.Min(room, Count(reward.amount));
                        if (add <= 0) break;
                        wallet.quickIdleTickets += add;
                        walletChanged = true;
                        granted.Add(MetaReward.QuickIdleTicket(add));
                        break;
                    }
                    case MetaRewardType.DoubleTicket:
                    {
                        int add = Count(reward.amount);
                        if (add <= 0) break;
                        Wallet.doubleTickets = SafeAdd(Wallet.doubleTickets, add);
                        walletChanged = true;
                        granted.Add(MetaReward.DoubleTicket(add));
                        break;
                    }
                    case MetaRewardType.SelectShards:
                    {
                        int add = Count(reward.amount);
                        if (add <= 0) break;
                        Wallet.selectShards = SafeAdd(Wallet.selectShards, add);
                        walletChanged = true;
                        granted.Add(MetaReward.SelectShards(add));
                        break;
                    }
                    case MetaRewardType.Lineage:
                    {
                        if (string.IsNullOrEmpty(reward.itemId) || _characters == null) break;
                        var result = _characters.Grant(reward.itemId);
                        if (result != null) granted.Add(MetaReward.Lineage(reward.itemId));
                        break;
                    }
                    case MetaRewardType.Costume:
                    {
                        if (string.IsNullOrEmpty(reward.itemId) || _costumes == null) break;
                        if (_characters?.Db != null && _characters.Db.GetCostume(reward.itemId) == null) break;
                        _costumes.Unlock(reward.itemId);
                        granted.Add(MetaReward.Costume(reward.itemId));
                        break;
                    }
                    case MetaRewardType.FriendSelectTicket:
                    {
                        // 친구 선택권 (PRG-04): 지갑에 쌓고, 고르기는 GachaMilestoneSystem.TryUseFriendTicket (전설 이하)
                        int add = Count(reward.amount);
                        if (add <= 0) break;
                        Wallet.friendSelectTickets = SafeAdd(Wallet.friendSelectTickets, add);
                        walletChanged = true;
                        granted.Add(FriendTicket(add));
                        break;
                    }
                }
            }

            var merged = Merge(granted);
            if (walletChanged) WalletChanged?.Invoke();
            if (merged.Count > 0) Granted?.Invoke(merged);
            return merged;
        }

        /// <summary>친구 선택권 보상 (itemId = 고를 수 있는 최고 등급 "Legendary", SPEC4 §2.11 #4)</summary>
        public static MetaReward FriendTicket(int count = 1) =>
            new() { type = MetaRewardType.FriendSelectTicket, itemId = nameof(ItemGrade.Legendary), amount = count };

        /// <summary>가진 친구 선택권 수 (meta.wallet.friendSelectTickets)</summary>
        public int FriendSelectTickets => Wallet.friendSelectTickets;

        /// <summary>친구 선택권 한 장을 쓴다 (없으면 false). 고른 친구는 부르는 쪽이 Grant(Lineage)로 준다</summary>
        public bool TrySpendFriendTicket()
        {
            var wallet = Wallet;
            if (wallet.friendSelectTickets <= 0) return false;
            wallet.friendSelectTickets--;
            WalletChanged?.Invoke();
            return true;
        }

        /// <summary>쓴 친구 선택권을 되돌린다 (고른 친구를 줄 수 없었을 때)</summary>
        public void RefundFriendTicket()
        {
            Wallet.friendSelectTickets = SafeAdd(Wallet.friendSelectTickets, 1);
            WalletChanged?.Invoke();
        }

        /// <summary>
        /// 보상 하나를 풀어 into 끝에 더한다 (지급하지 않음). GoldMinutes · 고정 단추는 단추 → 실타래 두 줄 (몫이 1 이상인 줄만, 안 D).
        /// 지급(Grant)과 같은 함수(IncomeOf → AddIncome)로 나누므로 미리보기와 실제 지급이 어긋나지 않는다
        /// </summary>
        public void ResolveInto(MetaReward reward, List<MetaReward> into)
        {
            if (reward == null || into == null) return;
            if (IsIncome(reward)) AddIncome(IncomeOf(reward), into);
            else into.Add(reward.Clone());
        }

        /// <summary>
        /// 목록 전체를 지급할 값으로 푼다 (지급하지 않음, into는 비우고 채움). 단추 · N분치는 지급과 같게 단추 · 실타래 두 줄로, N분치는 지금 수입으로 숫자가 된다
        /// (튜토리얼 선물 날리기 · 졸업 카드). 화면 미리보기 칩은 N분치를 글로 두는 PreviewAll을 쓴다
        /// </summary>
        public void ResolveAll(IReadOnlyList<MetaReward> rewards, List<MetaReward> into)
        {
            if (into == null) return;
            into.Clear();
            if (rewards == null) return;
            for (int i = 0; i < rewards.Count; i++) ResolveInto(rewards[i], into);
        }

        /// <summary>
        /// 화면 미리보기용 목록 (지급하지 않음, into는 비우고 채움, 팀 결정: 미리보기 칩 = 실제 지급). 고정 단추는 지급과 같은 나누기(AddIncome)로
        /// 단추 ⌊n × 몫⌋ · 실타래 ⌊n × 몫⌋ 두 줄, N분치는 그대로 두어 "단추 · 실타래 N분치" 글로 보인다 (받는 순간 수입이 정하므로 숫자로 바꾸지 않는다).
        /// 같은 재화 줄은 합친다 (Merge: 나뉜 실타래 + 직접 적은 실타래 = 한 줄)
        /// </summary>
        public void PreviewAll(IReadOnlyList<MetaReward> rewards, List<MetaReward> into)
        {
            if (into == null) return;
            into.Clear();
            if (rewards == null) return;
            _previewScratch.Clear();
            for (int i = 0; i < rewards.Count; i++)
            {
                var reward = rewards[i];
                if (reward == null) continue;
                if (reward.type == MetaRewardType.Currency && reward.currency == CurrencyType.Gold) AddIncome(IncomeOf(reward), _previewScratch);
                else _previewScratch.Add(reward.Clone());
            }
            into.AddRange(Merge(_previewScratch));
            _previewScratch.Clear();
        }

        readonly List<MetaReward> _previewScratch = new();

        /// <summary>표시 문구 (예: "단추 1,200", "별조각 300", "수선 천 ×5", "빠른 수선권 ×1", "루즈리"). GoldMinutes는 "단추 · 실타래 5분치" (안 D: 둘로 나뉘어 들어온다). 개수는 곱하기 기호(U+00D7)</summary>
        public string Describe(MetaReward reward)
        {
            if (reward == null) return string.Empty;
            switch (reward.type)
            {
                case MetaRewardType.Currency:
                    return $"{RewardService.CurrencyName(reward.currency)} {RewardService.FormatAmount(reward.currency, reward.amount)}";
                case MetaRewardType.Item:
                    return $"{NameOf(reward)} ×{Count(reward.amount)}";
                case MetaRewardType.GoldMinutes:
                    return IncomeLabel + " " + MinutesText(reward.amount);
                case MetaRewardType.QuickIdleTicket:
                case MetaRewardType.DoubleTicket:
                case MetaRewardType.SelectShards:
                case MetaRewardType.FriendSelectTicket:
                    return $"{NameOf(reward)} ×{Count(reward.amount)}";
                case MetaRewardType.Lineage:
                case MetaRewardType.Costume:
                    return NameOf(reward);
                default:
                    return string.Empty;
            }
        }

        /// <summary>목록 표시 문구 (", "로 잇기)</summary>
        public string Describe(IReadOnlyList<MetaReward> rewards, string separator = ", ")
        {
            if (rewards == null || rewards.Count == 0) return string.Empty;
            var builder = new StringBuilder();
            for (int i = 0; i < rewards.Count; i++)
            {
                string text = Describe(rewards[i]);
                if (string.IsNullOrEmpty(text)) continue;
                if (builder.Length > 0) builder.Append(separator);
                builder.Append(text);
            }
            return builder.ToString();
        }

        /// <summary>종류 이름 (단추/별조각/별빛 사탕/재료 이름/빠른 수선권/두 배 수선권/선택 기억편/계보 이름/의상 이름)</summary>
        public string NameOf(MetaReward reward)
        {
            if (reward == null) return string.Empty;
            switch (reward.type)
            {
                case MetaRewardType.Currency:
                    return RewardService.CurrencyName(reward.currency);
                case MetaRewardType.GoldMinutes:
                    return IncomeLabel;
                case MetaRewardType.Item:
                {
                    var def = _items != null ? _items.Get(reward.itemId) : null;
                    return def != null && !string.IsNullOrEmpty(def.name) ? def.name : reward.itemId ?? string.Empty;
                }
                case MetaRewardType.QuickIdleTicket:
                    return "빠른 수선권";
                case MetaRewardType.DoubleTicket:
                    return "두 배 수선권";
                case MetaRewardType.SelectShards:
                    return "선택 기억편";
                case MetaRewardType.FriendSelectTicket:
                    return "친구 선택권";
                case MetaRewardType.Lineage:
                {
                    var def = _characters?.Db != null ? _characters.Db.GetLineage(reward.itemId) : null;
                    return def != null && !string.IsNullOrEmpty(def.name) ? def.name : reward.itemId ?? string.Empty;
                }
                case MetaRewardType.Costume:
                {
                    var def = _characters?.Db != null ? _characters.Db.GetCostume(reward.itemId) : null;
                    return def != null && !string.IsNullOrEmpty(def.name) ? def.name : reward.itemId ?? string.Empty;
                }
                default:
                    return string.Empty;
            }
        }

        /// <summary>같은 종류를 합친 새 목록</summary>
        public static List<MetaReward> Merge(IEnumerable<MetaReward> rewards)
        {
            var merged = new List<MetaReward>();
            if (rewards == null) return merged;
            foreach (var reward in rewards)
            {
                if (reward == null) continue;
                MetaReward same = null;
                for (int i = 0; i < merged.Count; i++)
                {
                    if (merged[i].IsSameKind(reward))
                    {
                        same = merged[i];
                        break;
                    }
                }
                // 친구·의상은 같은 것이 둘이어도 한 칸씩 (두 번째는 기억편으로 바뀌었을 수 있다)
                bool stackable = reward.type != MetaRewardType.Lineage && reward.type != MetaRewardType.Costume;
                if (same != null && stackable) same.amount += reward.amount;
                else merged.Add(reward.Clone());
            }
            return merged;
        }

        // ── 메타 보관함 ──

        public int QuickIdleTickets => _data.meta.wallet.quickIdleTickets;
        public int DoubleTickets => _data.meta.wallet.doubleTickets;
        public int SelectShards => _data.meta.wallet.selectShards;

        /// <summary>빠른 수선권 보관 상한 (IdleChestConfig.maxQuickIdleTickets)</summary>
        public int MaxQuickIdleTickets => _db != null && _db.idleChest != null ? Math.Max(0, _db.idleChest.maxQuickIdleTickets) : DefaultMaxQuickIdleTickets;

        public bool TrySpendQuickIdleTicket()
        {
            var wallet = Wallet;
            if (wallet.quickIdleTickets <= 0) return false;
            wallet.quickIdleTickets--;
            WalletChanged?.Invoke();
            return true;
        }

        public bool TrySpendDoubleTicket()
        {
            var wallet = Wallet;
            if (wallet.doubleTickets <= 0) return false;
            wallet.doubleTickets--;
            WalletChanged?.Invoke();
            return true;
        }

        /// <summary>선택 기억편을 계보 기억편으로 넣는다 (보유 계보만, amount ≤ 보유량)</summary>
        public bool TryAssignSelectShards(string lineageId, int amount)
        {
            var wallet = Wallet;
            if (amount <= 0 || amount > wallet.selectShards || _characters == null || !_characters.IsOwned(lineageId)) return false;
            if (!_characters.AddShards(lineageId, amount)) return false;
            wallet.selectShards -= amount;
            WalletChanged?.Invoke();
            return true;
        }

        // ───────────────────────────── 내부 ─────────────────────────────

        /// <summary>N분치 기준 양 B (나누기 전, 내림 전) = 받는 순간의 기준 초당 수입 × 60 × 분</summary>
        double IncomeForMinutes(double minutes)
        {
            if (!(minutes > 0) || _growth == null) return 0;
            double perSecond = _growth.IncomePerSecond;
            return perSecond > 0 ? perSecond * 60.0 * minutes : 0;
        }

        /// <summary>나눌 보상인지 (Currency 단추 · GoldMinutes). 실타래로 직접 적은 보상은 나누지 않는다</summary>
        static bool IsIncome(MetaReward reward) =>
            reward.type == MetaRewardType.GoldMinutes || (reward.type == MetaRewardType.Currency && reward.currency == CurrencyType.Gold);

        /// <summary>나눌 보상의 기준 양 B (배수 전): 고정 단추 = 양, N분치 = 받는 순간 기준 초당 수입 × 60 × 분</summary>
        double IncomeOf(MetaReward reward) => reward.type == MetaRewardType.GoldMinutes ? IncomeForMinutes(reward.amount) : Math.Max(0, reward.amount);

        readonly List<MetaReward> _income = new(2);

        /// <summary>기준 양 B를 단추 → 실타래로 나눠(AddIncome, 미리보기와 같은 함수) 지급하고 granted에 적는다 (몫마다 내림, 0인 줄은 빠짐)</summary>
        void GrantIncome(double income, List<MetaReward> granted)
        {
            _income.Clear();
            AddIncome(income, _income);
            for (int i = 0; i < _income.Count; i++)
            {
                var line = _income[i];
                _rewards?.Grant(RewardData.Currency(line.currency, line.amount));
                granted.Add(line);
            }
            _income.Clear();
        }

        /// <summary>기준 양 B의 단추 → 실타래 줄을 into에 더한다 (지급하지 않음)</summary>
        static void AddIncome(double income, List<MetaReward> into)
        {
            double gold = IncomeSplit.GoldWhole(income);
            double lineageXp = IncomeSplit.LineageXpWhole(income);
            if (gold > 0) into.Add(MetaReward.Gold(gold));
            if (lineageXp > 0) into.Add(MetaReward.Currency(CurrencyType.LineageXp, lineageXp));
        }

        /// <summary>N분치 보상이 들어오는 재화 이름 ("단추 · 실타래", 안 D)</summary>
        public static string IncomeLabel => $"{RewardService.CurrencyName(CurrencyType.Gold)} · {RewardService.CurrencyName(CurrencyType.LineageXp)}";

        /// <summary>"5분치" / "2시간치" / "1시간 30분치"</summary>
        static string MinutesText(double minutes)
        {
            int total = (int)Math.Max(0, Math.Round(minutes));
            if (total >= 60)
            {
                int hours = total / 60;
                int rest = total % 60;
                return rest == 0 ? $"{hours}시간치" : $"{hours}시간 {rest}분치";
            }
            return $"{total}분치";
        }

        /// <summary>정수로 내림 (부동소수 오차 보정)</summary>
        static double Whole(double value) => double.IsNaN(value) || value <= 0 ? 0 : Math.Floor(value + 1e-9);

        static int Count(double amount) => (int)Math.Min(int.MaxValue, Math.Max(0, Math.Round(amount)));

        static int SafeAdd(int a, int b)
        {
            long sum = (long)a + b;
            return sum > int.MaxValue ? int.MaxValue : (int)sum;
        }
    }
}
