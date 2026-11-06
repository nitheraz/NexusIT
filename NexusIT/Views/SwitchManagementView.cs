using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Renci.SshNet;
using Renci.SshNet.Common;
using System.Diagnostics;
using System.Text;
using System.Threading;

namespace NexusIT.App.Views;

public sealed class SwitchManagementView : UserControl
{
    private readonly string _dataFolder;
    private readonly string _profilesPath;

    private readonly List<SwitchProfile> _profiles = new();

    private readonly ComboBox _profileCombo = new();
    private readonly TextBox _nameTextBox = new();
    private readonly TextBox _ipTextBox = new();
    private readonly TextBox _usernameTextBox = new();
    private readonly PasswordBox _passwordBox = new();
    private readonly CheckBox _savePasswordCheckBox = new();

    private readonly TextBlock _connectionStatus = new();
    private readonly TextBox _outputTextBox = new();

    private readonly Button _connectButton;
    private readonly Button _disconnectButton;

    private SshClient? _sshClient;
    private ShellStream? _shellStream;

    private readonly SemaphoreSlim _commandSemaphore = new(1, 1);

    private bool _isConnecting;

    private const int SshPort = 22;

    public SwitchManagementView()
    {
        _dataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "NexusIT",
            "SwitchDiagnostics");

        Directory.CreateDirectory(_dataFolder);

        _profilesPath = Path.Combine(_dataFolder, "profiles.json");

        _connectButton = CreateButton("Connect", "PrimaryButton");
        _disconnectButton = CreateButton("Disconnect", "DangerButton");

        Content = BuildInterface();

        LoadProfiles();

        _profileCombo.SelectionChanged += ProfileCombo_SelectionChanged;
        _connectButton.Click += ConnectButton_Click;
        _disconnectButton.Click += DisconnectButton_Click;

        _disconnectButton.IsEnabled = false;

        Unloaded += SwitchManagementView_Unloaded;
    }

    private UIElement BuildInterface()
    {
        var root = new Grid
        {
            Margin = new Thickness(4)
        };

        root.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        root.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });

        var topPanel = new StackPanel
        {
            Orientation = Orientation.Vertical
        };

        topPanel.Children.Add(CreateHeading(
            "Switch Diagnostics",
            "Connect to HP ProCurve / ArubaOS-Switch devices and inspect network ports."));

        var connectionCard = CreateCard();

        connectionCard.Child = BuildConnectionPanel();

        topPanel.Children.Add(connectionCard);

        Grid.SetRow(topPanel, 0);
        root.Children.Add(topPanel);

        var diagnosticsCard = CreateCard();

        diagnosticsCard.Margin = new Thickness(0, 14, 0, 0);
        diagnosticsCard.Child = BuildDiagnosticsPanel();

        Grid.SetRow(diagnosticsCard, 1);
        root.Children.Add(diagnosticsCard);

        return root;
    }

    private UIElement BuildConnectionPanel()
    {
        var panel = new StackPanel();

        panel.Children.Add(CreateSectionTitle("Switch Connection"));

        var profileGrid = new Grid
        {
            Margin = new Thickness(0, 8, 0, 12)
        };

        profileGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        profileGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = GridLength.Auto
        });

        _profileCombo.MinWidth = 220;
        _profileCombo.Height = 36;
        _profileCombo.Margin = new Thickness(0, 0, 10, 0);
        _profileCombo.DisplayMemberPath = nameof(SwitchProfile.Name);

        ApplyComboBoxStyle(_profileCombo);

        Grid.SetColumn(_profileCombo, 0);
        profileGrid.Children.Add(_profileCombo);

        var profileButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        var saveProfileButton = CreateButton("Save Profile", "SecondaryButton");
        saveProfileButton.Click += SaveProfileButton_Click;

        var deleteProfileButton = CreateButton("Delete", "DangerButton");
        deleteProfileButton.Margin = new Thickness(8, 0, 0, 0);
        deleteProfileButton.Click += DeleteProfileButton_Click;

        profileButtons.Children.Add(saveProfileButton);
        profileButtons.Children.Add(deleteProfileButton);

        Grid.SetColumn(profileButtons, 1);
        profileGrid.Children.Add(profileButtons);

        panel.Children.Add(profileGrid);

        var fieldsGrid = new Grid();

        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        fieldsGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        AddField(fieldsGrid, "Profile Name", _nameTextBox, 0);
        AddField(fieldsGrid, "Switch IP Address", _ipTextBox, 1);
        AddField(fieldsGrid, "Username", _usernameTextBox, 2);

        panel.Children.Add(fieldsGrid);

        var credentialsGrid = new Grid
        {
            Margin = new Thickness(0, 12, 0, 0)
        };

        credentialsGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        credentialsGrid.ColumnDefinitions.Add(new ColumnDefinition
        {
            Width = new GridLength(1, GridUnitType.Star)
        });

        var passwordPanel = new StackPanel
        {
            Margin = new Thickness(0, 0, 10, 0)
        };

        passwordPanel.Children.Add(CreateLabel("Password"));

        _passwordBox.Height = 36;
        _passwordBox.Padding = new Thickness(10, 6, 10, 6);
        _passwordBox.Background = Brush("#111827");
        _passwordBox.Foreground = Brush("#F9FAFB");
        _passwordBox.BorderBrush = Brush("#374151");
        _passwordBox.BorderThickness = new Thickness(1);

        passwordPanel.Children.Add(_passwordBox);

        Grid.SetColumn(passwordPanel, 0);
        credentialsGrid.Children.Add(passwordPanel);

        var optionsPanel = new StackPanel
        {
            VerticalAlignment = VerticalAlignment.Bottom,
            Margin = new Thickness(10, 0, 0, 0)
        };

        _savePasswordCheckBox.Content =
            "Save password in Windows Credential Manager";

        _savePasswordCheckBox.Foreground = Brush("#D1D5DB");
        _savePasswordCheckBox.Margin = new Thickness(0, 0, 0, 12);

        optionsPanel.Children.Add(_savePasswordCheckBox);

        var connectionButtons = new StackPanel
        {
            Orientation = Orientation.Horizontal
        };

        _connectButton.Margin = new Thickness(0, 0, 8, 0);

        _disconnectButton.Margin = new Thickness(0);

        connectionButtons.Children.Add(_connectButton);
        connectionButtons.Children.Add(_disconnectButton);

        optionsPanel.Children.Add(connectionButtons);

        Grid.SetColumn(optionsPanel, 1);
        credentialsGrid.Children.Add(optionsPanel);

        panel.Children.Add(credentialsGrid);

        _connectionStatus.Text = "Not connected";
        _connectionStatus.Foreground = Brush("#9CA3AF");
        _connectionStatus.Margin = new Thickness(0, 14, 0, 0);
        _connectionStatus.FontSize = 13;

        panel.Children.Add(_connectionStatus);

        return panel;
    }

    private UIElement BuildDiagnosticsPanel()
    {
        var panel = new Grid();

        panel.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        panel.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        panel.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });

        panel.Children.Add(CreateSectionTitle("Diagnostics"));

        var commandPanel = new WrapPanel
        {
            Margin = new Thickness(0, 12, 0, 12)
        };

        AddCommandButton(commandPanel, "System Information", "show system");
        AddCommandButton(commandPanel, "Port Status", "show interfaces brief");
        AddCommandButton(commandPanel, "MAC Address Table", "show mac-address");
        AddCommandButton(commandPanel, "VLANs", "show vlan");
        AddCommandButton(commandPanel, "LLDP Neighbours", "show lldp info remote-device");

        Grid.SetRow(commandPanel, 1);
        panel.Children.Add(commandPanel);

        var outputPanel = new Grid();

        outputPanel.RowDefinitions.Add(new RowDefinition
        {
            Height = GridLength.Auto
        });

        outputPanel.RowDefinitions.Add(new RowDefinition
        {
            Height = new GridLength(1, GridUnitType.Star)
        });

        var outputHeader = new DockPanel
        {
            LastChildFill = false,
            Margin = new Thickness(0, 0, 0, 8)
        };

        var outputTitle = CreateLabel("Command Output");
        outputTitle.FontSize = 13;

        outputHeader.Children.Add(outputTitle);

        var clearButton = CreateButton("Clear Output", "SecondaryButton");
        clearButton.Click += (_, _) => _outputTextBox.Clear();

        DockPanel.SetDock(clearButton, Dock.Right);
        outputHeader.Children.Add(clearButton);

        outputPanel.Children.Add(outputHeader);

        _outputTextBox.IsReadOnly = true;
        _outputTextBox.AcceptsReturn = true;
        _outputTextBox.AcceptsTab = true;
        _outputTextBox.TextWrapping = TextWrapping.NoWrap;
        _outputTextBox.VerticalScrollBarVisibility = ScrollBarVisibility.Auto;
        _outputTextBox.HorizontalScrollBarVisibility = ScrollBarVisibility.Auto;
        _outputTextBox.FontFamily = new FontFamily("Consolas");
        _outputTextBox.FontSize = 13;
        _outputTextBox.Padding = new Thickness(12);
        _outputTextBox.Background = Brush("#0B1220");
        _outputTextBox.Foreground = Brush("#D1D5DB");
        _outputTextBox.BorderBrush = Brush("#374151");
        _outputTextBox.BorderThickness = new Thickness(1);

        Grid.SetRow(_outputTextBox, 1);
        outputPanel.Children.Add(_outputTextBox);

        Grid.SetRow(outputPanel, 2);
        panel.Children.Add(outputPanel);

        return panel;
    }

    private void AddCommandButton(
        Panel panel,
        string label,
        string command)
    {
        var button = CreateButton(label, "SecondaryButton");

        button.Margin = new Thickness(0, 0, 8, 8);
        button.Click += async (_, _) => await RunDiagnosticCommandAsync(command);

        panel.Children.Add(button);
    }

    private void AddField(
        Grid grid,
        string label,
        TextBox textBox,
        int column)
    {
        var fieldPanel = new StackPanel
        {
            Margin = new Thickness(0, 0, 10, 0)
        };

        fieldPanel.Children.Add(CreateLabel(label));

        textBox.Height = 36;
        textBox.Padding = new Thickness(10, 6, 10, 6);
        textBox.Background = Brush("#111827");
        textBox.Foreground = Brush("#F9FAFB");
        textBox.BorderBrush = Brush("#374151");
        textBox.BorderThickness = new Thickness(1);
        textBox.VerticalContentAlignment = VerticalAlignment.Center;

        fieldPanel.Children.Add(textBox);

        Grid.SetColumn(fieldPanel, column);
        grid.Children.Add(fieldPanel);
    }

    private Border CreateCard()
    {
        return new Border
        {
            Background = Brush("#1F2937"),
            BorderBrush = Brush("#374151"),
            BorderThickness = new Thickness(1),
            CornerRadius = new CornerRadius(10),
            Padding = new Thickness(18),
            Margin = new Thickness(0, 12, 0, 0)
        };
    }

    private UIElement CreateHeading(string title, string subtitle)
    {
        var panel = new StackPanel();

        panel.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 25,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F9FAFB")
        });

        panel.Children.Add(new TextBlock
        {
            Text = subtitle,
            FontSize = 13,
            Foreground = Brush("#9CA3AF"),
            Margin = new Thickness(0, 5, 0, 0)
        });

        return panel;
    }

    private TextBlock CreateSectionTitle(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = Brush("#F9FAFB")
        };
    }

    private TextBlock CreateLabel(string text)
    {
        return new TextBlock
        {
            Text = text,
            FontSize = 12,
            Foreground = Brush("#9CA3AF"),
            Margin = new Thickness(0, 0, 0, 6)
        };
    }

    private Button CreateButton(string text, string styleKey)
    {
        var button = new Button
        {
            Content = text,
            Padding = new Thickness(14, 8, 14, 8),
            MinHeight = 36,
            Cursor = System.Windows.Input.Cursors.Hand
        };

        Style? style =
            Application.Current.MainWindow?.TryFindResource(styleKey) as Style;

        if (style != null)
            button.Style = style;

        return button;
    }

    private void ApplyComboBoxStyle(ComboBox comboBox)
    {
        Style? style =
            Application.Current.MainWindow?.TryFindResource("DarkComboBox") as Style;

        if (style != null)
        {
            comboBox.Style = style;
        }
        else
        {
            comboBox.Background = Brush("#111827");
            comboBox.Foreground = Brush("#F9FAFB");
        }
    }

    private static SolidColorBrush Brush(string color)
    {
        return new SolidColorBrush(
            (Color)ColorConverter.ConvertFromString(color));
    }

    // ---------------------------------------------------------
    // Profile management
    // ---------------------------------------------------------

    private void LoadProfiles()
    {
        try
        {
            if (!File.Exists(_profilesPath))
                return;

            string json = File.ReadAllText(_profilesPath);

            var profiles =
                JsonSerializer.Deserialize<List<SwitchProfile>>(json);

            if (profiles == null)
                return;

            _profiles.Clear();
            _profiles.AddRange(profiles);

            RefreshProfileCombo();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to load switch profiles.\n\n{ex.Message}",
                "Switch Diagnostics",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }
    }

    private void SaveProfiles()
    {
        string json = JsonSerializer.Serialize(
            _profiles,
            new JsonSerializerOptions
            {
                WriteIndented = true
            });

        File.WriteAllText(_profilesPath, json);
    }

    private void RefreshProfileCombo(Guid? selectedId = null)
    {
        _profileCombo.ItemsSource = null;
        _profileCombo.ItemsSource = _profiles;

        if (selectedId.HasValue)
        {
            _profileCombo.SelectedItem =
                _profiles.FirstOrDefault(x => x.Id == selectedId.Value);
        }
        else if (_profiles.Count > 0)
        {
            _profileCombo.SelectedIndex = 0;
        }
    }

    private void SaveProfileButton_Click(object sender, RoutedEventArgs e)
    {
        string name = _nameTextBox.Text.Trim();
        string ip = _ipTextBox.Text.Trim();
        string username = _usernameTextBox.Text.Trim();

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(ip) ||
            string.IsNullOrWhiteSpace(username))
        {
            MessageBox.Show(
                "Enter a profile name, switch IP address and username.",
                "Missing Information",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        SwitchProfile? profile = _profileCombo.SelectedItem as SwitchProfile;

        if (profile == null)
        {
            profile = new SwitchProfile
            {
                Id = Guid.NewGuid()
            };

            _profiles.Add(profile);
        }

        profile.Name = name;
        profile.IpAddress = ip;
        profile.Username = username;

        try
        {
            SaveProfiles();
            RefreshProfileCombo(profile.Id);

            _connectionStatus.Text = "Profile saved.";
            _connectionStatus.Foreground = Brush("#34D399");
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to save profile.\n\n{ex.Message}",
                "Switch Diagnostics",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
    }

    private void DeleteProfileButton_Click(object sender, RoutedEventArgs e)
    {
        if (_profileCombo.SelectedItem is not SwitchProfile profile)
            return;

        MessageBoxResult result = MessageBox.Show(
            $"Delete the profile '{profile.Name}'?",
            "Delete Switch Profile",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (result != MessageBoxResult.Yes)
            return;

        if (_sshClient?.IsConnected == true)
            Disconnect();

        _profiles.Remove(profile);

        WindowsCredentialManager.Delete(
            GetCredentialTarget(profile));

        SaveProfiles();
        RefreshProfileCombo();

        _nameTextBox.Clear();
        _ipTextBox.Clear();
        _usernameTextBox.Clear();
        _passwordBox.Clear();

        _connectionStatus.Text = "Profile deleted.";
        _connectionStatus.Foreground = Brush("#9CA3AF");
    }

    private void ProfileCombo_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_profileCombo.SelectedItem is not SwitchProfile profile)
            return;

        _nameTextBox.Text = profile.Name;
        _ipTextBox.Text = profile.IpAddress;
        _usernameTextBox.Text = profile.Username;

        _passwordBox.Clear();

        string? savedPassword =
            WindowsCredentialManager.Read(GetCredentialTarget(profile));

        if (!string.IsNullOrEmpty(savedPassword))
        {
            _passwordBox.Password = savedPassword;
            _savePasswordCheckBox.IsChecked = true;
        }
        else
        {
            _savePasswordCheckBox.IsChecked = false;
        }

        _connectionStatus.Text = "Profile selected.";
        _connectionStatus.Foreground = Brush("#9CA3AF");
    }

    // ---------------------------------------------------------
    // SSH connection
    // ---------------------------------------------------------

    private async void ConnectButton_Click(object sender, RoutedEventArgs e)
    {
        if (_isConnecting)
            return;

        string name = _nameTextBox.Text.Trim();
        string ip = _ipTextBox.Text.Trim();
        string username = _usernameTextBox.Text.Trim();
        string password = _passwordBox.Password;

        if (string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(ip) ||
            string.IsNullOrWhiteSpace(username) ||
            string.IsNullOrWhiteSpace(password))
        {
            MessageBox.Show(
                "Enter the switch profile details and password.",
                "Missing Connection Details",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        if (!System.Net.IPAddress.TryParse(ip, out _))
        {
            MessageBox.Show(
                "Enter a valid IPv4 or IPv6 address.",
                "Invalid IP Address",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);

            return;
        }

        SwitchProfile? profile =
            _profileCombo.SelectedItem as SwitchProfile;

        if (profile == null)
        {
            profile = new SwitchProfile
            {
                Id = Guid.NewGuid(),
                Name = name,
                IpAddress = ip,
                Username = username
            };

            _profiles.Add(profile);
        }
        else
        {
            profile.Name = name;
            profile.IpAddress = ip;
            profile.Username = username;
        }

        try
        {
            SaveProfiles();
            RefreshProfileCombo(profile.Id);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Unable to save the switch profile.\n\n{ex.Message}",
                "Switch Diagnostics",
                MessageBoxButton.OK,
                MessageBoxImage.Error);

            return;
        }

        if (_savePasswordCheckBox.IsChecked == true)
        {
            if (!WindowsCredentialManager.Write(
                    GetCredentialTarget(profile),
                    username,
                    password))
            {
                MessageBox.Show(
                    "Windows Credential Manager could not save the password. " +
                    "The connection will still be attempted.",
                    "Credential Storage",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }

        _isConnecting = true;
        _connectButton.IsEnabled = false;

        _connectionStatus.Text = $"Connecting to {ip}...";
        _connectionStatus.Foreground = Brush("#FBBF24");

        try
        {
            var connection = await Task.Run(() =>
            {
                var authentication =
                    new PasswordAuthenticationMethod(username, password);

                var connectionInfo = new ConnectionInfo(
                    ip,
                    SshPort,
                    username,
                    authentication);

                var sshClient = new SshClient(connectionInfo);

                sshClient.HostKeyReceived += (_, args) =>
                {
                    string fingerprint =
                        Convert.ToHexString(args.FingerPrint);

                    if (string.IsNullOrWhiteSpace(profile.HostKeyFingerprint))
                    {
                        bool trustHost = false;

                        Dispatcher.Invoke(() =>
                        {
                            MessageBoxResult answer = MessageBox.Show(
                                $"This is the first connection to this switch.\n\n" +
                                $"IP Address: {ip}\n" +
                                $"Host key fingerprint:\n{fingerprint}\n\n" +
                                "Only trust this key if it matches a fingerprint " +
                                "obtained from a trusted source.\n\n" +
                                "Do you want to trust and save this host key?",
                                "Verify SSH Host Key",
                                MessageBoxButton.YesNo,
                                MessageBoxImage.Warning);

                            trustHost = answer == MessageBoxResult.Yes;
                        });

                        if (trustHost)
                        {
                            profile.HostKeyFingerprint = fingerprint;

                            Dispatcher.Invoke(SaveProfiles);

                            args.CanTrust = true;
                        }
                        else
                        {
                            args.CanTrust = false;
                        }
                    }
                    else
                    {
                        args.CanTrust = string.Equals(
                            profile.HostKeyFingerprint,
                            fingerprint,
                            StringComparison.OrdinalIgnoreCase);
                    }
                };

                sshClient.Connect();

                // Create an interactive SSH shell instead of an exec channel.
                ShellStream shell = sshClient.CreateShellStream(
                    "vt100",
                    120,
                    40,
                    1200,
                    800,
                    4096);

                // Consume the initial login banner and switch prompt.
                ReadShellOutput(
                    shell,
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromMilliseconds(500));

                // Disable CLI pagination for this session.
                shell.WriteLine("no page");

                ReadShellOutput(
                    shell,
                    TimeSpan.FromSeconds(5),
                    TimeSpan.FromMilliseconds(500));

                return (Client: sshClient, Shell: shell);
            });

            _sshClient = connection.Client;
            _shellStream = connection.Shell;

            _connectionStatus.Text =
                $"Connected to {name} ({ip})";

            _connectionStatus.Foreground = Brush("#34D399");

            _connectButton.IsEnabled = false;
            _disconnectButton.IsEnabled = true;

            AppendOutput(
                $"Connected to {name} ({ip})\n" +
                $"Username: {username}\n" +
                $"SSH port: {SshPort}\n\n" +
                "Connection established successfully.\n");
        }
        catch (Exception ex)
        {
            _connectionStatus.Text = "Connection failed.";
            _connectionStatus.Foreground = Brush("#F87171");

            AppendOutput(
                $"Connection failed:\n{ex.Message}\n");

            MessageBox.Show(
                $"Unable to connect to the switch.\n\n{ex.Message}",
                "SSH Connection Error",
                MessageBoxButton.OK,
                MessageBoxImage.Error);
        }
        finally
        {
            _isConnecting = false;

            if (_sshClient?.IsConnected != true)
                _connectButton.IsEnabled = true;
        }
    }

    private void DisconnectButton_Click(object sender, RoutedEventArgs e)
    {
        Disconnect();
    }

    private void Disconnect()
    {
        try
        {
            if (_shellStream != null)
            {
                _shellStream.Dispose();
                _shellStream = null;
            }

            if (_sshClient != null)
            {
                if (_sshClient.IsConnected)
                    _sshClient.Disconnect();

                _sshClient.Dispose();
                _sshClient = null;
            }

            _connectionStatus.Text = "Not connected";
            _connectionStatus.Foreground = Brush("#9CA3AF");

            _connectButton.IsEnabled = true;
            _disconnectButton.IsEnabled = false;

            AppendOutput("\nDisconnected from switch.\n");
        }
        catch (Exception ex)
        {
            AppendOutput($"\nDisconnect error: {ex.Message}\n");
        }
    }

    // ---------------------------------------------------------
    // Read-only diagnostics
    // ---------------------------------------------------------

    private async Task RunDiagnosticCommandAsync(string command)
    {
        if (_sshClient?.IsConnected != true ||
            _shellStream == null ||
            !_sshClient.IsConnected)
        {
            MessageBox.Show(
                "Connect to a switch before running diagnostics.",
                "Not Connected",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            return;
        }

        ShellStream shell = _shellStream;

        await _commandSemaphore.WaitAsync();

        try
        {
            AppendOutput(
                $"\n==================================================\n" +
                $"> {command}\n" +
                $"==================================================\n");

            string result = await Task.Run(() =>
            {
                if (!shell.CanWrite)
                {
                    throw new InvalidOperationException(
                        "The SSH shell is no longer available.");
                }

                shell.WriteLine(command);
                shell.Flush();

                return ReadShellOutput(
                    shell,
                    TimeSpan.FromSeconds(30),
                    TimeSpan.FromMilliseconds(1200));
            });

            AppendOutput(
                string.IsNullOrWhiteSpace(result)
                    ? "(No output returned.)\n"
                    : result + "\n");
        }
        catch (Exception ex)
        {
            AppendOutput($"Command failed: {ex.Message}\n");
        }
        finally
        {
            _commandSemaphore.Release();
        }
    }
    private static string ReadShellOutput(
    ShellStream shell,
    TimeSpan timeout,
    TimeSpan quietPeriod)
    {
        var output = new StringBuilder();
        var buffer = new byte[4096];

        var totalTimer = Stopwatch.StartNew();
        var quietTimer = Stopwatch.StartNew();

        bool receivedData = false;

        while (totalTimer.Elapsed < timeout)
        {
            if (shell.DataAvailable)
            {
                int bytesRead = shell.Read(
                    buffer,
                    0,
                    buffer.Length);

                if (bytesRead > 0)
                {
                    output.Append(
                        Encoding.UTF8.GetString(buffer, 0, bytesRead));

                    receivedData = true;
                    quietTimer.Restart();
                }
            }
            else
            {
                if (receivedData && quietTimer.Elapsed >= quietPeriod)
                    break;

                Thread.Sleep(100);
            }
        }

        return output.ToString();
    }

    private void AppendOutput(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => AppendOutput(text));
            return;
        }

        _outputTextBox.AppendText(text);
        _outputTextBox.ScrollToEnd();
    }

    private static string GetCredentialTarget(SwitchProfile profile)
    {
        return $"NexusIT.SwitchDiagnostics.{profile.Id}";
    }

    private void SwitchManagementView_Unloaded(
        object sender,
        RoutedEventArgs e)
    {
        Disconnect();
    }

    // ---------------------------------------------------------
    // Profile model
    // ---------------------------------------------------------

    private sealed class SwitchProfile
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public string Name { get; set; } = string.Empty;

        public string IpAddress { get; set; } = string.Empty;

        public string Username { get; set; } = string.Empty;

        public string HostKeyFingerprint { get; set; } = string.Empty;
    }

    // ---------------------------------------------------------
    // Windows Credential Manager
    // ---------------------------------------------------------

    private static class WindowsCredentialManager
    {
        private const int CRED_TYPE_GENERIC = 1;
        private const int CRED_PERSIST_LOCAL_MACHINE = 2;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct NativeCredential
        {
            public uint Flags;
            public uint Type;
            public string TargetName;
            public string? Comment;
            public System.Runtime.InteropServices.ComTypes.FILETIME LastWritten;
            public uint CredentialBlobSize;
            public IntPtr CredentialBlob;
            public uint Persist;
            public uint AttributeCount;
            public IntPtr Attributes;
            public string? TargetAlias;
            public string? UserName;
        }

        [DllImport("Advapi32.dll", EntryPoint = "CredWriteW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredWrite(
            ref NativeCredential userCredential,
            uint flags);

        [DllImport("Advapi32.dll", EntryPoint = "CredReadW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredRead(
            string target,
            uint type,
            uint flags,
            out IntPtr credential);

        [DllImport("Advapi32.dll", EntryPoint = "CredDeleteW",
            CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool CredDelete(
            string target,
            uint type,
            uint flags);

        [DllImport("Advapi32.dll", SetLastError = true)]
        private static extern void CredFree(IntPtr buffer);

        public static bool Write(
            string target,
            string username,
            string password)
        {
            IntPtr passwordPointer = IntPtr.Zero;

            try
            {
                passwordPointer = Marshal.StringToCoTaskMemUni(password);

                var credential = new NativeCredential
                {
                    Type = CRED_TYPE_GENERIC,
                    TargetName = target,
                    UserName = username,
                    CredentialBlob = passwordPointer,
                    CredentialBlobSize = (uint)(password.Length * 2),
                    Persist = CRED_PERSIST_LOCAL_MACHINE
                };

                return CredWrite(ref credential, 0);
            }
            catch
            {
                return false;
            }
            finally
            {
                if (passwordPointer != IntPtr.Zero)
                {
                    Marshal.ZeroFreeCoTaskMemUnicode(passwordPointer);
                }
            }
        }

        public static string? Read(string target)
        {
            IntPtr credentialPointer = IntPtr.Zero;

            try
            {
                if (!CredRead(
                        target,
                        CRED_TYPE_GENERIC,
                        0,
                        out credentialPointer))
                {
                    return null;
                }

                var credential =
                    Marshal.PtrToStructure<NativeCredential>(
                        credentialPointer);

                if (credential.CredentialBlob == IntPtr.Zero ||
                    credential.CredentialBlobSize == 0)
                {
                    return null;
                }

                return Marshal.PtrToStringUni(
                    credential.CredentialBlob,
                    (int)credential.CredentialBlobSize / 2);
            }
            catch
            {
                return null;
            }
            finally
            {
                if (credentialPointer != IntPtr.Zero)
                    CredFree(credentialPointer);
            }
        }

        public static void Delete(string target)
        {
            try
            {
                CredDelete(target, CRED_TYPE_GENERIC, 0);
            }
            catch
            {
                // Ignore missing credentials.
            }
        }
    }
}