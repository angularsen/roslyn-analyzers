using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace AssignAll
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AssignAll_Analyzer : DiagnosticAnalyzer
    {
        internal const string Properties_UnassignedMemberNames = "UnassignedMemberNames";
        internal const string DiagnosticId = "AssignAll";

        internal const string CommentPattern_Disable = "AssignAll disable";
        internal const string CommentPattern_Enable = "AssignAll enable";
        private const string Category = "Usage";

        private const int MaxRootNodeCacheCount = 10;

        private static readonly LocalizableString Title = new LocalizableResourceString(
            nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));

        private static readonly LocalizableString MessageFormat =
            new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager,
                typeof(Resources));

        private static readonly LocalizableString Description =
            new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager,
                typeof(Resources));

        internal static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title,
            MessageFormat,
            Category, DiagnosticSeverity.Error, true, Description,
            "https://github.com/angularsen/roslyn-analyzers#assignall");

        /// <summary>
        ///     Regex that identifies:
        ///     Group 1: Name of property/field in commented assignment
        /// </summary>
        internal static readonly Regex CommentedMemberAssignmentRegex = new Regex(@"\/\/\s*(\w+)\s*=");

        private IImmutableDictionary<SyntaxReference, RegionsToAnalyze> _rootNodeToAnalyzerTextSpans =
            ImmutableDictionary<SyntaxReference, RegionsToAnalyze>.Empty;

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
            ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext ctx)
        {
            ctx.RegisterCodeBlockStartAction<SyntaxKind>(block =>
            {
                RegionsToAnalyze regionsToAnalyze = GetOrSetCachedRegionsToAnalyzeInFile(block.CodeBlock);

                ObjectInitializerAnalyzer objectInitializerAnalyzer = new ObjectInitializerAnalyzer(regionsToAnalyze);
                block.RegisterSyntaxNodeAction(objectInitializerAnalyzer.AnalyzeObjectInitializers,
                    SyntaxKind.ObjectInitializerExpression);
                block.RegisterCodeBlockEndAction(objectInitializerAnalyzer.CodeBlockEndAction);
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