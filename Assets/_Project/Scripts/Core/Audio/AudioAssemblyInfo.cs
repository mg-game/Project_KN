using System.Runtime.CompilerServices;

// AudioEngine(internal)을 EditMode 스모크 테스트가 직접 만들어 돌려 볼 수 있게 (AudioEngineSmokeTests)
[assembly: InternalsVisibleTo("IdleGame.Tests.EditMode")]
