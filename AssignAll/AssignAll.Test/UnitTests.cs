using AssignAll.Test.Verifiers;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Xunit;

namespace AssignAll.Test
{
    public class UnitTest : CodeFixVerifier
    {
        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AssignAllAnalyzer();
        }

        private static DiagnosticResult GetMissingAssignmentDiagnosticResult(string createdObjectTypeName, int line,
            int column,
            int fileIndex,
            params string[] unassignedMemberNames)
        {
            var unassignedMembersString = string.Join(", ", unassignedMemberNames);
            var expected = new DiagnosticResult
            {
                Id = "AssignAll",
                Message =
                    $"Missing member assignments in object initializer for type '{createdObjectTypeName}'. Properties: {unassignedMembersString}",
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation($"Test{fileIndex}.cs", line, column)
                    }
            };
            return expected;
        }

        private static DiagnosticResult GetMissingAssignmentDiagnosticResult(int line, int column, string typeName, string[] unassignedMemberNames)
        {
            return GetMissingAssignmentDiagnosticResult(typeName, line, column, 0, unassignedMemberNames);
        }

        private static DiagnosticResult GetMissingAssignmentDiagnosticResult(params string[] unassignedMemberNames)
        {
            // Most code snippets in the tests are identical up to the object initializer
            const int line = 9;
            const int column = 23;
            return GetMissingAssignmentDiagnosticResult("Foo", line, column, 0, unassignedMemberNames);
        }

        [Fact]
        public void AllPropertiesAndFieldsAssigned_AddsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            Foo foo = new Foo
            {
                PropInt = 1,
                PropString = ""2"",
                FieldBool = true
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; }
            public bool FieldBool;
        }
    }
}        
";

            VerifyCSharpDiagnostic(testContent);
        }

        // These properties are not assigned, but excluded from diagnostic due to being commented out.
        // Test different whitespace variations and different positions in assignment expression list.
        [Fact]
        public void CommentedMemberAssignments_ExcludedFromDiagnostic()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main()
        {
            // AssignAll enable
            var foo = new Foo
            {
                // Commented assignments after opening brace.
                // PropCommented1 = 1,

                // Assigned property, OK by analyzer
                PropAssigned = 1,

                // Commented assignments just before closing brace
                //PropCommented2 = ,
                // PropCommented3=,
            };
        }

        private class Foo
        {
            public int PropAssigned { get; set; }
            public int PropCommented1 { get; set; }
            public int PropCommented2 { get; set; }
            public int PropCommented3 { get; set; }
            public int PropUnassigned { get; set; }
        }
    }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("Foo", 9, 23, 0, "PropUnassigned");
            VerifyCSharpDiagnostic(testContent, expected);
        }

        [Fact]
        public void DoesNotAddDiagnostics_IfNoEnableCommentAbove()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var foo = new Foo
            {
                // Missing property assignments, but diagnostics not enabled by comment
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        // No diagnostics expected to show up
        [Fact]
        public void EmptyCode_AddsNoDiagnostics()
        {
            var test = @"";
            VerifyCSharpDiagnostic(test);
        }

        [Fact]
        public void EnableAndDisableComments_EnablesAndDisablesAnalyzerForTextSpans()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            Foo foo = new Foo
            {
                // PropInt not assigned, diagnostic error

                // AssignAll disable
                Bar = new Bar
                {
                    // PropInt not assigned, but analyzer is disabled, no diagnostic error

                    // Re-enable analzyer for Baz creation
                    // AssignAll enable
                    Baz = new Baz
                    {
                        // PropInt not assigned, diagnostic error
                    }
                }
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public Bar Bar { get; internal set; }
        }

        private class Bar
        {
            public int PropInt { get; set; }
        }

        private class Baz
        {
            public int PropInt { get; set; }
        }
    }
}        
";

            // Bar type has no diagnostic errors
            VerifyCSharpDiagnostic(testContent,
                GetMissingAssignmentDiagnosticResult("Foo", 9, 23, 0, "PropInt"),
                GetMissingAssignmentDiagnosticResult("Baz", 20, 27, 0, "PropInt")
            );
        }

        [Fact]
        public void EnableComment_DoesNotAffectOtherFiles()
        {
            var types = @"
namespace TestCode
{
    internal class Foo
    {
        public int FooPropInt { get; set; }
    }

    internal class Bar
    {
        public int BarPropInt { get; set; }
    }
";

            var fooInitializer = @"
namespace TestCode
{
    internal static class FooInitializer
    {
        private static void Initialize()
        {
            // AssignAll enable
            var foo = new Foo
            {
            };
        }
    }
}
";
            var barInitializer = @"
namespace TestCode
{
    internal static class BarInitializer
    {
        private static void Initialize()
        {
            var bar = new Bar
            {
            };
        }
    }
}
";
            string[] fileSources = {types, fooInitializer, barInitializer};

            DiagnosticResult expectedDiagnostics = GetMissingAssignmentDiagnosticResult("Foo", 9, 23, 1, "FooPropInt");
            VerifyCSharpDiagnostic(fileSources, expectedDiagnostics);
        }

        [Fact]
        public void EnableCommentAtTopOfFile_EnablesAnalyzerForEntireFile()
        {
            var testContent = @"
// AssignAll enable
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Foo foo = new Foo
            {
                // PropInt not assigned, diagnostic error
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
        }
    }
}        
";

            // Bar type has no diagnostic errors
            VerifyCSharpDiagnostic(testContent,
                GetMissingAssignmentDiagnosticResult("Foo", 9, 23, 0, "PropInt")
            );
        }

        // Verify that analyzer does not care about syntax scopes by adding it inside a method
        [Fact]
        public void EnableCommentInsideMethod_EnablesAnalyzerForEntireFileBelow()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void CreateBarAboveEnableComment_NotAnalyzed()
        {
            Bar foo = new Bar
            {
                // PropInt not assigned, diagnostic error
            };
        }

        private static void Main(string[] args)
        {
            // AssignAll enable
            Foo foo = new Foo
            {
                // PropInt not assigned, diagnostic error
            };
        }

        private static void CreateBarBelowEnableComment_IsAnalyzed()
        {
            Bar foo = new Bar
            {
                // PropInt not assigned, diagnostic error
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
        }

        private class Bar
        {
            public int PropInt { get; set; }
        }
    }
}        
";

            // Bar type has no diagnostic errors
            VerifyCSharpDiagnostic(testContent,
                GetMissingAssignmentDiagnosticResult("Foo", 17, 23, 0, "PropInt"),
                GetMissingAssignmentDiagnosticResult("Bar", 25, 23, 0, "PropInt")
            );
        }


        [Fact]
        public void FieldDeclaration_IsAnalyzed()
        {
            var testContent = @"
// AssignAll enable
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static readonly Foo _myField = new Foo { };

        private class Foo
        {
            public int FieldInt;
        }
    }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("Foo", 7, 48, 0, "FieldInt");
            VerifyCSharpDiagnostic(testContent, expected);
        }

        [Fact]
        public void FieldNotAssigned_AddsDiagnosticWithFieldName()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
    {
            // AssignAll enable
            var foo = new Foo
        {   
                // FieldInt not assigned, diagnostic error
            };
        }

        private class Foo
        {
            public int FieldInt;
        }
        }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("FieldInt");
            VerifyCSharpDiagnostic(testContent, expected);
        }


        [Fact]
        public void FieldNotAssigned_NoCommentToEnableAnalyzer_AddsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Here is missing comment to enable analyzer
            var foo = new Foo
            {
                // Not assigned, should give error
                // FieldInt = 1,
            };
        }

        private class Foo
        {
            public int FieldInt;
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }


        [Fact]
        public void IndexerPropertyNotAssigned_AddsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            var foo = new Foo
            {
            };
        }

        private class Foo
        {
            public int this[int val] => val;
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        [Fact]
        public void ListInitializer_AddsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var foo = new List<int> { 1, 2, 3 };
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }


        [Fact]
        public void MethodsNotAssigned_AddsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            var foo = new Foo
            {
            };
        }

        private class Foo
        {
            public void MethodVoid() { }
            public int MethodInt() => 1;
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        /// <remarks>
        ///     TODO Revisit this when the implementation supports looking at context and whether the member can be assigned
        ///     or not.
        /// </remarks>
        [Fact]
        public void NonPublicFieldsNotAssigned_AddsNoDiagnostics()
        {
            string[] accessModifiers = {"private", "internal", "protected", "protected internal"};
            foreach (var accessModifier in accessModifiers)
            {
                var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            var foo = new Foo
            {
                // FieldInt not assigned, diagnostic currently limited to public only, so all other access modifiers will be ignored
            };
        }

        private class Foo
        {
            {{accessModifier}} int FieldInt;
        }
    }
}
".Replace("{{accessModifier}}", accessModifier);

                VerifyCSharpDiagnostic(testContent);
            }
        }

        [Fact]
        public void PropertiesNotAssigned_AddsDiagnosticWithPropertyNames()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            var foo = new Foo
            {
                // PropInt and PropString not assigned, diagnostic error
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
        }
    }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("PropInt", "PropString");
            VerifyCSharpDiagnostic(testContent, expected);
        }

        [Fact]
        public void PropertiesNotAssigned_InFileWithTopLevelStatements_AddsDiagnosticWithPropertyNames()
        {
            var testContent = @"
// AssignAll enable
var foo = new Foo
{
    // PropInt and PropString not assigned, diagnostic error
};

// Add methods and nested types available to top level statements via a partial Program class.
public static partial class Program
{
    private class Foo
    {
        public int PropInt { get; set; }
        public string PropString { get; set; }
    }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult(line: 3, column: 11, typeName: "Foo",
                new[] { "PropInt", "PropString" });
            VerifyCSharpDiagnostic(testContent, expected);
        }

        [Fact]
        public void PropertiesNotAssigned_NoCommentToEnableAnalyzer_AddsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            var foo = new Foo
            {
                // Diagnostics should flag that these properties are not set
                // PropInt = 1,
                // PropString = ""my string""
            };
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        [Fact]
        public void ReadOnlyFieldNotAssigned_AddsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            var foo = new Foo
            {
                // Cannot assign read-only field
                // FieldIntReadOnly = 1,
            };
        }

        private class Foo
        {
            public readonly int PropIntReadOnly;
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        [Fact]
        public void ReadOnlyPropertyNotAssigned_AddsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            var foo = new Foo
            {
                // Cannot assign read-only property
                // PropIntReadOnly = 1,
            };
        }

        private class Foo
        {
            public int PropIntReadOnly { get; }
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        [Fact]
        public void UnassignedMembersWithoutObjectInitializer_AddsNoDiagnostics()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Unassigned properties and fields are ignored for this type of construction
            // AssignAll enable
            var foo = new Foo();
            // foo.FieldInt = 1;
        }

        private class Foo
        {   
            public int FieldInt;
        }
    }
}
";
            VerifyCSharpDiagnostic(testContent);
        }

        [Fact]
        public void Inheritance_AnalyzesMembersOfBaseTypes()
        {
            var testContent = @"
// AssignAll enable
// EXAMPLE 004 - Analyzer should also consider public members from any base types.
namespace Samples.ConsoleNet6;

public static class Example004_Inheritance
{
    public static void Irrelevant()
    {
        // This should give analyzer error:
        // Missing member assignments in object initializer for type 'Derived'. Properties: BasePropUnassigned, DerivedPropUnassigned
        var foo = new Derived
        {
            // Commented assignments after opening brace.
            // BasePropCommented = ,
            // DerivedPropCommented = ,

            // Assigned property, OK by analyzer
            BasePropAssigned = 1,
            DerivedPropAssigned = 1,
        };
    }
}

internal class Base
{
    public int BasePropAssigned { get; set; }
    public int BasePropCommented { get; set; }
    public int BasePropUnassigned { get; set; }
}

internal class Derived : Base
{
    public int DerivedPropAssigned { get; set; }
    public int DerivedPropCommented { get; set; }
    public int DerivedPropUnassigned { get; set; }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult(line: 12, column: 19, typeName: "Derived",
                new[] { "BasePropUnassigned", "DerivedPropUnassigned" });
            VerifyCSharpDiagnostic(testContent, expected);
        }

    }
}