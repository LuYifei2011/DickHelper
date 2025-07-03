using Avalonia.Controls;
using Avalonia.VisualTree;
using DialogHostAvalonia;
using DickHelper.ViewModels;

namespace DickHelper.Views;

public partial class HistoryView : UserControl
{
    public HistoryView()
    {
        InitializeComponent();
        DataContext = HistoryViewModel.Instance;

        var listBox = this.FindControl<ListBox>("RecordsListBox");
        if (listBox != null)
        {
            listBox.DoubleTapped += async (s, e) =>
            {
                if (listBox.SelectedItem is DickHelper.ViewModels.HistoryRecord record)
                {
                    var dialogContent = new HistoryDetailDialogContent(record);

                    // 查找父级窗口中的DialogHost
                    var window = this.FindAncestorOfType<Window>();
                    var dialogHost = window?.FindControl<DialogHost>("MainDialogHost");

                    if (dialogHost != null)
                    {
                        await DialogHost.Show(dialogContent, "MainDialogHost");
                    }
                    else
                    {
                        // 如果找不到指定的DialogHost，使用默认方式
                        await DialogHost.Show(dialogContent);
                    }
                }
            };
        }
    }
}
