using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;

// The rules and numerical effects are linked directly from Assets/Logic.
// This first adapter deliberately exposes one declared, solvable rehearsal family.
// It is not a full-game observation or a replacement rules implementation.
record Move(string Kind,string Card="",string Choice="",int Targets=0,int Parts=0,int Option=0,string Input="")
{
    public string Key => $"{Kind}:{Card}:{Choice}:{Targets}:{Parts}:{Option}"+(Input==""?"":":"+Input);
}

sealed class CombatEnv
{
    public const string Scope="dual032_arythea_level2_two_original_cards_v1";
    public readonly PveBattleSession Battle;
    static readonly MK.Logic.Data.Cards.ActionCardData[] Cards=CardJsonLoader.LoadBasicActions().ToArray();
    readonly SkillCard skill;
    public CombatEnv(int seed)
    {
        var player=new PlayerState(0,"阿瑞希亚"){HeroId="Arythea",Fame=3,Armor=2};
        player.Mana.AddCrystal(ManaColor.Red);player.Mana.AddCrystal(ManaColor.Blue);
        var hand=new[]{new DeedCard("basic_card_000",CardType.Action),new DeedCard("basic_card_021",CardType.Action)};
        if(new Random(seed).Next(2)==1)Array.Reverse(hand);
        player.Deck.Hand.AddRange(hand);
        skill=CardJsonLoader.LoadHeroSkills(OriginalHeroes.Get(player.HeroId).SkillPoolId)[2];
        HeroSkillAcquisition.Acquire(player,skill,new Random(seed));
        var monster=CardJsonLoader.LoadMonsters().Single(m=>m.Id=="monster_032");
        // Separate printed enemy tokens even when both IDs match.
        Battle=new PveBattleSession(player,new[]{monster with{},monster with{}},Cards,
            new ActionContext{DayPart=DayPart.Day},enemyRewardMode:EnemyRewardMode.PrintedFameOnly);
    }
    public Dictionary<string,string> Observe()
    {
        var p=Battle.Player;var c=Battle.Context;
        var d=new Dictionary<string,string>{["phase"]=Battle.Phase.ToString(),["won"]=Battle.Won.ToString(),
            ["hero.id"]=p.HeroId,["armor"]=p.Armor.ToString(),["fame"]=p.Fame.ToString(),
            ["hand"]=p.Deck.Hand.Count.ToString(),["deck.count"]=p.Deck.DrawPileCount.ToString(),
            ["wounds"]=p.Wounds.ToString(),["kills"]=c.DefeatedEnemyIndices.Count.ToString(),
            ["ranged"]=Battle.AttackTotal(AttackType.Ranged).ToString(),["siege"]=Battle.AttackTotal(AttackType.Siege).ToString(),
            ["melee"]=Battle.AttackTotal(AttackType.Melee).ToString(),["block"]=c.CombatPower.BlockTotal.ToString(),
            ["hand.ids"]=string.Join(",",p.Deck.Hand.Where(x=>x.Type!=CardType.Wound).Select(x=>x.Id).OrderBy(x=>x)),
            ["skill.used"]=SkillActions.IsUsed(p,skill).ToString(),["dead.indices"]=string.Join(",",c.DefeatedEnemyIndices.OrderBy(x=>x)),
            ["blocked.indices"]=string.Join(",",Enumerable.Range(0,2).Where(i=>Battle.IsBlocked(i))),
            ["attack.parts"]=string.Join(";",c.CombatPower.AvailableAttacks(Battle.Phase,c.AllAttacksSiege).Select(a=>$"{a.Value}/{a.Element}/{a.Type}/{(a.SourceCard as DeedCard)?.Id}/{a.SourceSkill?.SourceId}"))};
        foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black})
        {var name=color.ToString().ToLowerInvariant();d["crystals."+name]=p.Mana.Crystals.GetValueOrDefault(color).ToString();d["tokens."+name]=p.Mana.Tokens.GetValueOrDefault(color).ToString();}
        // Original enemy static features accompany the state, rather than learning from artwork.
        for(int i=0;i<2;i++){var e=Battle.Enemies[i];d[$"enemy.{i}"]=$"{e.Id}/{e.Armor}/{e.Attack}/{e.AttackElement}/{string.Join(',',e.Abilities)}";}
        return d;
    }
    public string StateKey()=>string.Join("|",Observe().OrderBy(x=>x.Key).Select(x=>$"{x.Key}={x.Value}"));
    public bool Done=>Battle.Phase==Phase.End;
    public void Apply(Move a)
    {
        switch(a.Kind)
        {
            case "advance":Battle.Advance();break;
            case "play":Battle.Play(Battle.Player.Deck.Hand.Single(c=>c.Id==a.Card),a.Choice);break;
            case "sideways":Battle.PlaySideways(Battle.Player.Deck.Hand.Single(c=>c.Id==a.Card));break;
            case "skill":Battle.UseSkill(skill,a.Option);break;
            case "block":Battle.Block(a.Targets);break;
            case "attack":
                var parts=Battle.Context.CombatPower.AvailableAttacks(Battle.Phase,Battle.Context.AllAttacksSiege).Where((_,i)=>(a.Parts&(1<<i))!=0).ToArray();
                Battle.AttackSelected(Enumerable.Range(0,2).Where(i=>(a.Targets&(1<<i))!=0).ToArray(),parts);break;
            default:throw new ArgumentException("Unsupported semantic action");
        }
    }
    // Membership comes from executing the formal rule entry on a reversible checkpoint.
    // No handwritten approximations of costs, target armor, or timing act as legality rules.
    public List<Move> Legal()
    {
        if(Done)return new();
        var candidates=new List<Move>{new("advance")};
        foreach(var card in Battle.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).ToArray())
        {
            candidates.AddRange(Battle.Choices(card).Select(c=>new Move("play",card.Id,c.Id)));
            candidates.Add(new("sideways",card.Id));
        }
        candidates.AddRange(Battle.SkillChoices(skill).Select(c=>new Move("skill",Option:c.Option)));
        if(Battle.Phase==Phase.Block)for(int i=0;i<2;i++)candidates.Add(new("block",Targets:i));
        if(Battle.Phase==Phase.Ranged||Battle.Phase==Phase.Melee)
        {
            int n=Battle.Context.CombatPower.AvailableAttacks(Battle.Phase,Battle.Context.AllAttacksSiege).Count();
            if(n>3)throw new InvalidOperationException("Expand the declared action adapter before exposing larger pools");
            for(int targets=1;targets<4;targets++)for(int parts=1;parts<(1<<n);parts++)candidates.Add(new("attack",Targets:targets,Parts:parts));
        }
        var checkpoint=DemoStateCheckpoint.Capture(Battle);
        var key=StateKey();var hand=Battle.Player.Deck.Hand.ToArray();var enemies=Battle.Enemies.ToArray();
        var result=new List<Move>();
        foreach(var a in candidates)
        {
            try{Apply(a);if(StateKey()!=key)result.Add(a);}
            catch(InvalidOperationException){} // Formal denial is masked, not silently accepted.
            finally{checkpoint.Restore();}
            if(StateKey()!=key||!Battle.Player.Deck.Hand.SequenceEqual(hand,ReferenceEqualityComparer.Instance)
                ||!Battle.Enemies.SequenceEqual(enemies,ReferenceEqualityComparer.Instance))
                throw new InvalidOperationException("Legality preview changed actual state or identity");
        }
        return result.OrderBy(x=>x.Key).ToList();
    }
    public static Dictionary<string,string> UiState(Dictionary<string,string> d)
    {
        var keys=new[]{"phase","won","hero.id","fame","hand","deck.count","wounds","kills","ranged","siege","melee"};
        var result=keys.ToDictionary(k=>k,k=>d[k]);
        foreach(var color in new[]{"red","blue","green","white"})result[color]=d["crystals."+color];
        return result;
    }
}

static class Program
{
    static readonly List<Move> Catalog=BuildCatalog();
    static string[] FeatureNames()
    {
        var n=new List<string>{"phase.Ranged","phase.Block","phase.AssignDamage","phase.Melee","phase.End",
            "episode.steps/24","wounds/8","fame/10","armor/5"};
        foreach(var c in new[]{"red","blue","green","white","gold","black"}){n.Add("crystals."+c+"/3");n.Add("tokens."+c+"/3");}
        n.AddRange(new[]{"hand.basic_card_000","hand.basic_card_021","skill.used"});
        for(int i=0;i<2;i++)n.AddRange(new[]{$"enemy.{i}.alive",$"enemy.{i}.armor/10",$"enemy.{i}.attack/10",$"enemy.{i}.blocked"});
        n.Add("block/10");
        for(int i=0;i<3;i++)n.AddRange(new[]{$"attack.{i}.value/10",$"attack.{i}.source.basic_card_000",$"attack.{i}.source.basic_card_021",$"attack.{i}.source.skill"});
        return n.ToArray();
    }
    static List<Move> BuildCatalog()
    {
        var a=new List<Move>{new("advance")};
        foreach(string id in new[]{"basic_card_000","basic_card_021"})
        {a.Add(new("sideways",id));a.AddRange(BattleCardChoice.For(id).Select(c=>new Move("play",id,c.Id)));}
        var skill=CardJsonLoader.LoadHeroSkills("hero_skill_pool_000")[2];
        a.AddRange(BattleSkillChoice.For(skill.SourceId).Select(c=>new Move("skill",Option:c.Option)));
        for(int i=0;i<2;i++)a.Add(new("block",Targets:i));
        for(int targets=1;targets<4;targets++)for(int parts=1;parts<8;parts++)a.Add(new("attack",Targets:targets,Parts:parts));
        return a.DistinctBy(x=>x.Key).OrderBy(x=>x.Key).ToList();
    }
    static double[] Features(CombatEnv e,int steps)
    {
        var b=e.Battle;var p=b.Player;var c=b.Context;
        var f=new List<double>();
        foreach(var phase in new[]{Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee,Phase.End})f.Add(b.Phase==phase?1:0);
        f.Add(steps/24.0);f.Add(p.Wounds/8.0);f.Add(p.Fame/10.0);f.Add(p.Armor/5.0);
        foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White,ManaColor.Gold,ManaColor.Black})
        {f.Add(p.Mana.Crystals.GetValueOrDefault(color)/3.0);f.Add(p.Mana.Tokens.GetValueOrDefault(color)/3.0);}
        foreach(var id in new[]{"basic_card_000","basic_card_021"})f.Add(p.Deck.Hand.Any(x=>x.Id==id)?1:0);
        f.Add(p.SkillsUsedThisTurn.Count>0?1:0);
        for(int i=0;i<2;i++){var enemy=b.Enemies[i];f.Add(c.DefeatedEnemyIndices.Contains(i)?0:1);f.Add(enemy.Armor/10.0);f.Add(enemy.Attack/10.0);f.Add(b.IsBlocked(i)?1:0);}
        f.Add(c.CombatPower.BlockTotal/10.0);
        var parts=c.CombatPower.AvailableAttacks(b.Phase,c.AllAttacksSiege).ToArray();
        for(int i=0;i<3;i++)
        {
            var part=i<parts.Length?parts[i]:null;
            f.Add((part?.Value??0)/10.0);
            f.Add(part?.SourceCard is DeedCard d&&d.Id=="basic_card_000"?1:0);
            f.Add(part?.SourceCard is DeedCard d2&&d2.Id=="basic_card_021"?1:0);
            f.Add(part?.SourceSkill!=null?1:0);
        }
        return f.ToArray();
    }
    static void Serve()
    {
        CombatEnv env=null;int steps=0;bool done=true;
        object State(double reward=0)=>new{scope=CombatEnv.Scope,state=env.Observe(),features=Features(env,steps),
            legal=done?Array.Empty<int>():Legal(env,env.StateKey()).Select(a=>Catalog.FindIndex(x=>x.Key==a.Key)).ToArray(),
            done,won=env.Battle.Won,reward,steps,maskCacheStates=MaskCache.Count};
        Console.WriteLine(JsonSerializer.Serialize(new{scope=CombatEnv.Scope,protocolVersion=1,abilitySchema=Enum.GetNames<Ability>(),featureNames=FeatureNames(),catalog=Catalog,algorithm="PPO environment protocol"}));
        string line;
        while((line=Console.ReadLine())!=null)
        {
            try
            {
                using var doc=JsonDocument.Parse(line);var r=doc.RootElement;string op=r.GetProperty("op").GetString();
                if(op=="quit")break;
                if(op=="reset"){env=new CombatEnv(r.GetProperty("seed").GetInt32());steps=0;done=false;Console.WriteLine(JsonSerializer.Serialize(State()));continue;}
                if(op!="step"||env==null||done)throw new InvalidOperationException("reset before step");
                int index=r.GetProperty("action").GetInt32();var move=Catalog[index];
                if(!Legal(env,env.StateKey()).Any(a=>a.Key==move.Key))throw new InvalidOperationException("Action is not in the formal legal mask");
                int kills=env.Battle.Context.DefeatedEnemyIndices.Count,wounds=env.Battle.Player.Wounds;
                env.Apply(move);steps++;done=env.Done||steps>=24;
                double reward=-.02+2*(env.Battle.Context.DefeatedEnemyIndices.Count-kills)-.02*(env.Battle.Player.Wounds-wounds);
                if(done)reward+=env.Battle.Won?10:-4;
                Console.WriteLine(JsonSerializer.Serialize(State(reward)));
            }
            catch(Exception ex){Console.WriteLine(JsonSerializer.Serialize(new{error=ex.ToString()}));}
        }
    }
    static readonly Dictionary<string,List<Move>> MaskCache=new();
    static List<Move> Legal(CombatEnv env,string key)
    {if(!MaskCache.TryGetValue(key,out var moves)){moves=env.Legal();MaskCache[key]=moves;}return moves;}
    public static void Main(string[] args)
    {
        CardJsonLoader.SetBaseDirectory(Path.GetFullPath("resources/text_json"));
        if(args.Contains("--serve-journey")){JourneyTraining.Serve();return;}
        if(args.Contains("--serve-journey-builds")){JourneyTraining.Serve(variedBuilds:true);return;}
        if(args.Contains("--serve-journey-units")){JourneyTraining.Serve(variedBuilds:true,unitBuilds:true);return;}
        if(args.Contains("--serve-journey-parties")){JourneyTraining.Serve(variedBuilds:true,unitBuilds:true,partyBuilds:true);return;}
        if(args.Contains("--serve-journey-artifacts")){JourneyTraining.Serve(variedBuilds:true,unitBuilds:true,partyBuilds:true,artifactBuilds:true);return;}
        if(args.Length==2 && args[0]=="--coverage-plan"){CoverageInventory.Write(args[1]);return;}
        if(!args.Contains("--serve"))throw new ArgumentException("Use --serve; PPO training lives in train_ppo.py");
        Serve();
    }
}
