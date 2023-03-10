using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace StronglyTypedIds
{
    /// <inheritdoc />
    [Generator]
    public class StronglyTypedIdGenerator : IIncrementalGenerator
    {
        const string TemplatePrefix = "StronglyTypedId_"; 
        const int TemplatePrefixLength = 16; 
        const string TemplateSuffix = ".txt"; 

        /// <inheritdoc />
        public void Initialize(IncrementalGeneratorInitializationContext context)
        {
            // Register the attribute and enum sources
            context.RegisterPostInitializationOutput(i =>
            {
                i.AddSource("StronglyTypedIdAttribute.g.cs", EmbeddedSources.StronglyTypedIdAttributeSource);
                i.AddSource("StronglyTypedIdDefaultsAttribute.g.cs", EmbeddedSources.StronglyTypedIdDefaultsAttributeSource);
            });

            IncrementalValuesProvider<(string Path, string Name, string? Content)> allTemplates = context.AdditionalTextsProvider
                .Where(template => Path.GetExtension(template.Path).Equals(TemplateSuffix, StringComparison.OrdinalIgnoreCase)
                                   && Path.GetFileName(template.Path).StartsWith(TemplatePrefix, StringComparison.OrdinalIgnoreCase))
                .Select((template, ct) => (
                    Path: template.Path,
                    Name: Path.GetFileNameWithoutExtension(template.Path).Substring(TemplatePrefixLength),
                    Content: template.GetText(ct)?.ToString()));

            var templatesWithErrors = allTemplates
                .Where(template => string.IsNullOrWhiteSpace(template.Name) || template.Content is null);

            var templates = allTemplates
                .Where(template => !string.IsNullOrWhiteSpace(template.Name) && template.Content is not null)
                .Collect()
                .Select((values, ct) =>
                {
                    var dict = new Dictionary<string, string>(values.Length, StringComparer.OrdinalIgnoreCase);
                    foreach (var value in values)
                    {
                        // TODO: document duplicate names
                        dict[value.Name] = value.Content!;
                    }

                    return dict;
                });
                
            IncrementalValuesProvider<Result<(StructToGenerate info, bool valid)>> structAndDiagnostics = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    Parser.StronglyTypedIdAttribute,
                    predicate: (node, _) => node is StructDeclarationSyntax,
                    transform: Parser.GetStructSemanticTarget)
                .Where(static m => m is not null);

            IncrementalValuesProvider<Result<(string defaultTemplateName, bool valid)>> defaultsAndDiagnostics = context.SyntaxProvider
                .ForAttributeWithMetadataName(
                    Parser.StronglyTypedIdDefaultsAttribute,
                    predicate: (node, _) => node is CompilationUnitSyntax,
                    transform: Parser.GetDefaults)
                .Where(static m => m is not null);

            context.RegisterSourceOutput(
                structAndDiagnostics.SelectMany((x, _) => x.Errors),
                static (context, info) => context.ReportDiagnostic(Diagnostic.Create(info.Descriptor, info.Location)));

            context.RegisterSourceOutput(
                defaultsAndDiagnostics.SelectMany((x, _) => x.Errors),
                static (context, info) => context.ReportDiagnostic(Diagnostic.Create(info.Descriptor, info.Location)));

            // context.RegisterSourceOutput(
            //     templatesWithErrors,
            //     static (context, info) => context.ReportDiagnostic(Diagnostic.Create(info.Descriptor, info.Location)));

            IncrementalValuesProvider<StructToGenerate> structs = structAndDiagnostics
                .Where(static x => x.Value.valid)
                .Select((result, _) => result.Value.info);

            IncrementalValueProvider<ImmutableArray<string>> allDefaultTemplateNames = defaultsAndDiagnostics
                .Where(static x => x.Value.valid)
                .Select((result, _) => result.Value.defaultTemplateName)
                .Collect();

            // we can only use one default attribute
            // more than one is an error, but lets do our best
            IncrementalValueProvider<string?> selectedDefaultTemplateName = allDefaultTemplateNames
                .Select((all, _) => all.IsDefaultOrEmpty ? (string?)null : all[0]);

            var structsWithDefaultsAndTemplates =
                structs.Combine(templates).Combine(selectedDefaultTemplateName);

            context.RegisterSourceOutput(structsWithDefaultsAndTemplates,
                static (spc, source) => Execute(source.Left.Left, source.Left.Right, source.Right, spc));
        }

        private static void Execute(
            StructToGenerate idToGenerate,
            Dictionary<string, string> templates,
            string? defaultTemplateName, 
            SourceProductionContext context)
        {
            var sb = new StringBuilder();

            var templateName = idToGenerate.TemplateName ?? defaultTemplateName ?? "Guid";
            if (templates.TryGetValue(templateName, out var template))
            {

                var result = SourceGenerationHelper.CreateId(
                    idToGenerate.NameSpace,
                    idToGenerate.Name,
                    idToGenerate.Parent,
                    template,
                    sb);

                var fileName = SourceGenerationHelper.CreateSourceName(
                    idToGenerate.NameSpace,
                    idToGenerate.Parent,
                    idToGenerate.Name);
                context.AddSource(fileName, SourceText.From(result, Encoding.UTF8));                
            }
            else
            {
                // context.ReportDiagnostic(); Unknown template name
            }
        }
    }
}