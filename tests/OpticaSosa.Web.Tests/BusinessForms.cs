using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using SkiaSharp;
using Xunit;

namespace OpticaSosa.Web.Tests;

internal static class BusinessForms
{
    public static byte[] Logo()
    {
        using var bitmap = new SKBitmap(20, 10); bitmap.Erase(SKColors.Teal);
        using var image = SKImage.FromBitmap(bitmap);
        using var png = image.Encode(SKEncodedImageFormat.Png, 100);
        return png.ToArray();
    }

    public static async Task<Dictionary<string, string>> ReadAsync(HttpClient browser)
    {
        var html = await browser.GetStringAsync("/admin/negocio");
        string Value(string name)
        {
            foreach (Match input in Regex.Matches(html, "<input\\b[^>]*>"))
                if (input.Value.Contains("name=\"" + name + "\""))
                    return WebUtility.HtmlDecode(Regex.Match(input.Value, "\\bvalue=\"([^\"]*)\"").Groups[1].Value);
            throw new InvalidOperationException("Falta campo del formulario: " + name);
        }
        return new() { ["Input.Version"] = Value("Input.Version"), ["__RequestVerificationToken"] = Value("__RequestVerificationToken") };
    }

    public static async Task<HttpResponseMessage> SaveAsync(HttpClient browser, Dictionary<string, string> values,
        byte[]? logo = null, string fileName = "logo.png", string contentType = "image/png", Dictionary<string, string>? staleForm = null)
    {
        var fields = staleForm ?? await ReadAsync(browser);
        foreach (var field in values) fields[field.Key] = field.Value;
        using var body = new MultipartFormDataContent();
        foreach (var field in fields) body.Add(new StringContent(field.Value), field.Key);
        if (logo is not null)
        {
            var file = new ByteArrayContent(logo); file.Headers.ContentType = new MediaTypeHeaderValue(contentType);
            body.Add(file, "Logo", fileName);
        }
        return await browser.PostAsync("/admin/negocio", body);
    }

    public static void Saved(HttpResponseMessage response) => Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
}
