using System;
using System.Collections.Generic;

namespace IdleGame
{
    [Serializable]
    public class LinkedAccount
    {
        public string userId;
        public string displayName;
        public long linkedAt;
    }

    /// <summary>
    /// 게스트 계정 + 외부 계정(Google, 카카오) 연동.
    /// 첫 실행 시 게스트 ID를 발급하고, 연동 결과는 PlayerData에 저장한다.
    /// </summary>
    public class AccountSystem
    {
        public event Action Changed;
        /// <summary>연동 성공 시 (연동 보상 등에 사용)</summary>
        public event Action<AuthProviderType> Linked;

        readonly PlayerData _data;
        readonly ITimeProvider _clock;
        readonly IAccountService _service;
        readonly Dictionary<AuthProviderType, IAuthProvider> _providers = new();

        public AccountSystem(PlayerData data, ITimeProvider clock, IAccountService service, IEnumerable<IAuthProvider> providers)
        {
            _data = data;
            _clock = clock;
            _service = service;
            foreach (var provider in providers) _providers[provider.Type] = provider;
            if (string.IsNullOrEmpty(_data.accountId)) _data.accountId = Guid.NewGuid().ToString("N");
        }

        public string AccountId => _data.accountId;
        public bool IsGuest => _data.linkedAccounts.Count == 0;
        public int LinkedCount => _data.linkedAccounts.Count;
        public bool IsBusy { get; private set; }

        public bool IsLinked(AuthProviderType type) => _data.linkedAccounts.ContainsKey(type);

        public LinkedAccount GetLink(AuthProviderType type) => _data.linkedAccounts.TryGetValue(type, out var link) ? link : null;

        public bool IsAvailable(AuthProviderType type) => _providers.TryGetValue(type, out var provider) && provider.IsAvailable;

        public void Link(AuthProviderType type, Action<LinkResult> onComplete)
        {
            if (IsBusy) { onComplete?.Invoke(LinkResult.Busy); return; }
            if (IsLinked(type)) { onComplete?.Invoke(LinkResult.AlreadyLinked); return; }
            if (!IsAvailable(type)) { onComplete?.Invoke(LinkResult.NotAvailable); return; }

            IsBusy = true;
            _providers[type].SignIn(auth =>
            {
                if (auth.Status != AuthStatus.Success)
                {
                    IsBusy = false;
                    onComplete?.Invoke(auth.Status == AuthStatus.Cancelled ? LinkResult.Cancelled : LinkResult.Failed);
                    return;
                }

                _service.Link(AccountId, auth.Credential, result =>
                {
                    IsBusy = false;
                    if (result == LinkResult.Success)
                    {
                        _data.linkedAccounts[type] = new LinkedAccount
                        {
                            userId = auth.Credential.userId,
                            displayName = auth.Credential.displayName,
                            linkedAt = TimeUtil.ToUnix(_clock.UtcNow),
                        };
                        Changed?.Invoke();
                        Linked?.Invoke(type);
                    }
                    else
                    {
                        _providers[type].SignOut();
                    }
                    onComplete?.Invoke(result);
                });
            });
        }

        public void Unlink(AuthProviderType type, Action<LinkResult> onComplete)
        {
            if (IsBusy) { onComplete?.Invoke(LinkResult.Busy); return; }
            if (!IsLinked(type)) { onComplete?.Invoke(LinkResult.Failed); return; }

            IsBusy = true;
            _service.Unlink(AccountId, type, result =>
            {
                IsBusy = false;
                if (result == LinkResult.Success)
                {
                    if (_providers.TryGetValue(type, out var provider)) provider.SignOut();
                    _data.linkedAccounts.Remove(type);
                    Changed?.Invoke();
                }
                onComplete?.Invoke(result);
            });
        }

        public static string ProviderName(AuthProviderType type) => type switch
        {
            AuthProviderType.Google => "Google",
            AuthProviderType.Kakao => "카카오",
            _ => type.ToString(),
        };

        /// <summary>기본 로그인 공급자. useMock이면 에디터/개발 빌드에서 가짜 로그인을 쓴다.</summary>
        public static IAuthProvider[] CreateDefaultProviders(bool useMock)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (useMock) return new IAuthProvider[] { new MockAuthProvider(AuthProviderType.Google), new MockAuthProvider(AuthProviderType.Kakao) };
#endif
            return new IAuthProvider[] { new GoogleAuthProvider(), new KakaoAuthProvider() };
        }
    }
}
