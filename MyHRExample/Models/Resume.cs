namespace MyHRExample.Models
{
    public class Resume
    {
        public string Key { get; set; } = "";
        public string Id => Path.GetFileNameWithoutExtension(Key);
        public string CandidateName { get; set; } = "";
        public string Contacts { get; set; } = "";
        public string FileName { get; set; } = "";
        public DateTime UploadedAt { get; set; }
        public Analysis? Analysis { get; set; }

        public string StatusText
        {
            get
            {
                if (Analysis == null) return "Очікує аналізу";
                if (Analysis.Status == "Completed") return "Проаналізовано";
                return "Помилка аналізу";
            }
        }

        public string StatusClass
        {
            get
            {
                if (Analysis == null) return "pending";
                if (Analysis.Status == "Completed") return "done";
                return "failed";
            }
        }
    }
}
