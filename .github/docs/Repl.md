---
layout: post
title: Using the Repl
---

The Repl (Read-Eval-Print Loop) is a command-line tool that provides the user with a simple method for testing short
code snippets, without having to create any files or set up a project.

The Repl is maintained alongside the compiler, so all language features are supported in the Repl, as well as many
tools for debugging.

## Invoking the Repl

To invoke the Repl, simply pass the `--repl` (or the shorthand `-r`) option to the compiler (`buckle --repl`), and the
Repl will start up in the terminal that it was invoked from.

## Interacting with the Repl

The Repl takes in submissions that are submitted by the enter key if the submission parses correctly, there are two
consecutive empty lines, or Ctrl + Enter is used. If the submission ends with an expression or expression statement, the
result of that expression or statement is displayed. You can exit the Repl at any time by submitting an empty line, or
by invoking the `#exit` command.

The Repl supports standard keyboard input, with some special actions.

| Keystrokes | Description |
|-|-|
| Ctrl + Enter | Forces the evaluation of a submission in it's current state, even if it does not parse |
| Shift + Enter | Enters a new line at the current cursor position, even if the submission would have submitted |
| Alt + Enter | Abandons a submission without evaluating it |
| Ctrl + C | If a submission is evaluating, cancels the evaluation and display how long the evaluation ran before being aborted otherwise exits the Repl |

## Repl Tools and Commands

The Repl provides an immediate evaluation result whenever a submission is completed, and this includes error feedback.
In addition, the Repl provides many commands for peeking inside of the program to possibly help to debug, especially
for Belte developers.

All Repl commands:

| Command Name | Usage | Description |
|-|-|-|
| [Clear](#clear-command) | `#clear`, `#cls` | Clear the screen |
| [Dump](#dump-command) | `#dump <signature>` | Show contents of symbol \<signature> |
| [Exit](#exit-command) | `#exit` | Exit the Repl |
| [Help](#help-command) | `#help` | Show this document |
| [Load](#load-command) | `#load <path>` | Load in text from \<path> |
| [LS](#ls-command) | `#ls` | List all defined symbols |
| [Reset](#reset-command) | `#reset` | Clear previous submissions |
| [Save to File](#save-to-file-command) | `#saveToFile <path> <count=1>` | Save previous \<count> submissions to \<path> |
| [Settings](#settings-command) | `#settings` | Open settings page |
| [Show IL](#show-il-command) | `#showIL` | Toggle display of IL code |
| [Show Program](#show-program-command) | `#showProgram` | Toggle display of the intermediate representation |
| [Show Time](#show-time-command) | `#showTime` | Toggle display of submission execution time |
| [Show Tokens](#show-tokens-command) | `#showTokens` | Toggle display of syntax tokens |
| [Show Tree](#show-tree-command) | `#showTree` | Toggle display of the parse tree |
| [Show Warnings](#show-warnings-command) | `#showWarnings` | Toggle display of warnings |
| [State](#state-command) | `#state` | Dump the current state of the Repl |

### Clear Command

Usage: `#clear` or `#cls`

The clear command will clear the entire terminal of any past submissions, and then you can continue coding snippets.
This command does not affect any of the Repl state like the reset command, it only clears the terminal buffer.

### Dump Command

Usage: `#dump <signature>`

The dump command will display information about any symbol defined in any scope. Currently, this information is only
declaration information and not the current state of any symbol (like a variable's value). It will show member
declarations and bodies.

Examples:

```belte
» int myInt = 3;
» #dump myInt
int myInt
```

```belte
» struct MyStruct {
·     int field1;
·     [NotNull]string field2;
· }
» #dump MyStruct
struct MyStruct {
    int field1
    [NotNull]string field2
}
```

```belte
» [NotNull]int AddAndTruncate([NotNull]decimal a, [NotNull]decimal b) {
·     return ([NotNull]int)(a + b);
· }
» #dump AddAndTruncate
[NotNull]int AddAndTruncate([NotNull]decimal a, [NotNull]decimal b) {
    return ([NotNull]int)([NotNull]decimal a + [NotNull]decimal b)
}
```

If the symbol is a method symbol with overloads you can provide a list of parameter types to specify the overload. If no
parameters are specified, the Repl does not know which overload the user is requesting, so a message will list all of
the overloads:

```belte
» void MyMethod(int a, int b) { }
» void MyMethod(string a, decimal b, int c) { }
» void MyMethod() { }
» #dump MyMethod
repl: error RE0007: 'MyMethod' is ambiguous between 'MyMethod()', 'MyMethod(string,decimal,int)', and 'MyMethod(int,int)'
» #dump MyMethod(string,decimal,int)
void MyMethod(string a, decimal b, int c) {
    return
}
```

It is important to note that the parameter list cannot contain whitespace, as breaking it up would make the Repl think
you are passing multiple arguments into the dump command, similar to the command line.

### Exit Command

Usage: `#exit`

The exit command will terminate the Repl program. The Repl will also be stopped if an empty submission is entered.

### Help Command

Usage: `#help`

Lists a brief message describing all the Repl commands.

### Load Command

Usage: `#load <path>`

The load command will load the entire contents of a Belte source file as a single submission, and evaluate it. This will
also add any declared symbols in that file to the Repl scope, and they can then be accessed, assigned, overloaded, etc.

For example:

Add.blt:

```belte
int Add(int a, int b) {
    return a + b;
}
```

The Repl:

```belte
» #load Add.blt
» Add(3, 6);
9
```

### LS Command

Usage: `#ls`

The LS command lists all currently declared symbols, including built-in ones.

For example:

```belte
» int myInt = 3;
» #ls
int myInt
[NotNull]bool HasValue(any value)
[NotNull]bool HasValue(bool value)
[NotNull]bool HasValue(decimal value)
[NotNull]bool HasValue(int value)
[NotNull]bool HasValue(string value)
[NotNull]string Input()
[NotNull]void Print(any text)
[NotNull]void PrintLine(any text)
[NotNull]void PrintLine()
[NotNull]int RandInt(int max)
[NotNull]any Value(any value)
[NotNull]bool Value(bool value)
[NotNull]decimal Value(decimal value)
[NotNull]int Value(int value)
[NotNull]string Value(string value)
```

### Reset Command

Usage: `#reset`

The reset command does just that, resets the Repl. Because submissions are saved across Repl instances, this is the only
way to truly "reset" the Repl. It will dispose of all submissions (including their symbol declarations), and reset all
Repl state to defaults, like the `#showTree` command.

### Save-to-File Command

Usage: `#saveToFile <path> <count=1>`

The save-to-file command will join together the last `count` submissions, and send them to `path`.

For example:

The Repl:

```belte
» int myInt = 3;
» bool myBool = true;
» string myString = "Test";
» #saveToFile Vars.blt 2
Wrote 2 lines
```

Vars.blt:

```belte
» bool myBool = true;
» string myString = "Test";
```

If the file at the location `path` already exists, it will prompt for confirmation before clearing and writing to the
file.

Note that the submission count includes all successful submissions, apart from the save-to-file command that is
executing the file write. This does include previous Repl command submissions currently, for example:

The Repl:

```belte
» #reset
» #saveToFile MyFile.blt
Wrote 1 line
```

MyFile.blt:

```belte
#reset
```

However, Repl commands are not valid Belte syntax.

### Settings Command

Usage: `#settings`

Opens the Repl settings page.

All Repl settings:

| Setting Name | Options | Default | Description |
|-|-|-|-|
| Theme | Dark, Light, Green | Dark | The color theme of the Repl; each contributor gets their own color theme! |

### Show IL Command

Usage: `#showIL`

The show-il command toggles the display of the IL representation of submissions before they are evaluated.

For example:

```belte
» #showIL
IL visible
» int myInt = 9;
<Program>$ {
    System.Object <Program>$::<Eval>$() {
        IL_0000: ldloca.s V_0
        IL_0002: ldc.i4.s 9
        IL_0004: call System.Void System.Nullable`1<System.Int32>::.ctor(T)
        IL_0009: ret
    }
}
```

### Show-Program Command

Usage: `#showProgram`

The show-program command shows the bound state of the program, after syntax and semantic analysis. The bound program is
also formatted to slightly resemble the user input.

For example:

```belte
» #showProgram
Bound trees visible
» int myInt = 5 + 6;
any <Eval>$() {
    int myInt = 11
    return
}
```

### Show-Time Command

Usage: `#showTime`

The show-time command toggles the display of the run-time of each submission after it executes. The run-time is always
shown if the submission is aborted early (by hitting Ctrl + C).

For example:

```belte
» #showTime
Execution time visible
» 5 + 4 * 8;
37
Finished after 39 milliseconds
```

### Show-Tokens Command

Usage: `#showTokens`

The show-tokens command toggles the display of lexer-produced syntax token breakdown of each submission before it
executes.

For example:

```belte
» #showTokens
Syntax tokens visible
» int myInt = 40;
⟨Identifier, "int"⟩ ⟨Identifier, "myInt"⟩ ⟨Equals, "="⟩ ⟨NumericLiteral, "40"⟩ ⟨Semicolon, ";"⟩ ⟨EndOfFile, ""⟩
```

### Show-Tree Command

Usage: `#showTree`

The show-tree command toggles the display of the parse tree of each submission before it executes.

For example:

```belte
» #showTree
Parse trees visible
» int myInt = 7;
└─CompilationUnit [0..14)
  ├─GlobalStatement [0..14)
  │ └─VariableDeclarationStatement [0..14)
  │   ├─Type [0..3)
  │   │ ├─IdentifierToken int [0..3)
  │   │ └─Trail: WhitespaceTrivia [3..4)
  │   ├─IdentifierToken myInt [4..9)
  │   ├─Trail: WhitespaceTrivia [9..10)
  │   ├─EqualsToken = [10..11)
  │   ├─Trail: WhitespaceTrivia [11..12)
  │   ├─LiteralExpression [12..13)
  │   │ └─NumericLiteralToken 7 [12..13)
  │   └─SemicolonToken ; [13..14)
  └─EndOfFileToken  [14..14)
```

### Show-Warnings Command

Usage: `#showWarnings`

The show-warnings command toggles the display of warnings. By default, all warnings are hidden (errors are not).

For example:

```belte
» if (false)
·     PrintLine("Hello, world!");
» #showWarnings
Warnings shown
» if (false)
·     PrintLine("Hello, world!");
2:5: warning BU0026: unreachable code
     PrintLine("Hello, world!");
     ^~~~~~~~~~~~~~~~~~~~~~~~~~~
```

### State Command

Usage: `#state`

The state command displays the current Repl state. This command is made purely for debugging purposes and serves little
use outside of development.

```belte
» #state
showTree            False
showProgram         False
showWarnings        True
showIL              False
loadingSubmissions  False
colorTheme          Repl.Themes.DarkTheme
currentPage         Repl
previous            Buckle.CodeAnalysis.Compilation
tree                #state
variables           System.Collections.Generic.Dictionary`2[Buckle.CodeAnalysis.Symbols.IVariableSymbol,Buckle.CodeAnalysis.Evaluating.IEvaluatorObject]
```
