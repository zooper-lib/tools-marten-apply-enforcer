namespace Zooper.Tools.Marten.ApplyEnforcer.Contracts;

public interface ICreate<in TEvent, out TProjection>
    where TProjection : ICreate<TEvent, TProjection>
{
    static abstract TProjection Create(TEvent domainEvent);
}