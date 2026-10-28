using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using MK.Logic.Runtime;
using MK.Logic.Runtime.Map;
using MK.Logic.Runtime.Battles;

// Offline split construction only. Never add this to State(), observations or support.
static class PrivateWorldExport
{
    static object Field(object source,string name)=>source.GetType().GetField(name,
        BindingFlags.Instance|BindingFlags.NonPublic)?.GetValue(source)
        ?? throw new InvalidOperationException("Private inventory schema changed: "+name);
    static string[] Ids(object sequence)=>((IEnumerable)sequence).Cast<object>().Select(x=>
        x.GetType().GetProperty("Id")?.GetValue(x) as string
        ?? throw new InvalidOperationException("Inventory entry has no definition ID")).ToArray();
    static string[] Deck(object supply)=>Ids(Field(Field(supply,"_deck"),"_cards"));
    public static object Capture(SoloConquestEpisode episode,PrintedTileCatalog catalog)
    {
        if(episode.Steps!=0)throw new InvalidOperationException("World export is only allowed before the first decision.");
        string before=SoloStateDigest.Compute(episode);
        var tiles=episode.Tiles.Capture();var journey=episode.Journey;var globals=journey.Game.Globals;
        var dummy=((SoloDummyDeck)Field(journey,"_soloDummy")).Capture();
        object result=new {
            version=1,private_metadata=true,complete_ruin_inventory=globals.Ruins!=null,
            map_tiles=new {opening=tiles.Revealed,main=tiles.Main,reserve_country=tiles.ReserveCountry,reserve_core=tiles.ReserveCore},
            city_colors=tiles.Revealed.Concat(tiles.Main).SelectMany(id=>catalog.Tiles[id].Cells)
                .Where(c=>c.Site=="City").Select(c=>c.CityColor).ToArray(),
            city_levels=new[]{5,8},
            initial_mana=globals.Mana.Dice.Select(d=>d.Face.ToString()).ToArray(),
            enemy_supply=new {draw=globals.Enemies.DrawPile.Select(x=>new{x.Id,color=x.TokenColor.ToString()}).ToArray(),
                in_use=globals.Enemies.InUse.Select(x=>new{x.Id,color=x.TokenColor.ToString()}).ToArray(),
                placed=journey.Game.Map.Places.OrderBy(x=>x.Key.Q).ThenBy(x=>x.Key.R)
                    .Select(x=>new {x.Key.Q,x.Key.R,defenders=x.Value.Defenders.Select(d=>d.Id).ToArray()}).ToArray()},
            offer_supply=new {advanced=Deck(globals.AdvActions),advanced_offer=Ids(globals.AdvActions.Offer),
                spells=Deck(globals.Spells),spell_offer=Ids(globals.Spells.Offer),artifacts=Deck(globals.Artifacts),
                regular_units=Ids(Field(globals.Units,"_regular")),elite_units=Ids(Field(globals.Units,"_elite")),
                unit_offer=Ids(globals.Units.Offer),skills=Deck(globals.Skills),skill_offer=Ids(globals.Skills.Offer)},
            dummy_setup=new {dummy.Cards,dummy.Draw,dummy.Crystals,dummy.DualColorRule},
            ruins=globals.Ruins==null?null:new {draw=Ids(Field(globals.Ruins,"_draw")),in_use=Ids(Field(globals.Ruins,"_inUse"))}
        };
        if(SoloStateDigest.Compute(episode)!=before)throw new InvalidOperationException("World export changed the environment.");
        return result;
    }
}
