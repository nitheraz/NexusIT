
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using NexusIT.App.Services;

namespace NexusIT.App.Views;

public sealed class WebToolsView : UserControl
{
    private const string AllCategoriesFilter = "All categories";
    private const string FavoritesFilter = "★ Favourites";
    private const string RecentFilter = "Recently used";

    private readonly WebToolService _webToolService;
    private readonly ChromeProfileService _chromeProfileService;

    private readonly List<WebTool> _tools;
    private readonly List<ChromeProfile> _chromeProfiles;

    private readonly ListBox _toolsList;
    private readonly TextBox _searchBox;
    private readonly ComboBox _categoryFilter;

    private readonly TextBox _nameTextBox;
    private readonly TextBox _categoryTextBox;
    private readonly TextBox _descriptionTextBox;
    private readonly TextBox _iconTextBox;
    private readonly TextBox _urlTextBox;
    private readonly ComboBox _profileComboBox;

    private readonly TextBlock _statusText;
    private readonly TextBlock _selectedToolTitle;

    private readonly Button _deleteButton;
    private readonly Button _favoriteButton;
    private readonly Button _removeIconButton;

    private readonly Border _iconPreviewBorder;
    private readonly Image _iconPreviewImage;
    private readonly TextBlock _iconPreviewText;

    private WebTool? _selectedTool;

    private static readonly Brush PanelBrush = BrushFrom("#1F2937");
    private static readonly Brush InputBrush = BrushFrom("#111827");
    private static readonly Brush MainTextBrush = BrushFrom("#E5E7EB");
    private static readonly Brush MutedTextBrush = BrushFrom("#9CA3AF");
    private static readonly Brush FavoriteBrush = BrushFrom("#D97706");

    public WebToolsView()
    {
        _webToolService = new WebToolService();
        _chromeProfileService = new ChromeProfileService();

        _tools = _webToolService.GetTools();
        _chromeProfiles = _chromeProfileService.GetProfiles();

        _toolsList = new ListBox
        {
            Background = Brushes.Transparent,
            BorderThickness = new Thickness(0),
            Foreground = MainTextBrush,
            FontSize = 13,
            Padding = new Thickness(0)
        };

        _searchBox = CreateTextBox();
        _searchBox.Margin = new Thickness(0, 7, 0, 12);
        _searchBox.ToolTip = "Search tools";

        _categoryFilter = new ComboBox
        {
            Height = 38,
            FontSize = 13,
            Padding = new Thickness(8, 5, 8, 5),
            Background = InputBrush,
            Foreground = MainTextBrush,
            BorderBrush = BrushFrom("#374151"),
            Margin = new Thickness(0, 7, 0, 12)
        };

        _selectedToolTitle = new TextBlock
        {
            Text = "Select a web tool",
            FontSize = 23,
            FontWeight = FontWeights.SemiBold,
            Foreground = MainTextBrush,
            Margin = new Thickness(0, 0, 0, 20)
        };

        _nameTextBox = CreateTextBox();
        _categoryTextBox = CreateTextBox();
        _descriptionTextBox = CreateTextBox();
        _iconTextBox = CreateTextBox();
        _urlTextBox = CreateTextBox();

        _profileComboBox = new ComboBox
        {
            Height = 38,
            FontSize = 13,
            Padding = new Thickness(8, 5, 8, 5),
            Background = InputBrush,
            Foreground = MainTextBrush,
            BorderBrush = BrushFrom("#374151"),
            ItemsSource = _chromeProfiles
        };

        _statusText = new TextBlock
        {
            FontSize = 12,
            Foreground = MutedTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 16, 0, 0)
        };

        _deleteButton = CreateButton(
            "Delete Tool",
            "DangerButton");

        _deleteButton.IsEnabled = false;

        _favoriteButton = CreateButton(
            "☆ Add to Favourites",
            "SecondaryButton");

        _favoriteButton.IsEnabled = false;

        _iconPreviewBorder = new Border
        {
            Width = 64,
            Height = 64,
            Background = BrushFrom("#26364B"),
            CornerRadius = new CornerRadius(10),
            BorderBrush = BrushFrom("#374151"),
            BorderThickness = new Thickness(1),
            ClipToBounds = true
        };

        _iconPreviewImage = new Image
        {
            Width = 64,
            Height = 64,
            Stretch = Stretch.UniformToFill,
            Visibility = Visibility.Collapsed
        };

        _iconPreviewText = new TextBlock
        {
            Text = "🌐",
            FontSize = 25,
            FontWeight = FontWeights.Bold,
            Foreground = BrushFrom("#93C5FD"),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };

        _iconPreviewBorder.Child = _iconPreviewText;

        _removeIconButton = CreateButton(
            "Remove Icon",
            "SecondaryButton");

        _removeIconButton.IsEnabled = false;
        _removeIconButton.Click += RemoveIconButton_Click;

        BuildInterface();

        _searchBox.TextChanged += (_, _) => RefreshToolList();

        _categoryFilter.SelectionChanged +=
            CategoryFilter_SelectionChanged;

        _toolsList.SelectionChanged +=
            ToolsList_SelectionChanged;

        RefreshCategoryFilter();
        RefreshToolList();
    }

    private void BuildInterface()
    {
        var root = new Grid
        {
            Margin = new Thickness(0, 5, 0, 10)
        };

        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        // TOP TOOLBAR

        var toolbar = new Border
        {
            Background = PanelBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16, 12, 16, 12),
            Margin = new Thickness(0, 0, 0, 12)
        };

        var toolbarGrid = new Grid();

        toolbarGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

        toolbarGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var toolbarInfo = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center
        };

        toolbarInfo.Children.Add(new TextBlock
        {
            Text = "Manage Web Tools",
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = MainTextBrush
        });

        toolbarInfo.Children.Add(new TextBlock
        {
            Text = "Launch, organise and manage your website shortcuts.",
            FontSize = 12,
            Foreground = MutedTextBrush,
            Margin = new Thickness(0, 4, 0, 0)
        });

        Grid.SetColumn(toolbarInfo, 0);
        toolbarGrid.Children.Add(toolbarInfo);

        var toolbarButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center
        };

        var addButton = CreateButton(
            "+ Add Tool",
            "PrimaryButton");

        addButton.Click += AddButton_Click;

        _deleteButton.Margin = new Thickness(10, 0, 0, 0);
        _deleteButton.Click += DeleteButton_Click;

        toolbarButtons.Children.Add(addButton);
        toolbarButtons.Children.Add(_deleteButton);

        Grid.SetColumn(toolbarButtons, 1);
        toolbarGrid.Children.Add(toolbarButtons);

        toolbar.Child = toolbarGrid;

        Grid.SetRow(toolbar, 0);
        root.Children.Add(toolbar);

        // MAIN CONTENT

        var contentGrid = new Grid();

        contentGrid.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(330) });

        contentGrid.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

        // LEFT TOOL LIST

        var leftPanel = new Border
        {
            Background = PanelBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 12, 0)
        };

        var leftGrid = new Grid();

        leftGrid.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        leftGrid.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        leftGrid.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        var searchPanel = new StackPanel();
        searchPanel.Children.Add(CreateLabel("SEARCH"));
        searchPanel.Children.Add(_searchBox);

        Grid.SetRow(searchPanel, 0);
        leftGrid.Children.Add(searchPanel);

        var categoryPanel = new StackPanel();
        categoryPanel.Children.Add(CreateLabel("VIEW"));
        categoryPanel.Children.Add(_categoryFilter);

        Grid.SetRow(categoryPanel, 1);
        leftGrid.Children.Add(categoryPanel);

        _toolsList.ItemContainerStyle =
            new Style(typeof(ListBoxItem))
            {
                Setters =
                {
                    new Setter(
                        Control.PaddingProperty,
                        new Thickness(8, 8, 8, 8)),

                    new Setter(
                        Control.MarginProperty,
                        new Thickness(0, 2, 0, 2)),

                    new Setter(
                        Control.HorizontalContentAlignmentProperty,
                        HorizontalAlignment.Stretch)
                }
            };

        Grid.SetRow(_toolsList, 2);
        leftGrid.Children.Add(_toolsList);

        leftPanel.Child = leftGrid;

        Grid.SetColumn(leftPanel, 0);
        contentGrid.Children.Add(leftPanel);

        // RIGHT EDITOR

        var rightPanel = new Border
        {
            Background = PanelBrush,
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(22)
        };

        var editorContent = new StackPanel();

        editorContent.Children.Add(_selectedToolTitle);

        editorContent.Children.Add(CreateLabel("TOOL NAME"));
        editorContent.Children.Add(_nameTextBox);

        editorContent.Children.Add(CreateLabel("CATEGORY"));
        editorContent.Children.Add(_categoryTextBox);

        editorContent.Children.Add(CreateLabel("DESCRIPTION"));

        _descriptionTextBox.Height = 65;
        _descriptionTextBox.AcceptsReturn = true;
        _descriptionTextBox.TextWrapping = TextWrapping.Wrap;
        _descriptionTextBox.VerticalContentAlignment =
            VerticalAlignment.Top;

        editorContent.Children.Add(_descriptionTextBox);

        editorContent.Children.Add(CreateLabel("ICON / SHORT LABEL"));
        editorContent.Children.Add(_iconTextBox);

        // CUSTOM ICON SECTION

        editorContent.Children.Add(CreateLabel("CUSTOM IMAGE ICON"));

        var iconPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 8, 0, 18)
        };

        iconPanel.Children.Add(_iconPreviewBorder);

        var iconButtons = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(14, 0, 0, 0)
        };

        var uploadIconButton = CreateButton(
            "Upload Icon",
            "PrimaryButton");

        uploadIconButton.Click += UploadIconButton_Click;

        _removeIconButton.Margin = new Thickness(0, 8, 0, 0);

        iconButtons.Children.Add(uploadIconButton);
        iconButtons.Children.Add(_removeIconButton);

        iconPanel.Children.Add(iconButtons);

        editorContent.Children.Add(iconPanel);

        editorContent.Children.Add(new TextBlock
        {
            Text = "Supported formats: PNG, JPG and JPEG.",
            FontSize = 11,
            Foreground = MutedTextBrush,
            Margin = new Thickness(0, -10, 0, 16)
        });

        editorContent.Children.Add(CreateLabel("WEBSITE ADDRESS"));
        editorContent.Children.Add(_urlTextBox);

        editorContent.Children.Add(CreateLabel("CHROME PROFILE"));
        editorContent.Children.Add(_profileComboBox);

        editorContent.Children.Add(new TextBlock
        {
            Text = _chromeProfiles.Count > 0
                ? "The selected existing Chrome profile will be used."
                : "No Chrome profiles detected. Open Chrome at least once and restart Nexus IT.",
            FontSize = 11,
            Foreground = MutedTextBrush,
            TextWrapping = TextWrapping.Wrap,
            Margin = new Thickness(0, 7, 0, 18)
        });

        var editorButtons = new WrapPanel();

        _favoriteButton.Margin = new Thickness(0, 0, 10, 10);
        _favoriteButton.Click += FavoriteButton_Click;

        var saveButton = CreateButton(
            "Save Tool",
            "PrimaryButton");

        saveButton.Margin = new Thickness(0, 0, 10, 10);
        saveButton.Click += SaveButton_Click;

        var openButton = CreateButton(
            "Open Website",
            "SuccessButton");

        openButton.Margin = new Thickness(0, 0, 10, 10);
        openButton.Click += OpenButton_Click;

        editorButtons.Children.Add(_favoriteButton);
        editorButtons.Children.Add(saveButton);
        editorButtons.Children.Add(openButton);

        editorContent.Children.Add(editorButtons);
        editorContent.Children.Add(_statusText);

        rightPanel.Child = new ScrollViewer
        {
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled,
            Content = editorContent
        };

        Grid.SetColumn(rightPanel, 1);
        contentGrid.Children.Add(rightPanel);

        Grid.SetRow(contentGrid, 1);
        root.Children.Add(contentGrid);

        Content = root;
    }

    private void RefreshCategoryFilter()
    {
        string? previousFilter =
            _categoryFilter.SelectedItem as string;

        var categories = _tools
            .Select(tool => tool.Category)
            .Where(category => !string.IsNullOrWhiteSpace(category))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(category => category)
            .ToList();

        _categoryFilter.SelectionChanged -=
            CategoryFilter_SelectionChanged;

        _categoryFilter.Items.Clear();

        _categoryFilter.Items.Add(AllCategoriesFilter);
        _categoryFilter.Items.Add(FavoritesFilter);
        _categoryFilter.Items.Add(RecentFilter);

        foreach (string category in categories)
            _categoryFilter.Items.Add(category);

        if (!string.IsNullOrWhiteSpace(previousFilter) &&
            _categoryFilter.Items.Contains(previousFilter))
        {
            _categoryFilter.SelectedItem = previousFilter;
        }
        else
        {
            _categoryFilter.SelectedIndex = 0;
        }

        _categoryFilter.SelectionChanged +=
            CategoryFilter_SelectionChanged;
    }

    private void CategoryFilter_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        RefreshToolList();
    }

    private void RefreshToolList()
    {
        string search = _searchBox.Text.Trim();

        string filter =
            _categoryFilter.SelectedItem as string
            ?? AllCategoriesFilter;

        IEnumerable<WebTool> filteredTools = _tools;

        if (filter == FavoritesFilter)
        {
            filteredTools = filteredTools.Where(tool => tool.IsFavorite);
        }
        else if (filter == RecentFilter)
        {
            filteredTools = filteredTools.Where(
                tool => tool.LastLaunchedUtc.HasValue);
        }
        else if (filter != AllCategoriesFilter)
        {
            filteredTools = filteredTools.Where(tool =>
                string.Equals(
                    tool.Category,
                    filter,
                    StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            filteredTools = filteredTools.Where(tool =>
                tool.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                tool.Description.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                tool.Category.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                tool.Url.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (filter == RecentFilter)
        {
            filteredTools = filteredTools
                .OrderByDescending(tool => tool.LastLaunchedUtc);
        }
        else
        {
            filteredTools = filteredTools
                .OrderByDescending(tool => tool.IsFavorite)
                .ThenByDescending(tool => tool.LastLaunchedUtc)
                .ThenBy(tool => tool.Category)
                .ThenBy(tool => tool.Name);
        }

        List<WebTool> results = filteredTools.ToList();

        _toolsList.Items.Clear();

        foreach (WebTool tool in results)
        {
            var rowGrid = new Grid();

            rowGrid.ColumnDefinitions.Add(
                new ColumnDefinition
                {
                    Width = new GridLength(1, GridUnitType.Star)
                });

            rowGrid.ColumnDefinitions.Add(
                new ColumnDefinition { Width = GridLength.Auto });

            var itemContent = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                VerticalAlignment = VerticalAlignment.Center
            };

            itemContent.Children.Add(CreateToolIcon(tool, 34));

            var details = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center
            };

            var nameRow = new StackPanel
            {
                Orientation = Orientation.Horizontal
            };

            if (tool.IsFavorite)
            {
                nameRow.Children.Add(new TextBlock
                {
                    Text = "★",
                    Foreground = FavoriteBrush,
                    FontSize = 13,
                    Margin = new Thickness(0, 0, 4, 0)
                });
            }

            nameRow.Children.Add(new TextBlock
            {
                Text = tool.Name,
                FontWeight = FontWeights.SemiBold,
                Foreground = MainTextBrush
            });

            details.Children.Add(nameRow);

            details.Children.Add(new TextBlock
            {
                Text = tool.Category,
                FontSize = 11,
                Foreground = MutedTextBrush,
                Margin = new Thickness(0, 3, 0, 0)
            });

            if (tool.LastLaunchedUtc.HasValue)
            {
                details.Children.Add(new TextBlock
                {
                    Text = "Last opened: " +
                           tool.LastLaunchedUtc.Value.ToLocalTime()
                               .ToString("dd MMM, h:mm tt"),
                    FontSize = 10,
                    Foreground = MutedTextBrush,
                    Margin = new Thickness(0, 3, 0, 0)
                });
            }

            itemContent.Children.Add(details);

            Grid.SetColumn(itemContent, 0);
            rowGrid.Children.Add(itemContent);

            var launchButton = CreateButton(
                "Open",
                "SuccessButton");

            launchButton.Padding = new Thickness(10, 6, 10, 6);
            launchButton.FontSize = 11;
            launchButton.VerticalAlignment = VerticalAlignment.Center;
            launchButton.Margin = new Thickness(8, 0, 0, 0);
            launchButton.Click += (_, _) => LaunchTool(tool);

            Grid.SetColumn(launchButton, 1);
            rowGrid.Children.Add(launchButton);

            _toolsList.Items.Add(new ListBoxItem
            {
                Content = rowGrid,
                Tag = tool,
                ToolTip = tool.Description
            });
        }

        if (_selectedTool != null)
        {
            foreach (ListBoxItem item in _toolsList.Items)
            {
                if (ReferenceEquals(item.Tag, _selectedTool))
                {
                    _toolsList.SelectedItem = item;
                    return;
                }
            }
        }

        if (_toolsList.Items.Count > 0)
        {
            _toolsList.SelectedIndex = 0;
        }
        else
        {
            _selectedTool = null;
            _deleteButton.IsEnabled = false;
            _favoriteButton.IsEnabled = false;
            _removeIconButton.IsEnabled = false;

            ClearEditor();
            _selectedToolTitle.Text = "No tools found";
        }
    }

    private Border CreateToolIcon(WebTool tool, double size)
    {
        var border = new Border
        {
            Width = size,
            Height = size,
            Background = BrushFrom("#26364B"),
            CornerRadius = new CornerRadius(7),
            Margin = new Thickness(0, 0, 9, 0),
            ClipToBounds = true
        };

        string? iconPath =
            _webToolService.GetCustomIconPath(tool.CustomIconFileName);

        BitmapImage? bitmap = iconPath == null
            ? null
            : LoadBitmap(iconPath, (int)size * 2);

        if (bitmap != null)
        {
            border.Child = new Image
            {
                Source = bitmap,
                Stretch = Stretch.UniformToFill,
                Width = size,
                Height = size
            };
        }
        else
        {
            border.Child = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(tool.Icon)
                    ? "🌐"
                    : tool.Icon,
                Foreground = BrushFrom("#93C5FD"),
                FontWeight = FontWeights.Bold,
                FontSize = 12,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };
        }

        return border;
    }

    private void ToolsList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_toolsList.SelectedItem is not ListBoxItem item ||
            item.Tag is not WebTool tool)
        {
            return;
        }

        _selectedTool = tool;

        _deleteButton.IsEnabled = true;
        _favoriteButton.IsEnabled = true;
        _removeIconButton.IsEnabled =
            !string.IsNullOrWhiteSpace(tool.CustomIconFileName);

        LoadEditor(tool);
    }

    private void LoadEditor(WebTool tool)
    {
        _selectedToolTitle.Text = tool.Name;

        _nameTextBox.Text = tool.Name;
        _categoryTextBox.Text = tool.Category;
        _descriptionTextBox.Text = tool.Description;
        _iconTextBox.Text = tool.Icon;
        _urlTextBox.Text = tool.Url;

        _profileComboBox.SelectedItem =
            _chromeProfiles.FirstOrDefault(profile =>
                string.Equals(
                    profile.DirectoryName,
                    tool.ChromeProfileDirectory,
                    StringComparison.OrdinalIgnoreCase));

        UpdateFavoriteButton();
        UpdateIconPreview(tool);

        _removeIconButton.IsEnabled =
            !string.IsNullOrWhiteSpace(tool.CustomIconFileName);

        _statusText.Text = string.IsNullOrWhiteSpace(tool.Url)
            ? "This tool needs a website address."
            : "Tool settings loaded.";
    }

    private void UpdateIconPreview(WebTool tool)
    {
        string? iconPath =
            _webToolService.GetCustomIconPath(tool.CustomIconFileName);

        BitmapImage? bitmap = iconPath == null
            ? null
            : LoadBitmap(iconPath, 128);

        if (bitmap != null)
        {
            _iconPreviewImage.Source = bitmap;
            _iconPreviewImage.Visibility = Visibility.Visible;
            _iconPreviewBorder.Child = _iconPreviewImage;
        }
        else
        {
            _iconPreviewImage.Source = null;
            _iconPreviewImage.Visibility = Visibility.Collapsed;

            _iconPreviewText.Text =
                string.IsNullOrWhiteSpace(tool.Icon)
                    ? "🌐"
                    : tool.Icon;

            _iconPreviewBorder.Child = _iconPreviewText;
        }
    }

    private static BitmapImage? LoadBitmap(
        string path,
        int decodePixelWidth)
    {
        try
        {
            var bitmap = new BitmapImage();

            bitmap.BeginInit();
            bitmap.CacheOption = BitmapCacheOption.OnLoad;
            bitmap.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
            bitmap.DecodePixelWidth = decodePixelWidth;
            bitmap.UriSource = new Uri(path, UriKind.Absolute);
            bitmap.EndInit();
            bitmap.Freeze();

            return bitmap;
        }
        catch (Exception)
        {
            return null;
        }
    }

    private void ClearEditor()
    {
        _nameTextBox.Clear();
        _categoryTextBox.Clear();
        _descriptionTextBox.Clear();
        _iconTextBox.Clear();
        _urlTextBox.Clear();
        _profileComboBox.SelectedItem = null;
        _statusText.Text = string.Empty;

        _iconPreviewImage.Source = null;
        _iconPreviewBorder.Child = _iconPreviewText;
        _iconPreviewText.Text = "🌐";
    }

    private void AddButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        var tool = new WebTool
        {
            Name = "New Tool",
            Category = "Other",
            Description = "Enter a description.",
            Icon = "🌐",
            Url = string.Empty,
            ChromeProfileDirectory =
                _chromeProfiles.FirstOrDefault()?.DirectoryName
                ?? string.Empty
        };

        _tools.Add(tool);

        _searchBox.Clear();
        _categoryFilter.SelectedIndex = 0;

        RefreshCategoryFilter();
        RefreshToolList();

        foreach (ListBoxItem item in _toolsList.Items)
        {
            if (ReferenceEquals(item.Tag, tool))
            {
                _toolsList.SelectedItem = item;
                break;
            }
        }

        _nameTextBox.Focus();
        _nameTextBox.SelectAll();

        _statusText.Text =
            "New tool created. Complete its details and save.";
    }

    private void DeleteButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedTool == null)
            return;

        MessageBoxResult result = MessageBox.Show(
            $"Are you sure you want to delete '{_selectedTool.Name}'?",
            "Delete Web Tool",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (result != MessageBoxResult.Yes)
            return;

        WebTool toolToDelete = _selectedTool;
        int originalIndex = _tools.IndexOf(toolToDelete);
        string oldIcon = toolToDelete.CustomIconFileName;

        _tools.Remove(toolToDelete);
        _selectedTool = null;

        try
        {
            _webToolService.SaveTools(_tools);
            _webToolService.DeleteCustomIcon(oldIcon);

            RefreshCategoryFilter();
            RefreshToolList();

            _statusText.Text = "Tool deleted successfully.";
        }
        catch (Exception ex)
        {
            _tools.Insert(
                Math.Max(0, originalIndex),
                toolToDelete);

            RefreshCategoryFilter();
            RefreshToolList();

            MessageBox.Show(
                ex.Message,
                "Unable to Delete Tool",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UploadIconButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedTool == null)
            return;

        var dialog = new OpenFileDialog
        {
            Title = "Select Web Tool Icon",
            Filter =
                "Image files (*.png;*.jpg;*.jpeg)|*.png;*.jpg;*.jpeg",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() != true)
            return;

        string oldIcon = _selectedTool.CustomIconFileName;
        string? newIcon = null;

        try
        {
            newIcon = _webToolService.SaveCustomIcon(dialog.FileName);

            _selectedTool.CustomIconFileName = newIcon;

            _webToolService.SaveTools(_tools);

            if (!string.IsNullOrWhiteSpace(oldIcon))
                _webToolService.DeleteCustomIcon(oldIcon);

            UpdateIconPreview(_selectedTool);
            _removeIconButton.IsEnabled = true;

            RefreshToolList();

            _statusText.Text = "Custom icon uploaded and saved.";
        }
        catch (Exception ex)
        {
            _selectedTool.CustomIconFileName = oldIcon;

            if (!string.IsNullOrWhiteSpace(newIcon))
                _webToolService.DeleteCustomIcon(newIcon);

            UpdateIconPreview(_selectedTool);

            MessageBox.Show(
                ex.Message,
                "Unable to Upload Icon",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void RemoveIconButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedTool == null ||
            string.IsNullOrWhiteSpace(
                _selectedTool.CustomIconFileName))
        {
            return;
        }

        string oldIcon = _selectedTool.CustomIconFileName;

        _selectedTool.CustomIconFileName = string.Empty;

        try
        {
            _webToolService.SaveTools(_tools);

            _webToolService.DeleteCustomIcon(oldIcon);

            UpdateIconPreview(_selectedTool);
            _removeIconButton.IsEnabled = false;

            RefreshToolList();

            _statusText.Text =
                "Custom icon removed. The text icon is now being used.";
        }
        catch (Exception ex)
        {
            _selectedTool.CustomIconFileName = oldIcon;

            UpdateIconPreview(_selectedTool);

            MessageBox.Show(
                ex.Message,
                "Unable to Remove Icon",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void FavoriteButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedTool == null)
            return;

        _selectedTool.IsFavorite = !_selectedTool.IsFavorite;

        try
        {
            _webToolService.SaveTools(_tools);

            UpdateFavoriteButton();
            RefreshToolList();

            _statusText.Text = _selectedTool.IsFavorite
                ? "Added to favourites."
                : "Removed from favourites.";
        }
        catch (Exception ex)
        {
            _selectedTool.IsFavorite = !_selectedTool.IsFavorite;
            UpdateFavoriteButton();

            MessageBox.Show(
                ex.Message,
                "Unable to Update Favourite",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void UpdateFavoriteButton()
    {
        if (_selectedTool == null)
            return;

        _favoriteButton.Content = _selectedTool.IsFavorite
            ? "★ Remove Favourite"
            : "☆ Add to Favourites";

        ApplyButtonStyle(
            _favoriteButton,
            _selectedTool.IsFavorite
                ? "WarningButton"
                : "SecondaryButton");
    }

    private void SaveButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (!SaveCurrentTool())
            return;

        RefreshCategoryFilter();
        RefreshToolList();

        _statusText.Text = "Tool settings saved successfully.";
    }

    private bool SaveCurrentTool()
    {
        if (_selectedTool == null)
            return false;

        string name = _nameTextBox.Text.Trim();
        string category = _categoryTextBox.Text.Trim();
        string url = _urlTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name))
        {
            MessageBox.Show(
                "Enter a name for this tool.",
                "Name Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            _nameTextBox.Focus();
            return false;
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            MessageBox.Show(
                "Enter a category for this tool.",
                "Category Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            _categoryTextBox.Focus();
            return false;
        }

        if (!string.IsNullOrWhiteSpace(url))
        {
            if (!Uri.TryCreate(
                    url,
                    UriKind.Absolute,
                    out Uri? uri) ||
                (uri.Scheme != Uri.UriSchemeHttp &&
                 uri.Scheme != Uri.UriSchemeHttps))
            {
                MessageBox.Show(
                    "Enter a valid website address beginning with https:// or http://.",
                    "Invalid Website Address",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                _urlTextBox.Focus();
                return false;
            }
        }

        _selectedTool.Name = name;
        _selectedTool.Category = category;
        _selectedTool.Description = _descriptionTextBox.Text.Trim();

        _selectedTool.Icon =
            string.IsNullOrWhiteSpace(_iconTextBox.Text)
                ? "🌐"
                : _iconTextBox.Text.Trim();

        _selectedTool.Url = url;

        _selectedTool.ChromeProfileDirectory =
            (_profileComboBox.SelectedItem as ChromeProfile)
            ?.DirectoryName ?? string.Empty;

        try
        {
            _webToolService.SaveTools(_tools);
            _selectedToolTitle.Text = name;

            UpdateIconPreview(_selectedTool);

            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Unable to Save Tool",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return false;
        }
    }

    private void OpenButton_Click(
        object sender,
        RoutedEventArgs e)
    {
        if (_selectedTool == null)
            return;

        if (!SaveCurrentTool())
            return;

        LaunchTool(_selectedTool);
    }

    private void LaunchTool(WebTool tool)
    {
        if (string.IsNullOrWhiteSpace(tool.Url))
        {
            MessageBox.Show(
                $"Enter a website address for {tool.Name} before opening it.",
                "Website Address Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        ChromeProfile? profile =
            _chromeProfiles.FirstOrDefault(item =>
                string.Equals(
                    item.DirectoryName,
                    tool.ChromeProfileDirectory,
                    StringComparison.OrdinalIgnoreCase));

        profile ??= _chromeProfiles.FirstOrDefault();

        if (profile == null)
        {
            MessageBox.Show(
                "No Chrome profiles were detected. Open Chrome at least once and restart Nexus IT.",
                "Chrome Profile Required",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        try
        {
            _chromeProfileService.OpenWebsite(
                tool.Url,
                profile.DirectoryName);

            tool.ChromeProfileDirectory = profile.DirectoryName;
            tool.LastLaunchedUtc = DateTime.UtcNow;

            _webToolService.SaveTools(_tools);

            _statusText.Text = $"Opening {tool.Name} in Chrome.";

            RefreshToolList();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                ex.Message,
                "Unable to Open Website",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private static TextBox CreateTextBox()
    {
        return new TextBox
        {
            Height = 38,
            FontSize = 13,
            Padding = new Thickness(10, 7, 10, 7),
            Background = InputBrush,
            Foreground = MainTextBrush,
            BorderBrush = BrushFrom("#374151"),
            CaretBrush = MainTextBrush,
            Margin = new Thickness(0, 7, 0, 17),
            VerticalContentAlignment = VerticalAlignment.Center
        };
    }

    private static TextBlock CreateLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = MutedTextBrush
        };
    }

    private static Button CreateButton(
        string text,
        string styleKey)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 9, 14, 9),
            FontSize = 12,
            FontWeight = FontWeights.SemiBold,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        ApplyButtonStyle(button, styleKey);

        return button;
    }

    private static void ApplyButtonStyle(
        Button button,
        string styleKey)
    {
        if (Application.Current.MainWindow?
                .TryFindResource(styleKey) is Style style)
        {
            button.Style = style;
        }
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        return new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color)!);
    }
}
