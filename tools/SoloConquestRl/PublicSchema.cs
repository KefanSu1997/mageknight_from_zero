using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json.Serialization;
using MK.Logic.Runtime.Battles;

// Reflect ONLY the explicit public DTO roots. Never traverse a journey, player,
// private checkpoint, or runtime instance. A type graph handles recursive copy
// selections without depth limits or silent truncation.
internal static class PublicSchema
{
    public static object Capture()
    {
        var types=new SortedDictionary<string,object>(StringComparer.Ordinal);
        string Visit(Type type)
        {
            string key=type.FullName??type.Name;
            if(types.ContainsKey(key))return key;
            types[key]=null;
            var underlying=Nullable.GetUnderlyingType(type);
            if(underlying!=null)types[key]=new {kind="nullable",item=Visit(underlying)};
            else if(type.IsArray)types[key]=new {kind="array",item=Visit(type.GetElementType())};
            else if(type==typeof(string))types[key]=new {kind="text"};
            else if(type==typeof(bool))types[key]=new {kind="bool"};
            else if(type.IsEnum)types[key]=new {kind="enum",values=Enum.GetValues(type).Cast<object>()
                .Select(v=>new {name=Enum.GetName(type,v),value=Convert.ToInt64(v)}).ToArray()};
            else if(type==typeof(int)||type==typeof(long)||type==typeof(short)||type==typeof(byte))
                types[key]=new {kind="integer"};
            else if(type==typeof(float)||type==typeof(double)||type==typeof(decimal))types[key]=new {kind="number"};
            else
            {
                if(type.Namespace==null||!type.Namespace.StartsWith("MK.Logic.",StringComparison.Ordinal))
                    throw new InvalidOperationException("Unreviewed public schema type: "+key);
                var properties=type.GetProperties(BindingFlags.Instance|BindingFlags.Public)
                    .Where(p=>p.GetMethod?.IsPublic==true&&p.GetIndexParameters().Length==0
                        &&p.GetCustomAttribute<JsonIgnoreAttribute>()?.Condition!=JsonIgnoreCondition.Always)
                    .OrderBy(p=>p.Name,StringComparer.Ordinal)
                    .Select(p=>new {name=p.GetCustomAttribute<JsonPropertyNameAttribute>()?.Name??p.Name,
                        type=Visit(p.PropertyType),reference_nullable=!p.PropertyType.IsValueType}).ToArray();
                types[key]=new {kind="object",properties};
            }
            return key;
        }
        string observation=Visit(typeof(SoloPolicyObservation));
        string command=Visit(typeof(SoloPolicyCommand));
        return new {version=1,observation,command,types,
            scope="public DTO serialization shapes only; strings need explicit categorical/text encoding"};
    }
}
