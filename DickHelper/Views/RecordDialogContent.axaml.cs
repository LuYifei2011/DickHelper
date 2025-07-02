using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using DialogHostAvalonia;

namespace DickHelper.Views
{
    public partial class RecordDialogContent : UserControl
    {
        public string Remark => this.FindControl<TextBox>("RemarkBox")?.Text ?? "";
        public string Location => this.FindControl<TextBox>("LocationBox")?.Text ?? "";
        public bool WatchedMovie => this.FindControl<CheckBox>("MovieBox")?.IsChecked ?? false;
        public bool Climax => this.FindControl<CheckBox>("ClimaxBox")?.IsChecked ?? false;
        public string Tool
        {
            get
            {
                if (this.FindControl<RadioButton>("HandRadio")?.IsChecked == true) return "手";
                if (this.FindControl<RadioButton>("CupRadio")?.IsChecked == true) return "飞机杯";
                if (this.FindControl<RadioButton>("DollRadio")?.IsChecked == true) return "娃娃";
                return "手";
            }
        }
        public double Score => this.FindControl<Slider>("ScoreSlider")?.Value ?? 3.0;
        public string Mood
        {
            get
            {
                if (this.FindControl<RadioButton>("MoodCalm")?.IsChecked == true) return "平静";
                if (this.FindControl<RadioButton>("MoodHappy")?.IsChecked == true) return "愉悦";
                if (this.FindControl<RadioButton>("MoodExcited")?.IsChecked == true) return "兴奋";
                if (this.FindControl<RadioButton>("MoodTired")?.IsChecked == true) return "疲惫";
                if (this.FindControl<RadioButton>("MoodLast")?.IsChecked == true) return "这是最后一次！";
                return "平静";
            }
        }

        public RecordDialogContent()
        {
            AvaloniaXamlLoader.Load(this);
            InitializeComponent();
        }

        private void InitializeComponent()
        {
            var slider = this.FindControl<Slider>("ScoreSlider");
            var scoreText = this.FindControl<TextBlock>("ScoreText");
            
            if (slider != null && scoreText != null)
            {
                slider.PropertyChanged += (s, e) =>
                {
                    if (e.Property.Name == "Value")
                    {
                        scoreText.Text = $"{slider.Value:F1}/5.0分";
                    }
                };
            }

            var okButton = this.FindControl<Button>("OkButton");
            var cancelButton = this.FindControl<Button>("CancelButton");
            
            if (okButton != null)
            {
                okButton.Click += (s, e) => DialogHost.Close(null, "OK");
            }
            
            if (cancelButton != null)
            {
                cancelButton.Click += (s, e) => DialogHost.Close(null, "CANCEL");
            }
        }
    }
}
