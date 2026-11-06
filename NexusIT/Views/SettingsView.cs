
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using NexusIT.App.Services;

namespace NexusIT.App.Views;

public class SettingsView : UserControl
{
    private readonly ModuleService _moduleService;

    public event EventHandler? ModulesChanged;

    public SettingsView(ModuleService moduleService)
    {
        _moduleService = moduleService;

        BuildInterface();
    }

    private void BuildInterface()
    {
        var page = new StackPanel();

        page.Children.Add(new TextBlock
        {
            Text = "Application Settings",
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        page.Children.Add(new TextBlock
        {
            Text = "Manage which administration modules are available in Nexus IT.",
            FontSize = 13,
            Foreground = BrushFrom("#9CA3AF"),
            Margin = new Thickness(0, 0, 0, 24),
            TextWrapping = TextWrapping.Wrap
        });

        page.Children.Add(CreateSection(
            "Google Workspace",
            "Enable the Google administration tools your organisation uses.",
            new[]
            {
                ("Chromebooks", "Search and manage ChromeOS devices."),
                ("Google Groups", "Manage Google Groups and membership.")
            }));

        page.Children.Add(CreateSection(
            "Infrastructure & Administration",
            "Tools for managing your Windows and infrastructure environment.",
            new[]
            {
                ("Active Directory", "Manage computers and organisational units."),
                ("MECM / SCCM", "Manage devices and collection memberships."),
                ("RDP", "Manage remote desktop connections."),
                ("Admin Tools", "Access everyday administration utilities."),
                ("Network & System Tools", "Network diagnostics and Windows system utilities."),
                ("Web Tools", "Launch web-based IT administration systems.")
            }));

        var note = new Border
        {
            Background = BrushFrom("#172033"),
            BorderBrush = BrushFrom("#29364B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 20, 0, 0),
            Child = new TextBlock
            {
                Text = "Module changes are saved automatically. Disabled modules are hidden from the navigation sidebar and dashboard. Their configuration and existing data are retained.",
                Foreground = BrushFrom("#9CA3AF"),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            }
        };

        page.Children.Add(note);

        Content = page;
    }

    private UIElement CreateSection(
        string title,
        string description,
        (string Name, string Description)[] modules)
    {
        var section = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 18)
        };

        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 12)
        };

        header.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 17,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 5)
        });

        header.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = BrushFrom("#9CA3AF"),
            TextWrapping = TextWrapping.Wrap
        });

        section.Children.Add(header);

        foreach (var module in modules)
        {
            section.Children.Add(CreateModuleRow(
                module.Name,
                module.Description));
        }

        return section;
    }

    private UIElement CreateModuleRow(
        string moduleName,
        string description)
    {
        var details = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        details.Children.Add(new TextBlock
        {
            Text = moduleName,
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#F9FAFB")
        });

        details.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 11,
            Foreground = BrushFrom("#9CA3AF"),
            Margin = new Thickness(0, 4, 0, 0),
            TextWrapping = TextWrapping.Wrap
        });

        var toggle = new CheckBox
        {
            IsChecked = _moduleService.IsEnabled(moduleName),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
            Cursor = System.Windows.Input.Cursors.Hand,
            Content = "Enabled",
            Foreground = BrushFrom("#D1D5DB"),
            FontSize = 12
        };

        toggle.Checked += (_, _) => UpdateModule(moduleName, true);
        toggle.Unchecked += (_, _) => UpdateModule(moduleName, false);

        var grid = new Grid();

        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        grid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        Grid.SetColumn(details, 0);
        Grid.SetColumn(toggle, 1);

        grid.Children.Add(details);
        grid.Children.Add(toggle);

        return new Border
        {
            Background = BrushFrom("#1F2937"),
            BorderBrush = BrushFrom("#303B4B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 0, 9),
            Child = grid
        };
    }

    private void UpdateModule(string moduleName, bool enabled)
    {
        try
        {
            _moduleService.SetEnabled(moduleName, enabled);
            ModulesChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Module Settings",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        return new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color)!);
    }
}
