// AssignAll enable
// EXAMPLE 005 - Target-typed new syntax before object initializer.
namespace Samples.ConsoleNet6;

public static class Example005_TargetTypedNew
{
    public static void Irrelevant()
    {
        // This should give analyzer error:
        // Missing member assignments in object initializer for type 'Foo'. Properties: PropUnassigned
        Foo foo = new()
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
