# roslyn-analyzers
Collection of useful Roslyn analyzers and code fixes.

## ObjectInitializer_AssignAll
Lists all properties or fields not assigned in an object initializer, unless explicitly marked as ignored.
Roslyn code analyzer to give build errors if not all members are assigned in an object initializer.


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

