using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace IdleGame.EditorTools
{
    /// <summary>
    /// 콘텐츠 데이터 파이프라인. 코드(Editor/Content/*.cs)가 원본이다.
    /// 실행할 때마다 각 에셋을 불러오고(없으면 만들고) 내용을 코드 값으로 덮어쓴다.
    /// 에셋 파일과 GUID는 그대로 두므로 씬/프리팹 참조는 유지된다.
    ///   · 아이템 / 캐릭터(+ 특성 · 고유 스킬) / 가챠 / 시너지: Content/*Content.cs의 데이터 표
    ///   · 전투 / 원정 / 상점: 각 데이터 클래스의 ApplyDefaults()
    ///   · GameConfig: 운영 수치라 처음 만들 때만 기본값을 쓰고, 이후엔 인스펙터에서 바꾼 값을 유지
    /// 스프라이트는 ArtLib으로 연결하고(파일이 없으면 null), 마지막에 Validate로 정합성을 검사한다.
    /// 배치 모드: -executeMethod IdleGame.EditorTools.IdleDataBuilder.RebuildContent
    /// </summary>
    public static class IdleDataBuilder
    {
        const string DataDir = IdleProjectSetup.Root + "/Data";
        const string ConfigPath = DataDir + "/GameConfig.asset";
        const string ItemsPath = DataDir + "/ItemDatabase.asset";
        const string CharactersPath = DataDir + "/CharacterDatabase.asset";
        /// <summary>예전 "장비 소환" 시절 파일 이름을 그대로 써서 GUID를 유지한다. 내용은 "잃어버린 친구 찾기".</summary>
        const string GachaPath = DataDir + "/GachaTable_Normal.asset";
        const string BattlePath = DataDir + "/BattleConfig.asset";
        const string ExpeditionsPath = DataDir + "/ExpeditionDatabase.asset";
        const string ShopsPath = DataDir + "/ShopDatabase.asset";
        const string SynergiesPath = DataDir + "/SynergyDatabase.asset";

        /// <summary>모든 콘텐츠 에셋을 코드 기준으로 갱신해 묶어 돌려준다. 저장(AssetDatabase.SaveAssets)은 호출한 쪽에서 한다.</summary>
        public static GameContent BuildContent()
        {
            var characters = EnsureCharacterDatabase();
            var content = new GameContent
            {
                Config = EnsureConfig(),
                Items = EnsureItemDatabase(),
                Characters = characters,
                GachaTables = new[] { EnsureGachaTable() },
                Battle = EnsureBattleConfig(),
                Expeditions = EnsureExpeditionDatabase(),
                Shops = EnsureShopDatabase(),
                Synergies = EnsureSynergyDatabase(characters),
            };
            Validate(content);
            // 소리 목록 (AUD-01) — 실패해도 콘텐츠 · 장면 만들기는 계속한다 (소리는 없으면 조용히 지나간다)
            try
            {
                AudioDataBuilder.Build();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
            return content;
        }

        [MenuItem("IdleGame/Setup/콘텐츠 데이터 갱신", priority = 20)]
        public static void RebuildContent()
        {
            BuildContent();
            AssetDatabase.SaveAssets();
            Debug.Log("[IdleGame] 콘텐츠 데이터 갱신 완료: " + DataDir);
        }

        [MenuItem("IdleGame/Setup/콘텐츠 검증", priority = 21)]
        public static void ValidateSavedContent() => Validate(LoadContent());

        /// <summary>
        /// 콘텐츠 정합성 검사: ID 중복, 진화 트리(부모/기본형), 레시피 재료, 재료 획득처(원정 구역/던전 층/상점),
        /// 상점 상품의 아이템·의상, 의상 해금 대상, 가챠 등급별 계보, 시작 지급 계보, 계보 특성,
        /// 형태 고유 스킬(효과 수치/연출 키), 시너지(ID/단계/조합 계보/보너스 스킬), 적 기믹, 아트 누락.
        /// 오류는 Debug.LogError, 경고(아트 누락 등)는 Debug.LogWarning으로 남긴다.
        /// </summary>
        /// <returns>오류 목록. 비어 있으면 통과 (경고는 포함하지 않는다)</returns>
        public static List<string> Validate(GameContent content)
        {
            var report = ContentValidator.Check(content);
            ContentValidator.Log(report);
            return report.Errors;
        }

        // ───────────────────────── 에셋별 갱신 ─────────────────────────

        public static GameConfig EnsureConfig() => LoadOrCreate<GameConfig>(ConfigPath);

        public static ItemDatabase EnsureItemDatabase() => Refresh<ItemDatabase>(ItemsPath, db =>
        {
            db.items = ItemContent.Build();
            foreach (var item in db.items) item.icon = ArtLib.Item(item.id);
        });

        public static CharacterDatabase EnsureCharacterDatabase() => Refresh<CharacterDatabase>(CharactersPath, db =>
        {
            db.lineages = CharacterContent.Build();
            foreach (var lineage in db.lineages)
            {
                foreach (var form in lineage.forms) form.portrait = ArtLib.Character(form.id);
                foreach (var costume in lineage.costumes)
                {
                    costume.portrait = ArtLib.Costume(costume.id);
                    costume.illustration = ArtLib.Concept(costume.id);
                }
            }
        });

        public static GachaTable EnsureGachaTable() => Refresh<GachaTable>(GachaPath, GachaContent.ApplyFriends);

        public static BattleConfig EnsureBattleConfig() => Refresh<BattleConfig>(BattlePath, config =>
        {
            config.ApplyDefaults();
            if (config.enemies == null) return;
            foreach (var enemy in config.enemies)
            {
                if (enemy != null) enemy.sprite = ArtLib.Enemy(enemy.id);
            }
        });

        public static ExpeditionDatabase EnsureExpeditionDatabase() => Refresh<ExpeditionDatabase>(ExpeditionsPath, db => db.ApplyDefaults());

        public static ShopDatabase EnsureShopDatabase() => Refresh<ShopDatabase>(ShopsPath, db => db.ApplyDefaults());

        /// <summary>
        /// 특성 7 / 역할 6 / 조합 12 시너지. 조합 조건 문장에 계보 이름을 쓰므로 캐릭터 DB를 받는다
        /// (없으면 캐릭터 콘텐츠 표에서 이름을 가져온다).
        /// </summary>
        public static SynergyDatabase EnsureSynergyDatabase(CharacterDatabase characters = null) => Refresh<SynergyDatabase>(SynergiesPath, db =>
        {
            db.synergies = SynergyContent.Build(characters != null ? characters.lineages : CharacterContent.Build());
            foreach (var synergy in db.synergies) synergy.icon = SynergyIcon(synergy);
        });

        /// <summary>특성/역할 시너지는 공용 특성·역할 아이콘, 조합은 Art/Synergy/{id}.png</summary>
        static Sprite SynergyIcon(SynergyDef synergy) => synergy.kind switch
        {
            SynergyKind.Trait => ArtLib.Trait(synergy.trait),
            SynergyKind.Style => ArtLib.Style(synergy.style),
            _ => ArtLib.Synergy(synergy.id),
        };

        // ───────────────────────── 공통 ─────────────────────────

        /// <summary>저장된 에셋을 수정하지 않고 그대로 불러온다 (검증 메뉴용). 없는 에셋은 null.</summary>
        static GameContent LoadContent()
        {
            var gacha = AssetDatabase.LoadAssetAtPath<GachaTable>(GachaPath);
            return new GameContent
            {
                Config = AssetDatabase.LoadAssetAtPath<GameConfig>(ConfigPath),
                Items = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemsPath),
                Characters = AssetDatabase.LoadAssetAtPath<CharacterDatabase>(CharactersPath),
                GachaTables = gacha != null ? new[] { gacha } : Array.Empty<GachaTable>(),
                Battle = AssetDatabase.LoadAssetAtPath<BattleConfig>(BattlePath),
                Expeditions = AssetDatabase.LoadAssetAtPath<ExpeditionDatabase>(ExpeditionsPath),
                Shops = AssetDatabase.LoadAssetAtPath<ShopDatabase>(ShopsPath),
                Synergies = AssetDatabase.LoadAssetAtPath<SynergyDatabase>(SynergiesPath),
            };
        }

        /// <summary>에셋을 불러오거나 만든 뒤 내용을 덮어쓴다 (파일/GUID 유지).</summary>
        static T Refresh<T>(string path, Action<T> fill) where T : ScriptableObject
        {
            var asset = LoadOrCreate<T>(path);
            fill(asset);
            ClearLookupCache(asset);
            EditorUtility.SetDirty(asset);
            return asset;
        }

        static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            var asset = AssetDatabase.LoadAssetAtPath<T>(path);
            if (asset != null) return asset;

            asset = ScriptableObject.CreateInstance<T>();
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        /// <summary>
        /// 데이터 클래스의 ID 조회 캐시는 OnValidate에서 비워진다. 같은 에디터 세션에서 이미 조회된 에셋을 덮어써도
        /// 새 내용이 바로 보이도록, 인스펙터 수정과 같은 경로(OnValidate)를 불러 준다.
        /// </summary>
        static void ClearLookupCache(ScriptableObject asset) =>
            asset.GetType()
                .GetMethod("OnValidate", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)
                ?.Invoke(asset, null);
    }
}
