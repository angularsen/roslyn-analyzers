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
            // ObjectInitializer_AssignAll enable
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
            // ObjectInitializer_AssignAll enable
            Foo foo = new Foo
            {
                // PropInt not assigned, diagnostic error
                // PropInt = 1,

                // ObjectInitializer_AssignAll disable
                Bar = new Bar
                {
                    // PropInt not assigned, but analyzer is disabled, no diagnostic error
                    // PropInt = 2,

                    // Re-enable analzyer for Baz creation
                    // ObjectInitializer_AssignAll enable
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
                GetMissingAssignmentDiagnosticResult("Foo", 9, 23, "PropInt"),
                GetMissingAssignmentDiagnosticResult("Baz", 22, 27, "PropInt")
            );
        }

        [TestMethod]
        public void EnableCommentAtTopOfFile_EnablesAnalyzerForEntireFile()
        {
            var testContent = @"
// ObjectInitializer_AssignAll enable
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Foo foo = new Foo
            {
                // PropInt not assigned, diagnostic error
                // PropInt = 1,
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
                GetMissingAssignmentDiagnosticResult("Foo", 9, 23, "PropInt")
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
                // PropInt = 1,
            };
        }

        private static void Main(string[] args)
        {
            // ObjectInitializer_AssignAll enable
            Foo foo = new Foo
            {
                // PropInt not assigned, diagnostic error
                // PropInt = 1,
            };
        }

        private static void CreateBarBelowEnableComment_IsAnalyzed()
        {
            Bar foo = new Bar
            {
                // PropInt not assigned, diagnostic error
                // PropInt = 1,
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
                GetMissingAssignmentDiagnosticResult("Foo", 18, 23, "PropInt"),
                GetMissingAssignmentDiagnosticResult("Bar", 27, 23, "PropInt")
            );
        }

        [TestMethod]
        public void ExceptComment_BeforeLocalDeclaration_ExcludesPropertiesFromDiagnostic()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // ObjectInitializer_AssignAll enable
            // ObjectInitializer_AssignAll except PropIgnored1, PropIgnored2, NonExistingProp
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
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("Foo", 10, 23, "PropUnassigned");
            VerifyCSharpDiagnostic(testContent, expected);
        }

        [TestMethod]
        public void ExceptComment_OnlyAffectsTheImmediatelyTrailingObjectInitializer()
        {
            var testContent = @"
// ObjectInitializer_AssignAll enable
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // ObjectInitializer_AssignAll except PropIgnored1, PropIgnored2, NonExistingProp
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

            // Analyzer should not ignore any properties for this object initializer
            var foo2 = new Foo
            {
                // These properties should no longer be ignored, and should give diagnostic errors
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
            VerifyCSharpDiagnostic(testContent,
                GetMissingAssignmentDiagnosticResult("Foo", 10, 23, "PropUnassigned"),
                GetMissingAssignmentDiagnosticResult("Foo", 24, 24, "PropIgnored1", "PropIgnored2", "PropUnassigned")
            );
        }

        [TestMethod]
        public void ExceptComment_BeforeObjectCreation_ExcludesPropertiesFromDiagnostic()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // ObjectInitializer_AssignAll enable
            // ObjectInitializer_AssignAll except PropIgnored1, PropIgnored2, NonExistingProp
            new Foo
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
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("Foo", 10, 13, "PropUnassigned");
            VerifyCSharpDiagnostic(testContent, expected);
        }

        [TestMethod]
        public void ExceptComment_BeforeReturn_ExcludesPropertiesFromDiagnostic()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static Foo Main()
        {
            // ObjectInitializer_AssignAll enable
            // ObjectInitializer_AssignAll except PropIgnored1, PropIgnored2, NonExistingProp
            return new Foo
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
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("Foo", 10, 20, "PropUnassigned");
            VerifyCSharpDiagnostic(testContent, expected);
        }

        [TestMethod]
        public void ExceptComment_BeforeLambdaReturn_ExcludesPropertiesFromDiagnostic()
        {
            var testContent = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static Foo Main()
        {
            // ObjectInitializer_AssignAll enable
            var foos = Enumerable.Repeat(1, 10).Select(i => 
            // ObjectInitializer_AssignAll except PropIgnored1, PropIgnored2, NonExistingProp
                new Foo
                {
                    // These properties are not assigned, but also ignored by above comment
                    // PropIgnored1 = 1,
                    // PropIgnored2 = 1,

                    // This unassigned property will give diagnostic error
                    // PropUnassigned = 1,

                    // Assigned property, OK'ed by analyzer
                    PropAssigned = 1
                };
            );
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
            DiagnosticResult expected = GetMissingAssignmentDiagnosticResult("Foo", 11, 17, "PropUnassigned");
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
            // ObjectInitializer_AssignAll enable
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
            // ObjectInitializer_AssignAll enable
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
            // ObjectInitializer_AssignAll enable
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
            // ObjectInitializer_AssignAll enable
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
            // ObjectInitializer_AssignAll enable
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
            // ObjectInitializer_AssignAll enable
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
            // ObjectInitializer_AssignAll enable
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
            // ObjectInitializer_AssignAll enable
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
                Message =
                    $"Missing member assignments in object initializer for type '{createdObjectTypeName}'. Properties: {unassignedMembersString}",
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
            const int line = 9;
            const int column = 23;
            return GetMissingAssignmentDiagnosticResult("Foo", line, column, unassignedMemberNames);
        }
    }
}