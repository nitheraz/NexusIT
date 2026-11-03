using NexusIT.App.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NexusIT.App.Views;

public class MecmView : UserControl, IDisposable
{
    // Connection fields
    private readonly ComboBox _profileCombo = new();
    private readonly TextBox _serverBox = new();
    private readonly TextBox _siteCodeBox = new();
    private readonly TextBox _usernameBox = new();
    private readonly PasswordBox _passwordBox = new();

    // Profile buttons
    private readonly Button _addProfileButton = new();
    private readonly Button _updateProfileButton = new();
    private readonly Button _deleteProfileButton = new();
    private readonly Button _defaultProfileButton = new();

    // Connection buttons
    private readonly Button _connectButton = new();
    private readonly Button _disconnectButton = new();
    private readonly Button _connectedDisconnectButton = new();

    // Status
    private readonly TextBlock _connectionStatus = new();
    private readonly TextBlock _connectedDetails = new();
    private readonly TextBlock _siteInformation = new();
    private readonly TextBlock _mainStatus = new();

    private Border? _connectionPanel;
    private Border? _connectedPanel;

    // Hosts the management view after a successful connection.
    private readonly ContentControl _managementContentHost = new();
    private MecmDeviceManagementView? _deviceManagementView;

    private readonly MecmConnectionService _connectionService = new();
    private readonly MecmDeviceService _deviceService;
    private readonly MecmCollectionService _collectionService;
    private readonly MecmActivityLogService _activityLogService;

    private List<MecmConnectionProfile> _profiles = [];

    private bool _isBusy;
    private bool _isLoadingProfile;
    private bool _disposed;

    private readonly string _connectionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NexusIT",
        "mecm-connections.json");

    private const string PanelColor = "#1F2937";
    private const string InputColor = "#111827";
    private const string TextColor = "#E5E7EB";
    private const string MutedColor = "#9CA3AF";
    private const string AccentColor = "#3B82F6";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public MecmView()
    {
        _deviceService = new MecmDeviceService(_connectionService);
        _collectionService = new MecmCollectionService(_connectionService);
        _activityLogService = new MecmActivityLogService();

        Content = BuildInterface();

        LoadConnections();
        UpdateProfileControls();
        UpdateConnectionControls(false);
    }

    // =========================================================
    // INTERFACE
    // =========================================================

    private UIElement BuildInterface()
    {
        var root = new Grid
        {
            Margin = new Thickness(20)
        };

        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        root.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Header
        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 18)
        };

        header.Children.Add(new TextBlock
        {
            Text = "MECM / SCCM Management",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });

        header.Children.Add(new TextBlock
        {
            Text = "Connect to Microsoft Configuration Manager and manage your environment.",
            FontSize = 13,
            Foreground = BrushFrom(MutedColor),
            Margin = new Thickness(0, 5, 0, 0)
        });

        root.Children.Add(header);

        // Connection panel
        _connectionPanel = CreatePanel();
        _connectionPanel.Margin = new Thickness(0, 0, 0, 16);

        var connectionContent = new StackPanel();

        // Saved profiles
        var profileRow = new Grid
        {
            Margin = new Thickness(0, 0, 0, 16)
        };

        profileRow.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        profileRow.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var profilePanel = new StackPanel();

        profilePanel.Children.Add(CreateLabel("Saved connections"));

        _profileCombo.MinWidth = 230;
        _profileCombo.Height = 36;
        _profileCombo.DisplayMemberPath = nameof(MecmConnectionProfile.Name);
        _profileCombo.Background = BrushFrom(InputColor);
        _profileCombo.Foreground = BrushFrom(TextColor);
        _profileCombo.BorderBrush = BrushFrom("#374151");
        _profileCombo.SelectionChanged += ProfileCombo_SelectionChanged;

        profilePanel.Children.Add(_profileCombo);
        profileRow.Children.Add(profilePanel);

        var profileButtons = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(10, 0, 0, 0)
        };

        AddProfileButton(
            profileButtons, _addProfileButton, "Add", AccentColor, AddProfile);

        AddProfileButton(
            profileButtons, _updateProfileButton, "Update", "#374151", UpdateProfile);

        AddProfileButton(
            profileButtons, _deleteProfileButton, "Delete", "#991B1B", DeleteProfile);

        AddProfileButton(
            profileButtons, _defaultProfileButton, "Set Default", "#374151", SetDefaultProfile);

        Grid.SetColumn(profileButtons, 1);
        profileRow.Children.Add(profileButtons);

        connectionContent.Children.Add(profileRow);

        // Connection fields
        var connectionGrid = new Grid();

        for (int i = 0; i < 4; i++)
        {
            connectionGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });
        }

        connectionGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        AddConnectionField(
            connectionGrid,
            "Site server",
            _serverBox,
            0,
            "MECM01.school.local");

        AddConnectionField(
            connectionGrid,
            "Site code",
            _siteCodeBox,
            1,
            "ABC");

        AddConnectionField(
            connectionGrid,
            "Username",
            _usernameBox,
            2,
            @"DOMAIN\username");

        var passwordPanel = new StackPanel
        {
            Margin = new Thickness(7, 0, 7, 0)
        };

        passwordPanel.Children.Add(CreateLabel("Password"));

        StyleInput(_passwordBox);
        _passwordBox.PasswordChar = '●';
        _passwordBox.MinWidth = 140;

        passwordPanel.Children.Add(_passwordBox);

        Grid.SetColumn(passwordPanel, 3);
        connectionGrid.Children.Add(passwordPanel);

        var connectionButtons = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(7, 0, 0, 0)
        };

        StyleButton(_connectButton, "Connect", AccentColor);
        _connectButton.Click += ConnectButton_Click;
        _connectButton.IsDefault = true;

        connectionButtons.Children.Add(_connectButton);

        StyleButton(_disconnectButton, "Disconnect", "#374151");
        _disconnectButton.Margin = new Thickness(0, 6, 0, 0);
        _disconnectButton.Click += (_, _) => Disconnect();

        connectionButtons.Children.Add(_disconnectButton);

        Grid.SetColumn(connectionButtons, 4);
        connectionGrid.Children.Add(connectionButtons);

        connectionContent.Children.Add(connectionGrid);

        // Connection status
        var statusRow = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 12, 0, 0)
        };

        statusRow.Children.Add(new TextBlock
        {
            Text = "Status:",
            Foreground = BrushFrom(MutedColor),
            Margin = new Thickness(0, 0, 6, 0)
        });

        _connectionStatus.Text = "Not connected";
        _connectionStatus.Foreground = BrushFrom("#FCA5A5");

        statusRow.Children.Add(_connectionStatus);
        connectionContent.Children.Add(statusRow);

        _connectionPanel.Child = connectionContent;

        Grid.SetRow(_connectionPanel, 1);
        root.Children.Add(_connectionPanel);

        // Connected panel
        _connectedPanel = CreatePanel();
        _connectedPanel.Margin = new Thickness(0, 0, 0, 16);
        _connectedPanel.Visibility = Visibility.Collapsed;

        var connectedGrid = new Grid
        {
            Margin = new Thickness(16, 10, 16, 10)
        };

        connectedGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        connectedGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        connectedGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        connectedGrid.Children.Add(new TextBlock
        {
            Text = "● Connected",
            Foreground = BrushFrom("#4ADE80"),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 0, 18, 0),
            VerticalAlignment = VerticalAlignment.Center
        });

        _connectedDetails.Foreground = BrushFrom(TextColor);
        _connectedDetails.TextWrapping = TextWrapping.Wrap;
        _connectedDetails.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(_connectedDetails, 1);
        connectedGrid.Children.Add(_connectedDetails);

        StyleButton(_connectedDisconnectButton, "Disconnect", "#991B1B");
        _connectedDisconnectButton.Click += (_, _) => Disconnect();

        Grid.SetColumn(_connectedDisconnectButton, 2);
        connectedGrid.Children.Add(_connectedDisconnectButton);

        _connectedPanel.Child = connectedGrid;

        Grid.SetRow(_connectedPanel, 1);
        root.Children.Add(_connectedPanel);

        // Main content
        var mainPanel = CreatePanel();

        var mainContent = new StackPanel();

        mainContent.Children.Add(CreateSectionTitle("Connection information"));

        _siteInformation.Text =
            "Connect to a MECM site to retrieve site information.";

        _siteInformation.Foreground = BrushFrom(TextColor);
        _siteInformation.TextWrapping = TextWrapping.Wrap;
        _siteInformation.Margin = new Thickness(0, 16, 0, 12);

        mainContent.Children.Add(_siteInformation);

        mainContent.Children.Add(new Separator
        {
            Background = BrushFrom("#374151"),
            Margin = new Thickness(0, 10, 0, 18)
        });

        mainContent.Children.Add(CreateSectionTitle("MECM Management"));

        _managementContentHost.Content = CreateManagementPlaceholder();
        _managementContentHost.Margin = new Thickness(0, 12, 0, 0);
        mainContent.Children.Add(_managementContentHost);

        mainPanel.Child = mainContent;

        Grid.SetRow(mainPanel, 2);
        root.Children.Add(mainPanel);

        return root;
    }

    // =========================================================
    // CONNECTION
    // =========================================================

    private async void ConnectButton_Click(
        object sender, RoutedEventArgs e)
    {
        if (_isBusy || _connectionService.IsConnected)
            return;

        string server = _serverBox.Text.Trim();
        string siteCode = _siteCodeBox.Text.Trim();
        string username = _usernameBox.Text.Trim();
        string password = _passwordBox.Password;

        if (string.IsNullOrWhiteSpace(server) ||
            string.IsNullOrWhiteSpace(siteCode) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(
                "Enter the site server, site code, username and password.",
                "MECM connection",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        SetBusy(true);

        _connectionStatus.Text = "Connecting...";
        _connectionStatus.Foreground = BrushFrom("#FCD34D");
        _mainStatus.Text = "Connecting to the MECM SMS Provider...";

        try
        {
            await Task.Run(() =>
            {
                _connectionService.Connect(
                    server,
                    siteCode,
                    username,
                    password);
            });

            _passwordBox.Clear();

            _connectionStatus.Text =
                $"Connected to site {_connectionService.SiteCode}";

            _connectionStatus.Foreground = BrushFrom("#86EFAC");

            _connectedDetails.Text =
                $"Site server: {_connectionService.SiteServer}    |    " +
                $"Site code: {_connectionService.SiteCode}    |    " +
                $"User: {_connectionService.ConnectedUsername}";

            _siteInformation.Text =
                $"Site server: {_connectionService.SiteServer}\n\n" +
                $"Site code: {_connectionService.SiteCode}\n\n" +
                $"Site name: {DisplayValue(_connectionService.SiteName)}\n\n" +
                $"MECM version: {DisplayValue(_connectionService.SiteVersion)}\n\n" +
                $"Connected account: {_connectionService.ConnectedUsername}";

            _mainStatus.Text =
                "Connection successful. MECM management tools can now be added.";

            UpdateConnectionControls(true);

            // The management view must be hosted here; connecting to the SMS
            // Provider alone does not automatically display the device tools.
            _deviceManagementView =
                new MecmDeviceManagementView(_connectionService);

            _managementContentHost.Content = _deviceManagementView;
        }
        catch (Exception ex)
        {
            _connectionStatus.Text = "Connection failed";
            _connectionStatus.Foreground = BrushFrom("#FCA5A5");

            _mainStatus.Text = "Could not connect to the MECM SMS Provider.";

            MessageBox.Show(
                $"Could not connect to MECM.\n\n{ex.Message}\n\n" +
                "Check the site server, site code, credentials, SMS Provider " +
                "availability and account permissions.",
                "MECM connection",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void Disconnect()
    {
        _deviceManagementView?.SetConnectedState(false);
        _deviceManagementView = null;

        _connectionService.Disconnect();
        _managementContentHost.Content = CreateManagementPlaceholder();

        _passwordBox.Clear();

        _connectionStatus.Text = "Not connected";
        _connectionStatus.Foreground = BrushFrom("#FCA5A5");

        _siteInformation.Text =
            "Connect to a MECM site to retrieve site information.";

        _mainStatus.Text = "Not connected to a MECM site.";

        UpdateConnectionControls(false);
    }

    private void UpdateConnectionControls(bool connected)
    {
        if (_connectionPanel is not null)
        {
            _connectionPanel.Visibility = connected
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        if (_connectedPanel is not null)
        {
            _connectedPanel.Visibility = connected
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        _serverBox.IsEnabled = !connected;
        _siteCodeBox.IsEnabled = !connected;
        _usernameBox.IsEnabled = !connected;
        _passwordBox.IsEnabled = !connected;

        _connectButton.IsEnabled = !connected && !_isBusy;
        _disconnectButton.IsEnabled = connected && !_isBusy;
        _connectedDisconnectButton.IsEnabled = connected && !_isBusy;

        UpdateProfileControls();
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;

        _connectButton.IsEnabled =
            !busy && !_connectionService.IsConnected;

        _disconnectButton.IsEnabled =
            !busy && _connectionService.IsConnected;

        _connectedDisconnectButton.IsEnabled =
            !busy && _connectionService.IsConnected;

        UpdateProfileControls();
    }

    // =========================================================
    // SAVED CONNECTIONS
    // =========================================================

    private void LoadConnections()
    {
        try
        {
            if (!File.Exists(_connectionFilePath))
            {
                _profiles = [];
                RefreshProfileList();
                return;
            }

            _profiles =
                JsonSerializer.Deserialize<List<MecmConnectionProfile>>(
                    File.ReadAllText(_connectionFilePath),
                    JsonOptions) ?? [];

            RefreshProfileList();
        }
        catch (Exception ex)
        {
            _profiles = [];

            MessageBox.Show(
                $"Could not load saved MECM connections.\n\n{ex.Message}",
                "Saved connections",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SaveConnections()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_connectionFilePath);

            if (!string.IsNullOrWhiteSpace(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(
                _connectionFilePath,
                JsonSerializer.Serialize(_profiles, JsonOptions));

            UpdateProfileControls();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Could not save MECM connections.\n\n{ex.Message}",
                "Saved connections",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RefreshProfileList(Guid? selectId = null)
    {
        _isLoadingProfile = true;

        try
        {
            _profiles = _profiles
                .OrderByDescending(x => x.IsDefault)
                .ThenBy(x => x.Name)
                .ToList();

            _profileCombo.ItemsSource = null;
            _profileCombo.ItemsSource = _profiles;

            MecmConnectionProfile? selected = selectId.HasValue
                ? _profiles.FirstOrDefault(x => x.Id == selectId.Value)
                : null;

            selected ??= _profiles.FirstOrDefault(x => x.IsDefault);
            selected ??= _profiles.FirstOrDefault();

            _profileCombo.SelectedItem = selected;

            if (selected is not null)
                PopulateConnectionFields(selected);
        }
        finally
        {
            _isLoadingProfile = false;
        }

        UpdateProfileControls();
    }

    private void ProfileCombo_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        if (_isLoadingProfile || _connectionService.IsConnected)
            return;

        if (_profileCombo.SelectedItem is MecmConnectionProfile profile)
            PopulateConnectionFields(profile);

        UpdateProfileControls();
    }

    private void PopulateConnectionFields(MecmConnectionProfile profile)
    {
        _serverBox.Text = profile.SiteServer;
        _siteCodeBox.Text = profile.SiteCode;
        _usernameBox.Text = profile.Username;

        // Passwords are deliberately never loaded from a profile.
        _passwordBox.Clear();
    }

    private void AddProfile()
    {
        if (!ValidateConnectionFields())
            return;

        var dialog = new ProfileNameDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
            return;

        if (_profiles.Any(x => string.Equals(
            x.Name,
            dialog.ProfileName,
            StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show(
                "A connection with this name already exists.");

            return;
        }

        var profile = new MecmConnectionProfile
        {
            Id = Guid.NewGuid(),
            Name = dialog.ProfileName,
            SiteServer = _serverBox.Text.Trim(),
            SiteCode = _siteCodeBox.Text.Trim().ToUpperInvariant(),
            Username = _usernameBox.Text.Trim(),
            IsDefault = _profiles.Count == 0
        };

        _profiles.Add(profile);

        SaveConnections();
        RefreshProfileList(profile.Id);
    }

    private void UpdateProfile()
    {
        if (_profileCombo.SelectedItem is not MecmConnectionProfile profile ||
            !ValidateConnectionFields())
            return;

        profile.SiteServer = _serverBox.Text.Trim();
        profile.SiteCode = _siteCodeBox.Text.Trim().ToUpperInvariant();
        profile.Username = _usernameBox.Text.Trim();

        SaveConnections();
        RefreshProfileList(profile.Id);
    }

    private void DeleteProfile()
    {
        if (_profileCombo.SelectedItem is not MecmConnectionProfile profile)
            return;

        if (MessageBox.Show(
            $"Delete saved connection '{profile.Name}'?",
            "Delete connection",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        bool wasDefault = profile.IsDefault;

        _profiles.RemoveAll(x => x.Id == profile.Id);

        if (wasDefault && _profiles.Count > 0)
            _profiles[0].IsDefault = true;

        SaveConnections();
        RefreshProfileList();
    }

    private void SetDefaultProfile()
    {
        if (_profileCombo.SelectedItem is not MecmConnectionProfile selected)
            return;

        foreach (var profile in _profiles)
            profile.IsDefault = profile.Id == selected.Id;

        SaveConnections();
        RefreshProfileList(selected.Id);
    }

    private bool ValidateConnectionFields()
    {
        if (string.IsNullOrWhiteSpace(_serverBox.Text))
        {
            MessageBox.Show("Enter the MECM site server.");
            return false;
        }

        if (_siteCodeBox.Text.Trim().Length != 3)
        {
            MessageBox.Show("Enter a valid three-character MECM site code.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(_usernameBox.Text))
        {
            MessageBox.Show("Enter the connection username.");
            return false;
        }

        return true;
    }

    private void UpdateProfileControls()
    {
        bool disconnected =
            !_connectionService.IsConnected && !_isBusy;

        bool hasSelection =
            _profileCombo.SelectedItem is MecmConnectionProfile;

        _profileCombo.IsEnabled = disconnected;
        _addProfileButton.IsEnabled = disconnected;
        _updateProfileButton.IsEnabled = disconnected && hasSelection;
        _deleteProfileButton.IsEnabled = disconnected && hasSelection;
        _defaultProfileButton.IsEnabled = disconnected && hasSelection;
    }

    // =========================================================
    // UI HELPERS
    // =========================================================

    private void AddConnectionField(
        Grid grid,
        string label,
        Control control,
        int column,
        string placeholder)
    {
        var panel = new StackPanel
        {
            Margin = new Thickness(7, 0, 7, 0)
        };

        panel.Children.Add(CreateLabel(label));

        if (control is TextBox textBox)
        {
            StyleInput(textBox);
            textBox.MinWidth = 130;
            textBox.ToolTip = placeholder;
        }

        panel.Children.Add(control);

        Grid.SetColumn(panel, column);
        grid.Children.Add(panel);
    }

    private static void AddProfileButton(
        Panel parent,
        Button button,
        string text,
        string color,
        Action action)
    {
        StyleButton(button, text, color);

        button.Margin = new Thickness(4, 0, 0, 0);
        button.Click += (_, _) => action();

        parent.Children.Add(button);
    }

    private static TextBlock CreateManagementPlaceholder() => new()
    {
        Text = "Connect to a MECM site to access device management. " +
               "Collections, client actions and deployments are not yet available.",
        Foreground = BrushFrom(MutedColor),
        FontSize = 13,
        TextWrapping = TextWrapping.Wrap,
        Margin = new Thickness(0, 4, 0, 12)
    };

    private static void AddFeatureRow(
        Panel parent,
        string title,
        string description)
    {
        var row = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 14)
        };

        row.Children.Add(new TextBlock
        {
            Text = title,
            Foreground = BrushFrom(TextColor),
            FontSize = 14,
            FontWeight = FontWeights.SemiBold
        });

        row.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = BrushFrom(MutedColor),
            FontSize = 12,
            Margin = new Thickness(0, 4, 0, 0)
        });

        parent.Children.Add(row);
    }

    private static Border CreatePanel() => new()
    {
        Background = BrushFrom(PanelColor),
        CornerRadius = new CornerRadius(8),
        Padding = new Thickness(16)
    };

    private static TextBlock CreateLabel(string text) => new()
    {
        Text = text,
        Foreground = BrushFrom(MutedColor),
        FontSize = 12,
        Margin = new Thickness(0, 0, 0, 6)
    };

    private static TextBlock CreateSectionTitle(string text) => new()
    {
        Text = text,
        FontSize = 16,
        FontWeight = FontWeights.SemiBold,
        Foreground = Brushes.White
    };

    private static void StyleInput(Control control)
    {
        control.Background = BrushFrom(InputColor);
        control.Foreground = BrushFrom(TextColor);
        control.BorderBrush = BrushFrom("#374151");
        control.BorderThickness = new Thickness(1);
        control.Padding = new Thickness(9, 7, 9, 7);
        control.FontSize = 13;
    }

    private static void StyleButton(
        Button button,
        string text,
        string background)
    {
        button.Content = text;

        if (Application.Current.TryFindResource(typeof(Button)) is Style style)
            button.Style = style;

        button.Background = BrushFrom(background);
        button.BorderBrush = BrushFrom(background);
        button.Foreground = Brushes.White;
    }

    private static string DisplayValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not available" : value;

    private static SolidColorBrush BrushFrom(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));

    // =========================================================
    // DISPOSAL
    // =========================================================

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        _connectionService.Dispose();
        _passwordBox.Clear();
    }

    // =========================================================
    // DATA MODELS
    // =========================================================

    private sealed class MecmConnectionProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string SiteServer { get; set; } = string.Empty;
        public string SiteCode { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool IsDefault { get; set; }

        public override string ToString() => Name;
    }

    // =========================================================
    // PROFILE NAME DIALOG
    // =========================================================

    private sealed class ProfileNameDialog : Window
    {
        private readonly TextBox _nameBox = new();

        public string ProfileName => _nameBox.Text.Trim();

        public ProfileNameDialog()
        {
            Title = "Save MECM Connection";
            Width = 400;
            Height = 215;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            Background = BrushFrom("#111827");
            Foreground = Brushes.White;

            var root = new StackPanel
            {
                Margin = new Thickness(22)
            };

            root.Children.Add(new TextBlock
            {
                Text = "Connection name",
                Foreground = Brushes.White,
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 14)
            });

            StyleInput(_nameBox);
            _nameBox.Text = "New MECM Connection";

            root.Children.Add(_nameBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var cancel = new Button();
            StyleButton(cancel, "Cancel", "#374151");
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += (_, _) => DialogResult = false;

            var save = new Button();
            StyleButton(save, "Save Connection", AccentColor);

            save.Click += (_, _) =>
            {
                if (string.IsNullOrWhiteSpace(ProfileName))
                {
                    MessageBox.Show("Enter a connection name.");
                    return;
                }

                DialogResult = true;
            };

            buttons.Children.Add(cancel);
            buttons.Children.Add(save);

            root.Children.Add(buttons);

            Content = root;

            Loaded += (_, _) =>
            {
                _nameBox.Focus();
                _nameBox.SelectAll();
            };
        }
    }
}