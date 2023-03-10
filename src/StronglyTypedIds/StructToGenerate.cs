using System;
using Microsoft.CodeAnalysis.Text;

namespace StronglyTypedIds;

internal readonly record struct StructToGenerate
{
    public StructToGenerate(string name, string nameSpace, string? templateName, ParentClass? parent)
    {
        Name = name;
        NameSpace = nameSpace;
        TemplateName = templateName;
        Parent = parent;
    }

    public string Name { get; }
    public string NameSpace { get; }
    public string? TemplateName { get; }
    public ParentClass? Parent { get; }
}

internal sealed record Result<TValue>
    where TValue : IEquatable<TValue>?
{
    public Result(TValue value, EquatableArray<DiagnosticInfo> errors)
    {
        Value = value;
        Errors = errors;
    }

    public TValue Value { get; }
    public EquatableArray<DiagnosticInfo> Errors { get; }

    public static Result<(TValue, bool)> Fail()
        => new((default!, false), EquatableArray<DiagnosticInfo>.Empty);
}