using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace OpticaSosa.Web.Tests;

internal static class CustomerBrowserForms
{
    public static async Task<HttpResponseMessage> PostAsync(HttpClient client, string page, string formName, Dictionary<string, string> values)
    {
        var html = await client.GetStringAsync(page);
        var fields = Read(html, formName);
        foreach (var field in values) fields[field.Key] = field.Value;
        return await client.PostAsync(page, new FormUrlEncodedContent(fields));
    }

    public static Dictionary<string, string> Read(string html, string formName)
    {
        foreach (Match form in Regex.Matches(html, "<form\\b[^>]*>.*?</form>", RegexOptions.Singleline))
        {
            var fields = new Dictionary<string, string>();
            foreach (Match input in Regex.Matches(form.Value, "<input\\b[^>]*>"))
            {
                if (!input.Value.Contains("type=\"hidden\"")) continue;
                var name = Regex.Match(input.Value, "\\bname=\"([^\"]+)\"");
                var value = Regex.Match(input.Value, "\\bvalue=\"([^\"]*)\"");
                if (name.Success && value.Success)
                    fields[WebUtility.HtmlDecode(name.Groups[1].Value)] = WebUtility.HtmlDecode(value.Groups[1].Value);
            }
            if (fields.TryGetValue("_handler", out var handler) && handler == formName)
            {
                Assert.True(fields.ContainsKey("__RequestVerificationToken"));
                return fields;
            }
        }
        throw new InvalidOperationException("No se encontró el formulario SSR: " + formName);
    }

    public static void Redirect(HttpResponseMessage response) =>
        Assert.True(response.StatusCode is HttpStatusCode.Redirect or HttpStatusCode.SeeOther, $"Se esperaba redirección, se obtuvo {(int)response.StatusCode}.");

    public static Dictionary<string, string> NewCustomer(string document = "FIC-HTTP-001") => new()
    {
        ["Input.FirstName"] = "Nombre ficticio", ["Input.LastName"] = "Prueba HTTP", ["Input.Document"] = document
    };
}
