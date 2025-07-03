using Avalonia.Controls;
using Avalonia.Threading;
using DialogHostAvalonia;
using System;
using System.Threading.Tasks;
using DickHelper.ViewModels;

namespace DickHelper.Views;

public partial class MainView : UserControl
{
    private DispatcherTimer _timer;
    private TimeSpan _elapsed;
    private bool _started;

    public MainView()
    {
        InitializeComponent();
        _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        _timer.Tick += Timer_Tick;
        _elapsed = TimeSpan.Zero;
        _started = false;
        TimerText.Text = "准备开始";

        StartButton.Click += (s, e) =>
        {
            _timer.Start();
            _started = true;
            TimerText.Text = FormatTime(_elapsed);
            StartButton.IsVisible = false;
            PauseButton.IsVisible = true;
            StopButton.IsVisible = true;
        };

        PauseButton.Click += (s, e) =>
        {
            if (_timer.IsEnabled)
            {
                _timer.Stop();
                PauseButton.Content = "继续";
            }
            else
            {
                _timer.Start();
                PauseButton.Content = "暂停";
            }
        };

        StopButton.Click += async (s, e) =>
        {
            _timer.Stop();
            if (_elapsed.TotalSeconds > 0)
            {
                var recordData = await ShowRecordDialog();
                if (recordData != null)
                {
                    HistoryViewModel.Instance.AddRecord(DateTime.Now, _elapsed, recordData);
                }
            }
            ResetTimer();
        };

        PauseButton.IsVisible = false;
        StopButton.IsVisible = false;
    }

    private void Timer_Tick(object? sender, EventArgs e)
    {
        _elapsed = _elapsed.Add(TimeSpan.FromSeconds(1));
        TimerText.Text = FormatTime(_elapsed);
    }

    private static string FormatTime(TimeSpan ts)
        => ts.ToString(@"mm\:ss");

    private async Task<Models.RecordDetail?> ShowRecordDialog()
    {
        var dialogContent = new RecordDialogContent();


        var result = await DialogHost.Show(dialogContent);

        if (result is string parameter && parameter == "OK")
        {
            return CreateRecordDetail(dialogContent);
        }

        return null;
    }

    private static Models.RecordDetail CreateRecordDetail(dynamic dialog)
    {
        return new Models.RecordDetail
        {
            Remark = dialog.Remark,
            Location = dialog.Location,
            WatchedMovie = dialog.WatchedMovie,
            Climax = dialog.Climax,
            Tool = dialog.Tool,
            Score = dialog.Score,
            Mood = dialog.Mood
        };
    }

    private void ResetTimer()
    {
        _elapsed = TimeSpan.Zero;
        _started = false;
        TimerText.Text = "准备开始";
        StartButton.IsVisible = true;
        PauseButton.IsVisible = false;
        StopButton.IsVisible = false;
        PauseButton.Content = "暂停";
    }
}