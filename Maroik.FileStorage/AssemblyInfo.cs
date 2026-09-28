using System.Runtime.CompilerServices;

// The test project exercises internal types (StoragePath) directly rather than only through
// the public HTTP surface, so it needs friend access here.
[assembly: InternalsVisibleTo("Maroik.FileStorage.Tests")]
