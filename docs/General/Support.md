# Support

## Discord

-   You can find us on [Discord](https://discord.gg/2BvnM4R).

## GitHub

-   You can create an issue in [GitHub](https://github.com/vis2k/Mirror/issues)
-   You can also contribute with Pull Requests...see below:

## Code Conventions

-   **KISS / Occam's Razor** - always use the most simple solution.
-   **No Premature Optimizations**
	MMOs need to run for weeks without issues or exploits.
	If you want your code to run 1% faster, spend \$100 on a better CPU.
-   **Curly Braces { }**  
    Always use braces even for one line if's. Unity did this everywhere, and there is value in not accidentally missing a line in an if statement because there were no braces.
-   **Variable naming**  
    \`NetworkIdentity identity\`, not \`NetworkIdentity uv\` or similar. If the variable needs a comment the name needs to be changed. For example, `msg = ... // the message` use `message = ...` without a comment instead Please Avoid **var** where possible. My text editor doesn't show me the type, so it needs to be obvious, and having two different ways to do the same thing only creates unnecessary complexity and confusion.
