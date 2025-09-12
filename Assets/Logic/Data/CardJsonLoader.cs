using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json; // 取代 System.Text.Json 以支援 Unity 環境
using Newtonsoft.Json.Linq;
using MK.Logic.Core;
using MK.Logic.Data.Cards;
using MK.Logic.Runtime.CardEffects;

namespace MK.Logic.Data
{
    /// <summary>
    /// 從 resources/text_json 讀取卡牌資料並轉成資料類型。
    /// </summary>
    public static class CardJsonLoader
    {
        // 預設路徑與舊版本相同，可在不同執行環境下覆寫
        private static string Folder = Path.Combine(AppContext.BaseDirectory, "../../../../resources/text_json");

        /// <summary>
        /// 設定欲讀取的基底資料夾，若為 <c>null</c> 則使用預設值。
        /// </summary>
        /// <param name="baseDir">卡牌 JSON 所在資料夾。</param>
        public static void SetBaseDirectory(string? baseDir = null)
        {
            Folder = baseDir ?? Path.Combine(AppContext.BaseDirectory, "../../../../resources/text_json");
        }

        /// <summary>讀取普通行動牌。</summary>
        public static List<ActionCardData> LoadBasicActions()
        {
            return LoadActions(Path.Combine(Folder, "basic_card.json"), CardSet.BasicAction);
        }

        /// <summary>讀取高級行動牌。</summary>
        public static List<ActionCardData> LoadAdvancedActions()
        {
            return LoadActions(Path.Combine(Folder, "advanced_card.json"), CardSet.AdvancedAction);
        }

        /// <summary>讀取神器與物品牌。</summary>
        public static List<ItemCardData> LoadItems()
        {
            var json = File.ReadAllText(Path.Combine(Folder, "items.json"));
            var raws = JsonConvert.DeserializeObject<List<ItemRaw>>(json)!;
            var list = new List<ItemCardData>(raws.Count);
            foreach (var r in raws)
            {
                list.Add(new ItemCardData(
                    r.id,
                    r.card_name,
                    CardSet.Item,
                    r.image,
                    r.en_image,
                    r.type,
                    r.assign_effect,
                    r.once_per_round
                ));
            }
            return list;
        }

        /// <summary>讀取法術牌。</summary>
        public static List<SpellCardData> LoadSpells()
        {
            var json = File.ReadAllText(Path.Combine(Folder, "magic.json"));
            var raws = JsonConvert.DeserializeObject<List<SpellRaw>>(json)!;
            var list = new List<SpellCardData>(raws.Count);
            foreach (var r in raws)
            {
                list.Add(new SpellCardData(
                    r.id,
                    r.spell_top.name,
                    CardSet.Spell,
                    r.image,
                    r.en_image,
                    ManaColorParser.ParseMany(r.mana_color)[0],
                    new HalfSpell(r.spell_top.name, r.spell_top.effect, ManaColorParser.ParseMany(r.spell_top.mana_cost)),
                    new HalfSpell(r.spell_bottom.name, r.spell_bottom.effect, ManaColorParser.ParseMany(r.spell_bottom.mana_cost))
                ));
            }
            return list;
        }

        private static List<ActionCardData> LoadActions(string path, CardSet set)
        {
            var json = File.ReadAllText(path);
            var raws = JsonConvert.DeserializeObject<List<ActionRaw>>(json)!;
            var list = new List<ActionCardData>(raws.Count);
            foreach (var r in raws)
            {
                list.Add(new ActionCardData(
                    r.id,
                    r.card_name,
                    set,
                    r.image,
                    r.en_image,
                    r.base_effect,
                    r.enhanced_effect,
                    ManaColorParser.ParseMany(r.required_crystals)
                ));
            }
            return list;
        }

        // ↓ 以下為對應 JSON 結構的臨時類型 -----------------
        private record ActionRaw(string id, string image, string en_image, string card_name, string base_effect, string required_crystals, string enhanced_effect);
        private record ItemRaw(string id, string image, string en_image, string card_name, string type, string assign_effect, string once_per_round);
        private record SpellHalf(string name, string effect, string mana_cost);
        private record SpellRaw(string id, string image, string en_image, string mana_color, SpellHalf spell_top, SpellHalf spell_bottom);

        /// <summary>
        /// 讀取指定的英雄技能池，目前已支援多個池子。
        /// </summary>
        public static List<SkillCard> LoadHeroSkills(string poolId)
        {
            // 直接以 JObject/JArray 解析，避免 AOT 下的反射限制
            var json = File.ReadAllText(Path.Combine(Folder, "skill.json"));
            var doc = JArray.Parse(json);
            foreach (var element in doc)
            {
                if ((string?)element["id"] != poolId) continue;
                var list = new List<SkillCard>();
                foreach (var s in element["skills"]!)
                {
                    string name = (string)s["skill_name"]!;
                    ICardEffect eff = name switch
                    {
                        "黑暗道路" => new Runtime.CardEffects.DarkPathEffect(),
                        "燃烧之力" => new Runtime.CardEffects.BurningPowerEffect(),
                        "炽热剑术" => new Runtime.CardEffects.HotSwordEffect(),
                        "秘密谈判" => new Runtime.CardEffects.SecretNegotiationEffect(),
                        "暗火魔法" => new Runtime.CardEffects.DarkFireMagicEffect(),
                        "痛苦之力" => new Runtime.CardEffects.PainPowerEffect(),
                        "魔咒" => new Runtime.CardEffects.CurseMagicEffect(),
                        "两极分化" => new Runtime.CardEffects.PolarizationEffect(),
                        "激励" => poolId switch
                        {
                            "hero_skill_pool_000" => new Runtime.CardEffects.MotivationEffect(),
                            "hero_skill_pool_002" => new Runtime.CardEffects.MotivationWhiteEffect(),
                            "hero_skill_pool_004" => new Runtime.CardEffects.MotivationFameEffect(),
                            "hero_skill_pool_005" => new Runtime.CardEffects.MotivationGreenEffect(),
                            "hero_skill_pool_007" => new Runtime.CardEffects.MotivationBlueEffect(),
                            _ => new Runtime.CardEffects.MotivationEffect()
                        },
                        "提神沐浴" => new Runtime.CardEffects.RefreshBathEffect(),
                        "提神微风" => new Runtime.CardEffects.RefreshBreezeEffect(),
                        "神鹰之眼" => new Runtime.CardEffects.EagleEyeEffect(),
                        "特立独行" => new Runtime.CardEffects.LoneWolfEffect(),
                        "百步穿杨" => new Runtime.CardEffects.BullseyeEffect(),
                        "洞悉猎物" => new Runtime.CardEffects.InsightPreyEffect(),
                        "嘲讽" => new Runtime.CardEffects.TauntEffect(),
                        "决斗" => new Runtime.CardEffects.DuelEffect(),
                        "群狼嚎叫/狼嚎" => new Runtime.CardEffects.WolfPackHowlEffect(),
                        "全军行进" => new Runtime.CardEffects.FullMarchEffect(),
                        "白昼狙击" => new Runtime.CardEffects.DaySniperEffect(),
                        "重整旗鼓" => new Runtime.CardEffects.RallyEffect(),
                        "公开谈判" => new Runtime.CardEffects.PublicNegotiationEffect(),
                        "风中残叶" => new Runtime.CardEffects.LeavesInWindEffect(),
                        "树梢风语" => new Runtime.CardEffects.TreetopWhisperEffect(),
                        "领导" => new Runtime.CardEffects.LeadershipSkillEffect(),
                        "忠诚契约" => new Runtime.CardEffects.LoyalContractEffect(),
                        "风雨平复/风雨祈祷" => new Runtime.CardEffects.CalmWindEffect(),
                        "元素抗性" => new Runtime.CardEffects.ElementalResistEffect(),
                        "野性盟友" => new Runtime.CardEffects.WildAllyEffect(),
                        "雷云风暴" => new Runtime.CardEffects.ThunderstormEffect(),
                        "闪电风暴" => new Runtime.CardEffects.LightningStormEffect(),
                        "迷醉" => new Runtime.CardEffects.TranceEffect(),
                        "叉形闪电" => new Runtime.CardEffects.ForkedLightningEffect(),
                        "变换身形" => new Runtime.CardEffects.ShapeshiftEffect(),
                        "隐秘之路" => new Runtime.CardEffects.HiddenPathEffect(),
                        "再生" => poolId switch
                        {
                            "hero_skill_pool_001" => new Runtime.CardEffects.RegenerationSkillEffect(),
                            "hero_skill_pool_003" => new Runtime.CardEffects.RegenerationRedEffect(),
                            _ => new Runtime.CardEffects.RegenerationSkillEffect(),
                        },
                        "灵魂向导" => new Runtime.CardEffects.SoulGuideEffect(),
                        "身经百战" => new Runtime.CardEffects.BattleHardenedEffect(),
                        "战斗狂乱" => new Runtime.CardEffects.BattleFrenzyEffect(),
                        "萨满仪式" => new Runtime.CardEffects.ShamanRitualEffect(),
                        "奥术伪装" => new Runtime.CardEffects.ArcaneDisguiseEffect(),
                        "傀儡大师" => new Runtime.CardEffects.PuppetMasterEffect(),
                        "混乱大师" => new Runtime.CardEffects.ChaosMasterEffect(),
                        "诅咒" => new Runtime.CardEffects.HexCurseEffect(),
                        "冰封之力" => new Runtime.CardEffects.FrozenPowerEffect(),
                        "药剂制作" => new Runtime.CardEffects.PotionCraftEffect(),
                        "白晶锻造" => new Runtime.CardEffects.WhiteForgeEffect(),
                        "绿晶锻造" => new Runtime.CardEffects.GreenForgeEffect(),
                        "红晶锻造" => new Runtime.CardEffects.RedForgeEffect(),
                        "荧光灵运" => new Runtime.CardEffects.CrystalLuckEffect(),
                        "飞行" => new Runtime.CardEffects.FlightEffect(),
                        "通用力量" => new Runtime.CardEffects.UniversalPowerEffect(),
                        "魔力开源/魔源冰封" => new Runtime.CardEffects.ManaOpenEffect(),
                        "魔源冰封" => new Runtime.CardEffects.ManaFreezeEffect(),
                        "猛毒药瓶" => new Runtime.CardEffects.PoisonVialEffect(),
                        "圣族之杯" => new Runtime.CardEffects.HolyChaliceEffect(),
                        "鬼魂灵药" => new Runtime.CardEffects.GhostElixirEffect(),
                        "隐匿法杖" => new Runtime.CardEffects.HiddenStaffEffect(),
                        "复苏项链" => new Runtime.CardEffects.RevivalNecklaceEffect(),
                        "晨暮宝珠" => new Runtime.CardEffects.DawnDuskOrbEffect(),
                        "疗伤草药" => new Runtime.CardEffects.HealingHerbEffect(),
                        "寒冰碎片" => new Runtime.CardEffects.IceShardEffect(),
                        "火焰宝石" => new Runtime.CardEffects.FireGemEffect(),
                        "秘法地图" => new Runtime.CardEffects.ArcaneMapEffect(),
                        "护御披风" => new Runtime.CardEffects.GuardCloakEffect(),
                        "重训典籍/重训典籍" => new Runtime.CardEffects.RetrainTomeEffect(),
                        "快步前行" => new Runtime.CardEffects.QuickStrideEffect(),
                        "黑夜狙击" => new Runtime.CardEffects.NightSniperEffect(),
                        "寒冰剑术" => new Runtime.CardEffects.IceSwordEffect(),
                        "盾牌精通" => new Runtime.CardEffects.ShieldMasteryEffect(),
                        "抗性弱化" => new Runtime.CardEffects.ResistWeakeningEffect(),
                        "无惧痛苦" => new Runtime.CardEffects.FearlessPainEffect(),
                        "漫不经心" => new Runtime.CardEffects.CarelessEffect(),
                        "魔法抗拒" => new Runtime.CardEffects.MagicRepelEffect(),
                        "魔力霸主" => new Runtime.CardEffects.ManaOverlordEffect(),
                        _ when name == "魔力强化/魔力抑制" => new Runtime.CardEffects.ManaBoostEffect(),
                        _ when name == "自然复仇/自然复仇" => new Runtime.CardEffects.NatureRevengeEffect(),
                        _ when name == "痛苦仪式/治疗仪式" => new Runtime.CardEffects.RitualPainEffect(),
                        _ => new Runtime.CardEffects.DummyEffect()
                    };

                    bool reusable = true; // 英雄技能預設可重複使用
                    list.Add(new SkillCard(name, eff, reusable));
                }
                return list;
            }
            return new List<SkillCard>();
        }
    }
}
