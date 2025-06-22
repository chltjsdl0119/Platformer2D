using System;

public interface IWorkflow<T>
    where T : Enum
{
    T ID { get; }
    int Current { get; }
    T MoveNext();
    void Reset();
}
