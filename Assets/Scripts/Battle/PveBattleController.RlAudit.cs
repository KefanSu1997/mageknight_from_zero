using System.Linq;
using Newtonsoft.Json;
using MK.Logic.Runtime;
using MK.Logic.Data;

namespace MageKnight.Battle
{
    public sealed partial class PveBattleController
    {
        // Audit output only: read the real session, never reconstruct or mutate it.
        private string CaptureRlSharedAudit()
        {
            if (_battle == null) return "";
            var player = _battle.Player;
            var context = _battle.Context;
            return JsonConvert.SerializeObject(new
            {
                phase = _battle.Phase.ToString(), inBattle = context.InBattle, battleSessionPresent = true,
                hero = player.HeroId, fame = player.Fame, wounds = player.Wounds,
                won = _battle.Won, kills = context.DefeatedEnemyIndices.Count,
                hand = player.Deck.Hand.Select(card => card.Id).ToArray(),
                deck = player.Deck.DrawPileCount, influence = context.InfluencePool,
                block = context.CombatPower.BlockTotal, dayPart = context.DayPart.ToString(),
                sourceUsedThisTurn = context.ManaSource?.WasUsedThisTurn(player) ?? false,
                tokens = player.Mana.Tokens.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                crystals = player.Mana.Crystals.ToDictionary(pair => pair.Key.ToString(), pair => pair.Value),
                units = player.Units.Select(unit => new { id = unit.Card.Id, ready = unit.IsReady, wounds = unit.Wounds }).ToArray(),
                attacks = context.CombatPower.AvailableAttacks(_battle.Phase, context.AllAttacksSiege)
                    .Select((part,index) => new { index, value = part.Value, bound = part.RequiredEnemy != null,
                        card = (part.SourceCard as DeedCard)?.Id ?? "" }).ToArray(),
                enemies = _battle.Enemies.Select((enemy,index) => new { index, id = enemy.Id,
                    defeated = context.DefeatedEnemyIndices.Contains(index), defend = enemy.DefendValue }).ToArray(),
                uiSelectedTargets = _targets.OrderBy(index => index).ToArray(),
                uiActionContextNeutral = _selected == null && _selectedUnit == null && _selectedSkill == null
                    && !_bowPanel && !_recovery && !_selectMaterials,
                uiReservedAttackSlots = context.CombatPower.AvailableAttacks(_battle.Phase, context.AllAttacksSiege)
                    .Select((part,index) => new { part,index }).Where(entry => _reservedAttacks.Any(reserved => ReferenceEquals(reserved,entry.part)))
                    .Select(entry => entry.index).ToArray()
            });
        }
    }
}
