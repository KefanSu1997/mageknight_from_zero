using System;
using System.IO;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace MK.Tests.Battles
{
    public sealed class TezlaElementalistSourceDataTests
    {
        [Test]
        public void ThreeOriginalElementalistSymbolsAreMetadataNotRegenerationWithPrintedStatsPreserved()
        {
            string root = Directory.GetCurrentDirectory();
            string source = File.ReadAllText(Path.Combine(root, "resources/text_json/monster.json"));
            string packed = File.ReadAllText(Path.Combine(root, "Assets/GameData/BattleDemo/original_monsters.json"));
            var raw = JArray.Parse(source);
            Assert.That(JToken.DeepEquals(raw, JArray.Parse(packed)), Is.True,
                "Runtime TextAsset and source data must contain identical original definitions");
            var formal = MonsterJsonLoader.ParseMonsters(source);
            var runtime = MonsterJsonLoader.ParseMonsters(packed);
            string[] ids = { "monster_015", "monster_054", "monster_057" };
            int[] armor = { 4, 7, 1 }, fame = { 3, 4, 1 };
            var attacks = new[] {
                new[] { new EnemyAttack(3, Element.Fire), new EnemyAttack(3, Element.Ice) },
                new[] { new EnemyAttack(6, Element.Ice) },
                new[] { new EnemyAttack(1, Element.Ice), new EnemyAttack(1, Element.Ice) }
            };
            var resistances = new[] {
                new[] { Ability.FireResist, Ability.IceResist },
                new[] { Ability.IceResist }, new[] { Ability.IceResist, Ability.Elusive, Ability.Defend }
            };
            for (int i = 0; i < ids.Length; i++)
            {
                var record = raw.Single(t => (string)t["id"] == ids[i]);
                Assert.That((string)record["faction"], Is.EqualTo("Elementalist"));
                Assert.That(record["abilities"].Children(), Is.Empty);
                foreach (var enemy in new[] { formal.Single(e => e.Id == ids[i]), runtime.Single(e => e.Id == ids[i]) })
                {
                    Assert.That(enemy.Abilities.Contains(Ability.Regenerate), Is.False);
                    Assert.That(enemy.Abilities, Is.EquivalentTo(resistances[i]));
                    Assert.That(enemy.Armor, Is.EqualTo(armor[i]));
                    Assert.That(enemy.ElusiveArmor, Is.EqualTo(i == 2 ? 2 : 0));
                    Assert.That(enemy.DefendValue, Is.EqualTo(i == 2 ? 1 : 0));
                    Assert.That(enemy.Fame, Is.EqualTo(fame[i]), "Printed fame is unchanged; faction reward mode remains pending");
                    Assert.That(Enumerable.Range(0, enemy.AttackCount).Select(enemy.GetAttack), Is.EqualTo(attacks[i]));
                }
            }
        }
    }
}
