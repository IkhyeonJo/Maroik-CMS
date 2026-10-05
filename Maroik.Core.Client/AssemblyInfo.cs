using System.Runtime.CompilerServices;

// Tests substitute the SMTP client (to make a step that MailKit itself never fails) through an internal seam.
[assembly: InternalsVisibleTo("Maroik.Core.Client.Tests")]
