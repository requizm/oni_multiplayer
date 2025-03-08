using System;
using MultiplayerMod.Core.Dependency;
using MultiplayerMod.ModRuntime;
using MultiplayerMod.Multiplayer.States;

namespace MultiplayerMod.Multiplayer.Objects.Reference;

[Serializable]
public class StateMachineReference(
    ComponentReference<StateMachineController> controllerReference,
    Type stateMachineInstanceType
) : TypedReference<StateMachine.Instance> {

    private ComponentReference<StateMachineController> ControllerReference { get; set; } = controllerReference;
    private Type StateMachineInstanceType { get; set; } = stateMachineInstanceType;

    public override StateMachine.Instance Resolve() => ControllerReference.Resolve().GetSMI(StateMachineInstanceType);

}

[DependenciesStaticTarget]
public static class ChoreStateMachineReferenceHelper {
    [InjectDependency] public static MultiplayerObjects Objects { get; set; } = null!;
}

[Serializable]
public class ChoreStateMachineReference<T>(Chore<T> chore)
    : TypedReference<StateMachine.Instance> where T : StateMachine.Instance {

    private MultiplayerId id = ChoreStateMachineReferenceHelper.Objects.Get(chore)!.Id;

    public override StateMachine.Instance Resolve() => Runtime.Instance.Dependencies.Get<StatesManager>()
        .GetSmi(ChoreStateMachineReferenceHelper.Objects.Get<Chore<StateMachine.Instance>>(id)!);

    public StateMachine.Instance Get() => Resolve();

}
