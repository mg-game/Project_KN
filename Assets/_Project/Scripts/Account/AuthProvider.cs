using System;

namespace IdleGame
{
    public enum AuthProviderType
    {
        Google = 1,
        Kakao = 2,
    }

    /// <summary>외부 로그인(SDK) 결과로 받은 계정 정보. token은 서버 검증용(ID 토큰, 액세스 토큰 등).</summary>
    public class AuthCredential
    {
        public AuthProviderType provider;
        public string userId;
        public string displayName;
        public string token;
    }

    public enum AuthStatus
    {
        Success,
        Cancelled,
        Failed,
    }

    public readonly struct AuthResult
    {
        public readonly AuthStatus Status;
        public readonly AuthCredential Credential;
        public readonly string Error;

        AuthResult(AuthStatus status, AuthCredential credential, string error)
        {
            Status = status;
            Credential = credential;
            Error = error;
        }

        public static AuthResult Success(AuthCredential credential) => new(AuthStatus.Success, credential, null);
        public static AuthResult Cancelled() => new(AuthStatus.Cancelled, null, null);
        public static AuthResult Failed(string error) => new(AuthStatus.Failed, null, error);
    }

    /// <summary>
    /// 외부 로그인 SDK 연결부. SDK 콜백 방식에 맞춰 결과를 onComplete로 돌려준다.
    /// </summary>
    public interface IAuthProvider
    {
        AuthProviderType Type { get; }
        /// <summary>이 기기/빌드에서 로그인할 수 있는지 (SDK 미연동이면 false)</summary>
        bool IsAvailable { get; }
        void SignIn(Action<AuthResult> onComplete);
        void SignOut();
    }

    /// <summary>에디터/개발 빌드용 가짜 로그인. 항상 성공한다.</summary>
    public sealed class MockAuthProvider : IAuthProvider
    {
        public MockAuthProvider(AuthProviderType type)
        {
            Type = type;
        }

        public AuthProviderType Type { get; }
        public bool IsAvailable => true;

        public void SignIn(Action<AuthResult> onComplete)
        {
            onComplete(AuthResult.Success(new AuthCredential
            {
                provider = Type,
                userId = $"mock-{Type.ToString().ToLowerInvariant()}-{Guid.NewGuid().ToString("N").Substring(0, 8)}",
                displayName = $"{AccountSystem.ProviderName(Type)} 테스트 계정",
                token = "mock-token",
            }));
        }

        public void SignOut() { }
    }

    /// <summary>
    /// Google 로그인 자리.
    /// Android: Google Play Games Services v2 플러그인(또는 Credential Manager 네이티브 플러그인),
    /// iOS: Google Sign-In SDK 를 붙이고, 받은 ID 토큰/서버 인증 코드를 AuthCredential.token에 담아 반환한다.
    /// </summary>
    public sealed class GoogleAuthProvider : IAuthProvider
    {
        public AuthProviderType Type => AuthProviderType.Google;
        public bool IsAvailable => false;

        public void SignIn(Action<AuthResult> onComplete) => onComplete(AuthResult.Failed("Google SDK가 연동되지 않았습니다."));

        public void SignOut() { }
    }

    /// <summary>
    /// 카카오 로그인 자리. 카카오는 Unity 공식 SDK가 없어서 Android/iOS 카카오 SDK를 네이티브 플러그인으로 감싸 호출한다.
    /// (카카오 디벨로퍼스 네이티브 앱 키, Android 키 해시, iOS 번들 ID 등록 필요) 받은 액세스 토큰을 AuthCredential.token에 담는다.
    /// </summary>
    public sealed class KakaoAuthProvider : IAuthProvider
    {
        public AuthProviderType Type => AuthProviderType.Kakao;
        public bool IsAvailable => false;

        public void SignIn(Action<AuthResult> onComplete) => onComplete(AuthResult.Failed("카카오 SDK가 연동되지 않았습니다."));

        public void SignOut() { }
    }
}
