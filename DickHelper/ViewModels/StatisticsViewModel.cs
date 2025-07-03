using System;
using System.Collections.ObjectModel;
using System.Linq;

namespace DickHelper.ViewModels
{
    public class StatisticsViewModel : ViewModelBase
    {
        private readonly ObservableCollection<HistoryRecord> _records;

        public StatisticsViewModel(ObservableCollection<HistoryRecord> records)
        {
            _records = records;
            _records.CollectionChanged += (s, e) => OnPropertyChanged(string.Empty);
        }

        public int TotalCount => _records.Count;

        public string AverageDuration => _records.Count == 0 ? "--" :
            TimeSpan.FromSeconds(_records.Average(r => r.Duration.TotalSeconds)).ToString(@"hh\:mm\:ss");

        public int ThisWeekCount => _records.Count(r => IsInThisWeek(r.Date));

        public int ThisMonthCount => _records.Count(r => IsInThisMonth(r.Date));

        public string Summary => $"总次数：{TotalCount}\n平均持续时间：{AverageDuration}\n本周次数：{ThisWeekCount}\n本月次数：{ThisMonthCount}";

        private bool IsInThisWeek(DateTime date)
        {
            var now = DateTime.Now;
            var diff = (int)now.DayOfWeek;
            var weekStart = now.Date.AddDays(-diff);
            var weekEnd = weekStart.AddDays(7);
            return date >= weekStart && date < weekEnd;
        }

        private bool IsInThisMonth(DateTime date)
        {
            var now = DateTime.Now;
            return date.Year == now.Year && date.Month == now.Month;
        }
    }
}
