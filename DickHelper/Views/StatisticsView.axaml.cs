using Avalonia.Controls;
using Avalonia.Styling;
using DickHelper.ViewModels;
using ScottPlot;
using System;
using System.Linq;

namespace DickHelper.Views
{
    public partial class StatisticsView : UserControl
    {
        private StatisticsViewModel? _viewModel;

        public StatisticsView()
        {
            InitializeComponent();
            _viewModel = new StatisticsViewModel(HistoryViewModel.Instance.Records);
            DataContext = _viewModel;

            // 监听数据变化以更新图表
            HistoryViewModel.Instance.Records.CollectionChanged += (s, e) => UpdateChart();

            // 初始化图表
            UpdateChart();
        }

        private void UpdateChart()
        {
            var records = HistoryViewModel.Instance.Records.OrderBy(r => r.Date).ToList();

            if (records.Count == 0)
            {
                DurationChart.Plot.Clear();
                DurationChart.Plot.Add.Text("暂无数据", 0.5, 0.5);
                DurationChart.Refresh();
                MonthCountChart.Plot.Clear();
                MonthCountChart.Plot.Add.Text("暂无数据", 0.5, 0.5);
                MonthCountChart.Refresh();
                WeekCountChart.Plot.Clear();
                WeekCountChart.Plot.Add.Text("暂无数据", 0.5, 0.5);
                WeekCountChart.Refresh();
                return;
            }

            if (this.ActualThemeVariant == ThemeVariant.Dark)
            {
                // 如果是暗黑主题，设置图表样式
                var darkStyle = new ScottPlot.PlotStyles.Dark();
                DurationChart.Plot.SetStyle(darkStyle);
                MonthCountChart.Plot.SetStyle(darkStyle);
                WeekCountChart.Plot.SetStyle(darkStyle);
            }

            // 清除之前的数据
            DurationChart.Plot.Clear();
            MonthCountChart.Plot.Clear();
            WeekCountChart.Plot.Clear();

            // 准备数据
            var dates = records.Select(r => r.Date.ToOADate()).ToArray();
            var durations = records.Select(r => r.Duration.TotalMinutes).ToArray();

            // 添加折线图
            var line = DurationChart.Plot.Add.Scatter(dates, durations);
            line.LineWidth = 2;
            line.Color = new ScottPlot.Color(0, 80, 255);
            line.MarkerSize = 5;

            // 设置坐标轴
            DurationChart.Plot.Axes.DateTimeTicksBottom();
            DurationChart.Plot.Axes.Bottom.Label.Text = "日期";
            DurationChart.Plot.Axes.Left.Label.Text = "持续时间 (分钟)";
            DurationChart.Plot.Title("持续时间趋势图");

            // 自动调整坐标轴范围
            DurationChart.Plot.Axes.AutoScale();

            // 解决中文显示问题
            DurationChart.Plot.Font.Automatic();

            // 刷新图表
            DurationChart.Refresh();

            // 月次数条形图
            var monthGroups = records
                .GroupBy(r => new { r.Date.Year, r.Date.Month })
                .OrderBy(g => g.Key.Year).ThenBy(g => g.Key.Month)
                .ToList();
            var monthLabels = monthGroups.Select(g => $"{g.Key.Year}-{g.Key.Month:D2}").ToArray();
            var monthCounts = monthGroups.Select(g => (double)g.Count()).ToArray();
            double[] monthPositions = Enumerable.Range(0, monthCounts.Length).Select(i => (double)i).ToArray();
            var monthBars = MonthCountChart.Plot.Add.Bars(monthPositions, monthCounts);
            monthBars.Color = new ScottPlot.Color(0, 120, 255);
            MonthCountChart.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(monthPositions, monthLabels);
            MonthCountChart.Plot.Axes.Bottom.Label.Text = "月份";
            MonthCountChart.Plot.Axes.Left.Label.Text = "次数";
            MonthCountChart.Plot.Title("每月次数统计");
            MonthCountChart.Plot.Axes.SetLimitsY(0, monthCounts.Max() * 1.1);
            MonthCountChart.Plot.Font.Automatic();
            MonthCountChart.Refresh();

            // 周次数条形图
            var weekGroups = records
                .GroupBy(r => System.Globalization.ISOWeek.GetWeekOfYear(r.Date))
                .OrderBy(g => g.Key)
                .ToList();
            var weekLabels = weekGroups.Select(g => g.Key.ToString()).ToArray();
            var weekCounts = weekGroups.Select(g => (double)g.Count()).ToArray();
            double[] weekPositions = Enumerable.Range(0, weekCounts.Length).Select(i => (double)i).ToArray();
            var weekBars = WeekCountChart.Plot.Add.Bars(weekPositions, weekCounts);
            weekBars.Color = new ScottPlot.Color(255, 180, 0);
            WeekCountChart.Plot.Axes.Bottom.TickGenerator = new ScottPlot.TickGenerators.NumericManual(weekPositions, weekLabels);
            WeekCountChart.Plot.Axes.Bottom.Label.Text = "周";
            WeekCountChart.Plot.Axes.Left.Label.Text = "次数";
            WeekCountChart.Plot.Title("每周次数统计");
            WeekCountChart.Plot.Axes.SetLimitsY(0, weekCounts.Max() * 1.1);
            WeekCountChart.Plot.Font.Automatic();
            WeekCountChart.Refresh();
        }
    }
}
