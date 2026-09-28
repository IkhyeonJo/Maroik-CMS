using System.Runtime.CompilerServices;

// The test project exercises internal types (message handlers, workers) directly,
// so it needs friend access to this assembly.
[assembly: InternalsVisibleTo("Maroik.Worker.Tests")]
