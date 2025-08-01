using System;
using System.Text.Json.Serialization;

namespace DickHelper.ViewModels
{
    public class HistoryRecord
    {
        public DateTime Date { get; set; }
        public TimeSpan Duration { get; set; }
        public DickHelper.Models.RecordDetail? Detail { get; set; }

        [JsonIgnore]
        public string DisplayDate => $"{Date:yyyy-MM-dd HH:mm:ss}";
        [JsonIgnore]
        public string DisplayDuration => $"用时: {Duration:hh\\:mm\\:ss}";
        [JsonIgnore]
        public string DisplayRemark => Detail?.Remark ?? string.Empty;
        [JsonIgnore]
        public string DisplayLocation => Detail?.Location ?? string.Empty;
        [JsonIgnore]
        public string DisplayTool => Detail?.Tool ?? string.Empty;
        [JsonIgnore]
        public string DisplayScore => Detail != null ? $"评分: {Detail.Score:F1}/5.0分" : string.Empty;
        [JsonIgnore]
        public string DisplayMood => Detail?.Mood ?? string.Empty;
        [JsonIgnore]
        public string DisplayWatchedMovie => Detail?.WatchedMovie == true ? "观看小电影" : string.Empty;
        [JsonIgnore]
        public string DisplayClimax => Detail?.Climax == true ? "高潮" : string.Empty;
    }
}
