using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Xunit;
using VerifyCS = AssignAll.Test.Verifiers.CSharpCodeFixVerifier<
    AssignAll.AssignAllAnalyzer,
    AssignAll.AssignAllCodeFixProvider>;

namespace AssignAll.Test
{
    public class UnitTest
    {
        [Fact]
        public async Task AllPropertiesAndFieldsAssigned_AddsNoDiagnostics()
        {
            var test = @"
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
            public string PropString { get; set; }
            public bool FieldBool;
        }
    }
}        
";

            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // These properties are not assigned, but excluded from diagnostic due to being commented out.
        // Test different whitespace variations and different positions in assignment expression list.
        [Fact]
        public async Task CommentedMemberAssignments_ExcludedFromDiagnostic()
        {
            var test = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main()
        {
            // AssignAll enable
            var foo = {|#0:new Foo
            {
                // Commented assignments after opening brace.
                // PropCommented1 = 1,

                // Assigned property, OK by analyzer
                PropAssigned = 1,

                // Commented assignments just before closing brace
                //PropCommented2 = ,
                // PropCommented3=,
            }|#0};
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
            var expected = VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "PropUnassigned");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task DoesNotAddDiagnostics_IfNoEnableCommentAbove()
        {
            var test = @"
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        // No diagnostics expected to show up
        [Fact]
        public async Task EmptyCode_AddsNoDiagnostics()
        {
            var test = @"";
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Theory]
        [InlineData("// AssignAll")]
        [InlineData("//AssignAll")]
        public async Task EnableAndDisableComments_EnablesAndDisablesAnalyzerForTextSpans(string commentStart)
        {
            var code = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            {commentStart} enable
            Foo foo = {|#0:new Foo
            {
                // PropInt not assigned, diagnostic error

                {commentStart} disable
                Bar = new Bar
                {
                    // PropInt not assigned, but analyzer is disabled, no diagnostic error

                    // Re-enable analyzer for Baz creation
                    {commentStart} enable
                    Baz = {|#1:new Baz
                    {
                        // PropInt not assigned, diagnostic error
                    }|#1}
                }
            }|#0};
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public Bar Bar { get; internal set; }
        }

        private class Bar
        {
            public int PropInt { get; set; }
            public Baz Baz { get; internal set; }
        }

        private class Baz
        {
            public int PropInt { get; set; }
        }
    }
}
";
            var test = code.Replace("{commentStart}", commentStart);

            // Bar type has no diagnostic errors
            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "PropInt"),
                VerifyCS.Diagnostic("AssignAll").WithLocation(1).WithArguments("Baz", "PropInt"));
        }

        [Fact]
        public async Task EnableComment_DoesNotAffectOtherFiles()
        {
            var types = @"
namespace TestCode
{
    internal class Foo
    {
        public int PropInt { get; set; }
    }

    internal class Bar
    {
        public int PropInt { get; set; }
    }
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
            var foo = {|#0:new Foo
            {
            }|#0};
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

            // Bar does not have AssignAll enabled and should have no diagnostics.
            await VerifyCS.VerifyAnalyzerAsync(new[] { fooInitializer, barInitializer, types },
                VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "PropInt"));
        }

        [Fact]
        public async Task EnableCommentAtTopOfFile_EnablesAnalyzerForEntireFile()
        {
            var test = @"
// AssignAll enable
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            Foo foo = {|#0:new Foo
            {
                // PropInt not assigned, diagnostic error
            }|#0};
        }

        private class Foo
        {
            public int PropInt { get; set; }
        }
    }
}        
";

            var expected = VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "PropInt");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        // Verify that analyzer does not care about syntax scopes by adding it inside a method
        [Fact]
        public async Task EnableCommentInsideMethod_EnablesAnalyzerForEntireFileBelow()
        {
            var test = @"
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
            Foo foo = {|#0:new Foo
            {
                // PropInt not assigned, diagnostic error
            }|#0};
        }

        private static void CreateBarBelowEnableComment_IsAnalyzed()
        {
            Bar foo = {|#1:new Bar
            {
                // PropInt not assigned, diagnostic error
            }|#1};
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

            await VerifyCS.VerifyAnalyzerAsync(test,
                VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "PropInt"),
                VerifyCS.Diagnostic("AssignAll").WithLocation(1).WithArguments("Bar", "PropInt"));
        }


        [Fact]
        public async Task FieldDeclaration_IsAnalyzed()
        {
            var test = @"
// AssignAll enable
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static readonly Foo _myField = {|#0:new Foo { }|#0};

        private class Foo
        {
            public int FieldInt;
        }
    }
}
";
            var expected = VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "FieldInt");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task FieldNotAssigned_AddsDiagnosticWithFieldName()
        {
            var test = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            var foo = {|#0:new Foo
            {   
                // FieldInt not assigned, diagnostic error
            }|#0};
        }

        private class Foo
        {
            public int FieldInt;
        }
    }
}
";
            var expected = VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "FieldInt");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }


        [Fact]
        public async Task FieldNotAssigned_NoCommentToEnableAnalyzer_AddsNoDiagnostics()
        {
            var test = @"
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }


        [Fact]
        public async Task IndexerPropertyNotAssigned_AddsNoDiagnostics()
        {
            var test = @"
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ListInitializer_AddsNoDiagnostics()
        {
            var test = @"
using System.Collections.Generic;
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }


        [Fact]
        public async Task MethodsNotAssigned_AddsNoDiagnostics()
        {
            var test = @"
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        /// <remarks>
        ///     TODO Revisit this when the implementation supports looking at context and whether the member can be assigned
        ///     or not.
        /// </remarks>
        [Fact]
        public async Task NonPublicFieldsNotAssigned_AddsNoDiagnostics()
        {
            string[] accessModifiers = {"private", "internal", "protected", "protected internal"};
            foreach (var accessModifier in accessModifiers)
            {
                var test = @"
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

                await VerifyCS.VerifyAnalyzerAsync(test);
            }
        }

        [Fact]
        public async Task PropertiesNotAssigned_AddsDiagnosticWithPropertyNames()
        {
            var test = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            var foo = {|#0:new Foo
            {
                // PropInt and PropString not assigned, diagnostic error
            }|#0};
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
        }
    }
}
";

            var expected = VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "PropInt, PropString");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task FileWithTopLevelStatements_AddsDiagnostic()
        {
            var test = @"
// AssignAll enable
var foo = {|#0:new Program.Foo
{
    // PropInt and PropString not assigned, diagnostic error
}|#0};

// Add methods and nested types available to top level statements via a partial Program class.
public static partial class Program
{
    internal class Foo
    {
        public int PropInt { get; set; }
        public string PropString { get; set; }
    }
}
";

            // Baz does not have AssignAll enabled and should have no diagnostics.
            await VerifyCS.VerifyAnalyzerAsync(test,
                t => t.TestState.OutputKind = OutputKind.ConsoleApplication,
                VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "PropInt, PropString")
            );
        }

        [Fact]
        public async Task PropertiesNotAssigned_NoCommentToEnableAnalyzer_AddsNoDiagnostics()
        {
            var test = @"
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ReadOnlyFieldNotAssigned_AddsNoDiagnostics()
        {
            var test = @"
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task ReadOnlyPropertyNotAssigned_AddsNoDiagnostics()
        {
            var test = @"
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task UnassignedMembersWithoutObjectInitializer_AddsNoDiagnostics()
        {
            var test = @"
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
            await VerifyCS.VerifyAnalyzerAsync(test);
        }

        [Fact]
        public async Task Inheritance_AnalyzesMembersOfBaseTypes()
        {
            var test = @"
// AssignAll enable
// EXAMPLE 004 - Analyzer should also consider public members from any base types.
namespace Samples.ConsoleNet6
{
    public static class Example004_Inheritance
    {
        public static void Irrelevant()
        {
            // This should give analyzer error:
            // Missing member assignments in object initializer for type 'Derived'. Properties: BasePropUnassigned, DerivedPropUnassigned
            var foo = {|#0:new Derived
            {
                // Commented assignments after opening brace.
                // BasePropCommented = ,
                // DerivedPropCommented = ,

                // Assigned property, OK by analyzer
                BasePropAssigned = 1,
                DerivedPropAssigned = 1,
            }|#0};
        }

        private class Base
        {
            public int BasePropAssigned { get; set; }
            public int BasePropCommented { get; set; }
            public int BasePropUnassigned { get; set; }
        }

        private class Derived : Base
        {
            public int DerivedPropAssigned { get; set; }
            public int DerivedPropCommented { get; set; }
            public int DerivedPropUnassigned { get; set; }
        }
    }
}
";

            var expected = VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Derived", "BasePropUnassigned, DerivedPropUnassigned" );
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }

        [Fact]
        public async Task TargetTypedNew_UnassignedMembers_AddsDiagnostic()
        {
            var test = @"
namespace SampleConsoleApp
{
    internal static class Program
    {
        private static void Main(string[] args)
        {
            // AssignAll enable
            Foo foo = {|#0:new()
            {
                // PropString not assigned, diagnostic error
                PropInt = 1,
            }|#0};
        }

        private class Foo
        {
            public int PropInt { get; set; }
            public string PropString { get; set; }
        }
    }
}
";

            var expected = VerifyCS.Diagnostic("AssignAll").WithLocation(0).WithArguments("Foo", "PropString");
            await VerifyCS.VerifyAnalyzerAsync(test, expected);
        }
    }
}