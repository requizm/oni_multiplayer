using System;
using System.Reflection;

namespace MultiplayerMod.Core.Dependency;

public class DependencyInfo {

    public string Name { get; }
    public Type Type { get; }
    public bool Lazy { get; }
    public int Priority { get; }

    public DependencyInfo(string name, Type type, bool lazy) {
        if (!type.IsClass)
            throw new InvalidDependencyException($"\"{type}\" must be a class.");

        Name = name;
        Type = type;
        Lazy = lazy;

        var priorityAttr = type.GetCustomAttribute<DependencyPriorityAttribute>();
        Priority = priorityAttr?.Priority ?? 0;
    }

}
