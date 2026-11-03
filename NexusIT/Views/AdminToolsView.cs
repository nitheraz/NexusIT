using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NexusIT.App.Views;

public class AdminToolsView : UserControl
{
    private readonly WrapPanel _builtInPanel = new();
    private readonly WrapPanel _customPanel = new();
    private readonly TextBlock _customCount = new();

    private readonly AdminToolStorageService _storage = new();
    private List<CustomAdminTool> _customTools = [];

    private static readonly Brush CardBackground = BrushFrom("#1F2937");
    private static readonly Brush CardBorder = BrushFrom("#374151");
    private static readonly Brush PrimaryText = BrushFrom("#F9FAFB");
    private static readonly Brush SecondaryText = BrushFrom("#9CA3AF");
    private static readonly Brush Accent = BrushFrom("#3B82F6");

    private readonly List<BuiltInAdminTool> _builtInTools = [];

    public AdminToolsView()
    {
        _builtInTools =
        [
            new BuiltInAdminTool(
                "PuTTY",
                "SSH and serial terminal client",
                "⌨",
                FindPutty),

            new BuiltInAdminTool(
                "Active Directory",
                "Active Directory Users and Computers",
                "♙",
                () => FindMsc("dsa.msc")),

            new BuiltInAdminTool(
                "MECM Console",
                "Microsoft Endpoint Configuration Manager",
                "▤",
                FindMecmConsole),

            new BuiltInAdminTool(
                "Group Policy",
                "Group Policy Management Console",
                "⚙",
                () => FindMsc("gpmc.msc")),

            new BuiltInAdminTool(
                "Task Manager",
                "View processes and system performance",
                "▥",
                () => "taskmgr.exe")
        ];

        BuildInterface();
        LoadCustomTools();
        RefreshBuiltInTools();
    }

    private void BuildInterface()
    {
        var root = new StackPanel();

        root.Children.Add(new TextBlock
        {
            Text = "Launch your everyday Windows administration tools.",
            Foreground = SecondaryText,
            FontSize = 13,
            Margin = new Thickness(0, 0, 0, 24)
        });

        root.Children.Add(CreateSectionHeader(
            "Built-in tools",
            "Applications and Windows management consoles"));

        root.Children.Add(new Border
        {
            Background = BrushFrom("#172033"),
            BorderBrush = BrushFrom("#29364B"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(14),
            Margin = new Thickness(0, 0, 0, 18),
            Child = new TextBlock
            {
                Text = "Some tools require Windows administrative permissions or " +
                       "additional software installed on this computer.",
                Foreground = BrushFrom("#CBD5E1"),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap
            }
        });

        _builtInPanel.Margin = new Thickness(0, 0, 0, 24);
        root.Children.Add(_builtInPanel);

        var customHeader = new Grid
        {
            Margin = new Thickness(0, 0, 0, 12)
        };

        customHeader.ColumnDefinitions.Add(
            new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        customHeader.ColumnDefinitions.Add(
            new ColumnDefinition { Width = GridLength.Auto });

        var customTitle = CreateSectionHeader(
            "My custom tools",
            "Add your own applications and utilities");

        customHeader.Children.Add(customTitle);

        var addButton = CreateButton("+  Add tool", Accent);
        addButton.Padding = new Thickness(16, 9, 16, 9);
        addButton.VerticalAlignment = VerticalAlignment.Top;
        addButton.Click += (_, _) => AddCustomTool();

        Grid.SetColumn(addButton, 1);
        customHeader.Children.Add(addButton);

        root.Children.Add(customHeader);

        _customCount.Text = "0 custom tools";
        _customCount.Foreground = SecondaryText;
        _customCount.FontSize = 12;
        _customCount.Margin = new Thickness(0, 0, 0, 12);

        root.Children.Add(_customCount);

        _customPanel.Margin = new Thickness(0, 0, 0, 20);
        root.Children.Add(_customPanel);

        Content = root;
    }

    private static StackPanel CreateSectionHeader(
        string title,
        string description)
    {
        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 18,
            FontWeight = FontWeights.SemiBold,
            Foreground = PrimaryText
        });

        panel.Children.Add(new TextBlock
        {
            Text = description,
            FontSize = 12,
            Foreground = SecondaryText,
            Margin = new Thickness(0, 4, 0, 12)
        });

        return panel;
    }

    private void RefreshBuiltInTools()
    {
        _builtInPanel.Children.Clear();

        foreach (BuiltInAdminTool tool in _builtInTools)
        {
            string? executable = tool.FindExecutable();

            _builtInPanel.Children.Add(
                CreateToolCard(
                    tool.Name,
                    tool.Description,
                    tool.Icon,
                    executable != null,
                    executable == null ? "Not installed or not found" : "Available",
                    () => LaunchBuiltIn(tool),
                    showElevationToggle: true,
                    elevationChanged: value => tool.RunAsAdministrator = value));
        }
    }

    private void LaunchBuiltIn(BuiltInAdminTool tool)
    {
        try
        {
            string? executable = tool.FindExecutable();

            if (string.IsNullOrWhiteSpace(executable))
            {
                MessageBox.Show(
                    $"Unable to find {tool.Name} on this computer.\n\n" +
                    "Check that the application or Windows management component " +
                    "is installed.",
                    "Tool unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            StartProcess(
                executable,
                runAsAdministrator: tool.RunAsAdministrator);
        }
        catch (Exception ex)
        {
            ShowLaunchError(tool.Name, ex);
        }
    }

    private void LoadCustomTools()
    {
        try
        {
            _customTools = _storage.Load();
        }
        catch (Exception ex)
        {
            _customTools = [];

            MessageBox.Show(
                $"Unable to load custom tools.\n\n{ex.Message}",
                "Nexus IT",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        RefreshCustomTools();
    }

    private void RefreshCustomTools()
    {
        _customPanel.Children.Clear();

        _customCount.Text =
            $"{_customTools.Count} custom tool{(_customTools.Count == 1 ? "" : "s")}";

        if (_customTools.Count == 0)
        {
            _customPanel.Children.Add(new Border
            {
                Background = CardBackground,
                BorderBrush = CardBorder,
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Child = new TextBlock
                {
                    Text = "No custom tools added yet. Select Add tool to register an application.",
                    Foreground = SecondaryText,
                    TextWrapping = TextWrapping.Wrap
                }
            });

            return;
        }

        foreach (CustomAdminTool tool in _customTools)
        {
            _customPanel.Children.Add(
                CreateToolCard(
                    tool.Name,
                    tool.Description,
                    "◆",
                    File.Exists(tool.ExecutablePath),
                    File.Exists(tool.ExecutablePath) ? "Available" : "File not found",
                    () => LaunchCustomTool(tool),
                    () => EditCustomTool(tool),
                    () => DeleteCustomTool(tool)));
        }
    }

    private Border CreateToolCard(
        string name,
        string description,
        string icon,
        bool available,
        string status,
        Action launch,
        Action? edit = null,
        Action? delete = null,
        bool showElevationToggle = false,
        Action<bool>? elevationChanged = null)
    {
        var content = new StackPanel();

        var heading = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Margin = new Thickness(0, 0, 0, 10)
        };

        heading.Children.Add(new TextBlock
        {
            Text = icon,
            FontSize = 23,
            Foreground = BrushFrom("#93C5FD"),
            Width = 38,
            VerticalAlignment = VerticalAlignment.Center
        });

        heading.Children.Add(new TextBlock
        {
            Text = name,
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = PrimaryText,
            VerticalAlignment = VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        });

        content.Children.Add(heading);

        content.Children.Add(new TextBlock
        {
            Text = description,
            Foreground = SecondaryText,
            FontSize = 12,
            TextWrapping = TextWrapping.Wrap,
            MinHeight = 34,
            Margin = new Thickness(0, 0, 0, 10)
        });

        content.Children.Add(new TextBlock
        {
            Text = "●  " + status,
            Foreground = available
                ? BrushFrom("#86EFAC")
                : BrushFrom("#FCA5A5"),
            FontSize = 11,
            Margin = new Thickness(0, 0, 0, 12)
        });

        if (showElevationToggle)
        {
            var elevationCheckBox = new CheckBox
            {
                Content = "Run as administrator",
                IsChecked = false,
                Foreground = SecondaryText,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 12),
                VerticalContentAlignment = VerticalAlignment.Center
            };

            elevationCheckBox.Checked += (_, _) => elevationChanged?.Invoke(true);
            elevationCheckBox.Unchecked += (_, _) => elevationChanged?.Invoke(false);

            content.Children.Add(elevationCheckBox);
        }

        var actions = new WrapPanel();

        var launchButton = CreateButton(
            "Launch",
            available ? Accent : BrushFrom("#374151"));

        launchButton.IsEnabled = available;
        launchButton.Click += (_, _) => launch();
        actions.Children.Add(launchButton);

        if (edit != null)
        {
            var editButton = CreateButton(
                "Edit",
                BrushFrom("#374151"));

            editButton.Margin = new Thickness(6, 0, 0, 0);
            editButton.Click += (_, _) => edit();
            actions.Children.Add(editButton);
        }

        if (delete != null)
        {
            var deleteButton = CreateButton(
                "Delete",
                BrushFrom("#7F1D1D"));

            deleteButton.Margin = new Thickness(6, 0, 0, 0);
            deleteButton.Click += (_, _) => delete();
            actions.Children.Add(deleteButton);
        }

        content.Children.Add(actions);

        return new Border
        {
            Width = 260,
            MinHeight = 185,
            Background = CardBackground,
            BorderBrush = CardBorder,
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(16),
            Margin = new Thickness(0, 0, 14, 14),
            Child = content
        };
    }

    private static Button CreateButton(string text, Brush background)
    {
        return new Button
        {
            Content = text,
            Background = background,
            Foreground = PrimaryText,
            BorderBrush = BrushFrom("#4B5563"),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(12, 6, 12, 6),
            Cursor = System.Windows.Input.Cursors.Hand
        };
    }

    private void AddCustomTool()
    {
        var dialog = new CustomToolDialog
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
            return;

        _customTools.Add(dialog.Tool);
        SaveCustomTools();
    }

    private void EditCustomTool(CustomAdminTool existing)
    {
        var dialog = new CustomToolDialog(existing)
        {
            Owner = Window.GetWindow(this)
        };

        if (dialog.ShowDialog() != true)
            return;

        existing.Name = dialog.Tool.Name;
        existing.Description = dialog.Tool.Description;
        existing.ExecutablePath = dialog.Tool.ExecutablePath;
        existing.Arguments = dialog.Tool.Arguments;
        existing.WorkingDirectory = dialog.Tool.WorkingDirectory;
        existing.RunAsAdministrator = dialog.Tool.RunAsAdministrator;

        SaveCustomTools();
    }

    private void DeleteCustomTool(CustomAdminTool tool)
    {
        MessageBoxResult result = MessageBox.Show(
            $"Remove '{tool.Name}' from your custom tools?",
            "Remove custom tool",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        _customTools.Remove(tool);
        SaveCustomTools();
    }

    private void SaveCustomTools()
    {
        try
        {
            _storage.Save(_customTools);
            RefreshCustomTools();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to save custom tools.\n\n{ex.Message}",
                "Nexus IT",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void LaunchCustomTool(CustomAdminTool tool)
    {
        try
        {
            if (!File.Exists(tool.ExecutablePath))
            {
                MessageBox.Show(
                    $"The executable could not be found:\n{tool.ExecutablePath}",
                    "Tool unavailable",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);

                return;
            }

            StartProcess(
                tool.ExecutablePath,
                tool.Arguments,
                tool.WorkingDirectory,
                tool.RunAsAdministrator);
        }
        catch (Exception ex)
        {
            ShowLaunchError(tool.Name, ex);
        }
    }

    private static void StartProcess(
        string executable,
        string? arguments = null,
        string? workingDirectory = null,
        bool runAsAdministrator = false)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = executable,
            UseShellExecute = true,
            Verb = runAsAdministrator ? "runas" : string.Empty
        };

        if (!string.IsNullOrWhiteSpace(arguments))
            startInfo.Arguments = arguments;

        if (!string.IsNullOrWhiteSpace(workingDirectory) &&
            Directory.Exists(workingDirectory))
        {
            startInfo.WorkingDirectory = workingDirectory;
        }

        Process.Start(startInfo);
    }

    private static void ShowLaunchError(string name, Exception ex)
    {
        MessageBox.Show(
            $"Unable to launch {name}.\n\n{ex.Message}",
            "Launch error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static string? FindMsc(string fileName)
    {
        string windowsDirectory =
            Environment.GetFolderPath(Environment.SpecialFolder.Windows);

        string path = Path.Combine(windowsDirectory, "System32", fileName);

        return File.Exists(path) ? path : null;
    }

    private static string? FindPutty()
    {
        string? pathResult = FindOnPath("putty.exe");

        if (pathResult != null)
            return pathResult;

        string[] candidates =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "PuTTY",
                "putty.exe"),

            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "PuTTY",
                "putty.exe")
        ];

        return candidates.FirstOrDefault(File.Exists);
    }

    private static string? FindMecmConsole()
    {
        string[] candidates =
        [
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "ConfigMgr Console",
                "bin",
                "Microsoft.ConfigurationManagement.exe"),

            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Configuration Manager",
                "AdminConsole",
                "bin",
                "Microsoft.ConfigurationManagement.exe"),

            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Endpoint Manager",
                "AdminConsole",
                "bin",
                "Microsoft.ConfigurationManagement.exe"),

            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "Microsoft Configuration Manager",
                "AdminConsole",
                "bin",
                "Microsoft.ConfigurationManagement.exe")
        ];

        return candidates.FirstOrDefault(File.Exists)
               ?? FindOnPath("Microsoft.ConfigurationManagement.exe");
    }

    private static string? FindOnPath(string executable)
    {
        string? pathVariable = Environment.GetEnvironmentVariable("PATH");

        if (string.IsNullOrWhiteSpace(pathVariable))
            return null;

        foreach (string directory in pathVariable.Split(Path.PathSeparator))
        {
            if (string.IsNullOrWhiteSpace(directory))
                continue;

            string candidate = Path.Combine(directory.Trim(), executable);

            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color)!);

        brush.Freeze();
        return brush;
    }
}

public class BuiltInAdminTool(
    string name,
    string description,
    string icon,
    Func<string?> findExecutable)
{
    public string Name { get; } = name;
    public string Description { get; } = description;
    public string Icon { get; } = icon;
    public Func<string?> FindExecutable { get; } = findExecutable;
    public bool RunAsAdministrator { get; set; }
}

public class CustomAdminTool
{
    public string Name { get; set; } = "";
    public string Description { get; set; } = "";
    public string ExecutablePath { get; set; } = "";
    public string Arguments { get; set; } = "";
    public string WorkingDirectory { get; set; } = "";
    public bool RunAsAdministrator { get; set; }
}

public class AdminToolStorageService
{
    private readonly string _filePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "NexusIT",
        "admin-tools.json");

    public List<CustomAdminTool> Load()
    {
        if (!File.Exists(_filePath))
            return [];

        string json = File.ReadAllText(_filePath);

        return JsonSerializer.Deserialize<List<CustomAdminTool>>(json) ?? [];
    }

    public void Save(List<CustomAdminTool> tools)
    {
        string? directory = Path.GetDirectoryName(_filePath);

        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var options = new JsonSerializerOptions
        {
            WriteIndented = true
        };

        string json = JsonSerializer.Serialize(tools, options);
        File.WriteAllText(_filePath, json);
    }
}

public class CustomToolDialog : Window
{
    private readonly TextBox _name = new();
    private readonly TextBox _description = new();
    private readonly TextBox _path = new();
    private readonly TextBox _arguments = new();
    private readonly TextBox _workingDirectory = new();
    private readonly CheckBox _runAsAdmin = new();

    public CustomAdminTool Tool { get; private set; } = new();

    public CustomToolDialog(CustomAdminTool? existing = null)
    {
        Title = existing == null ? "Add custom tool" : "Edit custom tool";
        Width = 540;
        Height = 570;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        ResizeMode = ResizeMode.NoResize;
        Background = BrushFrom("#111827");
        Foreground = BrushFrom("#F9FAFB");

        if (existing != null)
        {
            _name.Text = existing.Name;
            _description.Text = existing.Description;
            _path.Text = existing.ExecutablePath;
            _arguments.Text = existing.Arguments;
            _workingDirectory.Text = existing.WorkingDirectory;
            _runAsAdmin.IsChecked = existing.RunAsAdministrator;
        }

        BuildDialog();
    }

    private void BuildDialog()
    {
        var root = new StackPanel
        {
            Margin = new Thickness(24)
        };

        root.Children.Add(new TextBlock
        {
            Text = "Application details",
            FontSize = 20,
            FontWeight = FontWeights.SemiBold,
            Foreground = BrushFrom("#F9FAFB"),
            Margin = new Thickness(0, 0, 0, 18)
        });

        AddField(root, "Display name", _name);
        AddField(root, "Description (optional)", _description);

        var pathPanel = new StackPanel();

        pathPanel.Children.Add(CreateLabel("Executable path"));

        var pathRow = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, 0, 0, 12)
        };

        var browse = new Button
        {
            Content = "Browse...",
            Padding = new Thickness(12, 7, 12, 7),
            Margin = new Thickness(8, 0, 0, 0),
            Background = BrushFrom("#374151"),
            Foreground = BrushFrom("#F9FAFB")
        };

        browse.Click += (_, _) => BrowseExecutable();

        DockPanel.SetDock(browse, Dock.Right);
        pathRow.Children.Add(browse);
        pathRow.Children.Add(_path);

        pathPanel.Children.Add(pathRow);
        root.Children.Add(pathPanel);

        AddField(root, "Command-line arguments (optional)", _arguments);
        AddField(root, "Working directory (optional)", _workingDirectory);

        _runAsAdmin.Content = "Run this application as administrator";
        _runAsAdmin.Foreground = BrushFrom("#F9FAFB");
        _runAsAdmin.Margin = new Thickness(0, 4, 0, 18);
        root.Children.Add(_runAsAdmin);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right
        };

        var cancel = new Button
        {
            Content = "Cancel",
            Padding = new Thickness(16, 8, 16, 8),
            Background = BrushFrom("#374151"),
            Foreground = BrushFrom("#F9FAFB"),
            Margin = new Thickness(0, 0, 8, 0),
            IsCancel = true
        };

        var save = new Button
        {
            Content = "Save tool",
            Padding = new Thickness(16, 8, 16, 8),
            Background = BrushFrom("#3B82F6"),
            Foreground = BrushFrom("#FFFFFF"),
            IsDefault = true
        };

        save.Click += (_, _) => SaveTool();

        buttons.Children.Add(cancel);
        buttons.Children.Add(save);
        root.Children.Add(buttons);

        Content = root;
    }

    private static void AddField(
        Panel parent,
        string label,
        TextBox textBox)
    {
        parent.Children.Add(CreateLabel(label));

        textBox.Height = 34;
        textBox.Padding = new Thickness(8, 6, 8, 6);
        textBox.Background = BrushFrom("#1F2937");
        textBox.Foreground = BrushFrom("#F9FAFB");
        textBox.BorderBrush = BrushFrom("#374151");
        textBox.Margin = new Thickness(0, 0, 0, 12);

        parent.Children.Add(textBox);
    }

    private static TextBlock CreateLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            Foreground = BrushFrom("#D1D5DB"),
            FontSize = 12,
            Margin = new Thickness(0, 0, 0, 5)
        };
    }

    private void BrowseExecutable()
    {
        var dialog = new OpenFileDialog
        {
            Title = "Select application",
            Filter = "Applications (*.exe)|*.exe|All files (*.*)|*.*",
            CheckFileExists = true
        };

        if (dialog.ShowDialog(this) == true)
        {
            _path.Text = dialog.FileName;

            if (string.IsNullOrWhiteSpace(_workingDirectory.Text))
                _workingDirectory.Text =
                    Path.GetDirectoryName(dialog.FileName) ?? "";
        }
    }

    private void SaveTool()
    {
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            MessageBox.Show(
                "Enter a display name.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (!File.Exists(_path.Text.Trim()))
        {
            MessageBox.Show(
                "Select a valid executable file.",
                "Validation",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        Tool = new CustomAdminTool
        {
            Name = _name.Text.Trim(),
            Description = _description.Text.Trim(),
            ExecutablePath = _path.Text.Trim(),
            Arguments = _arguments.Text.Trim(),
            WorkingDirectory = _workingDirectory.Text.Trim(),
            RunAsAdministrator = _runAsAdmin.IsChecked == true
        };

        DialogResult = true;
    }

    private static SolidColorBrush BrushFrom(string color)
    {
        var brush = new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color)!);

        brush.Freeze();
        return brush;
    }
}
