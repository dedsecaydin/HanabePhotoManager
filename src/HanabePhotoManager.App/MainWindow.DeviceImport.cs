using System.IO;
using System.Windows.Threading;
using HanabePhotoManager.App.Services;

namespace HanabePhotoManager.App;

public partial class MainWindow
{
    private readonly MediaDeviceMonitor _mediaDeviceMonitor = new();
    private readonly CancellationTokenSource _deviceMonitorCancellation = new();
    private DispatcherTimer? _deviceMonitorTimer;
    private bool _deviceCheckRunning;
    private readonly Queue<string> _pendingMediaDevices = new();

    private void StartMediaDeviceMonitor()
    {
        if (App.ScreenshotPage is not null) return;
        _deviceMonitorTimer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromSeconds(5) };
        _deviceMonitorTimer.Tick += CheckMediaDevices;
        _deviceMonitorTimer.Start();
    }

    private async void CheckMediaDevices(object? sender, EventArgs e)
    {
        if (_deviceCheckRunning || !_viewModel.PromptOnMediaDevice || _viewModel.IsBusy) return;
        _deviceCheckRunning = true;
        try
        {
            var roots = await Task.Run(() => _mediaDeviceMonitor.Discover(_deviceMonitorCancellation.Token));
            foreach (var root in roots) _pendingMediaDevices.Enqueue(root);
            while (_pendingMediaDevices.Count > 0 && !_viewModel.IsBusy && !_deviceMonitorCancellation.IsCancellationRequested)
            {
                var root = _pendingMediaDevices.Dequeue();
                if (Directory.Exists(root)) await _viewModel.OfferDeviceImportAsync(root);
            }
        }
        catch (OperationCanceledException) { }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        finally { _deviceCheckRunning = false; }
    }
}
