using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using MK.Logic.Core;
using MK.Logic.Data;

namespace MageKnight.Tests.RulesState
{
    /// <summary>Independent original-image transcription; not a gameplay or model coverage claim.</summary>
    public sealed class OriginalEnemyPrintedMetadataTests
    {
        private static JArray Oracle => (JArray)JObject.Parse(File.ReadAllText(
            "AutomationConfigs/original_enemy_combat_oracle_20261004.json"))["entries"];
        public static IEnumerable<string> OriginalIds() => Oracle.Select(e=>(string)e["id"]);

        [TestCaseSource(nameof(OriginalIds))]
        public void FormalParserMatchesReviewedOriginalPrintedCore(string id)
        {
            var expected=Oracle.Single(e=>(string)e["id"]==id);
            var actual=MonsterJsonLoader.LoadMonsters("resources/text_json").Single(e=>e.Id==id);
            int number=int.Parse(id.Substring("monster_".Length));
            using(var hash=SHA256.Create())
            {
                var bytes=hash.ComputeHash(File.ReadAllBytes("Assets/GameData/cards/monster ("+number+").jpg"));
                Assert.That(BitConverter.ToString(bytes).Replace("-","").ToLowerInvariant(),
                    Is.EqualTo((string)expected["source_image_sha256"]),"Actual original image must match reviewed source");
            }
            Assert.That(actual.Armor,Is.EqualTo((int)expected["armor"]));
            Assert.That(actual.ElusiveArmor,Is.EqualTo((int)expected["elusive"]));
            Assert.That(actual.Fame,Is.EqualTo((int)expected["fame"]));
            Assert.That(actual.DefendValue,Is.EqualTo((int)expected["defend"]));
            var abilities=((JArray)expected["abilities"]).Select(a=>
                (Ability)Enum.Parse(typeof(Ability),((string)a).Replace(" ",""),true)).ToList();
            foreach(var shield in (JArray)expected["resist"])
                abilities.Add((Ability)Enum.Parse(typeof(Ability),(string)shield+"Resist"));
            if((bool)expected["fortified"])abilities.Add(Ability.Fortified);
            if(actual.ElusiveArmor>0)abilities.Add(Ability.Elusive);
            if(actual.DefendValue>0)abilities.Add(Ability.Defend);
            Assert.That(actual.Abilities,Is.EquivalentTo(abilities));
            int summons=(int)expected["summon_count"];
            Assert.That(actual.SummonAttack,Is.EqualTo(summons>0));
            if(summons>0)Assert.That(actual.SummonCount,Is.EqualTo(summons));
            var attacks=(JArray)expected["attacks"];
            Assert.That(actual.AttackCount,Is.EqualTo(attacks.Count));
            for(int index=0;index<attacks.Count;index++)
            {
                Assert.That(actual.GetAttack(index).Value,Is.EqualTo((int)attacks[index][0]));
                Assert.That(actual.GetAttack(index).Element.ToString(),Is.EqualTo((string)attacks[index][1]));
            }
        }

        [Test] public void UnityPackedEnemyDefinitionsMatchFormalRuleSourceExactly()
        {
            Assert.That(File.ReadAllBytes("Assets/GameData/BattleDemo/original_monsters.json"),
                Is.EqualTo(File.ReadAllBytes("resources/text_json/monster.json")),
                "Unity must not keep obsolete enemy rules while CLI uses corrected definitions");
        }
    }
}
