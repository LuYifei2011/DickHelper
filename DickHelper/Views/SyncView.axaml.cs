using Avalonia.Controls;
using DickHelper.ViewModels;

namespace DickHelper.Views
{
    public partial class SyncView : UserControl
    {
        public SyncView()
        {
            InitializeComponent();
            DataContext = new SyncViewModel();
        }
    }
}
