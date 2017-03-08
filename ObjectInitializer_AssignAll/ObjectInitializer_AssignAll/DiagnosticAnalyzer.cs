using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace ObjectInitializer_AssignAll
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class ObjectInitializer_AssignAllAnalyzer : DiagnosticAnalyzer
    {
        private const string DiagnosticId = "ObjectInitializer_AssignAll";
        private const string Category = "Naming";

        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager,
                typeof(Resources));

        private static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager,
                typeof(Resources));

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat,
            Category, DiagnosticSeverity.Error, true, Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.RegisterSyntaxNodeAction(AnalyzeObjectInitializer, SyntaxKind.ObjectInitializerExpression);
        }

        private static void AnalyzeObjectInitializer(SyntaxNodeAnalysisContext ctx)
        {
            InitializerExpressionSyntax objectInitializer = (InitializerExpressionSyntax) ctx.Node;

            // Should be direct parent of ObjectInitializerExpression
            ObjectCreationExpressionSyntax objectCreation =
                objectInitializer.Ancestors().OfType<ObjectCreationExpressionSyntax>().First();

            SymbolInfo symbolInfo = ctx.SemanticModel.GetSymbolInfo(objectCreation.Type);

            ImmutableArray<ISymbol> members = ((INamedTypeSymbol) symbolInfo.Symbol).GetMembers();

            List<string> assignedMemberNames = objectInitializer.ChildNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Select(assignmentSyntax => ((IdentifierNameSyntax) assignmentSyntax.Left).Identifier.ValueText)
                .ToList();


            IEnumerable<ISymbol> assignableProperties = members
                .OfType<IPropertySymbol>()
                .Where(m => !m.IsIndexer && !m.IsReadOnly && !assignedMemberNames.Contains(m.Name));

            IEnumerable<ISymbol> assignableFields = members.OfType<IFieldSymbol>()
                .Where(m => !m.IsReadOnly && !m.HasConstantValue);

            IEnumerable<string> assignableMemberNames = assignableProperties
                .Concat(assignableFields)
                .Select(x => x.Name);

            List<string> unassignedMemberNames =
                assignableMemberNames
                    .Except(assignedMemberNames)
                    .ToList();

            if (unassignedMemberNames.Any())
            {
                string unassignedMembersString = string.Join(", ", unassignedMemberNames);

                Diagnostic diagnostic = Diagnostic.Create(Rule, ctx.Node.GetLocation(), symbolInfo.Symbol.Name,
                    unassignedMembersString);
                ctx.ReportDiagnostic(diagnostic);
            }
        }
    }
}