using System;
using System.IO;
using System.Linq;
using MageKnight.Adventure.Content;
using MageKnight.Adventure.Presentation;
using MK.Logic.Core;
using MK.Logic.Runtime.Adventure;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static MageKnight.Adventure.Presentation.AdventureWidgets;

namespace MageKnight.SceneAutomation.Editor
{
    /// <summary>首次建立示例内容。再次执行只补缺失资产，保留Inspector中的设计修改。</summary>
    public static class AdventureContentBuilder
    {
        private const string Root = "Assets/Resources/Adventure/";

        [MenuItem("Tools/Mage Knight/Adventure/Build Reusable Content")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出Play Mode。");
            foreach (string folder in new[] { "Actors", "Cards", "Locations", "Encounters", "Scenarios", "Prefabs" })
                Directory.CreateDirectory(Root + folder);
            AssetDatabase.Refresh();
            foreach (string path in Directory.GetFiles(Root + "Art", "*.png"))
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(path.Replace('\\', '/'));
                importer.textureType = TextureImporterType.Sprite;
                importer.spriteImportMode = SpriteImportMode.Single;
                importer.alphaIsTransparency = true;
                importer.mipmapEnabled = false;
                importer.maxTextureSize = 2048;
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
            }
            var hero = Actor("hero", "秘法剑士", 2, 0, 0, 0);
            var orc = Actor("orc", "荒野兽人", 4, 4, 0, 3);
            var wolf = Actor("wolf", "迅捷灰狼", 5, 3, 0, 4, abilities: new[] { Ability.Swift });
            var guard = Actor("guard", "边境守卫", 3, 3, 3, 0, 5, RecruitLocation.Village | RecruitLocation.Keep);
            var ranger = Actor("ranger", "林地游侠", 3, 4, 0, 0, 7);
            var monk = Actor("monk", "修道院学徒", 2, 0, 0, 0, 6, RecruitLocation.Monastery, new[] { Ability.Heal });
            var forest = Location("forest", "林间遗迹");
            var village = Location("village", "边境村庄");
            var valley = Location("valley", "翡翠山谷");
            var forestOrc = Encounter("forest_orc", forest, orc);
            var forestWolf = Encounter("forest_wolf", forest, wolf);
            var villageWolf = Encounter("village_wolf", village, wolf);
            var block = Card("basic_card_016");
            var strike = Card("basic_card_021");
            var march = Card("basic_card_000");
            var stamina = Card("basic_card_014");
            var promise = Card("basic_card_008");
            var combatDeck = Deck(block, block, strike, strike, march);
            var offers = new[] { guard, ranger, monk };
            var camp = Site("camp", "营地", 0, 0, TerrainType.Plains, valley);
            Scenario("01_combat", "combat", "林间交锋", AdventureMode.Combat, hero, forest, forestOrc,
                combatDeck, new[] { camp }, Array.Empty<ActorDefinition>(), "用格挡抵御兽人，再以攻击击破护甲。先选牌，再点敌人。");
            Scenario("02_exploration", "exploration", "穿越翡翠山谷", AdventureMode.Exploration, hero, valley, null,
                Deck(march, stamina, march, stamina, promise), new[] {
                    camp, Site("forest", "森林", 1, 0, TerrainType.Forest, forest),
                    Site("frontier", "平原", 2, 0, TerrainType.Plains, valley, false),
                    Site("desert", "沙漠", 1, -1, TerrainType.Desert, valley),
                    Site("lake", "湖泊", -1, 0, TerrainType.Lake, valley),
                    Site("mountain", "高山", 0, -1, TerrainType.Mountain, valley),
                    Site("plains", "草原", 0, 1, TerrainType.Plains, valley) }, Array.Empty<ActorDefinition>(),
                "打出移动牌，选择相邻地点；揭示迷雾和进入地形分别付费。");
            Scenario("03_recruitment", "recruitment", "村庄的盟友", AdventureMode.Recruitment, hero, village, null,
                Deck(promise, promise, march, block, strike), new[] { Site("village", "村庄", 0, 0, TerrainType.Plains, village, true, RecruitLocation.Village) }, offers,
                "选择伙伴，打出交涉牌积累影响力，再确认招募。部队还需要空闲指挥槽。", 4);
            Scenario("04_journey", "journey", "从营地到并肩作战", AdventureMode.Journey, hero, valley, villageWolf,
                Deck(march, promise, promise, strike, strike), new[] {
                    camp, Site("village", "村庄", 1, 0, TerrainType.Plains, village, false, RecruitLocation.Village) }, offers,
                "探索村庄 → 招募守卫 → 结束回合 → 与守卫一起抵挡迅捷灰狼。");
            Scenario("05_wolf", "wolf_encounter", "林间的另一场遭遇", AdventureMode.Combat, hero, forest, forestWolf,
                combatDeck, new[] { camp }, Array.Empty<ActorDefinition>(), "迅捷灰狼需要格挡6、攻击5。同一名英雄，在林间迎战新的敌人。");
            UseOfficialCards();
            BuildActorPrefab();
            AssetDatabase.SaveAssets();
            Debug.Log("[AdventureContent] 五份冒险已接回原卡资产与 ActionSystem，角色和地点配置保持原样。");
        }

        private static T Asset<T>(string path, Action<T> initialize) where T : ScriptableObject
        {
            var existing = AssetDatabase.LoadAssetAtPath<T>(Root + path + ".asset");
            if (existing != null) return existing;
            var asset = ScriptableObject.CreateInstance<T>(); initialize(asset);
            AssetDatabase.CreateAsset(asset, Root + path + ".asset"); return asset;
        }
        private static Sprite Art(string id) => AssetDatabase.LoadAssetAtPath<Sprite>(Root + "Art/" + id + "_v1.png");
        private static ActorDefinition Actor(string id, string label, int armor, int attack, int block, int fame,
            int cost = 0, RecruitLocation at = RecruitLocation.Village, Ability[] abilities = null)
            => Asset<ActorDefinition>("Actors/" + id, a => {
                a.id = id; a.displayName = label; a.artwork = Art(id); a.armor = armor; a.attack = attack; a.block = block;
                a.fame = fame; a.recruitmentCost = cost; a.recruitAt = at; a.abilities = abilities ?? Array.Empty<Ability>();
            });
        private static LocationDefinition Location(string id, string label) => Asset<LocationDefinition>("Locations/" + id,
            a => { a.id = id; a.displayName = label; a.background = Art(id); });
        private static EncounterDefinition Encounter(string id, LocationDefinition location, params ActorDefinition[] enemies)
            => Asset<EncounterDefinition>("Encounters/" + id, a => {
                a.id = id; a.displayName = location.displayName + " · " + enemies[0].displayName;
                a.location = location; a.enemies = enemies;
            });
        private static ActionCardDefinition Card(string id)
        {
            var binding = Asset<ActionCardDefinition>("Cards/" + id, _ => { });
            binding.source = AssetDatabase.LoadAssetAtPath<ActionCardSO>("Assets/GameData/CardsAssets/" + id + ".asset");
            binding.artwork = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameData/cards/" + id + ".png");
            if (binding.source == null || binding.artwork == null) throw new InvalidOperationException("缺少原卡或原卡面：" + id);
            EditorUtility.SetDirty(binding);
            return binding;
        }

        // 确定性教学牌组：五种既有基础牌各两张。不是英雄标准初始牌库。
        private static ActionCardDefinition[] Deck(params ActionCardDefinition[] opening)
        {
            var remaining = new[] { 16, 16, 21, 21, 0, 0, 14, 14, 8, 8 }
                .Select(i => Card("basic_card_" + i.ToString("000"))).ToList();
            foreach (var card in opening)
                if (!remaining.Remove(card)) throw new InvalidOperationException("教学牌数量超出配置：" + card.id);
            return opening.Concat(remaining).ToArray();
        }

        [MenuItem("Tools/Mage Knight/Adventure/Use Existing Mage Knight Cards")]
        public static void UseOfficialCards()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出 Play Mode。");
            var march = Card("basic_card_000"); var stamina = Card("basic_card_014");
            var promise = Card("basic_card_008"); var block = Card("basic_card_016"); var fury = Card("basic_card_021");
            foreach (string file in new[] { "01_combat", "02_exploration", "03_recruitment", "04_journey", "05_wolf" })
            {
                var asset = AssetDatabase.LoadAssetAtPath<AdventureDefinition>(Root + "Scenarios/" + file + ".asset");
                if (asset == null) throw new InvalidOperationException("缺少教学冒险：" + file);
                asset.deck = asset.mode switch
                {
                    AdventureMode.Exploration => Deck(march, stamina, march, stamina, promise),
                    AdventureMode.Recruitment => Deck(promise, promise, march, block, fury),
                    AdventureMode.Journey => Deck(march, promise, promise, fury, fury),
                    _ => Deck(block, block, fury, fury, march)
                };
                asset.influence = 0;
                asset.redCrystals = asset.blueCrystals = asset.greenCrystals = asset.whiteCrystals = 1;
                asset.objective = asset.mode switch
                {
                    AdventureMode.Exploration => "教学手牌：行进 / 耐力提供移动2，强化4；探索与进入分别付费。",
                    AdventureMode.Recruitment => "教学手牌：承诺提供影响2，白色强化4；凑足费用后招募。",
                    AdventureMode.Journey => "教学手牌：行进探索村庄 → 承诺招募 → 决心格挡 → 狂怒攻击。",
                    _ => "教学手牌：决心基础攻击或格挡2，蓝色强化格挡5；狂怒红色强化攻击4。"
                };
                EditorUtility.SetDirty(asset);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[OfficialCards] 已绑定五种原卡及完整卡面；十张确定性教学牌，不是英雄初始牌组。");
        }

        private static AdventureSite Site(string id, string name, int q, int r, TerrainType terrain, LocationDefinition location,
            bool revealed = true, RecruitLocation at = RecruitLocation.None)
            => new() { id = id, displayName = name, q = q, r = r, terrain = terrain, location = location, revealed = revealed, recruitAt = at };
        private static AdventureDefinition Scenario(string file, string id, string label, AdventureMode mode, ActorDefinition hero,
            LocationDefinition background, EncounterDefinition encounter, ActionCardDefinition[] deck, AdventureSite[] sites,
            ActorDefinition[] offers, string objective, int influence = 0) => Asset<AdventureDefinition>("Scenarios/" + file, a => {
                a.id = id; a.displayName = label; a.mode = mode; a.hero = hero; a.startingBackground = background;
                a.encounter = encounter; a.deck = deck; a.sites = sites; a.startSite = sites[0].id; a.offers = offers;
                a.objective = objective; a.influence = influence;
            });

        private static void BuildActorPrefab()
        {
            string path = Root + "Prefabs/ActorView.prefab";
            if (File.Exists(path)) return;
            var root = Rect(null, "ActorView", 0, 0, 330, 455);
            try
            {
                var image = root.gameObject.AddComponent<Image>(); image.color = Color.clear;
                var view = root.gameObject.AddComponent<AdventureActorView>();
                view.button = root.gameObject.AddComponent<Button>(); view.button.targetGraphic = image;
                view.portrait = Sprite(root, "Img_Actor", null, 0, 0, 330, 388);
                var plate = Panel(root, "CaptionPlate", 5, 390, 320, 65, new Color32(8, 25, 24, 230), true);
                view.selection = Panel(root, "Selection", 5, 388, 320, 4, Color.clear).GetComponent<Image>();
                view.caption = Text(plate, "Txt_Name", "", 8, 3, 304, 29, 21, Cream, TextAlignmentOptions.Center);
                view.stats = Text(plate, "Txt_Stats", "", 5, 36, 310, 26, 16, Muted, TextAlignmentOptions.Center);
                PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            }
            finally { UnityEngine.Object.DestroyImmediate(root.gameObject); }
        }
    }
}
