using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

static class ResourceBinding
{
    public static SortedDictionary<string,string> Capture(string root)
    {
        var result=new SortedDictionary<string,string>(StringComparer.Ordinal);
        foreach(string folder in new[]{"resources/text_json","resources/original_maps"})
        foreach(string path in Directory.EnumerateFiles(Path.Combine(root,folder),"*.json",SearchOption.AllDirectories))
            result.Add(Path.GetRelativePath(root,path).Replace('\\','/'),HashFile(path));
        string ruins=Path.Combine(root,"resources","original_ruins");
        if(Directory.Exists(ruins))
            foreach(string path in Directory.EnumerateFiles(ruins,"*",SearchOption.AllDirectories))
                result.Add(Path.GetRelativePath(root,path).Replace('\\','/'),HashFile(path));
        foreach(string path in Directory.EnumerateFiles(AppContext.BaseDirectory,"*.dll"))
            result.Add("binary/"+Path.GetFileName(path),HashFile(path));
        result.Add("runtime",System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription);
        result.Add("observation_adapter",Environment.GetEnvironmentVariable("SOLO_BASELINE_A_OBSERVATION")=="1"
            ?"baseline_A_public6314_solo_v1":"solo_public_dto_v1");
        return result;
    }
    static string HashFile(string path)
    {
        using var stream=File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
    public static string Digest(SortedDictionary<string,string> files)=>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(files)))).ToLowerInvariant();
}
