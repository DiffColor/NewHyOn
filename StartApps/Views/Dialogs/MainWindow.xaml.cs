using System;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Forms = System.Windows.Forms;
using StartApps.Helpers;
using StartApps.Models;
using StartApps.Services;
using StartApps.ViewModels;
using StartApps.Views.Dialogs;
using Wpf.Ui.Controls;
using WpfApplication = System.Windows.Application;
using WpfDragEventArgs = System.Windows.DragEventArgs;
using WpfItemsControl = System.Windows.Controls.ItemsControl;
using WpfMessageBox = System.Windows.MessageBox;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using WpfMouseEventArgs = System.Windows.Input.MouseEventArgs;
using WpfPoint = System.Windows.Point;
using WpfGiveFeedbackEventArgs = System.Windows.GiveFeedbackEventArgs;
using WpfIDataObject = System.Windows.IDataObject;
using WpfDragDropEffects = System.Windows.DragDropEffects;
using WpfDataObject = System.Windows.DataObject;

namespace StartApps.Views;

public partial class MainWindow : FluentWindow
{
    private const string DragExistingFormat = nameof(AppEntryViewModel);
    private const string DragNewAppFormat = "StartApps.NewAppType";

    private readonly AppDependencyService _dependencyService;
    private readonly GlobalHotkeyService _hotkeyService;
    private readonly AppProfile _profile;
    private Forms.NotifyIcon? _notifyIcon;
    private bool _isExitRequested;
    private UIElement? _dragScope;
    private AdornerLayer? _adornerLayer;
    private DragAdorner? _dragAdorner;
    private DropPreviewAdorner? _dropPreviewAdorner;
    private FrameworkElement? _pendingVisualSource;
    private AppEntryViewModel? _pendingCardEntry;
    private string? _pendingAppType;
    private WpfPoint _pendingDragStartPoint;
    private bool _isDragExecuting;
    private bool _skipCardToggle;
    private FrameworkElement? _activeDraggedVisual;
    private double? _activeDraggedVisualOpacity;
    private ItemsControl? _currentDropTarget;
    private int _currentDropIndex = -1;

    public MainWindow(MainWindowViewModel viewModel, AppDependencyService dependencyService, AppProfile profile, GlobalHotkeyService hotkeyService)
    {
        InitializeComponent();
        DataContext = viewModel;
        _dependencyService = dependencyService;
        _hotkeyService = hotkeyService;
        _profile = profile;
        InitializeTrayIcon();
    }

    private MainWindowViewModel ViewModel => (MainWindowViewModel)DataContext;

    private void InitializeTrayIcon()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Visible = true,
            Text = BuildNotifyText(_profile.DisplayName)
        };

        try
        {
            var iconUri = new Uri("pack://application:,,,/StartApps;component/Assets/startapps.ico", UriKind.Absolute);
            var streamInfo = WpfApplication.GetResourceStream(iconUri);
            if (streamInfo != null)
            {
                _notifyIcon.Icon = new System.Drawing.Icon(streamInfo.Stream);
            }
        }
        catch
        {
            // ignore, default icon
        }

        _notifyIcon.DoubleClick += (_, _) => Dispatcher.Invoke(ShowFromTray);
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("열기", null, (_, _) => Dispatcher.Invoke(ShowFromTray));
        menu.Items.Add("종료", null, (_, _) => Dispatcher.Invoke(RequestExit));
        _notifyIcon.ContextMenuStrip = menu;
    }

    private async void FluentWindow_OnLoaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();
    }

    private void FluentWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        if (_isExitRequested)
        {
            _notifyIcon?.Dispose();
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
    }

    private void RequestExit()
    {
        _isExitRequested = true;
        Close();
    }

    private static string BuildNotifyText(string text)
    {
        const int maxLength = 63;
        if (string.IsNullOrWhiteSpace(text))
        {
            return "StartApps";
        }

        return text.Length <= maxLength ? text : text[..maxLength];
    }

    private async void TypeButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.Button button || button.Tag is not string tag || !Enum.TryParse<AppType>(tag, true, out var type))
        {
            return;
        }

        await CreateAppAsync(type, AppExecutionZone.Parallel);
    }

    private void TypeButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement button || button.Tag is not string tag)
        {
            return;
        }

        _pendingAppType = tag;
        _pendingCardEntry = null;
        _pendingVisualSource = button;
        _pendingDragStartPoint = e.GetPosition(null);
    }

    private void TypeButton_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_pendingAppType == null || sender != _pendingVisualSource)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (!HasMetDragThreshold(e.GetPosition(null)))
        {
            return;
        }

        BeginNewAppDrag((FrameworkElement)sender);
    }

    private void TypeButton_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        ClearPendingTypeCandidate();
    }

    private void AppCard_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not AppEntryViewModel entry)
        {
            return;
        }

        _pendingCardEntry = entry;
        _pendingAppType = null;
        _pendingVisualSource = element;
        _pendingDragStartPoint = e.GetPosition(null);
    }

    private void AppCard_PreviewMouseMove(object sender, WpfMouseEventArgs e)
    {
        if (_pendingCardEntry == null || sender != _pendingVisualSource)
        {
            return;
        }

        if (e.LeftButton != MouseButtonState.Pressed)
        {
            return;
        }

        if (!HasMetDragThreshold(e.GetPosition(null)))
        {
            return;
        }

        BeginExistingAppDrag((FrameworkElement)sender, _pendingCardEntry);
    }

    private async void AppCard_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (_isDragExecuting || _skipCardToggle)
        {
            _skipCardToggle = false;
            return;
        }

        if (sender is FrameworkElement element && element.DataContext is AppEntryViewModel entry)
        {
            var actionText = entry.DisplayName;
            var prompt = entry.IsEnabled
                ? $"{actionText} 앱을 비활성화하시겠습니까?"
                : $"{actionText} 앱을 실행하시겠습니까?";

            var confirm = WpfMessageBox.Show(this, prompt, "확인", WpfMessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != WpfMessageBoxResult.Yes)
            {
                return;
            }

            await ViewModel.ToggleAppAsync(entry);
        }

        ClearPendingCardCandidate();
    }

    private async void AppCard_SettingsClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem menuItem)
        {
            return;
        }

        var entry = menuItem.Tag as AppEntryViewModel ?? menuItem.DataContext as AppEntryViewModel;
        if (entry == null)
        {
            return;
        }

        var settingsWindow = new AppSettingsWindow(entry.Definition, _dependencyService, _hotkeyService, CreateDefinitionsSnapshot())
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true && settingsWindow.ResultDefinition is { } result)
        {
            var updated = await ViewModel.AddOrUpdateAsync(result);
            await ViewModel.ApplyEntryStateAsync(updated);
        }
    }

    private void AppList_DragEnter(object sender, WpfDragEventArgs e)
    {
        HandleListDrag(sender, e);
    }

    private void AppList_DragOver(object sender, WpfDragEventArgs e)
    {
        HandleListDrag(sender, e);
    }

    private void AppList_DragLeave(object sender, WpfDragEventArgs e)
    {
        if (sender is not ItemsControl list || sender != _currentDropTarget)
        {
            e.Handled = true;
            return;
        }

        var position = e.GetPosition(list);
        if (position.X < 0 || position.Y < 0 || position.X > list.ActualWidth || position.Y > list.ActualHeight)
        {
            ResetDropVisuals();
        }

        e.Handled = true;
    }

    private async void AppList_Drop(object sender, WpfDragEventArgs e)
    {
        if (sender is not ItemsControl list)
        {
            return;
        }

        if (!TryResolvePayload(e.Data, out var entry, out var appType))
        {
            ResetDropVisuals();
            e.Handled = true;
            return;
        }

        var zone = GetZoneFromList(list);
        var index = _currentDropTarget == list ? _currentDropIndex : CalculateDropIndex(list, e.GetPosition(list));

        if (entry != null)
        {
            await ViewModel.MoveAppAsync(entry, zone, index);
        }
        else if (appType != null && Enum.TryParse<AppType>(appType, true, out var parsedType))
        {
            var created = await CreateAppAsync(parsedType, zone);
            if (created != null)
            {
                await ViewModel.MoveAppAsync(created, zone, index);
            }
        }

        ResetDropVisuals();
        e.Handled = true;
    }

    private void HandleListDrag(object sender, WpfDragEventArgs e)
    {
        if (sender is not ItemsControl list)
        {
            return;
        }

        if (!TryResolvePayload(e.Data, out var entry, out var appType))
        {
            e.Effects = WpfDragDropEffects.None;
            ResetDropVisuals();
            e.Handled = true;
            return;
        }

        e.Effects = entry != null ? WpfDragDropEffects.Move : WpfDragDropEffects.Copy;
        e.Handled = true;

        var index = CalculateDropIndex(list, e.GetPosition(list));
        _currentDropTarget = list;
        _currentDropIndex = index;
        ShowDropPreview(list, index);
    }

    private static bool TryResolvePayload(WpfIDataObject data, out AppEntryViewModel? entry, out string? appType)
    {
        entry = null;
        appType = null;

        if (data.GetDataPresent(DragExistingFormat) && data.GetData(DragExistingFormat) is AppEntryViewModel vm)
        {
            entry = vm;
            return true;
        }

        if (data.GetDataPresent(DragNewAppFormat) && data.GetData(DragNewAppFormat) is string type)
        {
            appType = type;
            return true;
        }

        return false;
    }

    private int CalculateDropIndex(ItemsControl list, WpfPoint point)
    {
        for (int i = 0; i < list.Items.Count; i++)
        {
            if (list.ItemContainerGenerator.ContainerFromIndex(i) is not FrameworkElement item)
            {
                continue;
            }

            var bounds = VisualTreeHelper.GetDescendantBounds(item);
            if (bounds.IsEmpty)
            {
                bounds = new Rect(new WpfPoint(0, 0), new System.Windows.Size(item.ActualWidth, item.ActualHeight));
            }

            var topLeft = item.TranslatePoint(new WpfPoint(0, 0), list);
            var rect = new Rect(topLeft, bounds.Size);
            if (rect.Contains(point))
            {
                return i;
            }
        }

        return list.Items.Count;
    }

    private void ShowDropPreview(ItemsControl list, int targetIndex)
    {
        if (!EnsureAdornerInfrastructure())
        {
            return;
        }

        var rect = GetDropPreviewRect(list, targetIndex);
        if (rect == null)
        {
            HideDropPreview();
            return;
        }

        if (_dropPreviewAdorner == null && _dragScope != null && _adornerLayer != null)
        {
            _dropPreviewAdorner = new DropPreviewAdorner(_dragScope);
            _adornerLayer.Add(_dropPreviewAdorner);
        }

        _dropPreviewAdorner?.Update(rect.Value);
    }

    private Rect? GetDropPreviewRect(ItemsControl list, int targetIndex)
    {
        if (_dragScope == null)
        {
            return null;
        }

        if (targetIndex < list.Items.Count &&
            list.ItemContainerGenerator.ContainerFromIndex(targetIndex) is FrameworkElement container)
        {
            return GetElementRectRelativeToScope(container);
        }

        if (FindVisualChild<WrapPanel>(list) is WrapPanel wrapPanel)
        {
            var itemWidth = wrapPanel.ItemWidth > 0 ? wrapPanel.ItemWidth : wrapPanel.ActualWidth;
            var itemHeight = wrapPanel.ItemHeight > 0 ? wrapPanel.ItemHeight : wrapPanel.ActualHeight;
            if (itemWidth <= 0 || itemHeight <= 0)
            {
                return null;
            }

            var origin = wrapPanel.TranslatePoint(new WpfPoint(0, 0), _dragScope);
            var offset = CalculateWrapOffset(wrapPanel, targetIndex, itemWidth, itemHeight);
            var topLeft = new WpfPoint(origin.X + offset.X, origin.Y + offset.Y);
            return new Rect(topLeft, new System.Windows.Size(itemWidth, itemHeight));
        }

        if (list.Items.Count > 0 &&
            list.ItemContainerGenerator.ContainerFromIndex(list.Items.Count - 1) is FrameworkElement lastContainer)
        {
            return GetElementRectRelativeToScope(lastContainer);
        }

        return null;
    }

    private Rect? GetElementRectRelativeToScope(FrameworkElement element)
    {
        if (_dragScope == null)
        {
            return null;
        }

        var bounds = VisualTreeHelper.GetDescendantBounds(element);
        if (bounds.IsEmpty)
        {
            bounds = new Rect(new WpfPoint(0, 0), new System.Windows.Size(element.ActualWidth, element.ActualHeight));
        }

        var topLeft = element.TranslatePoint(bounds.TopLeft, _dragScope);
        return new Rect(topLeft, bounds.Size);
    }

    private Vector CalculateWrapOffset(WrapPanel panel, int slotIndex, double itemWidth, double itemHeight)
    {
        var availableWidth = panel.ActualWidth;
        var columns = availableWidth > 0 ? Math.Max(1, (int)Math.Floor(availableWidth / itemWidth)) : 1;
        var row = columns > 0 ? slotIndex / columns : slotIndex;
        var column = columns > 0 ? slotIndex % columns : 0;
        return new Vector(column * itemWidth, row * itemHeight);
    }

    private static T? FindVisualChild<T>(DependencyObject parent) where T : DependencyObject
    {
        var count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T typedChild)
            {
                return typedChild;
            }

            var descendant = FindVisualChild<T>(child);
            if (descendant != null)
            {
                return descendant;
            }
        }

        return null;
    }

    private static AppExecutionZone GetZoneFromList(ItemsControl list)
    {
        return list.Tag is string tag && tag.Equals("Sequential", StringComparison.OrdinalIgnoreCase)
            ? AppExecutionZone.Sequential
            : AppExecutionZone.Parallel;
    }

    private void HideDropPreview()
    {
        _dropPreviewAdorner?.Clear();
    }

    private void ResetDropVisuals()
    {
        HideDropPreview();
        _currentDropTarget = null;
        _currentDropIndex = -1;
    }

    private void BeginNewAppDrag(FrameworkElement source)
    {
        if (_pendingAppType == null)
        {
            return;
        }

        var type = _pendingAppType;
        if (!Enum.TryParse<AppType>(type, true, out _))
        {
            ClearPendingTypeCandidate();
            return;
        }

        _pendingAppType = null;

        var data = new WpfDataObject();
        data.SetData(DragNewAppFormat, type);
        BeginDragInternal(source, data, WpfDragDropEffects.Copy, hideSource: false);
    }

    private void BeginExistingAppDrag(FrameworkElement source, AppEntryViewModel entry)
    {
        _pendingCardEntry = null;
        _skipCardToggle = true;
        var data = new WpfDataObject();
        data.SetData(DragExistingFormat, entry);
        BeginDragInternal(source, data, WpfDragDropEffects.Move, hideSource: true);
    }

    private void BeginDragInternal(FrameworkElement source, WpfIDataObject data, WpfDragDropEffects effects, bool hideSource)
    {
        ResetDropVisuals();
        _isDragExecuting = true;

        var preview = CreateDragVisual(source);

        if (hideSource)
        {
            HideDraggedVisual(source);
        }
        if (EnsureAdornerInfrastructure() && preview.Image != null && _dragScope != null && _adornerLayer != null)
        {
            _dragAdorner = new DragAdorner(_dragScope, preview.Image, preview.Size);
            _adornerLayer.Add(_dragAdorner);
            _dragAdorner.SetPosition(Mouse.GetPosition(_dragScope));
            _dragScope.PreviewDragOver += DragScopeOnPreviewDragOver;
        }

        GiveFeedback += MainWindow_GiveFeedback;

        try
        {
            DragDrop.DoDragDrop(source, data, effects);
        }
        finally
        {
            RestoreDraggedVisual();
            EndDragVisuals();
            ResetDragState();
        }
    }

    private bool EnsureAdornerInfrastructure()
    {
        _dragScope ??= DragScopeHost ?? Content as UIElement ?? this;
        if (_dragScope == null)
        {
            return false;
        }

        _adornerLayer ??= AdornerLayer.GetAdornerLayer(_dragScope);
        if (_adornerLayer == null && _dragScope != this)
        {
            _adornerLayer = AdornerLayer.GetAdornerLayer(this);
        }

        return _adornerLayer != null;
    }

    private void DragScopeOnPreviewDragOver(object? sender, WpfDragEventArgs e)
    {
        if (_dragScope == null || _dragAdorner == null)
        {
            return;
        }

        _dragAdorner.SetPosition(e.GetPosition(_dragScope));
        e.Handled = true;
    }

    private void MainWindow_GiveFeedback(object? sender, WpfGiveFeedbackEventArgs e)
    {
        e.UseDefaultCursors = false;
        Mouse.SetCursor(System.Windows.Input.Cursors.Arrow);
        e.Handled = true;
    }

    private void EndDragVisuals()
    {
        if (_dragScope != null)
        {
            _dragScope.PreviewDragOver -= DragScopeOnPreviewDragOver;
        }

        GiveFeedback -= MainWindow_GiveFeedback;

        if (_dropPreviewAdorner != null && _adornerLayer != null)
        {
            _adornerLayer.Remove(_dropPreviewAdorner);
            _dropPreviewAdorner = null;
        }

        if (_dragAdorner != null && _adornerLayer != null)
        {
            _adornerLayer.Remove(_dragAdorner);
            _dragAdorner = null;
        }

        _adornerLayer = null;
        _dragScope = null;
    }

    private bool HasMetDragThreshold(WpfPoint current)
    {
        return Math.Abs(current.X - _pendingDragStartPoint.X) >= SystemParameters.MinimumHorizontalDragDistance ||
               Math.Abs(current.Y - _pendingDragStartPoint.Y) >= SystemParameters.MinimumVerticalDragDistance;
    }

    private void ClearPendingTypeCandidate()
    {
        _pendingAppType = null;
        if (_pendingCardEntry == null)
        {
            _pendingVisualSource = null;
        }
    }

    private void ClearPendingCardCandidate()
    {
        _pendingCardEntry = null;
        if (_pendingAppType == null)
        {
            _pendingVisualSource = null;
        }
    }

    private void ResetDragState()
    {
        _pendingVisualSource = null;
        _pendingAppType = null;
        _pendingCardEntry = null;
        _isDragExecuting = false;
        _skipCardToggle = false;
        ResetDropVisuals();
    }

    private void HideDraggedVisual(FrameworkElement visual)
    {
        _activeDraggedVisual = visual;
        _activeDraggedVisualOpacity = visual.Opacity;
        visual.Opacity = 0;
    }

    private void RestoreDraggedVisual()
    {
        if (_activeDraggedVisual != null)
        {
            _activeDraggedVisual.Opacity = _activeDraggedVisualOpacity ?? 1;
        }

        _activeDraggedVisual = null;
        _activeDraggedVisualOpacity = null;
    }

    private (ImageSource? Image, System.Windows.Size Size) CreateDragVisual(UIElement source)
    {
        if (source is not FrameworkElement frameworkElement)
        {
            return (null, default);
        }

        if (frameworkElement.ActualWidth <= 0 || frameworkElement.ActualHeight <= 0)
        {
            frameworkElement.UpdateLayout();
        }

        var width = Math.Max(1, frameworkElement.ActualWidth);
        var height = Math.Max(1, frameworkElement.ActualHeight);
        if (double.IsNaN(width) || double.IsInfinity(width) || width <= 0)
        {
            width = Math.Max(1, frameworkElement.DesiredSize.Width);
        }
        if (double.IsNaN(height) || double.IsInfinity(height) || height <= 0)
        {
            height = Math.Max(1, frameworkElement.DesiredSize.Height);
        }

        var visualBrush = new VisualBrush(frameworkElement)
        {
            Stretch = Stretch.Fill,
            AlignmentX = AlignmentX.Left,
            AlignmentY = AlignmentY.Top
        };

        var drawingVisual = new DrawingVisual();
        using (var context = drawingVisual.RenderOpen())
        {
            context.DrawRectangle(visualBrush, null, new Rect(new System.Windows.Size(width, height)));
        }

        var renderTarget = new RenderTargetBitmap(
            (int)Math.Ceiling(width),
            (int)Math.Ceiling(height),
            96,
            96,
            PixelFormats.Pbgra32);
        renderTarget.Render(drawingVisual);

        return (renderTarget, new System.Windows.Size(width, height));
    }

    private async Task<AppEntryViewModel?> CreateAppAsync(AppType type, AppExecutionZone zone)
    {
        var definition = new AppDefinition
        {
            Type = type,
            Name = type.ToString(),
            Zone = zone
        };

        ApplyDefaults(definition, _dependencyService);

        var settingsWindow = new AppSettingsWindow(definition, _dependencyService, _hotkeyService, CreateDefinitionsSnapshot())
        {
            Owner = this
        };

        if (settingsWindow.ShowDialog() == true && settingsWindow.ResultDefinition is { } result)
        {
            var entry = await ViewModel.AddOrUpdateAsync(result);
            await ViewModel.ApplyEntryStateAsync(entry);
            return entry;
        }

        return null;
    }

    private static void ApplyDefaults(AppDefinition definition, AppDependencyService dependencyService)
    {
        switch (definition.Type)
        {
            case AppType.Rdb:
                definition.Port = definition.Port ?? 28015;
                break;
            case AppType.Ntp:
                definition.Name = "NtpServer";
                definition.IsEnabled = true;
                definition.RunAsAdministrator = true;
                definition.Port = 123;
                definition.Arguments = "-listen :123 -firewall-remote-address Any -stratum 10 -ref-id LOCL -root-dispersion 1s -log-interval 10m -log-file logs\\ntp-server.log";
                definition.ShowWindow = false;
                definition.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                break;
            case AppType.Ftp:
                if (definition.Port is null or 21)
                {
                    definition.Port = AppDependencyService.DefaultFtpPort;
                }
                if (string.IsNullOrWhiteSpace(definition.PassivePortRange))
                {
                    definition.PassivePortRange = "24000-24240";
                }
                if (string.IsNullOrWhiteSpace(definition.FtpHomeDirectory))
                {
                    definition.FtpHomeDirectory = dependencyService.GetDefaultFtpHomeDirectory();
                }
                break;
            case AppType.Msg:
            case AppType.Msg472:
            case AppType.Msg10:
                definition.Port = definition.Port ?? 5000;
                if (string.IsNullOrWhiteSpace(definition.MsgHubPath))
                {
                    definition.MsgHubPath = "/Data";
                }
                definition.ShowWindow = false;
                definition.WindowStyle = System.Diagnostics.ProcessWindowStyle.Hidden;
                definition.RequireNetworkAvailable = true;
                break;
            default:
                break;
        }
    }

    private async void AppCard_DeleteClick(object sender, RoutedEventArgs e)
    {
        if (sender is not System.Windows.Controls.MenuItem menuItem)
        {
            return;
        }

        var entry = menuItem.Tag as AppEntryViewModel ?? menuItem.DataContext as AppEntryViewModel;
        if (entry == null)
        {
            return;
        }

        e.Handled = true;

        var message = $"{entry.DisplayName} 앱을 삭제하시겠습니까?\n이 작업은 실행 목록과 저장된 설정에서 앱을 제거합니다.";
        var confirm = WpfMessageBox.Show(this, message, "삭제 확인", WpfMessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != WpfMessageBoxResult.Yes)
        {
            return;
        }

        await ViewModel.DeleteEntryAsync(entry);
    }

    private IReadOnlyList<AppDefinition> CreateDefinitionsSnapshot() =>
        ViewModel.EnumerateEntries()
            .Select(x => x.Definition)
            .ToList();
}
