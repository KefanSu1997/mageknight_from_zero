using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text.Json;
using MK.Logic.Core;
using MK.Logic.Runtime;

// Hash-bound inventory. Provenance may be an explicitly declared provisional decision;
// source integrity alone does not certify fidelity to printed token faces.
static class OriginalRuinCatalog
{
    public static RuinToken[] LoadOptional(string root)
    {
        string folder=Path.Combine(root,"resources","original_ruins");
        string file=Path.Combine(folder,"base_tokens.json");
        if(!File.Exists(file))return null;
        using var document=JsonDocument.Parse(File.ReadAllText(file));
        var data=document.RootElement;
        if(data.GetProperty("version").GetInt32()!=1 || data.GetProperty("edition").GetString()!="base")
            throw new InvalidDataException("Unsupported original ruin inventory version/edition.");
        var sources=new HashSet<string>(StringComparer.Ordinal);
        foreach(var source in data.GetProperty("sources").EnumerateArray())
        {
            string id=source.GetProperty("id").GetString();
            string relative=source.GetProperty("file").GetString();
            if(string.IsNullOrWhiteSpace(id)||!sources.Add(id)||string.IsNullOrWhiteSpace(relative)||Path.IsPathRooted(relative))
                throw new InvalidDataException("Invalid ruin source identity/path.");
            string path=Path.GetFullPath(Path.Combine(folder,relative));
            if(!path.StartsWith(Path.GetFullPath(folder)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("Ruin source must stay inside original_ruins.");
            using var stream=File.OpenRead(path);
            string sha=Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            if(sha!=source.GetProperty("sha256").GetString())throw new InvalidDataException("Ruin source SHA mismatch.");
        }
        T[] Values<T>(JsonElement token,string key) where T:struct,Enum =>
            token.GetProperty(key).EnumerateArray().Select(x=>
            {
                string name=x.GetString();
                if(!Enum.GetNames<T>().Contains(name,StringComparer.Ordinal))throw new InvalidDataException("Unknown ruin enum name.");
                return Enum.Parse<T>(name);
            }).ToArray();
        var tokens=new List<RuinToken>();
        foreach(var token in data.GetProperty("tokens").EnumerateArray())
        {
            if(!sources.Contains(token.GetProperty("source_id").GetString()) ||
                string.IsNullOrWhiteSpace(token.GetProperty("source_location").GetString()))
                throw new InvalidDataException("Every ruin requires a source and face locator.");
            tokens.Add(new RuinToken(token.GetProperty("id").GetString(),Values<ManaColor>(token,"mana_cost"),
                token.GetProperty("fame").GetInt32(),Values<EnemyTokenColor>(token,"enemies"),Values<RuinReward>(token,"rewards")));
        }
        if(tokens.Count!=12 || tokens.Select(x=>x.Id).Distinct().Count()!=12 || tokens.Count(x=>x.ManaCost.Count>0)!=4)
            throw new InvalidDataException("Base ruin inventory requires twelve distinct physical tokens, four altars.");
        return tokens.ToArray();
    }
}
