# Code Style

Additional code style preferences not listed in the .editorconfig.

## Naming

- Object names are UpperCamelCase
- Label names are UpperCamelCase
- Namespace names are UpperCamelCase
- Enum members are UpperCamelCase
- Preprocessor constants are UPPER_CASE
- Folder names (apart from abbreviations or root folders e.g. src) are UpperCamelCase
- Source file names are UpperCamelCase
- Root markdown file names are UPPER_CASE

## Other

- All switch statements must implement the default label
- Prefer diagnostics over exceptions
- Gotos only allowed inside switch-case unless necessary (which is never)
- Using statements should always be in order
- Order of members follows [SA1201](https://github.com/DotNetAnalyzers/StyleCopAnalyzers/blob/master/documentation/SA1201.md)
