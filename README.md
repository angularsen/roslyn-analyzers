# roslyn-analyzers
Collection of useful Roslyn analyzers and code fixes.

## AssignAll
Diagnostic error if there are unassigned members in an object initializer and a code fix to add the missing ones.

This is particularly useful when mapping between types, such as DTO types and database entity types. It ensures you don't forget to update the mapping when a new property is added or refactored.

I previously used [AutoMapper](http://automapper.org/) to help with that, but now I can finally go back to good old object initializers that in my experience are easier to set up and as performant as can be.

* [Installation](#installation)
* [Sample](#sample)
* [Enable/disable by comments](#enabledisable-by-comments)
* [Ignore properties and fields](#ignore-properties-and-fields)
* [Code fix: Assign all members](#code-fix-assign-all-members)
* [Future improvements](#future-improvements)


### Installation
1. Install nuget [AssignAll](https://www.nuget.org/packages/AssignAll/)
2. Add comment `// AssignAll enable` somewhere above the object initializers you want to analyze

### Sample
```csharp
private static void UnassignedMembersGiveBuildError()
{
    // AssignAll enable
    var foo = new Foo
    {
        // UnassignedProp and UnassignedField not assigned, diagnostic error lists both
        AssignedProp = true
    };
}

private class Foo
{
    public bool AssignedProp { get; set; }
    public int UnassignedProp { get; set; }
    public int UnassignedField;
}
```

![Red squigglies on unassigned members in object initializer](Docs/Images/AssignAll_RedSquigglies.png?raw=true "Red squigglies on unassigned members in object initializer")

![Error list describes what properties or fields are not assigned.](Docs/Images/AssignAll_ErrorList.png?raw=true "Error list describes what properties or fields are not assigned.")

### Enable/disable by comments
Analysis must be explicitly enabled by a special comment, and can be disabled and re-enabled to only apply analysis to certain blocks of code. These comments can occur anywhere in the file and affects all the code below it, or until the next special comment.
```csharp
// AssignAll enable
Foo foo = new Foo
{
    // PropInt not assigned, diagnostic error

    // AssignAll disable
    Bar = new Bar
    {
        // PropInt not assigned, but analyzer is disabled, no diagnostic error

        // AssignAll enable
        Baz = new Baz
        {
            // PropInt not assigned, analysis re-enabled, diagnostic error
        }
    }
};
```

### Ignore properties and fields
Simply comment out the member assignments you want to ignore. This is particularly convenient when using the [codefix](#code-fix-assign-all-members) to first generate all member assignments, then commenting out the ones you want to skip.
```csharp
// AssignAll enable
var foo = new Foo
{
    // Ignore these assignments by commenting them out, it is whitespace tolerant
    // PropIgnored1 = ,
    //PropIgnored2=2,

    // PropUnassigned is unassigned and not commented out, diagnostic error
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

### Code fix: Assign all members
Quickly populate all missing property assignments with an empty value so it does not compile until you assign all the values.
This saves a lot of typing and intellisense browsing.

![Apply code fix 'Assign all members'](Docs/Images/AssignAll_AssignAllMembers.gif?raw=true "Apply code fix 'Assign all members'")


### Future improvements
* Similar diagnostic for constructors (Constructor_AssignAll)
* Auto-fill mapping members with identical or similar names
* Code fix to ignore remaining missing members
* Attributes to enable analysis for certain types
* Attributes to ignore properties/fields
* Configuration to enable/disable by default
