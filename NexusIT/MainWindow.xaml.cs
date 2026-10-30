
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using NexusIT.App.Views;
using NexusIT.App.Views.Rdp;

namespace NexusIT.App;

public partial class MainWindow : Window
{
    private readonly List<Button> _navigationButtons = [];

    private static readonly Dictionary<string, string> PageSubtitles = new()
    {
        ["Dashboard"] = "Your central workspace for IT administration.",
        ["Chromebooks"] = "Search and manage ChromeOS devices.",
        ["Google Groups"] = "Manage Google Groups and membership.",
        ["Active Directory"] = "Manage computers and organisational units.",
        ["MECM / SCCM"] = "Manage devices and collection memberships.",
        ["RDP"] = "Manage your remote desktop connections.",
        ["Admin Tools"] = "Access your everyday administration utilities.",
        ["Settings"] = "Configure integrations and application preferences."
    };

    public MainWindow()
    {
        InitializeComponent();

        Loaded += (_, _) =>
        {
            CollectNavigationButtons(this);
            NavigateTo("Dashboard");
        };
    }

    private void NavigationButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button button && button.Tag is string page)
            NavigateTo(page);
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
                ? BrushFrom("#1D3557")
                : Brushes.Transparent;

            button.Foreground = isActive
                ? BrushFrom("#93C5FD")
                : BrushFrom("#D1D5DB");
        }

        MainContent.Content = page switch
        {
            "Dashboard" => CreateDashboard(),
            "Chromebooks" => new ChromebookManagementView(),
            "RDP" => new RdpManagerView(),
            _ => CreatePlaceholderPage(page)
        };

        StatusText.Text = $"Current workspace: {page}";
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
            Foreground = BrushFrom("#9CA3AF"),
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 30)
        });

        page.Children.Add(new TextBlock
        {
            Text = "Administration workspaces",
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 14)
        });

        var cards = new UniformGrid
        {
            Columns = 2,
            Rows = 4
        };

        AddDashboardCard(cards, "Chromebooks", "▣",
            "Search devices and perform bulk operations.", "#93C5FD");

        AddDashboardCard(cards, "Google Groups", "♙",
            "Manage groups, members and membership roles.", "#86EFAC");

        AddDashboardCard(cards, "Active Directory", "⌘",
            "Look up computers, move OUs and manage objects.", "#C4B5FD");

        AddDashboardCard(cards, "MECM / SCCM", "▤",
            "Manage devices and collection memberships.", "#FCD34D");

        AddDashboardCard(cards, "RDP", "▱",
            "Open and manage remote desktop connections.", "#67E8F9");

        AddDashboardCard(cards, "Admin Tools", "⚒",
            "Access the existing miscellaneous admin utilities.", "#F9A8D4");

        AddDashboardCard(cards, "Settings", "⚙",
            "Configure integrations and application preferences.", "#D1D5DB");

        page.Children.Add(cards);

        var note = new Border
        {
            Background = BrushFrom("#172033"),
            BorderBrush = BrushFrom("#29364B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 24, 0, 0),
            Child = new StackPanel
            {
                Children =
                {
                    new TextBlock
                    {
                        Text = "Getting started",
                        FontWeight = FontWeights.SemiBold,
                        Foreground = BrushFrom("#F9FAFB"),
                        Margin = new Thickness(0, 0, 0, 6)
                    },
                    new TextBlock
                    {
                        Text = "The application shell is ready for development. " +
                               "Management integrations are being connected in stages.",
                        Foreground = BrushFrom("#9CA3AF"),
                        TextWrapping = TextWrapping.Wrap
                    }
                }
            }
        };

        page.Children.Add(note);
        return page;
    }

    private void AddDashboardCard(
        Panel parent,
        string title,
        string icon,
        string description,
        string accent)
    {
        var content = new StackPanel();

        var heading = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 12)
        };

        heading.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 24,
            Foreground = BrushFrom(accent),
            Width = 42,
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

        content.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = BrushFrom("#9CA3AF"),
            TextWrapping = TextWrapping.Wrap,
            LineHeight = 19
        });

        content.Children.Add(new TextBlock
        {
            Text = "Open workspace  →",
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom(accent),
            Margin = new Thickness(0, 16, 0, 0)
        });

        var card = new Border
        {
            Background = BrushFrom("#1F2937"),
            BorderBrush = BrushFrom("#303B4B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(20),
            Margin = new Thickness(0, 0, 14, 14),
            MinHeight = 155,
            Cursor = Cursors.Hand,
            Child = content
        };

        card.MouseLeftButtonUp += (_, e) =>
        {
            NavigateTo(title);
            e.Handled = true;
        };

        card.MouseEnter += (_, _) =>
        {
            card.Background = BrushFrom("#273449");
            card.BorderBrush = BrushFrom(accent);
        };

        card.MouseLeave += (_, _) =>
        {
            card.Background = BrushFrom("#1F2937");
            card.BorderBrush = BrushFrom("#303B4B");
        };

        parent.Children.Add(card);
    }

    private UIElement CreatePlaceholderPage(string page)
    {
        var container = new StackPanel();

        var panel = new Border
        {
            Background = BrushFrom("#1F2937"),
            BorderBrush = BrushFrom("#303B4B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(12),
            Padding = new Thickness(26),
            Margin = new Thickness(0, 0, 0, 20)
        };

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
            Foreground = BrushFrom("#9CA3AF"),
            FontSize = 14,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 0, 0, 20)
        });

        content.Children.Add(new Border
        {
            Background = BrushFrom("#332B1B"),
            BorderBrush = BrushFrom("#66502B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(7),
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

        panel.Child = content;
        container.Children.Add(panel);

        var backButton = new Button
        {
            Content = "←  Back to Dashboard",
            Padding = new Thickness(15, 9, 15, 9),
            Background = BrushFrom("#26364B"),
            Foreground = BrushFrom("#F9FAFB"),
            BorderThickness = new Thickness(0),
            Cursor = Cursors.Hand,
            HorizontalAlignment = HorizontalAlignment.Left
        };

        backButton.Click += (_, _) => NavigateTo("Dashboard");
        container.Children.Add(backButton);

        return container;
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        return new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color)!);
    }
}
