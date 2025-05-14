using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using WallMod.ViewModels;

namespace WallMod.Views;

public partial class QueueView : UserControl
{
    public QueueView()
    {
        InitializeComponent();
        DataContext = new QueueViewModel();
    }
}