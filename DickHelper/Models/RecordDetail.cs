namespace DickHelper.Models
{
    public class RecordDetail
    {
        public string Remark { get; set; } = string.Empty;
        public string Location { get; set; } = string.Empty;
        public bool WatchedMovie { get; set; }
        public bool Climax { get; set; }
        public string Tool { get; set; } = "手";
        public double Score { get; set; } = 3.0;
        public string Mood { get; set; } = "平静";
    }
}
