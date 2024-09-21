# Member Ordering

The order of members in the Standard Library is as follows (subject to change):

Within a class or struct:

- Private/protected fields
- Constructors
- Public fields
- Abstract methods
- Methods
- Structs
- Classes

Within each of these groups, order by access:

- Public
- Protected
- Private

Within each of the access groups, order by static then non-static:

- Static (except operators)
- Non-static

Within each of these groups, order by originality:

- Virtual
- None
- Override

Within each of these groups, order by constant-ality:

- Constant expression
- Constant
- Non-constant

Within each of these groups, order by non-lowlevel then lowlevel:

- Non-lowlevel
- Lowlevel

Within each of these groups, order alphabetically or by relation.

Operators go below all other methods in the same accessibility group.
