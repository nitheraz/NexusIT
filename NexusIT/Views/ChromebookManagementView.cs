
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NexusIT.App.Models;
using NexusIT.App.Services;

namespace NexusIT.App.Views;

public sealed class ChromebookManagementView : UserControl
{
    private readonly GamService _gamService = new();
    private readonly GamHealthService _healthService = new();

    private readonly TextBox _searchBox;
    private readonly ComboBox _searchMode;
    private readonly TextBlock _message;
    private readonly TextBlock _healthStatus;
    private readonly StackPanel _detailsPanel;
    private readonly Button _searchButton;
    private readonly Button _refreshButton;
    private readonly Button _healthButton;

    public ChromebookManagementView()
    {
        Background = BrushFrom("#0F172A");

        var page = new StackPanel();

        page.Children.Add(CreateText(
            "Chromebook management", 25, "#F9FAFB",
            FontWeights.SemiBold, new Thickness(0, 0, 0, 8)));

        page.Children.Add(CreateText(
            "Look up ChromeOS devices using Google Admin and GAM7.",
            13, "#9CA3AF", FontWeights.Normal,
            new Thickness(0, 0, 0, 24)));

        var healthCard = CreateCard();

        var healthContent = new Grid();
        healthContent.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        healthContent.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var healthText = new StackPanel();
        healthText.Children.Add(CreateText(
            "GAM7 connection", 16, "#F9FAFB",
            FontWeights.SemiBold, new Thickness(0, 0, 0, 6)));

        _healthStatus = CreateText(
            "Not checked yet", 12, "#9CA3AF",
            FontWeights.Normal, new Thickness(0));

        healthText.Children.Add(_healthStatus);
        healthContent.Children.Add(healthText);

        _healthButton = CreateButton("Check GAM7", "#1D3557");
        _healthButton.Click += async (_, _) => await CheckHealthAsync();

        Grid.SetColumn(_healthButton, 1);
        healthContent.Children.Add(_healthButton);

        healthCard.Child = healthContent;
        page.Children.Add(healthCard);

        var searchCard = CreateCard();
        var searchContent = new StackPanel();

        searchContent.Children.Add(CreateText(
            "Find a Chromebook", 17, "#F9FAFB",
            FontWeights.SemiBold, new Thickness(0, 0, 0, 16)));

        searchContent.Children.Add(CreateText(
            "Search by serial number or annotated asset ID.",
            12, "#9CA3AF", FontWeights.Normal,
            new Thickness(0, 0, 0, 12)));

        var searchRow = new Grid();
        searchRow.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(180) });
        searchRow.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        searchRow.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        _searchMode = new ComboBox
        {
            ItemsSource = new[] { "Serial number", "Asset ID" },
            SelectedIndex = 0,
            Height = 38,
            Margin = new Thickness(0, 0, 10, 0),
            Padding = new Thickness(8, 6, 8, 6),
            Background = BrushFrom("#111827"),
            Foreground = BrushFrom("#F9FAFB"),
            BorderBrush = BrushFrom("#374151")
        };

        Grid.SetColumn(_searchMode, 0);
        searchRow.Children.Add(_searchMode);

        _searchBox = new TextBox
        {
            Height = 38,
            Padding = new Thickness(10, 8, 10, 8),
            Background = BrushFrom("#111827"),
            Foreground = BrushFrom("#F9FAFB"),
            BorderBrush = BrushFrom("#374151"),
            CaretBrush = Brushes.White,
            VerticalContentAlignment = VerticalAlignment.Center,
            ToolTip = "Enter a Chromebook serial number or asset ID"
        };

        _searchBox.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                await SearchAsync();
            }
        };

        Grid.SetColumn(_searchBox, 1);
        searchRow.Children.Add(_searchBox);

        _searchButton = CreateButton("Search", "#1D3557");
        _searchButton.Margin = new Thickness(10, 0, 0, 0);
        _searchButton.Click += async (_, _) => await SearchAsync();

        Grid.SetColumn(_searchButton, 2);
        searchRow.Children.Add(_searchButton);

        searchContent.Children.Add(searchRow);

        _refreshButton = CreateButton(
            "Refresh asset ID cache", "#263449");

        _refreshButton.HorizontalAlignment = HorizontalAlignment.Left;
        _refreshButton.Margin = new Thickness(0, 14, 0, 0);
        _refreshButton.Click += async (_, _) => await RefreshAssetCacheAsync();

        searchContent.Children.Add(_refreshButton);

        _message = CreateText(
            "Enter a search value to begin.", 12, "#9CA3AF",
            FontWeights.Normal, new Thickness(0, 14, 0, 0));

        searchContent.Children.Add(_message);
        searchCard.Child = searchContent;
        page.Children.Add(searchCard);

        var detailsCard = CreateCard();
        var detailsContent = new StackPanel();

        detailsContent.Children.Add(CreateText(
            "Device details", 17, "#F9FAFB",
            FontWeights.SemiBold, new Thickness(0, 0, 0, 16)));

        _detailsPanel = new StackPanel();
        _detailsPanel.Children.Add(CreateText(
            "No device selected.", 12, "#9CA3AF",
            FontWeights.Normal, new Thickness(0)));

        detailsContent.Children.Add(_detailsPanel);
        detailsCard.Child = detailsContent;
        page.Children.Add(detailsCard);

        Content = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = page
        };
    }

    private async Task CheckHealthAsync()
    {
        SetBusy(true);
        _healthStatus.Text = "Checking GAM7...";
        _healthStatus.Foreground = BrushFrom("#FCD34D");

        try
        {
            GamResult result = await _healthService.CheckAsync();

            if (result.Success)
            {
                string version = FirstNonEmptyLine(result.Output);

                _healthStatus.Text = string.IsNullOrWhiteSpace(version)
                    ? "GAM7 responded successfully."
                    : $"GAM7 is responding: {version}";

                _healthStatus.Foreground = BrushFrom("#86EFAC");
                _message.Text = "GAM7 health check succeeded.";
                _message.Foreground = BrushFrom("#86EFAC");
            }
            else
            {
                _healthStatus.Text = "GAM7 check failed.";
                _healthStatus.Foreground = BrushFrom("#FCA5A5");

                _message.Text = string.IsNullOrWhiteSpace(result.Error)
                    ? $"GAM7 exited with code {result.ExitCode}."
                    : result.Error;

                _message.Foreground = BrushFrom("#FCA5A5");
            }
        }
        catch (Exception ex)
        {
            _healthStatus.Text = "GAM7 check failed.";
            _healthStatus.Foreground = BrushFrom("#FCA5A5");
            _message.Text = ex.Message;
            _message.Foreground = BrushFrom("#FCA5A5");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task SearchAsync()
    {
        string query = _searchBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(query))
        {
            _message.Text = "Enter a serial number or asset ID first.";
            _message.Foreground = BrushFrom("#FCD34D");
            _searchBox.Focus();
            return;
        }

        SetBusy(true);
        _message.Text = "Searching Google Admin...";
        _message.Foreground = BrushFrom("#FCD34D");

        _detailsPanel.Children.Clear();
        _detailsPanel.Children.Add(CreateText(
            "Searching...", 12, "#9CA3AF",
            FontWeights.Normal, new Thickness(0)));

        try
        {
            ChromebookInfo? device;
            GamResult result;

            if (_searchMode.SelectedIndex == 1)
            {
                (device, result) =
                    await _gamService.GetChromebookInfoByAssetIdAsync(query);
            }
            else
            {
                (device, result) =
                    await _gamService.GetChromebookInfoAsync(query);
            }

            if (!result.Success || device is null)
            {
                _detailsPanel.Children.Clear();
                _detailsPanel.Children.Add(CreateText(
                    "No device details available.", 12, "#9CA3AF",
                    FontWeights.Normal, new Thickness(0)));

                string error = FirstNonEmptyLine(result.Error);

                if (string.IsNullOrWhiteSpace(error))
                    error = FirstNonEmptyLine(result.Output);

                _message.Text = string.IsNullOrWhiteSpace(error)
                    ? $"Lookup failed (exit code {result.ExitCode})."
                    : error;

                _message.Foreground = BrushFrom("#FCA5A5");
                return;
            }

            ShowDevice(device);

            _message.Text = "Chromebook details loaded successfully.";
            _message.Foreground = BrushFrom("#86EFAC");
        }
        catch (Exception ex)
        {
            _detailsPanel.Children.Clear();
            _message.Text = $"Lookup failed: {ex.Message}";
            _message.Foreground = BrushFrom("#FCA5A5");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private async Task RefreshAssetCacheAsync()
    {
        string assetId = _searchBox.Text.Trim();

        if (_searchMode.SelectedIndex != 1)
        {
            _message.Text =
                "Select Asset ID search and enter an asset ID to refresh the cache.";
            _message.Foreground = BrushFrom("#FCD34D");
            return;
        }

        if (string.IsNullOrWhiteSpace(assetId))
        {
            _message.Text = "Enter an asset ID first.";
            _message.Foreground = BrushFrom("#FCD34D");
            return;
        }

        SetBusy(true);
        _message.Text = "Refreshing the asset ID cache and searching...";
        _message.Foreground = BrushFrom("#FCD34D");

        try
        {
            var (device, result) =
                await _gamService.GetChromebookInfoByAssetIdAsync(
                    assetId, forceRefresh: true);

            if (!result.Success || device is null)
            {
                _message.Text = string.IsNullOrWhiteSpace(result.Error)
                    ? $"Lookup failed (exit code {result.ExitCode})."
                    : result.Error;

                _message.Foreground = BrushFrom("#FCA5A5");
                return;
            }

            ShowDevice(device);
            _message.Text = "Asset ID cache refreshed; device details loaded.";
            _message.Foreground = BrushFrom("#86EFAC");
        }
        catch (Exception ex)
        {
            _message.Text = $"Cache refresh failed: {ex.Message}";
            _message.Foreground = BrushFrom("#FCA5A5");
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void ShowDevice(ChromebookInfo device)
    {
        _detailsPanel.Children.Clear();

        AddDetail("Serial number", device.SerialNumber);
        AddDetail("Asset ID", device.AnnotatedAssetId);
        AddDetail("Model", device.Model);
        AddDetail("MAC address", device.MacAddress);
        AddDetail("Organisation unit", device.OrgUnitPath);
        AddDetail("Organisation unit ID", device.OrgUnitId);
        AddDetail("Recent user", device.RecentUserEmail);
        AddDetail("Last sync", device.LastSync);
        AddDetail("Status", device.Status);
        AddDetail("Device ID", device.DeviceId);
    }

    private void AddDetail(string label, string? value)
    {
        var row = new Grid { Margin = new Thickness(0, 0, 0, 12) };

        row.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(190) });
        row.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var labelText = CreateText(
            label, 12, "#9CA3AF", FontWeights.Normal,
            new Thickness(0, 0, 12, 0));

        var valueText = CreateText(
            string.IsNullOrWhiteSpace(value) ? "—" : value,
            13, "#F9FAFB", FontWeights.Normal, new Thickness(0));

        valueText.TextWrapping = TextWrapping.Wrap;

        Grid.SetColumn(labelText, 0);
        Grid.SetColumn(valueText, 1);

        row.Children.Add(labelText);
        row.Children.Add(valueText);

        _detailsPanel.Children.Add(row);
    }

    private void SetBusy(bool busy)
    {
        _searchButton.IsEnabled = !busy;
        _refreshButton.IsEnabled = !busy;
        _healthButton.IsEnabled = !busy;
        _searchMode.IsEnabled = !busy;
        _searchBox.IsEnabled = !busy;
        Cursor = busy ? System.Windows.Input.Cursors.Wait : null;
    }

    private static string FirstNonEmptyLine(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        foreach (string line in value.Split(
                     new[] { "\r\n", "\n" },
                     StringSplitOptions.RemoveEmptyEntries))
        {
            string trimmed = line.Trim();

            if (!string.IsNullOrWhiteSpace(trimmed))
                return trimmed;
        }

        return string.Empty;
    }

    private static Border CreateCard()
    {
        return new Border
        {
            Background = BrushFrom("#1F2937"),
            BorderBrush = BrushFrom("#303B4B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 0, 18)
        };
    }

    private static Button CreateButton(string text, string background)
    {
        return new Button
        {
            Content = text,
            Height = 38,
            Padding = new Thickness(14, 0, 14, 0),
            Background = BrushFrom(background),
            Foreground = BrushFrom("#F9FAFB"),
            BorderBrush = BrushFrom("#3B4A60"),
            BorderThickness = new Thickness(1),
            Cursor = System.Windows.Input.Cursors.Hand,
            VerticalContentAlignment = VerticalAlignment.Center
        };
    }

    private static TextBlock CreateText(
        string text,
        double fontSize,
        string foreground,
        FontWeight weight,
        Thickness margin)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = fontSize,
            Foreground = BrushFrom(foreground),
            FontWeight = weight,
            Margin = margin,
            TextWrapping = TextWrapping.Wrap
        };
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        return new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color)!);
    }
}
