using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace HanabePhotoManager.App.Tools;

public sealed class TaskStatusItem : ObservableObject
{
    private readonly Func<bool> _running;
    private readonly Func<double> _progress;
    private readonly Func<string> _detail;
    private DateTimeOffset? _started;
    private TimeSpan _elapsed;
    private bool _wasRunning;
    public string Name { get; }
    public ICommand OpenCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand? ContinueCommand { get; }
    public bool HasContinue => ContinueCommand is not null;
    public double Progress => Math.Clamp(double.IsFinite(_progress()) ? _progress() : 0, 0, 100);
    public string Detail => _detail();
    public string State => _running() ? $"运行中 · {Progress:0}% · 已用 {Elapsed:mm\\:ss}" :
        _started is not null ? $"已结束 · 用时 {_elapsed:mm\\:ss}（结果见下方）" : "待命";
    public TimeSpan Elapsed => _started is { } started && _running() ? DateTimeOffset.Now - started : _elapsed;

    public TaskStatusItem(string name, INotifyPropertyChanged source, Func<bool> running, Func<double> progress,
        Func<string> detail, Action open, ICommand cancel, ICommand? resume = null)
    {
        Name = name; _running = running; _progress = progress; _detail = detail;
        OpenCommand = new RelayCommand(open); CancelCommand = cancel; ContinueCommand = resume;
        source.PropertyChanged += (_, _) => Refresh();
        Refresh();
    }

    private void Refresh()
    {
        bool running = _running();
        if (running && !_wasRunning) { _started = DateTimeOffset.Now; _elapsed = TimeSpan.Zero; }
        if (!running && _wasRunning && _started is { } start) _elapsed = DateTimeOffset.Now - start;
        _wasRunning = running;
        OnPropertyChanged(nameof(Progress)); OnPropertyChanged(nameof(Detail)); OnPropertyChanged(nameof(State));
    }
}
