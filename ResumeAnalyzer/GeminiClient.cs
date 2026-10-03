using System.Net;
using System.Text;
using System.Text.Json;

namespace ResumeAnalyzer;

public class GeminiClient
{
    private const string BaseUrl = "https://generativelanguage.googleapis.com/v1beta/models";
    private const int AttemptsPerModel = 2;

    private readonly HttpClient _http = new();
    private readonly string _apiKey;
    private readonly string[] _models;

    public GeminiClient(string apiKey, string models)
    {
        _apiKey = apiKey;
        _models = models.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public async Task<string> AnalyzeAsync(byte[] pdf, string prompt)
    {
        var body = JsonSerializer.Serialize(new
        {
            contents = new[]
            {
                new
                {
                    parts = new object[]
                    {
                        new { inlineData = new { mimeType = "application/pdf", data = Convert.ToBase64String(pdf) } },
                        new { text = prompt }
                    }
                }
            },
            generationConfig = new { responseMimeType = "application/json" }
        });

        var lastError = "";

        foreach (var model in _models)
        {
            for (var attempt = 1; attempt <= AttemptsPerModel; attempt++)
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/{model}:generateContent")
                {
                    Content = new StringContent(body, Encoding.UTF8, "application/json")
                };
                request.Headers.Add("x-goog-api-key", _apiKey);

                var response = await _http.SendAsync(request);
                var json = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    Console.WriteLine($"Gemini: відповідь від моделі {model}");
                    return ReadText(json);
                }

                lastError = $"Gemini API ({model}): {(int)response.StatusCode} {json}";
                Console.WriteLine(lastError);

                var temporary = response.StatusCode == HttpStatusCode.ServiceUnavailable
                    || response.StatusCode == HttpStatusCode.TooManyRequests
                    || response.StatusCode == HttpStatusCode.InternalServerError;

                if (!temporary)
                {
                    break;
                }

                await Task.Delay(3000);
            }
        }

        throw new Exception(lastError);
    }

    private static string ReadText(string json)
    {
        using var document = JsonDocument.Parse(json);
        return document.RootElement
            .GetProperty("candidates")[0]
            .GetProperty("content")
            .GetProperty("parts")[0]
            .GetProperty("text")
            .GetString() ?? "";
    }
}
