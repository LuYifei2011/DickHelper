using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using OpenAI;
using OpenAI.Chat;
using OpenAI.Models;

namespace DickHelper.Services;

public class OpenAIService
{
    private readonly OpenAIClient _client;
    private readonly string _model;

    public OpenAIService()
    {
        var configuration = LoadConfiguration();
        var apiKey = configuration["OpenAI:ApiKey"];
        var baseUrl = configuration["OpenAI:BaseUrl"];
        _model = configuration["OpenAI:Model"] ?? "gpt-3.5-turbo";

        if (string.IsNullOrEmpty(apiKey) || apiKey == "your-openai-api-key-here")
        {
            throw new InvalidOperationException("请在appsettings.json中配置有效的OpenAI API密钥");
        }

        var options = new OpenAIClientOptions();
        if (!string.IsNullOrEmpty(baseUrl))
        {
            options.Endpoint = new Uri(baseUrl);
        }

        _client = new OpenAIClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
    }

    private IConfiguration LoadConfiguration()
    {
        // 运行时判断平台
        if (OperatingSystem.IsAndroid())
        {
            try
            {
                // 反射获取 Android.App.Application.Context
                var appType = Type.GetType("Android.App.Application, Mono.Android");
                var contextProp = appType?.GetProperty("Context");
                var context = contextProp?.GetValue(null);
                var assetsProp = context?.GetType().GetProperty("Assets");
                var assets = assetsProp?.GetValue(context);
                var openMethod = assets?.GetType().GetMethod("Open", new[] { typeof(string) });
                var streamObj = openMethod?.Invoke(assets, new object[] { "appsettings.json" });
                if (streamObj is not Stream stream)
                    throw new InvalidOperationException("无法从Android Assets读取appsettings.json");
                using (stream)
                using (var reader = new StreamReader(stream))
                {
                    var json = reader.ReadToEnd();
                    var dict = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
                    if (dict == null || !dict.ContainsKey("OpenAI"))
                        throw new InvalidOperationException("appsettings.json 缺少 OpenAI 配置段");
                    var openAiSection = dict["OpenAI"];
                    var configBuilder = new ConfigurationBuilder();
                    configBuilder.AddInMemoryCollection(new[]
                    {
                        new KeyValuePair<string, string?>("OpenAI:ApiKey", openAiSection.ContainsKey("ApiKey") ? openAiSection["ApiKey"] : null),
                        new KeyValuePair<string, string?>("OpenAI:BaseUrl", openAiSection.ContainsKey("BaseUrl") ? openAiSection["BaseUrl"] : null),
                        new KeyValuePair<string, string?>("OpenAI:Model", openAiSection.ContainsKey("Model") ? openAiSection["Model"] : null)
                    });
                    return configBuilder.Build();
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"读取Android appsettings.json失败: {ex.Message}");
            }
        }
        else
        {
            var configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appsettings.json");
            if (!File.Exists(configPath))
            {
                // 创建默认配置文件
                var defaultConfig = """
                {
                  "OpenAI": {
                    "ApiKey": "sk-W0rpStc95T7JVYVwDYc29IyirjtpPPby6SozFMQr17m8KWeo",
                    "BaseUrl": "https://api.suanli.cn/v1",
                    "Model": "free:QwQ-32B"
                  }
                }
                """;
                File.WriteAllText(configPath, defaultConfig, Encoding.UTF8);
            }
            var builder = new ConfigurationBuilder()
                .SetBasePath(AppDomain.CurrentDomain.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
            return builder.Build();
        }
    }

    public async Task<string> AnalyzeHistoryRecordsAsync(IEnumerable<ViewModels.HistoryRecord> records)
    {
        try
        {
            var prompt = BuildAnalysisPrompt(records);
            
            var messages = new List<ChatMessage>
            {
                new SystemChatMessage("你是一个专业的数据分析师，专门分析个人行为模式和习惯。请基于提供的历史记录数据，提供深入、有用的分析和建议。分析应该包括时间模式、频率分析、趋势识别和健康建议。"),
                new UserChatMessage(prompt)
            };

            var chatRequest = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 1500,
                Temperature = 0.7f
            };

            var response = await _client.GetChatClient(_model).CompleteChatAsync(messages, chatRequest);
            
            return response.Value.Content[0].Text;
        }
        catch (Exception ex)
        {
            return $"AI分析失败: {ex.Message}";
        }
    }

    public async IAsyncEnumerable<string> AnalyzeHistoryRecordsStreamAsync(IEnumerable<ViewModels.HistoryRecord> records, string? model = null)
    {
        var prompt = BuildAnalysisPrompt(records);
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage("你是一个专业的数据分析师，专门分析个人行为模式和习惯。请基于提供的历史记录数据，提供深入、有用的分析和建议。分析应该包括时间模式、频率分析、趋势识别和健康建议。"),
            new UserChatMessage(prompt)
        };
        var chatRequest = new ChatCompletionOptions
        {
            MaxOutputTokenCount = 1500,
            Temperature = 0.7f
        };
        var useModel = string.IsNullOrWhiteSpace(model) ? _model : model;
        var completionUpdates = _client.GetChatClient(useModel).CompleteChatStreamingAsync(messages, chatRequest);
        await foreach (var update in completionUpdates)
        {
            if (update.ContentUpdate != null && update.ContentUpdate.Count > 0)
            {
                foreach (var part in update.ContentUpdate)
                {
                    if (!string.IsNullOrEmpty(part.Text))
                        yield return part.Text;
                }
            }
        }
    }

    private string BuildAnalysisPrompt(IEnumerable<ViewModels.HistoryRecord> records)
    {
        var sb = new StringBuilder();
        sb.AppendLine("请分析以下历史记录数据：");
        sb.AppendLine();

        var recordList = records.ToList();
        if (!recordList.Any())
        {
            return "暂无历史记录数据可分析";
        }

        sb.AppendLine($"总记录数: {recordList.Count}");
        sb.AppendLine();
        sb.AppendLine("详细记录:");

        foreach (var record in recordList.Take(50)) // 限制数量以避免超出token限制
        {
            sb.AppendLine($"- 日期: {record.Date:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"  持续时间: {record.Duration:hh\\:mm\\:ss}");
            
            if (record.Detail != null)
            {
                if (record.Detail.Score > 0)
                    sb.AppendLine($"  评分: {record.Detail.Score:F1}/5.0");
                if (!string.IsNullOrEmpty(record.Detail.Mood))
                    sb.AppendLine($"  心情: {record.Detail.Mood}");
                if (!string.IsNullOrEmpty(record.Detail.Location))
                    sb.AppendLine($"  地点: {record.Detail.Location}");
                if (!string.IsNullOrEmpty(record.Detail.Tool))
                    sb.AppendLine($"  工具: {record.Detail.Tool}");
                if (!string.IsNullOrEmpty(record.Detail.Remark))
                    sb.AppendLine($"  备注: {record.Detail.Remark}");
                if (record.Detail.WatchedMovie)
                    sb.AppendLine($"  观看小电影: 是");
                if (record.Detail.Climax)
                    sb.AppendLine($"  高潮: 是");
            }
            sb.AppendLine();
        }

        sb.AppendLine();
        sb.AppendLine("请提供以下方面的分析:");
        sb.AppendLine("1. 时间模式分析（一天中的活跃时段、星期几的偏好等）");
        sb.AppendLine("2. 频率和持续时间分析");
        sb.AppendLine("3. 评分和心情趋势分析（如果有数据）");
        sb.AppendLine("4. 习惯模式识别");
        sb.AppendLine("5. 健康和生活方式建议");
        sb.AppendLine("6. 其他有价值的洞察");
        sb.AppendLine();
        sb.AppendLine("请使用友好、专业的语调，避免过于直白，并提供实用的建议。");

        return sb.ToString();
    }

    public async Task<List<string>> GetAvailableModelsAsync()
    {
        var models = new List<string>();
        try
        {
            var configuration = LoadConfiguration();
            var apiKey = configuration["OpenAI:ApiKey"];
            var baseUrl = configuration["OpenAI:BaseUrl"];
            if (string.IsNullOrEmpty(apiKey))
                throw new InvalidOperationException("OpenAI:ApiKey 未配置");
            var options = new OpenAIClientOptions();
            if (!string.IsNullOrEmpty(baseUrl))
            {
                options.Endpoint = new Uri(baseUrl);
            }
            var modelClient = new OpenAIModelClient(new System.ClientModel.ApiKeyCredential(apiKey), options);
            var modelList = await modelClient.GetModelsAsync();
            foreach (var model in modelList.Value)
            {
                if (!string.IsNullOrEmpty(model.Id))
                    models.Add(model.Id);
            }
        }
        catch (Exception ex)
        {
            models.Add($"[获取模型失败: {ex.Message}]");
        }
        return models;
    }
}
