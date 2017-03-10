# roslyn-analyzers
Collection of useful Roslyn analyzers and code fixes.

## ObjectInitializer_AssignAll
Gives diagnostic errors if not all members are assigned in an object initializer.
This is particularly useful in cases such as mapping between DTO types and database entity types on a web server. One of the major benefits of using libraries like [AutoMapper](http://automapper.org/) is to ensure you don't forget to update the mappings when new properties are added or renamed. This analyzer helps you with exactly that and I can go back to good old object initializers that in my experience is simpler, faster to set up and probably a significant performance increase over AutoMapper.

Nuget: [anjdreas.RoslynAnalyzers.ObjectInitializer_AssignAll](https://www.nuget.org/packages/anjdreas.RoslynAnalyzers.ObjectInitializer_AssignAll/)

Features
* Enable/disable by comments to limit analysis to blocks of code
* Ignore properties by name

Future improvements
* Attributes to enable analysis for certain types
* Attributes to ignore properties/fields

```csharp
private static void UnassignedMembersGiveBuildError()
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
```

![Red squigglies on unassigned members in object initializer](Docs/Images/ObjectInitializer_AssignAll_RedSquigglies.png?raw=true "Red squigglies on unassigned members in object initializer")

![Error list describes what properties or fields are not assigned.](Docs/Images/ObjectInitializer_AssignAll_ErrorList.png?raw=true "Error list describes what properties or fields are not assigned.")

### Enable/disable analysis
Analysis must be explicitly enabled by a special comment, and can be disabled and re-enabled to only apply analysis to certain blocks of code.
```csharp
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
```

### Ignore Properties
```csharp
// ObjectInitializer_AssignAll enable
// ObjectInitializer_AssignAll IgnoreProperties: PropIgnored1, PropIgnored2, NonExistingProp
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

private class Foo
{
    public int PropIgnored1 { get; set; }
    public int PropIgnored2 { get; set; }
    public int PropAssigned { get; set; }
    public int PropUnassigned { get; set; }
}
```
