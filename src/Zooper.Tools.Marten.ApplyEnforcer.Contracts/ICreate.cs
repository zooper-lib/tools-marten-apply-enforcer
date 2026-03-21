namespace Zooper.Tools.Marten.ApplyEnforcer.Contracts;

public interface ICreate<TEvent, TProjection>
    where TProjection : ICreate<TEvent, TProjection>
{
    static abstract TProjection Create(TEvent domainEvent);
}