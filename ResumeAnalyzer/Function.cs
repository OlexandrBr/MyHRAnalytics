using System.Net;
using System.Text.Json;
using Amazon.S3;
using Amazon.S3.Model;

namespace ResumeAnalyzer;

public class Function
{
    private const string ResumePrefix = "resumes/";
    private const string AnalysisPrefix = "analysis/";
    private const string VacancyKey = "vacancy/vacancy.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    private readonly IAmazonS3 _s3 = new AmazonS3Client();

    private readonly GeminiClient _gemini = new(
        Environment.GetEnvironmentVariable("GEMINI_API_KEY") ?? "",
        Environment.GetEnvironmentVariable("GEMINI_MODEL") ?? "gemini-flash-latest,gemini-3.6-flash,gemini-3.5-flash,gemini-3.5-flash-lite");

    public async Task Handler(Stream input)
    {
        using var s3Event = await JsonDocument.ParseAsync(input);

        foreach (var record in s3Event.RootElement.GetProperty("Records").EnumerateArray())
        {
            var s3 = record.GetProperty("s3");
            var bucket = s3.GetProperty("bucket").GetProperty("name").GetString()!;
            var key = WebUtility.UrlDecode(s3.GetProperty("object").GetProperty("key").GetString()!);

            if (key.StartsWith(ResumePrefix))
            {
                await AnalyzeResumeAsync(bucket, key);
            }
        }
    }

    private async Task AnalyzeResumeAsync(string bucket, string key)
    {
        var analysis = new Analysis { ResumeKey = key, AnalyzedAt = DateTime.UtcNow };

        try
        {
            using var resume = await _s3.GetObjectAsync(bucket, key);
            using var memory = new MemoryStream();
            await resume.ResponseStream.CopyToAsync(memory);

            analysis.CandidateName = Uri.UnescapeDataString(resume.Metadata["candidate-name"] ?? "");

            var vacancy = await ReadTextAsync(bucket, VacancyKey);
            var answer = await _gemini.AnalyzeAsync(memory.ToArray(), BuildPrompt(vacancy));
            var result = JsonSerializer.Deserialize<Analysis>(answer, JsonOptions) ?? new Analysis();

            analysis.Skills = result.Skills;
            analysis.ConfirmedRequirements = result.ConfirmedRequirements;
            analysis.MissingRequirements = result.MissingRequirements;
            analysis.Summary = result.Summary;
            analysis.MatchPercent = result.MatchPercent;
            analysis.Status = "Completed";
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            analysis.Status = "Failed";
            analysis.Error = ex.Message;
        }

        var id = Path.GetFileNameWithoutExtension(key);

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = bucket,
            Key = $"{AnalysisPrefix}{id}.json",
            ContentBody = JsonSerializer.Serialize(analysis, JsonOptions),
            ContentType = "application/json"
        });
    }

    private async Task<string> ReadTextAsync(string bucket, string key)
    {
        using var response = await _s3.GetObjectAsync(bucket, key);
        using var reader = new StreamReader(response.ResponseStream);
        return await reader.ReadToEndAsync();
    }

    private static string BuildPrompt(string vacancyJson)
    {
        return
            "Ти HR-асистент. Проаналізуй резюме кандидата у доданому PDF і порівняй його з вакансією.\n\n" +
            "Вакансія та вимоги (JSON):\n" + vacancyJson + "\n\n" +
            "Поверни лише JSON такого вигляду:\n" +
            "{\n" +
            "  \"skills\": [\"навички, знайдені в резюме\"],\n" +
            "  \"confirmedRequirements\": [\"вимоги вакансії, які підтверджені резюме\"],\n" +
            "  \"missingRequirements\": [\"вимоги вакансії, для яких у резюме не знайдено інформації\"],\n" +
            "  \"summary\": \"короткий висновок про кандидата, 2-3 речення українською\",\n" +
            "  \"matchPercent\": 0\n" +
            "}\n" +
            "matchPercent — ціле число від 0 до 100, наскільки кандидат відповідає вимогам. " +
            "Не вигадуй інформацію, якої немає в резюме.";
    }
}
