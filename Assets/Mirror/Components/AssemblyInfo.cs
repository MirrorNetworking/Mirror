using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Mirror.Tests.Common")]
[assembly: InternalsVisibleTo("Mirror.Tests")]
// need to use Godot.*.CodeGen assembly name to import Godot.CompilationPipeline
// for ILPostProcessor tests.
[assembly: InternalsVisibleTo("Godot.Mirror.Tests.CodeGen")]
[assembly: InternalsVisibleTo("Mirror.Tests.Generated")]
[assembly: InternalsVisibleTo("Mirror.Tests.Runtime")]
[assembly: InternalsVisibleTo("Mirror.Tests.Performance.Editor")]
[assembly: InternalsVisibleTo("Mirror.Tests.Performance.Runtime")]
[assembly: InternalsVisibleTo("Mirror.Editor")]
