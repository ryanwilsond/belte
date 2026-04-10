<p align="center">
<img width="300" src="images/BelteCapital.png" alt="Belte Logo">
</p>

<h1 align="center">Belte Documentation</h1>

- #### [The Belte Language & Specification](Belte.md)

- #### Buckle Docs

  - [Using Buckle](Buckle.md)
  - [Using the Repl](Repl.md)
  - [Building Buckle](Building.md)

- #### Code Samples

  - [github.com/ryanwilsond/belte/main/samples](https://github.com/ryanwilsond/belte/blob/main/samples).

- #### Belte Language Docs in Its Current State

  - [1](Current/Overview.md) Overview
    - [1.1](Current/Overview.md#11-endpoint-specific-features) Endpoint Specific Features
    - [1.2](Current/Overview.md#12-keywords) Keywords
  - [2](Current/ControlFlow.md) Control Flow
    - [2.1](Current/ControlFlow.md#21-functions) Functions
      - [2.1.1](Current/ControlFlow.md#211-nested-functions) Nested Functions
      - [2.1.2](Current/ControlFlow.md#212-overloads) Overloads
      - [2.1.3](Current/ControlFlow.md#213-default-arguments) Default Arguments
      - [2.1.4](Current/ControlFlow.md#214-named-arguments) Named Arguments
      - [2.1.5](Current/ControlFlow.md#215-template-arguments) Template Arguments
    - [2.2](Current/ControlFlow.md#22-entry-point) Entry Point
      - [2.2.1](Current/ControlFlow.md#221-main) Main
      - [2.2.2](Current/ControlFlow.md#222-program-and-update) Program And Update
    - [2.3](Current/ControlFlow.md#23-conditionals) Conditionals
    - [2.4](Current/ControlFlow.md#24-loops) Loops
      - [2.4.1](Current/ControlFlow.md#241-while-loops) While Loops
      - [2.4.2](Current/ControlFlow.md#242-do-while-loops) Do-While Loops
      - [2.4.3](Current/ControlFlow.md#243-for-loops) For Loops
      - [2.4.4](Current/ControlFlow.md#244-for-each-loops) For Each Loops
        - [2.4.4.1](Current/ControlFlow.md#2441-string-collections) String Collections
        - [2.4.4.2](Current/ControlFlow.md#2442-array-collections) Array Collections
        - [2.4.4.3](Current/ControlFlow.md#2443-indexed-collections) Indexed Collections
        - [2.4.4.4](Current/ControlFlow.md#2444-enumerated-collections) Enumerated Collections
    - [2.5](Current/ControlFlow.md#25-switch) Switch
    - [2.6](Current/ControlFlow.md#26-exceptions) Exceptions
  - [3](Current/Data.md) Data
    - [3.1](Current/Data.md#31-data-types) Data Types
      - [3.1.1](Current/Data.md#311-casts) Casts
      - [3.1.2](Current/Data.md#312-string-interpolation) String Interpolation
    - [3.2](Current/Data.md#32-operators) Operators
      - [3.2.1](Current/Data.md#321-operator-precedence) Operator Precedence
      - [3.2.2](Current/Data.md#322-uncommon-operators) Uncommon Operators
        - [3.2.2.1](Current/Data.md#3221-x) `x!`
        - [3.2.2.2](Current/Data.md#3222-x) `x?`
        - [3.2.2.3](Current/Data.md#3223-ai) `a?[i]`
        - [3.2.2.4](Current/Data.md#3224-xy) `x?.y`
        - [3.2.2.5](Current/Data.md#3225-x--y) `x ?? y`
        - [3.2.2.6](Current/Data.md#3226-x--y) `x ?! y`
        - [3.2.2.7](Current/Data.md#3227-xy) `x..y`
        - [3.2.2.8](Current/Data.md#3228-xy) `x?..y`
    - [3.3](Current/Data.md#33-variables-and-constants) Variables and Constants
      - [3.3.1](Current/Data.md#331-implicit-typing) Implicit Typing
    - [3.4](Current/Data.md#34-attributes-and-modifiers) Attributes and Modifiers
    - [3.5](Current/Data.md#35-references) References
    - [3.6](Current/Data.md#36-arrays) Arrays
    - [3.7](Current/Data.md#37-compile-time-expressions) Compile-Time Expressions
      - [3.7.1](Current/Data.md#371-examples) Examples
      - [3.7.2](Current/Data.md#372-side-effects) Side Effects
  - [4](Current/ClassesAndObjects.md) Namespaces, Classes, and Objects
    - [4.1](Current/ClassesAndObjects.md#41-classes) Classes
      - [4.1.1](Current/ClassesAndObjects.md#411-declaring-and-using-classes) Declaring And Using Classes
      - [4.1.2](Current/ClassesAndObjects.md#412-inheritance) Inheritance
      - [4.1.3](Current/ClassesAndObjects.md#413-base-access) Base Access
    - [4.2](Current/ClassesAndObjects.md#42-members) Members
      - [4.2.1](Current/ClassesAndObjects.md#421-fields) Fields
      - [4.2.2](Current/ClassesAndObjects.md#422-methods) Methods
      - [4.2.3](Current/ClassesAndObjects.md#423-operators) Operators
        - [4.2.3.1](Current/ClassesAndObjects.md#4231-operator-overloading) Operator Overloading
        - [4.2.3.2](Current/ClassesAndObjects.md#4232-casts) Casts
    - [4.3](Current/ClassesAndObjects.md#43-modifiers) Modifiers
      - [4.3.1](Current/ClassesAndObjects.md#431-accessibility-modifiers) Accessibility Modifiers
      - [4.3.2](Current/ClassesAndObjects.md#432-overriding-modifiers) Overriding Modifiers
      - [4.3.3](Current/ClassesAndObjects.md#433-static--constexpr) Static & ConstExpr
      - [4.3.4](Current/ClassesAndObjects.md#434-const) Const
      - [4.3.5](Current/ClassesAndObjects.md#435-sealed--abstract) Sealed & Abstract
    - [4.4](Current/ClassesAndObjects.md#44-constructors) Constructors
    - [4.5](Current/ClassesAndObjects.md#45-templates) Templates
      - [4.5.1](Current/ClassesAndObjects.md#451-constraint-clauses) Constraint Clauses
        - [4.5.1.1](Current/ClassesAndObjects.md#4511-expression-constraints) Expression Constraints
        - [4.5.1.2](Current/ClassesAndObjects.md#4512-special-constraints) Special Constraints
    - [4.6](Current/ClassesAndObjects.md#46-enums) Enums
      - [4.6.1](Current/ClassesAndObjects.md#461-flags) Flags
      - [4.6.2](Current/ClassesAndObjects.md#462-implicit-enum-fields) Implicit Enum Fields
      - [4.6.3](Current/ClassesAndObjects.md#463-experimental-underlying-types) Experimental Underlying Types
    - [4.7](Current/ClassesAndObjects.md#47-namespaces) Namespaces
    - [4.8](Current/ClassesAndObjects.md#48-using-directives) Using Directives
      - [4.8.1](Current/ClassesAndObjects.md#481-aliasing) Aliasing
      - [4.8.2](Current/ClassesAndObjects.md#482-global-disambiguation) Global Disambiguation
      - [4.8.3](Current/ClassesAndObjects.md#483-global-using-directive) Global Using Directive
  - [5](Current/StandardLibrary.md) The Standard Library
    - [5.1](Current/StandardLibrary/Console.md) Console
    - [5.2](Current/StandardLibrary/Math.md) Math
    - [5.3](Current/StandardLibrary/Random.md) Random
    - [5.4](Current/StandardLibrary/String.md) String
    - [5.5](Current/StandardLibrary/Time.md) Time
    - [5.6](Current/StandardLibrary/IO.md) IO
      - [5.6.1](Current/StandardLibrary/IO.md#561-file-methods) File
      - [5.6.2](Current/StandardLibrary/IO.md#562-directory-methods) Directory
    - [5.7](Current/StandardLibrary/Collections.md) Collections
      - [5.7.1](Current/StandardLibrary/List.md) List
      - [5.7.2](Current/StandardLibrary/Dictionary.md) Dictionary
    - [5.8](Current/StandardLibrary/LowLevel.md) LowLevel
    - [5.9](Current/StandardLibrary/Int.md) Int
  - [6](Current/LowLevelFeatures.md) Low-Level Features
    - [6.1](Current/LowLevelFeatures.md#61-low-level-contexts) Low-Level Contexts
    - [6.2](Current/LowLevelFeatures.md#62-structures) Structures
    - [6.3](Current/LowLevelFeatures.md#63-arrays) Arrays
      - [6.3.1](Current/LowLevelFeatures.md#631-initializer-lists) Initializer Lists
    - [6.4](Current/LowLevelFeatures.md#64-numerics) Numerics
    - [6.5](Current/LowLevelFeatures.md#65-pointers) Pointers
      - [6.5.1](Current/LowLevelFeatures.md#651-creating-and-dereferencing-pointers) Creating and Dereferencing Pointers
      - [6.5.2](Current/LowLevelFeatures.md#652-pointer-arithmetic) Pointer Arithmetic
    - [6.6](Current/LowLevelFeatures.md#66-function-pointers) Function Pointers
    - [6.7](Current/LowLevelFeatures.md#67-extern-methods) Extern Methods
    - [6.8](Current/LowLevelFeatures.md#68-fixed-size-buffers) Fixed Size Buffers
    - [6.9](Current/LowLevelFeatures.md#69-sizeof-operator) Sizeof Operator
    - [6.10](Current/LowLevelFeatures.md#610-stackalloc-operator) Stackalloc Operator
      - [6.10.1](Current/LowLevelFeatures.md#6101-stackalloc-locals) Stackalloc Locals
    - [6.11](Current/LowLevelFeatures.md#611-inline-il) Inline IL
      - [6.11.1](Current/LowLevelFeatures.md#6111-verification) Verification
      - [6.11.2](Current/LowLevelFeatures.md#6112-unsupported-instructions) Unsupported Instructions
    - [6.12](Current/LowLevelFeatures.md#612-pinned-locals) Pinned Locals
  - [7](Current/Preprocessor.md) Preprocessor Directives
    - [7.1](Current/Preprocessor.md#71-defineundef) Define/Undef
    - [7.2](Current/Preprocessor.md#72-control) Control
  - [8](Current/Interop.md) .NET DLL References
    - [8.1](Current/Interop.md#81-referencing-net-dlls) Referencing .NET DLLs
    - [8.2](Current/Interop.md#82-feature-workarounds) Feature Workarounds
  - [9](Current/GraphicsLibrary.md) Graphics Library

___

[Belte GitHub Repository](https://github.com/ryanwilsond/belte/)
