# Code Conventions

## General Philosophy

**KISS / Occam's Razor** - always use the most simple solution.

**No Premature Optimizations** - MMOs need to run for weeks without issues or
exploits. If you want your code to run 1% faster, spend \$100 on a better CPU.

## <https://github.com/vis2k/Mirror/wiki/Code-Conventions#parentheses>Parentheses

Always use {} even for one line ifs. HLAPI did this everywhere, and there is
value in not accidentally missing a line in an if statement because there were
no parentheses.

## <https://github.com/vis2k/Mirror/wiki/Code-Conventions#variable-naming>Variable naming

'NetworkIdentity identity', not 'NetworkIdentity uv' or similar

If the variable needs a comment the name needs to be changed. For example, `msg
= ... // the message` use `message = ...` without a comment instead

Please Avoid **var** where possible. My text editor doesn't show me the type, it
needs to be obvious. And having two different ways to do the same thing only
creates unnecessary complexity and confusion.
