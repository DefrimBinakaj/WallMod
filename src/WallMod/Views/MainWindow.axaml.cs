using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Controls.Presenters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using WallMod.Helpers;
using WallMod.Models;
using WallMod.State;
using WallMod.ViewModels;

namespace WallMod.Views;

/**
 * Code behind for main application functionality
 * 
 * NOTE: messy code for dragging rect functionality
 */
public partial class MainWindow : Window
{

    private readonly UniversalAppStore uniVM = App.Services!.GetRequiredService<UniversalAppStore>();

    private enum RectOperation { None, Dragging, Resizing }
    private bool _isPointerDown;
    private RectOperation _operation = RectOperation.None;

    private Avalonia.Point _dragStart;
    private double _rectStartX, _rectStartY;
    private double _rectStartWidth, _rectStartHeight;
    private double _aspectRatio = 1.0;
    private const double CornerHitSize = 16;

    private DateTime lastTapTime = DateTime.MinValue;
    private int DoubleTapThreshold = 500;
    public Wallpaper lastTapImage = new();

    public MainWindow(MainWindowViewModel vm)
    {
        InitializeComponent();

        DataContext = vm;

        this.SizeChanged += OnWindowSizeChanged;
    }


    private void OnWindowSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        ResetRectangle();
        UnselectAllPreviewMonitors();
    }


    // gallery section ---------------------------------------------------------
    // when an image is tapped, reset opacity of all other items (folders)
    public void ResetAllItemsOpacity()
    {
        // 1) Find the ItemsControl
        var itemsControl = this.FindControl<ItemsControl>("ImageViewControl");
        if (itemsControl == null) return;

        // 2) Find the generated WrapPanel in the visual tree
        var wrapPanel = itemsControl.GetVisualDescendants()
                                    .OfType<WrapPanel>()
                                    .FirstOrDefault();
        if (wrapPanel == null) return;

        // 3) Each item is typically hosted in a ContentPresenter or direct StackPanel child
        foreach (var child in wrapPanel.GetVisualChildren())
        {
            // child might be a ContentPresenter
            if (child is ContentPresenter cp)
            {
                // The actual StackPanel is inside the content presenter
                var sp = cp.GetVisualDescendants().OfType<StackPanel>().FirstOrDefault();
                if (sp != null) sp.Opacity = 1.0;
            }
            else if (child is StackPanel sp2)
            {
                // If the item is a direct StackPanel
                sp2.Opacity = 1.0;
            }
        }
    }

    public async void OnImageTapped(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.DataContext is Wallpaper wallpaper)
        {
            if (wallpaper.IsDirectory == true)
            {
                ResetAllItemsOpacity();
                control.Opacity = 0.7;
            }
            await HandleImageTapped(wallpaper);
        }
    }

    public async Task HandleImageTapped(Wallpaper wallpaper)
    {
        // if user clicked a diff wallpaper than before, reset rect and disable set button
        // (dont reset if All Monitors is selected)
        if (wallpaper != lastTapImage && SelectOneToggle.IsChecked == true)
        {
            ResetRectangle();
            uniVM.SetBackgroundButtonEnabled = false;
            UnselectAllPreviewMonitors();
        }

        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel != null)
        {
            if (CheckDoubleTapped(wallpaper))
            {
                lastTapTime = DateTime.Now;
                lastTapImage = wallpaper;
                await viewModel.ImageDoubleTapped(wallpaper);
            }
            else
            {
                lastTapTime = DateTime.Now;
                lastTapImage = wallpaper;
                await viewModel.ImageTapped(wallpaper);
            }
        }
    }

    private bool CheckDoubleTapped(Wallpaper tappedWallpaper)
    {
        var currentTime = DateTime.Now;
        var elapsed = (currentTime - lastTapTime).TotalMilliseconds;

        if (elapsed < DoubleTapThreshold && lastTapImage == tappedWallpaper)
        {
            return true;
        }
        else
        {
            lastTapTime = currentTime;
            return false;
        }
    }

    // jump to top / bottom (buttons)
    public void JumpToTopClicked(object? sender, RoutedEventArgs e)
    {
        ImageGalleryScrollView.ScrollToHome();
    }
    public void JumpToBottomClicked(object? sender, RoutedEventArgs e)
    {
        ImageGalleryScrollView.ScrollToEnd();
    }

    // scrolling while hovering on thumbnail-size slider
    private void OnZoomSliderScroll(object? sender, PointerWheelEventArgs e)
    {
        if (DataContext is not MainWindowViewModel vm) return;

        // scroll up = zoom in ; scroll down = zoom out
        if (e.Delta.Y > 0) vm.HotkeyPlus();
        else if (e.Delta.Y < 0) vm.HotkeyMinus();

        e.Handled = true; // stop the scroll from bubbling to the gallery scrollviewer
    }


    // HOTKEYS --------------------

    // jump hotkeys
    public void HotkeyG()
    {
        ImageGalleryScrollView.ScrollToHome();
    }
    public void HotkeyShiftG()
    {
        ImageGalleryScrollView.ScrollToEnd();
    }


    // horizontally shift vertical splitter hotkeys
    public void HotkeySqBraceLeft()
    {
        double shiftAmount = 10;

        ColumnDefinition leftCol = MainGrid.ColumnDefinitions[0];
        ColumnDefinition rightCol = MainGrid.ColumnDefinitions[2];


        double leftColPixels = leftCol.ActualWidth;
        double rightColPixels = rightCol.ActualWidth;
        double totalPixels = leftColPixels + rightColPixels;


        double newLeftPixels = leftColPixels - shiftAmount; // shift
        double newRightPixels = rightColPixels + shiftAmount; // shift

        // enforce mid width
        if (newLeftPixels < leftCol.MinWidth)
            newLeftPixels = leftCol.MinWidth;
        if (newRightPixels < rightCol.MinWidth)
            newRightPixels = rightCol.MinWidth;


        double clampedTotalPixels = newLeftPixels + newRightPixels;
        // derive new star values based on the new pixel ratio
        double totalStars = leftCol.Width.Value + rightCol.Width.Value;
        double newLeftColStars = (newLeftPixels / clampedTotalPixels) * totalStars;
        double newRightColStars = totalStars - newLeftColStars;


        // apply calcd values to UI
        leftCol.Width = new GridLength(newLeftColStars, GridUnitType.Star);
        rightCol.Width = new GridLength(newRightColStars, GridUnitType.Star);

    }
    public void HotkeySqBraceRight()
    {
        double shiftAmount = -10;

        ColumnDefinition leftCol = MainGrid.ColumnDefinitions[0];
        ColumnDefinition rightCol = MainGrid.ColumnDefinitions[2];


        double leftColPixels = leftCol.ActualWidth;
        double rightColPixels = rightCol.ActualWidth;
        double totalPixels = leftColPixels + rightColPixels;


        double newLeftPixels = leftColPixels - shiftAmount; // shift
        double newRightPixels = rightColPixels + shiftAmount; // shift

        // enforce mid width
        if (newLeftPixels < leftCol.MinWidth)
            newLeftPixels = leftCol.MinWidth;
        if (newRightPixels < rightCol.MinWidth)
            newRightPixels = rightCol.MinWidth;


        double clampedTotalPixels = newLeftPixels + newRightPixels;
        // derive new star values based on the new pixel ratio
        double totalStars = leftCol.Width.Value + rightCol.Width.Value;
        double newLeftColStars = (newLeftPixels / clampedTotalPixels) * totalStars;
        double newRightColStars = totalStars - newLeftColStars;


        // apply calcd values to UI
        leftCol.Width = new GridLength(newLeftColStars, GridUnitType.Star);
        rightCol.Width = new GridLength(newRightColStars, GridUnitType.Star);
    }



    private void GridSplitterDragExec(object? sender, VectorEventArgs e)
    {
        ResetRectangle();
        UnselectAllPreviewMonitors();
    }


    private void OnEnlargedImageClick(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel)
        {
            EnlargedPreviewImage.IsVisible = false;
            MainGrid.Opacity = 1;
        }
    }

    // opens file explorer and navigates to image
    private void OpenImageClicked(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.CurrentWallpaperPreview != null)
        {
            FileExporerHelper fileExporerHelper = new FileExporerHelper();
            if (viewModel.LastSelectedWallpaper != null)
            {
                fileExporerHelper.OpenFileInExplorer(viewModel.LastSelectedWallpaper.FilePath);
            }
        }
    }



    // draggable rect section ------------------------------------

    public void ShowDraggableRectangle(MonitorInfo monitor)
    {
        if (DragRect == null || OverlayCanvas == null || PreviewImage == null) return;

        double realW = monitor.Bounds.Width, realH = monitor.Bounds.Height;
        if (realW <= 0 || realH <= 0) return;

        double previewW = PreviewImage.Bounds.Width, previewH = PreviewImage.Bounds.Height;
        if (previewW <= 0 || previewH <= 0) return;

        double monitorRatio = realW / realH;
        double finalWidth = previewW, finalHeight = previewH;

        if (monitorRatio > previewW / previewH)
        {
            finalHeight = previewW / monitorRatio;
        }
        else
        {
            finalWidth = previewH * monitorRatio;
        }

        DragRect.Width = finalWidth;
        DragRect.Height = finalHeight;
        _aspectRatio = finalWidth / finalHeight;

        Canvas.SetLeft(DragRect, (previewW - finalWidth) / 2);
        Canvas.SetTop(DragRect, (previewH - finalHeight) / 2);

        DragRect.IsVisible = true;
    }

    public void ResetRectangle()
    {
        if (DragRect == null) return;
        DragRect.IsVisible = false;
        DragRect.Width = DragRect.Height = 0;
        Canvas.SetLeft(DragRect, 0);
        Canvas.SetTop(DragRect, 0);
        _aspectRatio = 1.0;
    }

    // ------------------------------------------------------
    // 2) Pointer Pressed => Decide DRAG vs RESIZE
    // ------------------------------------------------------
    private void DragRect_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DragRect == null || OverlayCanvas == null) return;

        _isPointerDown = true;
        e.Pointer.Capture(DragRect);

        var pos = e.GetPosition(OverlayCanvas);

        // Save current rect coords
        _dragStart = pos;
        _rectStartX = Canvas.GetLeft(DragRect);
        _rectStartY = Canvas.GetTop(DragRect);
        _rectStartWidth = DragRect.Width;
        _rectStartHeight = DragRect.Height;

        double rectRight = _rectStartX + _rectStartWidth;
        double rectBottom = _rectStartY + _rectStartHeight;

        // If near bottom-right corner => Resize
        if (Math.Abs(pos.X - rectRight) < CornerHitSize && Math.Abs(pos.Y - rectBottom) < CornerHitSize)
        {
            _operation = RectOperation.Resizing;
        }
        else
        {
            _operation = RectOperation.Dragging;
        }
    }

    private void DragRect_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (!_isPointerDown || e.Pointer.Captured != DragRect) return;
        var pos = e.GetPosition(OverlayCanvas);
        double dx = pos.X - _dragStart.X, dy = pos.Y - _dragStart.Y;
        if (_operation == RectOperation.Dragging) DoDragging(dx, dy);
        if (_operation == RectOperation.Resizing) DoResizing(dx, dy);
    }

    private void DragRect_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (e.Pointer.Captured == DragRect) e.Pointer.Capture(null);
        _isPointerDown = false;
        _operation = RectOperation.None;
    }

    private void DoDragging(double dx, double dy)
    {
        double maxX = OverlayCanvas.Bounds.Width - DragRect.Width;
        double maxY = OverlayCanvas.Bounds.Height - DragRect.Height;

        double newX = Math.Clamp(_rectStartX + dx, 0, maxX);
        double newY = Math.Clamp(_rectStartY + dy, 0, maxY);

        Canvas.SetLeft(DragRect, newX);
        Canvas.SetTop(DragRect, newY);
    }


    private void DoResizing(double dx, double dy)
    {
        double newWidth = _rectStartWidth + dx;
        double newHeight = _rectStartHeight + dy;

        // 1) Enforce aspect ratio: newHeight is derived from newWidth
        newHeight = newWidth / _aspectRatio;

        // 2) Minimum size
        if (newWidth < 10) newWidth = 10;
        if (newHeight < 10) newHeight = 10;

        // 3) Clamp to canvas edges
        double maxWidth = OverlayCanvas.Bounds.Width - _rectStartX;
        double maxHeight = OverlayCanvas.Bounds.Height - _rectStartY;

        if (newWidth > maxWidth) newWidth = maxWidth;
        if (newHeight > maxHeight) newHeight = maxHeight;

        // 4) If we hit the bottom edge (vertical clamp), re-calc width so aspect ratio holds
        if (newHeight == maxHeight)
        {
            newWidth = newHeight * _aspectRatio;
            if (newWidth > maxWidth)
                newWidth = maxWidth;
        }

        // 5) If we hit the right edge (horizontal clamp), re-calc height so aspect ratio holds
        if (newWidth == maxWidth)
        {
            newHeight = newWidth / _aspectRatio;
            if (newHeight > maxHeight)
                newHeight = maxHeight;
        }

        DragRect.Width = newWidth;
        DragRect.Height = newHeight;
    }


    




    private async void OnPreviewMonitorTapped(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control control && control.DataContext is MonitorInfo monitor)
        {
            var viewModel = DataContext as MainWindowViewModel;
            if (viewModel != null)
            {
                Debug.WriteLine("monitor tapped = " + monitor.MonitorIdPath);
                SelectOneToggle.IsChecked = true; // flip first

                await viewModel.MonitorTapped(monitor);
                ShowDraggableRectangle(monitor); // size rect specific monitors real aspect ratio
                uniVM.SetBackgroundButtonEnabled = true;
            }
        }
    }

    private void UnselectAllPreviewMonitors()
    {
        if (DataContext is MainWindowViewModel vm)
        {
            vm.LastSelMonitor = null;
            // manipulate monitor UI
            foreach (MonitorInfo mon in vm.MonitorList)
            {
                mon.FillColour = "Navy";
            }
            uniVM.SetBackgroundButtonEnabled = false;
        }
    }

    private void OnSelectOneMonitorToggleChecked(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel == null) return;

        viewModel.StyleDropdownEnabled = false;
        UnselectAllPreviewMonitors();
        ResetRectangle();
        uniVM.SetBackgroundButtonEnabled = false;
    }

    private async void OnSelectAllMonitorsToggleChecked(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel == null) return;

        uniVM.SetBackgroundButtonEnabled = true;
        ResetRectangle();
        viewModel.StyleDropdownEnabled = true;
        await viewModel.AllMonitorsSelected();
    }


    private void OnFavouriteClicked(object? sender, RoutedEventArgs e)
    {
        Debug.WriteLine("favourite clicked (not yet implemented)");
    }


    private void EnlargePreviewImg(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainWindowViewModel viewModel && viewModel.CurrentWallpaperPreview != null)
        {
            EnlargedPreviewImage.IsVisible = true;
            MainGrid.Opacity = 0.01;
        }
    }




    private async void OnSetWallpaperClicked(object? sender, RoutedEventArgs e)
    {
        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel == null) return;

        uniVM.SetBackgroundButtonEnabled = false; // disable button to ensure no spamming

        // delay button enable by a second (Task.Delay) ; helps users intuitively understand that the button worked
        if (viewModel.StyleDropdownEnabled == true)
        {
            await viewModel.SetWallpaperWithoutCrop();
            await Task.Delay(1000);
            uniVM.SetBackgroundButtonEnabled = true;
        }
        else
        {
            await SetCroppedWallpaper();
            await Task.Delay(1000);
            uniVM.SetBackgroundButtonEnabled = true;
        }
    }

    public async Task SetCroppedWallpaper()
    {
        if (DragRect == null || OverlayCanvas == null || PreviewImage == null) return;

        var viewModel = DataContext as MainWindowViewModel;
        if (viewModel?.LastSelectedWallpaper == null || viewModel.LastSelMonitor == null) return;

        var monitor = viewModel.LastSelMonitor;
        var wallpaper = viewModel.LastSelectedWallpaper;

        using var originalImage = SkiaSharp.SKBitmap.Decode(wallpaper.FilePath);
        int cropX = (int)(Canvas.GetLeft(DragRect) * (originalImage.Width / PreviewImage.Bounds.Width));
        int cropY = (int)(Canvas.GetTop(DragRect) * (originalImage.Height / PreviewImage.Bounds.Height));
        int cropWidth = (int)(DragRect.Width * (originalImage.Width / PreviewImage.Bounds.Width));
        int cropHeight = (int)(DragRect.Height * (originalImage.Height / PreviewImage.Bounds.Height));

        await viewModel.SetWallpaperWithCrop(wallpaper.FilePath, monitor.MonitorIdPath, cropX, cropY, cropWidth, cropHeight);
    }


}