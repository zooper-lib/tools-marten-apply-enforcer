namespace Zooper.Tools.Marten.ApplyEnforcer.Contracts;

public interface IApply<in TEvent>
{
    void Apply(TEvent domainEvent);
}