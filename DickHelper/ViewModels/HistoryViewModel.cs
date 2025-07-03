using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace DickHelper.ViewModels;

public partial class HistoryViewModel : ViewModelBase
{
    public void NotifyRecordsChanged()
    {
        OnPropertyChanged(nameof(Records));
    }
    private static HistoryViewModel? _instance;
    public static HistoryViewModel Instance => _instance ??= new HistoryViewModel();

    private static string GetHistoryFilePath()
    {
        string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        string dir = Path.Combine(baseDir, "DickHelper");
        Directory.CreateDirectory(dir);
        return Path.Combine(dir, "history.json");
    }

    private readonly string _dataFilePath = GetHistoryFilePath();

    public ObservableCollection<HistoryRecord> Records { get; } = new();

    [ObservableProperty]
    private HistoryRecord? _selectedRecord;

    private HistoryViewModel()
    {
        LoadHistoryAsync();
    }

    public void AddRecord(DateTime date, TimeSpan duration, DickHelper.Models.RecordDetail? detail = null)
    {
        Records.Insert(0, new HistoryRecord { Date = date, Duration = duration, Detail = detail });
        SaveHistoryAsync();
    }

    [RelayCommand]
    private void DeleteRecord()
    {
        if (SelectedRecord != null)
        {
            Records.Remove(SelectedRecord);
            SaveHistoryAsync();
        }
    }

    [RelayCommand]
    private void ClearAll()
    {
        Records.Clear();
        SaveHistoryAsync();
    }

    private async void LoadHistoryAsync()
    {
        try
        {
            if (File.Exists(_dataFilePath))
            {
                var json = await File.ReadAllTextAsync(_dataFilePath);
                var records = JsonSerializer.Deserialize<HistoryRecord[]>(json);
                if (records != null)
                {
                    foreach (var record in records)
                    {
                        Records.Add(record);
                    }
                }
            }
        }
        catch
        {

        }
    }

    public async void SaveHistoryAsync()
    {
        try
        {
            var directory = Path.GetDirectoryName(_dataFilePath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory!);
            }
            var json = JsonSerializer.Serialize(Records.ToArray());
            await File.WriteAllTextAsync(_dataFilePath, json);
        }
        catch
        {

        }
    }
}
