using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using AssignAll;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AssignAll2
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AssignAll2Analyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "AssignAll2";

        internal const string Properties_UnassignedMemberNames = "UnassignedMemberNames";

        internal const string CommentPattern_Disable = "AssignAll disable";
        internal const string CommentPattern_Enable = "AssignAll enable";

        private const int MaxRootNodeCacheCount = 10;

        /// <summary>
        ///     Regex that identifies:
        ///     Group 1: Name of property/field in commented assignment
        /// </summary>
        internal static readonly Regex CommentedMemberAssignmentRegex = new Regex(@"\/\/\s*(\w+)\s*=");


        // You can change these strings in the Resources.resx file. If you do not want your analyzer to be localize-able, you can use regular strings for Title and MessageFormat.
        // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Localizing%20Analyzers.md for more on localization
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Usage";

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        private IImmutableDictionary<SyntaxReference, RegionsToAnalyze> _rootNodeToAnalyzerTextSpans =
            ImmutableDictionary<SyntaxReference, RegionsToAnalyze>.Empty;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext ctx)
        {
            // TODO: Consider registering other actions that act on syntax instead of or in addition to symbols
            // See https://github.com/dotnet/roslyn/blob/master/docs/analyzers/Analyzer%20Actions%20Semantics.md for more information
            // ctx.EnableConcurrentExecution(); We have some shared state I'm not sure survives concurrency, hold off on this
            ctx.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None); // Don't touch code classified as generated

            ctx.RegisterCodeBlockStartAction<SyntaxKind>(block =>
            {
                RegionsToAnalyze regionsToAnalyze = GetOrSetCachedRegionsToAnalyzeInFile(block.CodeBlock);

                ObjectInitializerAnalyzer objectInitializerAnalyzer = new ObjectInitializerAnalyzer(regionsToAnalyze);
                block.RegisterSyntaxNodeAction(objectInitializerAnalyzer.AnalyzeObjectInitializers, SyntaxKind.ObjectInitializerExpression);
                // block.RegisterCodeBlockEndAction(objectInitializerAnalyzer.CodeBlockEndAction);
            });
        }

        private RegionsToAnalyze GetOrSetCachedRegionsToAnalyzeInFile(SyntaxNode codeBlock)
        {
            SyntaxNode rootNode = codeBlock.Ancestors().Last();
            SyntaxReference rootNodeRef = rootNode.GetReference();

            if (_rootNodeToAnalyzerTextSpans.TryGetValue(rootNodeRef, out var regionsToAnalyze))
                return regionsToAnalyze;

            regionsToAnalyze = RegionsToAnalyzeProvider.GetRegionsToAnalyze(rootNode);
            _rootNodeToAnalyzerTextSpans = _rootNodeToAnalyzerTextSpans.SetItem(rootNodeRef, regionsToAnalyze);
            Debug.WriteLine("Cache root node: " + rootNode);

            while (_rootNodeToAnalyzerTextSpans.Count > MaxRootNodeCacheCount)
            {
                SyntaxReference oldestCacheKey = _rootNodeToAnalyzerTextSpans.OrderBy(x => x.Value.Created)
                    .Last().Key;
                _rootNodeToAnalyzerTextSpans = _rootNodeToAnalyzerTextSpans.Remove(oldestCacheKey);
                Debug.WriteLine("Remove cached item: " + oldestCacheKey);
            }
            return regionsToAnalyze;
        }
    }
}
