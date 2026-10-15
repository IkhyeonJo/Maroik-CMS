using System.Net.Http.Headers;

namespace Maroik.Website.Tests.Infrastructure;

/// <summary>
/// Builds the multipart form bodies the tests post. Each part is handed over to its form, which disposes its
/// parts when it is disposed itself; the hand-over is a try/finally so a part that could not be added is
/// disposed straight away (the CA2000 ownership-transfer pattern).
/// </summary>
internal static class MultipartForm
{
    /// <summary>A form holding the given text fields, in order.</summary>
    public static MultipartFormDataContent Of(params (string Name, string Value)[] fields)
    {
        MultipartFormDataContent? form = new();
        try
        {
            foreach (var (name, value) in fields)
                AddText(form, name, value);
            MultipartFormDataContent built = form;
            form = null;
            return built;
        }
        finally
        {
            form?.Dispose();
        }
    }

    /// <summary>Adds the text field <paramref name="name"/> = <paramref name="value"/> to <paramref name="form"/>.</summary>
    public static void AddText(MultipartFormDataContent form, string name, string value)
    {
        StringContent? part = new(value);
        try
        {
            form.Add(part, name);
            part = null;
        }
        finally
        {
            part?.Dispose();
        }
    }

    /// <summary>Adds a file part named <paramref name="name"/>, with a Content-Type header when <paramref name="contentType"/> is given.</summary>
    public static void AddFile(MultipartFormDataContent form, string name, string fileName, byte[] bytes, string? contentType = null)
    {
        ByteArrayContent? part = new(bytes);
        try
        {
            if (contentType != null)
                part.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            form.Add(part, name, fileName);
            part = null;
        }
        finally
        {
            part?.Dispose();
        }
    }
}
