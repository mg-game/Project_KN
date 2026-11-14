using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace IdleGame.Tests
{
    /// <summary>
    /// PlayMode 시험 동안 소리를 끈다 (사용자 요청: 앱을 켜서 시험할 때 소리 없음). 이 어셈블리의 IdleGame.Tests 시험 전체에 걸린다.
    /// 장면이 뜨면 GameBootstrap → GameSettings.Apply가 AudioListener.volume을 설정값으로 되돌리므로, 장면이 바뀌어도 남는 지킴이가
    /// 매 프레임 끝에 0으로 돌린다. 게임 코드 · 저장된 소리 설정(PlayerPrefs)은 바꾸지 않고, 시험이 끝나면 지킴이를 지운다.
    /// 에디터에서 그냥 Play할 때는 걸리지 않는다 (시험 실행 때만 만드는 SetUpFixture).
    /// </summary>
    [SetUpFixture]
    public class TestAudioMute
    {
        GameObject _keeper;

        [OneTimeSetUp]
        public void Mute()
        {
            AudioListener.volume = 0f;
            _keeper = new GameObject("[TestAudioMute]");
            Object.DontDestroyOnLoad(_keeper);
            _keeper.AddComponent<TestAudioMuteKeeper>();
            // 장면이 뜬 직후(부팅 Awake의 GameSettings.Apply 바로 뒤, 첫 프레임 전)에도 0
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => AudioListener.volume = 0f;

        [OneTimeTearDown]
        public void Release()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (_keeper != null) Object.Destroy(_keeper);
            _keeper = null;
        }
    }

    /// <summary>시험 동안 AudioListener.volume을 0으로 지킨다 (TestAudioMute가 만든다)</summary>
    public sealed class TestAudioMuteKeeper : MonoBehaviour
    {
        void LateUpdate()
        {
            if (AudioListener.volume != 0f) AudioListener.volume = 0f;
        }
    }
}
