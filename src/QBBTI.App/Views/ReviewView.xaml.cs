using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using QBBTI.App.ViewModels;

namespace QBBTI.App.Views;

public partial class ReviewView : UserControl
{
    private static readonly Thickness NormalBorderThickness = new(1);
    private static readonly Thickness HighlightBorderThickness = new(2);
    private static readonly Brush HighlightBorderBrush = new SolidColorBrush(Color.FromRgb(0x40, 0x80, 0xFF));

    public ReviewView()
    {
        InitializeComponent();
    }

    private void GripHandle_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element) return;
        var groupVm = element.DataContext as TransactionGroupViewModel;
        if (groupVm == null) return;

        DragDrop.DoDragDrop(element, groupVm, DragDropEffects.Move);
        e.Handled = true;
    }

    private void GroupBorder_DragOver(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TransactionGroupViewModel)))
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        var source = e.Data.GetData(typeof(TransactionGroupViewModel)) as TransactionGroupViewModel;
        var target = (sender as FrameworkElement)?.DataContext as TransactionGroupViewModel;

        e.Effects = (source != null && target != null && source != target)
            ? DragDropEffects.Move
            : DragDropEffects.None;

        e.Handled = true;
    }

    private void GroupBorder_Drop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TransactionGroupViewModel))) return;

        var source = e.Data.GetData(typeof(TransactionGroupViewModel)) as TransactionGroupViewModel;
        var target = (sender as FrameworkElement)?.DataContext as TransactionGroupViewModel;

        if (source == null || target == null || source == target) return;

        var reviewVm = DataContext as ReviewViewModel;
        reviewVm?.MergeGroups(source, target);

        // Reset highlight
        if (sender is Border border)
        {
            border.BorderThickness = NormalBorderThickness;
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
        }

        e.Handled = true;
    }

    private void GroupBorder_DragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(typeof(TransactionGroupViewModel))) return;

        var source = e.Data.GetData(typeof(TransactionGroupViewModel)) as TransactionGroupViewModel;
        var target = (sender as FrameworkElement)?.DataContext as TransactionGroupViewModel;

        if (source != null && target != null && source != target && sender is Border border)
        {
            border.BorderThickness = HighlightBorderThickness;
            border.BorderBrush = HighlightBorderBrush;
        }
    }

    private void GroupBorder_DragLeave(object sender, DragEventArgs e)
    {
        if (sender is Border border)
        {
            border.BorderThickness = NormalBorderThickness;
            border.BorderBrush = new SolidColorBrush(Color.FromRgb(0xDD, 0xDD, 0xDD));
        }
    }
}
