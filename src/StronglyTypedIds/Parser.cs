using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using StronglyTypedIds.Diagnostics;

namespace StronglyTypedIds;

internal static class Parser
{
    public const string StronglyTypedIdAttribute = "StronglyTypedIds.StronglyTypedIdAttribute";
    public const string StronglyTypedIdDefaultsAttribute = "StronglyTypedIds.StronglyTypedIdDefaultsAttribute";

    public static Result<(StructToGenerate info, bool valid)> GetStructSemanticTarget(GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var structSymbol = ctx.TargetSymbol as INamedTypeSymbol;
        if (structSymbol is null)
        {
            return Result<StructToGenerate>.Fail();
        }

        var structSyntax = (StructDeclarationSyntax)ctx.TargetNode;

        var hasMisconfiguredInput = false;
        List<DiagnosticInfo>? diagnostics = null;
        string? templateName = null;

        foreach (AttributeData attribute in structSymbol.GetAttributes())
        {
            if (!((attribute.AttributeClass?.Name == "StronglyTypedIdAttribute" ||
                  attribute.AttributeClass?.Name == "StronglyTypedId") &&
                  attribute.AttributeClass.ToDisplayString() == StronglyTypedIdAttribute))
            {
                // wrong attribute
                continue;
            }

            if (!attribute.ConstructorArguments.IsEmpty)
            {
                // make sure we don't have any errors
                ImmutableArray<TypedConstant> args = attribute.ConstructorArguments;

                foreach (TypedConstant arg in args)
                {
                    if (arg.Kind == TypedConstantKind.Error)
                    {
                        // have an error, so don't try and do any generation
                        hasMisconfiguredInput = true;
                    }
                }

                templateName = (string?)args[0].Value;
                
                if (string.IsNullOrWhiteSpace(templateName))
                {
                    // TODO: add diagnostic
                    hasMisconfiguredInput = true;
                }
            }

            if (!attribute.NamedArguments.IsEmpty)
            {
                foreach (KeyValuePair<string, TypedConstant> arg in attribute.NamedArguments)
                {
                    TypedConstant typedConstant = arg.Value;
                    if (typedConstant.Kind == TypedConstantKind.Error)
                    {
                        hasMisconfiguredInput = true;
                        break;
                    }

                    templateName = (string?)typedConstant.Value;
                    
                    if (string.IsNullOrWhiteSpace(templateName))
                    {
                        // TODO: add diagnostic
                        hasMisconfiguredInput = true;
                    }
                }
            }

            // no need to check any more attributes
            break;
        }

        if (hasMisconfiguredInput)
        {
            var errors = diagnostics is null
                ? EquatableArray<DiagnosticInfo>.Empty
                : new EquatableArray<DiagnosticInfo>(diagnostics.ToArray());
            return new Result<(StructToGenerate, bool)>((default, false), errors);
        }

        var hasPartialModifier = false;
        foreach (var modifier in structSyntax.Modifiers)
        {
            if (modifier.IsKind(SyntaxKind.PartialKeyword))
            {
                hasPartialModifier = true;
                break;
            }
        }

        if (!hasPartialModifier)
        {
            diagnostics ??= new();
            diagnostics.Add(NotPartialDiagnostic.CreateInfo(structSyntax));
        }

        string nameSpace = GetNameSpace(structSyntax);
        ParentClass? parentClass = GetParentClasses(structSyntax);
        var name = structSymbol.Name;

        var errs = diagnostics is null
            ? EquatableArray<DiagnosticInfo>.Empty
            : new EquatableArray<DiagnosticInfo>(diagnostics.ToArray());
        var toGenerate = new StructToGenerate(name: name, nameSpace: nameSpace, templateName: templateName, parent: parentClass);
        return new Result<(StructToGenerate, bool)>((toGenerate, true), errs);
    }

    public static Result<(string templateName, bool valid)> GetDefaults(
        GeneratorAttributeSyntaxContext ctx, CancellationToken ct)
    {
        var assemblyAttributes = ctx.TargetSymbol.GetAttributes();
        if (assemblyAttributes.IsDefaultOrEmpty)
        {
            return Result<string>.Fail();
        }

        // We only return the first config that we find
        string? templateName = null;
        List<DiagnosticInfo>? diagnostics = null;
        bool hasMisconfiguredInput = false;

        foreach (AttributeData attribute in assemblyAttributes)
        {
            if (!((attribute.AttributeClass?.Name == "StronglyTypedIdDefaultsAttribute" ||
                   attribute.AttributeClass?.Name == "StronglyTypedIdDefaults") &&
                  attribute.AttributeClass.ToDisplayString() == StronglyTypedIdDefaultsAttribute))
            {
                // wrong attribute
                continue;
            }

            if (!string.IsNullOrWhiteSpace(templateName))
            {
                if (attribute.ApplicationSyntaxReference?.GetSyntax() is { } s)
                {
                    diagnostics ??= new();
                    diagnostics.Add(MultipleAssemblyAttributeDiagnostic.CreateInfo(s));
                }

                continue;
            }

            if (!attribute.ConstructorArguments.IsEmpty)
            {
                // make sure we don't have any errors
                ImmutableArray<TypedConstant> args = attribute.ConstructorArguments;
            
                foreach (TypedConstant arg in args)
                {
                    if (arg.Kind == TypedConstantKind.Error)
                    {
                        // have an error, so don't try and do any generation
                        hasMisconfiguredInput = true;
                    }
                }
                
                templateName = (string?)args[0].Value;
                if (string.IsNullOrWhiteSpace(templateName))
                {
                    // TODO: add diagnostic
                    hasMisconfiguredInput = true;
                }
            }
            
            if (!attribute.NamedArguments.IsEmpty)
            {
                foreach (KeyValuePair<string, TypedConstant> arg in attribute.NamedArguments)
                {
                    TypedConstant typedConstant = arg.Value;
                    if (typedConstant.Kind == TypedConstantKind.Error)
                    {
                        hasMisconfiguredInput = true;
                        break;
                    }

                    templateName = (string?)typedConstant.Value;
                    
                    if (string.IsNullOrWhiteSpace(templateName))
                    {
                        // TODO: add diagnostic
                        hasMisconfiguredInput = true;
                    }
                    break;
                }
            }

            if (hasMisconfiguredInput)
            {
                // skip further generator execution and let compiler generate the errors
                break;
            }
        }

        var errors = diagnostics is null
            ? EquatableArray<DiagnosticInfo>.Empty
            : new EquatableArray<DiagnosticInfo>(diagnostics.ToArray());

        if (hasMisconfiguredInput)
        {
            return new Result<(string, bool)>((string.Empty, false), errors);
        }

        return new Result<(string, bool)>((templateName!, true), errors);
    }

    private static string GetNameSpace(StructDeclarationSyntax structSymbol)
    {
        // determine the namespace the struct is declared in, if any
        SyntaxNode? potentialNamespaceParent = structSymbol.Parent;
        while (potentialNamespaceParent != null &&
               potentialNamespaceParent is not NamespaceDeclarationSyntax
               && potentialNamespaceParent is not FileScopedNamespaceDeclarationSyntax)
        {
            potentialNamespaceParent = potentialNamespaceParent.Parent;
        }

        if (potentialNamespaceParent is BaseNamespaceDeclarationSyntax namespaceParent)
        {
            string nameSpace = namespaceParent.Name.ToString();
            while (true)
            {
                if(namespaceParent.Parent is not NamespaceDeclarationSyntax namespaceParentParent)
                {
                    break;
                }

                namespaceParent = namespaceParentParent;
                nameSpace = $"{namespaceParent.Name}.{nameSpace}";
            }

            return nameSpace;
        }
        return string.Empty;
    }

    private static ParentClass? GetParentClasses(StructDeclarationSyntax structSymbol)
    {
        TypeDeclarationSyntax? parentIdClass = structSymbol.Parent as TypeDeclarationSyntax;
        ParentClass? parentClass = null;

        while (parentIdClass != null && IsAllowedKind(parentIdClass.Kind()))
        {
            parentClass = new ParentClass(
                keyword: parentIdClass.Keyword.ValueText,
                name: parentIdClass.Identifier.ToString() + parentIdClass.TypeParameterList,
                constraints: parentIdClass.ConstraintClauses.ToString(),
                child: parentClass);

            parentIdClass = (parentIdClass.Parent as TypeDeclarationSyntax);
        }

        return parentClass;

        static bool IsAllowedKind(SyntaxKind kind) =>
            kind == SyntaxKind.ClassDeclaration ||
            kind == SyntaxKind.StructDeclaration ||
            kind == SyntaxKind.RecordDeclaration;
    }
}