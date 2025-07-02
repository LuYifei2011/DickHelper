using System;

namespace DickHelper.ViewModels
{
    public class HistoryRecord
    {
        public DateTime Date { get; set; }
        public TimeSpan Duration { get; set; }
        public DickHelper.Models.RecordDetail? Detail { get; set; }
        public string DisplayDate => $"{Date:yyyy-MM-dd HH:mm:ss}";
        public string DisplayDuration => $"用时: {Duration:hh\\:mm\\:ss}";
        public string DisplayRemark => Detail?.Remark ?? string.Empty;
        public string DisplayLocation => Detail?.Location ?? string.Empty;
        public string DisplayTool => Detail?.Tool ?? string.Empty;
        public string DisplayScore => Detail != null ? $"评分: {Detail.Score:F1}/5.0分" : string.Empty;
        public string DisplayMood => Detail?.Mood ?? string.Empty;
        public string DisplayWatchedMovie => Detail?.WatchedMovie == true ? "观看小电影" : string.Empty;
        public string DisplayClimax => Detail?.Climax == true ? "高潮" : string.Empty;
    }
}
