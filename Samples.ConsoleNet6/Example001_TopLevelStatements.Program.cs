// AssignAll enable

// EXAMPLE 001 - Top level statements in the single main file, typically Program.cs.
// https://learn.microsoft.com/en-us/dotnet/csharp/fundamentals/program-structure/top-level-statements
//
// This should give analyzer error:
// Missing member assignments in object initializer for type 'Foo'. Properties: PropUnassigned
var foo = new Foo
{
    // Commented assignments after opening brace. OK by analyzer.
    // PropCommented1 = 1,

    // Assigned property. OK by analyzer
    PropAssigned = 1,

    // Commented assignments just before closing brace. OK by analyzer.
    //PropCommented2 = ,
    // PropCommented3=,
};

Console.WriteLine($"Hello, {foo}!");

// Add methods and nested types available to top level statements via a partial Program class.
// ReSharper disable once UnusedType.Global
public static partial class Program
{
}
