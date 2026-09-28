using System.Runtime.CompilerServices;

// The test project exercises internal types (controllers, filters, helpers) directly
// rather than only through the public HTTP surface, so it needs friend access here.
[assembly: InternalsVisibleTo("Maroik.Website.Tests")]
