// AssignAll enable
// EXAMPLE 002 - File-scoped namespaces.
// https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/proposals/csharp-10.0/file-scoped-namespaces
namespace Samples.ConsoleNet6;

public static class Example002_FileScopedNamespace
{
    public static void Irrelevant()
    {
        // This should give analyzer error:
        // Missing member assignments in object initializer for type 'Foo'. Properties: PropUnassigned
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
}
