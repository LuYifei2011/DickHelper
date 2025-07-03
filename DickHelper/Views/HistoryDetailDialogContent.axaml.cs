using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;
using DickHelper.ViewModels;

namespace DickHelper.Views
{
    public partial class HistoryDetailDialogContent : UserControl
    {
        public HistoryDetailDialogContent()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public HistoryDetailDialogContent(HistoryRecord record)
        {
            AvaloniaXamlLoader.Load(this);
            InitializeComponent(record);
        }

        private void InitializeComponent(HistoryRecord record)
        {
            this.FindControl<TextBlock>("DateText")!.Text = record.DisplayDate;
            this.FindControl<TextBlock>("DurationText")!.Text = record.DisplayDuration;
            this.FindControl<TextBlock>("RemarkText")!.Text = record.DisplayRemark;
            this.FindControl<TextBlock>("LocationText")!.Text = record.DisplayLocation;
            this.FindControl<TextBlock>("ToolText")!.Text = record.DisplayTool;
            this.FindControl<TextBlock>("ScoreText")!.Text = record.DisplayScore;
            this.FindControl<TextBlock>("MoodText")!.Text = record.DisplayMood;
            this.FindControl<TextBlock>("MovieText")!.Text = string.IsNullOrWhiteSpace(record.DisplayWatchedMovie) ? "否" : "是";
            this.FindControl<TextBlock>("ClimaxText")!.Text = string.IsNullOrWhiteSpace(record.DisplayClimax) ? "否" : "是";

            var okButton = this.FindControl<Button>("OkButton");
            if (okButton != null)
            {
                okButton.Click += (s, e) => DialogHost.Close(null);
            }
        }
    }
}
