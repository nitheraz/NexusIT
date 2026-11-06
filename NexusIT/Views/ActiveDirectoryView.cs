
using System;
using System.Collections.Generic;
using System.DirectoryServices;
using System.DirectoryServices.AccountManagement;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Effects;

namespace NexusIT.App.Views;

public class ActiveDirectoryView : UserControl, IDisposable
{
    // Connection
    private readonly ComboBox _profileCombo = new();
    private readonly TextBox _domainBox = new();
    private readonly TextBox _serverBox = new();
    private readonly TextBox _usernameBox = new();
    private readonly PasswordBox _passwordBox = new();

    private readonly Button _addProfileButton = new();
    private readonly Button _updateProfileButton = new();
    private readonly Button _deleteProfileButton = new();
    private readonly Button _defaultProfileButton = new();
    private readonly Button _connectButton = new();
    private readonly Button _disconnectButton = new();
    private readonly Button _connectedDisconnectButton = new();

    // Search and mode
    private readonly ComboBox _modeCombo = new();
    private readonly TextBox _searchBox = new();
    private readonly Button _searchButton = new();
    private readonly DataGrid _resultsGrid = new();

    // Details and groups
    private readonly TextBox _detailsBox = new();
    private readonly DataGrid _groupsGrid = new();
    private readonly Button _refreshGroupsButton = new();
    private readonly Button _addToGroupButton = new();
    private readonly Button _removeFromGroupButton = new();

    // User actions
    private readonly Button _enableButton = new();
    private readonly Button _disableButton = new();
    private readonly Button _unlockButton = new();
    private readonly Button _resetPasswordButton = new();

    // Computer actions
    private readonly Button _enableComputerButton = new();
    private readonly Button _disableComputerButton = new();
    private readonly Button _deleteComputerButton = new();

    // Shared actions
    private readonly Button _moveOuButton = new();

    private readonly TextBlock _connectionStatus = new();
    private readonly TextBlock _resultStatus = new();
    private readonly TextBlock _groupStatus = new();
    private readonly TextBlock _connectedDetails = new();

    private Border? _connectionPanel;
    private Border? _connectedStatusPanel;

    private PrincipalContext? _context;
    private UserPrincipal? _selectedUser;
    private ComputerPrincipal? _selectedComputer;

    // Credentials are kept in memory only while connected.
    // They are never written to the saved profile JSON.
    private string? _adUsername;
    private string? _adPassword;
    private string? _ldapServer;
    private string? _domainDn;

    private List<ConnectionProfile> _profiles = [];

    private bool _isBusy;
    private bool _isLoadingProfile;
    private bool _disposed;

    private bool IsComputerMode =>
        _modeCombo.SelectedIndex == 1;

    private readonly string _connectionFilePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NexusIT",
        "ad-connections.json");

    // Glass theme colours
    private const string PanelColor = "#CC1A2B40";
    private const string InputColor = "#99101828";
    private const string TextColor = "#E5E7EB";
    private const string MutedColor = "#A7B4C7";
    private const string AccentColor = "#3B82F6";
    private const string BorderColor = "#665F7896";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public ActiveDirectoryView()
    {
        Background = Brushes.Transparent;
        Content = BuildInterface();

        LoadConnections();
        UpdateConnectionControls(false);
        UpdateProfileControls();
        UpdateModeControls();
    }

    // =========================================================
    // INTERFACE
    // =========================================================

    private UIElement BuildInterface()
    {
        var root = new Grid
        {
            Margin = new Thickness(20),
            Background = Brushes.Transparent
        };

        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });

        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 18)
        };

        header.Children.Add(new TextBlock
        {
            Text = "Active Directory Management",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });

        header.Children.Add(new TextBlock
        {
            Text = "Manage domain users, computers, groups and organisational units.",
            FontSize = 13,
            Foreground = BrushFrom(MutedColor),
            Margin = new Thickness(0, 5, 0, 0)
        });

        root.Children.Add(header);

        // Connection panel
        _connectionPanel = CreatePanel();
        _connectionPanel.Margin = new Thickness(0, 0, 0, 16);

        var connectionContent = new StackPanel();

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

        _profileCombo.MinWidth = 220;
        _profileCombo.Height = 36;
        _profileCombo.DisplayMemberPath = nameof(ConnectionProfile.Name);
        StyleComboBox(_profileCombo);
        _profileCombo.SelectionChanged += ProfileCombo_SelectionChanged;

        profilePanel.Children.Add(_profileCombo);
        profileRow.Children.Add(profilePanel);

        var profileButtons = new WrapPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(10, 0, 0, 0)
        };

        AddProfileButton(profileButtons, _addProfileButton, "Add", AccentColor, AddProfile);
        AddProfileButton(profileButtons, _updateProfileButton, "Update", "#663B506B", UpdateProfile);
        AddProfileButton(profileButtons, _deleteProfileButton, "Delete", "#66991B1B", DeleteProfile);
        AddProfileButton(profileButtons, _defaultProfileButton, "Set Default", "#663B506B", SetDefaultProfile);

        Grid.SetColumn(profileButtons, 1);
        profileRow.Children.Add(profileButtons);
        connectionContent.Children.Add(profileRow);

        var connectionGrid = new Grid();

        for (int i = 0; i < 4; i++)
            connectionGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        connectionGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        AddConnectionField(connectionGrid, "Domain / DNS name", _domainBox, 0, "example.local");
        AddConnectionField(connectionGrid, "Domain controller (optional)", _serverBox, 1, "dc01.example.local");
        AddConnectionField(connectionGrid, "Username", _usernameBox, 2, @"DOMAIN\username");

        var passwordPanel = new StackPanel { Margin = new Thickness(7, 0, 7, 0) };
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

        StyleButton(_disconnectButton, "Disconnect", "#66991B1B");
        _disconnectButton.Margin = new Thickness(0, 6, 0, 0);
        _disconnectButton.Click += (_, _) => Disconnect();
        connectionButtons.Children.Add(_disconnectButton);

        Grid.SetColumn(connectionButtons, 4);
        connectionGrid.Children.Add(connectionButtons);

        connectionContent.Children.Add(connectionGrid);

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

        // Connected status panel
        _connectedStatusPanel = CreatePanel();
        _connectedStatusPanel.Margin = new Thickness(0, 0, 0, 16);
        _connectedStatusPanel.Visibility = Visibility.Collapsed;

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

        StyleButton(_connectedDisconnectButton, "Disconnect", "#66991B1B");
        _connectedDisconnectButton.Click += (_, _) => Disconnect();

        Grid.SetColumn(_connectedDisconnectButton, 2);
        connectedGrid.Children.Add(_connectedDisconnectButton);

        _connectedStatusPanel.Child = connectedGrid;

        Grid.SetRow(_connectedStatusPanel, 1);
        root.Children.Add(_connectedStatusPanel);

        // Management area
        var managementGrid = new Grid
        {
            Background = Brushes.Transparent
        };

        managementGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1.25, GridUnitType.Star) });

        managementGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        managementGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        managementGrid.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        managementGrid.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

        // Search panel
        var searchPanel = CreatePanel();
        searchPanel.Margin = new Thickness(0, 0, 8, 0);

        var searchContent = new StackPanel();

        var modeRow = new Grid();
        modeRow.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        modeRow.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        modeRow.Children.Add(CreateSectionTitle("Directory search"));

        _modeCombo.Width = 125;
        _modeCombo.Height = 32;
        _modeCombo.Items.Add("Users");
        _modeCombo.Items.Add("Computers");
        _modeCombo.SelectedIndex = 0;
        StyleComboBox(_modeCombo);
        _modeCombo.SelectionChanged += (_, _) => ChangeMode();

        Grid.SetColumn(_modeCombo, 1);
        modeRow.Children.Add(_modeCombo);

        searchContent.Children.Add(modeRow);

        var searchRow = new Grid { Margin = new Thickness(0, 12, 0, 10) };

        searchRow.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        searchRow.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        StyleInput(_searchBox);
        _searchBox.MinWidth = 180;
        _searchBox.VerticalContentAlignment = VerticalAlignment.Center;
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
                SearchDirectory();
        };

        searchRow.Children.Add(_searchBox);

        StyleButton(_searchButton, "Search", AccentColor);
        _searchButton.Margin = new Thickness(8, 0, 0, 0);
        _searchButton.Click += (_, _) => SearchDirectory();

        Grid.SetColumn(_searchButton, 1);
        searchRow.Children.Add(_searchButton);

        searchContent.Children.Add(searchRow);

        _resultStatus.Text = "Connect to Active Directory to search.";
        _resultStatus.Foreground = BrushFrom(MutedColor);
        _resultStatus.Margin = new Thickness(0, 0, 0, 10);
        _resultStatus.TextWrapping = TextWrapping.Wrap;
        searchContent.Children.Add(_resultStatus);

        ConfigureDataGrid(_resultsGrid);
        _resultsGrid.SelectionChanged += ResultsGrid_SelectionChanged;

        searchContent.Children.Add(_resultsGrid);
        searchPanel.Child = searchContent;

        Grid.SetRow(searchPanel, 1);
        managementGrid.Children.Add(searchPanel);

        // Details panel
        var detailsPanel = CreatePanel();
        detailsPanel.Margin = new Thickness(8, 0, 8, 0);

        var detailsContent = new StackPanel();
        detailsContent.Children.Add(CreateSectionTitle("Account details"));

        _detailsBox.IsReadOnly = true;
        _detailsBox.AcceptsReturn = true;
        _detailsBox.TextWrapping = TextWrapping.Wrap;
        _detailsBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        StyleInput(_detailsBox);
        _detailsBox.Padding = new Thickness(10);
        _detailsBox.FontSize = 13;
        _detailsBox.Height = 330;
        _detailsBox.Margin = new Thickness(0, 12, 0, 12);
        _detailsBox.Text = "Select an account to view its details.";

        detailsContent.Children.Add(_detailsBox);

        var groupHeading = CreateSectionTitle("Group memberships");
        groupHeading.Margin = new Thickness(0, 8, 0, 0);
        detailsContent.Children.Add(groupHeading);

        var groupButtons = new WrapPanel { Margin = new Thickness(0, 8, 0, 8) };

        StyleButton(_refreshGroupsButton, "Refresh", "#663B506B");
        _refreshGroupsButton.Margin = new Thickness(0, 0, 6, 0);
        _refreshGroupsButton.Click += (_, _) => LoadGroupMemberships();

        StyleButton(_addToGroupButton, "Add to group", AccentColor);
        _addToGroupButton.Margin = new Thickness(0, 0, 6, 0);
        _addToGroupButton.Click += (_, _) => AddUserToGroup();

        StyleButton(_removeFromGroupButton, "Remove", "#66991B1B");
        _removeFromGroupButton.Click += (_, _) => RemoveUserFromGroup();

        groupButtons.Children.Add(_refreshGroupsButton);
        groupButtons.Children.Add(_addToGroupButton);
        groupButtons.Children.Add(_removeFromGroupButton);

        detailsContent.Children.Add(groupButtons);

        _groupStatus.Text = "Select a user to view group memberships.";
        _groupStatus.Foreground = BrushFrom(MutedColor);
        _groupStatus.TextWrapping = TextWrapping.Wrap;
        _groupStatus.Margin = new Thickness(0, 0, 0, 8);
        detailsContent.Children.Add(_groupStatus);

        ConfigureDataGrid(_groupsGrid);
        _groupsGrid.Height = 180;
        _groupsGrid.SelectionChanged += (_, _) => UpdateGroupControls();

        _groupsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Group",
            Binding = new System.Windows.Data.Binding(nameof(GroupRow.Name)),
            Width = new DataGridLength(1.5, DataGridLengthUnitType.Star)
        });

        _groupsGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Type",
            Binding = new System.Windows.Data.Binding(nameof(GroupRow.Type)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        detailsContent.Children.Add(_groupsGrid);
        detailsPanel.Child = detailsContent;

        Grid.SetColumn(detailsPanel, 1);
        Grid.SetRow(detailsPanel, 1);
        managementGrid.Children.Add(detailsPanel);

        // Actions panel
        var actionsPanel = CreatePanel();
        actionsPanel.Margin = new Thickness(8, 0, 0, 0);

        var actionsContent = new StackPanel();
        actionsContent.Children.Add(CreateSectionTitle("Account actions"));

        AddActionButton(actionsContent, _enableButton, "Enable user", "#663B9B65",
            (_, _) => ChangeUserEnabled(true));

        AddActionButton(actionsContent, _disableButton, "Disable user", "#66991B1B",
            (_, _) => ChangeUserEnabled(false));

        AddActionButton(actionsContent, _unlockButton, "Unlock user", "#663B506B",
            (_, _) => UnlockSelectedUser());

        AddActionButton(actionsContent, _resetPasswordButton, "Reset password", AccentColor,
            (_, _) => ResetSelectedPassword());

        AddActionButton(actionsContent, _enableComputerButton, "Enable computer", "#663B9B65",
            (_, _) => ChangeComputerEnabled(true));

        AddActionButton(actionsContent, _disableComputerButton, "Disable computer", "#66991B1B",
            (_, _) => ChangeComputerEnabled(false));

        AddActionButton(actionsContent, _deleteComputerButton, "Delete computer", "#667F1D1D",
            (_, _) => DeleteSelectedComputer());

        AddActionButton(actionsContent, _moveOuButton, "Move to OU", "#663B506B",
            (_, _) => MoveSelectedObject());

        actionsContent.Children.Add(new TextBlock
        {
            Text = "Actions require sufficient Active Directory permissions. Computer deletion is permanent.",
            TextWrapping = TextWrapping.Wrap,
            Foreground = BrushFrom(MutedColor),
            FontSize = 12,
            Margin = new Thickness(0, 18, 0, 0)
        });

        actionsPanel.Child = actionsContent;

        Grid.SetColumn(actionsPanel, 2);
        Grid.SetRow(actionsPanel, 1);
        managementGrid.Children.Add(actionsPanel);

        Grid.SetRow(managementGrid, 2);
        root.Children.Add(managementGrid);

        return root;
    }

    // =========================================================
    // MODE MANAGEMENT
    // =========================================================

    private void ChangeMode()
    {
        _selectedUser?.Dispose();
        _selectedUser = null;

        _selectedComputer?.Dispose();
        _selectedComputer = null;

        _resultsGrid.ItemsSource = null;
        _resultsGrid.Columns.Clear();

        _groupsGrid.ItemsSource = null;
        _detailsBox.Text = IsComputerMode
            ? "Select a computer to view its details."
            : "Select a user to view their details.";

        _groupStatus.Text = IsComputerMode
            ? "Group membership management is available in Users mode."
            : "Select a user to view group memberships.";

        if (IsComputerMode)
        {
            _searchBox.ToolTip = "Search by computer name or computer account name.";
            _searchBox.Text = string.Empty;
            _resultStatus.Text = "Search for a computer by name or account name.";

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Computer",
                Binding = new System.Windows.Data.Binding(nameof(ComputerRow.Name)),
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "DNS hostname",
                Binding = new System.Windows.Data.Binding(nameof(ComputerRow.DnsHostName)),
                Width = new DataGridLength(1.4, DataGridLengthUnitType.Star)
            });

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Operating system",
                Binding = new System.Windows.Data.Binding(nameof(ComputerRow.OperatingSystem)),
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Enabled",
                Binding = new System.Windows.Data.Binding(nameof(ComputerRow.Enabled)),
                Width = new DataGridLength(65)
            });
        }
        else
        {
            _searchBox.ToolTip = "Search by display name, username or email address.";
            _searchBox.Text = string.Empty;
            _resultStatus.Text = "Search for a user to get started.";

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Name",
                Binding = new System.Windows.Data.Binding(nameof(UserRow.Name)),
                Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
            });

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Username",
                Binding = new System.Windows.Data.Binding(nameof(UserRow.Username)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Email",
                Binding = new System.Windows.Data.Binding(nameof(UserRow.Email)),
                Width = new DataGridLength(1.3, DataGridLengthUnitType.Star)
            });

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Enabled",
                Binding = new System.Windows.Data.Binding(nameof(UserRow.Enabled)),
                Width = new DataGridLength(65)
            });
        }

        UpdateModeControls();
        UpdateActionControls();
        UpdateGroupControls();
    }

    private void UpdateModeControls()
    {
        bool connected = _context is not null;
        bool userMode = !IsComputerMode;

        _modeCombo.IsEnabled = connected && !_isBusy;
        _searchBox.IsEnabled = connected && !_isBusy;
        _searchButton.IsEnabled = connected && !_isBusy;

        _enableButton.Visibility = userMode ? Visibility.Visible : Visibility.Collapsed;
        _disableButton.Visibility = userMode ? Visibility.Visible : Visibility.Collapsed;
        _unlockButton.Visibility = userMode ? Visibility.Visible : Visibility.Collapsed;
        _resetPasswordButton.Visibility = userMode ? Visibility.Visible : Visibility.Collapsed;

        _enableComputerButton.Visibility = userMode ? Visibility.Collapsed : Visibility.Visible;
        _disableComputerButton.Visibility = userMode ? Visibility.Collapsed : Visibility.Visible;
        _deleteComputerButton.Visibility = userMode ? Visibility.Collapsed : Visibility.Visible;

        _refreshGroupsButton.Visibility = userMode ? Visibility.Visible : Visibility.Collapsed;
        _addToGroupButton.Visibility = userMode ? Visibility.Visible : Visibility.Collapsed;
        _removeFromGroupButton.Visibility = userMode ? Visibility.Visible : Visibility.Collapsed;

        UpdateActionControls();
        UpdateGroupControls();
    }

    // =========================================================
    // CONNECTION
    // =========================================================

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isBusy)
            return;

        string domain = _domainBox.Text.Trim();
        string server = _serverBox.Text.Trim();
        string username = _usernameBox.Text.Trim();
        string password = _passwordBox.Password;

        if (string.IsNullOrWhiteSpace(domain) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(
                "Enter the domain, username and password.",
                "Active Directory",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        SetBusy(true);
        _connectionStatus.Text = "Connecting...";
        _connectionStatus.Foreground = BrushFrom("#FCD34D");

        try
        {
            var connection = await Task.Run(() =>
            {
                var context = new PrincipalContext(
                    ContextType.Domain,
                    string.IsNullOrWhiteSpace(server) ? domain : server,
                    username,
                    password);

                if (!context.ValidateCredentials(username, password))
                {
                    context.Dispose();
                    throw new UnauthorizedAccessException(
                        "Active Directory rejected the supplied credentials.");
                }

                string dc = string.IsNullOrWhiteSpace(server)
                    ? context.ConnectedServer
                    : server;

                string rootDsePath = $"LDAP://{dc}/RootDSE";

                using var rootDse = new DirectoryEntry(
                    rootDsePath,
                    username,
                    password,
                    AuthenticationTypes.Secure);

                string namingContext =
                    rootDse.Properties["defaultNamingContext"].Value?.ToString()
                    ?? throw new InvalidOperationException(
                        "Could not read the domain naming context.");

                using var domainRoot = new DirectoryEntry(
                    $"LDAP://{dc}/{namingContext}",
                    username,
                    password,
                    AuthenticationTypes.Secure);

                domainRoot.RefreshCache();

                return (context, dc, namingContext);
            });

            Disconnect();

            _context = connection.context;
            _ldapServer = connection.dc;
            _domainDn = connection.namingContext;
            _adUsername = username;
            _adPassword = password;

            _passwordBox.Clear();

            _connectionStatus.Text = $"Connected to {domain}";
            _connectionStatus.Foreground = BrushFrom("#86EFAC");

            UpdateConnectionControls(true);

            _resultStatus.Text =
                "Connected. Select Users or Computers and search.";
        }
        catch (Exception ex)
        {
            _connectionStatus.Text = "Connection failed";
            _connectionStatus.Foreground = BrushFrom("#FCA5A5");

            MessageBox.Show(
                $"Could not connect to Active Directory.\n\n{ex.Message}",
                "Active Directory connection",
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
        _selectedUser?.Dispose();
        _selectedUser = null;

        _selectedComputer?.Dispose();
        _selectedComputer = null;

        _context?.Dispose();
        _context = null;

        _adUsername = null;
        _adPassword = null;
        _ldapServer = null;
        _domainDn = null;

        _connectionStatus.Text = "Not connected";
        _connectionStatus.Foreground = BrushFrom("#FCA5A5");

        _passwordBox.Clear();

        UpdateConnectionControls(false);
    }

    private void UpdateConnectionControls(bool connected)
    {
        if (_connectionPanel is not null)
            _connectionPanel.Visibility = connected
                ? Visibility.Collapsed : Visibility.Visible;

        if (_connectedStatusPanel is not null)
            _connectedStatusPanel.Visibility = connected
                ? Visibility.Visible : Visibility.Collapsed;

        if (connected)
        {
            _connectedDetails.Text =
                $"Domain: {_domainBox.Text.Trim()}    |    " +
                $"User: {_usernameBox.Text.Trim()}    |    " +
                $"Domain Controller: {_ldapServer}";
        }

        _domainBox.IsEnabled = !connected;
        _serverBox.IsEnabled = !connected;
        _usernameBox.IsEnabled = !connected;
        _passwordBox.IsEnabled = !connected;

        _connectButton.IsEnabled = !connected;
        _disconnectButton.IsEnabled = connected;
        _connectedDisconnectButton.IsEnabled = connected;

        if (!connected)
        {
            _resultsGrid.ItemsSource = null;
            _groupsGrid.ItemsSource = null;

            _detailsBox.Text = "Connect and select an account to view its details.";
            _resultStatus.Text = "Connect to Active Directory to search.";
            _groupStatus.Text = "Select a user to view group memberships.";
        }

        UpdateModeControls();
        UpdateProfileControls();
    }

    private void SetBusy(bool busy)
    {
        _isBusy = busy;

        _connectButton.IsEnabled = !busy && _context is null;
        _disconnectButton.IsEnabled = !busy && _context is not null;
        _connectedDisconnectButton.IsEnabled = !busy && _context is not null;

        UpdateProfileControls();
        UpdateModeControls();
    }

    // =========================================================
    // SEARCH
    // =========================================================

    private async void SearchDirectory()
    {
        if (_context is null || _isBusy)
            return;

        string query = _searchBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            MessageBox.Show(
                "Enter a search term.",
                "Active Directory",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        SetBusy(true);
        _resultsGrid.ItemsSource = null;
        _resultStatus.Text = "Searching...";

        ClearSelection();

        try
        {
            PrincipalContext context = _context;

            if (IsComputerMode)
            {
                var rows = await Task.Run(() => SearchComputers(context, query));
                _resultsGrid.ItemsSource = rows;
                _resultStatus.Text =
                    $"{rows.Count} computer(s) found. Maximum 500 results.";
            }
            else
            {
                var rows = await Task.Run(() => SearchUsers(context, query));
                _resultsGrid.ItemsSource = rows;
                _resultStatus.Text =
                    $"{rows.Count} user(s) found. Maximum 500 results.";
            }
        }
        catch (Exception ex)
        {
            _resultStatus.Text = "Search failed.";

            MessageBox.Show(
                $"The Active Directory search failed.\n\n{ex.Message}",
                "Active Directory search",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            SetBusy(false);
        }
    }

    private static List<UserRow> SearchUsers(
        PrincipalContext context, string query)
    {
        var matches = new Dictionary<string, UserRow>(
            StringComparer.OrdinalIgnoreCase);

        string[] properties =
        [
            "Name",
            "DisplayName",
            "SamAccountName",
            "EmailAddress"
        ];

        foreach (string property in properties)
        {
            using var queryUser = new UserPrincipal(context);

            switch (property)
            {
                case "Name":
                    queryUser.Name = $"*{query}*";
                    break;
                case "DisplayName":
                    queryUser.DisplayName = $"*{query}*";
                    break;
                case "SamAccountName":
                    queryUser.SamAccountName = $"*{query}*";
                    break;
                case "EmailAddress":
                    queryUser.EmailAddress = $"*{query}*";
                    break;
            }

            using var searcher = new PrincipalSearcher(queryUser);

            foreach (var result in searcher.FindAll())
            {
                if (result is not UserPrincipal user)
                {
                    result.Dispose();
                    continue;
                }

                using (user)
                using (var entry = user.GetUnderlyingObject() as DirectoryEntry)
                {
                    string username = user.SamAccountName ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(username))
                        continue;

                    string name = user.DisplayName;
                    if (string.IsNullOrWhiteSpace(name))
                        name = user.Name ?? string.Empty;

                    string dn = user.DistinguishedName ?? string.Empty;

                    matches[username] = new UserRow
                    {
                        Name = name,
                        Username = username,
                        Email = user.EmailAddress ?? string.Empty,
                        Department = ReadAttribute(entry, "department"),
                        JobTitle = ReadAttribute(entry, "title"),
                        DistinguishedName = dn,
                        OrganizationalUnit = GetOrganizationalUnit(dn),
                        Enabled = user.Enabled.GetValueOrDefault() ? "Yes" : "No",
                        LockedOut = SafeLockedOut(user) ? "Yes" : "No",
                        LastLogon = ReadLastLogon(entry),
                        PasswordExpired = ReadPasswordExpired(entry)
                    };
                }

                if (matches.Count >= 500)
                    break;
            }

            if (matches.Count >= 500)
                break;
        }

        return matches.Values.OrderBy(x => x.Name).ToList();
    }

    private static List<ComputerRow> SearchComputers(
        PrincipalContext context, string query)
    {
        var matches = new Dictionary<string, ComputerRow>(
            StringComparer.OrdinalIgnoreCase);

        string[] properties = ["Name", "SamAccountName"];

        foreach (string property in properties)
        {
            using var queryComputer = new ComputerPrincipal(context);

            if (property == "Name")
                queryComputer.Name = $"*{query}*";
            else
                queryComputer.SamAccountName = $"*{query}*";

            using var searcher = new PrincipalSearcher(queryComputer);

            foreach (var result in searcher.FindAll())
            {
                if (result is not ComputerPrincipal computer)
                {
                    result.Dispose();
                    continue;
                }

                using (computer)
                using (var entry = computer.GetUnderlyingObject() as DirectoryEntry)
                {
                    string name = computer.Name ?? string.Empty;

                    if (string.IsNullOrWhiteSpace(name))
                        continue;

                    string dn = computer.DistinguishedName ?? string.Empty;

                    matches[name] = new ComputerRow
                    {
                        Name = name,
                        SamAccountName = computer.SamAccountName ?? string.Empty,
                        DnsHostName = ReadAttribute(entry, "dNSHostName"),
                        OperatingSystem = ReadAttribute(entry, "operatingSystem"),
                        OperatingSystemVersion = ReadAttribute(entry, "operatingSystemVersion"),
                        Description = ReadAttribute(entry, "description"),
                        Enabled = computer.Enabled.GetValueOrDefault() ? "Yes" : "No",
                        LastLogon = ReadLastLogon(entry),
                        DistinguishedName = dn,
                        OrganizationalUnit = GetOrganizationalUnit(dn)
                    };
                }

                if (matches.Count >= 500)
                    break;
            }

            if (matches.Count >= 500)
                break;
        }

        return matches.Values.OrderBy(x => x.Name).ToList();
    }

    private void ClearSelection()
    {
        _selectedUser?.Dispose();
        _selectedUser = null;

        _selectedComputer?.Dispose();
        _selectedComputer = null;

        _groupsGrid.ItemsSource = null;

        _detailsBox.Text = IsComputerMode
            ? "Select a computer to view its details."
            : "Select a user to view their details.";

        _groupStatus.Text = "Select a user to view group memberships.";

        UpdateActionControls();
        UpdateGroupControls();
    }

    // =========================================================
    // SELECTION AND DETAILS
    // =========================================================

    private void ResultsGrid_SelectionChanged(
        object sender, SelectionChangedEventArgs e)
    {
        _selectedUser?.Dispose();
        _selectedUser = null;

        _selectedComputer?.Dispose();
        _selectedComputer = null;

        _groupsGrid.ItemsSource = null;

        if (_context is null)
        {
            UpdateActionControls();
            UpdateGroupControls();
            return;
        }

        try
        {
            if (IsComputerMode &&
                _resultsGrid.SelectedItem is ComputerRow computerRow)
            {
                LoadSelectedComputer(computerRow);
            }
            else if (!IsComputerMode &&
                     _resultsGrid.SelectedItem is UserRow userRow)
            {
                LoadSelectedUser(userRow);
            }
            else
            {
                _detailsBox.Text = "Select an account to view its details.";
            }
        }
        catch (Exception ex)
        {
            _detailsBox.Text = $"Could not load account details.\n\n{ex.Message}";
        }

        UpdateActionControls();
        UpdateGroupControls();
    }

    private void LoadSelectedUser(UserRow row)
    {
        using var identity = new UserPrincipal(_context!)
        {
            SamAccountName = row.Username
        };

        using var searcher = new PrincipalSearcher(identity);
        _selectedUser = searcher.FindOne() as UserPrincipal;

        if (_selectedUser is null)
        {
            _detailsBox.Text = "The selected user could not be found.";
            return;
        }

        _detailsBox.Text = BuildUserDetails(row);
        LoadGroupMemberships();
    }

    private void LoadSelectedComputer(ComputerRow row)
    {
        using var identity = new ComputerPrincipal(_context!)
        {
            SamAccountName = row.SamAccountName
        };

        using var searcher = new PrincipalSearcher(identity);
        _selectedComputer = searcher.FindOne() as ComputerPrincipal;

        if (_selectedComputer is null)
        {
            _detailsBox.Text = "The selected computer could not be found.";
            return;
        }

        _detailsBox.Text = BuildComputerDetails(row);
        _groupStatus.Text = "Group membership management is available in Users mode.";
    }

    private static string BuildUserDetails(UserRow user)
    {
        return
            "GENERAL INFORMATION\n" +
            "────────────────────────\n\n" +
            $"Display name: {DisplayValue(user.Name)}\n\n" +
            $"Username: {DisplayValue(user.Username)}\n\n" +
            $"Email: {DisplayValue(user.Email)}\n\n" +
            "ORGANISATION\n" +
            "────────────────────────\n\n" +
            $"Department: {DisplayValue(user.Department)}\n\n" +
            $"Job title: {DisplayValue(user.JobTitle)}\n\n" +
            $"Organisational unit: {DisplayValue(user.OrganizationalUnit)}\n\n" +
            "ACCOUNT STATUS\n" +
            "────────────────────────\n\n" +
            $"Account: {(user.Enabled == "Yes" ? "Enabled" : "Disabled")}\n\n" +
            $"Lockout: {(user.LockedOut == "Yes" ? "Locked" : "Not locked")}\n\n" +
            $"Password expired: {DisplayValue(user.PasswordExpired)}\n\n" +
            "ACTIVITY\n" +
            "────────────────────────\n\n" +
            $"Last logon (replicated): {DisplayValue(user.LastLogon)}\n\n" +
            "DIRECTORY\n" +
            "────────────────────────\n\n" +
            $"Distinguished Name:\n{DisplayValue(user.DistinguishedName)}";
    }

    private static string BuildComputerDetails(ComputerRow computer)
    {
        return
            "COMPUTER INFORMATION\n" +
            "────────────────────────\n\n" +
            $"Computer name: {DisplayValue(computer.Name)}\n\n" +
            $"Account name: {DisplayValue(computer.SamAccountName)}\n\n" +
            $"DNS hostname: {DisplayValue(computer.DnsHostName)}\n\n" +
            "OPERATING SYSTEM\n" +
            "────────────────────────\n\n" +
            $"Operating system: {DisplayValue(computer.OperatingSystem)}\n\n" +
            $"OS version: {DisplayValue(computer.OperatingSystemVersion)}\n\n" +
            "ACCOUNT STATUS\n" +
            "────────────────────────\n\n" +
            $"Enabled: {computer.Enabled}\n\n" +
            $"Description: {DisplayValue(computer.Description)}\n\n" +
            "ACTIVITY\n" +
            "────────────────────────\n\n" +
            $"Last logon (replicated): {DisplayValue(computer.LastLogon)}\n\n" +
            "ORGANISATION\n" +
            "────────────────────────\n\n" +
            $"Organisational unit: {DisplayValue(computer.OrganizationalUnit)}\n\n" +
            "DIRECTORY\n" +
            "────────────────────────\n\n" +
            $"Distinguished Name:\n{DisplayValue(computer.DistinguishedName)}";
    }

    private static string DisplayValue(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not available" : value;

    // =========================================================
    // COMPUTER ACTIONS
    // =========================================================

    private void ChangeComputerEnabled(bool enabled)
    {
        if (_context is null || _selectedComputer is null || !IsComputerMode)
            return;

        string action = enabled ? "enable" : "disable";
        string name = _selectedComputer.Name ?? "selected computer";

        if (MessageBox.Show(
            $"Are you sure you want to {action} the computer account '{name}'?",
            "Confirm computer account change",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            _selectedComputer.Enabled = enabled;
            _selectedComputer.Save();

            MessageBox.Show(
                $"Computer account '{name}' has been {action}d.",
                "Active Directory",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            SearchDirectory();
        }
        catch (Exception ex)
        {
            ShowOperationError($"Could not {action} the computer account.", ex);
        }
    }

    private void DeleteSelectedComputer()
    {
        if (_context is null || _selectedComputer is null || !IsComputerMode)
            return;

        string name = _selectedComputer.Name ?? "selected computer";

        if (MessageBox.Show(
            $"You are about to permanently delete the computer account '{name}'.\n\n" +
            "This action cannot be undone.\n\nDo you want to continue?",
            "Confirm computer deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            _selectedComputer.Delete();
            _selectedComputer.Dispose();
            _selectedComputer = null;

            _detailsBox.Text = "The computer account has been deleted.";

            MessageBox.Show(
                $"Computer account '{name}' has been deleted.",
                "Active Directory",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            SearchDirectory();
        }
        catch (Exception ex)
        {
            ShowOperationError("Could not delete the computer account.", ex);
        }
    }

    // =========================================================
    // USER ACTIONS
    // =========================================================

    private void ChangeUserEnabled(bool enabled)
    {
        if (_context is null || _selectedUser is null || IsComputerMode)
            return;

        string action = enabled ? "enable" : "disable";
        string username = _selectedUser.SamAccountName ?? "selected user";

        if (MessageBox.Show(
            $"Are you sure you want to {action} the user account '{username}'?",
            "Confirm account change",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            _selectedUser.Enabled = enabled;
            _selectedUser.Save();
            SearchDirectory();
        }
        catch (Exception ex)
        {
            ShowOperationError($"Could not {action} the user account.", ex);
        }
    }

    private void UnlockSelectedUser()
    {
        if (_context is null || _selectedUser is null || IsComputerMode)
            return;

        try
        {
            _selectedUser.UnlockAccount();
            _selectedUser.Save();
            SearchDirectory();
        }
        catch (Exception ex)
        {
            ShowOperationError("Could not unlock the user account.", ex);
        }
    }

    private void ResetSelectedPassword()
    {
        if (_context is null || _selectedUser is null || IsComputerMode)
            return;

        var dialog = new PasswordResetDialog(
            _selectedUser.SamAccountName ?? "Selected user")
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
            return;

        try
        {
            _selectedUser.SetPassword(dialog.NewPassword);
            _selectedUser.Save();

            MessageBox.Show(
                "The password was reset successfully. Nexus IT has not stored the new password.",
                "Password reset",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            SearchDirectory();
        }
        catch (Exception ex)
        {
            ShowOperationError("Could not reset the password.", ex);
        }
        finally
        {
            dialog.ClearPasswords();
        }
    }

    // =========================================================
    // GROUP MEMBERSHIP
    // =========================================================

    private void LoadGroupMemberships()
    {
        if (_context is null || _selectedUser is null || IsComputerMode)
        {
            UpdateGroupControls();
            return;
        }

        try
        {
            var rows = new List<GroupRow>();

            using var groups = _selectedUser.GetGroups();

            foreach (Principal principal in groups)
            {
                using (principal)
                {
                    if (principal is not GroupPrincipal group)
                        continue;

                    rows.Add(new GroupRow
                    {
                        Name = group.Name ?? "(Unnamed group)",
                        Type = group.IsSecurityGroup.GetValueOrDefault()
                            ? "Security" : "Distribution",
                        Scope = group.GroupScope?.ToString() ?? "Unknown",
                        DistinguishedName = group.DistinguishedName ?? string.Empty
                    });
                }
            }

            _groupsGrid.ItemsSource = rows.OrderBy(x => x.Name).ToList();
            _groupStatus.Text = $"{rows.Count} direct group membership(s) found.";
        }
        catch (Exception ex)
        {
            _groupStatus.Text = "Could not load group memberships.";
            ShowOperationError("Could not retrieve group memberships.", ex);
        }

        UpdateGroupControls();
    }

    private void AddUserToGroup()
    {
        if (_context is null || _selectedUser is null || IsComputerMode)
            return;

        var dialog = new GroupSelectionDialog(_context)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true || dialog.SelectedGroup is null)
            return;

        GroupRow selected = dialog.SelectedGroup;
        string username = _selectedUser.SamAccountName ?? "Selected user";

        if (MessageBox.Show(
            $"Add '{username}' to '{selected.Name}'?",
            "Confirm group membership",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        try
        {
            using GroupPrincipal? group = FindGroup(selected.DistinguishedName);

            if (group is null)
                throw new InvalidOperationException("The group could not be found.");

            if (!group.Members.Contains(_selectedUser))
            {
                group.Members.Add(_selectedUser);
                group.Save();
            }

            LoadGroupMemberships();
        }
        catch (Exception ex)
        {
            ShowOperationError("Could not add the user to the group.", ex);
        }
    }

    private void RemoveUserFromGroup()
    {
        if (_context is null || _selectedUser is null || IsComputerMode)
            return;

        if (_groupsGrid.SelectedItem is not GroupRow selected)
        {
            MessageBox.Show("Select a group membership first.");
            return;
        }

        string username = _selectedUser.SamAccountName ?? "Selected user";

        if (MessageBox.Show(
            $"Remove '{username}' from '{selected.Name}'?",
            "Confirm group removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            using GroupPrincipal? group = FindGroup(selected.DistinguishedName);

            if (group is null)
                throw new InvalidOperationException("The group could not be found.");

            if (group.Members.Contains(_selectedUser))
            {
                group.Members.Remove(_selectedUser);
                group.Save();
            }

            LoadGroupMemberships();
        }
        catch (Exception ex)
        {
            ShowOperationError("Could not remove the user from the group.", ex);
        }
    }

    private GroupPrincipal? FindGroup(string distinguishedName)
    {
        if (_context is null || string.IsNullOrWhiteSpace(distinguishedName))
            return null;

        return GroupPrincipal.FindByIdentity(
            _context,
            IdentityType.DistinguishedName,
            distinguishedName);
    }

    // =========================================================
    // GROUP AND ACTION CONTROL STATES
    // =========================================================

    private void UpdateGroupControls()
    {
        bool hasUser = _context is not null &&
                       _selectedUser is not null &&
                       !IsComputerMode;

        _refreshGroupsButton.IsEnabled = hasUser && !_isBusy;
        _addToGroupButton.IsEnabled = hasUser && !_isBusy;
        _removeFromGroupButton.IsEnabled =
            hasUser && _groupsGrid.SelectedItem is GroupRow && !_isBusy;
    }

    private void UpdateActionControls()
    {
        bool hasUser = _context is not null &&
                       _selectedUser is not null &&
                       !IsComputerMode;

        bool hasComputer = _context is not null &&
                           _selectedComputer is not null &&
                           IsComputerMode;

        _enableButton.IsEnabled =
            hasUser && !_isBusy && _selectedUser?.Enabled != true;

        _disableButton.IsEnabled =
            hasUser && !_isBusy && _selectedUser?.Enabled == true;

        _unlockButton.IsEnabled =
            hasUser && !_isBusy && SafeLockedOut(_selectedUser);

        _resetPasswordButton.IsEnabled = hasUser && !_isBusy;

        _enableComputerButton.IsEnabled =
            hasComputer && !_isBusy && _selectedComputer?.Enabled != true;

        _disableComputerButton.IsEnabled =
            hasComputer && !_isBusy && _selectedComputer?.Enabled == true;

        _deleteComputerButton.IsEnabled = hasComputer && !_isBusy;

        _moveOuButton.IsEnabled =
            !_isBusy && (hasUser || hasComputer);
    }

    // =========================================================
    // MOVE USER OR COMPUTER TO OU
    // =========================================================

    private void MoveSelectedObject()
    {
        if (_context is null ||
            _adUsername is null ||
            _adPassword is null ||
            _ldapServer is null ||
            _domainDn is null)
            return;

        string? distinguishedName = null;
        string objectName = string.Empty;

        if (!IsComputerMode && _selectedUser is not null)
        {
            distinguishedName = _selectedUser.DistinguishedName;
            objectName = _selectedUser.SamAccountName ?? "Selected user";
        }
        else if (IsComputerMode && _selectedComputer is not null)
        {
            distinguishedName = _selectedComputer.DistinguishedName;
            objectName = _selectedComputer.Name ?? "Selected computer";
        }

        if (string.IsNullOrWhiteSpace(distinguishedName))
            return;

        var dialog = new OuSelectionDialog(
            _ldapServer,
            _domainDn,
            _adUsername,
            _adPassword)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true ||
            string.IsNullOrWhiteSpace(dialog.SelectedOuDn))
            return;

        string targetDn = dialog.SelectedOuDn;

        if (string.Equals(
            GetParentDn(distinguishedName),
            targetDn,
            StringComparison.OrdinalIgnoreCase))
        {
            MessageBox.Show(
                "The account is already in the selected OU.",
                "Move account",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        if (MessageBox.Show(
            $"Move '{objectName}' to the following destination?\n\n" +
            $"{dialog.SelectedOuName}\n\n" +
            $"{targetDn}",
            "Confirm OU move",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning) != MessageBoxResult.Yes)
            return;

        try
        {
            using var source = new DirectoryEntry(
                $"LDAP://{_ldapServer}/{distinguishedName}",
                _adUsername,
                _adPassword,
                AuthenticationTypes.Secure);

            using var destination = new DirectoryEntry(
                $"LDAP://{_ldapServer}/{targetDn}",
                _adUsername,
                _adPassword,
                AuthenticationTypes.Secure);

            source.MoveTo(destination);
            source.CommitChanges();

            MessageBox.Show(
                $"'{objectName}' was moved successfully.",
                "Move account",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            SearchDirectory();
        }
        catch (Exception ex)
        {
            ShowOperationError($"Could not move '{objectName}'.", ex);
        }
    }

    private static string GetParentDn(string distinguishedName)
    {
        int comma = FindUnescapedComma(distinguishedName);
        return comma >= 0 ? distinguishedName[(comma + 1)..] : string.Empty;
    }

    private static int FindUnescapedComma(string value)
    {
        bool escaped = false;

        for (int i = 0; i < value.Length; i++)
        {
            if (escaped)
            {
                escaped = false;
                continue;
            }

            if (value[i] == '\\')
            {
                escaped = true;
                continue;
            }

            if (value[i] == ',')
                return i;
        }

        return -1;
    }

    // =========================================================
    // LDAP ATTRIBUTE HELPERS
    // =========================================================

    private static string ReadAttribute(DirectoryEntry? entry, string attribute)
    {
        try
        {
            return entry?.Properties[attribute].Value?.ToString() ?? string.Empty;
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadLastLogon(DirectoryEntry? entry)
    {
        try
        {
            object? value = entry?.Properties["lastLogonTimestamp"].Value;

            if (value is null)
                return string.Empty;

            long fileTime = Convert.ToInt64(value);

            return fileTime <= 0
                ? string.Empty
                : DateTime.FromFileTimeUtc(fileTime)
                    .ToLocalTime()
                    .ToString("dd MMM yyyy HH:mm");
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ReadPasswordExpired(DirectoryEntry? entry)
    {
        try
        {
            object? value =
                entry?.Properties["msDS-User-Account-Control-Computed"].Value;

            if (value is null)
                return "Unknown";

            int flags = Convert.ToInt32(value);
            return (flags & 0x800000) != 0 ? "Yes" : "No";
        }
        catch
        {
            return "Unknown";
        }
    }

    private static string GetOrganizationalUnit(string distinguishedName)
    {
        if (string.IsNullOrWhiteSpace(distinguishedName))
            return string.Empty;

        var parts = new List<string>();
        var current = new System.Text.StringBuilder();
        bool escaped = false;

        foreach (char c in distinguishedName)
        {
            if (escaped)
            {
                current.Append(c);
                escaped = false;
                continue;
            }

            if (c == '\\')
            {
                current.Append(c);
                escaped = true;
                continue;
            }

            if (c == ',')
            {
                parts.Add(current.ToString());
                current.Clear();
            }
            else
            {
                current.Append(c);
            }
        }

        if (current.Length > 0)
            parts.Add(current.ToString());

        var ous = parts
            .Where(x => x.Trim().StartsWith("OU=", StringComparison.OrdinalIgnoreCase))
            .Select(x => x.Trim()[3..])
            .Reverse()
            .ToList();

        return ous.Count == 0 ? "Domain root" : string.Join(" / ", ous);
    }

    private static bool SafeLockedOut(UserPrincipal? user)
    {
        try
        {
            return user?.IsAccountLockedOut() == true;
        }
        catch
        {
            return false;
        }
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

            _profiles = JsonSerializer.Deserialize<List<ConnectionProfile>>(
                File.ReadAllText(_connectionFilePath), JsonOptions) ?? [];

            RefreshProfileList();
        }
        catch (Exception ex)
        {
            _profiles = [];

            MessageBox.Show(
                $"Could not load saved connections.\n\n{ex.Message}",
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
                $"Could not save connections.\n\n{ex.Message}",
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

            ConnectionProfile? selected = selectId.HasValue
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
        if (_isLoadingProfile || _context is not null)
            return;

        if (_profileCombo.SelectedItem is ConnectionProfile profile)
            PopulateConnectionFields(profile);

        UpdateProfileControls();
    }

    private void PopulateConnectionFields(ConnectionProfile profile)
    {
        _domainBox.Text = profile.Domain;
        _serverBox.Text = profile.Server;
        _usernameBox.Text = profile.Username;
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
            x.Name, dialog.ProfileName, StringComparison.OrdinalIgnoreCase)))
        {
            MessageBox.Show("A connection with this name already exists.");
            return;
        }

        var profile = new ConnectionProfile
        {
            Id = Guid.NewGuid(),
            Name = dialog.ProfileName,
            Domain = _domainBox.Text.Trim(),
            Server = _serverBox.Text.Trim(),
            Username = _usernameBox.Text.Trim(),
            IsDefault = _profiles.Count == 0
        };

        _profiles.Add(profile);
        SaveConnections();
        RefreshProfileList(profile.Id);
    }

    private void UpdateProfile()
    {
        if (_profileCombo.SelectedItem is not ConnectionProfile profile ||
            !ValidateConnectionFields())
            return;

        profile.Domain = _domainBox.Text.Trim();
        profile.Server = _serverBox.Text.Trim();
        profile.Username = _usernameBox.Text.Trim();

        SaveConnections();
        RefreshProfileList(profile.Id);
    }

    private void DeleteProfile()
    {
        if (_profileCombo.SelectedItem is not ConnectionProfile profile)
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
        if (_profileCombo.SelectedItem is not ConnectionProfile selected)
            return;

        foreach (var profile in _profiles)
            profile.IsDefault = profile.Id == selected.Id;

        SaveConnections();
        RefreshProfileList(selected.Id);
    }

    private bool ValidateConnectionFields()
    {
        if (!string.IsNullOrWhiteSpace(_domainBox.Text) &&
            !string.IsNullOrWhiteSpace(_usernameBox.Text))
            return true;

        MessageBox.Show(
            "Enter the domain and username before saving.",
            "Saved connections",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);

        return false;
    }

    private void UpdateProfileControls()
    {
        bool disconnected = _context is null;
        bool hasSelection = _profileCombo.SelectedItem is ConnectionProfile;

        _profileCombo.IsEnabled = disconnected && !_isBusy;
        _addProfileButton.IsEnabled = disconnected && !_isBusy;
        _updateProfileButton.IsEnabled = disconnected && hasSelection && !_isBusy;
        _deleteProfileButton.IsEnabled = disconnected && hasSelection && !_isBusy;
        _defaultProfileButton.IsEnabled = disconnected && hasSelection && !_isBusy;
    }

    // =========================================================
    // UI HELPERS
    // =========================================================

    private void AddConnectionField(
        Grid grid, string label, Control control, int column, string placeholder)
    {
        var panel = new StackPanel { Margin = new Thickness(7, 0, 7, 0) };
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
        Panel parent, Button button, string text, string color, Action action)
    {
        StyleButton(button, text, color);
        button.Margin = new Thickness(4, 0, 0, 0);
        button.Click += (_, _) => action();
        parent.Children.Add(button);
    }

    private static Border CreatePanel() => new()
    {
        Background = CreateGlassBrush(),
        BorderBrush = BrushFrom(BorderColor),
        BorderThickness = new Thickness(1),
        CornerRadius = new CornerRadius(14),
        Padding = new Thickness(16),
        Effect = CreatePanelShadow()
    };

    private static LinearGradientBrush CreateGlassBrush()
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromArgb(225, 38, 59, 85), 0),
                new GradientStop(Color.FromArgb(205, 29, 46, 68), 0.55),
                new GradientStop(Color.FromArgb(190, 22, 35, 54), 1)
            }
        };
    }

    private static LinearGradientBrush CreateDialogGlassBrush()
    {
        return new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(Color.FromRgb(23, 39, 60), 0),
                new GradientStop(Color.FromRgb(17, 29, 46), 1)
            }
        };
    }

    private static DropShadowEffect CreatePanelShadow()
    {
        return new DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 18,
            ShadowDepth = 3,
            Opacity = 0.22
        };
    }

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
        control.BorderBrush = BrushFrom("#665F7896");
        control.BorderThickness = new Thickness(1);
        control.Padding = new Thickness(9, 7, 9, 7);
        control.FontSize = 13;
    }

    private static void StyleComboBox(ComboBox comboBox)
    {
        comboBox.Background = BrushFrom(InputColor);
        comboBox.Foreground = BrushFrom(TextColor);
        comboBox.BorderBrush = BrushFrom("#665F7896");
        comboBox.BorderThickness = new Thickness(1);
        comboBox.Padding = new Thickness(7, 4, 7, 4);
    }



    private static void StyleButton(Button button, string text, string background)
    {
        button.Content = text;

        string styleKey = background.ToUpperInvariant() switch
        {
            "#3B82F6" => "PrimaryButton",

            "#66991B1B" or
            "#667F1D1D" => "DangerButton",

            "#663B9B65" => "SuccessButton",

            "#66FBBF24" or
            "#66A68A49" => "WarningButton",

            _ => "SecondaryButton"
        };

        // MainWindow owns the named button styles.
        if (Application.Current.MainWindow?.TryFindResource(styleKey) is Style style)
        {
            button.Style = style;
        }
    }
    private static void AddActionButton(
        Panel parent, Button button, string text,
        string color, RoutedEventHandler handler)
    {
        StyleButton(button, text, color);
        button.HorizontalAlignment = HorizontalAlignment.Stretch;
        button.Margin = new Thickness(0, 12, 0, 0);
        button.Click += handler;
        parent.Children.Add(button);
    }

    private static void ConfigureDataGrid(DataGrid grid)
    {
        grid.AutoGenerateColumns = false;
        grid.IsReadOnly = true;
        grid.SelectionMode = DataGridSelectionMode.Single;
        grid.SelectionUnit = DataGridSelectionUnit.FullRow;
        grid.HeadersVisibility = DataGridHeadersVisibility.Column;
        grid.GridLinesVisibility = DataGridGridLinesVisibility.Horizontal;

        grid.ColumnHeaderStyle = new Style(typeof(DataGridColumnHeader))
        {
            Setters =
            {
                new Setter(Control.BackgroundProperty, BrushFrom("#CC142238")),
                new Setter(Control.ForegroundProperty, Brushes.White),
                new Setter(Control.BorderBrushProperty, BrushFrom("#554F6682")),
                new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 1, 1)),
                new Setter(Control.PaddingProperty, new Thickness(10, 9, 10, 9)),
                new Setter(Control.FontWeightProperty, FontWeights.SemiBold),
                new Setter(Control.FontSizeProperty, 12.0)
            }
        };

        grid.Background = BrushFrom("#66101828");
        grid.Foreground = BrushFrom(TextColor);
        grid.BorderBrush = BrushFrom("#665F7896");
        grid.RowBackground = BrushFrom("#55101828");
        grid.AlternatingRowBackground = BrushFrom("#442C405B");
        grid.HorizontalGridLinesBrush = BrushFrom("#334F6682");
        grid.VerticalGridLinesBrush = BrushFrom("#224F6682");
    }

    private static void StyleDialogWindow(Window window)
    {
        window.Background = CreateDialogGlassBrush();
        window.Foreground = Brushes.White;
    }

    private static void ShowOperationError(string message, Exception ex)
    {
        MessageBox.Show(
            $"{message}\n\n{ex.Message}",
            "Active Directory operation",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

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

        _selectedUser?.Dispose();
        _selectedComputer?.Dispose();
        _context?.Dispose();

        _selectedUser = null;
        _selectedComputer = null;
        _context = null;

        _adUsername = null;
        _adPassword = null;
        _ldapServer = null;
        _domainDn = null;
    }

    // =========================================================
    // DATA MODELS
    // =========================================================

    private sealed class ConnectionProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = string.Empty;
        public string Domain { get; set; } = string.Empty;
        public string Server { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public bool IsDefault { get; set; }

        public override string ToString() => Name;
    }

    private sealed class UserRow
    {
        public string Name { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string Department { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public string OrganizationalUnit { get; set; } = string.Empty;
        public string LastLogon { get; set; } = string.Empty;
        public string PasswordExpired { get; set; } = string.Empty;
        public string Enabled { get; set; } = string.Empty;
        public string LockedOut { get; set; } = string.Empty;
        public string DistinguishedName { get; set; } = string.Empty;
    }

    private sealed class ComputerRow
    {
        public string Name { get; set; } = string.Empty;
        public string SamAccountName { get; set; } = string.Empty;
        public string DnsHostName { get; set; } = string.Empty;
        public string OperatingSystem { get; set; } = string.Empty;
        public string OperatingSystemVersion { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Enabled { get; set; } = string.Empty;
        public string LastLogon { get; set; } = string.Empty;
        public string OrganizationalUnit { get; set; } = string.Empty;
        public string DistinguishedName { get; set; } = string.Empty;
    }

    private sealed class GroupRow
    {
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string Scope { get; set; } = string.Empty;
        public string DistinguishedName { get; set; } = string.Empty;
    }

    // =========================================================
    // GROUP SELECTION DIALOG
    // =========================================================

    private sealed class GroupSelectionDialog : Window
    {
        private readonly PrincipalContext _context;
        private readonly TextBox _searchBox = new();
        private readonly DataGrid _resultsGrid = new();
        private readonly TextBlock _status = new();

        public GroupRow? SelectedGroup { get; private set; }

        public GroupSelectionDialog(PrincipalContext context)
        {
            _context = context;

            Title = "Select Active Directory Group";
            Width = 720;
            Height = 570;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            StyleDialogWindow(this);

            var root = new Grid
            {
                Margin = new Thickness(20),
                Background = Brushes.Transparent
            };

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = "Add user to group",
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 14)
            });

            var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StyleInput(_searchBox);
            _searchBox.KeyDown += (_, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)
                    SearchGroups();
            };
            searchRow.Children.Add(_searchBox);

            var searchButton = new Button();
            StyleButton(searchButton, "Search", AccentColor);
            searchButton.Margin = new Thickness(8, 0, 0, 0);
            searchButton.Click += (_, _) => SearchGroups();

            Grid.SetColumn(searchButton, 1);
            searchRow.Children.Add(searchButton);

            Grid.SetRow(searchRow, 1);
            root.Children.Add(searchRow);

            ConfigureDataGrid(_resultsGrid);

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Group",
                Binding = new System.Windows.Data.Binding(nameof(GroupRow.Name)),
                Width = new DataGridLength(1.5, DataGridLengthUnitType.Star)
            });

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Type",
                Binding = new System.Windows.Data.Binding(nameof(GroupRow.Type)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            _resultsGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Scope",
                Binding = new System.Windows.Data.Binding(nameof(GroupRow.Scope)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            Grid.SetRow(_resultsGrid, 2);
            root.Children.Add(_resultsGrid);

            var footer = new DockPanel { Margin = new Thickness(0, 12, 0, 0) };

            _status.Text = "Enter a group name and search.";
            _status.Foreground = BrushFrom(MutedColor);
            _status.VerticalAlignment = VerticalAlignment.Center;
            footer.Children.Add(_status);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancel = new Button();
            StyleButton(cancel, "Cancel", "#663B506B");
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += (_, _) => DialogResult = false;

            var select = new Button();
            StyleButton(select, "Select group", AccentColor);
            select.Click += (_, _) => SelectGroup();

            buttons.Children.Add(cancel);
            buttons.Children.Add(select);

            DockPanel.SetDock(buttons, Dock.Right);
            footer.Children.Add(buttons);

            Grid.SetRow(footer, 3);
            root.Children.Add(footer);

            Content = root;
        }

        private void SearchGroups()
        {
            string query = _searchBox.Text.Trim();

            if (string.IsNullOrWhiteSpace(query))
                return;

            try
            {
                var matches = new Dictionary<string, GroupRow>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (string property in new[] { "Name", "SamAccountName" })
                {
                    using var queryGroup = new GroupPrincipal(_context);

                    if (property == "Name")
                        queryGroup.Name = $"*{query}*";
                    else
                        queryGroup.SamAccountName = $"*{query}*";

                    using var searcher = new PrincipalSearcher(queryGroup);

                    foreach (var result in searcher.FindAll())
                    {
                        if (result is not GroupPrincipal group)
                        {
                            result.Dispose();
                            continue;
                        }

                        using (group)
                        {
                            string dn = group.DistinguishedName ?? string.Empty;

                            if (string.IsNullOrWhiteSpace(dn))
                                continue;

                            matches[dn] = new GroupRow
                            {
                                Name = group.Name ?? "(Unnamed group)",
                                Type = group.IsSecurityGroup.GetValueOrDefault()
                                    ? "Security" : "Distribution",
                                Scope = group.GroupScope?.ToString() ?? "Unknown",
                                DistinguishedName = dn
                            };
                        }

                        if (matches.Count >= 500)
                            break;
                    }

                    if (matches.Count >= 500)
                        break;
                }

                var results = matches.Values.OrderBy(x => x.Name).ToList();
                _resultsGrid.ItemsSource = results;
                _status.Text = $"{results.Count} group(s) found.";
            }
            catch (Exception ex)
            {
                _status.Text = "Group search failed.";
                ShowOperationError("Could not search Active Directory groups.", ex);
            }
        }

        private void SelectGroup()
        {
            if (_resultsGrid.SelectedItem is not GroupRow group)
            {
                MessageBox.Show("Select a group first.");
                return;
            }

            SelectedGroup = group;
            DialogResult = true;
        }
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
            Title = "Save AD Connection";
            Width = 400;
            Height = 215;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            StyleDialogWindow(this);

            var root = new StackPanel
            {
                Margin = new Thickness(22),
                Background = Brushes.Transparent
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
            _nameBox.Text = "New AD Connection";
            root.Children.Add(_nameBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 20, 0, 0)
            };

            var cancel = new Button();
            StyleButton(cancel, "Cancel", "#663B506B");
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

    // =========================================================
    // PASSWORD RESET DIALOG
    // =========================================================

    private sealed class PasswordResetDialog : Window
    {
        private readonly PasswordBox _newPasswordBox = new();
        private readonly PasswordBox _confirmPasswordBox = new();

        public string NewPassword { get; private set; } = string.Empty;

        public PasswordResetDialog(string username)
        {
            Title = "Reset Active Directory Password";
            Width = 430;
            Height = 310;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            StyleDialogWindow(this);

            var root = new StackPanel
            {
                Margin = new Thickness(22),
                Background = Brushes.Transparent
            };

            root.Children.Add(new TextBlock
            {
                Text = $"Set a new password for {username}",
                FontSize = 17,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 18)
            });

            root.Children.Add(CreateLabel("New password"));
            StyleInput(_newPasswordBox);
            root.Children.Add(_newPasswordBox);

            var confirmLabel = CreateLabel("Confirm new password");
            confirmLabel.Margin = new Thickness(0, 14, 0, 6);
            root.Children.Add(confirmLabel);

            StyleInput(_confirmPasswordBox);
            root.Children.Add(_confirmPasswordBox);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 22, 0, 0)
            };

            var cancel = new Button();
            StyleButton(cancel, "Cancel", "#663B506B");
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += (_, _) => DialogResult = false;

            var save = new Button();
            StyleButton(save, "Reset password", AccentColor);
            save.Click += Save_Click;

            buttons.Children.Add(cancel);
            buttons.Children.Add(save);
            root.Children.Add(buttons);

            Content = root;
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_newPasswordBox.Password))
            {
                MessageBox.Show("Enter a new password.");
                return;
            }

            if (_newPasswordBox.Password != _confirmPasswordBox.Password)
            {
                MessageBox.Show("The passwords do not match.");
                return;
            }

            NewPassword = _newPasswordBox.Password;
            DialogResult = true;
        }

        public void ClearPasswords()
        {
            _newPasswordBox.Clear();
            _confirmPasswordBox.Clear();
            NewPassword = string.Empty;
        }
    }

    // =========================================================
    // OU SELECTION DIALOG
    // =========================================================

    private sealed class OuSelectionDialog : Window
    {
        private readonly string _server;
        private readonly string _domainDn;
        private readonly string _username;
        private readonly string _password;

        private readonly TextBox _searchBox = new();
        private readonly DataGrid _ouGrid = new();
        private readonly TextBlock _status = new();
        private readonly TextBlock _preview = new();

        public string? SelectedOuDn { get; private set; }
        public string? SelectedOuName { get; private set; }

        public OuSelectionDialog(
            string server,
            string domainDn,
            string username,
            string password)
        {
            _server = server;
            _domainDn = domainDn;
            _username = username;
            _password = password;

            Title = "Select Destination OU";
            Width = 800;
            Height = 620;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            StyleDialogWindow(this);

            var root = new Grid
            {
                Margin = new Thickness(20),
                Background = Brushes.Transparent
            };

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            root.Children.Add(new TextBlock
            {
                Text = "Move account to organisational unit",
                FontSize = 19,
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 14)
            });

            var searchRow = new Grid { Margin = new Thickness(0, 0, 0, 10) };
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            StyleInput(_searchBox);
            _searchBox.ToolTip = "Filter OUs by name";
            _searchBox.TextChanged += (_, _) => SearchOus();

            searchRow.Children.Add(_searchBox);

            var refreshButton = new Button();
            StyleButton(refreshButton, "Refresh", "#663B506B");
            refreshButton.Margin = new Thickness(8, 0, 0, 0);
            refreshButton.Click += (_, _) => SearchOus();

            Grid.SetColumn(refreshButton, 1);
            searchRow.Children.Add(refreshButton);

            Grid.SetRow(searchRow, 1);
            root.Children.Add(searchRow);

            ConfigureDataGrid(_ouGrid);

            _ouGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Organisational unit",
                Binding = new System.Windows.Data.Binding(nameof(OuRow.Name)),
                Width = new DataGridLength(1, DataGridLengthUnitType.Star)
            });

            _ouGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Distinguished name",
                Binding = new System.Windows.Data.Binding(nameof(OuRow.DistinguishedName)),
                Width = new DataGridLength(2, DataGridLengthUnitType.Star)
            });

            _ouGrid.SelectionChanged += (_, _) => UpdatePreview();

            Grid.SetRow(_ouGrid, 2);
            root.Children.Add(_ouGrid);

            _status.Text = "Loading organisational units...";
            _status.Foreground = BrushFrom(MutedColor);
            _status.Margin = new Thickness(0, 10, 0, 6);

            Grid.SetRow(_status, 3);
            root.Children.Add(_status);

            _preview.Text = "Destination preview: select an OU.";
            _preview.Foreground = BrushFrom("#93C5FD");
            _preview.TextWrapping = TextWrapping.Wrap;
            _preview.Margin = new Thickness(0, 0, 0, 12);

            Grid.SetRow(_preview, 4);
            root.Children.Add(_preview);

            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var cancel = new Button();
            StyleButton(cancel, "Cancel", "#663B506B");
            cancel.Margin = new Thickness(0, 0, 8, 0);
            cancel.Click += (_, _) => DialogResult = false;

            var select = new Button();
            StyleButton(select, "Select destination", AccentColor);
            select.Click += (_, _) =>
            {
                if (_ouGrid.SelectedItem is not OuRow selected)
                {
                    MessageBox.Show("Select an OU first.");
                    return;
                }

                SelectedOuDn = selected.DistinguishedName;
                SelectedOuName = selected.Name;
                DialogResult = true;
            };

            footer.Children.Add(cancel);
            footer.Children.Add(select);

            Grid.SetRow(footer, 5);
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.Children.Add(footer);

            Content = root;

            Loaded += (_, _) => SearchOus();
        }

        private void SearchOus()
        {
            try
            {
                string query = _searchBox.Text.Trim();

                using var root = new DirectoryEntry(
                    $"LDAP://{_server}/{_domainDn}",
                    _username,
                    _password,
                    AuthenticationTypes.Secure);

                using var searcher = new DirectorySearcher(root)
                {
                    SearchScope = SearchScope.Subtree,
                    PageSize = 500,
                    Filter = string.IsNullOrWhiteSpace(query)
                        ? "(objectCategory=organizationalUnit)"
                        : $"(&(objectCategory=organizationalUnit)(name=*{EscapeLdapFilter(query)}*))"
                };

                searcher.PropertiesToLoad.Add("name");
                searcher.PropertiesToLoad.Add("distinguishedName");

                var results = new List<OuRow>
                {
                    new()
                    {
                        Name = "Domain root",
                        DistinguishedName = _domainDn
                    }
                };

                using SearchResultCollection found = searcher.FindAll();

                foreach (SearchResult result in found)
                {
                    string name = result.Properties["name"].Count > 0
                        ? result.Properties["name"][0]?.ToString() ?? string.Empty
                        : string.Empty;

                    string dn = result.Properties["distinguishedName"].Count > 0
                        ? result.Properties["distinguishedName"][0]?.ToString() ?? string.Empty
                        : string.Empty;

                    if (!string.IsNullOrWhiteSpace(dn))
                    {
                        results.Add(new OuRow
                        {
                            Name = name,
                            DistinguishedName = dn
                        });
                    }
                }

                _ouGrid.ItemsSource = results
                    .OrderBy(x => x.Name)
                    .ToList();

                _status.Text = $"{results.Count} destination(s) found.";
            }
            catch (Exception ex)
            {
                _status.Text = "Could not load organisational units.";
                ShowOperationError("Could not search Active Directory OUs.", ex);
            }
        }

        private void UpdatePreview()
        {
            if (_ouGrid.SelectedItem is not OuRow selected)
            {
                _preview.Text = "Destination preview: select an OU.";
                return;
            }

            _preview.Text =
                $"Destination preview: {selected.Name}\n{selected.DistinguishedName}";
        }

        private static string EscapeLdapFilter(string value)
        {
            return value
                .Replace("\\", "\\5c")
                .Replace("*", "\\2a")
                .Replace("(", "\\28")
                .Replace(")", "\\29")
                .Replace("\0", "\\00");
        }

        private sealed class OuRow
        {
            public string Name { get; set; } = string.Empty;
            public string DistinguishedName { get; set; } = string.Empty;
        }
    }
}
