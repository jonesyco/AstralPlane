using AstralPlane.App.ViewModels;
using AstralPlane.Core;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Storage.Pickers;

namespace AstralPlane_App;

/// <summary>
/// Main content page. Owns the <see cref="MainViewModel"/>, wires file/folder
/// pickers and drag-drop, and runs conversions via the Core engine.
/// </summary>
public sealed partial class MainPage : Page
{
    private CancellationTokenSource? _cts;

    public MainViewModel ViewModel { get; } =
        new(new ConversionOptionsViewModel(new MagickFormatCapabilityProbe()));

    public MainPage()
    {
        InitializeComponent();
    }

    private static void InitializeWithWindow(object target)
    {
        nint hwnd = WinRT.Interop.WindowNative.GetWindowHandle(App.MainWindow);
        WinRT.Interop.InitializeWithWindow.Initialize(target, hwnd);
    }

    private async void AddFiles_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FileOpenPicker { ViewMode = PickerViewMode.List };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow(picker);

        var files = await picker.PickMultipleFilesAsync();
        if (files.Count > 0)
            ViewModel.AddFiles(files.Select(f => f.Path));
    }

    private async void AddFolder_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { ViewMode = PickerViewMode.List };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow(picker);

        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
            ViewModel.AddFolder(folder.Path, ViewModel.Options.RecurseSubfolders);
    }

    private void Clear_Click(object sender, RoutedEventArgs e) => ViewModel.Clear();

    private void Queue_DragOver(object sender, DragEventArgs e)
    {
        if (e.DataView.Contains(StandardDataFormats.StorageItems))
        {
            e.AcceptedOperation = DataPackageOperation.Copy;
            e.DragUIOverride.Caption = "Add to queue";
        }
    }

    private async void Queue_Drop(object sender, DragEventArgs e)
    {
        if (!e.DataView.Contains(StandardDataFormats.StorageItems))
            return;

        var deferral = e.GetDeferral();
        try
        {
            var items = await e.DataView.GetStorageItemsAsync();
            var files = new List<string>();
            foreach (var item in items)
            {
                if (item is StorageFolder folder)
                    ViewModel.AddFolder(folder.Path, ViewModel.Options.RecurseSubfolders);
                else if (item is StorageFile file)
                    files.Add(file.Path);
            }
            if (files.Count > 0)
                ViewModel.AddFiles(files);
        }
        finally
        {
            deferral.Complete();
        }
    }

    private async void ChooseOutput_Click(object sender, RoutedEventArgs e)
    {
        var picker = new FolderPicker { ViewMode = PickerViewMode.List };
        picker.FileTypeFilter.Add("*");
        InitializeWithWindow(picker);

        StorageFolder? folder = await picker.PickSingleFolderAsync();
        if (folder is not null)
        {
            ViewModel.Options.ChosenFolder = folder.Path;
            ViewModel.Options.OutputLocation = OutputLocationMode.ChosenFolder;
        }
    }

    private async void Convert_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.Options.OutputLocation == OutputLocationMode.ChosenFolder &&
            string.IsNullOrWhiteSpace(ViewModel.Options.ChosenFolder))
        {
            await ShowMessageAsync("Choose an output folder", "Select an output folder or switch to “Same as source”.");
            return;
        }

        _cts = new CancellationTokenSource();
        try
        {
            var engine = new ConversionEngine(new MagickInputLoader());
            await ViewModel.ConvertAsync(engine, _cts.Token);
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }
    }

    private void Cancel_Click(object sender, RoutedEventArgs e) => _cts?.Cancel();

    private async void OpenOutput_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.LastOutputFolder is { } folder && Directory.Exists(folder))
            await Windows.System.Launcher.LaunchFolderPathAsync(folder);
    }

    private async Task ShowMessageAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = message,
            CloseButtonText = "OK",
            XamlRoot = XamlRoot,
        };
        await dialog.ShowAsync();
    }
}
