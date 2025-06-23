using System;

public interface IWorkflow<T>
    where T : Enum
{
    T ID { get; }
    bool CanExecute { get; }
    int Current { get; }
    void OnEnter(object[] parameters);
    void OnExit();
    T OnUpdate();
    void OnFixedUpdate();
    void Reset();
}
