using Microsoft.AspNetCore.Mvc;
using MyHRExample.Services;

namespace MyHRExample.Controllers
{
    public class CandidateController : Controller
    {
        private readonly S3Service _s3Service;

        public CandidateController(S3Service s3Service)
        {
            _s3Service = s3Service;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Index(string name, string contacts, IFormFile? resume)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(contacts) || resume == null)
            {
                ViewBag.Error = "Заповніть усі поля та додайте резюме.";
                return View();
            }

            if (Path.GetExtension(resume.FileName).ToLower() != ".pdf" || !await IsPdfAsync(resume))
            {
                ViewBag.Error = "Резюме має бути у форматі PDF.";
                return View();
            }

            var key = $"resumes/{Guid.NewGuid()}.pdf";
            var metadata = new Dictionary<string, string>
            {
                ["candidate-name"] = name,
                ["contacts"] = contacts,
                ["file-name"] = resume.FileName
            };

            using var stream = resume.OpenReadStream();
            await _s3Service.UploadAsync(stream, key, "application/pdf", metadata);

            ViewBag.Success = true;
            return View();
        }

        private static async Task<bool> IsPdfAsync(IFormFile file)
        {
            var header = new byte[4];
            using var stream = file.OpenReadStream();
            var read = await stream.ReadAsync(header);
            return read == 4 && header[0] == '%' && header[1] == 'P' && header[2] == 'D' && header[3] == 'F';
        }
    }
}
