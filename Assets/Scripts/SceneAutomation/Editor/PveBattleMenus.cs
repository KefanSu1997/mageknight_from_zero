using System;
using System.IO;
using System.Linq;
using MageKnight.Adventure.Content;
using MageKnight.Battle;
using MK.Logic.Core;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace MageKnight.SceneAutomation.Editor
{
    public static class PveBattleMenus
    {
        private const string Root = "Assets/GameData/BattleDemo/";
        public const string ScenePath = "Assets/Scenes/Part1/Part1_PveBattle.unity";

        // Keep all original token art available when formal summoning replaces the encounter enemies.
        private static bool BindOriginalEnemies(PveBattleController controller, JArray source)
        {
            var views = Enemies(source, source.Select(row => (string)row["id"]).ToArray());
            if (views.Any(view => view.originalToken == null))
                throw new InvalidOperationException("原敌人目录缺少素材，不能保存不完整场景");
            bool same = controller.worldEnemyCatalog.Length == views.Length &&
                controller.worldEnemyCatalog.Zip(views, (old, next) => old.originalId == next.originalId &&
                    old.displayName == next.displayName && old.originalToken == next.originalToken).All(equal => equal);
            if (!same) controller.worldEnemyCatalog = views;
            return !same;
        }

        public static void SyncOriginalEnemyCatalog()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出演练");
            var scene = SceneManager.GetSceneByPath(ScenePath);
            if (!scene.IsValid() || !scene.isLoaded) throw new InvalidOperationException("请先打开战斗Demo场景");
            var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PveBattleController>()).Single();
            if (!BindOriginalEnemies(controller, JArray.Parse(File.ReadAllText("resources/text_json/monster.json")))) return;
            EditorUtility.SetDirty(controller);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
        }

        [MenuItem("Tools/Mage Knight/Battle/Build or Locate Demo")]
        public static void Build()
        {
            if (EditorApplication.isPlaying) throw new InvalidOperationException("请先退出演练");
            Directory.CreateDirectory(Root + "Cards");
            string json = File.ReadAllText("resources/text_json/monster.json");
            string dataPath = Root + "original_monsters.json";
            if (!File.Exists(dataPath) || File.ReadAllText(dataPath) != json)
            {
                File.WriteAllText(dataPath, json);
                AssetDatabase.ImportAsset(dataPath, ImportAssetOptions.ForceSynchronousImport);
            }
            var data = AssetDatabase.LoadAssetAtPath<TextAsset>(dataPath);
            var source = JArray.Parse(json);
            var a = Asset<BattleLoadout>("Guard", value =>
            {
                value.displayName = "稳守反击"; value.openingHand = Cards(16, 21, 0, 14, 8);
                value.heroArtwork = Art(); value.redCrystals = 1; value.blueCrystals = 1;
            });
            var b = Asset<BattleLoadout>("Ranged", value =>
            {
                value.displayName = "先发制人"; value.openingHand = Cards(7, 11, 21, 16, 0);
                value.heroArtwork = Art(); value.redCrystals = 1; value.blueCrystals = 2; value.whiteCrystals = 1;
            });
            var companions = Asset<BattleLoadout>("Companions", value =>
            {
                value.displayName = "魔像协防"; value.openingHand = Cards(16, 21, 0);
                value.heroArtwork = Art(); value.redCrystals = 1; value.blueCrystals = 1;
                value.units = new[] { Unit("army_2_001") };
            });
            var fireSupport = Asset<BattleLoadout>("FireSupport", value =>
            {
                value.displayName = "烈焰援军"; value.openingHand = Cards(16, 21, 0);
                value.heroArtwork = Art(); value.redCrystals = 1; value.blueCrystals = 1;
                value.units = new[] { Unit("army_003") };
            });
            var honor = Asset<BattleLoadout>("HonorGuard", value =>
            {
                value.displayName = "荣誉护卫"; value.openingHand = Cards(16, 21, 0);
                value.heroArtwork = Art(); value.redCrystals = 1; value.blueCrystals = 1;
                value.units = new[] { Unit("army_2_001") }; value.artifacts = new[] { Artifact("items_000") };
            });
            var sword = Asset<BattleLoadout>("JusticeSword", value =>
            {
                value.displayName = "公正之剑"; value.openingHand = Cards(16, 21, 0);
                value.heroArtwork = Art(); value.redCrystals = 1; value.blueCrystals = 1;
                value.artifacts = new[] { Artifact("items_008") };
            });
            var aryth = Asset<BattleLoadout>("ArytheaSword", value =>
            {
                value.displayName = "阿瑞希亚·炽热剑术"; value.openingHand = Cards(16, 21, 0);
                value.hero = Hero("Arythea", "阿瑞希亚", "hero_skill_pool_000", "skill_000");
                value.startingFame = 3; value.learnedSkills = new[] { 2 };
                value.heroArtwork = Art();
            });
            var tovak = Asset<BattleLoadout>("TovakSword", value =>
            {
                value.displayName = "托瓦克·寒冰剑术"; value.openingHand = Cards(16, 21, 0);
                value.hero = Hero("Tovak", "托瓦克", "hero_skill_pool_007", "skill_021");
                value.startingFame = 3; value.learnedSkills = new[] { 2 };
                value.heroArtwork = Art();
            });
            var patrol = Asset<BattleEncounter>("Patrol", value =>
            {
                value.displayName = "遗迹哨卫"; value.originalMonsterJson = data;
                value.background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Adventure/Art/forest_v1.png");
                value.enemies = Enemies(source, "monster_032");
            });
            var warband = Asset<BattleEncounter>("Warband", value =>
            {
                value.displayName = "兽人战团"; value.originalMonsterJson = data;
                value.background = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Resources/Adventure/Art/valley_v1.png");
                value.enemies = Enemies(source, "monster_006", "monster_007");
            });
            var sentries = Asset<BattleEncounter>("Sentries", value =>
            {
                value.displayName = "双重哨戒"; value.originalMonsterJson = data;
                value.background = patrol.background; value.enemies = Enemies(source, "monster_032", "monster_032");
            });
            var poison = Asset<BattleEncounter>("PoisonPatrol", value =>
            {
                value.displayName = "毒素兽人"; value.originalMonsterJson = data;
                value.background = warband.background; value.enemies = Enemies(source, "monster_006");
            });
            var utemGuard = Asset<BattleLoadout>("UtemGuard", value =>
            {
                value.displayName = "乌顿守卫"; value.openingHand = Cards(16, 21, 0);
                value.heroArtwork = Art(); value.units = new[] { Unit("army_2_013") };
            });
            var utemSword = Asset<BattleLoadout>("UtemSwordsmen", value =>
            {
                value.displayName = "乌顿剑士"; value.openingHand = Cards(16, 21, 0);
                value.heroArtwork = Art(); value.units = new[] { Unit("army_2_014") };
            });
            var swift = Asset<BattleEncounter>("SwiftPatrol", value =>
            {
                value.displayName = "迅捷突袭"; value.originalMonsterJson = data;
                value.background = patrol.background; value.enemies = Enemies(source, "monster_007");
            });
            var sniper = TovakLoadout("TovakSniper", "托瓦克·黑夜狙击", 1);
            var shield = TovakLoadout("TovakShield", "托瓦克·盾牌精通", 3);
            var weaken = TovakLoadout("TovakWeaken", "托瓦克·抗性弱化", 4);
            var northern = MonkLoadout("NorthernMonks", "北地僧侣", "army_2_005");
            var redMonks = MonkLoadout("RedCapeMonks", "赤袍僧侣", "army_2_007");
            var savageMonks = Asset<BattleLoadout>("SavageMonks", value =>
            {
                value.displayName = "凶蛮僧侣"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.greenCrystals = 1; value.units = new[] { Unit("army_2_008") };
            });
            var crossbow = Asset<BattleLoadout>("UtemCrossbow", value =>
            {
                value.displayName = "乌顿弩手"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.units = new[] { Unit("army_2_012") };
            });
            var golemArsenal = Asset<BattleLoadout>("GolemArsenal", value =>
            {
                value.displayName = "魔像元素守御"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.redCrystals = 2; value.units = new[] { Unit("army_2_001") };
            });
            var necroShield = Asset<BattleLoadout>("NecroShield", value =>
            {
                value.displayName = "亡君之盾"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.artifacts = new[] { Artifact("items_020") };
            });
            var harvester = Asset<BattleLoadout>("SoulHarvester", value =>
            {
                value.displayName = "灵魂收割者"; value.openingHand = Cards(16, 16, 21, 21, 0, 11); value.heroArtwork = Art();
                value.redCrystals = 2; value.blueCrystals = 2; value.artifacts = new[] { Artifact("items_019") };
            });
            var fear = Asset<BattleLoadout>("FearBanner", value =>
            {
                value.displayName = "恐惧旌旗"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.units = new[] { Unit("army_2_001") }; value.artifacts = new[] { Artifact("items_001") };
            });
            var night = Asset<BattleEncounter>("NightPatrol", value =>
            {
                value.displayName = "夜间哨卫"; value.originalMonsterJson = data; value.background = patrol.background;
                value.enemies = Enemies(source, "monster_032"); value.dayPart = MK.Logic.Core.DayPart.Night;
            });
            var fire = Asset<BattleEncounter>("FireSkeleton", value =>
            {
                value.displayName = "火焰骷髅"; value.originalMonsterJson = data; value.background = warband.background;
                value.enemies = Enemies(source, "monster_029");
            });
            var ice = Asset<BattleEncounter>("IceLich", value =>
            {
                value.displayName = "寒冰巫妖"; value.originalMonsterJson = data; value.background = warband.background;
                value.enemies = Enemies(source, "monster_004");
            });
            var witch = Asset<BattleEncounter>("Witch", value =>
            {
                value.displayName = "幽魂女巫"; value.originalMonsterJson = data; value.background = patrol.background;
                value.enemies = Enemies(source, "monster_015");
            });
            var fearPatrol = Asset<BattleEncounter>("FearPatrol", value =>
            {
                value.displayName = "女巫与双哨卫"; value.originalMonsterJson = data; value.background = patrol.background;
                value.enemies = Enemies(source, "monster_015", "monster_032", "monster_032");
            });
            var eagle = Asset<BattleEncounter>("FortifiedEagle", value =>
            {
                value.displayName = "巨鹰城防"; value.originalMonsterJson = data; value.background = patrol.background;
                value.enemies = Enemies(source, "monster_017");
            });
            var witchSentry = Asset<BattleEncounter>("WitchSentry", value =>
            {
                value.displayName = "女巫与哨卫"; value.originalMonsterJson = data; value.background = patrol.background;
                value.enemies = Enemies(source, "monster_015", "monster_032");
            });
            var elementalGuard = Asset<BattleEncounter>("ElementalGuard", value =>
            {
                value.displayName = "双元素守卫"; value.originalMonsterJson = data; value.background = warband.background;
                value.enemies = Enemies(source, "monster_029", "monster_004");
            });
            var courage = Asset<BattleLoadout>("CourageBanner", value =>
            {
                value.displayName = "勇气旌旗·乌顿剑士"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.startingFame = 8; value.redCrystals = 2; value.blueCrystals = 2;
                value.units = new[] { Unit("army_2_014"), Unit("army_2_014") }; value.artifacts = new[] { Artifact("items_003") };
            });
            var fortitude = Asset<BattleLoadout>("FortitudeBanner", value =>
            {
                value.displayName = "强韧旌旗·乌顿剑士"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.startingFame = 8; value.redCrystals = 2; value.blueCrystals = 2;
                value.units = new[] { Unit("army_2_014"), Unit("army_2_014") }; value.artifacts = new[] { Artifact("items_018") };
            });
            var iceSwords = MonkLoadout("IceSwordsmen", "埃莫提普冰霜剑士", "army_002");
            var catapults = MonkLoadout("Catapults", "投石车", "army_004");
            var fireGolems = MonkLoadout("FireGolems", "火焰魔像", "army_006");
            var iceGolems = MonkLoadout("IceGolems", "寒冰魔像", "army_012");
            var arythSiege = SiegeSkillLoadout("ArytheaSiege", "阿瑞希亚·燃烧之力", "Arythea", "阿瑞希亚", "hero_skill_pool_000", "skill_000", 1);
            var goldyxSiege = SiegeSkillLoadout("GoldyxSiege", "高蒂克斯·冰封之力", "Goldyx", "高蒂克斯", "hero_skill_pool_005", "skill_017", 0);
            var norowasSniper = Asset<BattleLoadout>("NorowasSniper", value =>
            {
                value.displayName = "诺洛瓦斯·白昼狙击"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.hero = Hero("Norowas", "诺洛瓦斯", "hero_skill_pool_002", "skill_007", 1);
                value.startingFame = 3; value.learnedSkills = new[] { 1 };
            });
            var whiteForge = ForgeLoadout("WhiteForge", "白晶锻造", 2);
            var greenForge = ForgeLoadout("GreenForge", "绿晶锻造", 3);
            var redForge = ForgeLoadout("RedForge", "红晶锻造", 4);
            var cappedForge = ForgeLoadout("CappedForge", "红晶锻造·蓝晶已满", 4, 3);
            var fireMages = MageLoadout("FireMages", "火焰法师", "army_007", 2, 2);
            var iceMages = MageLoadout("IceMages", "寒冰法师", "army_013", 2, 2);
            var fireMana = MageLoadout("FireMageMana", "火焰法师·魔力补给", "army_007", 0, 1);
            var iceMana = MageLoadout("IceMageMana", "寒冰法师·魔力补给", "army_013", 1, 0);
            var wrathHorn = HornLoadout("WrathHorn", "愤怒号角", false, 0);
            var hornGold = HornLoadout("HornGold", "愤怒号角·金色演练", true, 0);
            var hornBlack = HornLoadout("HornBlack", "愤怒号角·黑色演练", true, 6);
            var hornMixed = HornLoadout("HornMixed", "愤怒号角·多骰演练", true, 2);
            var herbalists = Asset<BattleLoadout>("Herbalists", value =>
            {
                value.displayName = "药草师·战后恢复"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.startingFame = 8; value.greenCrystals = 1;
                value.units = new[] { Unit("army_2_002"), Unit("army_2_014") };
            });
            var herbalistMana = Asset<BattleLoadout>("HerbalistMana", value =>
            {
                value.displayName = "药草师·绿色补给"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.startingFame = 8; value.greenCrystals = 0;
                value.units = new[] { Unit("army_2_002"), Unit("army_2_008") };
            });
            var grail = Asset<BattleLoadout>("GoldenGrail", value =>
            {
                value.displayName = "黄金圣杯·战后治疗"; value.openingHand = Cards(16, 21, 0, 11); value.drawPile = Cards(0, 11);
                value.heroArtwork = Art(); value.units = new[] { Unit("army_2_014") };
                value.artifacts = new[] { Artifact("items_012") };
            });
            var grailHerbs = Asset<BattleLoadout>("GrailHerbalists", value =>
            {
                value.displayName = "黄金圣杯·药草师"; value.openingHand = Cards(16, 21, 0, 11); value.drawPile = Cards(0, 11);
                value.heroArtwork = Art(); value.startingFame = 8; value.greenCrystals = 1;
                value.units = new[] { Unit("army_2_002"), Unit("army_2_014") }; value.artifacts = new[] { Artifact("items_012") };
            });
            var refreshBath = HealingSkillLoadout("RefreshBath", "提神沐浴", false, 0);
            var refreshBreeze = HealingSkillLoadout("RefreshBreeze", "提神微风", false, 1);
            var potionCraft = HealingSkillLoadout("PotionCraft", "药剂制作", true, 1);
            var altemGuards = Asset<BattleLoadout>("AltemGuardians", value =>
            {
                value.displayName = "阿尔蒂姆守护者·全体抗性"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.startingFame = 8; value.greenCrystals = 1;
                value.units = new[] { Unit("army_000"), Unit("army_2_014") };
            });
            var altemMages = Asset<BattleLoadout>("AltemMages", value =>
            {
                value.displayName = "阿尔蒂姆法师·冰火与转换"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.startingFame = 8; value.redCrystals = 2; value.blueCrystals = 2; value.blackTokens = 1;
                value.units = new[] { Unit("army_001"), Unit("army_2_014") };
            });
            var altemSupply = Asset<BattleLoadout>("AltemSupply", value =>
            {
                value.displayName = "阿尔蒂姆法师·自选魔力"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
                value.startingFame = 8; value.redCrystals = 0; value.blueCrystals = 0;
                value.units = new[] { Unit("army_001"), Unit("army_001") };
            });
            var starBowRanged = Asset<BattleLoadout>("StarBowRanged", value =>
            {
                value.displayName = "星辰之弓·远程选择"; value.openingHand = Cards(16, 21, 0, 11, 7); value.heroArtwork = Art();
                value.startingFame = 3; value.redCrystals = 2; value.blueCrystals = 1; value.whiteCrystals = 1;
                value.units = new[] { Unit("army_003") }; value.artifacts = new[] { Artifact("items_016") };
            });
            var starBowSiege = Asset<BattleLoadout>("StarBowSiege", value =>
            {
                value.displayName = "星辰之弓·攻城选择"; value.openingHand = Cards(16, 21, 0, 11, 7); value.heroArtwork = Art();
                value.startingFame = 3; value.redCrystals = 1; value.blueCrystals = 1; value.whiteCrystals = 1;
                value.units = new[] { Unit("army_004") }; value.artifacts = new[] { Artifact("items_016") };
            });
            var darkFireRed = ManaSkillLoadout("DarkFireRed", "暗火魔法·红色魔力", true, 4, "army_003");
            var darkFireBlack = ManaSkillLoadout("DarkFireBlack", "暗火魔法·黑色魔力", true, 4, "army_001");
            var leavesWind = ManaSkillLoadout("LeavesWind", "风中残叶", false, 4, "army_2_008");
            var treetopWhisper = ManaSkillLoadout("TreetopWhisper", "树梢风语", false, 5, "army_2_008");
            var peasants = CommonerLoadout("Peasants", "农民", "army_2_006");
            var thugs = CommonerLoadout("Thugs", "暴徒", "army_2_011");
            var commoners = CommonerLoadout("Commoners", "农民与暴徒", "army_2_006", "army_2_011");
            var unitCatalog = MK.Logic.Data.UnitJsonLoader.SupportedIds.Select(Unit).ToArray();
            var tactics = MK.Logic.Data.OriginalTactics.All.Select(TacticArtwork).ToArray();
            var skillReferences=PveBattleController.SkillReferenceIndices.Select(i=>AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameData/cards/skill_"+i.ToString("000")+".png")).ToArray();
            var deeds = AssetDatabase.FindAssets("t:CardSO",new[]{"Assets/GameData/CardsAssets"})
                .Select(g=>AssetDatabase.LoadAssetAtPath<CardSO>(AssetDatabase.GUIDToAssetPath(g)))
                .Where(c=>c is ActionCardSO || c is SpellCardSO || c is ItemCardSO)
                // Original source audit: three empty image slots and one deck back are not playable faces.
                .Where(c=>c.Id is not ("advanced_card_044" or "advanced_card_045" or "advanced_card_046" or "advanced_card_047"))
                .OrderBy(c=>c.Id).Select(Deed).ToArray();
            var artifacts = deeds.Where(d=>d.source is ItemCardSO).Select(d=>Artifact(d.id)).ToArray();
            var travelRoute=Asset<AdventureDefinition>("JourneyRoute",route=>
            {
                var source=AssetDatabase.LoadAssetAtPath<AdventureDefinition>("Assets/Resources/Adventure/Scenarios/02_exploration.asset");
                EditorJsonUtility.FromJsonOverwrite(EditorJsonUtility.ToJson(source),route);
                route.id="pve_journey";route.displayName="连续旅程";
                var village=route.sites.Single(s=>s.id=="plains");
                village.displayName="村庄";village.recruitAt=RecruitLocation.Village;
                village.location=AssetDatabase.LoadAssetAtPath<LocationDefinition>("Assets/Resources/Adventure/Locations/village.asset");
            });
            if (!File.Exists(ScenePath))
            {
                var previous = SceneManager.GetActiveScene();
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
                SceneManager.SetActiveScene(scene);
                var camera = new GameObject("Main Camera", typeof(Camera)).GetComponent<Camera>();
                camera.clearFlags = CameraClearFlags.SolidColor; camera.backgroundColor = Color.black;
                var controller = new GameObject("PveBattle").AddComponent<PveBattleController>();
                BindOriginalEnemies(controller, source);
                controller.unitCatalog = unitCatalog; controller.tacticArtwork = tactics; controller.deedCatalog=deeds; controller.artifactCatalog=artifacts; controller.travelRoute=travelRoute;
                controller.skillReferencePages=skillReferences;
                controller.loadouts = new[] { a, b, companions, fireSupport, honor, sword, aryth, tovak, utemGuard, utemSword, sniper, shield, weaken, northern, redMonks, fear, savageMonks, crossbow, golemArsenal, necroShield, harvester, catapults, fireGolems, iceGolems, arythSiege, goldyxSiege, norowasSniper, iceSwords, fortitude, courage, whiteForge, greenForge, redForge, cappedForge, fireMages, iceMages, fireMana, iceMana, wrathHorn, hornGold, hornBlack, hornMixed, herbalists, herbalistMana, grail, grailHerbs, refreshBath, refreshBreeze, potionCraft, altemGuards, altemMages, altemSupply, starBowRanged, starBowSiege, darkFireRed, darkFireBlack, leavesWind, treetopWhisper, peasants, thugs, commoners }; controller.encounters = new[] { patrol, warband, sentries, poison, swift, night, fire, ice, witch, fearPatrol, eagle, witchSentry, elementalGuard };
                EditorSceneManager.SaveScene(scene, ScenePath); EditorSceneManager.CloseScene(scene, true);
                if (previous.IsValid()) SceneManager.SetActiveScene(previous);
            }
            else
            {
                var scene = SceneManager.GetSceneByPath(ScenePath);
                bool alreadyOpen = scene.IsValid() && scene.isLoaded;
                if (!alreadyOpen) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Additive);
                var controller = scene.GetRootGameObjects().SelectMany(g => g.GetComponentsInChildren<PveBattleController>()).Single();
                bool changed = BindOriginalEnemies(controller, source);
                if(controller.skillReferencePages==null || !controller.skillReferencePages.SequenceEqual(skillReferences))
                {controller.skillReferencePages=skillReferences;changed=true;}
                if(controller.deedCatalog==null || !controller.deedCatalog.SequenceEqual(deeds))
                {controller.deedCatalog=deeds;changed=true;}
                if(controller.travelRoute!=travelRoute){controller.travelRoute=travelRoute;changed=true;}
                if(controller.artifactCatalog==null || !controller.artifactCatalog.SequenceEqual(artifacts))
                {controller.artifactCatalog=artifacts;changed=true;}
                if (controller.tacticArtwork == null || !controller.tacticArtwork.SequenceEqual(tactics))
                { controller.tacticArtwork = tactics; changed = true; }
                if (controller.unitCatalog == null || !controller.unitCatalog.SequenceEqual(unitCatalog))
                { controller.unitCatalog = unitCatalog; changed = true; }
                foreach (var value in new[] { companions, fireSupport, honor, sword, aryth, tovak, utemGuard, utemSword, sniper, shield, weaken, northern, redMonks, fear, savageMonks, crossbow, golemArsenal, necroShield, harvester, catapults, fireGolems, iceGolems, arythSiege, goldyxSiege, norowasSniper, iceSwords, fortitude, courage, whiteForge, greenForge, redForge, cappedForge, fireMages, iceMages, fireMana, iceMana, wrathHorn, hornGold, hornBlack, hornMixed, herbalists, herbalistMana, grail, grailHerbs, refreshBath, refreshBreeze, potionCraft, altemGuards, altemMages, altemSupply, starBowRanged, starBowSiege, darkFireRed, darkFireBlack, leavesWind, treetopWhisper, peasants, thugs, commoners })
                    if (!controller.loadouts.Contains(value)) { controller.loadouts = controller.loadouts.Append(value).ToArray(); changed = true; }
                foreach (var value in new[] { sentries, poison, swift, night, fire, ice, witch, fearPatrol, eagle, witchSentry, elementalGuard })
                    if (!controller.encounters.Contains(value)) { controller.encounters = controller.encounters.Append(value).ToArray(); changed = true; }
                if (changed) { EditorUtility.SetDirty(controller); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene); }
                if (!alreadyOpen) EditorSceneManager.CloseScene(scene, true);
            }
            AssetDatabase.SaveAssets();
            Debug.Log("[PveBattle] 场景与独立出战配置已就绪：" + ScenePath);
        }

        [MenuItem("Tools/Mage Knight/Battle/Open Demo")]
        public static void Open()
        {
            Build(); GameViewResolutionUtility.TrySetFixedResolution(1920, 1080, "Battle_1920x1080");
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        [MenuItem("Tools/Mage Knight/Battle/Run Acceptance")]
        public static void RunAcceptance()
        {
            Build(); GameViewResolutionUtility.TrySetFixedResolution(1920, 1080, "Battle_1920x1080");
            string directory = "AutomationOutputs/PveBattle/" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + "_" + Guid.NewGuid().ToString("N").Substring(0, 8);
            Directory.CreateDirectory(directory);
            var request = JsonUtility.FromJson<SceneAutomationRequest>(File.ReadAllText("AutomationConfigs/pve_battle.json"));
            request.reportPath = directory + "/report.json"; request.screenshotsDirectory = directory + "/captures";
            string config = directory + "/request.json"; File.WriteAllText(config, JsonUtility.ToJson(request, true));
            Debug.Log("[PveBattle] Acceptance: " + directory);
            SceneAutomationCommand.RunFromProjectRelativeConfig(config);
        }

        private static BattleDeedDefinition Deed(CardSO source)
        {
            Directory.CreateDirectory(Root+"Deeds");
            return Asset<BattleDeedDefinition>("Deeds/"+source.Id,binding=>
            {
                binding.source=source;
                binding.artwork=AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameData/cards/"+source.Id+".png");
                binding.Validate();
            });
        }

        private static Texture2D TacticArtwork(MK.Logic.Data.TacticDefinition tactic)
        {
            string path = Root + "Tactics/" + tactic.Card.Id + ".jpg";
            Directory.CreateDirectory(Root + "Tactics");
            if (!File.Exists(path))
            {
                File.Copy(tactic.ArtworkPath,path);
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
            }
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        private static Sprite Art() => AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Adventure/Art/hero_v1.png");
        private static BattleLoadout CommonerLoadout(string id, string name, params string[] units) => Asset<BattleLoadout>(id, value =>
        {
            value.displayName = name; value.openingHand = Cards(16, 21, 0, 7); value.heroArtwork = Art();
            value.startingFame = units.Length > 1 ? 8 : 3;
            value.units = units.Select(Unit).ToArray();
        });
        private static BattleLoadout ManaSkillLoadout(string id, string name, bool aryth, int skill, string unit) => Asset<BattleLoadout>(id, value =>
        {
            value.displayName = name; value.openingHand = Cards(16, 21, 0, 7); value.heroArtwork = Art();
            value.hero = aryth ? Hero("Arythea", "阿瑞希亚", "hero_skill_pool_000", "skill_000", skill)
                : Hero("Norowas", "诺洛瓦斯", "hero_skill_pool_002", "skill_007", skill);
            value.startingFame = 3; value.learnedSkills = new[] { skill };
            value.redCrystals = 0; value.blueCrystals = id == "DarkFireRed" ? 1 : 0;
            value.units = new[] { Unit(unit) };
        });
        private static BattleLoadout HealingSkillLoadout(string id, string name, bool goldyx, int skill) => Asset<BattleLoadout>(id, value =>
        {
            value.displayName = name + "·战后恢复"; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
            value.hero = goldyx ? Hero("Goldyx", "高蒂克斯", "hero_skill_pool_005", "skill_017", skill)
                : Hero("Wolfhawk", "Wolfhawk", "hero_skill_pool_004", "skill_014", skill);
            value.startingFame = 3; value.learnedSkills = new[] { skill };
            value.units = new[] { Unit(goldyx ? "army_2_014" : "army_2_002") };
        });
        private static BattleLoadout HornLoadout(string id, string name, bool replay, int seed) => Asset<BattleLoadout>(id, value =>
        {
            value.displayName = name; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
            value.artifacts = new[] { Artifact("items_009") }; value.replayEffectRandom = replay; value.effectRandomSeed = seed;
        });
        private static BattleLoadout MageLoadout(string id, string name, string unit, int red, int blue) => Asset<BattleLoadout>(id, value =>
        {
            value.displayName = name; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
            value.units = new[] { Unit(unit) }; value.redCrystals = red; value.blueCrystals = blue;
        });
        private static BattleLoadout ForgeLoadout(string id, string name, int skill, int blue = 0) => Asset<BattleLoadout>(id, value =>
        {
            value.displayName = "高蒂克斯·" + name; value.openingHand = Cards(16, 21, 0, 7); value.heroArtwork = Art();
            value.hero = Hero("Goldyx", "高蒂克斯", "hero_skill_pool_005", "skill_017", skill);
            value.startingFame = 3; value.learnedSkills = new[] { skill };
            value.redCrystals = 0; value.blueCrystals = blue;
            if (skill == 3) value.units = new[] { Unit("army_2_008") };
        });
        private static BattleLoadout SiegeSkillLoadout(string id, string name, string heroId, string heroName, string pool, string sheet, int skill) => Asset<BattleLoadout>(id, value =>
        {
            value.displayName = name; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
            value.hero = Hero(heroId, heroName, pool, sheet, skill); value.startingFame = 3; value.learnedSkills = new[] { skill };
            value.units = new[] { Unit("army_004") };
        });
        private static BattleLoadout MonkLoadout(string id, string name, string unit) => Asset<BattleLoadout>(id, value =>
        {
            value.displayName = name; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
            value.redCrystals = 2; value.blueCrystals = 2; value.units = new[] { Unit(unit) };
        });
        private static BattleLoadout TovakLoadout(string id, string name, int skill) => Asset<BattleLoadout>(id, value =>
        {
            value.displayName = name; value.openingHand = Cards(16, 21, 0, 11); value.heroArtwork = Art();
            value.hero = Hero("Tovak", "托瓦克", "hero_skill_pool_007", "skill_021", skill);
            value.startingFame = 3; value.learnedSkills = new[] { skill };
        });
        private static BattleHeroDefinition Hero(string id, string name, string pool, string sheet, params int[] extraSkills)
        {
            Directory.CreateDirectory(Root + "Heroes");
            string path = Root + "original_skills.json";
            string json = File.ReadAllText("resources/text_json/skill.json");
            if (!File.Exists(path) || File.ReadAllText(path) != json)
            { File.WriteAllText(path, json); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
            var hero = Asset<BattleHeroDefinition>("Heroes/" + id, value =>
            {
                value.heroId = id; value.displayName = name; value.skillPoolId = pool;
                value.originalSkillsJson = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                value.skillArtwork = new[] { new BattleHeroDefinition.SkillArtwork { skillIndex = 2,
                    originalSheet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameData/cards/" + sheet + ".png"),
                    uvRect = new Rect(0.025f, 0.509f, 0.95f, 0.153f) } };
                value.LearnedSkill(2);
            });
            foreach (int skill in extraSkills)
            {
                if (hero.skillArtwork.Any(a => a.skillIndex == skill)) continue;
                float y = skill switch { 0 => 0.834f, 1 => 0.671f, 3 => 0.345f, 4 => 0.186f, 5 when id == "Norowas" => 0.022f, _ => throw new InvalidOperationException("此技能原图尚未核对") };
                if (id == "Wolfhawk" && skill == 1) y = 0.684f;
                hero.skillArtwork = hero.skillArtwork.Append(new BattleHeroDefinition.SkillArtwork { skillIndex = skill,
                    originalSheet = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameData/cards/" + sheet + ".png"),
                    uvRect = new Rect(0.025f, y, 0.95f, 0.153f) }).ToArray();
                hero.LearnedSkill(skill); EditorUtility.SetDirty(hero);
            }
            return hero;
        }
        private static BattleArtifactDefinition Artifact(string id)
        {
            Directory.CreateDirectory(Root + "Artifacts");
            return Asset<BattleArtifactDefinition>("Artifacts/" + id, value =>
            {
                value.source = AssetDatabase.LoadAssetAtPath<ItemCardSO>("Assets/GameData/CardsAssets/" + id + ".asset");
                value.artwork = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameData/cards/" + id + ".png"); value.Snapshot();
            });
        }
        private static BattleUnitDefinition Unit(string id)
        {
            Directory.CreateDirectory(Root + "Units");
            string file = id.StartsWith("army_2_") ? "army_2.json" : "army.json";
            string path = Root + "original_" + file;
            string json = File.ReadAllText("resources/text_json/" + file);
            if (!File.Exists(path) || File.ReadAllText(path) != json)
            { File.WriteAllText(path, json); AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport); }
            return Asset<BattleUnitDefinition>("Units/" + id, value =>
            {
                value.originalId = id; value.originalJson = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
                value.artwork = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameData/cards/" + id + ".png");
                value.Snapshot();
            });
        }
        private static T Asset<T>(string name, Action<T> initialize) where T : ScriptableObject
        {
            string path = Root + name + ".asset";
            var value = AssetDatabase.LoadAssetAtPath<T>(path);
            if (value != null) return value; // Preserve user-edited encounter and loadout configuration.
            value = ScriptableObject.CreateInstance<T>(); initialize(value); AssetDatabase.CreateAsset(value, path); return value;
        }
        private static ActionCardDefinition[] Cards(params int[] numbers) => numbers.Select(number =>
        {
            string id = "basic_card_" + number.ToString("000");
            return Asset<ActionCardDefinition>("Cards/" + id, binding =>
            {
                binding.source = AssetDatabase.LoadAssetAtPath<ActionCardSO>("Assets/GameData/CardsAssets/" + id + ".asset");
                binding.artwork = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/GameData/cards/" + id + ".png");
                binding.Snapshot();
            });
        }).ToArray();
        private static OriginalEnemyView[] Enemies(JArray source, params string[] ids) => ids.Select(id =>
        {
            var row = source.Single(r => (string)r["id"] == id);
            return new OriginalEnemyView { originalId = id, displayName = (string)row["monster_name"],
                originalToken = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/GameData/" + ((string)row["image"]).Replace('\\', '/')) };
        }).ToArray();
    }
}
