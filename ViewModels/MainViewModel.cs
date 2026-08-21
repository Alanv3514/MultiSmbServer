using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Win32;
using MultiSmbServer.Core;
using MultiSmbServer.Models;
using MultiSmbServer.Services;

namespace MultiSmbServer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private const int MaxLogLength = 250_000;
    private const int MaxPendingLogLines = 10_000;

    private readonly SmbServerManager _serverManager = new();
    private readonly ConcurrentQueue<string> _pendingLogLines = new();
    private readonly DispatcherTimer _logTimer;

    public ObservableCollection<ShareEntry> Shares { get; } = new();

    [ObservableProperty]
    private int port = 445;

    [ObservableProperty]
    private string username = "ps2";

    [ObservableProperty]
    private string password = "opl";

    [ObservableProperty]
    private bool enableGuest = true;

    [ObservableProperty]
    private bool lanOnly = true;

    [ObservableProperty]
    private bool isRunning;

    [ObservableProperty]
    private string logText = string.Empty;

    [ObservableProperty]
    private string statusText = string.Empty;

    public bool IsStopped => !IsRunning;

    private int _runningPort;
    private int _runningShareCount;

    public LocalizationManager Localization => LocalizationManager.Instance;

    public int LanguageIndex
    {
        get => LocalizationManager.Instance.Language == LocalizationManager.AppLanguage.English ? 0 : 1;
        set
        {
            LocalizationManager.Instance.Language = value == 1
                ? LocalizationManager.AppLanguage.Spanish
                : LocalizationManager.AppLanguage.English;

            RefreshStatusText();
            SaveConfig();
        }
    }

    public MainViewModel(Dispatcher dispatcher)
    {
        _serverManager.OnLog += AppendLog;
        LoadSettings();

        _logTimer = new DispatcherTimer(DispatcherPriority.Background, dispatcher)
        {
            Interval = TimeSpan.FromMilliseconds(200)
        };
        _logTimer.Tick += (_, _) => FlushLog();
        _logTimer.Start();

        RefreshStatusText();
    }

    private void LoadSettings()
    {
        ServerConfig saved = AppSettings.Load();

        Shares.Clear();
        foreach (ShareConfig share in saved.Shares)
            Shares.Add(new ShareEntry { Name = share.Name, Path = share.Path });

        if (Shares.Count == 0)
            Shares.Add(new ShareEntry { Name = "PS2SMB" });

        Port = saved.Port;
        Username = saved.Username;
        Password = saved.Password;
        EnableGuest = saved.EnableGuest;
        LanOnly = saved.LanOnly;

        LocalizationManager.Instance.Language = saved.Language == "es"
            ? LocalizationManager.AppLanguage.Spanish
            : LocalizationManager.AppLanguage.English;
        OnPropertyChanged(nameof(LanguageIndex));
    }

    public ServerConfig BuildConfig()
    {
        return new ServerConfig
        {
            Shares = Shares
                .Where(s => !string.IsNullOrWhiteSpace(s.Name) || !string.IsNullOrWhiteSpace(s.Path))
                .Select(s => new ShareConfig { Name = s.Name.Trim(), Path = s.Path.Trim() })
                .ToList(),
            Port = Port,
            Username = Username.Trim(),
            Password = Password,
            EnableGuest = EnableGuest,
            LanOnly = LanOnly,
            Language = LocalizationManager.Instance.Language == LocalizationManager.AppLanguage.English ? "en" : "es"
        };
    }

    public void SaveConfig()
    {
        AppSettings.Save(BuildConfig());
    }

    [RelayCommand]
    private void SaveSettings()
    {
        SaveConfig();
        AppendLog(LocalizationManager.Instance["ConfigSaved"]);
    }

    [RelayCommand]
    private void AddShare()
    {
        Shares.Add(new ShareEntry { Name = "PS2SMB" });
    }

    [RelayCommand]
    private void RemoveShare(ShareEntry entry)
    {
        Shares.Remove(entry);

        if (Shares.Count == 0)
            Shares.Add(new ShareEntry { Name = "PS2SMB" });
    }

    [RelayCommand]
    private void BrowseShare(ShareEntry entry)
    {
        var dialog = new OpenFolderDialog
        {
            Title = string.Format(LocalizationManager.Instance["BrowseTitle"], entry.Name)
        };

        if (Directory.Exists(entry.Path))
            dialog.InitialDirectory = entry.Path;

        if (dialog.ShowDialog() == true)
            entry.Path = dialog.FolderName;
    }

    [RelayCommand]
    private void Start()
    {
        if (IsRunning)
            return;

        var config = BuildConfig();

        try
        {
            _serverManager.Start(config);
            IsRunning = true;
            SaveConfig();
            _runningPort = config.Port;
            _runningShareCount = config.Shares.Count;
            RefreshStatusText();
        }
        catch (Exception ex)
        {
            AppendLog("ERROR: " + ex.Message);
        }
    }

    [RelayCommand]
    private void Stop()
    {
        if (!IsRunning)
            return;

        try
        {
            _serverManager.Stop();
        }
        finally
        {
            IsRunning = false;
            RefreshStatusText();
        }
    }

    private void RefreshStatusText()
    {
        StatusText = IsRunning
            ? string.Format(LocalizationManager.Instance["StatusRunning"], _runningPort, _runningShareCount)
            : LocalizationManager.Instance["StatusStopped"];
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(IsStopped));
    }

    private void AppendLog(string message)
    {
        // Se invoca desde hilos del servidor; solo encolamos. Un timer en el hilo UI vacía la cola por lotes.
        if (_pendingLogLines.Count >= MaxPendingLogLines)
            return;

        _pendingLogLines.Enqueue($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {message}");
    }

    private void FlushLog()
    {
        if (_pendingLogLines.IsEmpty)
            return;

        var builder = new StringBuilder(LogText);
        while (_pendingLogLines.TryDequeue(out string? line))
            builder.AppendLine(line);

        string result = builder.ToString();
        if (result.Length > MaxLogLength)
        {
            int cut = result.Length - MaxLogLength;
            int newline = result.IndexOf('\n', cut);
            result = newline >= 0 ? result[(newline + 1)..] : result[cut..];
        }

        LogText = result;
    }
}
