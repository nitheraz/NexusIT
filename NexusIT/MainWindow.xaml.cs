
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Effects;
using NexusIT.App.Views;
using NexusIT.App.Views.Rdp;
using NexusIT.App.Services;

namespace NexusIT.App;

public partial class MainWindow : Window
{
    private readonly List<Button> _navigationButtons = [];
    private readonly ModuleService _moduleService = new();

    private static readonly Dictionary<string, string> PageSubtitles = new()
    {
        ["Dashboard"] = "Your central workspace for IT administration.",
        ["Chromebooks"] = "Search and manage ChromeOS devices.",
        ["Google Groups"] = "Manage Google Groups and membership.",
        ["Active Directory"] = "Manage computers and organisational units.",
        ["MECM / SCCM"] = "Manage devices and collection memberships.",
        ["RDP"] = "Manage your remote desktop connections.",
        ["Admin Tools"] = "Access your everyday administration utilities.",
        ["Network & System Tools"] = "Network diagnostics and Windows system utilities.",
        ["Switch Diagnostics"] = "Connect to Aruba switches and troubleshoot network ports and connected devices.",
        ["Web Tools"] = "Launch and manage your web-based IT administration tools.",
        ["Settings"] = "Configure modules and application preferences."
    };

    public MainWindow()
    {
        InitializeComponent();

        GlobalMouseWheelScrolling.Attach(this);

        WindowState = WindowState.Maximized;

        Loaded += (_, _) =>
        {
            CollectNavigationButtons(this);
            ApplyModuleVisibility();
            NavigateTo("Dashboard");
        };
    }

    // =========================================================
    // NAVIGATION
    // =========================================================

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string page)
        {
            if (page != "Dashboard" &&
                page != "Settings" &&
                !_moduleService.IsEnabled(page))
            {
                return;
            }

            NavigateTo(page);
        }
    }

    private void NavigateTo(string page)
    {
        CurrentPageTitle.Text = page;

        CurrentPageSubtitle.Text =
            PageSubtitles.TryGetValue(page, out string? subtitle)
                ? subtitle
                : "IT administration workspace.";

        foreach (Button button in _navigationButtons)
        {
            bool isActive = string.Equals(
                button.Tag?.ToString(),
                page,
                StringComparison.OrdinalIgnoreCase);

            button.Background = isActive
                ? BrushFrom("#663B82F6")
                : Brushes.Transparent;

            button.Foreground = isActive
                ? BrushFrom("#93C5FD")
                : BrushFrom("#D1D5DB");

            button.BorderBrush = isActive
                ? BrushFrom("#665B9CF0")
                : Brushes.Transparent;
        }

        switch (page)
        {
            case "Dashboard":
                MainContent.Content = CreateDashboard();
                break;

            case "Chromebooks":
                MainContent.Content = new ChromebookManagementView();
                break;

            case "RDP":
                MainContent.Content = new RdpManagerView();
                break;

            case "Admin Tools":
                MainContent.Content = new AdminToolsView();
                break;

            case "Network & System Tools":
                MainContent.Content = new NetworkSystemToolsView();
                break;

            case "Switch Diagnostics":
                MainContent.Content = new SwitchManagementView();
                break;

            case "Active Directory":
                MainContent.Content = new ActiveDirectoryView();
                break;

            case "MECM / SCCM":
                MainContent.Content = new MecmView();
                break;

            case "Web Tools":
                MainContent.Content = new WebToolsView();
                break;

            case "Settings":
                var settingsView = new SettingsView(_moduleService);
                settingsView.ModulesChanged += SettingsView_ModulesChanged;
                MainContent.Content = settingsView;
                break;

            default:
                MainContent.Content = CreatePlaceholderPage(page);
                break;
        }

        StatusText.Text = $"Current workspace: {page}";
    }

    private void SettingsView_ModulesChanged(object? sender, EventArgs e)
    {
        ApplyModuleVisibility();

        string currentPage = CurrentPageTitle.Text;

        if (currentPage != "Dashboard" &&
            currentPage != "Settings" &&
            !_moduleService.IsEnabled(currentPage))
        {
            NavigateTo("Dashboard");
        }
    }

    private void ApplyModuleVisibility()
    {
        foreach (Button button in _navigationButtons)
        {
            if (button.Tag is not string page)
                continue;

            if (page == "Dashboard" || page == "Settings")
            {
                button.Visibility = Visibility.Visible;
                continue;
            }

            button.Visibility = _moduleService.IsEnabled(page)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }
    }

    private void CollectNavigationButtons(DependencyObject parent)
    {
        int childCount = VisualTreeHelper.GetChildrenCount(parent);

        for (int i = 0; i < childCount; i++)
        {
            DependencyObject child = VisualTreeHelper.GetChild(parent, i);

            if (child is Button button && button.Tag is string)
                _navigationButtons.Add(button);

            CollectNavigationButtons(child);
        }
    }

    // =========================================================
    // DASHBOARD
    // =========================================================

    private UIElement CreateDashboard()
    {
        var page = new StackPanel();

        page.Children.Add(new TextBlock
        {
            Text = "Welcome to Nexus IT",
            FontSize = 27,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 8)
        });

        page.Children.Add(new TextBlock
        {
            Text = "Everything you need to manage your IT environment, in one place.",
            FontSize = 14,
            Foreground = BrushFrom("#B0BDCE"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 30)
        });

        page.Children.Add(new TextBlock
        {
            Text = "Administration workspaces",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 16)
        });

        var cards = new UniformGrid
        {
            Columns = 2,
            Rows = 4
        };

        if (_moduleService.IsEnabled("Chromebooks"))
        {
            AddDashboardCard(
                cards,
                "Chromebooks",
                "▣",
                "Search devices and perform bulk operations.",
                "#93C5FD");
        }

        if (_moduleService.IsEnabled("Google Groups"))
        {
            AddDashboardCard(
                cards,
                "Google Groups",
                "♙",
                "Manage groups, members and membership roles.",
                "#86EFAC");
        }

        if (_moduleService.IsEnabled("Active Directory"))
        {
            AddDashboardCard(
                cards,
                "Active Directory",
                "⌘",
                "Look up computers, move OUs and manage objects.",
                "#C4B5FD");
        }

        if (_moduleService.IsEnabled("MECM / SCCM"))
        {
            AddDashboardCard(
                cards,
                "MECM / SCCM",
                "▤",
                "Manage devices and collection memberships.",
                "#FCD34D");
        }

        if (_moduleService.IsEnabled("RDP"))
        {
            AddDashboardCard(
                cards,
                "RDP",
                "▱",
                "Open and manage remote desktop connections.",
                "#67E8F9");
        }

        if (_moduleService.IsEnabled("Admin Tools"))
        {
            AddDashboardCard(
                cards,
                "Admin Tools",
                "⚒",
                "Access the existing miscellaneous admin utilities.",
                "#F9A8D4");
        }

        if (_moduleService.IsEnabled("Network & System Tools"))
        {
            AddDashboardCard(
                cards,
                "Network & System Tools",
                "⌘",
                "Network diagnostics and Windows system utilities.",
                "#A5B4FC");
        }

        if (_moduleService.IsEnabled("Web Tools"))
        {
            AddDashboardCard(
                cards,
                "Web Tools",
                "🌐",
                "Launch Google Admin, ServiceNow, Aruba Central and other web-based IT systems.",
                "#60A5FA");
        }

        AddDashboardCard(
            cards,
            "Settings",
            "⚙",
            "Configure modules and application preferences.",
            "#D1D5DB");

        page.Children.Add(cards);

        // GETTING STARTED GLASS PANEL

        var noteContent = new StackPanel();

        noteContent.Children.Add(new TextBlock
        {
            Text = "Getting started",
            FontWeight = FontWeights.SemiBold,
            FontSize = 14,
            Foreground = BrushFrom("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 7)
        });

        noteContent.Children.Add(new TextBlock
        {
            Text = "Nexus IT brings your IT administration workspaces together in one place. Enable the modules your organisation needs from Settings.",
            Foreground = BrushFrom("#B0BDCE"),
            TextWrapping = TextWrapping.Wrap,
            FontSize = 13,
            LineHeight = 20
        });

        var note = new Border
        {
            Background = CreateGlassGradient(),
            BorderBrush = BrushFrom("#665F7896"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 12, 0, 0),
            Child = noteContent,
            Effect = CreateGlassShadow()
        };

        page.Children.Add(note);

        return page;
    }

    // =========================================================
    // DASHBOARD CARDS
    // =========================================================

    private void AddDashboardCard(
        Panel parent,
        string title,
        string icon,
        string description,
        string accent)
    {
        var content = new StackPanel();

        // CARD HEADING

        var heading = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12)
        };

        heading.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 25,
            Foreground = BrushFrom(accent),
            Width = 44,
            VerticalAlignment = VerticalAlignment.Center
        });

        heading.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#F9FAFB"),
            VerticalAlignment = VerticalAlignment.Center
        });

        content.Children.Add(heading);

        // DESCRIPTION

        content.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = BrushFrom("#B0BDCE"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19
        });

        // OPEN WORKSPACE LABEL

        content.Children.Add(new TextBlock
        {
            Text = "Open workspace  →",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom(accent),
            Margin = new Thickness(0, 16, 0, 0)
        });

        // RADIAL GLASS CARD

        var card = new Border
        {
            Background = CreateGlassGradient(),
            BorderBrush = BrushFrom("#665F7896"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 14, 14),
            MinHeight = 155,
            Cursor = Cursors.Hand,
            Child = content,
            Effect = CreateGlassShadow()
        };

        // CARD CLICK

        card.MouseLeftButtonUp += (_, e) =>
        {
            NavigateTo(title);
            e.Handled = true;
        };

        // HOVER EFFECT

        card.MouseEnter += (_, _) =>
        {
            card.Background = CreateGlassHoverGradient();
            card.BorderBrush = BrushFrom(accent);
            card.Effect = CreateGlassHoverShadow();
        };

        card.MouseLeave += (_, _) =>
        {
            card.Background = CreateGlassGradient();
            card.BorderBrush = BrushFrom("#665F7896");
            card.Effect = CreateGlassShadow();
        };

        parent.Children.Add(card);
    }

    // =========================================================
    // RADIAL GLASS GRADIENT HELPERS
    // =========================================================

    private static RadialGradientBrush CreateGlassGradient()
    {
        return new RadialGradientBrush
        {
            // Darkest point in the centre
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),

            // Light fades towards the outside edges
            RadiusX = 0.95,
            RadiusY = 0.95,

            GradientStops =
            {
                new GradientStop(Color.FromArgb(245, 12, 24, 43), 0.0),
                new GradientStop(Color.FromArgb(235, 17, 33, 56), 0.35),
                new GradientStop(Color.FromArgb(225, 30, 56, 86), 0.70),
                new GradientStop(Color.FromArgb(215, 58, 88, 125), 1.0)
            }
        };
    }

    private static RadialGradientBrush CreateGlassHoverGradient()
    {
        return new RadialGradientBrush
        {
            Center = new Point(0.5, 0.5),
            GradientOrigin = new Point(0.5, 0.5),

            RadiusX = 0.95,
            RadiusY = 0.95,

            GradientStops =
            {
                new GradientStop(Color.FromArgb(250, 14, 29, 51), 0.0),
                new GradientStop(Color.FromArgb(245, 21, 43, 72), 0.35),
                new GradientStop(Color.FromArgb(240, 39, 72, 111), 0.70),
                new GradientStop(Color.FromArgb(235, 76, 115, 160), 1.0)
            }
        };
    }

    // =========================================================
    // GLASS SHADOW HELPERS
    // =========================================================

    private static DropShadowEffect CreateGlassShadow()
    {
        return new DropShadowEffect
        {
            Color = Color.FromRgb(0, 0, 0),
            BlurRadius = 16,
            ShadowDepth = 3,
            Opacity = 0.18
        };
    }

    private static DropShadowEffect CreateGlassHoverShadow()
    {
        return new DropShadowEffect
        {
            Color = Color.FromRgb(59, 130, 246),
            BlurRadius = 24,
            ShadowDepth = 2,
            Opacity = 0.28
        };
    }

    // =========================================================
    // PLACEHOLDER PAGE
    // =========================================================

    private UIElement CreatePlaceholderPage(string page)
    {
        var container = new StackPanel();

        var content = new StackPanel();

        content.Children.Add(new TextBlock
        {
            Text = page,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 12)
        });

        content.Children.Add(new TextBlock
        {
            Text = PageSubtitles.TryGetValue(page, out string? subtitle)
                ? subtitle
                : "Manage your IT environment.",
            Foreground = BrushFrom("#B0BDCE"),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        });

        content.Children.Add(new Border
        {
            Background = BrushFrom("#663B3020"),
            BorderBrush = BrushFrom("#997F6330"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(9),
            Padding = new Thickness(12, 9, 12, 9),
            HorizontalAlignment = HorizontalAlignment.Left,
            Child = new TextBlock
            {
                Text = "Development placeholder",
                Foreground = BrushFrom("#FCD34D"),
                FontSize = 12,
                FontWeight = FontWeights.SemiBold
            }
        });

        content.Children.Add(new TextBlock
        {
            Text = $"The {page} workspace has been created in the application shell. " +
                   "Its real management functionality will be migrated in a later stage.",
            Foreground = BrushFrom("#D1D5DB"),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 20, 0, 0),
            MaxWidth = 650
        });

        var panel = new Border
        {
            Background = CreateGlassGradient(),
            BorderBrush = BrushFrom("#665F7896"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(14),
            Padding = new Thickness(26),
            Margin = new Thickness(0, 0, 0, 20),
            Child = content,
            Effect = CreateGlassShadow()
        };

        container.Children.Add(panel);

        // BACK TO DASHBOARD

        var backButton = new Button
        {
            Content = "←  Back to Dashboard",
            Padding = new Thickness(15, 9, 15, 9),
            Background = BrushFrom("#B3263F5B"),
            Foreground = BrushFrom("#F9FAFB"),
            BorderBrush = BrushFrom("#665F7896"),
            BorderThickness = new Thickness(1),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        backButton.Click += (_, _) => NavigateTo("Dashboard");

        container.Children.Add(backButton);

        return container;
    }

    // =========================================================
    // COLOUR HELPER
    // =========================================================

    private static SolidColorBrush BrushFrom(string color)
    {
        return new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color)!);
    }
}
