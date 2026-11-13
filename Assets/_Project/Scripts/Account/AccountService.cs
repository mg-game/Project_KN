using System;

namespace IdleGame
{
    public enum LinkResult
    {
        Success,
        Cancelled,
        /// <summary>SDK 미연동 등으로 이 기기에서 쓸 수 없음</summary>
        NotAvailable,
        /// <summary>이 게임 계정에 이미 연동됨</summary>
        AlreadyLinked,
        /// <summary>해당 외부 계정이 다른 게임 계정에 연동되어 있음 (서버 판정)</summary>
        LinkedToOtherAccount,
        /// <summary>다른 연동 요청이 진행 중</summary>
        Busy,
        Failed,
    }

    /// <summary>
    /// 계정 서버 역할. 서버/BaaS가 정해지면 이 인터페이스로 구현한다.
    /// 서버는 credential.token을 검증하고 외부 계정 ↔ 게임 계정(accountId) 매핑을 저장해야 한다.
    /// </summary>
    public interface IAccountService
    {
        void Link(string accountId, AuthCredential credential, Action<LinkResult> onComplete);
        void Unlink(string accountId, AuthProviderType provider, Action<LinkResult> onComplete);
    }

    /// <summary>서버 없이 로컬에서 항상 성공 처리하는 임시 구현</summary>
    public sealed class LocalAccountService : IAccountService
    {
        public void Link(string accountId, AuthCredential credential, Action<LinkResult> onComplete) => onComplete(LinkResult.Success);

        public void Unlink(string accountId, AuthProviderType provider, Action<LinkResult> onComplete) => onComplete(LinkResult.Success);
    }
}
