using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace OpticaSosa.Web.Tests;

internal static class BrowserForms
{
    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string page, string target, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync(page);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, "El formulario debe incluir token CSRF.");
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value);
        // Reproducir los marcadores que Razor genera para números HTML con punto decimal,
        // incluso si la cultura de Windows usa coma. El navegador los envía junto al formulario.
        var fields = values.ToList();
        foreach (Match marker in Regex.Matches(html, "name=\"__Invariant\"[^>]*value=\"([^\"]+)\""))
            fields.Add(new("__Invariant", WebUtility.HtmlDecode(marker.Groups[1].Value)));
        return await client.PostAsync(target, new FormUrlEncodedContent(fields));
    }

    public static Task<HttpResponseMessage> LoginAsync(HttpClient client, string name, string password = SecurityApp.TestPassword) =>
        PostAsync(client, "/cuenta/ingresar", "/cuenta/ingresar", new() { ["UserName"] = name, ["Password"] = password });
}
