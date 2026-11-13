using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace IdleGame.Tests
{
    /// <summary>
    /// G1 게이트 (PHASE4-ROADMAP §2.5 5번 · §6.2 "버전 3 세이브가 읽히지 않으면 실패"): 실제 버전 3 세이브로 게임을 켜고 돌린 뒤 버전 4로 다시 저장한다.
    /// SaveMigrationV4Tests는 저장소 단계(읽기 · 옮기기 · 다시 쓰기)만 본다 — 여기서는 실제 콘텐츠(Assets/_Project/Data/*.asset)로
    /// Game.Setup → MetaSession.Start → 전투 · 메타 Tick 5분 → 저장 → 다시 읽기까지, GameBootstrap과 같은 순서로 돈다.
    /// 픽스처 두 개: Wave-2 r2 AutoShot 세이브(autoshot_player_v3.json)와 그보다 오래된 모양의 ux_shots 세이브(ux_shots_autoshot_player_v3.json,
    /// 자동 보스 도전 · 원정 마지막 편성 · 배치 완료 칸 없음, 지금 콘텐츠에 없는 계보 포함). 사용자 세이브는 건드리지 않는다 (임시 폴더).
    /// </summary>
    public class SaveMigrationV4BootTests
    {
        const string DataFolder = "Assets/_Project/Data";

        string _dir;

        [SetUp]
        public void CreateTempDir()
        {
            _dir = Path.Combine(Path.GetTempPath(), "ll_save_v4_boot_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        [TearDown]
        public void CleanUp()
        {
            Game.Shutdown();
            try
            {
                if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
            }
            catch (IOException)
            {
                // 임시 폴더 — 지우지 못해도 테스트 결과와 무관
            }
        }

        static string FixturePath(string name, [CallerFilePath] string thisFile = "")
        {
            string fromAssets = Path.Combine(Application.dataPath, "_Project", "Tests", "EditMode", "Fixtures", name);
            if (File.Exists(fromAssets)) return fromAssets;
            string besideThis = string.IsNullOrEmpty(thisFile) ? null : Path.Combine(Path.GetDirectoryName(thisFile) ?? ".", "Fixtures", name);
            return besideThis != null && File.Exists(besideThis) ? besideThis : fromAssets;
        }

        static T LoadData<T>(string file) where T : UnityEngine.Object
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>($"{DataFolder}/{file}");
            if (asset == null) Assert.Ignore($"{DataFolder}/{file} 없음 (IdleProjectSetup.BatchBuildAll 뒤에 돈다)");
            return asset;
        }

        /// <summary>GameBootstrap이 씬에서 받는 것과 같은 실제 콘텐츠</summary>
        static GameContent RealContent()
        {
            var tables = new List<GachaTable>();
            foreach (string guid in AssetDatabase.FindAssets("t:GachaTable", new[] { DataFolder }))
            {
                var table = AssetDatabase.LoadAssetAtPath<GachaTable>(AssetDatabase.GUIDToAssetPath(guid));
                if (table != null) tables.Add(table);
            }
            if (tables.Count == 0) Assert.Ignore("GachaTable 에셋 없음");

            return new GameContent
            {
                Config = LoadData<GameConfig>("GameConfig.asset"),
                Items = LoadData<ItemDatabase>("ItemDatabase.asset"),
                Characters = LoadData<CharacterDatabase>("CharacterDatabase.asset"),
                GachaTables = tables.ToArray(),
                Battle = LoadData<BattleConfig>("BattleConfig.asset"),
                Expeditions = LoadData<ExpeditionDatabase>("ExpeditionDatabase.asset"),
                Shops = LoadData<ShopDatabase>("ShopDatabase.asset"),
                Synergies = LoadData<SynergyDatabase>("SynergyDatabase.asset"),
                Meta = LoadData<MetaDatabase>("MetaDatabase.asset"),
            };
        }

        static long Lifetime(PlayerData data, CounterKey key) =>
            data.meta.counters.lifetime.TryGetValue(key, out long value) ? value : 0;

        [TestCase("autoshot_player_v3.json")]
        [TestCase("ux_shots_autoshot_player_v3.json")]
        public void V3Save_BootsPlaysAndReSavesV4(string fixture)
        {
            string source = FixturePath(fixture);
            Assert.IsTrue(File.Exists(source), $"픽스처가 없음: {source}");
            var json = JObject.Parse(File.ReadAllText(source));
            Assert.AreEqual(3, (int)json["version"], "픽스처는 버전 3 세이브여야 한다");
            int fixtureStage = (int?)json["battle"]?["highestStage"] ?? 1;

            var content = RealContent();

            // ── 읽기 (실제 저장소, 임시 경로) ──
            string path = Path.Combine(_dir, "player.json");
            File.Copy(source, path, true);
            var storage = new LocalSaveStorage(path);
            var data = storage.Load();
            Assert.AreEqual(SaveLoadResult.Loaded, storage.LastLoadResult, "버전 3 세이브가 읽혀야 한다");
            Assert.IsNotNull(data);
            Assert.AreEqual(4, data.version, "읽으면서 버전 4로 옮긴다");
            Assert.IsTrue(data.meta.codex.backfillPending, "도감 채우기 예약");

            // ── 켜기 (GameBootstrap.Awake와 같은 순서) ──
            Game.Setup(data, content, new LocalTimeProvider(), new System.Random(7));
            Assert.IsTrue(Game.IsReady);
            Assert.IsTrue(Game.IsMetaActive, "메타가 켜진 채로 시작");
            Game.Mail.RemoveExpired();
            Game.Costumes.CheckNightVisit();
            Game.MetaSession.Start(); // NotImplementedException이 나면 그대로 실패 (부트스트랩의 안전장치가 메타를 끄는 경우)
            Assert.IsFalse(data.meta.playMinutesSeedPending, "Start가 설치 일수로 PlayMinutes 두 번째 단계를 끝낸다");
            long playMinutesAfterStart = Lifetime(data, CounterKey.PlayMinutes);

            // ── 5분 돌리기 (GameBootstrap.Update: 전투 Tick + 메타 Tick) ──
            const float dt = 0.1f;
            for (int i = 0; i < 3000; i++)
            {
                Game.Battle.Tick(dt);
                if (Game.IsMetaActive) Game.MetaSession.Tick(dt);
            }

            Assert.IsTrue(Game.IsMetaActive, "돌리는 동안 메타가 꺼지지 않는다");
            foreach (var module in Game.MetaSession.Modules)
                Assert.IsFalse(Game.MetaSession.IsModuleDisabled(module), $"4차 모듈이 예외로 꺼짐: {module.GetType().Name}");
            Assert.GreaterOrEqual(Lifetime(data, CounterKey.PlayMinutes), playMinutesAfterStart + 4, "켜 둔 5분이 PlayMinutes에 쌓인다");
            Assert.GreaterOrEqual(data.battle.highestStage, fixtureStage, "진행이 줄지 않는다");

            // ── 다시 저장 (GameBootstrap.Save) → 버전 4, 다시 읽어도 두 번 옮기지 않음 ──
            Game.Data.lastSaveUnix = TimeUtil.ToUnix(Game.Clock.UtcNow);
            storage.Save(Game.Data);
            long playMinutesSaved = Lifetime(data, CounterKey.PlayMinutes);
            int stageSaved = data.battle.highestStage;
            Game.Shutdown();

            var saved = JObject.Parse(File.ReadAllText(path));
            Assert.AreEqual(4, (int)saved["version"], "다시 쓴 세이브는 버전 4");
            Assert.IsNotNull(saved["meta"]?["codex"], "4차 칸이 저장된다");
            Assert.IsFalse((bool?)saved["meta"]?["playMinutesSeedPending"] ?? false);

            var reload = new LocalSaveStorage(path).Load();
            Assert.IsNotNull(reload);
            Assert.AreEqual(4, reload.version);
            Assert.AreEqual(playMinutesSaved, Lifetime(reload, CounterKey.PlayMinutes), "다시 읽어도 PlayMinutes를 또 넣지 않는다");
            Assert.AreEqual(stageSaved, reload.battle.highestStage);
        }
    }
}
