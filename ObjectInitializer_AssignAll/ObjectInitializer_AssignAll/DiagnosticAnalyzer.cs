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
            var objectCreation = objectInitializer.Parent as ObjectCreationExpressionSyntax;

            // Only handle initializers immediately following object creation,
            // not sure what the scenario would be since we are only registered for
            // object initializers, not things like list/collection initializers.
            if (objectCreation == null)
                return;

            // For now, only perform analysis when explicitly enabled by comment
            // TODO Support other means to enable, such as static configuration (analyze all/none by default), attributes on types and members
            bool isEnabledByComment = IsAnalysisEnabledByLeadingComment(objectCreation);
            if (!isEnabledByComment)
                return;

            SymbolInfo symbolInfo = ctx.SemanticModel.GetSymbolInfo(objectCreation.Type);

            ImmutableArray<ISymbol> members = ((INamedTypeSymbol) symbolInfo.Symbol).GetMembers();

            List<string> assignedMemberNames = objectInitializer.ChildNodes()
                .OfType<AssignmentExpressionSyntax>()
                .Select(assignmentSyntax => ((IdentifierNameSyntax) assignmentSyntax.Left).Identifier.ValueText)
                .ToList();


            // TODO Check if member is assignable using Roslyn data flow analysis instead of these constraints,
            // as that is the only way to properly determine if it is assignable or not in a context
            IEnumerable<ISymbol> assignableProperties = members
                .OfType<IPropertySymbol>()
                .Where(m =>
                    // Exclude indexer properties
                        !m.IsIndexer &&
                        // Exclude read-only getter properties
                        !m.IsReadOnly &&
                        // Simplification, only care about public members
                        m.DeclaredAccessibility == Accessibility.Public);

            IEnumerable<ISymbol> assignableFields = members.OfType<IFieldSymbol>()
                .Where(m =>
                    // Exclude readonly fields
                        !m.IsReadOnly &&
                        // Exclude const fields
                        !m.HasConstantValue &&
                        // Exclude generated backing fields for properties
                        !m.IsImplicitlyDeclared &&
                        // Simplification, only care about public members
                        m.DeclaredAccessibility == Accessibility.Public);

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

        private static bool IsAnalysisEnabledByLeadingComment(ObjectCreationExpressionSyntax objectCreation)
        {
            // Case 1: Comment before variable declaration and assignment:
            // <comment here>
            // Foo foo = new Foo { .. };
            if (new[] {SyntaxKind.EqualsValueClause, SyntaxKind.VariableDeclarator, SyntaxKind.VariableDeclaration}
                .SequenceEqual(objectCreation.Ancestors().Take(3).Select(x => x.Kind())))
            {
                var variableDeclaration = (VariableDeclarationSyntax)objectCreation.Ancestors().Skip(2).First();
                IdentifierNameSyntax identifierName =
                    variableDeclaration.ChildNodes().OfType<IdentifierNameSyntax>().First();

                SyntaxTrivia[] singleLineComments =
                    identifierName.Identifier.LeadingTrivia.Where(x => x.IsKind(SyntaxKind.SingleLineCommentTrivia))
                        .ToArray();

                return singleLineComments.Any(IsSingleLineCommentForEnablingAnalyzer);
            }

            // Case 2: Comment before assignment
            // Foo foo;
            // <comment here>
            // foo = new Foo { .. };
            // Did not recognize syntax to locate leading comment for object initializer
            if (new[] {SyntaxKind.SimpleAssignmentExpression}
                .SequenceEqual(objectCreation.Ancestors().Take(1).Select(x => x.Kind())))
            {
                var assignmentExpression = (AssignmentExpressionSyntax) objectCreation.Ancestors().First();
                IdentifierNameSyntax identifierName =
                    assignmentExpression.ChildNodes().OfType<IdentifierNameSyntax>().First();

                SyntaxTrivia[] singleLineComments =
                    identifierName.Identifier.LeadingTrivia.Where(x => x.IsKind(SyntaxKind.SingleLineCommentTrivia))
                        .ToArray();

                return singleLineComments.Any(IsSingleLineCommentForEnablingAnalyzer);
            }

            // Did not recognize syntax to locate leading comment
            return false;
        }

        private static bool IsSingleLineCommentForEnablingAnalyzer(SyntaxTrivia comment)
        {
            return comment.ToString().StartsWith("// Roslyn enable analyzer ObjectInitializer_AssignAll");
        }
    }
}