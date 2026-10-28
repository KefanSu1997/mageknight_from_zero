using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace MK.Logic.Runtime.Battles
{
    /// <summary>Private audit fingerprint, never a policy input. Includes hidden piles,
    /// random generators and reference aliases. Bound to the exact rules/runtime build;
    /// this is not a portable save format or a semantic cycle detector.</summary>
    public static class SoloStateDigest
    {
        private sealed class Identity : IEqualityComparer<object>
        {
            public new bool Equals(object a,object b)=>ReferenceEquals(a,b);
            public int GetHashCode(object value)=>RuntimeHelpers.GetHashCode(value);
        }
        public static string Compute(object root,TextWriter diagnostic=null)
        {
            using var sha=SHA256.Create();
            using var stream=new CryptoStream(Stream.Null,sha,CryptoStreamMode.Write);
            using(var writer=new BinaryWriter(stream,Encoding.UTF8,true))
            {
                writer.Write("solo-native-graph-v1");
                new Encoder(writer,diagnostic).Visit(root);
            }
            stream.FlushFinalBlock();
            return BitConverter.ToString(sha.Hash).Replace("-","").ToLowerInvariant();
        }
        private sealed class Encoder
        {
            private readonly BinaryWriter _writer;
            private readonly Dictionary<object,int> _ids=new(new Identity());
            private readonly TextWriter _diagnostic;
            public Encoder(BinaryWriter writer,TextWriter diagnostic){_writer=writer;_diagnostic=diagnostic;}
            private static bool Domain(Type t)=>t.Namespace?.StartsWith("MK.Logic",StringComparison.Ordinal)==true;
            public void Visit(object value)
            {
                if(value==null){_writer.Write("null");return;}
                var type=value.GetType();
                _diagnostic?.WriteLine("type "+type.FullName);
                _writer.Write(type.FullName??type.Name);
                if(type.IsPrimitive||type.IsEnum||value is string||value is decimal||value is Guid
                    ||value is DateTime||value is TimeSpan)
                {
                    _diagnostic?.WriteLine("value "+Convert.ToString(value,CultureInfo.InvariantCulture));
                    _writer.Write(value is DateTime date?date.ToBinary().ToString(CultureInfo.InvariantCulture)
                        :value is TimeSpan span?span.Ticks.ToString(CultureInfo.InvariantCulture)
                        :Convert.ToString(value,CultureInfo.InvariantCulture));return;
                }
                if(value is Type reflected){_writer.Write(reflected.FullName??reflected.Name);return;}
                if(!type.IsValueType)
                {
                    if(_ids.TryGetValue(value,out int id)){_diagnostic?.WriteLine("ref "+id);_writer.Write("ref");_writer.Write(id);return;}
                    _writer.Write("object");_writer.Write(_ids.Count);_ids.Add(value,_ids.Count);
                }
                if(value is Delegate callback)
                {
                    var calls=callback.GetInvocationList();_writer.Write(calls.Length);
                    foreach(var call in calls)
                    {
                        _writer.Write(call.Method.DeclaringType?.FullName??"");
                        _writer.Write(call.Method.ToString());
                        if(call.Target!=null&&!Domain(call.Target.GetType()))
                            throw new NotSupportedException("Non-rule callback in audited state: "+call.Method);
                        Visit(call.Target);
                    }
                    return;
                }
                if(value is IEnumerable sequence&&(type.IsArray||type.Namespace?.StartsWith("System.Collections",StringComparison.Ordinal)==true))
                {
                    // Logical enumeration avoids unused backing-array capacity and randomized
                    // dictionary bucket hashes. Preserve order because game code can observe it.
                    foreach(var item in sequence){_writer.Write(true);Visit(item);}
                    _writer.Write(false);return;
                }
                bool random=type==typeof(Random)||type.FullName?.StartsWith("System.Random+",StringComparison.Ordinal)==true;
                if(!Domain(type)&&!random&&!type.IsValueType)
                    throw new NotSupportedException("Unaudited state type: "+type.FullName);
                var fields=new List<FieldInfo>();
                for(var current=type;current!=null&&current!=typeof(object);current=current.BaseType)
                    fields.AddRange(current.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly));
                foreach(var field in fields.OrderBy(f=>f.DeclaringType.FullName,StringComparer.Ordinal).ThenBy(f=>f.Name,StringComparer.Ordinal))
                {
                    _diagnostic?.WriteLine("field "+field.DeclaringType.FullName+"/"+field.Name);
                    _writer.Write(field.DeclaringType.FullName+"/"+field.Name);Visit(field.GetValue(value));
                }
                _writer.Write("end-fields");
            }
        }
    }
}
