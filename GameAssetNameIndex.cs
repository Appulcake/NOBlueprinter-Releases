using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Blueprinter;

// ResolveGameAsset() caching and speedup
internal static class GameAssetNameIndex
{
    private static readonly Dictionary<Type, Dictionary<string, Match>> Indexes = new();
    
    internal static bool Enabled { get; private set; }
    
    internal static void Start()
    {
        Clear();
        Enabled = true;
    }
    
    internal static void End()
    {
        Enabled = false;
        Clear();
    }
    
    internal static void Clear()
    {
        if (Indexes.Count > 0)
            Indexes.Clear();
    }
    
    internal static bool TryGet(Type type, string key, out Object result, out int matchCount)
    {
        result = null;
        matchCount = 0;
        if (!Enabled || !Indexes.TryGetValue(type, out var names) || !names.TryGetValue(key, out var match))
            return false;
        
        if (match.First == null || !Matches(match.First, key))
        {
            Indexes.Remove(type);
            return false;
        }
        
        result = match.First;
        matchCount = match.Count;
        return true;
    }
    
    internal static void Build(Type type, Object[] objects)
    {
        if (!Enabled)
            return;
        
        var names = new Dictionary<string, Match>(StringComparer.OrdinalIgnoreCase);
        foreach (var obj in objects)
        {
            if (obj == null || (obj is GameObject go && go.transform.parent != null))
                continue;
            
            var ownObjName = obj.name;
            Add(names, ownObjName, obj);
            if (obj is not Component component)
                continue;
            
            var rootName = component.transform.root?.name;
            if (!string.IsNullOrEmpty(rootName) &&
                !string.Equals(rootName, ownObjName, StringComparison.OrdinalIgnoreCase))
                Add(names, rootName, obj);
        }
        
        Indexes[type] = names;
    }
    
    private static void Add(Dictionary<string, Match> names, string name, Object obj)
    {
        if (string.IsNullOrEmpty(name))
            return;
        
        if (!names.TryGetValue(name, out var match))
            names.Add(name, new Match { First = obj, Count = 1 });
        else
            match.Count++;
    }
    
    private static bool Matches(Object obj, string key)
    {
        if (obj is GameObject go && go.transform.parent != null)
            return false;
        
        if (string.Equals(obj.name, key, StringComparison.OrdinalIgnoreCase))
            return true;
        
        if (obj is not Component component)
            return false;
        
        var rootName = component.transform?.root?.name;
        return !string.IsNullOrEmpty(rootName) && string.Equals(rootName, key, StringComparison.OrdinalIgnoreCase);
    }
    
    private sealed class Match
    {
        internal int Count;
        internal Object First;
    }
}