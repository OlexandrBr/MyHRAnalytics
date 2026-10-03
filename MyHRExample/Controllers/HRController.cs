using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using MyHRExample.Models;
using MyHRExample.Services;

namespace MyHRExample.Controllers
{
    public class HRController : Controller
    {
        private const string VacancyKey = "vacancy/vacancy.json";
        private const string ResumePrefix = "resumes/";

        private readonly S3Service _s3Service;

        public HRController(S3Service s3Service)
        {
            _s3Service = s3Service;
        }

        public async Task<IActionResult> Index()
        {
            var json = await _s3Service.ReadTextAsync(VacancyKey);
            var vacancy = json == null ? new Vacancy() : JsonSerializer.Deserialize<Vacancy>(json);
            return View(vacancy);
        }

        [HttpPost]
        public async Task<IActionResult> Index(string vacancyName, string requirements)
        {
            var vacancy = new Vacancy { Name = vacancyName, Requirements = requirements };
            await _s3Service.SaveTextAsync(VacancyKey, JsonSerializer.Serialize(vacancy));

            ViewBag.Saved = true;
            return View(vacancy);
        }

        public async Task<IActionResult> Resumes()
        {
            var resumes = new List<Resume>();

            foreach (var file in await _s3Service.ReadAsync(ResumePrefix))
            {
                var metadata = await _s3Service.GetMetadataAsync(file.Key);

                resumes.Add(new Resume
                {
                    Key = file.Key,
                    CandidateName = metadata.GetValueOrDefault("candidate-name", ""),
                    Contacts = metadata.GetValueOrDefault("contacts", ""),
                    FileName = metadata.GetValueOrDefault("file-name", file.Key),
                    UploadedAt = (file.LastModified ?? DateTime.UtcNow).ToLocalTime()
                });
            }

            return View(resumes.OrderByDescending(r => r.UploadedAt).ToList());
        }

        public async Task<IActionResult> Open(string key)
        {
            if (!key.StartsWith(ResumePrefix))
            {
                return BadRequest();
            }

            var url = await _s3Service.GetPresignedUrlAsync(key);
            return Redirect(url);
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string key)
        {
            if (!key.StartsWith(ResumePrefix))
            {
                return BadRequest();
            }

            await _s3Service.DeleteAsync(key);
            return RedirectToAction("Resumes");
        }
    }
}
