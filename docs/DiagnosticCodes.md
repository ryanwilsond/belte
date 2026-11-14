# Diagnostic Codes

- [Compiler Diagnostics](#compiler-diagnostics)
- [Command Line Diagnostics](#command-line-diagnostics)
- [Repl Diagnostics](#repl-diagnostics)

## Compiler Diagnostics

| Code | Severity | Message |
| - | - | - |
| BU0001 | Warning | expression will always result to '{0}' |
| BU0002 | Error | cannot access fields through a nullable receiver; consider using a null assert or conditional access |
| BU0003 | Error | "{0}": file is not a valid assembly |
| BU0004 | Error | integral constant '{0}' is too large |
| BU0005 | Error | unexpected character '{0}' |
| BU0006 | Error | unexpected {0} |
| BU0007 | Error | cannot convert from type '{0}' to '{1}' implicitly; an explicit conversion exists (are you missing a cast?) |
| BU0008 | Error | unary operator '{0}' is not defined for type '{1}' |
| BU0009 | Error | cannot infer array type from initializer list; try using a new expression instead |
| BU0010 | Error | named argument '{0}' cannot be specified multiple times |
| BU0011 | Error | binary operator '{0}' is not defined for operands of types '{1}' and '{2}' |
| BU0012 | Error | multiple files with global statements creates ambiguous entry point |
| BU0013 | Error | members defined in a namespace cannot be explicitly declared as private or protected |
| BU0014 | Error | unexpected use of an aliased name |
| BU0015 | Error | the best overload for '{0}' does not have a parameter named '{1}' |
| BU0016 | Error | declaring a main method and using global statements creates ambiguous entry point |
| BU0017 | Error | undefined symbol '{0}' |
| BU0018 | Error | cannot use alias '{0}' with '::' since the alias references a type; use '.' instead |
| BU0019 | Error | not all code paths return a value |
| BU0020 | Error | cannot convert from type '{0}' to '{1}' |
| BU0021 | Error | the namespace '{0}' already contains a definition for '{1}' |
| BU0022 | Error | the using alias '{0}' appeared previously in this namespace |
| BU0023 | Error | ambiguous which statement this else-clause belongs to; use curly braces |
| BU0024 | Error | the using directive for '{0}' appeared previously as global using |
| BU0025 | Error | cannot apply indexing with [] to an expression of type '{0}' |
| BU0026 | Warning | unreachable code |
| BU0027 | Error | unterminated string literal |
| BU0028 | Error | a 'using static' directive cannot be used to declare an alias |
| BU0029 | Error | a 'using static' directive can only be applied to types; '{0}' is a namespace not a type |
| BU0030 | Error | duplicate user-defined conversion in type '{0}' |
| BU0031 | Error | the using directive for '{0}' appeared previously in this namespace |
| BU0032 | Error | called object is not a method |
| BU0033 | Error | only assignment, call, throw, and increment expressions can be used as a statement |
| BU0034 | Error | a 'using' directive can only be applied to namespaces; '{0}' is a type not a namespace; consider a 'using static' directive instead |
| BU0035 | Error | break and continue statements can only be used within a loop |
| BU0036 | Error | '{0}' type is not valid for 'using static'; only a class can be used |
| BU0037 | Error | cannot return a value in a method returning void |
| BU0038 | Error | cannot return without a value in a method returning non-void |
| BU0039 | Error | failed to open file '{0}'; most likely due to the file being used by another process |
| BU0040 | Error | implicitly-typed locals must have initializer |
| BU0041 | Error | unterminated multi-line comment |
| BU0042 | Error | cannot assign \<null> to an implicitly-typed data container |
| BU0043 | Error | a nested initializer list is expected |
| BU0044 | Error | an array initializer of length '{0}' is expected |
| BU0045 | Error | entry point '{0}' must return 'void' or 'int32!' |
| BU0046 | Error | try statement must have a catch or finally |
| BU0047 | Error | cannot assign {0} to an implicitly-typed local |
| BU0048 | Error | expected overloadable unary, arithmetic, equality, or comparison operator |
| BU0049 | Error | a by-reference data container must be initialized with a reference |
| BU0050 | Error | cannot initialize a by-value data container with a reference |
| BU0051 | Error | unexpected {0}, expected {1} |
| BU0052 | Error | expected {0} at end of input |
| BU0053 | Warning | implicit types infer reference types making the 'ref' keyword not necessary in this context |
| BU0054 | Error | cannot assign a reference to a constant to a by-reference data container expecting a reference to a data container |
| BU0055 | Error | cannot use void as a type |
| BU0056 | Error | expected {0} |
| BU0057 | Error | no overload for method '{0}' takes {1} arguments |
| BU0059 | Error | unexpected {0}, expected {1} or {2} |
| BU0060 | Error | expected {0} or {1} at end of input |
| BU0061 | Error | '{0}' contains no such member '{1}' |
| BU0062 | Error | left side of assignment operation must be a variable, parameter, field, or indexer |
| BU0063 | Error | cannot use a nullability annotation in object or array creation |
| BU0064 | Error | cannot assign a reference to a data container to a by-reference data container expecting a reference to a constant |
| BU0065 | Error | '{0}' contains no such field '{1}' |
| BU0066 | Error | overloaded unary operator '{0}' takes 1 parameter |
| BU0067 | Error | named argument '{0}' specifies a parameter for which a positional argument has already been given |
| BU0068 | Error | default parameter value for '{0}' must be a compile-time constant |
| BU0069 | Error | all optional parameters must be specified after any required parameters |
| BU0070 | Error | cannot mark a data container as both constant and variable |
| BU0071 | Warning | local '{0}' shares a name with a type in this namespace |
| BU0072 | Error | argument {0}: cannot implicitly pass 'null' to a parameter of non-nullable type '{1}' |
| BU0073 | Error | expected overloadable unary operator |
| BU0074 | Error | modifier '{0}' has already been applied to this item |
| BU0075 | Error | cannot use a reference type in this context |
| BU0076 | Error | cannot divide by zero |
| BU0077 | Error | expected overloadable arithmetic, equality, or comparison operator |
| BU0078 | Error | cannot return by reference a member of parameter '{0}' because it is scoped to the current method |
| BU0079 | Error | unrecognized escape sequence '\\{0}' |
| BU0080 | Error | primitive types do not contain any members |
| BU0081 | Error | invalid object creation; cannot construct primitive |
| BU0082 | Error | not all code paths initialize field '{0}' |
| BU0083 | Error | operators cannot have ref parameters |
| BU0084 | Error | cannot use structs outside of low-level contexts |
| BU0085 | Error | cannot use 'this' in the current context |
| BU0086 | Error | '{0}' is inaccessible due to its protection level |
| BU0087 | Error | type '{0}' does not contain a constructor that takes {1} arguments |
| BU0088 | Error | modifier '{0}' is not valid for this item |
| BU0089 | Error | member '{0}' cannot be accessed with an instance reference; qualify it with the type name instead |
| BU0090 | Error | an object reference is required for non-static member '{0}' |
| BU0091 | Error | cannot initialize fields in structure definitions |
| BU0092 | Error | cannot have multiple 'Main' entry points |
| BU0093 | Error | attributes are not valid in this context |
| BU0094 | Error | non-indexing operators cannot return by reference |
| BU0095 | Error | cannot return a global by reference |
| BU0096 | Error | cannot return by reference a member of parameter '{0}' through a ref parameter; it can only be returned in a return statement |
| BU0097 | Error | the type name '{0}' does not exist in the type '{1}' |
| BU0098 | Error | static constructors must be parameterless |
| BU0099 | Error | cannot declare a field or data container with a static type |
| BU0100 | Error | cannot create an instance of the static class '{0}' |
| BU0101 | Error | cannot mark symbol as both {0} and {1} |
| BU0102 | Error | cannot assign to an instance member in a method marked as constant |
| BU0103 | Error | cannot call non-constant method '{0}' in a method marked as constant |
| BU0104 | Error | cannot call non-constant method '{0}' on constant |
| BU0105 | Error | reference type cannot be marked as a constant expression because references are not compile-time constants |
| BU0106 | Error | expected a compile-time constant value |
| BU0107 | Error | '{0}': static types cannot be used as return types |
| BU0108 | Error | overloaded binary operator '{0}' takes 2 parameters |
| BU0109 | Error | user-defined operator '{0}' must be declared public and static |
| BU0110 | Error | static classes cannot contain operators |
| BU0111 | Error | cannot return by reference a member of parameter '{0}' because is not a ref parameter |
| BU0112 | Error | cannot return a parameter by reference '{0}' because it is scoped to the current method |
| BU0113 | Error | cannot return a parameter by reference '{0}' through a ref parameter; it can only be returned in a return statement |
| BU0114 | Error | cannot initialize type '{0}' with an initializer list because the type does not define a conversion from 'T[]' or 'Buffer\<T>' |
| BU0115 | Error | character literal cannot be empty |
| BU0116 | Error | character literal cannot be more than one character |
| BU0117 | Error | use of unassigned local '{0}' |
| BU0118 | Error | '{0}': virtual or abstract methods cannot be private |
| BU0119 | Error | cannot return a parameter by reference '{0}' because it is not a ref parameter |
| BU0120 | Error | expression cannot be used in this context because it may indirectly expose variables outside of their declaration scope |
| BU0121 | Error | cannot derive from primitive type '{0}' |
| BU0122 | Error | symbol '{0}' has no such template parameter '{1}' |
| BU0123 | Error | template '{0}' is not a type; cannot extension check a non-type |
| BU0124 | Error | template constraint is not a compile-time constant |
| BU0125 | Error | a member of '{0}' is returned by reference but was initialized to a value that cannot be returned by reference |
| BU0126 | Error | the type '{0}' must be or derive from '{1}' in order to use it as parameter '{2}' in the template type or method '{3}' |
| BU0127 | Error | template constraint on '{0}' fails: constraint results in null ('{1}') |
| BU0128 | Error | template constraint on '{0}' fails ('{1}') |
| BU0129 | Error | cannot return local '{0}' by reference because it was initialized to a value that cannot be returned by reference |
| BU0130 | Error | cannot return a member of local '{0}' by reference because it is not a ref local |
| BU0131 | Error | cannot return local '{0}' by reference because it is not a ref local |
| BU0132 | Error | '{0}': a member marked as override cannot be marked as new or virtual |
| BU0133 | Error | branches of a ref conditional operator cannot refer to variables with incompatible declaration scopes |
| BU0134 | Error | cannot derive from sealed type '{0}' |
| BU0135 | Error | cannot derive from static type '{0}' |
| BU0136 | Error | this combination of arguments to '{0}' is disallowed because it may expose variables referenced by parameter '{1}' outside of their declaration scope |
| BU0137 | Error | cannot use 'base' in the current context |
| BU0138 | Error | cannot create an instance of the abstract class '{0}' |
| BU0139 | Error | '{0}' must declare a body because it is not marked abstract or extern |
| BU0140 | Error | '{0}' cannot declare a body because it is marked abstract |
| BU0141 | Error | '{0}' is abstract but it is contained in non-abstract type '{1}' |
| BU0142 | Error | '{0}' does not implement inherited abstract member '{1}' |
| BU0143 | Error | the operator {0} requires a matching operator '{1}' to also be defined |
| BU0144 | Error | invalid expression term {0} |
| BU0145 | Error | unexpected use of a templated name |
| BU0146 | Error | cannot apply multiple accessibility modifiers |
| BU0147 | Error | template parameters '{0}' and '{1}' form a circular constraint |
| BU0148 | Error | template parameter '{0}' cannot be used as a constraint for template parameter '{1}' |
| BU0149 | Error | template parameter '{0}' cannot be constrained to both types '{1}' and '{2}' |
| BU0150 | Error | template parameter '{0}' cannot be constrained as both a reference type and a value type |
| BU0151 | Error | cannot declare a member with the same name as the enclosing type '{0}' |
| BU0152 | Error | circular base dependency involving '{0}' and '{1}' |
| BU0153 | Error | inconsistent accessibility: class '{0}' is less accessible than class '{1}' |
| BU0154 | Error | static class '{0}' cannot derive from type '{1}'; static classes must derive from Object |
| BU0155 | Error | cannot derive from template parameter '{0}' |
| BU0156 | Error | inconsistent accessibility: type '{0}' is less accessible than field '{1}' |
| BU0157 | Error | inconsistent accessibility: return type '{0}' is less accessible than operator '{1}' |
| BU0158 | Error | inconsistent accessibility: return type '{0}' is less accessible than method '{1}' |
| BU0159 | Error | inconsistent accessibility: parameter type '{0}' is less accessible than operator '{1}' |
| BU0160 | Error | inconsistent accessibility: parameter type '{0}' is less accessible than method '{1}' |
| BU0161 | Error | no suitable entry point found |
| BU0162 | Error | array elements cannot be of static type '{0}' |
| BU0163 | Error | cannot use local '{0}' before it is declared; '{1}' hides the field '{2}' |
| BU0164 | Error | cannot use local '{0}' before it is declared |
| BU0165 | Error | cannot use 'this' in a static method |
| BU0166 | Error | cannot use 'base' in a static method |
| BU0167 | Error | '{0}' is an ambiguous reference between '{1}' and '{2}' |
| BU0168 | Error | Ambiguity between '{0}' and '{1}' |
| BU0169 | Error | cannot access protected member '{0}' via a qualifier of type '{1}'; the qualifier must be of type '{2}' (or derive from it) |
| BU0170 | Error | cannot initialize an implicitly-typed data container with the static type '{0}' |
| BU0171 | Error | cannot return by-reference in a method without a reference return type |
| BU0172 | Error | must return by-reference in a method with a reference return type |
| BU0173 | Error | cannot convert from type '{0}' to '{1}' implicitly |
| BU0174 | Error | method group '{0}' cannot be used as a value |
| BU0175 | Error | cannot declare a local with the name '{0}' because that name is already used by a parameter in an enclosing scope |
| BU0176 | Error | cannot declare a parameter, local, or local function with the name '{0}' because that name is already used by a template parameter in an enclosing scope |
| BU0177 | Error | a local or local function with the name '{0}' has already been declared in this scope |
| BU0178 | Error | argument {0}: cannot convert from type '{1}' to '{2}' |
| BU0179 | Error | cannot convert from type '{0}' to '{1}' implicitly; an explicit conversion exists (are you missing a cast?) |
| BU0180 | Warning | the given expression is never of the provided type ('{0}') |
| BU0181 | Error | binary operator '{0}' is ambiguous for operands with types '{1}' and '{2}' |
| BU0182 | Error | cannot reference synthesized program local '{0}' outside of top level statements |
| BU0183 | Error | cannot convert null to '{0}' because it is a non-nullable type |
| BU0184 | Error | invalid object creation |
| BU0185 | Error | unary operator '{0}' is ambiguous for operands with type '{1}' |
| BU0186 | Error | both conditional operator operands must be ref expressions or neither may be a ref expressions |
| BU0187 | Error | cannot perform a 'not null' assertion on an expression with constant value 'null' |
| BU0188 | Error | cannot perform a 'not null' assertion on an expression with type '{0}' as it is a non-nullable type |
| BU0189 | Error | cannot cast to static type '{0}' |
| BU0190 | Error | cannot specify array size in a declaration |
| BU0191 | Error | there is no target type for the initializer list |
| BU0192 | Error | a field initializer cannot reference non-static member '{0}' |
| BU0193 | Error | argument {0} may not be passed with the '{1}' keyword |
| BU0194 | Error | argument {0} must be passed with the '{1}' keyword |
| BU0195 | Error | there is no argument given that corresponds to the required parameter '{0}' of '{1}' |
| BU0196 | Error | named argument '{0}' is used out-of-position but is followed by an unnamed argument |
| BU0197 | Error | named argument '{0}' specifies a parameter for which a positional argument has already been given |
| BU0198 | Warning | possible mistaken empty statement |
| BU0199 | Error | embedded statement cannot be a declaration |
| BU0200 | Error | left side of increment or decrement operation must be a variable, parameter, field, or indexer |
| BU0201 | Error | left side of ref assignment must be a ref variable, ref field, ref parameter, or ref indexer |
| BU0202 | Error | ref value must be an assignable variable, field, parameter, or indexer |
| BU0203 | Error | an expression cannot be used in this context because it may not be passed or returned by reference |
| BU0204 | Error | internal non-fatal compiler error |
| BU0205 | Error | '{0}' is a {1} but is used like a {2} |
| BU0206 | Error | non-invocable member '{0}' cannot be used like a method |
| BU0207 | Error | '{0}' is a {1}, which is not valid in the given context |
| BU0208 | Error | cannot use 'this' as a ref value because it is constant |
| BU0209 | Error | cannot return 'this' by reference |
| BU0210 | Error | cannot assign to 'this' because it is constant |
| BU0211 | Error | cannot modify the return value of '{0}' because it is not variable |
| BU0212 | Error | cannot return {0} '{1}' by writable reference because it is constant |
| BU0213 | Error | cannot use {0} '{1}' as a ref value because it is constant |
| BU0214 | Error | cannot assign to {0} '{1}' or use it as the right hand side of a ref assignment because it is constant |
| BU0215 | Error | members of {0} '{1}' cannot be returned by writable reference because it is constant |
| BU0216 | Error | members of {0} '{1}' cannot be used as a ref value because it is constant |
| BU0217 | Error | cannot assign to a member of {0} '{1}' or use it as the right hand side of a ref assignment because it is constant |
| BU0218 | Error | a constant field cannot be returned by writable reference |
| BU0219 | Error | a constant field cannot be used as a ref value (except in a constructor); consider using 'ref const' |
| BU0220 | Error | a constant field cannot be assigned to (except in a constructor) |
| BU0221 | Error | a static constant field cannot be returned by writable reference |
| BU0222 | Error | a static constant field cannot be used as a ref value |
| BU0223 | Error | a static constant field cannot be assigned to |
| BU0224 | Error | members of constant field '{0}' cannot be returned by writable reference |
| BU0225 | Error | members of constant field '{0}' cannot be used as a ref value (except in a constructor); consider using 'ref const' |
| BU0226 | Error | members of constant field '{0}' cannot be modified (except in a constructor) |
| BU0227 | Error | fields of static constant field '{0}' cannot be returned by writable reference |
| BU0228 | Error | fields of static constant field '{0}' cannot be used as a ref value |
| BU0229 | Error | fields of static constant field '{0}' cannot be assigned to |
| BU0230 | Error | cannot use '{0}' as a ref value because it is a {1} |
| BU0231 | Error | cannot assign to '{0}' because it is a {1} |
| BU0232 | Error | to cast a negative value it must be enclosed in parentheses |
| BU0233 | Error | the return expression must be of type '{0}' because this method returns by reference |
| BU0234 | Error | the expression must be of type '{0}' because it is being assigned by reference |
| BU0235 | Error | '{0}': a parameter, local, or local function cannot have the same name as a method template parameter |
| BU0236 | Error | the parameter name '{0}' is a duplicate |
| BU0237 | Error | constructor '{0}' cannot call itself |
| BU0238 | Error | '{0}': cannot provide arguments when creating an instance of a variable type |
| BU0239 | Warning | assignment in conditional expression is always constant; did you mean to use '==' instead of '=' ? |
| BU0240 | Error | cannot do non-virtual member lookup in '{0}' because it is a template parameter |
| BU0241 | Error | cannot call abstract base member '{0}' |
| BU0242 | Error | static field '{0}' cannot be assigned in an object initializer |
| BU0243 | Warning | argument {0} should be a variable because it is passed to a 'ref const' parameter |
| BU0244 | Warning | argument {0} should be passed with the 'ref' keyword |
| BU0245 | Error | the expression must be of type '{0}' to match the alternative ref value |
| BU0246 | Error | duplicate template parameter '{0}' |
| BU0247 | Warning | template parameter '{0}' has the same name as the template parameter from outer method '{1}' |
| BU0248 | Warning | template parameter '{0}' has the same name as the template parameter from outer type '{1}' |
| BU0249 | Error | a ref parameter cannot have a default value |
| BU0250 | Error | a value of type '{0}' cannot be used as a default parameter because there are no casts to type '{1}' |
| BU0251 | Error | parameter '{0}' is of type '{1}'; a default parameter value of a reference type can only be initialized with 'null' |
| BU0252 | Warning | the default value specified for parameter '{0}' will have no effect because it applies to a member that is used in contexts that do not allow optional arguments |
| BU0253 | Error | 'final' modifier must be specified after 'ref' |
| BU0254 | Error | 'ref' and 'out' are not valid in this context |
| BU0255 | Error | 'const' modifier must be specified after 'ref' |
| BU0256 | Error | '{0}': static types cannot be used as parameters |
| BU0257 | Error | the evaluation of the constant value for '{0}' involves a circular definition |
| BU0258 | Error | the type '{0}' already contains a definition for '{1}' |
| BU0259 | Error | '{0}' cannot define an overloaded {1} that differs only on parameter modifiers '{2}' and '{3}' |
| BU0260 | Error | type '{0}' already defines a constructor with the same parameter types |
| BU0261 | Error | type '{0}' already defines a member called '{1}' with the same parameter types |
| BU0262 | Error | '{0}': static classes cannot contain protected members |
| BU0263 | Warning | '{0}' overrides 'Object.Equals(Object)' but does not override 'Object.GetHashCode()' |
| BU0264 | Warning | '{0}' defines operator == or operator != but does not override 'Object.Equals(Object)' |
| BU0265 | Warning | '{0}' defines operator == or operator != but does not override 'Object.GetHashCode()' |
| BU0266 | Error | '{0}' cannot be sealed because it is not an override |
| BU0267 | Error | '{0}' cannot be both abstract and sealed |
| BU0268 | Error | the abstract {0} '{1}' cannot be marked virtual |
| BU0269 | Error | the static member '{0}' cannot be marked 'const' |
| BU0270 | Error | '{0}' is a new virtual member in sealed type '{1}' |
| BU0271 | Error | '{0}': cannot declare instance members in a static class |
| BU0272 | Warning | '{0}': new protected member declared in sealed type; no different than private |
| BU0273 | Warning | '{0}' hides inherited member '{1}'; use the new keyword if hiding was intended |
| BU0274 | Warning | the member '{0}' does not hide an accessible member; the new keyword is not required |
| BU0275 | Error | '{0}' hides inherited abstract member '{1}' |
| BU0276 | Warning | '{0}' hides inherited member '{1}'; to make the current member override that implementation, add the override keyword; otherwise add the new keyword |
| BU0277 | Warning | reference kind modifier of parameter '{0}' doesn't match the corresponding parameter '{1}' in hidden member |
| BU0278 | Error | '{0}': cannot override because '{1}' is not a method |
| BU0279 | Error | '{0}': no suitable method found to override |
| BU0280 | Error | the inherited members '{0}' and '{1}' have the same signature in type '{2}', so they cannot be overridden |
| BU0281 | Error | '{0}': cannot override inherited member '{1}' because it is not marked virtual, abstract, or override |
| BU0282 | Error | '{0}': cannot override inherited member '{1}' because it is sealed |
| BU0283 | Error | '{0}': cannot change access modifiers when overriding '{1}' inherited member '{2}' |
| BU0284 | Error | '{0}' must match by reference return of overridden member '{1}' |
| BU0285 | Error | '{0}': return type must be '{1}' to match overridden member '{2}' |
| BU0286 | Warning | reference kind modifier of parameter '{0}' doesn't match the corresponding parameter '{1}' in overridden or implemented member |
| BU0287 | Warning | nullability of type of parameter '{0}' doesn't match overridden member |
| BU0288 | Warning | nullability of reference types in type of parameter '{0}' doesn't match overridden member |
| BU0289 | Warning | nullability of return type doesn't match overridden member |
| BU0290 | Warning | nullability of reference types in return type doesn't match overridden member |
| BU0291 | Fatal | failed to load required libraries; libraries contained errors |
| BU0292 | Error | user-defined operators cannot return void |
| BU0293 | Error | the parameter of a unary operator must be the containing type |
| BU0294 | Error | the parameter of a unary operator must be the containing type, or its type template parameter constrained to it |
| BU0295 | Error | the first operand of an overloaded shift operator must have the same type as the containing type |
| BU0296 | Error | the first operand of an overloaded shift operator must have the same type as the containing type or its type template parameter constrained to it |
| BU0297 | Error | one of the parameters of a binary operator must be the containing type |
| BU0298 | Error | one of the parameters of a binary operator must be the containing type, or its type template parameter constrained to it |
| BU0299 | Error | one of the parameters of an equality, or inequality operator declared in interface '{0}' must be a type template parameter on '{1}' constrained to '{2}' |
| BU0300 | Error | the parameter type for ++ or -- operator must be the containing type |
| BU0301 | Error | the parameter type for ++ or -- operator must be the containing type, or its type template parameter constrained to it |
| BU0302 | Error | the return type for ++ or -- operator must match the parameter type or be derived from the parameter type |
| BU0303 | Error | the return type for ++ or -- operator must either match the parameter type, or be derived from the parameter type, or be the containing type's type template parameter constrained to it unless the parameter type is a different type template parameter |
| BU0304 | Error | wrong number of indices inside []; expected {0} |
| BU0305 | Error | cannot have multiple 'Update' graphics update points |
| BU0306 | Error | the 'Main' entry point and 'Update' graphics update point must be declared in the same class |
| BU0307 | Error | fields cannot be implicitly typed |
| BU0308 | Error | array sizes must be of type 'int!' |
| BU0309 | Error | the template {0} '{1}' requires {2} template arguments |
| BU0310 | Error | '{0}': protected member declared in struct |
| BU0311 | Error | use of result of '{0}' in this context may expose locals referenced by parameter '{1}' outside of their declaration scope |
| BU0312 | Error | use of member of result of '{0}' in this context may expose locals referenced by parameter '{1}' outside of their declaration scope |
| BU0313 | Error | use of local '{0}' in this context may expose referenced locals outside of their declaration scope |
| BU0314 | Error | cannot ref-assign '{0}' to '{1}' because '{2}' can only escape the current method through a return statement |
| BU0315 | Error | cannot ref-assign '{0}' to '{1}' because '{2}' has a narrower escape scope than '{3}' |
| BU0316 | Error | cannot ref-assign '{0}' to '{1}' because '{2}' has a wider value escape scope than '{3}' allowing assignment through '{4}' of values with narrower escapes scopes than '{5}' |
| BU0317 | Error | array creation must have array size or array initializer |
| BU0318 | Error | cannot use a global using directive in a namespace declaration |
| BU0319 | Error | alias '{0}' not found |
| BU0320 | Error | the type or namespace name '{0}' could not be found |
| BU0321 | Warning | namespace '{0}' potentially shadows parts of the Standard Library |
| BU0322 | Error | the type or namespace name '{0}' could not be found in the global namespace |
| BU0323 | Error | the type or namespace name '{0}' does not exist in the namespace '{1}' |
| BU0324 | Error | namespace '{0}' contains a definition conflicting with alias '{1}' |
| BU0325 | Error | unexpected use of an unbound template name |
| BU0326 | Error | the non-template {0} '{1}' cannot be used with template arguments |
| BU0327 | Error | the {0} '{1}' cannot be used with template arguments |
| BU0328 | Error | the type '{0}' may not be used as a type argument |
| BU0329 | Error | '{0}': static types cannot be used as type arguments |
| BU0330 | Error | the type '{0}' must be a reference type in order to use it as parameter '{1}' in the template type or method '{2}' |
| BU0331 | Error | the type '{0}' must be a value type in order to use it as parameter '{1}' in the template type or method '{2}' |
| BU0332 | Error | the type '{0}' must be a non-nullable type in order to use it as parameter '{1}' in the template type or method '{2}' |
| BU0333 | Error | duplicate constraint on template parameter '{0}' |
| BU0334 | Error | template '{0}' is not a type; cannot use 'is' constraints a non-type template parameter |
| BU0335 | Error | cannot pass a global by reference |
| BU0336 | Error | a throw expression is not valid in this context |
| BU0337 | Error | control cannot leave the body of a finally clause |
| BU0338 | Error | access modifiers are not allowed on static constructors |
| BU0339 | Error | static constructor cannot have an explicit 'this' or 'base' constructor call |
| BU0340 | Error | cannot infer dictionary type from initializer; try using an object creation expression instead |
| BU0341 | Error | cannot take the address of the given expression |
| BU0342 | Error | cannot dereference a non-pointer |
| BU0343 | Error | must cast a void pointer before dereferencing |
| BU0344 | Error | constant value '{0}' cannot be converted to '{1}' |
| BU0345 | Error | '{0}' cannot be both abstract and extern |
| BU0346 | Error | '{0}' cannot declare a body because it is marked extern |
| BU0347 | Error | the 'DllImport' attribute must be specified on a method marked 'static' and 'extern' |
| BU0348 | Error | the 'DllImport' attribute cannot be applied to a method that is template or contained in a template method or type |
| BU0349 | Error | invalid value for argument to '{0}' attribute |
| BU0350 | Error | a fixed buffer can only have one dimension |
| BU0351 | Error | fixed size buffer of length '{0}' and type '{1}' is too big |
| BU0352 | Error | fixed size buffers must have a length greater than zero |
| BU0353 | Error | fixed size buffer fields may only be members of structs |
| BU0354 | Error | fixed fields cannot be ref fields |
| BU0355 | Error | fixed size buffer type must a non-nullable bool or a non-nullable sized numeric primitive |
| BU0356 | Error | there is no target type for the null pointer |
| BU0357 | Error | expression is not computable at compile time |
| BU0358 | Error | fixed size buffers can only be accessed through locals or fields |
| BU0359 | Error | cascade expression must be an assignment or call expression |
| BU0360 | Error | cascade expression must access a direct member of the target receiver |
| BU0361 | Error | compile time expression must result in a primitive or struct type |
| BU0362 | Error | a stackalloc expression or local requires a type with a single array size specifier |
| BU0363 | Error | cannot use a negative size with a stackalloc expression or local |
| BU0364 | Error | stackalloc expression can only be used as a data container initializer |
| BU0365 | Error | a stackalloc expression or local may not be used in a catch or finally block |
| BU0366 | Error | a stackalloc local cannot not have an explicit initializer |
| BU0367 | Error | a stackalloc local cannot be implicitly typed |
| BU0368 | Error | an enum type can only derive from integral primitives or string |
| BU0369 | Error | '{0}': the enum value is too large to fit in its type |
| BU0370 | Error | inline IL block does not have a balanced stack |
| BU0371 | Error | unknown IL instruction '{0}' |
| BU0372 | Error | IL instruction '{0}' expects an operand of type '{1}' |
| BU0373 | Error | IL instruction '{0}' does not expect an operand |
| BU0374 | Error | preprocessor directives must appear as the first non-whitespace character on a line |
| BU0375 | Error | #endif directive expected |
| BU0376 | Error | unexpected preprocessor directive |
| BU0377 | Error | cannot define/undefine preprocessor symbols after first token in file |
| BU0378 | Error | invalid preprocessor expression |
| BU0379 | Error | enum members with a string underlying type must have explicit values |
| BU0380 | Error | there is no target type for the implicit enum field |
| BU0381 | Error | cannot infer enum type from implicit enum field |
| BU0382 | Error | a goto is only valid inside a switch statement |
| BU0383 | Error | a switch expression or case label must be a primitive |
| BU0384 | Error | the switch expression must be a value; found '{0}' |
| BU0385 | Error | no such label '{0}' within the scope of the goto statement |
| BU0386 | Error | the switch statement contains multiple cases with the label value '{0}' |
| BU0387 | Error | the switch case is unreachable |
| BU0388 | Error | file can only contain one file-scoped namespace declaration |
| BU0389 | Error | file can not contain both file-scoped and normal namespace declarations |
| BU0390 | Error | file-scoped namespace must precede all other members in a file |
| BU0391 | Error | entry point type cannot define instance constructors |
| BU0392 | Error | struct member '{0}' of type '{1}' causes a cycle in the struct layout |
| BU0393 | Error | for iterator expression must be an array, string, Enumerator or define the iter operator or define the length and [] operators |
| BU0394 | Error | length operator must return 'int!' |
| BU0395 | Error | iter operator must return 'Enumerator!' |
| BU0396 | Error | unexpected parameter list (operand is not a method) |
| BU0397 | Error | no overload for method '{0}' matches the parameter list |
| BU0398 | Error | namespaces cannot directly contain fields, methods, or statements |
| BU0399 | Error | cannot apply a null erasure operator to an expression with type '{0}' as it is a non-nullable type |
| BU0400 | Error | cannot apply a null erasure operator to an expression with type '{0}' because it has no non-null default value |
| BU0401 | Error | cannot apply a null erasure operator to a null literal |
| BU0402 | Error | the source expression type of a null-binding contract must be nullable |
| BU0403 | Error | cannot create a null-binding contract on a null literal |
| BU0404 | Error | type '{0}' contains no valid handle method |
| BU0405 | Error | type '{0}' contains more than one valid handle method |
| BU0406 | Error | function types cannot contain pointers or function pointers; consider using a function pointer instead |
| BU0407 | Error | no overload for '{0}' matches function '{1}' |
| BU0408 | Error | '{0} {1}' has the wrong return type; expected '{2}' |
| BU0409 | Error | ref mismatch between '{0}' and function '{1}' |
| BU0410 | Error | unrecognized calling convention '{0}'; valid calling conventions are 'stdcall', 'winapi', 'fastcall', 'cdecl', and 'thiscall' |
| BU0411 | Error | cannot use a nullable annotation on a pointer or function pointer type |
| BU0412 | Error | type template parameters cannot be nullable |
| BU0413 | Error | cannot use a non-nullable annotation on a template parameter type |
| BU0414 | Error | there is no target type for the default literal |
| BU0415 | Error | cannot use a default literal for type '{0}' because it has no default value |
| BU0416 | Warning | using directive is unnecessary |
| BU0417 | Error | cannot infer the type of implicitly-typed out data container '{0}' |
| BU0418 | Error | cannot annotate the type of an implicitly typed out data container |
| BU0419 | Error | not all code paths assign out parameter '{0}' |
| BU0420 | Error | invalid operand for pattern match; value required, but found '{0}' |
| BU0421 | Error | cannot use nullable type '{0}' in a pattern; use the underlying type '{1}' or a null-binding contract instead |
| BU0422 | Error | an expression of type '{0}' cannot be handled by a pattern of type '{1}' |
| BU0423 | Error | cannot use a default literal for struct type '{0}' because it has fields with no default value |
| BU0424 | Error | the context expression of a with statement or with expression must be an assignment |
| BU0425 | Warning | exiting the with body early will result in the reversals not taking place; consider using a 'with (...) try' |
| BU0426 | Error | 'Unmanaged' can only be applied to ordinary static non-abstract, non-virtual methods or static local functions |
| BU0427 | Error | methods attributed with 'Unmanaged' cannot have template parameters and cannot be declared in a template type |
| BU0428 | Error | static classes cannot contain destructors |
| BU0429 | Error | control cannot leave the body of a defer statement |
| BU0430 | Error | '{0}': type used in a scoped statement must define a destructor |
| BU0431 | Error | there is no target type for the implicit object creation |
| BU0432 | Error | '{0}' is not a valid type for object creation |
| BU0433 | Error | file-scoped class must precede all other members in a namespace or type |
| BU0434 | Error | file-scoped class cannot be contained within a non-file-scoped namespace or non-file-scoped class |
| BU0435 | Error | no suitable build method found |
| BU0436 | Error | '#!' must be the first characters on the first line of the file |
| BU0437 | Error | the type of conditional expression cannot be determined because '{0}' and '{1}' implicitly convert to one another |
| BU0438 | Error | the type of conditional expression cannot be determined because there is no implicit conversion between '{0}' and '{1}' |
| BU0439 | Error | cannot create a function pointer to '{0}' because it is not a static method |
| BU0440 | Error | parameterless struct constructors must be 'public' |
| BU0441 | Error | unions must contain at least 1 non-static field |
| BU0442 | Error | cannot clamp on type '{0}'; can only clamp on numeric primitives |
| BU0443 | Error | cannot bit cast operand of nullable type '{0}' |
| BU0444 | Error | cannot bit cast to nullable type '{0}' |
| BU0445 | Error | cannot bit cast with '{0}' as it's size is not known at compile time; consider using 'LowLevel.BitCast\<type TFrom, type TTo>(TFrom)' instead |
| BU0446 | Error | cannot bit cast from '{0}' to '{1}' because they don't have the same size |
| BU0447 | Warning | ignoring return value of method '{0}'; consider using a discard assignment if this is intended |
| BU0448 | Error | a value of type 'void' may not be assigned |
| BU0449 | Error | cannot infer the type of implicitly-typed discard |
| BU0450 | Error | static classes cannot contain finalizers |
| BU0451 | Error | argument {0}: cannot pass a reference to a constant to a parameter expecting a reference to a variable |
| BU0452 | Error | tuple must contain at least 2 elements |
| BU0453 | Error | tuple element name '{0}' is disallowed |
| BU0454 | Error | tuple element name '{0}' is only allowed at position {1} |
| BU0455 | Error | '{0}': tuple element names must be unique |
| BU0456 | Error | 'new' cannot be used with tuple types; use a tuple literal instead |
| BU0457 | Error | invalid token '{0}' in a member declaration |
| BU0458 | Error | using directive must precede all other namespace members |
| BU0459 | Error | there is no target type for the literal with suffix '{0}' |
| BU0460 | Error | type '{0}' has no definition for literals of type '{1}' with the suffix '{2}' |
| BU0461 | Error | the return type for literal operators must match the containing type or be derived from the containing type |
| BU0462 | Error | literal operators must have exactly 1 parameter |
| BU0463 | Error | literal operator parameter must be a type represented by a literal |
| BU0464 | Warning | assignment to a for-each iterator local does not modify the element in the source collection |
| BU0465 | Error | a tuple may not contain a value of type 'void' |
| BU0466 | Warning | '{0}': struct crosses an unnecessary cache line; struct layout could be reduced from {1} bytes to {2} bytes by reordering fields |
| BU0467 | Warning | '{0}': struct layout could be reduced from {1} bytes to {2} bytes by reordering fields |
| BU0468 | Info | '{0}': struct layout could be reduced from {1} bytes to {2} bytes by reordering fields |
| BU0469 | Error | struct pack alignment must be 1, 2, 4, 8, 16, 32, 64, or 128 |
| BU0470 | Error | embedded statement cannot be a defer |
| BU0471 | Warning | long tuple ({0} elements); consider using a named struct |
| BU0472 | Error | predefined type '{0}' is not defined (are you using '--nostdlib'?) |
| BU0473 | Error | a declaration is not allowed in this context |
| BU0474 | Error | a deconstruction variable cannot be declared as a ref local |
| BU0475 | Error | cannot infer the type of implicitly-typed deconstruction variable '{0}' |
| BU0476 | Error | cannot deconstruct a tuple of '{0}' elements into '{1}' variables |
| BU0477 | Error | deconstruction must contain at least 2 variables |
| BU0478 | Error | reverse clause cannot take a parameter because the target method returns void |
| BU0479 | Error | ref mismatch between '{0}' and parameter '{1}' |
| BU0480 | Error | the reverse clause parameter must be of type '{0}' because it is being assigned by reference |
| BU0481 | Error | a method with a state clause cannot return by reference |
| BU0482 | Error | a method with a state clause must have a reverse clause |
| BU0483 | Error | use of unassigned field '{0}' |
| BU0484 | Error | cannot declare a class field without an initializer or definite constructor assignment with type '{0}' because it is non-nullable |
| BU0485 | Error | cannot use a lowlevel default literal outside of a lowlevel context |
| BU0486 | Error | non-nullable globals and const or final locals must have an initializer |
| BU0488 | Error | cannot declare a struct field without definite constructor assignment with type '{0}' because it has no default value |
| BU0489 | Error | cannot declare a lowlevel field inside of a non-lowlevel type |
| BU0490 | Error | cannot access arrays through a nullable receiver; consider using a null assert or conditional access |
| BU0491 | Error | cannot call methods through a nullable receiver; consider using a null assert or conditional access |
| BU0492 | Error | cannot index a nullable receiver; consider using a null assert or conditional access |
| BU0493 | Error | reverse clause must have a parameter that matches the type of the state clause |
| BU0494 | Error | undefined token '{0}' |
| BU0495 | Error | a token with the name '{0}' has already been declared in this scope |
| BU0496 | Error | the target expression of a reversible expression must be a call to a reversible method |
| BU0497 | Error | the target expression of a reverse defer statement must be a call to a reversible method |
| BU0498 | Error | commit statements can only be used within a with statement |
| BU0499 | Error | type '{0}' has no implicit conversion to a tuple of cardinality {1} |
| BU0500 | Error | deconstruct assignment requires an expression with a type on the right-hand-side |
| BU0501 | Error | deconstruction of type '{0}' is ambiguous; consider explicitly typing deconstruction variables |
| BU0502 | Error | expected identifier after verbatim specifier: {0} |
| BU0503 | Warning | lowlevel default literal is unnecessary because the type '{0}' has a default value; consider using a regular default literal |
| BU0504 | Warning | lowlevel default expression is unnecessary because the type '{0}' has a default value; consider using a regular default expression |
| BU0505 | Error | buffer creation must have exactly 1 or 2 arguments |
| BU0506 | Error | arguments in buffer creation cannot be by-reference |
| BU0507 | Error | arguments in buffer creation cannot be omitted |
| BU0508 | Error | second argument in buffer creation must be an initializer list |
| BU0509 | Warning | local function uses the entry point name but is not treated as the entry point because it does not have the correct signature |
| BU0510 | Error | argument {0}: cannot pass a constant to a parameter expecting a variable |
| BU0511 | Error | '{0}': member must be marked 'const' when overriding inherited member '{1}' because it is marked 'const' |
| BU0512 | Warning | '{0}': member is marked 'const' but overridden member '{1}' is not |
| BU0513 | Error | '{0}': parameter '{1}' must be marked 'const' when overriding inherited member '{2}' because the corresponding parameter is marked 'const' |
| BU0514 | Warning | '{0}': parameter '{1}' is marked 'const' but the corresponding parameter on overridden member '{2}' is not |
| BU0515 | Error | invalid object creation; cannot construct the type containing the entry point |
| BU0516 | Error | cannot use a conditional field access because the receiver is not nullable; consider using a regular field access |
| BU0517 | Error | cannot use a conditional array access because the receiver is not nullable; consider using a regular array access |
| BU0518 | Error | cannot use a conditional call because the receiver is not nullable; consider using a regular call |
| BU0519 | Error | cannot use a conditional indexer because the receiver is not nullable; consider using a regular indexer |
| BU0520 | Error | cannot perform an '{0} null' check on an operand of type '{1}' because it is a non-nullable type |
| BU0521 | Error | cannot access properties through a nullable receiver; consider using a null assert or conditional access |
| BU0522 | Error | cannot use a conditional property access because the receiver is not nullable; consider using a regular property access |
| BU0523 | Error | the type '{0}' exists in both '{1}' and '{2}' |
| BU0524 | Warning | "{0}": reference has already been added to the compilation |
| BU0525 | Warning | "{0}": assembly has already been added to the compilation |
| BU0526 | Error | cannot create an instance of the interface '{0}' |
| BU0527 | Warning | '{0}': sealed member declared in sealed type; no different than non-sealed override |
| BU0528 | Warning | null checks should use the 'is' or 'isnt' operator |
| BU0529 | Error | inherited interface '{0}' causes a cycle in the interface hierarchy of '{1}' |
| BU0530 | Error | inconsistent accessibility: interface '{0}' is less accessible than interface '{1}' |
| BU0531 | Error | '{0}' is already listed in the interface list |
| BU0532 | Error | '{0}': static classes cannot implement interfaces |
| BU0533 | Error | type '{0}' in interface list is not an interface |
| BU0534 | Error | '{0}': user-defined conversions to or from an interface are not allowed |
| BU0535 | Error | user-defined conversion in an interface must convert to or from a type template parameter on the enclosing type constrained to the enclosing type |
| BU0536 | Error | user-defined conversion must convert to or from the enclosing type |
| BU0537 | Error | user-defined conversion cannot convert a type to itself |
| BU0538 | Error | '{0}': user-defined conversions to or from a base type are not allowed |
| BU0539 | Error | '{0}': user-defined conversions to or from a derived type are not allowed |
| BU0540 | Error | '{0}' is already listed in the interface list on type '{1}' with different tuple element names, as '{2}' |
| BU0541 | Error | '{0}' is already listed in the interface list on type '{1}' as '{2}' |
| BU0542 | Error | interface members cannot define an implementation |
| BU0543 | Error | interfaces cannot contain constructors |
| BU0544 | Error | only class types can contain finalizers |
| BU0545 | Error | interfaces cannot contain fields |
| BU0546 | Error | conversion, equality, or inequality operators declared in interfaces must be abstract |
| BU0547 | Error | explicit implementation of a user-defined operator '{0}' must be declared static |
| BU0548 | Error | '{0}': explicit interface declaration can only be declared in a class, struct, or interface |
| BU0549 | Error | '{0}' in explicit interface declaration is not an interface |
| BU0550 | Error | '{0}': containing type does not implement interface '{1}' |
| BU0551 | Error | '{0}': return type must be '{1}' to match implemented member '{2}' |
| BU0552 | Error | '{0}': type must be '{1}' to match implemented member '{2}' |
| BU0553 | Error | '{0}' in explicit interface declaration is not found among members of the interface that can be implemented |
| BU0554 | Error | the tuple element names in the signature of method '{0}' must match the tuple element names of interface method '{1}' (including on the return type) |
| BU0555 | Error | cannot inherit interface '{0}' with the specified template parameters because it causes method '{1}' to contain overloads which differ only on ref and out |
| BU0556 | Error | '{0}' is explicitly implemented more than once |
| BU0557 | Error | '{0}' does not implement interface member '{1}' |
| BU0558 | Error | '{0}' cannot implement both '{1}' and '{2}' because they may unify for some type template parameter substitutions |
| BU0559 | Error | interface member '{0}' does not have a most specific implementation; neither '{1}', nor '{2}' are most specific |
| BU0560 | Error | '{0}' does not implement interface member '{1}'; '{2}' cannot implicitly implement an inaccessible member |
| BU0561 | Error | 'Unmanaged' method '{0}' cannot implement interface member '{1}' in type '{2}' |
| BU0562 | Error | the constraints for type template parameter '{0}' of method '{1}' must match the constraints for type template parameter '{2}' of interface method '{3}'; consider using an explicit interface implementation instead |
| BU0563 | Error | '{0}' does not implement instance interface member '{1}'; '{2}' cannot implement the interface member because it is static |
| BU0564 | Error | '{0}' does not implement static interface member '{1}'; '{2}' cannot implement the interface member because it is not static |
| BU0565 | Error | '{0}' does not implement interface member '{1}'; '{2}' cannot implement the interface member because it is not public |
| BU0566 | Error | '{0}' does not implement interface member '{1}'; '{2}' cannot implement '{3}' because it does not have matching return by reference |
| BU0567 | Error | '{0}' does not implement interface member '{1}'; '{2}' cannot implement '{3}' because one of them is not an operator |
| BU0568 | Error | '{0}' does not implement interface member '{1}'; '{2}' cannot implement '{3}' because it does not have the matching return type of '{4}' |
| BU0569 | Error | cannot apply attribute class '{0}' because it is abstract |
| BU0570 | Error | '{0}' is not an attribute class |
| BU0571 | Error | all lines must start with the same whitespace as the closing line of the multiline string; place the closing quotations on a non-isolated line if this is intentional |
| BU0572 | Error | the type '{0}' must have a default value in order to use it as parameter '{1}' in the template type or method '{2}' |
| BU0573 | Warning | assignment made to same variable; did you mean to assign something else? |
| BU0574 | Error | expression is too long or complex to compile |
| BU0575 | Error | template parameter underlying type must be 'type' or a primitive |
| BU0576 | Error | template constraint on '{0}' fails to evaluate ('{1}') |
| BU0577 | Error | a static virtual or abstract interface member can be accessed only on a type template parameter |
| BU0578 | Error | ignoring return value of method '{0}' with 'MustUseReturnValue' attribute |
| BU0579 | Error | 'MustUseReturnValue' can only be applied to methods returning a value |
| BU0580 | Error | a get or set accessor expected |
| BU0581 | Error | patterns are not permitted for pointer types |
| BU0582 | Error | '{0}' -> '{1}': template instantiation depth exceeds maximum; recurse caused by {2} '{3}' |
| BU0583 | Warning | compile-time expression is unnecessary as the target expression is already a compile-time constant |
| BU0584 | Error | overloaded compound assignment operator '{0}' takes 1 parameter |
| BU0585 | Error | the return type for assignment operators must be void |
| BU0586 | Error | user-defined operator '{0}' must be declared public |
| BU0587 | Error | user-defined operator '{0}' cannot have template parameters |
| BU0588 | Error | the typeof operator cannot be used on a nullable reference type |
| BU0589 | Error | enum methods cannot have a reverse clause |
| BU0590 | Error | attribute '{0}' is only valid on classes derived from Attribute |
| BU0591 | Error | duplicate '{0}' attribute |
| BU0592 | Error | attribute '{0}' is not valid on this declaration type; it is only valid on '{1}' declarations |
| BU0593 | Error | failed to emit module '{0}' |
| BU0594 | Error | attribute constructor parameter '{0}' has type '{1}', which is not a valid attribute parameter type |
| BU0595 | Error | an attribute argument must be a constant expression, typeof expression, or array creation expression of an attribute parameter type |
| BU0596 | Error | '{0}' is attributed with 'Unmanaged' and cannot be called directly; obtain an unmanaged function pointer to this method |
| BU0597 | Error | behavior specifier '{0}' has already been applied to this item |
| BU0598 | Error | behavior specifier 'memoize' can only be used with the behavior specifier 'pure' |
| BU0599 | Error | behavior specifier '{0}' is not valid for this item |
| BU0600 | Error | cannot allocate a new object in a 'noalloc' context |
| BU0601 | Error | cannot call method '{0}' in the current context because it is not marked '{1}' |
| BU0602 | Error | cannot write to a member of a reference type in a method marked as 'pure' |
| BU0603 | Error | cannot read from a mutable member of a reference type in a method marked as 'pure' |
| BU0604 | Error | cannot throw an uncaught exception in a 'nothrow' context |
| BU0605 | Error | method marked as 'pure' cannot have a reverse clause |
| BU0606 | Error | '{0}': member must be marked '{1}' when overriding inherited member '{2}' because it is marked '{3}' |
| BU0607 | Warning | '{0}': member is marked '{1}' but overridden member '{2}' is not |
| BU0608 | Error | cannot downcast 'this' to derived type '{0}' in a constructor |
| BU0609 | Warning | call potentially leaks uninitialized object state |
| BU0610 | Error | expression potentially throws; cannot throw an uncaught exception in a 'nothrow' context |
| BU0611 | Warning | try statement is unnecessary as no statements within it can throw |
| BU0612 | Warning | failed to emit assembly attribute 'BelteMetadataAttribute' (are you using '--nostdlib'?) |
| BU0613 | Warning | failed to emit attribute '{0}' (are you using '--nostdlib'?) |
| BU0614 | Error | compile-time template parameter must have an underlying type of 'type!' |
| BU0615 | Warning | template specialization is unnecessary because the target is marked as compile-time only |
| BU0616 | Error | cannot use template specialization on type '{0}' because it has no template metadata to instantiate from |
| BU0617 | Error | cannot use a Buffer with element type '{0}' outside of a lowlevel context because it has no default value |
| BU0618 | Error | the type '{0}' must have a public parameterless constructor in order to use it as parameter '{1}' in the template type or method '{2}' |
| BU0619 | Error | the 'Conditional' attribute is not valid on interface members |
| BU0620 | Error | the 'Conditional' attribute is not valid on '{0}' because it is an override method |
| BU0621 | Error | the 'Conditional' attribute is not valid on '{0}' because it is a constructor, finalizer, operator, or explicit interface implementation |
| BU0622 | Error | the 'Conditional' attribute is not valid on '{0}' because its return type is not void |
| BU0623 | Error | the 'Conditional' attribute is not valid on '{0}' because it has an out parameter |
| BU0624 | Error | local function '{0}' must be 'static' in order to use the 'Conditional' attribute |
| BU0625 | Error | attribute '{0}' is only valid on methods or attribute classes |
| BU0626 | Error | methods marked with 'memoize' must be static |
| BU0627 | Error | methods marked with 'memoize' cannot have pointer types in their signature |
| BU0628 | Error | methods marked with 'memoize' cannot have ref parameters or return by-reference |
| BU0629 | Error | cannot use inline IL inside of a 'pure' context |
| BU0630 | Error | the type '{0}' is defined in an assembly that is not referenced; you must add a reference to assembly '{1}' |
| BU0631 | Error | the type '{0}' is defined in a module that has not been added; you must add the module '{1}' |
| BU0632 | Error | '{0}': property must have at least one accessor |
| BU0633 | Error | auto-implemented properties cannot return by reference |
| BU0634 | Error | properties which return by reference must have a get accessor |
| BU0635 | Error | properties which return by reference cannot have set accessors |
| BU0636 | Error | a non ref-returning property may not be used as an out or ref value |
| BU0637 | Error | property '{0}' cannot be assigned to |
| BU0638 | Error | the property '{0}' cannot be used in this context because the set accessor is inaccessible |
| BU0639 | Error | the property '{0}' cannot be used in this context because it lacks the get accessor |
| BU0640 | Error | the property '{0}' cannot be used in this context because the get accessor is inaccessible |
| BU0641 | Error | 'or' expressions require an operand of type 'Result\<type! T, type! E>!' |
| BU0642 | Error | 'or return' expression requires the containing member to have a return type of '{0}' or a Result type with the same second template argument '{1}' |
| BU0643 | Error | only range for loops can use the 'unroll' specifier |
| BU0644 | Error | range must be compile-time to use the 'unroll' specifier |
| BU0645 | Error | range for loops cannot use an index local |
| BU0646 | Error | use of unassigned out parameter '{0}' |
| BU0647 | Error | cannot declare a class property without an initializer or definite constructor assignment with type '{0}' because it is non-nullable |
| BU0648 | Error | cannot declare a struct property without definite constructor assignment with type '{0}' because it has no default value |
| BU0649 | Error | default parameter value for '{0}' cannot reference later parameter '{1}' |
| BU0650 | Error | default parameter value for '{0}' cannot reference itself |
| BU0651 | Error | default parameter value for '{0}' cannot reference local function '{1}' |
| BU0652 | Error | '{0}': property cannot have void type |
| BU0653 | Error | '{0}': cannot reference runtime state in a compile-time expression |
| BU0654 | Error | expression threw an uncaught exception when evaluating at compile time |
| BU0655 | Info | expression threw an uncaught exception when evaluating at compile time; code will be executed at runtime |
| BU0656 | Error | expression is not computable at compile time; reason: |
| BU0657 | Error | evaluation of compile-time expression caused stack overflow |
| BU0658 | Info | evaluation of compile-time expression caused stack overflow; code will be executed at runtime |
| BU0659 | Error | '{0}' cannot use the 'EntryType' attribute because it contains no methods with an entry point signature |
| BU0660 | Warning | '{0}': unused parameter '{1}' |
| BU0661 | Warning | unnecessary parameter discard as parameter '{0}' is used elsewhere |
| BU0662 | Warning | local '{0}' is unused |
| BU0663 | Warning | local '{0}' could be marked 'const' |
| BU0664 | Warning | local '{0}' could be marked 'final' |
| BU0666 | Error | null-binding target must be marked as constant if the source expression is constant |
| BU0667 | Error | cannot mark a data container as both final and variable |
| BU0668 | Error | cannot mark a data container as both constant expression and variable |
| BU0669 | Error | symbol '{0}' cannot be marked as constant because it has a pointer type |
| BU0670 | Error | parameter '{0}' cannot be marked as constant because it has a pointer type |
| BU0671 | Error | argument {0}: parameter requires a compile-time constant argument |
| BU0672 | Error | template constraint on '{0}' fails to evaluate ('{1}') and could not be proven by the substituted constraints |
| BU0673 | Error | non-template types cannot have constraints |
| BU0674 | Error | cannot use a compile time expression in a constraint clause |
| BU0675 | Error | pattern type is identical to the expression type; pattern always succeeds |
| BU0676 | Error | floating-point constant '{0}' is too large |
| BU0677 | Error | a field initializer cannot reference non-static member '{0}' |
| BU0678 | Error | preprocessor directive expected |
| BU0679 | Error | single-line comment or end-of-line expected |
| BU0680 | Error | cannot use captured data container '{0}' by-reference inside of a local function |
| BU0681 | Error | cannot emit type '{0}'; it is a compile-time type only |
| BU0682 | Error | insert statement requires a compile-time constant of type 'Code' |
| BU0683 | Error | cannot insert code from parameter '{0}' because '{1}' is not marked 'expand' |
| BU0684 | Error | methods marked with 'expand' must be static |
| BU0685 | Error | not all code paths for expansion method return a value |
| BU0686 | Error | code string must represent a single statement |
| BU0687 | Error | exec expression must be an ordinary static non-template method call with compile-time constant arguments |
| BU0688 | Error | exec expression must result in a primitive type |
| BU0689 | Error | type string must represent a single type declaration |
| BU0690 | Error | exec expression failed to execute due to errors in method '{0}' |
| BU0691 | Error | cycle in exec expression; exec expression depends on '{0}' which it is contained in |
| BU0692 | Error | late-bound identifier names can only appear within methods marked 'expand' |
| BU0693 | Error | there is no target type for the late-bound identifier name |
| BU0694 | Error | late-bound identifier name must refer to a parameter or local; '{0}' is a {1} |
| BU0695 | Error | late-bound identifier name must refer to a parameter or local with the target type '{0}'; late-bound identifier name refers to a symbol with type '{1}' |
| BU0696 | Error | late-bound identifier name must refer to a by-value variable |
| BU0697 | Error | non-isolated diagnostic expression must be followed by an expression |
| BU0698 | Error | cannot alias method '{0}' because it is not static |
| BU0699 | Error | the alias '{0}' cannot be used with template arguments because it targets an already constructed method group |
| BU0700 | Error | the type caught must be derived from 'System.Exception' |
| BU9000 | Unsupported | unsupported: cannot compile independently currently; must specify '-i' or '-r' |
| BU9001 | Unsupported | unsupported: cannot declare a non-type template local function |
| BU9002 | Unsupported | unsupported: inline IL instruction '{0}' not supported |
| BU9003 | Unsupported | unsupported: cannot use non-integral enums when building for .NET, transpiling to C#, or executing |
| BU9004 | Unsupported | unsupported: cannot use the graphics project type when building for .NET or transpiling to C#; consider executing instead |
| BU9005 | Unsupported | cannot make Graphics calls when the output kind is not graphics |
| BU9006 | Unsupported | unsupported: cannot use inline IL when transpiling to C# |

## Command Line Diagnostics

| Code | Severity | Message |
| - | - | - |
| CL0001 | Error | missing filename after '-o' |
| CL0002 | Error | cannot specify '--explain' more than once |
| CL0003 | Error | missing diagnostic code after '--explain' (usage: '--explain[BU\|RE\|CL]\<code>') |
| CL0004 | Error | missing name after '{0}' (usage: '--modulename=\<name>') |
| CL0005 | Error | missing name after '{0}' (usage: '--ref[,flat,copy]=\<path>') |
| CL0006 | Error | failed to open file '{0}'; most likely due to the file being used by another process |
| CL0007 | Error | missing severity after '{0}' (usage: '--severity=\<severity>') |
| CL0008 | Error | unrecognized severity '{0}' |
| CL0009 | Error | unrecognized command line option '{0}'; see 'buckle --help' |
| CL0010 | Info | all arguments are ignored when invoking the Repl |
| CL0011 | Fatal | cannot specify '-p', '-s', '-c', or '-t' with .NET integration |
| CL0012 | Fatal | cannot specify output file with '-p', '-s', '-c', or '-t' with multiple files |
| CL0013 | Fatal | cannot specify output path or use '-p', '-s', '-c', or '-t' with interpreter |
| CL0014 | Fatal | cannot specify module name without .NET integration |
| CL0015 | Fatal | cannot specify references without .NET integration |
| CL0016 | Fatal | no input files |
| CL0017 | Error | {0}: no such file or directory |
| CL0018 | Info | unknown file type of input file '{0}'; ignoring |
| CL0019 | Error | '{0}' is not a valid diagnostic code; must be in the format: [BU\|CL\|RE]\<code>{1} examples: BU0001, CL0001, BU54, CL012, RE0001, RE6 |
| CL0020 | Info | {0}: file already compiled; ignoring |
| CL0021 | Error | '{0}' is not a used diagnostic code |
| CL0022 | Fatal | cannot pass multiple files when running as a script |
| CL0023 | Fatal | cannot interpret file |
| CL0024 | Error | missing warning level after '{0}' (usage: '--warnlevel=\<warning level>') |
| CL0025 | Error | invalid warning level '{0}'; warning level must be a number between 0 and 3 |
| CL0026 | Error | missing warning code after '{0}' (usage: '--wignore=\<[BU\|RE\|CL]\<code>,...>') |
| CL0027 | Error | missing warning code after '{0}' (usage: '--winclude=\<[BU\|RE\|CL]\<code>,...>') |
| CL0028 | Error | '{0}' is not the code of a warning |
| CL0029 | Error | missing project type after '{0}' (usage: '--type=[console\|graphics\|...]') |
| CL0030 | Error | unrecognized project type '{0}' |
| CL0031 | Fatal | cannot compile to a dynamically linked library without .NET integration |
| CL0032 | Fatal | cannot specify an output file and module name when building a dynamically linked library |
| CL0033 | Error | missing path after '{0}' (usage: '--vpath=\<path>') |
| CL0034 | Error | missing core count after '{0}' (usage: '-m:\<count>') |
| CL0035 | Error | '{0}' is not a valid core count; core count must be a positive integer |
| CL0036 | Error | missing type name after '{0}' (usage: '--entry=\<name>') |
| CL0037 | Fatal | output file '{0}' matches the path of an existing directory |
| CL0038 | Error | missing file association after '{0}' |
| CL0039 | Error | unrecognized file association '{0}' |
| CL0040 | Error | missing path after '--flat' (usage: '--flat \<path>') |
| CL0041 | Warning | unable to copy '{0}' to '{1}'; most likely due to a file being used by another process |
| CL0042 | Error | '{0}': cannot create new project because location is occupied |
| CL0043 | Error | unable to run project; project does not have a runnable build mode |
| CL0044 | Error | failed to start process: {0} |
| CL0045 | Error | missing warning code after '{0}' (usage: '--werrignore=\<[BU\|RE\|CL]\<code>,...>') |
| CL0046 | Error | missing warning code after '{0}' (usage: '--werrinclude=\<[BU\|RE\|CL]\<code>,...>') |
| CL0047 | Error | missing warning level after '{0}' (usage: '--werror=\<warning level>') |
| CL0048 | Error | missing filename after '{0}' |
| CL0049 | Fatal | cannot specify '--skiptm' when not building a dynamically linked library |
| CL0050 | Fatal | cannot specify '--r2r' without .NET integration |
| CL0051 | Warning | failed to create R2R assembly |
| CL0052 | Error | missing preprocessor symbol after '{0}' |
| CL0053 | Error | missing path after '{0}' (usage: '--scpath=\<path>') |

## Repl Diagnostics

| Code | Severity | Message |
| - | - | - |
| RE0001 | Error | unknown repl command '{0}' |
| RE0002 | Error | invalid number of arguments{0}usage: #{1} {2} |
| RE0003 | Error | undefined symbol '{0}' |
| RE0004 | Error | {0}: no such file |
| RE0005 | Error | invalid argument '{0}'; expected argument of type '{1}' |
| RE0006 | Error | no such method with the signature '{0}' exists |
| RE0008 | Error | failed to generate IL: cannot reference locals or globals from previous submissions with the '#showIL' toggle on |
| RE0009 | Error | invalid argument '{0}'; expected {1} |
| RE0010 | Error | failed to open file '{0}'; most likely due to the file being used by another process |

---

*<small>Generated from https://github.com/ryanwilsond/belte/tree/main/src/Tools/DiagnosticCodeDoc</small>*
