using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace ObjectInitializer_AssignAll.Test
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
            // Roslyn enable analyzer ObjectInitializer_AssignAll
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
        public void CommentsCanEnableAndDisableAnalyzerForTextSpans()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Roslyn enable analyzer ObjectInitializer_AssignAll
            Foo foo = new Foo
            {
                // PropInt not assigned, diagnostic error
                // PropInt = 1,

                // Roslyn disable analyzer ObjectInitializer_AssignAll
                Bar = new Bar
                {
                    // PropInt not assigned, but analyzer is disabled, no diagnostic error
                    // PropInt = 2,

                    // Re-enable analzyer for Baz creation
                    // Roslyn enable analyzer ObjectInitializer_AssignAll
                    Baz = new Baz
                    {
                        // PropInt not assigned, diagnostic error
                        // PropInt = 3,
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
                GetMissingAssignmentDiagnosticResult("Foo", 10, 13, "PropInt"),
                GetMissingAssignmentDiagnosticResult("Baz", 23, 21, "PropInt")
            );
        }

        [TestMethod]
        public void IgnorePropertiesComment_ExcludesPropertiesByNameFromDiagnostic()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // Roslyn enable analyzer ObjectInitializer_AssignAll
            // Roslyn ObjectInitializer_AssignAll IgnoreProperties: PropIgnored1, PropIgnored2, NonExistingProp
            var foo = new Foo
            {
                // These properties are not assigned, but also ignored by above comment
                // PropIgnored1 = 1,
                // PropIgnored2 = 1,

                // This unassigned property will give diagnostic error
                // PropUnassigned = 1,

                // Assigned property, OK'ed by analyzer
                PropAssigned = 1
            };
        }

        private class Foo
        {
            public int PropIgnored1 { get; set; }
            public int PropIgnored2 { get; set; }
            public int PropAssigned { get; set; }
            public int PropUnassigned { get; set; }
        }
    }
}
";
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("Foo", 11, 13, "PropUnassigned");
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
            // Roslyn enable analyzer ObjectInitializer_AssignAll
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
            // Roslyn enable analyzer ObjectInitializer_AssignAll
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
            // Roslyn enable analyzer ObjectInitializer_AssignAll
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
            // Roslyn enable analyzer ObjectInitializer_AssignAll
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
            // Roslyn enable analyzer ObjectInitializer_AssignAll
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
            // Roslyn enable analyzer ObjectInitializer_AssignAll
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
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("FieldInt");
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
            // Roslyn enable analyzer ObjectInitializer_AssignAll
            var foo = new Foo
            {
                // The implementation is currently limited to public only, so all other access modifiers will be ignored
                // FieldInt = 1,
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
            // Roslyn enable analyzer ObjectInitializer_AssignAll
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

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new ObjectInitializer_AssignAllAnalyzer();
        }

        private static DiagnosticResult GetMissingAssignmentDiagnosticResult(string createdObjectTypeName, int line,
            int column,
            params string[] unassignedMemberNames)
        {
            string unassignedMembersString = string.Join(", ", unassignedMemberNames);
            DiagnosticResult expected = new DiagnosticResult
            {
                Id = "ObjectInitializer_AssignAll",
                Message = $"Missing assignment for members of type '{createdObjectTypeName}': {unassignedMembersString}",
                Severity = DiagnosticSeverity.Error,
                Locations =
                    new[]
                    {
                        new DiagnosticResultLocation("Test0.cs", line, column)
                    }
            };
            return expected;
        }

        private static DiagnosticResult GetMissingAssignmentDiagnosticResult(params string[] unassignedMemberNames)
        {
            // Most code snippets in the tests are identical up to the object initializer
            const int line = 10;
            const int column = 13;
            return GetMissingAssignmentDiagnosticResult("Foo", line, column, unassignedMemberNames);
        }
    }
}