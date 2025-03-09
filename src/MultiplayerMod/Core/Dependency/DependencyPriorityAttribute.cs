using System;

namespace MultiplayerMod.Core.Dependency;

[AttributeUsage(AttributeTargets.Class)]
public class DependencyPriorityAttribute : Attribute {
    public int Priority { get; }

    public DependencyPriorityAttribute(int priority) {
        Priority = priority;
    }
}
