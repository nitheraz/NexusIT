
using NexusIT.App.Models;
using NexusIT.App.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;

namespace NexusIT.App.Views;

public sealed class MecmDeviceManagementView : UserControl
{
    private readonly MecmConnectionService _connectionService;
    private readonly MecmDeviceService _deviceService;

    private readonly TextBox _searchBox = new();
    private readonly Button _searchButton = new();
    private readonly Button _refreshButton = new();
    private readonly DataGrid _deviceGrid = new();
    private readonly TextBlock _statusText = new();
    private readonly TextBlock _deviceCountText = new();
    private readonly TextBlock _detailsText = new();

    private bool _isBusy;

    private const string PanelColor = "#1F2937";
    private const string InputColor = "#111827";
    private const string TextColor = "#E5E7EB";
    private const string MutedColor = "#9CA3AF";
    private const string AccentColor = "#3B82F6";

    public MecmDeviceManagementView(
        MecmConnectionService connectionService)
    {
        _connectionService = connectionService
            ?? throw new ArgumentNullException(nameof(connectionService));

        _deviceService = new MecmDeviceService(_connectionService);

        Content = BuildInterface();

        SetConnectedState(_connectionService.IsConnected);
    }

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

        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        // Header
        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 18)
        };

        header.Children.Add(new TextBlock
        {
            Text = "Device Management",
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brushes.White
        });

        header.Children.Add(new TextBlock
        {
            Text = "Search and inspect devices registered in Configuration Manager.",
            FontSize = 13,
            Foreground = BrushFrom(MutedColor),
            Margin = new Thickness(0, 5, 0, 0)
        });

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        // Search panel
        var searchPanel = CreatePanel();
        searchPanel.Margin = new Thickness(0, 0, 0, 16);

        var searchLayout = new Grid();

        searchLayout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

        searchLayout.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        searchLayout.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var searchStack = new StackPanel();

        searchStack.Children.Add(CreateLabel(
            "Search by device name or username"));

        StyleInput(_searchBox);
        _searchBox.Height = 38;
        _searchBox.MinWidth = 220;
        _searchBox.ToolTip = "Enter a device name or username";
        _searchBox.KeyDown += (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                _ = SearchDevicesAsync();
            }
        };

        searchStack.Children.Add(_searchBox);
        searchLayout.Children.Add(searchStack);

        StyleButton(_searchButton, "Search", AccentColor);
        _searchButton.Margin = new Thickness(10, 22, 0, 0);
        _searchButton.Click += async (_, _) =>
            await SearchDevicesAsync();

        Grid.SetColumn(_searchButton, 1);
        searchLayout.Children.Add(_searchButton);

        StyleButton(_refreshButton, "Refresh", "#374151");
        _refreshButton.Margin = new Thickness(8, 22, 0, 0);
        _refreshButton.Click += async (_, _) =>
            await SearchDevicesAsync();

        Grid.SetColumn(_refreshButton, 2);
        searchLayout.Children.Add(_refreshButton);

        searchPanel.Child = searchLayout;

        Grid.SetRow(searchPanel, 1);
        root.Children.Add(searchPanel);

        // Main content: results and details
        var contentGrid = new Grid();

        contentGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1.7, GridUnitType.Star)
            });

        contentGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(12) });

        contentGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

        var resultsPanel = CreatePanel();

        var resultsLayout = new Grid();

        resultsLayout.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        resultsLayout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        var resultsHeader = new Grid
        {
            Margin = new Thickness(0, 0, 0, 12)
        };

        resultsHeader.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

        resultsHeader.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var resultsTitle = CreateSectionTitle("Devices");
        resultsHeader.Children.Add(resultsTitle);

        _deviceCountText.Text = "0 devices";
        _deviceCountText.Foreground = BrushFrom(MutedColor);
        _deviceCountText.VerticalAlignment = VerticalAlignment.Center;

        Grid.SetColumn(_deviceCountText, 1);
        resultsHeader.Children.Add(_deviceCountText);

        resultsLayout.Children.Add(resultsHeader);

        ConfigureDeviceGrid();

        Grid.SetRow(_deviceGrid, 1);
        resultsLayout.Children.Add(_deviceGrid);

        resultsPanel.Child = resultsLayout;

        Grid.SetColumn(resultsPanel, 0);
        contentGrid.Children.Add(resultsPanel);

        // Details panel
        var detailsPanel = CreatePanel();

        var detailsLayout = new StackPanel();

        detailsLayout.Children.Add(
            CreateSectionTitle("Device details"));

        _detailsText.Text =
            "Select a device from the results to view its information.";

        _detailsText.Foreground = BrushFrom(TextColor);
        _detailsText.TextWrapping = TextWrapping.Wrap;
        _detailsText.Margin = new Thickness(0, 16, 0, 0);

        detailsLayout.Children.Add(_detailsText);

        detailsPanel.Child = detailsLayout;

        Grid.SetColumn(detailsPanel, 2);
        contentGrid.Children.Add(detailsPanel);

        Grid.SetRow(contentGrid, 2);
        root.Children.Add(contentGrid);

        // Status
        _statusText.Text = "Connect to MECM to search for devices.";
        _statusText.Foreground = BrushFrom(MutedColor);
        _statusText.Margin = new Thickness(0, 12, 0, 0);

        Grid.SetRow(_statusText, 3);
        root.Children.Add(_statusText);

        return root;
    }


    private void ConfigureDeviceGrid()
    {
        _deviceGrid.AutoGenerateColumns = false;
        _deviceGrid.IsReadOnly = true;
        _deviceGrid.CanUserAddRows = false;
        _deviceGrid.SelectionMode = DataGridSelectionMode.Single;
        _deviceGrid.SelectionUnit = DataGridSelectionUnit.FullRow;
        _deviceGrid.HeadersVisibility = DataGridHeadersVisibility.Column;
        _deviceGrid.GridLinesVisibility = DataGridGridLinesVisibility.None;

        _deviceGrid.Background = BrushFrom(PanelColor);
        _deviceGrid.Foreground = BrushFrom(TextColor);
        _deviceGrid.BorderThickness = new Thickness(0);
        _deviceGrid.RowBackground = BrushFrom(PanelColor);
        _deviceGrid.AlternatingRowBackground = BrushFrom("#263244");

        _deviceGrid.SelectionChanged += DeviceGrid_SelectionChanged;

        // Explicit dark column header styling.
        var headerStyle = new Style(
    typeof(System.Windows.Controls.Primitives.DataGridColumnHeader));

        headerStyle.Setters.Add(
            new Setter(Control.BackgroundProperty, BrushFrom("#111827")));

        headerStyle.Setters.Add(
            new Setter(Control.ForegroundProperty, BrushFrom("#E5E7EB")));

        headerStyle.Setters.Add(
            new Setter(Control.BorderBrushProperty, BrushFrom("#374151")));

        headerStyle.Setters.Add(
            new Setter(Control.BorderThicknessProperty, new Thickness(0, 0, 0, 1)));

        headerStyle.Setters.Add(
            new Setter(Control.PaddingProperty, new Thickness(10, 8, 10, 8)));

        headerStyle.Setters.Add(
            new Setter(Control.FontWeightProperty, FontWeights.SemiBold));

        headerStyle.Setters.Add(
            new Setter(Control.FontSizeProperty, 12.0));

        _deviceGrid.ColumnHeaderStyle = headerStyle;

        _deviceGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Device name",
            Binding = new System.Windows.Data.Binding(nameof(MecmDevice.Name)),
            Width = new DataGridLength(1.2, DataGridLengthUnitType.Star)
        });

        _deviceGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Username",
            Binding = new System.Windows.Data.Binding(nameof(MecmDevice.UserName)),
            Width = new DataGridLength(1, DataGridLengthUnitType.Star)
        });

        _deviceGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Operating system",
            Binding = new System.Windows.Data.Binding(nameof(MecmDevice.OperatingSystem)),
            Width = new DataGridLength(1.3, DataGridLengthUnitType.Star)
        });

        _deviceGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Client",
            Binding = new System.Windows.Data.Binding(nameof(MecmDevice.ClientVersion)),
            Width = new DataGridLength(0.8, DataGridLengthUnitType.Star)
        });

        _deviceGrid.Columns.Add(new DataGridTextColumn
        {
            Header = "Resource ID",
            Binding = new System.Windows.Data.Binding(nameof(MecmDevice.ResourceId)),
            Width = new DataGridLength(0.7, DataGridLengthUnitType.Star)
        });
    }


    private async Task SearchDevicesAsync()
    {
        if (_isBusy)
            return;

        if (!_connectionService.IsConnected)
        {
            SetStatus("Connect to a MECM site first.", true);
            return;
        }

        _isBusy = true;
        UpdateButtons();

        _deviceGrid.ItemsSource = null;
        _detailsText.Text = "Select a device from the results.";
        _deviceCountText.Text = "Searching...";
        SetStatus("Searching MECM devices...");

        try
        {
            string searchText = _searchBox.Text.Trim();

            List<MecmDevice> devices = await Task.Run(
                () => _deviceService.SearchDevices(searchText, 500));

            _deviceGrid.ItemsSource = devices;

            _deviceCountText.Text =
                $"{devices.Count:N0} device(s)";

            SetStatus(
                devices.Count == 500
                    ? "Search complete. Results are limited to 500 devices."
                    : "Device search completed.");
        }
        catch (Exception ex)
        {
            _deviceCountText.Text = "Search failed";
            SetStatus($"Device search failed: {ex.Message}", true);
        }
        finally
        {
            _isBusy = false;
            UpdateButtons();
        }
    }

    private async void DeviceGrid_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_deviceGrid.SelectedItem is not MecmDevice device)
            return;

        _detailsText.Text = "Loading device details...";

        try
        {
            MecmDevice? details = await Task.Run(
                () => _deviceService.GetDevice(device.ResourceId));

            if (details is null)
            {
                _detailsText.Text = "Device information is no longer available.";
                return;
            }

            DisplayDeviceDetails(details);
        }
        catch (Exception ex)
        {
            _detailsText.Text =
                $"Could not retrieve device details.\n\n{ex.Message}";
        }
    }

    private void DisplayDeviceDetails(MecmDevice device)
    {
        _detailsText.Text =
            $"Device name\n{Value(device.Name)}\n\n" +
            $"Resource ID\n{device.ResourceId}\n\n" +
            $"Username\n{Value(device.UserName)}\n\n" +
            $"Last logon user\n{Value(device.LastLogonUser)}\n\n" +
            $"Operating system\n{Value(device.OperatingSystem)}\n\n" +
            $"Client version\n{Value(device.ClientVersion)}\n\n" +
            $"Client installed\n{(device.ClientInstalled ? "Yes" : "No")}\n\n" +
            $"Site code\n{Value(device.SiteCode)}\n\n" +
            $"Domain\n{Value(device.Domain)}\n\n" +
            $"MAC address\n{Value(device.MacAddress)}\n\n" +
            $"IP address\n{Value(device.IpAddress)}\n\n" +
            $"SMS unique identifier\n{Value(device.SmsUniqueIdentifier)}";
    }

    public void SetConnectedState(bool connected)
    {
        _searchBox.IsEnabled = connected;
        _searchButton.IsEnabled = connected;
        _refreshButton.IsEnabled = connected;

        if (!connected)
        {
            _deviceGrid.ItemsSource = null;
            _detailsText.Text = "Connect to MECM to view device information.";
            _deviceCountText.Text = "0 devices";
            SetStatus("Not connected to MECM.");
        }
    }

    private void UpdateButtons()
    {
        bool enabled =
            !_isBusy && _connectionService.IsConnected;

        _searchButton.IsEnabled = enabled;
        _refreshButton.IsEnabled = enabled;
        _searchBox.IsEnabled = enabled;
    }

    private void SetStatus(string message, bool error = false)
    {
        _statusText.Text = message;
        _statusText.Foreground = BrushFrom(
            error ? "#FCA5A5" : MutedColor);
    }

    private static string Value(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "Not available" : value;

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
        button.Padding = new Thickness(14, 8, 14, 8);
    }

    private static SolidColorBrush BrushFrom(string color) =>
        new((Color)ColorConverter.ConvertFromString(color));
}
