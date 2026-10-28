using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using MK.Logic.Core;
using MK.Logic.Data;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Battles;

// Original persistent inventories and consumers. Missing formal input branches remain explicit gaps.
static partial class JourneyTraining
{
    const int EnemySlots=6; // Three actual encounters, each with up to two printed summons.
    const int EnemyMasks=1<<EnemySlots;
    static readonly bool NativePayments=Environment.GetEnvironmentVariable("BATTLE_RL_NATIVE_PAYMENTS")=="1";
    static readonly bool NativeConversions=Environment.GetEnvironmentVariable("BATTLE_RL_NATIVE_CONVERSIONS")=="1";
    static readonly bool NativeEnergyTargets=Environment.GetEnvironmentVariable("BATTLE_RL_NATIVE_ENERGY_TARGETS")=="1";
    static readonly bool NativeSharedSkills=Environment.GetEnvironmentVariable("BATTLE_RL_SHARED_SKILLS")=="1";
    static readonly bool NativeShapeshift=Environment.GetEnvironmentVariable("BATTLE_RL_SHAPESHIFT")=="1";
    static readonly bool NativeWorld=Environment.GetEnvironmentVariable("BATTLE_RL_ORIGINAL_WORLD")=="1";
    static readonly string OriginalWorldSourceSha=NativeWorld?Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes("resources/original_maps/tutorial_opening.json"))).ToLowerInvariant():null;
    static readonly MK.Logic.Runtime.Map.OriginalMapCatalog OriginalWorldOpening=NativeWorld?MK.Logic.Runtime.Map.OriginalMapCatalog.Load("resources/original_maps/tutorial_opening.json"):null;
    static string FeatureSchema=>"original_combat_six_targets_summon_parents_repeated_v48"+(NativePayments?"_native_payment_selection_turn_obligation_v55":"")+(NativeConversions?"_combat_conversions_v58":"")+(NativeEnergyTargets?"_energy_unit_targets_v59":"")+"_explosive_followup_v80_improvisation_artifact_materials_v92_effective_fortification_v94_independent_task_progress_v1"+(NativeSharedSkills?"_shared_seven_owner_inputs_v2":"")+(NativeShapeshift?"_native_shapeshift_v1":"")+(NativeWorld?"_original_tutorial_world_v1":"");
    static readonly MK.Logic.Data.Cards.ActionCardData[] Basics=CardJsonLoader.LoadBasicActions().ToArray();
    static readonly MK.Logic.Data.Cards.ActionCardData[] Advanced=CardJsonLoader.LoadAdvancedActions().ToArray();
    static readonly MK.Logic.Data.Cards.ActionCardData[] Cards=Basics.Concat(Advanced).ToArray();
    static readonly MK.Logic.Data.Cards.SpellCardData[] Spells=CardJsonLoader.LoadSpells().ToArray();
    // The formal play API needs both the original spell definition and its printed
    // deed descriptor. This is the same packaging used by the Unity deed catalog.
    static readonly MK.Logic.Data.Cards.ActionCardData[] OriginalDeeds=Cards.Concat(Spells.Select(s=>new MK.Logic.Data.Cards.ActionCardData(s.Id,s.Name,s.Set,s.ImagePath,s.EnImagePath,s.Top.Effect,s.Bottom.Effect,s.Top.ManaCost))).ToArray();
    static IEnumerable<string> DeedIds=>Cards.Select(c=>c.Id).Concat(Spells.Select(s=>s.Id));
    static readonly Monster[] Enemies=CardJsonLoader.LoadMonsters().ToArray();
    static readonly SkillCard[] Skills=OriginalHeroes.All.SelectMany(h=>CardJsonLoader.LoadHeroSkills(h.SkillPoolId)).ToArray();
    static readonly UnitCard[] Units=UnitJsonLoader.SupportedIds.Select(CardJsonLoader.LoadUnit).ToArray();
    static readonly MK.Logic.Data.Cards.ItemCardData[] Items=CardJsonLoader.LoadItems().Where(c=>PveArtifactInputs.Supports(c.Id)).ToArray();
    static readonly Dictionary<string,string[]> Profiles=DeedIds.Distinct().ToDictionary(id=>id,id=>InputProfiles(id).Distinct().ToArray());
    static IEnumerable<string> UnitOrders(string prefix="")
    {
        if(prefix.Length>0)yield return prefix;
        foreach(char unit in "0123")if(!prefix.Contains(unit))
            foreach(var order in UnitOrders(prefix+unit))yield return order;
    }
    static readonly string[] DamageOrders=UnitOrders().ToArray();
    static readonly List<Move> Catalog=BuildCatalog();
    static readonly Dictionary<string,int> CatalogIndices=Catalog.Select((a,i)=>(a.Key,i)).ToDictionary(x=>x.Key,x=>x.i);
    static bool VariedBuilds;
    static bool UnitBuilds;
    static bool PartyBuilds;
    static bool ArtifactBuilds;
    static bool IncludeOriginalOpenings;
    static IEnumerable<Move> ArtifactMoves(string id)
    {
        if(id=="items_024"){yield return new Move("boxReveal",id);yield break;}
        foreach(bool burn in new[]{false,true})
        {
            if(id=="items_022")
            {
                foreach(var skill in Skills)
                {
                    if(burn){yield return new Move("artifact",id,"burn",Input:"skill="+skill.SourceId);continue;}
                    var choices=HeadbandBorrowResolution.IsUnit(skill)?new[]{0,1}:BattleSkillChoice.For(skill.SourceId).Select(c=>c.Option).Distinct().ToArray();
                    foreach(var option in choices)
                        for(int target=0;target<(BattleSkillChoice.NeedsEnemy(skill.SourceId)?EnemySlots:1);target++)
                            for(int slot=0;slot<(HeadbandBorrowResolution.IsUnit(skill)?3:HeadbandBorrowResolution.IsWound(skill)?8:BattleSkillChoice.NeedsAttack(skill.SourceId,option)?4:1);slot++)
                                yield return new Move("artifact",id,"normal",Targets:target,Parts:slot,Option:option,Input:"skill="+skill.SourceId);
                }
                continue;
            }
            if(id=="items_023")
            {
                foreach(var spell in Spells)foreach(var choice in BattleCardChoice.For(spell.Id).Where(c=>c.Enhanced==burn))
                    for(int slot=0;slot<8;slot++)for(int enemy=0;enemy<(choice.Target==BattleCardTarget.None?1:EnemySlots);enemy++)
                        for(int unit=0;unit<(spell.Id is "magic_000" or "magic_001" or "magic_005"?3:1);unit++)
                            foreach(var profile in Profiles[spell.Id])
                                yield return new Move("artifact",id,burn?"burn":"normal",Targets:enemy,Parts:unit,Input:"materialSlot="+slot+";spellId="+spell.Id+";spellChoice="+choice.Id+(profile==""?"":";"+profile));
                continue;
            }
            if(id=="items_013")
            {
                for(int slot=0;slot<8;slot++)
                    foreach(var offered in burn?Spells.Select(c=>c.Id):Advanced.Select(c=>c.Id))
                        yield return new Move("artifact",id,burn?"burn":"normal",Input:"materialSlot="+slot+";"+(burn?"spells=":"advanced=")+offered);
                continue;
            }
            if(id=="items_021")
            {
                // White travel needs the actual revealed-map curriculum; do not
                // expose an empty no-op path as a movement consumer.
                for(int first=1;first<4;first++)for(int second=burn?1:0;second<(burn?4:1);second++)
                    if(!burn||first!=second)
                        for(int color=0;color<(first==1||second==1?4:1);color++)for(int unit=0;unit<(first==2||second==2?3:1);unit++)
                            for(int slot=0;slot<(burn?1:8);slot++)
                                yield return new Move("artifact",id,burn?"burn":"normal",Targets:unit,Input:(burn?"":"materialSlot="+slot+";")+"first="+first+";second="+second+";color="+color);
                continue;
            }
            if(id=="items_017"&&burn)
            {foreach(var offered in Units)for(int disband=-1;disband<3;disband++)yield return new Move("artifact",id,"burn",Targets:disband,Input:"offer="+offered.Id);continue;}
            int count=PveArtifactInputs.ColorCount(id,burn);
            var colors=new List<string>{""};
            for(int i=0;i<count;i++)colors=colors.SelectMany(prefix=>Enum.GetValues<ManaColor>().Where(c=>id!="items_011"||(int)c<4).Select(c=>prefix+(prefix==""?"":",")+c)).ToList();
            foreach(var color in colors)for(int unit=0;unit<(PveArtifactInputs.IsAttachedBanner(id)&&!burn?3:1);unit++)
                for(int option=0;option<(id=="items_009"&&burn?6:id=="items_020"?2:1);option++)
                {
                    if(id=="items_001"&&burn)
                    {for(int targets=0;targets<EnemyMasks;targets++)yield return new Move("artifact",id,"burn",Parts:targets);}
                    else if(id is "items_008" or "items_016" && !burn)
                    {for(int mask=0;mask<256;mask++)yield return new Move("artifact",id,"normal",unit,mask,option);}
                    else yield return new Move("artifact",id,burn?"burn":"normal",unit,Option:option,Input:color==""?"":"colors="+color);
                }
        }
    }
    static IEnumerable<Move> UnitMoves(UnitCard unit)
    {
        for(int ability=0;ability<unit.Actions.Length;ability++)
        {
            var printed=unit.Actions[ability];
            int options=printed.Kind is UnitAbilityKind.AttackOrBlock or UnitAbilityKind.MovementOrInfluence or UnitAbilityKind.TransformAttacks?2:1;
            var inputs=new List<string>{""};
            if(printed.Kind==UnitAbilityKind.GainChosenTokens)
            {
                inputs.Clear();
                foreach(var first in Enum.GetValues<ManaColor>().Where(c=>c>=ManaColor.Red&&c<=ManaColor.Black))
                    foreach(var second in Enum.GetValues<ManaColor>().Where(c=>c>=ManaColor.Red&&c<=ManaColor.Black))inputs.Add("colors="+first+","+second);
            }
            bool needsTarget=printed.Kind is UnitAbilityKind.FreezeArmor or UnitAbilityKind.StopUnfortifiedEnemy or UnitAbilityKind.StopEnemy or UnitAbilityKind.DestroyBlockedEnemy or UnitAbilityKind.ReduceEnemyArmor or UnitAbilityKind.RemoveEnemyDefenses or UnitAbilityKind.WeakenEnemyAttack or UnitAbilityKind.InterceptEnemyAttack or UnitAbilityKind.ReadyUnit || printed.RemoveTargetFortifications || printed.RemoveTargetResistances;
            bool needsAttack=printed.Kind is UnitAbilityKind.WeakenEnemyAttack or UnitAbilityKind.InterceptEnemyAttack;
            for(int option=0;option<options;option++)for(int target=0;target<(needsTarget?(printed.Kind==UnitAbilityKind.ReadyUnit?3:EnemySlots):1);target++)for(int attack=0;attack<(needsAttack?4:1);attack++)
                for(int boost=0;boost<(printed.RedBlueBoost?4:1);boost++)foreach(var input in inputs)
                    yield return new Move("unit",unit.Id,ability.ToString(),target,attack,option,input+(boost==0?"":(input==""?"":";")+"boost="+boost));
        }
    }
    // Semantic inputs select actual references at execution. Identical unmodified
    // deeds/faces are interchangeable; never synthesize a material card or die.
    static IEnumerable<string> InputProfiles(string id)
    {
        yield return "";
        if(id=="advanced_card_024")
            foreach(var donor in Cards.Where(c=>c.Id is not ("advanced_card_024" or "advanced_card_030")))
                foreach(var choice in BattleCardChoice.For(donor.Id))foreach(var input in InputProfiles(donor.Id))
                    yield return "copy="+donor.Id+";borrow="+choice.Id+(input==""?"":";"+input);
        if(id=="advanced_card_012")
        {
            var colors=Enum.GetValues<ManaColor>();
            foreach(var color in colors)yield return "colors="+color;
            // Tokens are a multiset, so permutations of the same three colors
            // are identical original effects; retain every multiset and repeats.
            for(int first=0;first<colors.Length;first++)
                for(int second=first;second<colors.Length;second++)
                    for(int third=second;third<colors.Length;third++)
                    {
                        string tokens="colors="+colors[first]+","+colors[second]+","+colors[third];
                        yield return tokens;
                        foreach(var crystal in colors.Where(c=>(int)c<4))yield return tokens+";crystal="+crystal;
                    }
        }
        if(id is "magic_006" or "magic_018")foreach(var color in Enum.GetValues<ManaColor>().Where(c=>(int)c<4))yield return "colors="+color;
        if(id=="magic_012")foreach(var face in Enum.GetValues<ManaColor>().Where(c=>(int)c<4))yield return "dice="+face+";colors="+face;
        if(id=="magic_020")foreach(var unit in Units)for(int disband=-1;disband<3;disband++)yield return "offeredUnit="+unit.Id+";disband="+disband;
        if(id=="basic_card_023")
            foreach(var material in DeedIds.Concat(Items.Select(item=>item.Id)))yield return "material="+material;
        if(id is "advanced_card_020" or "advanced_card_029" or "advanced_card_025")foreach(var material in DeedIds)yield return "material="+material;
        if(id=="advanced_card_027")foreach(var material in Cards)foreach(var offered in Advanced)yield return "material="+material.Id+";advanced="+offered.Id;
        if(id=="advanced_card_030")foreach(var offered in Advanced)yield return "advanced="+offered.Id;
        if(id is "advanced_card_025" or "advanced_card_033")foreach(var spell in Spells)
        {
            yield return "spells="+spell.Id;
            if(id=="advanced_card_033")foreach(var second in Spells)yield return "spells="+spell.Id+","+second.Id;
        }
        if(id is "advanced_card_021" or "advanced_card_040")foreach(var color in Enum.GetValues<ManaColor>())yield return "colors="+color;
        if(id is "basic_card_009" or "basic_card_013")
            foreach(var face in Enum.GetValues<ManaColor>())foreach(var color in Enum.GetValues<ManaColor>())
            {
                yield return "dice="+face+";colors="+color;
                if(id=="basic_card_013")foreach(var face2 in Enum.GetValues<ManaColor>())foreach(var color2 in Enum.GetValues<ManaColor>())
                    yield return "dice="+face+","+face2+";colors="+color+","+color2;
            }
    }
    static BattleCardTarget InputTarget(string id,BattleCardChoice choice,string input)
    {
        if(id!="advanced_card_024"||input=="")return choice.Target;
        var fields=input.Split(';').Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
        return BattleCardChoice.For(fields["copy"]).Single(c=>c.Id==fields["borrow"]).Target;
    }
    static List<Move> BuildCatalog()
    {
        var a=new List<Move>{new("advance"),new("nextTurn"),new("fight"),new("rest"),new("headbandFinish")};
        for(int target=-1;target<EnemySlots;target++)a.Add(new("explosiveArmor",Targets:target));
        foreach(var color in Enum.GetValues<ManaColor>())a.Add(new("source",Option:(int)color));
        foreach(var face in Enum.GetValues<ManaColor>())foreach(var color in Enum.GetValues<ManaColor>())
            if(face!=ManaColor.Gold || color==ManaColor.Gold)a.Add(new("extraSource",Targets:(int)face,Option:(int)color));
        foreach(var card in Advanced)foreach(bool strong in new[]{false,true})a.Add(new("learn",card.Id,Option:strong?1:0));
        foreach(var id in DeedIds){a.Add(new("rest",id));a.Add(new("sideways",id));foreach(var choice in BattleCardChoice.For(id))foreach(var input in Profiles[id])
            for(int target=0;target<(InputTarget(id,choice,input)!=BattleCardTarget.None?EnemySlots:1);target++)
                for(int attack=0;attack<(InputTarget(id,choice,input)!=BattleCardTarget.None?4:1);attack++)a.Add(new("play",id,choice.Id,Targets:target,Option:attack,Input:input));}
        foreach(var donor in Cards.Where(c=>c.Id is not ("advanced_card_024" or "advanced_card_030")))
            foreach(var choice in BattleCardChoice.For(donor.Id))foreach(var input in Profiles[donor.Id])
                for(int target=0;target<(choice.Target!=BattleCardTarget.None?EnemySlots:1);target++)
                    for(int attack=0;attack<(choice.Target!=BattleCardTarget.None?4:1);attack++)a.Add(new("repeat",donor.Id,choice.Id,Targets:target,Option:attack,Input:input));
        for(int target=0;target<EnemySlots;target++)for(int attack=0;attack<4;attack++){a.Add(new("block",Targets:target,Option:attack));a.Add(new("cumbersome",Targets:target,Option:attack));}
        for(int target=0;target<EnemySlots;target++)for(int attack=0;attack<4;attack++)for(int parts=1;parts<256;parts++)
            a.Add(new("blockSelected",Targets:target,Parts:parts,Option:attack));
        foreach(var skill in Skills)
            foreach(int option in SkillOptions(skill))
                for(int target=0;target<(BattleSkillChoice.NeedsEnemy(skill.SourceId)?EnemySlots:skill.SourceId=="hero_skill_pool_002:02"?3:1);target++)
                    for(int attack=0;attack<(BattleSkillChoice.NeedsAttack(skill.SourceId,option)?4:1);attack++)a.Add(new("skill",skill.SourceId,Targets:target,Parts:attack,Option:option));
        foreach(var skill in Skills.Where(s=>IsSidewaysSkill(s.SourceId)))a.AddRange(SidewaysSkillMoves(skill));
        for(int targets=1;targets<EnemyMasks;targets++)a.Add(new("skillFork",ForkedLightningActions.SourceId,Targets:targets));
        for(int targets=1;targets<EnemyMasks;targets++)for(int i=1;i<256;i++)a.Add(new("attack",Targets:targets,Parts:i));
        for(int part=0;part<8;part++){a.Add(new("skillBullseye","hero_skill_pool_004:04",Parts:part));a.Add(new("soulGuideBlock","hero_skill_pool_003:00",Parts:part));}
        a.Add(new("clearDefend"));
        a.AddRange(Units.SelectMany(UnitMoves));
        a.AddRange(Units.SelectMany(LeadershipMoves));
        a.AddRange(Units.SelectMany(UnitMoves).Select(a=>a with{Kind="offeredUnit"}));
        foreach(var item in Items){a.Add(new("rest",item.Id));a.Add(new("sideways",item.Id));a.AddRange(ArtifactMoves(item.Id));}
        foreach(var item in Items.Where(i=>i.Id!="items_024"))a.AddRange(ArtifactMoves(item.Id).Select(m=>m with{Kind="boxUse"}));
        for(int target=0;target<EnemySlots;target++)for(int attack=0;attack<4;attack++)foreach(int clear in new[]{0,1})a.Add(new("unitDamage",Targets:target,Parts:attack,Option:clear));
        for(int target=0;target<EnemySlots;target++)for(int attack=0;attack<4;attack++)foreach(var order in DamageOrders)a.Add(new("unitDamage",Choice:order,Targets:target,Parts:attack));
        for(int target=-1;target<3;target++)a.Add(new("heal",Targets:target));
        for(int targets=1;targets<EnemyMasks;targets++)for(int source=0;source<EnemySlots;source++)for(int target=0;target<EnemySlots;target++)a.Add(new("defend",Targets:targets,Parts:source,Option:target));
        for(int part=0;part<8;part++)for(int choice=0;choice<3;choice++)a.Add(new("bowChoice",Targets:part,Option:choice));
        for(int target=0;target<EnemySlots;target++)foreach(var color in Enum.GetValues<ManaColor>().Where(c=>(int)c<4))a.Add(new("killCrystal",Targets:target,Option:(int)color));
        for(int unit=0;unit<3;unit++)
        {
            a.Add(new("bannerUse","items_003",Targets:unit));
            for(int use=0;use<2;use++)a.Add(new("bannerUse","items_018",Targets:unit,Option:use));
            for(int enemy=0;enemy<EnemySlots;enemy++)for(int attack=0;attack<4;attack++)a.Add(new("bannerUse","items_001",Targets:unit,Parts:enemy,Option:attack));
        }
        for(int use=0;use<2;use++)a.Add(new("protectionCleanup","items_002",Option:use));
        foreach(var enemy in Enemies)
        {
            a.Add(new("puppetKeep",PuppetMasterActions.SkillId,enemy.Id));
            for(int mode=1;mode<=2;mode++)a.Add(new("puppetUse",PuppetMasterActions.SkillId,enemy.Id,Option:mode));
        }
        a.Add(new("roundFinish"));
        for(int unit=0;unit<3;unit++)for(int take=0;take<2;take++)a.Add(new("roundReturn",Targets:unit,Option:take));
        if(NativePayments)a.AddRange(PaymentMoves());
        if(NativeConversions)a.AddRange(ConversionMoves());
        if(NativeEnergyTargets)a.AddRange(EnergyTargetMoves());
        if(NativeSharedSkills)a.AddRange(SharedSkillMoves());
        if(NativeShapeshift)a.AddRange(ShapeshiftMoves());
        if(NativeWorld)a.AddRange(WorldMoves());
        return a.DistinctBy(x=>x.Key).OrderBy(x=>x.Key).ToList();
    }
    // Actual owned unit slots; original outside-play API validates exhaustion,
    // ownership and printed payment. No targets are synthesized or readied here.
    static IEnumerable<Move> EnergyTargetMoves()
    {
        foreach(var choice in new[]{"base","strong"})for(int unit=0;unit<3;unit++)
            yield return new Move("play","magic_000",choice,Input:"unit="+unit);
    }
    static IEnumerable<Move> LeadershipMoves(UnitCard unit)=>UnitMoves(unit).Where(a=>
        unit.Actions[int.Parse(a.Choice)].Kind==UnitAbilityKind.Block ||
        unit.Actions[int.Parse(a.Choice)].Kind==UnitAbilityKind.AttackOrBlock&&a.Option==1 ||
        (unit.Actions[int.Parse(a.Choice)].Kind==UnitAbilityKind.Attack||unit.Actions[int.Parse(a.Choice)].Kind==UnitAbilityKind.AttackOrBlock&&a.Option==0)
            &&unit.Actions[int.Parse(a.Choice)].AttackType!=AttackType.Siege).Select(a=>a with{Kind="unitLeadership"});
    static bool IsSidewaysSkill(string id)=>id is "hero_skill_pool_007:06" or "hero_skill_pool_007:07" or "hero_skill_pool_005:07";
    static IEnumerable<Move> SidewaysSkillMoves(SkillCard skill)
    {
        for(int slot=0;slot<8;slot++)foreach(var type in Enum.GetValues<SidewaysType>())
        {
            var payments=skill.SourceId=="hero_skill_pool_005:07"?Enum.GetValues<ManaColor>().Select(c=>(int)c):skill.SourceId=="hero_skill_pool_007:07"?new[]{0,1}:new[]{0};
            foreach(int payment in payments)yield return new("skillSideways",skill.SourceId,type.ToString(),Parts:slot,Option:payment);
        }
    }
    static IEnumerable<int> SkillOptions(SkillCard skill)
    {
        var options=BattleSkillChoice.For(skill.SourceId).Select(c=>c.Option);
        if(skill.SourceId==HeroSocialSkillRules.Disguise)options=options.Concat(new[]{0,1,100});
        if(skill.SourceId==HeroSocialSkillRules.LoneWolf)options=options.Concat(new[]{0,1});
        if(skill.SourceId=="hero_skill_pool_003:07")options=options.Concat(Enumerable.Range(0,6));
        if(skill.SourceId=="hero_skill_pool_004:05")options=options.Concat(Enum.GetValues<Ability>().Select(a=>100+(int)a)).Concat(Enum.GetValues<Element>().Select(e=>200+(int)e));
        return options.Distinct();
    }
    sealed partial class Env
    {
        public PveJourneySession Journey;
        public PveBattleSession B=>Journey.Battle;
        public int Steps,Turns=1,Kills,Rounds=1,PlanningRun;
        public bool RoundWindow;
        public Monster[] PendingEnemies;
        public IList<Monster> CurrentEnemies=>B?.Enemies??PendingEnemies;
        public Monster PendingEnemy=>PendingEnemies[0];
        public readonly List<string> Acquired=new();
        public ActionContext Context=>Journey.Context??ObservationOnlyContext;
        IEnumerable<DeedCard> OwnedCards()=>Journey.Player.Deck.Hand.Concat(Journey.Player.Deck.DrawPile)
            .Concat(Journey.Player.Deck.PlayedCards).Concat(Journey.Player.Deck.DiscardPile).Concat(Journey.Player.Deck.RemovedCards)
            .Concat(Journey.Player.Deck.RoundAsideCards).Concat(Journey.Player.Deck.TacticAsideCards).Concat(Journey.Player.Units.Where(u=>u.Banner!=null).Select(u=>u.Banner));
        public int ActiveWounds=>OwnedCards().Where(c=>c.Type==CardType.Wound&&!Journey.Player.Deck.RemovedCards.Any(removed=>ReferenceEquals(removed,c))).Distinct(ReferenceEqualityComparer.Instance).Count();
        readonly Dictionary<DeedCard,int> identities=new(ReferenceEqualityComparer.Instance);
        readonly Dictionary<Monster,int> enemyIdentities=new(ReferenceEqualityComparer.Instance);
        int CardIdentity(DeedCard c){if(!identities.TryGetValue(c,out int id)){id=identities.Count;identities[c]=id;}return id;}
        int EnemyIdentity(Monster e){if(!enemyIdentities.TryGetValue(e,out int id)){id=enemyIdentities.Count;enemyIdentities[e]=id;}return id;}
        object PuppetToken(Monster enemy)=>new{instance=EnemyIdentity(enemy),id=enemy.Id,armor=enemy.Armor,
            attacks=Enumerable.Range(0,enemy.AttackCount).Select(i=>new{value=enemy.GetAttack(i).Value,element=enemy.GetAttack(i).Element.ToString()}).ToArray(),
            abilities=enemy.Abilities.Select(a=>a.ToString()).ToArray()};
        IEnumerable<Move> PuppetCandidates()
        {
            if(!Journey.Player.Skills.Any(s=>s.SourceId==PuppetMasterActions.SkillId))yield break;
            foreach(var enemy in Journey.Player.PuppetTokens.DefeatedThisTurn.DistinctBy(e=>e.Id))yield return new("puppetKeep",PuppetMasterActions.SkillId,enemy.Id);
            if(B?.Phase is Phase.Block or Phase.Melee)
                foreach(var enemy in Journey.Player.PuppetTokens.KeptTokens.DistinctBy(e=>e.Id))yield return new("puppetUse",PuppetMasterActions.SkillId,enemy.Id,Option:B.Phase==Phase.Block?2:1);
        }
        public bool Done;
        PveDeedSelection ResolveInput(Move action,DeedCard host)
        {
            if(action.Input=="")return null;
            var fields=action.Input.Split(';').Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
            if(action.Card=="advanced_card_024")
            {
                var donor=Journey.Player.Deck.Hand.FirstOrDefault(c=>c.Id==fields.GetValueOrDefault("copy")&&c.Type==CardType.Action&&!ReferenceEquals(c,host))
                    ??throw new InvalidOperationException("Copy input must choose an actual other original action in hand");
                string nested=string.Join(";",fields.Where(f=>f.Key is not ("copy" or "borrow")).Select(f=>f.Key+"="+f.Value));
                var borrowed=ResolveInput(action with{Card=donor.Id,Input=nested},donor)
                    ??new PveDeedSelection{TargetIndex=action.Targets,AttackIndex=action.Option};
                return new PveDeedSelection{Cards=new[]{donor},BorrowedChoice=fields.GetValueOrDefault("borrow"),BorrowedInputs=borrowed};
            }
            if(action.Card=="magic_012" && fields.TryGetValue("dice",out var claimFace) && (!fields.TryGetValue("colors",out var claimColor)||claimFace!=claimColor))
                throw new InvalidOperationException("Mana Claim reserves the selected basic-color face and gains that same color");
            var dice=new List<ManaDie>();
            if(fields.TryGetValue("dice",out var faces))foreach(var face in faces.Split(',').Select(Enum.Parse<ManaColor>))
            {
                var die=Context.ManaSource.Dice.FirstOrDefault(d=>d.Face==face&&!dice.Contains(d));
                if(die==null)throw new InvalidOperationException("Selected real source faces are unavailable");
                dice.Add(die);
            }
            var materials=new List<DeedCard>();
            if(fields.TryGetValue("material",out var material))
            {
                var actual=Journey.Player.Deck.Hand.FirstOrDefault(c=>c.Id==material&&!ReferenceEquals(c,host));
                if(actual==null)throw new InvalidOperationException("Selected actual material is unavailable");
                materials.Add(actual);
            }
            AdvActionCard advanced=null;
            if(fields.TryGetValue("advanced",out var advancedId))advanced=Context.AdvActionSupply.Offer.FirstOrDefault(c=>c.Id==advancedId)??throw new InvalidOperationException("Selected real advanced supply is unavailable");
            var spells=new List<SpellCard>();
            if(fields.TryGetValue("spells",out var spellIds))foreach(var id in spellIds.Split(','))spells.Add(Context.SpellSupply.Offer.FirstOrDefault(c=>c.Id==id)??throw new InvalidOperationException("Selected real spell supply is unavailable"));
            var offered=fields.TryGetValue("offeredUnit",out var offeredId)?Context.UnitOffer.FirstOrDefault(u=>u.Id==offeredId)??throw new InvalidOperationException("Selected actual unit supply is unavailable"):null;
            var disband=fields.TryGetValue("disband",out var disbandIndex)&&int.Parse(disbandIndex)>=0?Journey.Player.Units.ElementAtOrDefault(int.Parse(disbandIndex))??throw new InvalidOperationException("Selected actual replacement unit is unavailable"):null;
            return new PveDeedSelection{Dice=dice.ToArray(),Cards=materials.ToArray(),Advanced=advanced,Spells=spells.ToArray(),OfferedUnit=offered,DisbandUnit=disband,TargetIndex=action.Targets,AttackIndex=action.Option,Crystal=fields.TryGetValue("crystal",out var crystal)?Enum.Parse<ManaColor>(crystal):null,Colors=fields.TryGetValue("colors",out var colors)?colors.Split(',').Select(Enum.Parse<ManaColor>).ToArray():Array.Empty<ManaColor>()};
        }
        bool InputMatchesChoice(Move action,BattleCardChoice choice)
        {
            if(action.Input=="")return true;
            var fields=action.Input.Split(';').Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
            bool strong=choice.Enhanced||Context.NextCardEnhanced;
            if(action.Card=="advanced_card_024")
            {
                string donor=fields.GetValueOrDefault("copy");
                return donor!=null&&Journey.Player.Deck.Hand.Any(c=>c.Id==donor&&c.Type==CardType.Action)
                    &&PveCopyInputs.Choices(Context,donor,strong).Any(c=>c.Id==fields.GetValueOrDefault("borrow"));
            }
            if(fields.ContainsKey("crystal")&&(action.Card!="advanced_card_012"||!strong))return false;
            if(action.Card=="magic_020")return strong&&fields.ContainsKey("offeredUnit");
            if(action.Card=="advanced_card_025")return strong?fields.ContainsKey("spells"):fields.ContainsKey("material");
            if(action.Card=="advanced_card_030")return !strong&&fields.ContainsKey("advanced");
            if(action.Card=="advanced_card_033")return fields.TryGetValue("spells",out var ids)&&ids.Split(',').Length==(strong?2:1);
            int needed=PveDeedInputs.ColorCount(action.Card,strong);
            if((fields.TryGetValue("colors",out var colors)?colors.Split(',').Length:0)!=needed)return false;
            if(action.Card is "basic_card_009" or "basic_card_013")
                return (fields.TryGetValue("dice",out var dice)?dice.Split(',').Length:0)==needed;
            return true;
        }
        public string StopReason="";
        (DeedCard Material,Dictionary<string,string> Fields) ArtifactInput(Move action,DeedCard host)
        {
            var fields=action.Input.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
            DeedCard material=null;
            if(fields.TryGetValue("materialSlot",out var slot))
            {
                material=Journey.Player.Deck.Hand.Where(c=>!ReferenceEquals(c,host)).ElementAtOrDefault(int.Parse(slot));
                if(material==null)throw new InvalidOperationException("Selected actual artifact material slot is unavailable");
            }
            return(material,fields);
        }
        public readonly int Seed;
        public readonly bool IsOriginalOpening;
        public Env(int seed) : this(seed, null) { }
        public Env(int seed, TaskDefinition task)
        {
            task?.Validate();DeclaredTask=task;
            Seed=seed;var inventorySeed=task?.InventorySeed??seed;
            IsOriginalOpening=task?.OriginalOpening??(IncludeOriginalOpenings&&(seed/7)%4==0);
            var hero=task==null?OriginalHeroes.All[seed%7]:OriginalHeroes.All.Single(h=>h.Id==task.HeroId);
            if(Enemies.Any(e=>e.AttackCount>4))throw new InvalidOperationException("Expand the original attack-slot vocabulary before training");
            var p=hero.CreatePlayer(Basics,new Random(task?.ShuffleSeed??seed));
            if(task?.Build!=null)InitializeDeclaredBuild(p,task.Build);
            if(UnitBuilds&&!IsOriginalOpening&&task?.Build==null)
            {
                // Declared owned midgame inventory, not free opening recruitment.
                int count=PartyBuilds?1+inventorySeed%3:1;
                if(PartyBuilds){p.Fame=count==1?0:count==2?8:24;p.Armor=2+(p.Level+1)/4;}
                var random=new Random(inventorySeed^0x1357ab);var candidates=Units.ToList();
                for(int i=0;i<count;i++){int chosen=random.Next(candidates.Count);p.Units.Add(new UnitState(candidates[chosen]));candidates.RemoveAt(chosen);}
                if(p.FreeSlots<0)throw new InvalidOperationException("Unit curriculum exceeds original command slots");
            }
            if(VariedBuilds&&!IsOriginalOpening&&inventorySeed%3!=0&&task?.Build==null)
            {
                // A declared legal midgame inventory snapshot, not a claim that a
                // level-one opening grants these cards/resources. No foreign hero
                // basics, duplicate unique deeds, fabricated effects or free tokens.
                var random=new Random(inventorySeed^0x4d4b120);var deck=p.Deck.DrawPile.ToList();
                var advanced=Advanced.Where(c=>!string.IsNullOrWhiteSpace(c.Name)&&!string.IsNullOrWhiteSpace(c.BaseEffect)).ToArray();
                int count=inventorySeed%3==1?3:6;
                // Independent curriculum substream: decks must not be determined
                // by the enemy ID or a fixed stride through the card catalog.
                T[] Sample<T>(T[] source,int n)
                {
                    var pool=source.ToArray();
                    for(int i=pool.Length-1;i>0;i--){int j=random.Next(i+1);(pool[i],pool[j])=(pool[j],pool[i]);}
                    return pool.Take(n).ToArray();
                }
                foreach(var card in Sample(advanced,count))deck.Add(new DeedCard(card.Id,CardType.Action));
                foreach(var card in Sample(Spells,count/2+1))deck.Add(new DeedCard(card.Id,CardType.Spell));
                if(deck.Where(c=>c.Id.StartsWith("advanced_")||c.Type==CardType.Spell).Select(c=>c.Id).Distinct().Count()!=count+count/2+1)
                    throw new InvalidOperationException("Configured inventory repeats a unique original deed");
                for(int i=deck.Count-1;i>0;i--){int j=random.Next(i+1);(deck[i],deck[j])=(deck[j],deck[i]);}
                p.Deck.SetDeck(deck);
                foreach(var color in new[]{ManaColor.Red,ManaColor.Blue,ManaColor.Green,ManaColor.White})p.Mana.AddCrystal(color,random.Next(4));
            }
            if(ArtifactBuilds&&!IsOriginalOpening&&task?.Build==null)
            {
                var deck=p.Deck.DrawPile.ToList();var random=new Random(inventorySeed^0x2ab417);var choices=Items.ToList();
                for(int i=0;i<1+inventorySeed%2;i++){int chosen=random.Next(choices.Count);deck.Add(new DeedCard(choices[chosen].Id,CardType.Artifact));choices.RemoveAt(chosen);}
                for(int i=deck.Count-1;i>0;i--){int chosen=random.Next(i+1);(deck[i],deck[chosen])=(deck[chosen],deck[i]);}p.Deck.SetDeck(deck);
            }
            // One physical token per verified brown definition in this declared curriculum.
            // Box copy multiplicities remain unverified; never claim this is the full box supply.
            var summonSupply=new EnemyTokenSupply(Enemies.Where(e=>e.TokenColor==EnemyTokenColor.Brown||task?.OriginalWorld==true&&e.TokenColor==EnemyTokenColor.Green).Select(e=>e with{}),new Random(task?.SummonSeed??(seed^0x49b023)));
            var world=CreateOriginalWorld(task,summonSupply);
            Journey=new PveJourneySession(p,OriginalDeeds,map:world,artifacts:ArtifactBuilds?Items:null,spells:Spells,manaRandom:new Random(task?.ManaSeed??seed),tacticRandom:new Random(task?.RandomSeed??seed),supplyRandom:new Random(task?.SupplySeed??seed),originalSkills:CardJsonLoader.LoadHeroSkills(hero.SkillPoolId),progressionRandom:new Random(task?.ProgressionSeed??seed),roundShuffleRandom:new Random(task?.ShuffleSeed??seed),unitDeck:UnitBuilds?Units.Where(u=>!ArtifactBuilds||!p.Units.Any(owned=>ReferenceEquals(owned.Card,u))):null,enemySupply:summonSupply);
            if(task?.Build!=null)
            {
                if(task.Build.SkillIds.Length>p.Level/2)throw new InvalidOperationException("Declared skill count exceeds original resolved level allowance");
                var own=CardJsonLoader.LoadHeroSkills(hero.SkillPoolId).ToArray();
                foreach(var id in task.Build.SkillIds)HeroSkillAcquisition.Acquire(p,own.Single(s=>s.SourceId==id),new Random(task.ProgressionSeed),Journey.Game.Globals.Units);
                if(p.OrdinaryFreeSlots<0)throw new InvalidOperationException("Declared units exceed actual original ordinary command slots");
            }
            if(PartyBuilds&&!IsOriginalOpening&&task?.Build==null)
            {
                var ownSkills=CardJsonLoader.LoadHeroSkills(hero.SkillPoolId).ToList();var random=new Random(inventorySeed^0x7a771);
                for(int i=0;i<p.Level/2;i++)
                {int chosen=random.Next(ownSkills.Count);HeroSkillAcquisition.Acquire(p,ownSkills[chosen],random,Journey.Game.Globals.Units);ownSkills.RemoveAt(chosen);}
            }
            Journey.PrepareRound();ResolveUpkeep();Journey.SelectTactic("day_1");Journey.BeginTurn();if(ArtifactBuilds)Context.EffectRandom=new Random(EffectSeed);
            PendingEnemies=Encounter(0);
        }
        void ResolveUpkeep()
        {
            foreach(var unit in Journey.Player.Units.Where(u=>u.NeedsAttunementUpkeep).ToArray())
            {
                ManaColor? color=Journey.Player.Mana.Crystals.Where(x=>FamiliarActions.IsBasicColor(x.Key)&&x.Value>0).OrderBy(x=>x.Key).Select(x=>(ManaColor?)x.Key).FirstOrDefault();
                Journey.Game.ResolveFamiliarUpkeep(Journey.Player,unit,color);
            }
        }
        Monster[] Encounter(int offset)=>DeclaredTask==null
            ?Enumerable.Range(0,1+Seed%3).Select(i=>Enemies[((Seed/7)+offset+i*13)%Enemies.Length] with{}).ToArray()
            :DeclaredTask.EnemyGroups[TaskEncounterCursor].Select(id=>Enemies.Single(e=>e.Id==id) with{}).ToArray();
        object Zone(IEnumerable<DeedCard> cards)=>cards.Select(c=>new{instance=CardIdentity(c),id=c.Id,wound=c.Type==CardType.Wound}).OrderBy(c=>c.instance).ToArray();
        object RawState()=>new{explosiveArmorUses=Context.ExplosiveBoltArmorUses,setup=!IsOriginalOpening&&(UnitBuilds||VariedBuilds&&Seed%3!=0)?"configured_midgame_inventory":"original_level_one_start",hero=Journey.Player.HeroId,enemy=(B?.Enemies[0]??PendingEnemy).Id,phase=B?.Phase.ToString()??"Outside",
            units=Journey.Player.Units.Select(u=>new{id=u.Card.Id,ready=u.IsReady,wounds=u.Wounds,destroyed=u.IsDestroyed,level=u.Card.Level,attunement=u.AttunementColor?.ToString(),upkeep=u.NeedsAttunementUpkeep,banner=u.Banner?.Id,bannerEffect=u.BannerEffectId,bannerSpent=u.CourageBannerSpent||u.FortitudeBannerSpent,fortitudeSelected=u.FortitudeProtectionSelected,usedAbilities=Context.MastersUsedAbilities.TryGetValue(u,out var used)?used.OrderBy(x=>x).ToArray():Array.Empty<int>()}).ToArray(),commandSlots=Journey.Player.CommandSlots,healing=Context.Healing.Total,
            artifactFlags=new{infinite=Context.InfiniteMana.Select(c=>c.ToString()).OrderBy(x=>x).ToArray(),famePerSpell=Context.FamePerSpell.ToDictionary(x=>x.Key.ToString(),x=>x.Value),doublePhysical=Context.DoublePhysicalAttack,ignorePhysical=Context.IgnorePhysicalResist,starBow=Context.StarBowActive,swordFame=Context.SwordFamePerKill,bowFame=Context.BowFamePerKill,crystalsPerKill=Context.CrystalsPerKill,drawPerHeal=Context.DrawPerHeal,honor=Context.HonorBannerTurnBonus,blackWild=Context.BlackManaWild,protectionBurned=Context.ProtectionBannerBurned,removeNewWounds=Context.RemoveNewWoundsAtTurnEnd},lastManaRolls=Context.LastManaRolls.Select(c=>c.ToString()).ToArray(),
            unitOffer=Context.UnitOffer.Select(u=>u.Id).ToArray(),offeredUnitUses=Context.OfferedUnitUses,
            publicSkills=Context.SkillSupply.Offer.Select(s=>s.SourceId).ToArray(),
            hardened=new{enemy=Context.HardenedEnemy==null?-1:EnemyIdentity(Context.HardenedEnemy),attack=Context.HardenedAttack,physical=Context.IgnorePhysicalDamage,nonPhysical=Context.IgnoreNonPhysicalDamage},
            pendingHeadband=Context.PendingHeadbandBorrow==null?null:new{physical=CardIdentity(Context.PendingHeadbandBorrow.PhysicalCard),skill=Context.PendingHeadbandBorrow.Skill.SourceId,remaining=Context.PendingHeadbandBorrow.Remaining,completed=Context.PendingHeadbandBorrow.Completed},
            mysticBox=Context.MysticBox==null?null:new{physical=CardIdentity(Context.MysticBox.Box),revealed=Context.MysticBox.RevealedArtifact.Id,used=Context.MysticBox.Used},artifactSupply=Context.ArtifactSupply.Count,
            unitDamagePlan=B?.DamagePlan.Select(a=>new{target=a.TargetIndex,attack=a.AttackIndex,units=a.Units.Select(u=>u.Card.Id).ToArray()}).ToArray(),
            unitModifiers=new{resistAll=Context.UnitsResistAll,attack=Context.UnitAttackBonus,block=Context.UnitBlockBonus,armor=Context.UnitArmorBonus,charge=Context.ChargeUnitBonus,allColdFire=Context.AllAttacksColdFire,allSiege=Context.AllAttacksSiege,otherAttack=Journey.Player.Units.Select(u=>new{id=u.Card.Id,bonus=Context.OtherUnitAttackBonus(u)}).ToArray()},
            hand=Journey.Player.Deck.Hand.Select(c=>c.Id).ToArray(),handOrder=Journey.Player.Deck.Hand.Select(CardIdentity).ToArray(),deck=Journey.Player.Deck.DrawPileCount,
            wounds=Journey.Player.Wounds,totalWounds=ActiveWounds,fame=Journey.Player.Fame,level=Journey.Player.Level,fameLevel=Journey.Player.FameLevel,turns=Turns,rounds=Rounds,dayPart=Context.DayPart.ToString(),kills=Kills,won=B?.Won??false,stopReason=StopReason,
            attackResistanceFlags=new{ignoreAll=Context.IgnoreResist,ignorePhysical=Context.IgnorePhysicalResist},soulGuideBlockPending=Context.SoulGuideBlockPending,
            puppetCandidates=Journey.Player.PuppetTokens.DefeatedThisTurn.Select(PuppetToken).ToArray(),
            puppetStored=Journey.Player.PuppetTokens.KeptTokens.Select(PuppetToken).ToArray(),
            forgoSource=Journey.Player.ForgoSourceDiceThisTurn,sourceUsedThisTurn=Context.ManaSource?.WasUsedThisTurn(Journey.Player)??false,
            completedTurns=Journey.CompletedTurns,completedBattles=Journey.CompletedBattles,rested=Context.RestedThisTurn,inBattle=Context.InBattle,turnActionCommitted=Context.TurnActionCommitted,activity=Journey.Player.Deck.CardsPlayedOrDiscardedThisTurn,planningRun=PlanningRun,roundWindow=RoundWindow,returningBanners=Journey.Player.Units.Select(u=>Journey.IsBannerReturning(Journey.Player,u)).ToArray(),
            tokens=Journey.Player.Mana.Tokens.ToDictionary(x=>x.Key.ToString(),x=>x.Value),crystals=Journey.Player.Mana.Crystals.ToDictionary(x=>x.Key.ToString(),x=>x.Value),
            source=Context.ManaSource.Dice.Select(d=>d.Face.ToString()).OrderBy(x=>x).ToArray(),held=Journey.Player.HeldManaDie?.Face.ToString(),
            extraDice=Context.ExtraManaDice,extraBlackDice=Context.ExtraBlackManaDice,
            movement=Context.MovementPool,influence=Context.InfluencePool,learning=Context.LearningModes,
            advancedOffer=Context.AdvActionSupply.Offer.Select(c=>c.Id).ToArray(),spellOffer=Context.SpellSupply.Offer.Select(c=>c.Id).ToArray(),spellColors=Context.SpellColors.Select(c=>c.ToString()).OrderBy(c=>c).ToArray(),
            claimedDice=Context.ManaSource.TurnClaims.Where(x=>ReferenceEquals(x.Value,Journey.Player)).Select(x=>x.Key.Face.ToString()).OrderBy(x=>x).ToArray(),
            roundClaimedDice=Context.ManaSource.RoundClaims.Where(x=>ReferenceEquals(x.Value,Journey.Player)).Select(x=>x.Key.Face.ToString()).OrderBy(x=>x).ToArray(),tokensPerTurn=Journey.Player.TokensPerTurn.ToDictionary(x=>x.Key.ToString(),x=>x.Value),
            nextEnhanced=Context.NextCardEnhanced,boost=Context.BoostValue,
            auditDrawOrder=Journey.Player.Deck.DrawPile.Select(CardIdentity).ToArray(),auditNextDrawBonus=Context.NextDrawBonus,
            block=Context.CombatPower.BlockTotal,
            acquired=Acquired.ToArray(),skills=Journey.Player.Skills.Select(s=>s.SourceId).ToArray(),
            usedSkillsTurn=Journey.Player.SkillsUsedThisTurn.OrderBy(x=>x).ToArray(),usedSkillsRound=Journey.Player.SkillsUsedThisRound.OrderBy(x=>x).ToArray(),flippedSkills=Journey.Player.FlippedSkills.OrderBy(x=>x).ToArray(),
            repeated=Context.PendingRepeatedEffect==null?null:new{original=Context.PendingRepeatedEffect.OriginalCardId,enhanced=Context.PendingRepeatedEffect.Enhanced,remaining=Context.PendingRepeatedEffect.Remaining,completed=Context.PendingRepeatedEffect.Completed},
            attacks=Context.CombatPower.AvailableAttacks(B?.Phase??Phase.End,Context.AllAttacksSiege).Select(a=>new{value=a.Value,element=a.Element.ToString(),type=a.Type.ToString(),card=(a.SourceCard as DeedCard)?.Id??"",cardInstance=a.SourceCard is DeedCard deed?CardIdentity(deed):-1,skill=a.SourceSkill?.SourceId??"",unit=a.SourceUnit?.Card.Id??"",requiredEnemy=a.RequiredEnemy==null?-1:EnemyIdentity(a.RequiredEnemy),starBowChoice=a.StarBowChoice,effectiveValue=CombatMath.AttackValue(a,B?.Phase??Phase.End,Context),effectiveElement=(Context.AllAttacksColdFire?Element.ColdFire:a.Element).ToString(),effectiveSiege=CombatMath.IsSiege(a,Context)}).ToArray(),
            killCrystalChoices=Context.KillCrystalChoices.ToDictionary(x=>x.Key.ToString(),x=>x.Value.ToString()),
            blocks=Context.CombatPower.Blocks.Select(b=>new{value=b.Value,element=b.Element.ToString(),distinctGroup=b.DistinctAttackGroup,skill=b.SourceSkill?.SourceId??"",card=(b.SourceCard as DeedCard)?.Id??"",cardInstance=b.SourceCard is DeedCard deed?CardIdentity(deed):-1}).ToArray(),
            monster=new{armor=(B?.Enemies[0]??PendingEnemy).Armor,abilities=(B?.Enemies[0]??PendingEnemy).Abilities.Select(a=>a.ToString()).ToArray()},
            enemies=CurrentEnemies.Select((e,i)=>new{index=i,instance=EnemyIdentity(e),id=e.Id,defeated=B!=null&&Context.DefeatedEnemyIndices.Contains(i),armor=e.Armor,effectiveArmor=B==null?(int?)null:CombatMath.EffectiveArmor(e,i,Context,B.Phase),defend=e.DefendValue,effectiveFortified=CombatMath.IsFortified(e,i,Context),fortificationRemoved=Context.FortificationsRemoved.Contains(i),skipped=Context.SkipAttackIndices.Contains(i),effectiveResistances=Enum.GetValues<Element>().Where(element=>CombatMath.HasResistance(e,i,element,Context)).Select(element=>element.ToString()).ToArray(),ignoredAbilities=e.Abilities.Where(a=>Context.InsightIgnores(e,a)).Select(a=>a.ToString()).ToArray(),abilities=e.Abilities.Select(a=>a.ToString()).ToArray(),attacks=Enumerable.Range(0,e.AttackCount).Select(j=>new{index=j,forcedUnit=Context.ForcedDamageUnit(e,j)?.Card.Id,effectiveAttack=CardCombatActions.DefenseProfile(e,i,Context,j).Attack,blocked=B?.IsBlocked(i,j)??false,cancelled=Context.IsAttackCancelled(i,j)}).ToArray()}).ToArray(),
            summoning=new{active=Context.Summoning.IsActive,bindings=Context.Summoning.Bindings.Select(b=>new{index=b.Index,originalIndex=b.OriginalIndex,summoner=EnemyIdentity(b.Summoner),summonerId=b.Summoner.Id,token=EnemyIdentity(b.Token),tokenId=b.Token.Id}).ToArray()},
            enemySupply=new{scope="one physical token per brown definition; box multiplicities unverified",draw=Journey.EnemySupply.DrawPile.Select(e=>new{instance=EnemyIdentity(e),id=e.Id}).ToArray(),discard=Journey.EnemySupply.DiscardPile.Select(e=>new{instance=EnemyIdentity(e),id=e.Id}).ToArray(),inUse=Journey.EnemySupply.InUse.Select(e=>new{instance=EnemyIdentity(e),id=e.Id}).ToArray()},
            defenderApplied=Context.EnemyDefense.Applied.Select(a=>new{source=EnemyIdentity(a.Source),target=EnemyIdentity(a.Target),value=a.Value}).ToArray(),
            defenderSelected=Context.EnemyDefense.Selected.Select(a=>new{source=EnemyIdentity(a.Source),target=EnemyIdentity(a.Target),value=a.Value}).ToArray(),
            // Audit-only, sorted physical IDs. The policy receives Features(), never these zones.
            zones=new{hand=Zone(Journey.Player.Deck.Hand),draw=Zone(Journey.Player.Deck.DrawPile),played=Zone(Journey.Player.Deck.PlayedCards),discard=Zone(Journey.Player.Deck.DiscardPile),removed=Zone(Journey.Player.Deck.RemovedCards),roundAside=Zone(Journey.Player.Deck.RoundAsideCards),tacticAside=Zone(Journey.Player.Deck.TacticAsideCards),banners=Zone(Journey.Player.Units.Where(u=>u.Banner!=null).Select(u=>u.Banner))}};
        public double[] Features()
        {
            var p=Journey.Player;var c=Context;var f=new List<double>();
            if(CurrentEnemies.Count>EnemySlots)throw new InvalidOperationException("Expand target schema before training this actual encounter");
            f.AddRange(OriginalHeroes.All.Select(h=>h.Id==p.HeroId?1.0:0));
            foreach(var phase in new[]{Phase.Ranged,Phase.Block,Phase.AssignDamage,Phase.Melee,Phase.End})f.Add(B?.Phase==phase?1:0);
            f.AddRange(new[]{Steps/96.0,Turns/3.0,p.Wounds/16.0,p.Fame/40.0,p.Armor/5.0,p.Deck.DrawPileCount/16.0});
            foreach(var color in Enum.GetValues<ManaColor>()){f.Add(p.Mana.Crystals.GetValueOrDefault(color)/3.0);f.Add(p.Mana.Tokens.GetValueOrDefault(color)/3.0);}
            foreach(var color in Enum.GetValues<ManaColor>())f.Add(c.ManaSource.Dice.Count(d=>d.Face==color)/3.0);
            f.Add(p.HeldManaDie!=null?1:0);
            foreach(var card in Basics)f.Add(p.Deck.Hand.Count(h=>h.Id==card.Id)/2.0);
            var enemy=B?.Enemies.FirstOrDefault()??PendingEnemies.FirstOrDefault();
            f.AddRange(Enemies.Select(e=>e.Id==enemy?.Id?1.0:0));
            f.Add((enemy?.Armor??0)/20.0);f.Add((enemy?.Attack??0)/20.0);f.Add(enemy!=null&&B?.IsBlocked(0)==true?1:0);f.Add(c.CombatPower.BlockTotal/20.0);
            var parts=c.CombatPower.AvailableAttacks(B?.Phase??Phase.End,c.AllAttacksSiege);
            for(int i=0;i<8;i++){f.Add(i<parts.Length?parts[i].Value/20.0:0);f.Add(i<parts.Length?(int)parts[i].Element/5.0:0);}
            f.Add(B==null?1:0);f.Add(Rounds/2.0);f.Add(c.DayPart==DayPart.Night?1:0);f.Add(c.RestedThisTurn?1:0);
            foreach(var card in Advanced)f.Add(p.Deck.Hand.Count(h=>h.Id==card.Id)/2.0);
            f.Add(p.Level/10.0);
            foreach(var skill in Skills){f.Add(p.Skills.Any(s=>s.SourceId==skill.SourceId)?1:0);f.Add(p.SkillsUsedThisTurn.Contains(skill.SourceId)?1:0);f.Add(p.SkillsUsedThisRound.Contains(skill.SourceId)?1:0);f.Add(p.FlippedSkills.Contains(skill.SourceId)?1:0);}
            for(int i=0;i<EnemySlots;i++)
            {
                var actual=i<CurrentEnemies.Count?CurrentEnemies[i]:null;
                f.Add(actual!=null?1:0);f.Add(actual!=null&&B!=null&&Context.DefeatedEnemyIndices.Contains(i)?1:0);
                f.AddRange(Enemies.Select(e=>actual?.Id==e.Id?1.0:0));f.Add((actual?.Armor??0)/20.0);
                foreach(var ability in Enum.GetValues<Ability>())f.Add(actual?.Abilities.Contains(ability)==true?1:0);
                for(int j=0;j<4;j++)
                {
                    bool present=actual!=null&&j<actual.AttackCount;f.Add(present?1:0);f.Add(present&&B?.IsBlocked(i,j)==true?1:0);
                    var profile=present&&B!=null?CardCombatActions.DefenseProfile(actual,i,Context,j):null;
                    f.Add(profile!=null?profile.Attack/20.0:0);f.Add(profile!=null?(int)profile.AttackElement/5.0:0);
                }
            }
            for(int i=0;i<8;i++)
            {
                var part=i<parts.Length?parts[i]:null;
                f.AddRange(Skills.Select(s=>part?.SourceSkill?.SourceId==s.SourceId?1.0:0));
                for(int j=0;j<EnemySlots;j++)f.Add(part?.RequiredEnemy!=null&&j<CurrentEnemies.Count&&ReferenceEquals(part.RequiredEnemy,CurrentEnemies[j])?1:0);
                foreach(var type in Enum.GetValues<AttackType>())f.Add(part!=null&&part.Type==type?1:0);
            }
            for(int i=0;i<EnemySlots;i++)
            {
                var actual=i<CurrentEnemies.Count?CurrentEnemies[i]:null;
                f.Add(actual!=null&&B!=null?CombatMath.EffectiveArmor(actual,i,Context,B.Phase)/20.0:0);
                foreach(var ability in Enum.GetValues<Ability>())f.Add(actual!=null&&Context.InsightIgnores(actual,ability)?1:0);
                for(int j=0;j<EnemySlots;j++)
                {
                    var target=j<CurrentEnemies.Count?CurrentEnemies[j]:null;
                    f.Add(Context.EnemyDefense.Selected.Where(a=>ReferenceEquals(a.Source,actual)&&ReferenceEquals(a.Target,target)).Sum(a=>a.Value)/20.0);
                    f.Add(Context.EnemyDefense.Applied.Where(a=>ReferenceEquals(a.Source,actual)&&ReferenceEquals(a.Target,target)).Sum(a=>a.Value)/20.0);
                }
            }
            f.Add(Context.ExtraManaDice/3.0);f.Add(Context.ExtraBlackManaDice/3.0);
            foreach(var color in Enum.GetValues<ManaColor>())f.Add(Context.ManaSource.TurnClaims.Count(x=>ReferenceEquals(x.Value,p)&&x.Key.Face==color)/3.0);
            foreach(var spell in Spells)f.Add(p.Deck.Hand.Count(c=>c.Id==spell.Id)/2.0);
            foreach(var card in Advanced)f.Add(Context.AdvActionSupply.Offer.Any(c=>c.Id==card.Id)?1:0);
            foreach(var spell in Spells)f.Add(Context.SpellSupply.Offer.Any(c=>c.Id==spell.Id)?1:0);
            foreach(var color in Enum.GetValues<ManaColor>())f.Add(Context.SpellColors.Contains(color)?1:0);
            f.Add(Context.MovementPool/20.0);f.Add(Context.InfluencePool/20.0);f.Add(Context.LearningModes/3.0);
            if(UnitBuilds)
            {
                foreach(var unit in Units)
                {
                    var actual=p.Units.SingleOrDefault(u=>u.Card.Id==unit.Id);
                    f.Add(actual!=null?1:0);f.Add(actual?.CanActivate==true?1:0);f.Add((actual?.Wounds??0)/4.0);f.Add(actual?.IsDestroyed==true?1:0);
                    foreach(var color in Enum.GetValues<ManaColor>())f.Add(actual?.AttunementColor==color?1:0);
                    for(int ability=0;ability<6;ability++)f.Add(actual!=null&&Context.MastersUsedAbilities.TryGetValue(actual,out var used)&&used.Contains(ability)?1:0);
                }
                f.Add(Context.Healing.Total/10.0);
                for(int enemyIndex=0;enemyIndex<EnemySlots;enemyIndex++)for(int attack=0;attack<4;attack++)f.Add(B?.DamagePlan.Any(a=>a.TargetIndex==enemyIndex&&a.AttackIndex==attack)==true?1:0);
                for(int part=0;part<8;part++)foreach(var unit in Units)f.Add(part<parts.Length&&parts[part].SourceUnit?.Card.Id==unit.Id?1:0);
                if(PartyBuilds)
                {
                    for(int slot=0;slot<3;slot++)foreach(var unit in Units)f.Add(slot<p.Units.Count&&p.Units[slot].Card.Id==unit.Id?1:0);
                    for(int enemyIndex=0;enemyIndex<EnemySlots;enemyIndex++)for(int attack=0;attack<4;attack++)for(int position=0;position<3;position++)for(int slot=0;slot<3;slot++)
                    {var plan=B?.DamagePlan.SingleOrDefault(a=>a.TargetIndex==enemyIndex&&a.AttackIndex==attack);f.Add(plan!=null&&position<plan.Units.Count&&slot<p.Units.Count&&ReferenceEquals(plan.Units[position],p.Units[slot])?1:0);}
                    f.Add(Context.UnitsResistAll?1:0);f.Add(Context.UnitAttackBonus/10.0);f.Add(Context.UnitBlockBonus/10.0);f.Add(Context.UnitArmorBonus/10.0);f.Add(Context.ChargeUnitBonus/10.0);f.Add(Context.AllAttacksColdFire?1:0);f.Add(Context.AllAttacksSiege?1:0);
                    foreach(var unit in Units)f.Add(p.Units.Where(u=>u.Card.Id==unit.Id).Sum(u=>Context.OtherUnitAttackBonus(u))/10.0);
                }
            }
            if(ArtifactBuilds)foreach(var item in Items)
            {
                f.Add(p.Deck.Hand.Count(c=>c.Id==item.Id));f.Add(p.Deck.RemovedCards.Count(c=>c.Id==item.Id));
                for(int unit=0;unit<3;unit++)f.Add(unit<p.Units.Count&&p.Units[unit].Banner?.Id==item.Id?1:0);
            }
            if(ArtifactBuilds)
            {
                foreach(var unit in Units)f.Add(Context.UnitOffer.Any(u=>u.Id==unit.Id)?1:0);
                foreach(var color in Enum.GetValues<ManaColor>()){f.Add(Context.InfiniteMana.Contains(color)?1:0);f.Add(Context.FamePerSpell.GetValueOrDefault(color)/3.0);}
                f.Add(Context.DoublePhysicalAttack?1:0);f.Add(Context.IgnorePhysicalResist?1:0);f.Add(Context.StarBowActive?1:0);f.Add(Context.SwordFamePerKill/3.0);f.Add(Context.BowFamePerKill/3.0);f.Add(Context.CrystalsPerKill/3.0);f.Add(Context.DrawPerHeal/3.0);f.Add(Context.HonorBannerTurnBonus/3.0);f.Add(Context.BlackManaWild?1:0);
                for(int unit=0;unit<3;unit++){f.Add(unit<p.Units.Count&&p.Units[unit].CourageBannerSpent?1:0);f.Add(unit<p.Units.Count&&p.Units[unit].FortitudeBannerSpent?1:0);f.Add(unit<p.Units.Count&&p.Units[unit].FortitudeProtectionSelected?1:0);}
                for(int slot=0;slot<8;slot++)
                {
                    var card=slot<p.Deck.Hand.Count?p.Deck.Hand[slot]:null;
                    foreach(var id in DeedIds.Concat(Items.Select(i=>i.Id)))f.Add(card?.Id==id?1:0);
                    f.Add(card?.Type==CardType.Wound?1:0);
                }
                for(int slot=0;slot<8;slot++)for(int choice=0;choice<3;choice++)f.Add(slot<parts.Length&&parts[slot].StarBowChoice==choice?1:0);
                for(int target=0;target<EnemySlots;target++)foreach(var color in Enum.GetValues<ManaColor>().Where(c=>(int)c<4))f.Add(Context.KillCrystalChoices.TryGetValue(target,out var selected)&&selected==color?1:0);
                for(int slot=0;slot<8;slot++)
                {
                    var block=slot<Context.CombatPower.Blocks.Count?Context.CombatPower.Blocks[slot]:null;
                    f.Add((block?.Value??0)/20.0);f.Add(block!=null?(int)block.Element/5.0:0);f.Add(block?.DistinctAttackGroup>0?1:0);
                    for(int other=0;other<8;other++)f.Add(block?.DistinctAttackGroup>0&&other<Context.CombatPower.Blocks.Count&&Context.CombatPower.Blocks[other].DistinctAttackGroup==block.DistinctAttackGroup?1:0);
                }
                for(int slot=0;slot<8;slot++){f.Add(slot<parts.Length?CombatMath.AttackValue(parts[slot],B?.Phase??Phase.End,Context)/20.0:0);f.Add(slot<parts.Length&&CombatMath.IsSiege(parts[slot],Context)?1:0);}
                for(int enemyIndex=0;enemyIndex<EnemySlots;enemyIndex++)
                {
                    f.Add(enemyIndex<CurrentEnemies.Count&&Context.SkipAttackIndices.Contains(enemyIndex)?1:0);
                    foreach(var element in Enum.GetValues<Element>())f.Add(enemyIndex<CurrentEnemies.Count&&CombatMath.HasResistance(CurrentEnemies[enemyIndex],enemyIndex,element,Context)?1:0);
                    for(int attack=0;attack<4;attack++)f.Add(enemyIndex<CurrentEnemies.Count&&Context.IsAttackCancelled(enemyIndex,attack)?1:0);
                }
                foreach(var color in Enum.GetValues<ManaColor>()){f.Add(p.TokensPerTurn.GetValueOrDefault(color)/3.0);f.Add(Context.ManaSource.RoundClaims.Count(x=>ReferenceEquals(x.Value,p)&&x.Key.Face==color)/3.0);}
                f.Add(Context.RemoveNewWoundsAtTurnEnd?1:0);
                f.Add(Math.Min(32,PlanningRun)/32.0);
                f.Add(RoundWindow?1:0);
                for(int unit=0;unit<3;unit++)f.Add(unit<p.Units.Count&&Journey.IsBannerReturning(p,p.Units[unit])?1:0);
                f.Add(Context.OfferedUnitUses/3.0);
                foreach(var unit in Units)f.Add(Context.UnitOffer.Any(u=>u.Id==unit.Id)?1:0);
            }
            if(ArtifactBuilds){f.Add(Context.MysticBox!=null?1:0);f.Add(Context.MysticBox?.Used==true?1:0);foreach(var item in Items)f.Add(Context.MysticBox?.RevealedArtifact.Id==item.Id?1:0);for(int slot=0;slot<3;slot++)foreach(var item in Items)f.Add(slot<p.Units.Count&&p.Units[slot].BannerEffectId==item.Id?1:0);}
            if(ArtifactBuilds)for(int target=0;target<EnemySlots;target++)for(int attack=0;attack<4;attack++)
            {
                var damageEnemy=B!=null&&target<B.Enemies.Count?B.Enemies[target]:null;
                var forced=damageEnemy!=null&&attack<damageEnemy.AttackCount?Context.ForcedDamageUnit(damageEnemy,attack):null;
                f.Add(damageEnemy!=null&&attack<damageEnemy.AttackCount?CardCombatActions.DefenseProfile(damageEnemy,target,Context,attack).Attack/20.0:0);
                for(int slot=0;slot<3;slot++)f.Add(forced!=null&&slot<p.Units.Count&&ReferenceEquals(p.Units[slot],forced)?1:0);
            }
            if(ArtifactBuilds)
            {
                foreach(var skill in Skills)f.Add(Context.SkillSupply.Offer.Any(s=>s.SourceId==skill.SourceId)?1:0);
                f.Add(Context.PendingHeadbandBorrow!=null?1:0);f.Add((Context.PendingHeadbandBorrow?.Remaining??0)/2.0);
                foreach(var skill in Skills)f.Add(Context.PendingHeadbandBorrow?.Skill.SourceId==skill.SourceId?1:0);
                f.Add(Context.IgnorePhysicalDamage/2.0);f.Add(Context.IgnoreNonPhysicalDamage);
                for(int target=0;target<EnemySlots;target++)for(int attack=0;attack<4;attack++)f.Add(B!=null&&target<B.Enemies.Count&&ReferenceEquals(B.Enemies[target],Context.HardenedEnemy)&&attack==Context.HardenedAttack?1:0);
            }
            if(ArtifactBuilds)f.Add(ActiveWounds/32.0);
            if(ArtifactBuilds){f.Add(p.ForgoSourceDiceThisTurn?1:0);f.Add(Context.ManaSource?.WasUsedThisTurn(p)==true?1:0);}
            if(ArtifactBuilds)f.Add(Context.SoulGuideBlockPending?1:0);
            if(ArtifactBuilds)foreach(var puppetTemplate in Enemies)
            {
                f.Add(p.PuppetTokens.DefeatedThisTurn.Count(e=>e.Id==puppetTemplate.Id)/3.0);
                f.Add(p.PuppetTokens.KeptTokens.Count(e=>e.Id==puppetTemplate.Id)/16.0);
            }
            // Public binding knowledge: remember the original enemy that returns after defense.
            for(int slot=0;slot<EnemySlots;slot++)
            {
                var binding=Context.Summoning.Bindings.FirstOrDefault(b=>b.Index==slot);
                f.Add(binding!=null?1:0);
                for(int original=0;original<3;original++)f.Add(binding?.OriginalIndex==original?1:0);
                foreach(var template in Enemies)f.Add(binding?.Summoner.Id==template.Id?1:0);
                f.Add((binding?.Summoner.Armor??0)/20.0);
            }
            // Public turn state: restoring Shaman Ritual spends the action even
            // though no card is played. The policy must observe that commitment.
            f.Add(c.TurnActionCommitted?1:0);
            f.Add(c.PendingRepeatedEffect!=null?1:0);
            f.Add((c.PendingRepeatedEffect?.Remaining??0)/3.0);
            f.Add(c.PendingRepeatedEffect?.Enhanced==true?1:0);
            foreach(var original in Cards)f.Add(c.PendingRepeatedEffect?.OriginalCardId==original.Id?1:0);
            if(NativePayments)AppendPaymentFeatures(f);
            if(NativeConversions)AppendConversionFeatures(f);
            f.Add(c.ExplosiveBoltArmorUses/6.0);
            // Public current fortification, including real removal/site permissions.
            for(int slot=0;slot<EnemySlots;slot++)
                f.Add(slot<CurrentEnemies.Count && CombatMath.IsFortified(CurrentEnemies[slot],slot,Context)?1:0);
            // Public task horizon/progress, not future enemy IDs or hidden shuffle.
            f.Add(DeclaredTask!=null?1:0);
            f.Add((DeclaredTask?.EnemyGroups.Length??0)/16.0);
            f.Add(TaskEncounterCursor/16.0);
            if(NativeSharedSkills)AppendSharedSkillFeatures(f);
            if(NativeWorld)AppendWorldFeatures(f);
            return f.ToArray();
        }
        public void Apply(Move a)
        {
            if(NativePayments&&ApplyPaymentSelection(a))return;
            if(PendingPayment!=null)
            {
                if(a.Kind is not ("play" or "unit"))throw new InvalidOperationException("Complete or cancel the selected native payment before another operation");
                var selection=PendingPayment;
                ManaSkillPaymentActions.Execute(Journey.Player,Context,selection,()=>ApplyCore(a));
                if(!SkillActions.IsUsed(Journey.Player,selection.Skill))throw new InvalidOperationException("Original consumer did not commit the selected native payment");
                PendingPayment=null;
                return;
            }
            ApplyCore(a);
        }
        void ApplyCore(Move a)
        {
            if(ApplyWorld(a))return;
            if(ApplyShapeshift(a))return;
            if(ApplySharedSkill(a))return;
            var p=Journey.Player;
            int beforeKills=B?.Context.DefeatedEnemyIndices.Count??0;
            switch(a.Kind)
            {
                case "repeat":
                    var pending=Context.PendingRepeatedEffect??throw new InvalidOperationException("No live repeated original effect");
                    if(pending.OriginalCardId!=a.Card)throw new InvalidOperationException("Repeated effect must use its actual original donor");
                    var repeatedInputs=ResolveInput(a,new DeedCard(a.Card,CardType.Action))??new PveDeedSelection{TargetIndex=a.Targets,AttackIndex=a.Option};
                    if(B==null)Journey.ContinueCopiedEffect(a.Choice,repeatedInputs);
                    else B.ContinueCopiedEffect(a.Choice,repeatedInputs);
                    break;
                case "fight":Journey.BeginBattle(PendingEnemies);break;
                case "roundReturn":Journey.ChooseBannerRoundReturn(p,p.Units[a.Targets],a.Option==1);break;
                case "roundFinish":
                    if(!RoundWindow)throw new InvalidOperationException("当前没有正式轮末选择窗口");
                    FinishRound();break;
                case "bannerUse":
                    var bannerUnit=p.Units[a.Targets];
                    if(bannerUnit.BannerEffectId!=a.Card)throw new InvalidOperationException("请选择该原旌旗附着的实际部队");
                    if(a.Card=="items_001")B.UseFearBanner(bannerUnit,a.Parts,a.Option);
                    else if(a.Card=="items_018")B.SelectFortitude(bannerUnit,a.Option==1);
                    else BannerActions.UseCourage(p,Context,bannerUnit);
                    break;
                case "protectionCleanup":BannerActions.ChooseProtectionCleanup(Context,a.Option==1);break;
                case "bowChoice":
                    var bowPart=Context.CombatPower.AvailableAttacks(B.Phase,Context.AllAttacksSiege)[a.Targets];
                    var rawIndex=Context.CombatPower.Attacks.Select((part,index)=>(part,index)).Single(x=>ReferenceEquals(x.part,bowPart)).index;
                    B.ChooseBowAttack(rawIndex,a.Option);break;
                case "killCrystal":B.ChooseKillCrystal(a.Targets,(ManaColor)a.Option);break;
                case "headbandFinish":Journey.FinishHeadbandBorrowIfImpossible();break;
                case "puppetKeep":
                    PuppetMasterActions.Keep(p,Context,p.Skills.Single(s=>s.SourceId==PuppetMasterActions.SkillId),p.PuppetTokens.DefeatedThisTurn.First(e=>e.Id==a.Choice));break;
                case "puppetUse":
                    B.UsePuppetToken(p.Skills.Single(s=>s.SourceId==PuppetMasterActions.SkillId),p.PuppetTokens.KeptTokens.First(e=>e.Id==a.Choice),a.Option);break;
                case "boxReveal":Journey.RevealBox(p.Deck.Hand.First(c=>c.Id=="items_024"));break;
                case "boxUse":
                case "artifact":
                    var artifact=a.Kind=="boxUse"?Context.MysticBox?.Box??throw new InvalidOperationException("No actual revealed box"):a.Card=="items_022"&&Context.PendingHeadbandBorrow!=null?Context.PendingHeadbandBorrow.PhysicalCard:p.Deck.Hand.First(c=>c.Id==a.Card);
                    if(a.Kind=="boxUse"&&(Context.MysticBox.Used||Context.MysticBox.RevealedArtifact.Id!=a.Card))throw new InvalidOperationException("Borrow only the actual unused revealed artifact");
                    bool burn=a.Choice=="burn";
                    if(a.Card=="items_022")
                    {
                        var offeredSkill=Context.SkillSupply.Offer.Single(s=>s.SourceId==a.Input.Substring("skill=".Length));
                        if(burn||a.Kind=="boxUse"||!HeadbandBorrowResolution.Handles(offeredSkill))
                        {
                            var priorEnemy=Context.SelectedEnemy;int priorAttack=Context.SelectedEnemyAttackIndex;
                            try
                            {
                                if(!burn&&BattleSkillChoice.NeedsEnemy(offeredSkill.SourceId))
                                {if(B==null||a.Targets>=B.Enemies.Count||a.Parts>=B.Enemies[a.Targets].AttackCount)throw new InvalidOperationException("Select an actual enemy attack for the borrowed public skill");Context.SelectedEnemy=B.Enemies[a.Targets];Context.SelectedEnemyAttackIndex=a.Parts;}
                                Journey.PlaySpecialArtifact(artifact,burn,skill:offeredSkill,skillOption:a.Option,enemy:a.Targets,unit:p.Units.ElementAtOrDefault(a.Parts));
                            }
                            finally {Context.SelectedEnemy=priorEnemy;Context.SelectedEnemyAttackIndex=priorAttack;}
                        }
                        else if(HeadbandBorrowResolution.IsFlip(offeredSkill))Journey.UseHeadbandFlip(artifact,offeredSkill,a.Option);
                        else if(HeadbandBorrowResolution.IsSocial(offeredSkill))Journey.UseHeadbandSocial(artifact,offeredSkill,a.Option);
                        else if(HeadbandBorrowResolution.IsUnit(offeredSkill))Journey.UseHeadbandUnit(artifact,offeredSkill,p.Units[a.Parts],a.Option==1);
                        else if(HeadbandBorrowResolution.IsWound(offeredSkill))Journey.UseHeadbandWound(artifact,offeredSkill,p.Deck.Hand[a.Parts],(ManaColor)a.Option);
                        else throw new InvalidOperationException("This original public skill needs its specialized real consumer input");
                        break;
                    }
                    if(a.Card=="items_023")
                    {
                        var selected=ArtifactInput(a,artifact);var fields=selected.Fields;
                        var spell=Context.SpellSupply.Offer.Single(c=>c.Id==fields["spellId"]);
                        var profile=string.Join(";",fields.Where(x=>x.Key is not ("materialSlot" or "spellId" or "spellChoice")).Select(x=>x.Key+"="+x.Value));
                        var definition=new Move("play",spell.Id,fields["spellChoice"],Targets:a.Targets,Input:profile);
                        var inputs=ResolveInput(definition,artifact);
                        Journey.PlayMaterialArtifact(artifact,burn,selected.Material,spell:spell,spellChoice:fields["spellChoice"],target:a.Targets,unit:p.Units.ElementAtOrDefault(a.Parts),inputs:inputs);
                        break;
                    }
                    if(a.Card is "items_013" or "items_021")
                    {
                        var selected=ArtifactInput(a,artifact);var fields=selected.Fields;
                        var ownedBefore=OwnedCards().ToHashSet(ReferenceEqualityComparer.Instance);
                        if(a.Card=="items_013")
                        {
                            string gained=fields[burn?"spells":"advanced"];
                            Journey.PlayMaterialArtifact(artifact,burn,selected.Material,
                                advanced:burn?null:Context.AdvActionSupply.Offer.Single(c=>c.Id==gained),
                                spell:burn?Context.SpellSupply.Offer.Single(c=>c.Id==gained):null);
                            foreach(var card in OwnedCards().Where(c=>!ownedBefore.Contains(c)))
                            {if(card.Id!=gained)throw new InvalidOperationException("Unrecorded original artifact acquisition: "+card.Id);Acquired.Add(card.Id);}
                        }
                        else Journey.PlayMaterialArtifact(artifact,burn,selected.Material,unit:p.Units.ElementAtOrDefault(a.Targets),first:int.Parse(fields["first"]),second:int.Parse(fields["second"]),crystal:(ManaColor)int.Parse(fields["color"]));
                        break;
                    }
                    if(a.Card=="items_017")
                    {Journey.PlaySpecialArtifact(artifact,burn,offer:burn?Context.UnitOffer.Single(c=>c.Id==a.Input.Substring("offer=".Length)):null,disband:a.Targets<0?null:p.Units.ElementAtOrDefault(a.Targets));break;}
                    var artifactColors=a.Input==""?Array.Empty<ManaColor>():a.Input.Substring("colors=".Length).Split(',').Select(Enum.Parse<ManaColor>).ToArray();
                    var attached=PveArtifactInputs.IsAttachedBanner(a.Card)&&!burn?p.Units.ElementAtOrDefault(a.Targets):null;
                    if(B==null&&a.Kind=="boxUse")Journey.PlaySpecialArtifact(artifact,burn,unit:attached,colors:artifactColors);
                    else if(B==null)Journey.PlayOutsideArtifact(artifact,burn,attached,artifactColors);
                    else if(PveArtifactInputs.IsResource(a.Card))B.PlayResourceArtifact(artifact,burn,artifactColors);
                    else if(a.Card=="items_009")B.PlayHorn(artifact,burn,a.Option);
                    else if(a.Card=="items_020")B.PlayShield(artifact,burn,a.Option==1);
                    else if(a.Card=="items_001"&&burn)B.BurnFearBanner(artifact,Enumerable.Range(0,B.Enemies.Count).Where(i=>(a.Parts&(1<<i))!=0).ToArray());
                    else B.PlayArtifact(artifact,burn,attached,p.Deck.Hand.Where(c=>!ReferenceEquals(c,artifact)).Take(8).Where((_,i)=>(a.Parts&(1<<i))!=0).ToArray());
                    break;
                case "offeredUnit":
                    var offer=Context.UnitOffer.Single(u=>u.Id==a.Card);var offerAbility=int.Parse(a.Choice);
                    var offerParams=a.Input.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
                    var offerColors=offerParams.TryGetValue("colors",out var rawOfferColors)?rawOfferColors.Split(',').Select(Enum.Parse<ManaColor>).ToArray():null;
                    int offerBoost=offerParams.TryGetValue("boost",out var rawOfferBoost)?int.Parse(rawOfferBoost):0;
                    var offerKind=offer.Actions[offerAbility].Kind;
                    bool enemyTarget=offerKind is UnitAbilityKind.FreezeArmor or UnitAbilityKind.StopUnfortifiedEnemy or UnitAbilityKind.StopEnemy or UnitAbilityKind.DestroyBlockedEnemy or UnitAbilityKind.ReduceEnemyArmor or UnitAbilityKind.RemoveEnemyDefenses or UnitAbilityKind.WeakenEnemyAttack or UnitAbilityKind.InterceptEnemyAttack || offer.Actions[offerAbility].RemoveTargetFortifications || offer.Actions[offerAbility].RemoveTargetResistances;
                    Journey.UseOfferedUnit(offer,offerAbility,a.Option,enemyTarget?a.Targets:-1,offerColors,offerBoost,offerKind==UnitAbilityKind.ReadyUnit?p.Units.ElementAtOrDefault(a.Targets):null,a.Parts);break;
                case "unitLeadership":
                case "unit":
                    var unit=p.Units.Single(u=>u.Card.Id==a.Card);var ability=int.Parse(a.Choice);
                    var parameters=a.Input.Split(';',StringSplitOptions.RemoveEmptyEntries).Select(s=>s.Split('=')).ToDictionary(s=>s[0],s=>s[1]);
                    var colors=parameters.TryGetValue("colors",out var rawColors)?rawColors.Split(',').Select(Enum.Parse<ManaColor>).ToArray():null;
                    int boost=parameters.TryGetValue("boost",out var rawBoost)?int.Parse(rawBoost):0;
                    if(B==null)Journey.UseOutsideUnit(unit,ability,a.Option,unitTarget:unit.Card.Actions[ability].Kind==UnitAbilityKind.ReadyUnit?p.Units.ElementAtOrDefault(a.Targets):null,manaColors:colors);
                    else B.UseUnit(unit,ability,a.Option,a.Targets,colors,boost,a.Parts,leadership:a.Kind=="unitLeadership"?p.Skills.Single(s=>s.SourceId=="hero_skill_pool_002:06"):null);
                    break;
                case "unitDamage":B.PlanDamage(a.Targets,a.Parts,a.Option==1?Array.Empty<UnitState>():a.Choice==""?p.Units.Where(u=>!u.IsDestroyed).ToArray():a.Choice.Select(c=>p.Units[c-'0']).ToArray());break;
                case "heal":if(B==null)Journey.HealOutsideBattle(a.Targets<0?null:p.Units[a.Targets]);else B.HealWound(a.Targets);break;
                case "rest":Journey.Rest(a.Card==""?null:p.Deck.Hand.First(c=>c.Id==a.Card),p.Deck.Hand.Where(c=>c.Type==CardType.Wound).Take(a.Card==""?1:int.MaxValue).ToArray());break;
                case "advance":B.Advance();break;
                case "explosiveArmor":B.ResolveExplosiveArmor(a.Targets);break;
                case "cumbersome":NatureRevengeActions.SpendMove(Context,B.Enemies[a.Targets],a.Option,1);break;
                case "source":var color=(ManaColor)a.Option;CardManaActions.UseSourceDie(p,Context,Context.ManaSource.Dice.First(d=>d.Face==color),color);break;
                case "extraSource":CardManaActions.UseSourceDie(p,Context,Context.ManaSource.Dice.First(d=>d.Face==(ManaColor)a.Targets),(ManaColor)a.Option,extra:true);break;
                case "play":
                    var host=p.Deck.Hand.First(c=>c.Id==a.Card);var input=ResolveInput(a,host);
                    var existing=OwnedCards().ToHashSet(ReferenceEqualityComparer.Instance);
                    if(NativeEnergyTargets&&a.Card=="magic_000"&&a.Input.StartsWith("unit="))
                    {
                        if(B!=null)throw new InvalidOperationException("Energy spells require actual outside battle state");
                        var selected=p.Units.ElementAtOrDefault(int.Parse(a.Input.Substring(5)))
                            ??throw new InvalidOperationException("Select an actual owned unit slot");
                        Journey.PlayOutsideBattle(host,a.Choice,selected);
                    }
                    else if(B==null)Journey.PlayOutsideWithInputs(host,a.Choice,input);
                    else B.Play(host,a.Choice,BattleCardChoice.For(a.Card).Single(c=>c.Id==a.Choice).Target!=BattleCardTarget.None?a.Targets:-1,a.Option,inputs:input);
                    var allowed=input==null?Array.Empty<string>():a.Card is "advanced_card_027" or "advanced_card_030"?new[]{input.Advanced?.Id}:a.Card=="advanced_card_025"?input.Spells.Select(s=>s.Id).ToArray():Array.Empty<string>();
                    foreach(var gained in OwnedCards().Where(c=>c.Type!=CardType.Wound&&!existing.Contains(c)))
                    {if(!allowed.Contains(gained.Id))throw new InvalidOperationException("Unrecorded original acquisition: "+gained.Id);Acquired.Add(gained.Id);}
                    break;
                case "learn":var obtained=Journey.PurchaseLearnedCard(Context.AdvActionSupply.Offer.Single(c=>c.Id==a.Card),a.Option==1);Acquired.Add(obtained.Id);break;
                case "sideways":B.PlaySideways(p.Deck.Hand.First(c=>c.Id==a.Card));break;
                case "skillBullseye":B.BoostCardAttack(p.Skills.Single(s=>s.SourceId==a.Card),Context.CombatPower.AvailableAttacks(B.Phase,Context.AllAttacksSiege)[a.Parts]);break;
                case "soulGuideBlock":B.ApplySoulGuideBlock(Context.CombatPower.Blocks[a.Parts]);break;
                case "skillFork":
                    B.UseForkedLightning(p.Skills.Single(s=>s.SourceId==a.Card),Enumerable.Range(0,B.Enemies.Count).Where(i=>(a.Targets&(1<<i))!=0).ToArray());break;
                case "skillSideways":
                    var sidewaysSkill=p.Skills.Single(s=>s.SourceId==a.Card);
                    var sidewaysCard=p.Deck.Hand[a.Parts];
                    var sidewaysType=Enum.Parse<SidewaysType>(a.Choice);
                    ManaColor? sidewaysColor=a.Card=="hero_skill_pool_005:07"?(ManaColor)a.Option:null;
                    bool forgo=a.Card=="hero_skill_pool_007:07"&&a.Option==1;
                    if(B==null)Journey.PlaySidewaysWithSkill(sidewaysSkill,sidewaysCard,sidewaysType,sidewaysColor,forgo);
                    else
                    {
                        if(sidewaysType!=(B.Phase==Phase.Block?SidewaysType.Block:SidewaysType.Attack))throw new InvalidOperationException("Actual battle phase must match selected sideways use");
                        B.PlaySidewaysWithSkill(sidewaysSkill,sidewaysCard,sidewaysColor,forgo);
                    }
                    break;
                case "skill":
                    var owned=p.Skills.Single(s=>s.SourceId==a.Card);var wound=p.Deck.Hand.FirstOrDefault(c=>c.Type==CardType.Wound);
                    if(B==null)Journey.UseOutsideSkill(owned,a.Option,target:owned.SourceId=="hero_skill_pool_002:02"?p.Units[a.Targets]:null,wound:wound);
                    else B.UseSkill(owned,a.Option,BattleSkillChoice.NeedsEnemy(owned.SourceId)?a.Targets:-1,wound,a.Parts);
                    break;
                case "convertMovement":B.ConvertMovement(a.Choice=="ranged",ConversionAmount(a));break;
                case "convertInfluence":B.ConvertInfluence(ConversionAmount(a));break;
                case "block":B.Block(a.Targets,a.Option);break;
                case "blockSelected":
                    var blockSources=Context.CombatPower.Blocks;
                    if(a.Parts<=0 || a.Parts>=(1<<Math.Min(8,blockSources.Count)))throw new InvalidOperationException("请选择当前实际格挡来源索引");
                    B.BlockSelected(a.Targets,blockSources.Where((_,i)=>i<8&&(a.Parts&(1<<i))!=0).ToArray(),a.Option);break;
                case "defend":B.ChooseDefend(a.Parts,a.Option,Enumerable.Range(0,B.Enemies.Count).Where(i=>(a.Targets&(1<<i))!=0).ToArray());break;
                case "clearDefend":B.ClearDefendChoices();break;
                case "attack":var parts=B.Context.CombatPower.AvailableAttacks(B.Phase,B.Context.AllAttacksSiege);B.AttackSelected(Enumerable.Range(0,B.Enemies.Count).Where(i=>(a.Targets&(1<<i))!=0).ToArray(),parts.Where((_,i)=>(a.Parts&(1<<i))!=0).ToArray());break;
                case "nextTurn":
                    var survivors=CurrentEnemies.Where((e,i)=>B==null||!Context.DefeatedEnemyIndices.Contains(i)).ToArray();
                    if(AtomicSingleBattle)
                    {
                        if(B?.Phase!=Phase.End)throw new InvalidOperationException("Single battle not formally ended");
                        Journey.EndTurn(p.Deck.Hand.Where(c=>c.Type!=CardType.Wound).ToArray());
                        while(Journey.Progression.Pending)
                        {
                            var skills=Journey.Progression.Choices.OrderBy(s=>s.SourceId).ToArray();
                            var reward=Journey.Progression.Actions[(ProgressionChoiceSeed+Turns)%Journey.Progression.Actions.Count];
                            Journey.ChooseLevelReward(skills[(ProgressionChoiceSeed+Turns)%skills.Length],reward);Acquired.Add(reward.Id);
                        }
                        if(p.ChaosUnusedRotationPending)ChaosSkillRules.ResolveUnusedRotation(p,false);
                        if(survivors.Length==0)TaskEncounterCursor=DeclaredTask.EnemyGroups.Length;
                        Done=true;StopReason=survivors.Length==0?"task_sequence_complete":"single_battle_lost";
                        break;
                    }
                    try{Journey.EndTurn(p.Deck.Hand.Where(c=>c.Type!=CardType.Wound).ToArray());}
                    catch(InvalidOperationException ex){Done=true;StopReason="formal_end_turn_denied: "+ex.Message;break;}
                    // Rewards are a deterministic legal curriculum choice, not a fabricated level increment.
                    while(Journey.Progression.Pending)
                    {
                        var choices=Journey.Progression.Choices.OrderBy(s=>s.SourceId).ToArray();
                        var skill=choices[(ProgressionChoiceSeed+Turns)%choices.Length];
                        var action=Journey.Progression.Actions[(ProgressionChoiceSeed+Turns)%Journey.Progression.Actions.Count];
                        Journey.ChooseLevelReward(skill,action);Acquired.Add(action.Id);
                    }
                    if(p.ChaosUnusedRotationPending)ChaosSkillRules.ResolveUnusedRotation(p,false);
                    if(DeclaredTask!=null&&survivors.Length==0)
                    {
                        TaskEncounterCursor++;
                        if(TaskEncounterCursor>=DeclaredTask.EnemyGroups.Length)
                        {Done=true;StopReason="task_sequence_complete";break;}
                    }
                    if(Turns>=16){Done=true;StopReason="turn_limit";break;}
                    PendingEnemies=survivors.Length==0?Encounter(Turns):survivors;
                    if(Journey.Game.CanAnnounceRoundEnd)
                    {
                        Journey.AnnounceRoundEnd();
                        if(ArtifactBuilds&&Journey.RoundBannerUnits(p).Length>0){RoundWindow=true;break;}
                        FinishRound();break;
                    }
                    BeginFollowingTurn();break;
                default:throw new InvalidOperationException(a.Kind);
            }
            if(a.Kind is not ("nextTurn" or "fight" or "roundFinish" or "roundReturn"))Kills+=Math.Max(0,(B?.Context.DefeatedEnemyIndices.Count??0)-beforeKills);
        }
        void BeginFollowingTurn()
        {
            Journey.BeginTurn();Turns++;if(ArtifactBuilds)Context.EffectRandom=new Random(EffectSeed^Turns);
        }
        void FinishRound()
        {
            Journey.EndRound();RoundWindow=false;
            if(Rounds>=2){Done=true;return;}
            Rounds++;Journey.PrepareRound();ResolveUpkeep();Journey.SelectTactic("night_1");BeginFollowingTurn();
        }
        public List<int> Legal()
        {
            if(Done)return new();
            if(RoundWindow)
            {
                var choices=Journey.Player.Units.SelectMany((u,i)=>new[]{new Move("roundReturn",Targets:i,Option:0),new Move("roundReturn",Targets:i,Option:1)}).Where(a=>a.Targets<3);
                return Preview(choices).Append(CatalogIndices[new Move("roundFinish").Key]).ToList();
            }
            if(B?.Phase==Phase.End)
            {
                var endActions=Preview(BannerCandidates().Concat(PuppetCandidates()).Concat(NativeSharedSkills?SharedSkillCandidates():Enumerable.Empty<Move>()));
                if(CanEndCurrentTurn())endActions.Add(Catalog.FindIndex(a=>a.Kind=="nextTurn"));
                return endActions.Distinct().ToList();
            }
            var candidates=new List<Move>();
            if(NativeConversions&&B!=null)candidates.AddRange(ConversionMoves());
            candidates.AddRange(BannerCandidates());
            candidates.AddRange(PuppetCandidates());
            if(NativeSharedSkills)candidates.AddRange(SharedSkillCandidates());
            if(NativeShapeshift)candidates.AddRange(ShapeshiftCandidates());
            if(NativeWorld)candidates.AddRange(WorldCandidates());
            if(B==null)
            {
                if(NativeEnergyTargets&&Journey.Player.Deck.Hand.Any(c=>c.Id=="magic_000"))
                    candidates.AddRange(EnergyTargetMoves());
                if(!Context.TurnActionCommitted)candidates.Add(new("fight"));
                if(!Context.TurnActionCommitted && Journey.Player.Deck.Hand.Any(c=>c.Type==CardType.Wound))candidates.Add(new("rest"));
                if(!Context.TurnActionCommitted)candidates.AddRange(Journey.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).DistinctBy(c=>c.Id).Select(c=>new Move("rest",c.Id)));
            }
            else candidates.Add(new("advance"));
            if(B!=null&&Context.ExplosiveBoltArmorUses>0)
                for(int target=-1;target<B.Enemies.Count;target++)candidates.Add(new("explosiveArmor",Targets:target));
            if(Context.PendingHeadbandBorrow!=null)candidates.Add(new("headbandFinish"));
            if(Context.OfferedUnitUses>0)candidates.AddRange(Context.UnitOffer.SelectMany(UnitMoves).Select(a=>a with{Kind="offeredUnit"}));
            if(ArtifactBuilds&&B!=null)
            {
                if(Context.StarBowActive&&(B.Phase==Phase.Ranged||B.Phase==Phase.Melee))
                    for(int part=0;part<Math.Min(8,Context.CombatPower.AvailableAttacks(B.Phase,Context.AllAttacksSiege).Length);part++)for(int choice=0;choice<3;choice++)candidates.Add(new("bowChoice",Targets:part,Option:choice));
                if(Context.CanChooseKillCrystal&&B.Phase==Phase.Melee)
                    for(int target=0;target<B.Enemies.Count;target++)foreach(var color in Enum.GetValues<ManaColor>().Where(c=>(int)c<4))candidates.Add(new("killCrystal",Targets:target,Option:(int)color));
            }
            if(ArtifactBuilds)foreach(var item in Journey.Player.Deck.Hand.Where(c=>c.Type==CardType.Artifact).Concat(Context.MysticBox is {Used:false} box?new[]{box.Box}:Array.Empty<DeedCard>()).Concat(Context.PendingHeadbandBorrow is {} headband?new[]{headband.PhysicalCard}:Array.Empty<DeedCard>()))
            {
                bool borrowingBox=ReferenceEquals(item,Context.MysticBox?.Box);
                string effectiveId=borrowingBox?Context.MysticBox.RevealedArtifact.Id:item.Id;
                foreach(var originalAction in ArtifactMoves(effectiveId))
                {
                    var action=borrowingBox?originalAction with{Kind="boxUse"}:originalAction;
                    int targetCount=action.Card is "items_023" or "items_022"?B?.Enemies.Count??0:Journey.Player.Units.Count;
                    if(action.Targets>=Math.Max(1,targetCount))continue;
                    if(action.Card=="items_001"&&action.Parts>=(1<<(B?.Enemies.Count??3)))continue;
                    if(action.Card is "items_008" or "items_016" && action.Parts>=(1<<Math.Min(8,Journey.Player.Deck.Hand.Count-(borrowingBox?0:1))))continue;
                    if(action.Card=="items_022")
                    {
                        var skill=Context.SkillSupply.Offer.FirstOrDefault(s=>s.SourceId==action.Input.Substring("skill=".Length));
                        if(skill==null||Context.PendingHeadbandBorrow!=null&&(action.Choice=="burn"||!ReferenceEquals(Context.PendingHeadbandBorrow.PhysicalCard,item)||!ReferenceEquals(Context.PendingHeadbandBorrow.Skill,skill)))continue;
                        if(action.Choice!="burn"&&HeadbandBorrowResolution.IsUnit(skill)&&action.Parts>=Journey.Player.Units.Count)continue;
                        if(action.Choice!="burn"&&HeadbandBorrowResolution.IsWound(skill)&&(action.Parts>=Journey.Player.Deck.Hand.Count||Journey.Player.Deck.Hand[action.Parts].Type!=CardType.Wound))continue;
                    }
                    if(action.Card is "items_013" or "items_021" or "items_023")
                    {
                        try
                        {
                            var selected=ArtifactInput(action,item);var fields=selected.Fields;
                            if(action.Card=="items_013")
                            {
                                var offer=action.Choice=="burn"?Context.SpellSupply.Offer.Select(c=>c.Id):Context.AdvActionSupply.Offer.Select(c=>c.Id);
                                if(!offer.Contains(fields[action.Choice=="burn"?"spells":"advanced"]))continue;
                            }
                            if(action.Card=="items_023")
                            {
                                var spell=Context.SpellSupply.Offer.FirstOrDefault(c=>c.Id==fields["spellId"]);
                                if(spell==null || CardColorHelper.Get(selected.Material.Id)!=CardColorHelper.Get(spell.Id))continue;
                                var choice=Journey.TomeChoices(spell,action.Choice=="burn").FirstOrDefault(c=>c.Id==fields["spellChoice"]);
                                if(choice==null || choice.Target!=BattleCardTarget.None&&action.Targets>=(B?.Enemies.Count??0) || action.Parts>=Math.Max(1,Journey.Player.Units.Count))continue;
                                var profile=string.Join(";",fields.Where(x=>x.Key is not ("materialSlot" or "spellId" or "spellChoice")).Select(x=>x.Key+"="+x.Value));
                                var definition=new Move("play",spell.Id,choice.Id,Targets:action.Targets,Input:profile);
                                if(!InputMatchesChoice(definition,choice))continue;
                                ResolveInput(definition,item);
                            }
                        }
                        catch(InvalidOperationException){continue;}
                    }
                    candidates.Add(action);
                }
            }
            if(UnitBuilds)
            {
                foreach(var unit in Journey.Player.Units.Where(u=>!u.IsDestroyed))candidates.AddRange(UnitMoves(unit.Card).Where(a=>B!=null || a.Parts==0&&!a.Input.Contains("boost=")&&(a.Targets==0||unit.Card.Actions[int.Parse(a.Choice)].Kind==UnitAbilityKind.ReadyUnit&&a.Targets<Journey.Player.Units.Count)));
                if(B?.Phase==Phase.AssignDamage&&Journey.Player.Units.Any(u=>!u.IsDestroyed))
                    for(int target=0;target<B.Enemies.Count;target++)for(int attack=0;attack<B.Enemies[target].AttackCount;attack++)foreach(int clear in new[]{0,1})candidates.Add(new("unitDamage",Targets:target,Parts:attack,Option:clear));
                if(PartyBuilds&&B?.Phase==Phase.AssignDamage)
                    for(int target=0;target<B.Enemies.Count;target++)for(int attack=0;attack<B.Enemies[target].AttackCount;attack++)foreach(var order in DamageOrders.Where(s=>s.All(c=>c-'0'<Journey.Player.Units.Count)))candidates.Add(new("unitDamage",Choice:order,Targets:target,Parts:attack));
                if(Context.Healing.Total>0)for(int target=-1;target<Journey.Player.Units.Count;target++)candidates.Add(new("heal",Targets:target));
            }
            if(B!=null&&(B.Phase is Phase.Ranged or Phase.Melee)&&Journey.Player.Skills.Any(s=>s.SourceId=="hero_skill_pool_004:04"))
                for(int part=0;part<Math.Min(8,Context.CombatPower.AvailableAttacks(B.Phase,Context.AllAttacksSiege).Length);part++)candidates.Add(new("skillBullseye","hero_skill_pool_004:04",Parts:part));
            if(B?.Phase==Phase.Block&&Context.SoulGuideBlockPending)
                for(int part=0;part<Math.Min(8,Context.CombatPower.Blocks.Count);part++)candidates.Add(new("soulGuideBlock","hero_skill_pool_003:00",Parts:part));
            if(B!=null&&B.Phase is Phase.Ranged or Phase.Melee&&Journey.Player.Skills.Any(s=>s.SourceId==ForkedLightningActions.SourceId))
                for(int targets=1;targets<(1<<Math.Min(EnemySlots,B.Enemies.Count));targets++)
                    if(!Enumerable.Range(0,B.Enemies.Count).Any(i=>(targets&(1<<i))!=0&&Context.DefeatedEnemyIndices.Contains(i)))candidates.Add(new("skillFork",ForkedLightningActions.SourceId,Targets:targets));
            if(B!=null&&Journey.Player.Skills.Any(s=>s.SourceId=="hero_skill_pool_002:06"))
                candidates.AddRange(Journey.Player.Units.Where(u=>!u.IsDestroyed).SelectMany(u=>LeadershipMoves(u.Card)));
            foreach(var skill in Journey.Player.Skills)
                for(int target=0;target<(B==null&&skill.SourceId=="hero_skill_pool_002:02"?Math.Min(3,Journey.Player.Units.Count):B!=null&&BattleSkillChoice.NeedsEnemy(skill.SourceId)?B.Enemies.Count:1);target++)
                    for(int attack=0;attack<(B!=null&&BattleSkillChoice.NeedsEnemy(skill.SourceId)?Math.Min(4,B.Enemies[target].AttackCount):1);attack++)
                        candidates.AddRange((B==null?Journey.OutsideSkillChoices(skill):B.SkillChoices(skill,target,attack)).Select(c=>new Move("skill",skill.SourceId,Targets:target,Parts:BattleSkillChoice.NeedsAttack(skill.SourceId,c.Option)?attack:0,Option:c.Option)).Where(a=>CatalogIndices.ContainsKey(a.Key)));
            foreach(var skill in Journey.Player.Skills.Where(s=>IsSidewaysSkill(s.SourceId)))
                candidates.AddRange(SidewaysSkillMoves(skill).Where(a=>a.Parts<Journey.Player.Deck.Hand.Count&&Journey.Player.Deck.Hand[a.Parts].Type!=CardType.Wound
                    &&(B==null?a.Choice is "Move" or "Influence":B.Phase==Phase.Block?a.Choice=="Block":B.Phase==Phase.Melee&&a.Choice=="Attack")));
            if(Journey.Player.HeldManaDie==null)candidates.AddRange(Context.ManaSource.Dice.Select(d=>new Move("source",Option:(int)d.Face)).DistinctBy(a=>a.Key));
            if(Context.LearningModes!=0)foreach(var offered in Context.AdvActionSupply.Offer)foreach(bool strong in new[]{false,true})candidates.Add(new("learn",offered.Id,Option:strong?1:0));
            if(Context.ExtraManaDice>0)foreach(var face in Context.ManaSource.Dice.Select(d=>d.Face).Distinct())foreach(var color in Enum.GetValues<ManaColor>())
                if(face!=ManaColor.Gold || color==ManaColor.Gold)candidates.Add(new("extraSource",Targets:(int)face,Option:(int)color));
            if(Context.PendingRepeatedEffect is {} repeated)
                foreach(var choice in PveCopyInputs.Choices(Context,repeated.OriginalCardId,repeated.Enhanced))
                    for(int target=0;target<(B!=null&&choice.Target!=BattleCardTarget.None?B.Enemies.Count:1);target++)
                        for(int attack=0;attack<(B!=null&&choice.Target!=BattleCardTarget.None?Math.Min(4,B.Enemies[target].AttackCount):1);attack++)
                            foreach(var profile in Profiles[repeated.OriginalCardId])
                            {
                                var candidate=new Move("repeat",repeated.OriginalCardId,choice.Id,Targets:target,Option:attack,Input:profile);
                                if(InputMatchesChoice(candidate,choice))candidates.Add(candidate);
                            }
            foreach(var card in Journey.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound).DistinctBy(c=>c.Id))
            {if(B!=null)candidates.Add(new("sideways",card.Id));if(card.Type==CardType.Artifact)continue;foreach(var choice in B?.Choices(card)??Journey.OutsideChoices(card))foreach(var profile in Profiles[card.Id])
                for(int target=0;target<(B!=null&&InputTarget(card.Id,choice,profile)!=BattleCardTarget.None?B.Enemies.Count:1);target++)
                    for(int attack=0;attack<(B!=null&&InputTarget(card.Id,choice,profile)!=BattleCardTarget.None?Math.Min(4,B.Enemies[target].AttackCount):1);attack++)
                        {
                            var candidate=new Move("play",card.Id,choice.Id,Targets:target,Option:attack,Input:profile);
                            if(InputMatchesChoice(candidate,choice))try{ResolveInput(candidate,card);candidates.Add(candidate);}catch(InvalidOperationException){}
                        }}
            if(B?.Phase==Phase.Block&&Context.MovementPool>0)
                for(int target=0;target<B.Enemies.Count;target++)for(int attack=0;attack<B.Enemies[target].AttackCount;attack++)candidates.Add(new("cumbersome",Targets:target,Option:attack));
            if(B?.Phase==Phase.Block)for(int target=0;target<B.Enemies.Count;target++)for(int attack=0;attack<Math.Min(4,B.Enemies[target].AttackCount);attack++)
            {
                candidates.Add(new("block",Targets:target,Option:attack));
                for(int parts=1;parts<(1<<Math.Min(8,Context.CombatPower.Blocks.Count));parts++)
                    candidates.Add(new("blockSelected",Targets:target,Parts:parts,Option:attack));
            }
            if(B?.Phase==Phase.Ranged||B?.Phase==Phase.Melee)
            {
                if(Context.EnemyDefense.Selected.Count>0)candidates.Add(new("clearDefend"));
                for(int targets=1;targets<(1<<B.Enemies.Count);targets++)
                {
                    // Enemy defense belongs to the impending actual attack, not an empty planning loop.
                    // Keep cancellation available above even if another effect removed its sources.
                    var chosenTargets=Enumerable.Range(0,B.Enemies.Count).Where(i=>(targets&(1<<i))!=0).ToArray();
                    if(Context.CombatPower.AvailableAttacks(B.Phase,chosenTargets,B.Enemies,Context.AllAttacksSiege).Length==0)continue;
                    try{candidates.AddRange(B.DefendChoices(chosenTargets).Select(c=>new Move("defend",Targets:targets,Parts:c.SourceIndex,Option:c.TargetIndex)));}
                    catch(InvalidOperationException){}
                }
                int n=Math.Min(8,B.Context.CombatPower.AvailableAttacks(B.Phase,B.Context.AllAttacksSiege).Length);
                for(int targets=1;targets<(1<<B.Enemies.Count);targets++)for(int mask=1;mask<(1<<n);mask++)
                    if(System.Numerics.BitOperations.PopCount((uint)mask)<=3 || mask==(1<<n)-1)candidates.Add(new("attack",Targets:targets,Parts:mask));
            }
            if(NativePayments&&PendingPayment!=null)
                return Preview(candidates.Where(a=>a.Kind is "play" or "unit").Append(new Move("cancelPayment")));
            var result=Preview(candidates);
            if(NativePayments)result.AddRange(LegalPaymentSelections(candidates));
            if(B==null && CanEndCurrentTurn())result.Add(Catalog.FindIndex(a=>a.Kind=="nextTurn"));
            return result.Distinct().ToList();
        }
        bool CanEndCurrentTurn()
        {
            try
            {
                Journey.Game.ValidateEndCurrentTurn(Context,Journey.Player.Deck.Hand.Where(c=>c.Type!=CardType.Wound));
                return true;
            }
            catch(InvalidOperationException){return false;}
        }
        IEnumerable<Move> BannerCandidates()
        {
            if(!ArtifactBuilds)yield break;
            // The printed choice resolves at turn end, not during every combat
            // phase. Keep both choices in the formal end-turn window.
            if(Context.ProtectionBannerBurned&&CanEndCurrentTurn())
                for(int use=0;use<2;use++)yield return new("protectionCleanup","items_002",Option:use);
            for(int unit=0;unit<Math.Min(3,Journey.Player.Units.Count);unit++)
            {
                string id=Journey.Player.Units[unit].BannerEffectId;
                if(id=="items_003"&&!Context.InBattle)yield return new("bannerUse",id,Targets:unit);
                if(id=="items_018"&&B!=null&&B.Phase!=Phase.End)for(int use=0;use<2;use++)yield return new("bannerUse",id,Targets:unit,Option:use);
                if(id=="items_001"&&B?.Phase==Phase.Block)for(int enemy=0;enemy<B.Enemies.Count;enemy++)for(int attack=0;attack<Math.Min(4,B.Enemies[enemy].AttackCount);attack++)yield return new("bannerUse",id,Targets:unit,Parts:enemy,Option:attack);
            }
        }
        List<int> Preview(IEnumerable<Move> candidates,bool firstOnly=false)
        {
            var checkpoint=DemoStateCheckpoint.Capture(B==null?Journey:B);var result=new List<int>();
            string before=JsonSerializer.Serialize(State())+JsonSerializer.Serialize(Features());
            var hand=Journey.Player.Deck.Hand.ToArray();
            int savedKills=Kills;
            int savedAcquired=Acquired.Count;
            var savedPayment=PendingPayment;
            foreach(var a in candidates.DistinctBy(x=>x.Key))
            {
                try{Apply(a);if(before!=JsonSerializer.Serialize(State())+JsonSerializer.Serialize(Features()))result.Add(CatalogIndices[a.Key]);}
                catch(InvalidOperationException ex)
                {if(Environment.GetEnvironmentVariable("BATTLE_RL_DIAGNOSTIC_KEY")==a.Key)Console.Error.WriteLine(a.Key+": "+ex.Message);}
                finally{checkpoint.Restore();PendingPayment=savedPayment;Kills=savedKills;if(Acquired.Count>savedAcquired)Acquired.RemoveRange(savedAcquired,Acquired.Count-savedAcquired);}
                if(before!=JsonSerializer.Serialize(State())+JsonSerializer.Serialize(Features()) || !Journey.Player.Deck.Hand.SequenceEqual(hand,ReferenceEqualityComparer.Instance))throw new InvalidOperationException("Preview mutated state");
                if(firstOnly&&result.Count>0)break;
            }
            return result.Distinct().ToList();
        }
    }
    public static void Serve(bool variedBuilds=false,bool unitBuilds=false,bool partyBuilds=false,bool artifactBuilds=false)
    {
        VariedBuilds=variedBuilds;
        UnitBuilds=unitBuilds;
        PartyBuilds=partyBuilds;
        ArtifactBuilds=artifactBuilds;
        IncludeOriginalOpenings=artifactBuilds&&Environment.GetEnvironmentVariable("BATTLE_RL_ORIGINAL_OPENINGS")=="1";
        string scope=IncludeOriginalOpenings?"original_heroes_opening_and_midgame_printed_original_enemy_formal_turn_mask_v41":artifactBuilds?"original_heroes_public_headband_printed_original_enemy_formal_turn_mask_v41":partyBuilds?"original_heroes_legal_levels_ordered_unit_parties_v14":unitBuilds?"original_heroes_original_unit_inventories_v13":variedBuilds?"original_heroes_random_legal_deed_inventories_v12":"original_heroes_actual_supply_and_spell_consumers_v10";
        Env env=null;List<int> legal=new();List<Move[]> atomicCache=null;var sample=new Env(0);
        string rewardDefinition=partyBuilds?"step -0.01, kill +10, formal whole-group win rising edge +20, active physical wound card delta -0.02 (all owned zones except removed; relocation is neutral), formal denial -5, no-kill terminal -5, unfinished active battle at step limit -10":"step -0.01, kill +10, active physical wound card delta -0.02 (all owned zones except removed; relocation is neutral), formal denial -5, no-kill terminal -5";
        if(artifactBuilds)rewardDefinition+=", planning action additional cost 0.01 * min(consecutive planning count, 32), reset by actual non-planning operation";
        Console.WriteLine(JsonSerializer.Serialize(new{scope,featureSchema=FeatureSchema,taskResetSchema="independent_original_factors_v2",enemySlots=EnemySlots,abilitySchema=Enum.GetNames<Ability>(),summonSupply="seeded actual brown tokens, one per definition",featureCompatibility="v37 expands indexed target blocks; do not prefix-copy earlier input weights",catalog=Catalog,featureNames=Enumerable.Range(0,sample.Features().Length).Select(i=>"state."+i),rewardDefinition,gaps=new[]{"advanced","spells","artifacts","units","specializedSkillInputs","mapSiteContexts","moreThanEightAttackContributions","originalTokenCopyMultiplicities"}}));
        object Output(double reward=0)=>new{scope,state=env.State(),features=env.Features(),legal,done=env.Done,won=env.Kills>0,reward,steps=env.Steps,maskCacheStates=0};
        string line;while((line=Console.ReadLine())!=null)
        {
            try
            {
                var r=JsonDocument.Parse(line).RootElement;var op=r.GetProperty("op").GetString();if(op=="quit")break;
                if(op=="inspectInventory")
                {var inspected=new Env(r.GetProperty("seed").GetInt32());Console.WriteLine(JsonSerializer.Serialize(new{scope,state=inspected.State()}));continue;}
                if(op=="reset")
                {
                    atomicCache=null;
                    if(r.TryGetProperty("task",out var taskElement))
                    {var task=TaskDefinition.Read(taskElement);env=new Env(task.RandomSeed,task);}
                    else env=new Env(r.GetProperty("seed").GetInt32());
                    legal=env.Legal();Console.WriteLine(JsonSerializer.Serialize(Output()));continue;
                }
                if(op=="inspectTask")
                {var task=TaskDefinition.Read(r.GetProperty("task"));var inspected=new Env(task.RandomSeed,task);Console.WriteLine(JsonSerializer.Serialize(new{scope,state=inspected.State()}));continue;}
                if(op=="state"){Console.WriteLine(JsonSerializer.Serialize(Output()));continue;}
                if(op=="atomicChoices")
                {env.AtomicSingleBattle=true;atomicCache=env.AtomicChoices();Console.WriteLine(JsonSerializer.Serialize(new{choices=atomicCache}));continue;}
                if(op=="atomicStep")
                {
                    env.AtomicSingleBattle=true;
                    var choices=atomicCache??throw new InvalidOperationException("Request atomic choices before execution");
                    var selected=r.GetProperty("choice").GetInt32();
                    if(selected<0||selected>=choices.Count)throw new InvalidOperationException("Illegal atomic choice");
                    var micro=env.ApplyAtomic(choices[selected],out var atomicError);
                    atomicCache=null;
                    legal=new();
                    Console.WriteLine(JsonSerializer.Serialize(new{state=env.State(),features=env.Features(),done=env.Done,reward=0.0,steps=env.Steps,micro,atomic_error=atomicError}));continue;
                }
                int index=r.GetProperty("action").GetInt32();if(!legal.Contains(index))throw new InvalidOperationException("Illegal action");
                int kills=env.Kills,wounds=env.ActiveWounds;bool beforeWon=env.B?.Won??false;
                env.Apply(Catalog[index]);env.Steps++;if(env.Steps>=256){env.Done=true;env.StopReason="step_limit";}
                var action=Catalog[index];bool planning=action.Kind is "unitDamage" or "defend" or "clearDefend" or "bowChoice" or "killCrystal" or "protectionCleanup" or "roundReturn" or "payment" or "cancelPayment" || action.Kind=="bannerUse"&&action.Card=="items_018";
                env.PlanningRun=ArtifactBuilds&&planning?env.PlanningRun+1:0;
                double reward=-.01+10*(env.Kills-kills)-.02*(env.ActiveWounds-wounds);
                if(ArtifactBuilds&&planning)reward-=.01*Math.Min(32,env.PlanningRun);
                if(PartyBuilds&&!beforeWon&&env.B?.Won==true)reward+=20;
                if(env.StopReason.StartsWith("formal_"))reward-=5;
                legal=env.Legal();if(legal.Count==0&&!env.Done){env.Done=true;env.StopReason="no_legal_continuation";reward-=2;}
                if(env.Done && env.Kills==0)reward-=5;
                if(PartyBuilds&&env.StopReason=="step_limit"&&env.B!=null&&env.B.Phase!=Phase.End)reward-=10;
                Console.WriteLine(JsonSerializer.Serialize(Output(reward)));
            }
            catch(Exception ex){Console.WriteLine(JsonSerializer.Serialize(new{error=ex.ToString()}));}
        }
    }
}



