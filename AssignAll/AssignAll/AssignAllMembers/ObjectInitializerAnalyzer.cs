using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AssignAll.AssignAllMembers
{
    internal class ObjectInitializerAnalyzer
    {
        private readonly RegionsToAnalyze _regionsToAnalyze;

        public ObjectInitializerAnalyzer(RegionsToAnalyze regionsToAnalyze)
        {
            _regionsToAnalyze = regionsToAnalyze;
        }

        internal void AnalyzeObjectInitializers(SyntaxNodeAnalysisContext ctx)
        {
            var objectInitializer = (InitializerExpressionSyntax) ctx.Node;

            // For now, only perform analysis when explicitly enabled by comment.
            // TODO Support other means to enable, such as static configuration (analyze all/none by default), attributes on types and members
            if (!_regionsToAnalyze.TextSpans.Any(enabledTextSpan => enabledTextSpan.Contains(objectInitializer.SpanStart))) return;

            // Only handle initializers immediately following object creation,
            // not sure what the scenario would be since we are only registered for
            // object initializers, not things like list/collection initializers.
            if (!(objectInitializer.Parent is ObjectCreationExpressionSyntax objectCreation))
                return;

            var objectCreationNamedType =
                (INamedTypeSymbol) ctx.SemanticModel.GetSymbolInfo(objectCreation.Type).Symbol;
            if (objectCreationNamedType == null)
                return;

            IEnumerable<ISymbol> membersEnumerable = objectCreationNamedType.GetMembers();
            var baseType = objectCreationNamedType.BaseType;
            for (int i = 0; i < 100; i++) // Max recursion
            {
                if (baseType == null || baseType.SpecialType == SpecialType.System_Object) break;
                membersEnumerable = membersEnumerable.Concat(baseType.GetMembers());
                baseType = baseType.BaseType;
            }

            var members = membersEnumerable.OrderBy(m => m.Name).ToImmutableList();

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

            ImmutableArray<string> ignoredPropertyNames = GetIgnoredPropertyNames(objectCreation);

            List<string> unassignedMemberNames =
                assignableMemberNames
                    .Except(assignedMemberNames)
                    .Except(ignoredPropertyNames)
                    .ToList();

            if (unassignedMemberNames.Any())
            {
                var unassignedMembersString = string.Join(", ", unassignedMemberNames);

                ImmutableDictionary<string, string> properties =
                    new Dictionary<string, string>
                        {
                            {
                                AssignAllAnalyzer.Properties_UnassignedMemberNames,
                                unassignedMembersString
                            }
                        }
                        .ToImmutableDictionary();

                var diagnostic = Diagnostic.Create(AssignAllAnalyzer.Rule,
                    objectCreation.GetLocation(),
                    properties, objectCreationNamedType.Name, unassignedMembersString);

                ctx.ReportDiagnostic(diagnostic);
            }
        }

        // public void CodeBlockEndAction(CodeBlockAnalysisContext ctx)
        // {
        // }

        private static ImmutableArray<string> GetIgnoredPropertyNames(ObjectCreationExpressionSyntax objectCreation)
        {
            ImmutableArray<string> propertiesByCommentedAssignment =
                GetIgnoredPropertyNamesFromCommentedAssignments(objectCreation);
            return propertiesByCommentedAssignment;
        }

        private static ImmutableArray<string> GetIgnoredPropertyNamesFromCommentedAssignments(
            ObjectCreationExpressionSyntax objectCreation)
        {
            // Case 1: Commented member assignments before one or more actual member assignments
            // return new Foo {
            //   // Prop1 = null,
            //   // Prop2 = null,
            //   Prop3 = 1
            // };
            SyntaxTriviaList memberAssignmentsLeadingTrivia =
                new SyntaxTriviaList().AddRange(objectCreation.Initializer.Expressions
                    .OfType<AssignmentExpressionSyntax>()
                    .SelectMany(e => e.Left.GetLeadingTrivia()));

            // Case 2: Commented member assignments before closing brace
            // return new Foo {
            //   Prop3 = 1
            //   // Prop1 = null,
            //   // Prop2 = null,
            // };
            SyntaxTriviaList closingBraceLeadingTrivia = objectCreation.Initializer.CloseBraceToken.LeadingTrivia;

            return
                memberAssignmentsLeadingTrivia
                    .Concat(closingBraceLeadingTrivia)
                    .Where(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia))
                    .Select(trivia => AssignAllAnalyzer.CommentedMemberAssignmentRegex
                        .Match(trivia.ToString()))
                    .Where(match => match.Success)
                    .Select(match => match.Groups[1].Value)
                    .ToImmutableArray();
        }
    }
}