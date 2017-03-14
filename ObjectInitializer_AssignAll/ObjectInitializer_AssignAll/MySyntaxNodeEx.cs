using JetBrains.Annotations;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace ObjectInitializer_AssignAll
{
    internal static class MySyntaxNodeEx
    {
        /// <summary>
        ///     Returns the <see cref="SyntaxNode.Parent" /> if its type is
        ///     <typeparam name="TParent"></typeparam>
        ///     .
        /// </summary>
        /// <typeparam name="TParent"></typeparam>
        /// <param name="node">Node to get parent of.</param>
        /// <param name="kind">Optionally require parent to also be of this kind.</param>
        [CanBeNull]
        public static TParent Parent<TParent>(this SyntaxNode node, SyntaxKind? kind = null) where TParent : SyntaxNode
        {
            TParent parent = node.Parent as TParent;
            if (parent == null) return default(TParent);
            if (kind == null) return parent;

            return parent.Kind() != kind ? default(TParent) : parent;
        }

        /// <summary>
        ///     Returns the <see cref="SyntaxNode.Parent" /> if it is of kind <paramref name="kind" />.
        /// </summary>
        /// <param name="node">Node to get parent of.</param>
        /// <param name="kind">Require parent to be of this kind.</param>
        [CanBeNull]
        public static SyntaxNode Parent(this SyntaxNode node, SyntaxKind kind)
        {
            SyntaxNode parent = node.Parent;
            if (parent == null) return default(SyntaxNode);

            return parent.Kind() != kind ? default(SyntaxNode) : parent;
        }
    }
}