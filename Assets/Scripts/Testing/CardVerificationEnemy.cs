using System;
using System.Linq;
using MK.Logic.Core;
using MK.Logic.Data;

[Serializable]
public sealed class CardVerificationEnemy
{
    public string id;
    public bool originalDefinition;
    public int armor;
    public int attack;
    public string element = "Physical";
    public int fame;
    public string[] abilities;
    public Monster Create() => originalDefinition ? CardJsonLoader.LoadMonsters().Single(m=>m.Id==id) : new(id, armor, attack, (Element)Enum.Parse(typeof(Element), element), fame,
        (abilities ?? Array.Empty<string>()).Select(a => (Ability)Enum.Parse(typeof(Ability), a)).ToArray());
}
