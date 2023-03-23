# Code Style

This code style is most similar to C++ standards, and is what the Belte Standard Libraries will use.

## Whitespace

- Indent with 4 spaces, for markdown indent with 2 spaces
- Use crlf line endings
- Use utf-8 charset
- Trim all trailing whitespace
- Insert final newline at the end of each file
- Indent case contents
- Indent switch labels
- Outdent labels
- No space after cast
- Single space after keywords in control of flow statements
- No space between method name and parameter list
- No space between method declaration and parameter list
- No space between parenthesis
- Single space between braces
- Single space before colon in inheritance clauses
- Single space after colon in inheritance clauses
- Single space around binary operators unless within loop declaration or primary expression
- Blocks/open braces start on same line
- Single space inside empty braces, no newline
- Decorators precede declarations by a line
- Control of flow must use braces (unless single body statement) and start body statements on new line
- Newline before and after control of flow
- No newline before using statements at the beginning of the file
- If no using statements, first line should be a newline
- Newline before keyword statements (such as return, break, and continue)

## Naming

- Object names are UpperCamelCase
- Public member names are lowerCamelCase
- Non-public member names are lowerCamelCase with a leading underscore (_lowerCamelCase)
- Variable names are lowerCamelCase
- Method names are UpperCamelCase
- Label names are UpperCamelCase
- Namespace names are UpperCamelCase
- Enum names are UpperCamelCase
- Enum members are UpperCamelCase
- Preprocessor constants are UPPER_CASE
- Constants are UpperCamelCase
- Folder names (apart from abbreviations or root folders e.g. src) are UpperCamelCase
- Source file names are UpperCamelCase
- Root markdown file names are UPPER_CASE

## Other

- All switch statements must implement the default label
- Prefer diagnostics over exceptions
- Gotos only allowed inside switch-case unless necessary (which is never)
- Using statements should always be in order
- Order of members follows [SA1201](https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/SA1201.md)
