# Description

Please include a summary of the changes and the related issue. Please also include relevant motivation and context. List any dependencies that are required for this change.

If applicable:

Fixes # (issue)

## Type of change

Please delete options that are not relevant.

- [ ] Bug fix (non-breaking change which fixes an issue)
- [ ] New feature (non-breaking change which adds functionality)
- [ ] This change requires a documentation update
- [ ] Breaking changes (fix or feature that would cause existing functionality not to work as expected):
  - List the breaking changes (APIs, language features, etc.)

# Checklist

- [ ] My code follows the style guidelines of this project
- [ ] I have performed a self-review of my code
- [ ] I have commented my code in hard-to-understand areas
- [ ] I have added XML comments to any internal or public members and classes
- [ ] I have made corresponding changes to the documentation
- [ ] My changes generate no new warnings
- [ ] I have added tests that prove my fix is effective or that my feature works
- [ ] New and existing unit tests pass locally with my changes
- [ ] I updated the error resource docs (e.g. src/Buckle/Buckle/Resources/ErrorDescriptionsBU.txt) if any new errors or warnings were added
- [ ] I have added a test case to the [diagnostics tests](../src/Buckle/Buckle.Tests/Diagnostics/DiagnosticTests.cs) if any new errors or warnings were added

**If no new syntax was added delete this section:**

- [ ] I added my new syntax to [Syntax.xml file](../src/Buckle/Buckle/CodeAnalysis/Syntax/Syntax.xml)
- [ ] I updated the SyntaxFactory class (create a partial inside the new syntax source file)
- [ ] I updated the Lexer class (if applicable)
- [ ] I updated the Parser class
- [ ] I updated the Binder class
- [ ] I updated the Lowerer class
- [ ] I updated the Optimizer class (if applicable)
- [ ] I updated the Expander class
- [ ] I updated the DisplayText class
- [ ] I updated the Evaluator class
- [ ] I updated the CSharpEmitter class
- [ ] I updated the ILEmitter class
