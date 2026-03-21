using System;

namespace Zooper.Tools.Marten.ApplyEnforcer.Contracts;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class ApprovedAppendWrapperAttribute : Attribute
{
}