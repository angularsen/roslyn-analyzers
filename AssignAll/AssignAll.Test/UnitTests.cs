using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace AssignAll.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        // No diagnostics expected to show up
        [TestMethod]
        public void EmptyCode_AddsNoDiagnostics()
        {
            var test = @"";
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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
        [TestMethod]
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

        // These properties are not assigned, but excluded from diagnostic due to being commented out.
        // Test different whitespace variations and different positions in assignment expression list.
        [TestMethod]
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

                // Assigned property, OK'ed by analyzer
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

        [TestMethod]
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
                // ProtInt and PropString not assigned, diagnostic error
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

        [TestMethod]
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


        [TestMethod]
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


        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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


        [TestMethod]
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

        [TestMethod]
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


        [TestMethod]
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

        /// <remarks>
        ///     TODO Revisit this when the implementation supports looking at context and whether the member can be assigned
        ///     or not.
        /// </remarks>
        [TestMethod]
        public void NonPublicFieldsNotAssigned_AddsNoDiagnostics()
        {
            string[] accessModifiers = {"private", "internal", "protected", "protected internal"};
            foreach (string accessModifier in accessModifiers)
            {
                string testContent = @"
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





        [TestMethod]
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

        [TestMethod]
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

        [TestMethod]
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

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new AssignAllAnalyzer();
        }

        private static DiagnosticResult GetMissingAssignmentDiagnosticResult(string createdObjectTypeName, int line,
            int column,
            int fileIndex,
            params string[] unassignedMemberNames)
        {
            string unassignedMembersString = string.Join(", ", unassignedMemberNames);
            DiagnosticResult expected = new DiagnosticResult
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

        private static DiagnosticResult GetMissingAssignmentDiagnosticResult(params string[] unassignedMemberNames)
        {
            // Most code snippets in the tests are identical up to the object initializer
            const int line = 9;
            const int column = 23;
            return GetMissingAssignmentDiagnosticResult("Foo", line, column, 0, unassignedMemberNames);
        }
    }
}
