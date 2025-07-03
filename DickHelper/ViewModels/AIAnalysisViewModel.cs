using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DickHelper.Services;

namespace DickHelper.ViewModels;

public partial class AIAnalysisViewModel : ViewModelBase
{
    private readonly OpenAIService? _openAIService;

    [ObservableProperty]
    private string _analysisResult = string.Empty;

    [ObservableProperty]
    private bool _isAnalyzing = false;

    [ObservableProperty]
    private string _statusMessage = "点击'开始分析'按钮来获取AI分析报告";

    [ObservableProperty]
    private bool _hasError = false;

    // 计算属性：是否可以开始分析
    public bool CanStartAnalysis => !IsAnalyzing && !HasError && _openAIService != null;

    partial void OnIsAnalyzingChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartAnalysis));
    }

    partial void OnHasErrorChanged(bool value)
    {
        OnPropertyChanged(nameof(CanStartAnalysis));
    }

    public AIAnalysisViewModel()
    {
        ModelList.CollectionChanged += (s, e) => OnPropertyChanged(nameof(IsModelListEmpty));
        try
        {
            _openAIService = new OpenAIService();
            StatusMessage = "AI服务已就绪，正在获取模型列表...";
            HasError = false;
            _ = LoadModelListAsync();
        }
        catch (Exception ex)
        {
            StatusMessage = $"AI服务初始化失败: {ex.Message}";
            HasError = true;
            // 确保不处于分析状态
            IsAnalyzing = false;
        }
        // 通知CanStartAnalysis属性变更
        OnPropertyChanged(nameof(CanStartAnalysis));
    }

    private async Task LoadModelListAsync()
    {
        if (_openAIService == null) return;
        ModelList.Clear();
        var models = await _openAIService.GetAvailableModelsAsync();
        foreach (var m in models)
            ModelList.Add(m);
        // 默认选中第一个可用模型
        if (ModelList.Count > 0)
            SelectedModel = ModelList[0];
        StatusMessage = "AI服务已就绪，点击'开始分析'按钮来获取分析报告";
    }

    [RelayCommand]
    private async Task StartAnalysis()
    {
        if (_openAIService == null || HasError)
        {
            AnalysisResult = "AI服务未正确初始化，请检查配置文件和网络连接";
            StatusMessage = "服务初始化失败";
            return;
        }
        if (string.IsNullOrWhiteSpace(SelectedModel))
        {
            AnalysisResult = "请先选择模型";
            StatusMessage = "未选择模型";
            return;
        }

        IsAnalyzing = true;
        AnalysisResult = string.Empty;
        StatusMessage = "正在连接AI服务进行智能分析...";

        try
        {
            var records = HistoryViewModel.Instance.Records;
            if (!records.Any())
            {
                AnalysisResult = "暂无历史记录可分析，请先添加一些记录后再进行分析。";
                StatusMessage = "分析完成";
                return;
            }

            StatusMessage = "AI正在深度分析您的历史记录，请稍候...";
            AnalysisResult = "=== AI 智能分析报告 ===\n\n";
            await foreach (var chunk in _openAIService.AnalyzeHistoryRecordsStreamAsync(records, SelectedModel))
            {
                if (!string.IsNullOrEmpty(chunk))
                {
                    AnalysisResult += chunk;
                }
            }
            // 分析完成后追加基础统计
            var basicStats = GenerateBasicStats(records);
            AnalysisResult += "\n\n" + basicStats;
            StatusMessage = "AI分析完成！";
        }
        catch (Exception ex)
        {
            AnalysisResult = $"AI分析失败: {ex.Message}\n\n请检查:\n• 网络连接是否正常\n• API密钥是否正确配置\n• API服务是否可用";
            StatusMessage = "分析失败，请检查网络和配置";
        }
        finally
        {
            IsAnalyzing = false;
        }
    }

    private string GenerateBasicStats(IEnumerable<HistoryRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== 基础统计信息 ===\n");

        var recordList = records.ToList();
        var totalCount = recordList.Count;
        var totalDuration = TimeSpan.FromTicks(recordList.Sum(r => r.Duration.Ticks));
        var avgDuration = TimeSpan.FromTicks((long)(recordList.Average(r => r.Duration.Ticks)));

        sb.AppendLine($"📊 总次数: {totalCount}\n");
        sb.AppendLine($"⏱️ 总时长: {totalDuration:hh\\:mm\\:ss}\n");
        sb.AppendLine($"📈 平均时长: {avgDuration:hh\\:mm\\:ss}\n");

        // 最近30天的统计
        var recentRecords = recordList.Where(r => r.Date >= DateTime.Now.AddDays(-30)).ToList();
        if (recentRecords.Any())
        {
            sb.AppendLine($"📅 最近30天: {recentRecords.Count}次\n");
        }

        // 评分统计（如果有）
        var scoredRecords = recordList.Where(r => r.Detail?.Score > 0).ToList();
        if (scoredRecords.Any())
        {
            var avgScore = scoredRecords.Average(r => r.Detail!.Score);
            sb.AppendLine($"⭐ 平均评分: {avgScore:F1}/5.0\n");
        }

        return sb.ToString();
    }

    public bool IsModelListEmpty => ModelList.Count == 0;

    public ObservableCollection<string> ModelList { get; } = new();

    [ObservableProperty]
    private string? _selectedModel;
}
