using System;
using NUnit.Framework;

namespace IdleGame.Tests
{
    public class AccountSystemTests : SystemsTestBase
    {
        /// <summary>정해진 결과를 돌려주거나, deferred면 콜백을 보관했다가 나중에 완료하는 테스트용 공급자</summary>
        sealed class FakeProvider : IAuthProvider
        {
            readonly AuthStatus _status;
            readonly bool _deferred;
            public Action<AuthResult> Pending;

            public FakeProvider(AuthProviderType type, AuthStatus status, bool deferred = false)
            {
                Type = type;
                _status = status;
                _deferred = deferred;
            }

            public AuthProviderType Type { get; }
            public bool IsAvailable => true;

            public void SignIn(Action<AuthResult> onComplete)
            {
                if (_deferred) Pending = onComplete;
                else onComplete(Result());
            }

            public AuthResult Result() => _status == AuthStatus.Success
                ? AuthResult.Success(new AuthCredential { provider = Type, userId = "u1", displayName = "테스터", token = "t" })
                : _status == AuthStatus.Cancelled ? AuthResult.Cancelled() : AuthResult.Failed("error");

            public void SignOut() { }
        }

        sealed class RejectingService : IAccountService
        {
            public void Link(string accountId, AuthCredential credential, Action<LinkResult> onComplete) =>
                onComplete(LinkResult.LinkedToOtherAccount);

            public void Unlink(string accountId, AuthProviderType provider, Action<LinkResult> onComplete) =>
                onComplete(LinkResult.Success);
        }

        AccountSystem Create(IAccountService service, params IAuthProvider[] providers) =>
            new AccountSystem(Data, Clock, service, providers);

        LinkResult LinkNow(AccountSystem account, AuthProviderType type)
        {
            LinkResult? result = null;
            account.Link(type, r => result = r);
            Assert.IsTrue(result.HasValue, "콜백이 호출되지 않음");
            return result.Value;
        }

        [Test]
        public void GuestId_IsIssuedOnce()
        {
            string id = Account.AccountId;

            Assert.IsFalse(string.IsNullOrEmpty(id));
            Assert.IsTrue(Account.IsGuest);
            Assert.AreEqual(id, Create(new LocalAccountService()).AccountId, "같은 데이터면 같은 ID");
        }

        [Test]
        public void Link_Success_SavesLinkAndSendsRewardMailOnce()
        {
            Assert.AreEqual(LinkResult.Success, LinkNow(Account, AuthProviderType.Google));

            Assert.IsTrue(Account.IsLinked(AuthProviderType.Google));
            Assert.IsFalse(Account.IsGuest);
            Assert.IsFalse(string.IsNullOrEmpty(Account.GetLink(AuthProviderType.Google).userId));
            Assert.AreEqual(1, Mail.Count, "첫 연동 보상");

            Assert.AreEqual(LinkResult.Success, LinkNow(Account, AuthProviderType.Kakao));
            Assert.AreEqual(1, Mail.Count, "연동 보상은 한 번만");
        }

        [Test]
        public void Link_AlreadyLinked()
        {
            LinkNow(Account, AuthProviderType.Google);
            Assert.AreEqual(LinkResult.AlreadyLinked, LinkNow(Account, AuthProviderType.Google));
        }

        [Test]
        public void Link_Cancelled_DoesNotLink()
        {
            var account = Create(new LocalAccountService(), new FakeProvider(AuthProviderType.Kakao, AuthStatus.Cancelled));

            Assert.AreEqual(LinkResult.Cancelled, LinkNow(account, AuthProviderType.Kakao));
            Assert.IsTrue(account.IsGuest);
            Assert.IsFalse(account.IsBusy);
        }

        [Test]
        public void Link_ServerRejects_DoesNotLink()
        {
            var account = Create(new RejectingService(), new FakeProvider(AuthProviderType.Google, AuthStatus.Success));

            Assert.AreEqual(LinkResult.LinkedToOtherAccount, LinkNow(account, AuthProviderType.Google));
            Assert.IsFalse(account.IsLinked(AuthProviderType.Google));
        }

        [Test]
        public void Link_SdkNotIntegrated_ReturnsNotAvailable()
        {
            var account = Create(new LocalAccountService(), AccountSystem.CreateDefaultProviders(useMock: false));

            Assert.AreEqual(LinkResult.NotAvailable, LinkNow(account, AuthProviderType.Google));
            Assert.AreEqual(LinkResult.NotAvailable, LinkNow(account, AuthProviderType.Kakao));
        }

        [Test]
        public void Link_WhileBusy_ReturnsBusy()
        {
            var provider = new FakeProvider(AuthProviderType.Google, AuthStatus.Success, deferred: true);
            var account = Create(new LocalAccountService(), provider, new MockAuthProvider(AuthProviderType.Kakao));
            LinkResult? first = null;

            account.Link(AuthProviderType.Google, r => first = r);
            Assert.IsTrue(account.IsBusy);
            Assert.AreEqual(LinkResult.Busy, LinkNow(account, AuthProviderType.Kakao));

            provider.Pending(provider.Result());
            Assert.AreEqual(LinkResult.Success, first);
            Assert.IsFalse(account.IsBusy);
        }

        [Test]
        public void Unlink_ReturnsToGuest()
        {
            LinkNow(Account, AuthProviderType.Kakao);
            LinkResult? result = null;

            Account.Unlink(AuthProviderType.Kakao, r => result = r);

            Assert.AreEqual(LinkResult.Success, result);
            Assert.IsTrue(Account.IsGuest);
        }
    }
}
