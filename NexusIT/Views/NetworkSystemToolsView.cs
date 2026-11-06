
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace NexusIT.App.Views
{
    public class NetworkSystemToolsView : UserControl
    {
        private readonly Brush PanelBrush =
            new SolidColorBrush(Color.FromRgb(27, 41, 64));

        private readonly Brush TextBrush =
            new SolidColorBrush(Color.FromRgb(243, 244, 246));

        private readonly Brush MutedBrush =
            new SolidColorBrush(Color.FromRgb(156, 163, 175));

        private readonly List<Button> _actionButtons = new();

        private TextBox _outputBox = null!;
        private TextBox _hostBox = null!;
        private TextBox _dnsBox = null!;
        private TextBox _serviceSearchBox = null!;
        private DataGrid _servicesGrid = null!;

        private List<ServiceItem> _services = new();
        private bool _isBusy;

        public NetworkSystemToolsView()
        {
            BuildInterface();
        }

        private void BuildInterface()
        {
            var root = new Grid
            {
                Margin = new Thickness(24),
                Background = Brushes.Transparent
            };

            // MAIN TWO-COLUMN LAYOUT
            var content = new Grid();

            content.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            content.ColumnDefinitions.Add(new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

            root.Children.Add(content);

            // ==================================================
            // LEFT COLUMN
            // ==================================================

            var leftPanel = new Grid
            {
                Margin = new Thickness(0, 0, 12, 0)
            };

            leftPanel.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });

            leftPanel.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });

            leftPanel.RowDefinitions.Add(new RowDefinition
            {
                Height = GridLength.Auto
            });

            Grid.SetColumn(leftPanel, 0);
            content.Children.Add(leftPanel);

            // NETWORK DIAGNOSTICS

            var networkCard = CreateCard(
                "Network Diagnostics",
                out var networkPanel);

            networkPanel.Children.Add(
                CreateLabel("Hostname or IP address"));

            _hostBox = CreateTextBox(
                "Enter hostname or IP address");

            networkPanel.Children.Add(_hostBox);

            var diagnosticButtons = new WrapPanel
            {
                Margin = new Thickness(0, 12, 0, 0)
            };

            diagnosticButtons.Children.Add(
                CreateButton("Ping", RunPing));

            diagnosticButtons.Children.Add(
                CreateButton("DNS Lookup", RunDnsLookup));

            networkPanel.Children.Add(diagnosticButtons);

            networkPanel.Children.Add(
                CreateLabel("DNS server (optional)"));

            _dnsBox = CreateTextBox("e.g. 8.8.8.8");

            networkPanel.Children.Add(_dnsBox);

            Grid.SetRow(networkCard, 0);
            leftPanel.Children.Add(networkCard);

            // IP CONFIGURATION

            var ipCard = CreateCard(
                "IP Configuration",
                out var ipPanel);

            ipPanel.Children.Add(
                CreateButton("View IP Configuration", RunIpConfig));

            ipPanel.Children.Add(
                CreateButton("Flush DNS Cache", FlushDns));

            ipPanel.Children.Add(
                CreateButton("Release IP Address", ReleaseIp));

            ipPanel.Children.Add(
                CreateButton("Renew IP Address", RenewIp));

            Grid.SetRow(ipCard, 1);
            leftPanel.Children.Add(ipCard);

            // COMPUTER INFORMATION

            var systemCard = CreateCard(
                "Computer Information",
                out var systemPanel);

            systemPanel.Children.Add(
                CreateButton(
                    "View Computer Information",
                    ShowComputerInformation));

            systemPanel.Children.Add(
                CreateButton(
                    "Open System Information",
                    () => StartExternalProcess("msinfo32.exe")));

            systemPanel.Children.Add(
                CreateButton(
                    "Open Device Manager (Admin)",
                    StartDeviceManagerAsAdmin));

            Grid.SetRow(systemCard, 2);
            leftPanel.Children.Add(systemCard);


            // ==================================================
            // RIGHT COLUMN
            // ==================================================

            var rightPanel = new Grid
            {
                Margin = new Thickness(12, 0, 0, 0)
            };

            // Command Output takes approximately 40%.
            // Windows Services takes the remaining 60%.

            rightPanel.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(4, GridUnitType.Star)
            });

            rightPanel.RowDefinitions.Add(new RowDefinition
            {
                Height = new GridLength(6, GridUnitType.Star)
            });

            Grid.SetColumn(rightPanel, 1);
            content.Children.Add(rightPanel);


            // ==================================================
            // COMMAND OUTPUT - TOP RIGHT
            // ==================================================

            var outputCard = CreateStretchCard(
                "Command Output",
                out var outputPanel);

            outputCard.Margin = new Thickness(0, 0, 0, 12);

            var outputToolbar = new WrapPanel
            {
                Margin = new Thickness(0, 0, 0, 8)
            };

            outputToolbar.Children.Add(
                CreateButton("Clear Output", () =>
                    _outputBox.Clear()));

            outputToolbar.Children.Add(
                CreateButton("Copy Output", CopyOutput));

            _outputBox = new TextBox
            {
                MinHeight = 80,
                Background = new SolidColorBrush(
                    Color.FromRgb(11, 18, 32)),
                Foreground = TextBrush,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(10),
                FontFamily = new FontFamily("Consolas"),
                FontSize = 12,
                IsReadOnly = true,
                TextWrapping = TextWrapping.Wrap,
                AcceptsReturn = true,
                VerticalScrollBarVisibility =
                    ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility =
                    ScrollBarVisibility.Auto
            };

            var outputContent = new Grid();

            outputContent.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            outputContent.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Star)
                });

            Grid.SetRow(outputToolbar, 0);
            outputContent.Children.Add(outputToolbar);

            Grid.SetRow(_outputBox, 1);
            outputContent.Children.Add(_outputBox);

            outputPanel.Children.Add(outputContent);

            Grid.SetRow(outputCard, 0);
            rightPanel.Children.Add(outputCard);


            // ==================================================
            // WINDOWS SERVICES - BELOW COMMAND OUTPUT
            // ==================================================

            var servicesCard = CreateStretchCard(
                "Windows Services",
                out var servicesPanel);

            servicesCard.Margin = new Thickness(0);

            // Service search and refresh toolbar.

            _serviceSearchBox = CreateTextBox(
                "Search services...");

            _serviceSearchBox.TextChanged +=
                (_, _) => FilterServices();

            var serviceToolbar = new StackPanel
            {
                Margin = new Thickness(0, 0, 0, 10)
            };

            serviceToolbar.Children.Add(_serviceSearchBox);

            serviceToolbar.Children.Add(
                CreateButton("Refresh Services", LoadServices));

            // Services DataGrid.

            _servicesGrid = new DataGrid
            {
                AutoGenerateColumns = false,
                IsReadOnly = true,
                CanUserAddRows = false,
                SelectionMode = DataGridSelectionMode.Single,
                HeadersVisibility = DataGridHeadersVisibility.Column,
                Background = PanelBrush,
                Foreground = TextBrush,
                BorderThickness = new Thickness(0),
                RowBackground = PanelBrush,
                AlternatingRowBackground =
                    new SolidColorBrush(Color.FromRgb(39, 50, 66)),
                GridLinesVisibility =
                    DataGridGridLinesVisibility.Horizontal,
                MinHeight = 100,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                HorizontalScrollBarVisibility = ScrollBarVisibility.Auto
            };

            _servicesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Service",
                Binding = new System.Windows.Data.Binding("DisplayName"),
                Width = new DataGridLength(
                    2,
                    DataGridLengthUnitType.Star)
            });

            _servicesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Status",
                Binding = new System.Windows.Data.Binding("State"),
                Width = new DataGridLength(
                    1,
                    DataGridLengthUnitType.Star)
            });

            _servicesGrid.Columns.Add(new DataGridTextColumn
            {
                Header = "Startup",
                Binding = new System.Windows.Data.Binding("StartMode"),
                Width = new DataGridLength(
                    1,
                    DataGridLengthUnitType.Star)
            });

            // Service action buttons.

            var serviceButtons = new WrapPanel
            {
                Margin = new Thickness(0, 10, 0, 0)
            };

            serviceButtons.Children.Add(
                CreateButton(
                    "Start",
                    () => ChangeSelectedService("Start")));

            serviceButtons.Children.Add(
                CreateButton(
                    "Stop",
                    () => ChangeSelectedService("Stop")));

            serviceButtons.Children.Add(
                CreateButton(
                    "Restart",
                    () => ChangeSelectedService("Restart")));

            // Arrange the service controls in a Grid.
            // The DataGrid receives all remaining available height.

            var serviceContent = new Grid();

            serviceContent.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            serviceContent.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Star)
                });

            serviceContent.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            Grid.SetRow(serviceToolbar, 0);
            serviceContent.Children.Add(serviceToolbar);

            Grid.SetRow(_servicesGrid, 1);
            serviceContent.Children.Add(_servicesGrid);

            Grid.SetRow(serviceButtons, 2);
            serviceContent.Children.Add(serviceButtons);

            servicesPanel.Children.Add(serviceContent);

            Grid.SetRow(servicesCard, 1);
            rightPanel.Children.Add(servicesCard);


            // ==================================================
            // FINISH
            // ==================================================

            Content = root;

            Loaded += (_, _) => LoadServices();
        }

        // --------------------------------------------------
        // UI HELPERS
        // --------------------------------------------------

        private Border CreateCard(
            string title,
            out StackPanel content)
        {
            content = new StackPanel();

            content.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush,
                Margin = new Thickness(0, 0, 0, 14)
            });

            return new Border
            {
                Background = PanelBrush,
                BorderBrush = new SolidColorBrush(
                    Color.FromRgb(52, 68, 92)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 12, 12),
                Child = content
            };
        }

        private Border CreateStretchCard(
            string title,
            out Grid content)
        {
            var layout = new Grid();

            layout.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = GridLength.Auto
                });

            layout.RowDefinitions.Add(
                new RowDefinition
                {
                    Height = new GridLength(1, GridUnitType.Star)
                });

            var titleText = new TextBlock
            {
                Text = title,
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                Foreground = TextBrush,
                Margin = new Thickness(0, 0, 0, 14)
            };

            Grid.SetRow(titleText, 0);
            layout.Children.Add(titleText);

            content = new Grid();

            Grid.SetRow(content, 1);
            layout.Children.Add(content);

            return new Border
            {
                Background = PanelBrush,
                BorderBrush = new SolidColorBrush(
                    Color.FromRgb(52, 68, 92)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(12),
                Padding = new Thickness(18),
                Child = layout
            };
        }

        private TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                Foreground = MutedBrush,
                FontSize = 12,
                Margin = new Thickness(0, 8, 0, 5)
            };
        }

        private TextBox CreateTextBox(string placeholder)
        {
            return new TextBox
            {
                Height = 34,
                Padding = new Thickness(8, 6, 8, 6),
                Background = new SolidColorBrush(
                    Color.FromRgb(11, 18, 32)),
                Foreground = TextBrush,
                BorderBrush = new SolidColorBrush(
                    Color.FromRgb(75, 85, 99)),
                BorderThickness = new Thickness(1),
                FontSize = 12,
                ToolTip = placeholder
            };
        }

        private Button CreateButton(
            string text,
            Action action)
        {
            var button = new Button
            {
                Content = text,
                Height = 36,
                MinWidth = 100,
                Padding = new Thickness(14, 7, 14, 7),
                Margin = new Thickness(0, 0, 8, 8),
                Cursor = System.Windows.Input.Cursors.Hand
            };

            // Use the shared glass button style defined in MainWindow.xaml.
            // Resolve it from the main window because this view is created
            // dynamically and may not yet be attached to the visual tree.
            if (Application.Current.MainWindow?.TryFindResource("PrimaryButton")
                is Style primaryButtonStyle)
            {
                button.Style = primaryButtonStyle;
            }

            button.Click += (_, _) =>
            {
                if (!_isBusy)
                    action();
            };

            _actionButtons.Add(button);

            return button;
        }

        private void SetBusy(bool busy)
        {
            _isBusy = busy;

            foreach (var button in _actionButtons)
                button.IsEnabled = !busy;
        }

        private void SetOutput(string text)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => SetOutput(text));
                return;
            }

            _outputBox.Text = text;
            _outputBox.ScrollToEnd();
        }

        // --------------------------------------------------
        // NETWORK DIAGNOSTICS
        // --------------------------------------------------

        private async void RunPing()
        {
            string host = _hostBox.Text.Trim();

            if (!IsValidHost(host))
            {
                ShowWarning("Enter a valid hostname or IP address.");
                return;
            }

            await RunCommandAsync(
                "ping.exe",
                "-n 4 " + QuoteWindowsArgument(host));
        }

        private async void RunDnsLookup()
        {
            string host = _hostBox.Text.Trim();

            if (!IsValidHost(host))
            {
                ShowWarning("Enter a valid hostname or IP address.");
                return;
            }

            string dnsServer = _dnsBox.Text.Trim();

            if (!string.IsNullOrWhiteSpace(dnsServer) &&
                !IsValidHost(dnsServer))
            {
                ShowWarning("Enter a valid DNS server address.");
                return;
            }

            string arguments = QuoteWindowsArgument(host);

            if (!string.IsNullOrWhiteSpace(dnsServer))
                arguments += " " + QuoteWindowsArgument(dnsServer);

            await RunCommandAsync("nslookup.exe", arguments);
        }

        private async void RunIpConfig()
        {
            await RunCommandAsync("ipconfig.exe", "/all");
        }

        // --------------------------------------------------
        // IP CONFIGURATION
        // --------------------------------------------------

        private async void FlushDns()
        {
            if (!ConfirmAction(
                "Flush the local DNS resolver cache?"))
                return;

            await RunElevatedPowerShellAsync(
                "Clear-DnsClientCache; " +
                "ipconfig /flushdns");
        }

        private async void ReleaseIp()
        {
            if (!ConfirmAction(
                "Release the IP address for all network adapters? " +
                "This may disconnect your computer from the network."))
                return;

            await RunElevatedPowerShellAsync(
                "ipconfig /release");
        }

        private async void RenewIp()
        {
            if (!ConfirmAction(
                "Renew the IP address for all network adapters?"))
                return;

            await RunElevatedPowerShellAsync(
                "ipconfig /renew");
        }

        // --------------------------------------------------
        // COMPUTER INFORMATION
        // --------------------------------------------------

        private void ShowComputerInformation()
        {
            var info = new StringBuilder();

            info.AppendLine("COMPUTER INFORMATION");
            info.AppendLine("====================");
            info.AppendLine();

            info.AppendLine(
                $"Computer Name: {Environment.MachineName}");

            info.AppendLine(
                $"Username: {Environment.UserName}");

            info.AppendLine(
                $"User Domain: {Environment.UserDomainName}");

            info.AppendLine(
                $"Operating System: {Environment.OSVersion}");

            info.AppendLine(
                $"64-bit OS: {Environment.Is64BitOperatingSystem}");

            info.AppendLine(
                $"64-bit Process: {Environment.Is64BitProcess}");

            info.AppendLine(
                $"Processor Count: {Environment.ProcessorCount}");

            info.AppendLine();

            try
            {
                using var identity = WindowsIdentity.GetCurrent();

                bool isAdmin = new WindowsPrincipal(identity)
                    .IsInRole(WindowsBuiltInRole.Administrator);

                info.AppendLine(
                    $"Administrator: {isAdmin}");
            }
            catch
            {
                info.AppendLine(
                    "Administrator: Unable to determine");
            }

            SetOutput(info.ToString());
        }

        // --------------------------------------------------
        // WINDOWS SERVICES
        // --------------------------------------------------

        private async void LoadServices()
        {
            if (_isBusy)
                return;

            SetBusy(true);

            try
            {
                SetOutput("Loading Windows services...\n");

                string script =
                    "@(Get-CimInstance Win32_Service | " +
                    "Select-Object Name,DisplayName,State,StartMode) | " +
                    "ConvertTo-Json -Compress";

                string output =
                    await RunPowerShellCaptureAsync(script);

                if (string.IsNullOrWhiteSpace(output))
                {
                    _services.Clear();
                    FilterServices();

                    SetOutput(
                        "No service information was returned.");

                    return;
                }

                using var document =
                    JsonDocument.Parse(output);

                var services = new List<ServiceItem>();

                if (document.RootElement.ValueKind ==
                    JsonValueKind.Array)
                {
                    foreach (var element in
                             document.RootElement.EnumerateArray())
                    {
                        var service =
                            element.Deserialize<ServiceItem>(
                                JsonOptions());

                        if (service != null)
                            services.Add(service);
                    }
                }
                else if (document.RootElement.ValueKind ==
                         JsonValueKind.Object)
                {
                    var service =
                        document.RootElement.Deserialize<ServiceItem>(
                            JsonOptions());

                    if (service != null)
                        services.Add(service);
                }

                _services = services
                    .OrderBy(s => s.DisplayName)
                    .ToList();

                FilterServices();

                SetOutput(
                    $"Successfully loaded {_services.Count} Windows services.");
            }
            catch (Exception ex)
            {
                SetOutput(
                    $"Unable to load Windows services:\n{ex.Message}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void FilterServices()
        {
            if (_servicesGrid == null)
                return;

            string search =
                _serviceSearchBox?.Text?.Trim() ?? "";

            var filtered = string.IsNullOrWhiteSpace(search)
                ? _services
                : _services.Where(s =>
                    (s.DisplayName ?? "").Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase) ||
                    (s.Name ?? "").Contains(
                        search,
                        StringComparison.OrdinalIgnoreCase));

            _servicesGrid.ItemsSource =
                filtered.ToList();
        }

        private async void ChangeSelectedService(string action)
        {
            if (_servicesGrid.SelectedItem is not ServiceItem service)
            {
                ShowInformation(
                    "Select a Windows service first.");

                return;
            }

            if (action is not ("Start" or "Stop" or "Restart"))
                return;

            if (action != "Start")
            {
                if (!ConfirmAction(
                    $"{action} the service '{service.DisplayName}'?"))
                    return;
            }

            string serviceName = service.Name ?? "";

            if (string.IsNullOrWhiteSpace(serviceName))
                return;

            string escapedName =
                serviceName.Replace("'", "''");

            string script = action switch
            {
                "Start" =>
                    $"Start-Service -Name '{escapedName}' -ErrorAction Stop; " +
                    $"Get-Service -Name '{escapedName}' | " +
                    "Format-List Name,DisplayName,Status",

                "Stop" =>
                    $"Stop-Service -Name '{escapedName}' -Force -ErrorAction Stop; " +
                    $"Get-Service -Name '{escapedName}' | " +
                    "Format-List Name,DisplayName,Status",

                "Restart" =>
                    $"Restart-Service -Name '{escapedName}' -Force -ErrorAction Stop; " +
                    $"Get-Service -Name '{escapedName}' | " +
                    "Format-List Name,DisplayName,Status",

                _ => ""
            };

            if (string.IsNullOrWhiteSpace(script))
                return;

            await RunElevatedPowerShellAsync(script);

            LoadServices();
        }

        // --------------------------------------------------
        // COMMAND EXECUTION
        // --------------------------------------------------

        private async Task RunCommandAsync(
            string executable,
            string arguments)
        {
            if (_isBusy)
                return;

            SetBusy(true);

            try
            {
                string executablePath = Path.Combine(
                    Environment.SystemDirectory,
                    executable);

                if (!File.Exists(executablePath))
                {
                    SetOutput(
                        $"Windows command not found:\n{executablePath}");
                    return;
                }

                SetOutput(
                    $"Running: {executablePath} {arguments}\n\nPlease wait...");

                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath,
                    Arguments = arguments,
                    WorkingDirectory = Environment.SystemDirectory,
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = new Process
                {
                    StartInfo = startInfo
                };

                bool started = process.Start();

                if (!started)
                {
                    SetOutput($"Unable to start {executable}.");
                    return;
                }

                Task<string> stdoutTask =
                    process.StandardOutput.ReadToEndAsync();

                Task<string> stderrTask =
                    process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                var result = new StringBuilder();

                result.AppendLine($"Command: {executable} {arguments}");
                result.AppendLine($"Exit Code: {process.ExitCode}");
                result.AppendLine();

                if (!string.IsNullOrWhiteSpace(stdout))
                    result.Append(stdout);

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    result.AppendLine();
                    result.AppendLine("ERROR:");
                    result.Append(stderr);
                }

                if (string.IsNullOrWhiteSpace(stdout) &&
                    string.IsNullOrWhiteSpace(stderr))
                {
                    result.AppendLine(
                        "The command completed without returning output.");
                }

                SetOutput(result.ToString());
            }
            catch (Exception ex)
            {
                SetOutput(
                    $"Command execution failed:\n{ex}");
            }
            finally
            {
                SetBusy(false);
            }
        }

        // --------------------------------------------------
        // ELEVATED POWERSHELL
        // --------------------------------------------------

        private async Task RunElevatedPowerShellAsync(
            string script)
        {
            if (_isBusy)
                return;

            SetBusy(true);

            string scriptPath = Path.Combine(
                Path.GetTempPath(),
                $"NexusIT_{Guid.NewGuid():N}.ps1");

            string outputPath = Path.Combine(
                Path.GetTempPath(),
                $"NexusIT_{Guid.NewGuid():N}.txt");

            try
            {
                string escapedOutputPath =
                    outputPath.Replace("'", "''");

                string fullScript =
                    "$ErrorActionPreference = 'Stop'\n" +
                    "try {\n" +
                    "$result = & {\n" +
                    script + "\n" +
                    "} 2>&1 | Out-String\n" +
                    $"[System.IO.File]::WriteAllText('{escapedOutputPath}', $result)\n" +
                    "if ($LASTEXITCODE -ne $null) { exit $LASTEXITCODE }\n" +
                    "exit 0\n" +
                    "} catch {\n" +
                    $"[System.IO.File]::WriteAllText('{escapedOutputPath}', ($_ | Out-String))\n" +
                    "exit 1\n" +
                    "}\n";

                await File.WriteAllTextAsync(
                    scriptPath,
                    fullScript);

                SetOutput(
                    "Waiting for administrator approval...\n\n" +
                    "Please approve the Windows UAC prompt.");

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    Arguments =
                        $"-NoProfile -ExecutionPolicy Bypass -File \"{scriptPath}\"",
                    UseShellExecute = true,
                    Verb = "runas",
                    WindowStyle = ProcessWindowStyle.Hidden
                };

                using var process = Process.Start(startInfo);

                if (process == null)
                {
                    SetOutput(
                        "Unable to start elevated PowerShell.");

                    return;
                }

                await process.WaitForExitAsync();

                string commandOutput =
                    File.Exists(outputPath)
                        ? await File.ReadAllTextAsync(outputPath)
                        : "";

                var result = new StringBuilder();

                result.AppendLine(
                    $"Administrator command exit code: {process.ExitCode}");

                result.AppendLine();

                if (!string.IsNullOrWhiteSpace(commandOutput))
                {
                    result.Append(commandOutput);
                }
                else if (process.ExitCode == 0)
                {
                    result.AppendLine(
                        "Command completed successfully.");
                }
                else
                {
                    result.AppendLine(
                        "The command failed or returned no output.");
                }

                SetOutput(result.ToString());
            }
            catch (System.ComponentModel.Win32Exception ex)
                when (ex.NativeErrorCode == 1223)
            {
                SetOutput(
                    "Administrator approval was cancelled.");
            }
            catch (Exception ex)
            {
                SetOutput(
                    $"Elevated command failed:\n{ex.Message}");
            }
            finally
            {
                TryDeleteFile(scriptPath);
                TryDeleteFile(outputPath);

                SetBusy(false);
            }
        }

        // --------------------------------------------------
        // POWERSHELL CAPTURE
        // --------------------------------------------------

        private async Task<string> RunPowerShellCaptureAsync(
            string script)
        {
            string scriptPath = Path.Combine(
                Path.GetTempPath(),
                $"NexusIT_{Guid.NewGuid():N}.ps1");

            try
            {
                await File.WriteAllTextAsync(
                    scriptPath,
                    script);

                var startInfo = new ProcessStartInfo
                {
                    FileName = "powershell.exe",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                startInfo.ArgumentList.Add("-NoProfile");
                startInfo.ArgumentList.Add("-ExecutionPolicy");
                startInfo.ArgumentList.Add("Bypass");
                startInfo.ArgumentList.Add("-File");
                startInfo.ArgumentList.Add(scriptPath);

                using var process = new Process
                {
                    StartInfo = startInfo
                };

                process.Start();

                Task<string> stdoutTask =
                    process.StandardOutput.ReadToEndAsync();

                Task<string> stderrTask =
                    process.StandardError.ReadToEndAsync();

                await process.WaitForExitAsync();

                string stdout = await stdoutTask;
                string stderr = await stderrTask;

                if (process.ExitCode != 0)
                {
                    throw new InvalidOperationException(
                        string.IsNullOrWhiteSpace(stderr)
                            ? $"PowerShell exited with code {process.ExitCode}."
                            : stderr);
                }

                if (!string.IsNullOrWhiteSpace(stderr))
                {
                    throw new InvalidOperationException(stderr);
                }

                return stdout.Trim();
            }
            finally
            {
                TryDeleteFile(scriptPath);
            }
        }

        // --------------------------------------------------
        // EXTERNAL APPLICATIONS
        // --------------------------------------------------

        private void StartExternalProcess(string executable)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = true
                });

                SetOutput(
                    $"Successfully launched {executable}.");
            }
            catch (Exception ex)
            {
                SetOutput(
                    $"Unable to launch {executable}:\n{ex.Message}");
            }
        }

        private void StartDeviceManagerAsAdmin()
        {
            try
            {
                string mmcPath = Path.Combine(
                    Environment.SystemDirectory,
                    "mmc.exe");

                string deviceManagerPath = Path.Combine(
                    Environment.SystemDirectory,
                    "devmgmt.msc");

                var startInfo = new ProcessStartInfo
                {
                    FileName = mmcPath,
                    Arguments = QuoteWindowsArgument(deviceManagerPath),
                    UseShellExecute = true,
                    Verb = "runas"
                };

                Process.Start(startInfo);

                SetOutput(
                    "Launching Device Manager with administrator privileges...");
            }
            catch (System.ComponentModel.Win32Exception ex)
                when (ex.NativeErrorCode == 1223)
            {
                SetOutput("Device Manager launch was cancelled.");
            }
            catch (Exception ex)
            {
                SetOutput(
                    $"Unable to launch Device Manager as administrator:\n{ex.Message}");
            }
        }

        // --------------------------------------------------
        // GENERAL HELPERS
        // --------------------------------------------------

        private static JsonSerializerOptions JsonOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
        }

        private static string QuoteWindowsArgument(string value)
        {
            return "\"" + value.Replace("\"", "\\\"") + "\"";
        }

        private bool IsValidHost(string value)
        {
            if (string.IsNullOrWhiteSpace(value) ||
                value.Length > 253)
                return false;

            return value.All(c =>
                char.IsLetterOrDigit(c) ||
                c == '.' ||
                c == '-' ||
                c == ':');
        }

        private bool ConfirmAction(string message)
        {
            return MessageBox.Show(
                message,
                "Confirm Action",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning) == MessageBoxResult.Yes;
        }

        private void ShowWarning(string message)
        {
            MessageBox.Show(
                message,
                "Network & System Tools",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        private void ShowInformation(string message)
        {
            MessageBox.Show(
                message,
                "Network & System Tools",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        private void CopyOutput()
        {
            try
            {
                if (string.IsNullOrEmpty(_outputBox.Text))
                {
                    ShowInformation("There is no output to copy.");
                    return;
                }

                Clipboard.SetText(_outputBox.Text);

                SetOutput(
                    _outputBox.Text +
                    Environment.NewLine +
                    Environment.NewLine +
                    "Output copied to clipboard.");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Unable to copy output:\n{ex.Message}",
                    "Clipboard Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch
            {
                // Temporary file cleanup is best-effort.
            }
        }

        private class ServiceItem
        {
            public string? Name { get; set; }

            public string? DisplayName { get; set; }

            public string? State { get; set; }

            public string? StartMode { get; set; }
        }
    }
}
