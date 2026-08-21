using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using MultiSmbServer.ViewModels;
using MessageBox = System.Windows.MessageBox;

namespace MultiSmbServer.Views;

public partial class MainWindow : Window
{
    private readonly MainViewModel _viewModel;
    private readonly NotifyIcon _trayIcon;
    private bool _isExiting;
    private bool _shownTrayHint;

    public MainWindow()
    {
        InitializeComponent();

        _viewModel = new MainViewModel(Dispatcher);
        DataContext = _viewModel;
        _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        _trayIcon = CreateTrayIcon();

        ParseCommandLineArgs();
    }

    private NotifyIcon CreateTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Abrir", null, (_, _) => RestoreFromTray());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Salir", null, (_, _) => ExitApplication());

        var trayIcon = new NotifyIcon
        {
            Icon = CreateTrayIconImage(),
            Text = "Multi SMB Server",
            Visible = true,
            ContextMenuStrip = menu
        };
        trayIcon.DoubleClick += (_, _) => RestoreFromTray();

        return trayIcon;
    }

    private static System.Drawing.Icon CreateTrayIconImage()
    {
        using var bitmap = new Bitmap(16, 16);
        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var brush = new SolidBrush(Color.FromArgb(0x5B, 0x8D, 0xEF));
            graphics.FillEllipse(brush, 1, 1, 14, 14);
        }

        return System.Drawing.Icon.FromHandle(bitmap.GetHicon());
    }

    private void ParseCommandLineArgs()
    {
        string[] args = Environment.GetCommandLineArgs();

        bool start = false;
        bool silent = false;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i].Trim().ToLowerInvariant())
            {
                case "/start":
                case "--start":
                    start = true;
                    break;
                case "/silent":
                case "/hide":
                case "--silent":
                case "--minimized":
                    silent = true;
                    break;
            }
        }

        if (silent)
            HideToTray();

        if (start && _viewModel.Shares.Any(s => !string.IsNullOrWhiteSpace(s.Path) && Directory.Exists(s.Path)))
            _viewModel.StartCommand.Execute(null);
    }

    protected override void OnStateChanged(EventArgs e)
    {
        base.OnStateChanged(e);

        if (WindowState == WindowState.Minimized)
            HideToTray();
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (!_isExiting)
        {
            MessageBoxResult choice = MessageBox.Show(
                this,
                "¿Qué quieres hacer al cerrar la ventana?\n\n" +
                "·  Sí  →  Salir de la aplicación (detiene el servidor).\n" +
                "·  No  →  Minimizar a la bandeja y seguir sirviendo en segundo plano.\n" +
                "·  Cancelar  →  Volver a la aplicación.",
                "Multi SMB Server",
                MessageBoxButton.YesNoCancel,
                MessageBoxImage.Question,
                MessageBoxResult.No);

            if (choice == MessageBoxResult.No)
            {
                e.Cancel = true;
                HideToTray();
                return;
            }

            if (choice == MessageBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            // Sí: continuar con el cierre real.
            _isExiting = true;
        }

        if (_viewModel.IsRunning)
            _viewModel.StopCommand.Execute(null);

        _viewModel.SaveConfig();

        _trayIcon.Visible = false;
        _trayIcon.Dispose();

        base.OnClosing(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.LogText))
        {
            LogBox.ScrollToEnd();
        }
        else if (e.PropertyName == nameof(MainViewModel.StatusText))
        {
            _trayIcon.Text = $"Multi SMB Server - {_viewModel.StatusText}";
        }
    }

    private void HideToTray()
    {
        Hide();
        ShowInTaskbar = false;

        if (!_shownTrayHint)
        {
            _shownTrayHint = true;
            _trayIcon.ShowBalloonTip(2000, "Multi SMB Server",
                "La aplicación sigue en segundo plano. Doble clic en el icono de la bandeja para abrirla.",
                ToolTipIcon.Info);
        }
    }

    private void RestoreFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        ShowInTaskbar = true;
        Activate();
    }

    private void ExitApplication()
    {
        _isExiting = true;
        Close();
    }
}
