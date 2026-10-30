
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using NexusIT.App.Models;
using NexusIT.App.Services.Rdp;

namespace NexusIT.App.Views.Rdp;

public partial class RdpManagerView : UserControl
{
    private readonly RdpStorageService _storageService = new();
    private readonly RdpLauncherService _launcherService = new();

    private readonly ObservableCollection<RdpConnection> _connections = [];
    private readonly ObservableCollection<RdpConnection> _filteredConnections = [];

    private Guid? _editingId;
    private bool _isLoadingEditor;
    private bool _isRefreshing;

    public RdpManagerView()
    {
        InitializeComponent();

        ConnectionsList.ItemsSource = _filteredConnections;

        Loaded += RdpManagerView_Loaded;
    }

    private async void RdpManagerView_Loaded(
        object sender,
        RoutedEventArgs e)
    {
        Loaded -= RdpManagerView_Loaded;

        try
        {
            List<RdpConnection> savedConnections =
                await _storageService.LoadAsync();

            _connections.Clear();

            foreach (RdpConnection connection in savedConnections)
                _connections.Add(connection);

            RefreshGroups();
            ApplyFilters();

            EditorStatusText.Text =
                $"Local storage: {_storageService.GetStoragePath()}";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to load saved RDP connections.\n\n{ex.Message}",
                "RDP Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void NewConnection_Click(
        object sender,
        RoutedEventArgs e)
    {
        ConnectionsList.SelectedItem = null;
        ClearEditor();

        EditorTitle.Text = "New connection";
        EditorStatusText.Text = "Enter the connection details and select Save.";

        _editingId = null;
    }

    private void ConnectionsList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (ConnectionsList.SelectedItem is not RdpConnection connection)
            return;

        LoadEditor(connection);
    }

    private void LoadEditor(RdpConnection connection)
    {
        _isLoadingEditor = true;

        _editingId = connection.Id;

        EditorTitle.Text = "Edit connection";

        NameTextBox.Text = connection.Name;
        HostTextBox.Text = connection.Host;
        PortTextBox.Text = connection.Port.ToString();

        GroupCombo.Text = connection.Group;
        UsernameTextBox.Text = connection.Username;

        AdminSessionCheckBox.IsChecked = connection.UseAdminSession;
        FullScreenCheckBox.IsChecked = connection.FullScreen;
        AllMonitorsCheckBox.IsChecked = connection.UseAllMonitors;

        WidthTextBox.Text = connection.Width.ToString();
        HeightTextBox.Text = connection.Height.ToString();

        EditorStatusText.Text =
            $"Selected: {connection.Name}";

        _isLoadingEditor = false;
    }

    private void ClearEditor()
    {
        _isLoadingEditor = true;

        NameTextBox.Clear();
        HostTextBox.Clear();
        PortTextBox.Text = "3389";

        GroupCombo.Text = "General";
        UsernameTextBox.Clear();

        AdminSessionCheckBox.IsChecked = false;
        FullScreenCheckBox.IsChecked = false;
        AllMonitorsCheckBox.IsChecked = false;

        WidthTextBox.Text = "1920";
        HeightTextBox.Text = "1080";

        _isLoadingEditor = false;
    }

    private async void SaveConnection_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!TryReadEditor(out RdpConnection? connection))
            return;

        try
        {
            RdpConnection? existing = _editingId.HasValue
                ? _connections.FirstOrDefault(
                    x => x.Id == _editingId.Value)
                : null;

            if (existing is null)
            {
                _connections.Add(connection);
            }
            else
            {
                existing.Name = connection.Name;
                existing.Host = connection.Host;
                existing.Port = connection.Port;
                existing.Group = connection.Group;
                existing.Username = connection.Username;
                existing.UseAdminSession = connection.UseAdminSession;
                existing.FullScreen = connection.FullScreen;
                existing.UseAllMonitors = connection.UseAllMonitors;
                existing.Width = connection.Width;
                existing.Height = connection.Height;
            }

            await _storageService.SaveAsync(_connections.ToList());

            RefreshGroups();
            ApplyFilters();

            RdpConnection saved = existing ?? connection;

            ConnectionsList.SelectedItem = saved;
            LoadEditor(saved);

            EditorStatusText.Text = "Connection saved successfully.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to save the connection.\n\n{ex.Message}",
                "RDP Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private bool TryReadEditor(out RdpConnection connection)
    {
        connection = new RdpConnection();

        string name = NameTextBox.Text.Trim();
        string host = HostTextBox.Text.Trim();
        string group = GroupCombo.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            ShowValidationError("Enter a connection name.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            ShowValidationError("Enter a computer name or IP address.");
            return false;
        }

        if (!int.TryParse(PortTextBox.Text, out int port) ||
            port is < 1 or > 65535)
        {
            ShowValidationError("Enter a valid port between 1 and 65535.");
            return false;
        }

        if (!int.TryParse(WidthTextBox.Text, out int width) ||
            width is < 640 or > 16384)
        {
            ShowValidationError("Enter a valid display width.");
            return false;
        }

        if (!int.TryParse(HeightTextBox.Text, out int height) ||
            height is < 480 or > 16384)
        {
            ShowValidationError("Enter a valid display height.");
            return false;
        }

        connection = new RdpConnection
        {
            Id = _editingId ?? Guid.NewGuid(),
            Name = name,
            Host = host,
            Port = port,
            Group = string.IsNullOrWhiteSpace(group)
                ? "General"
                : group,
            Username = UsernameTextBox.Text.Trim(),
            UseAdminSession = AdminSessionCheckBox.IsChecked == true,
            FullScreen = FullScreenCheckBox.IsChecked == true,
            UseAllMonitors = AllMonitorsCheckBox.IsChecked == true,
            Width = width,
            Height = height
        };

        return true;
    }

    private void ShowValidationError(string message)
    {
        MessageBox.Show(
            message,
            "Invalid connection details",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
    }

    private async void DeleteConnection_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!_editingId.HasValue)
        {
            MessageBox.Show(
                "Select a saved connection first.",
                "RDP Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        RdpConnection? connection = _connections.FirstOrDefault(
            x => x.Id == _editingId.Value);

        if (connection is null)
            return;

        MessageBoxResult result = MessageBox.Show(
            $"Are you sure you want to delete '{connection.Name}'?",
            "Delete connection",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        try
        {
            _connections.Remove(connection);

            await _storageService.SaveAsync(_connections.ToList());

            _editingId = null;
            ConnectionsList.SelectedItem = null;

            ClearEditor();
            RefreshGroups();
            ApplyFilters();

            EditorTitle.Text = "Connection details";
            EditorStatusText.Text = "Connection deleted.";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to delete the connection.\n\n{ex.Message}",
                "RDP Manager",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void Connect_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!TryReadEditor(out RdpConnection connection))
            return;

        try
        {
            _launcherService.Launch(connection);

            EditorStatusText.Text =
                $"Launching Remote Desktop for {connection.Name}...";
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to launch Remote Desktop.\n\n{ex.Message}",
                "RDP Connection",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void SearchTextBox_TextChanged(
        object sender,
        TextChangedEventArgs e)
    {
        ApplyFilters();
    }

    private void GroupFilterCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_isRefreshing)
            ApplyFilters();
    }

    private void RefreshGroups()
    {
        _isRefreshing = true;

        string previousSelection =
            GroupFilterCombo.SelectedItem?.ToString() ?? "All groups";

        List<string> groups = _connections
            .Select(x => x.Group)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x)
            .ToList();

        GroupFilterCombo.ItemsSource =
            new[] { "All groups" }.Concat(groups).ToList();

        GroupFilterCombo.SelectedItem =
            GroupFilterCombo.Items.Contains(previousSelection)
                ? previousSelection
                : "All groups";

        _isRefreshing = false;
    }

    private void ApplyFilters()
    {
        if (ConnectionsList is null)
            return;

        string search = SearchTextBox.Text.Trim();

        string selectedGroup =
            GroupFilterCombo.SelectedItem?.ToString() ?? "All groups";

        IEnumerable<RdpConnection> results = _connections;

        if (!string.IsNullOrWhiteSpace(search))
        {
            results = results.Where(x =>
                x.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Host.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Group.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                x.Username.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (selectedGroup != "All groups")
        {
            results = results.Where(x =>
                string.Equals(
                    x.Group,
                    selectedGroup,
                    StringComparison.OrdinalIgnoreCase));
        }

        _filteredConnections.Clear();

        foreach (RdpConnection connection in results
                     .OrderBy(x => x.Group)
                     .ThenBy(x => x.Name))
        {
            _filteredConnections.Add(connection);
        }

        ConnectionCountText.Text =
            $"{_filteredConnections.Count} of {_connections.Count} connections";
    }
}
