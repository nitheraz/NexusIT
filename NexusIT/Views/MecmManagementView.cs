
using NexusIT.App.Models;
using NexusIT.App.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Media;
using System.ComponentModel;

namespace NexusIT.App.Views;

public sealed class MecmManagementView : UserControl
{
    private readonly MecmConnectionService _connection;
    private readonly MecmDeviceService _deviceService;
    private readonly MecmCollectionService _collectionService;
    private readonly MecmDeploymentService _deploymentService;
    private readonly MecmActivityLogService _activityLogService;

    private readonly TextBox _deviceSearchBox = new();
    private readonly DataGrid _deviceGrid;
    private readonly TextBlock _deviceDetails = new();

    private readonly DataGrid _collectionGrid;
    private readonly DataGrid _memberGrid;
    private readonly TextBox _collectionSearchBox = new();
    private readonly TextBlock _collectionDetails = new();

    private readonly TextBox _deploymentSearchBox = new();
    private readonly DataGrid _deploymentGrid;

    private readonly DataGrid _activityGrid;
    private readonly TextBlock _statusText = new();

    private readonly Button _addToCollectionButton;
    private readonly Button _removeFromCollectionButton;

    private bool _devicesLoaded;
    private bool _collectionsLoaded;
    private bool _deploymentsLoaded;
    private bool _activityLoaded;

    private bool _devicesLoading;
    private bool _collectionsLoading;
    private bool _deploymentsLoading;
    private bool _activityLoading;

    private readonly Dictionary<string, List<MecmCollectionMember>>
        _memberCache = new();

    private readonly HashSet<string> _membersLoading = new();

    private static readonly Brush BackgroundBrush =
        new SolidColorBrush(Color.FromRgb(17, 24, 39));

    private static readonly Brush PanelBrush =
        new SolidColorBrush(Color.FromRgb(31, 41, 55));

    private static readonly Brush AccentBrush =
        new SolidColorBrush(Color.FromRgb(59, 130, 246));

    private static readonly Brush TextBrush =
        new SolidColorBrush(Color.FromRgb(229, 231, 235));

    private static readonly Brush MutedBrush =
        new SolidColorBrush(Color.FromRgb(156, 163, 175));

    private static readonly Brush HeaderBrush =
        new SolidColorBrush(Color.FromRgb(15, 23, 42));

    private static readonly Brush HeaderBorderBrush =
        new SolidColorBrush(Color.FromRgb(55, 65, 81));

    private static readonly Brush AlternateRowBrush =
        new SolidColorBrush(Color.FromRgb(25, 35, 49));

    private static readonly Brush HoverRowBrush =
        new SolidColorBrush(Color.FromRgb(38, 54, 74));

    public MecmManagementView(
        MecmConnectionService connection,
        MecmActivityLogService activityLogService)
    {
        _connection = connection
            ?? throw new ArgumentNullException(nameof(connection));

        _activityLogService = activityLogService
            ?? throw new ArgumentNullException(nameof(activityLogService));

        _deviceService = new MecmDeviceService(_connection);
        _collectionService = new MecmCollectionService(_connection);
        _deploymentService = new MecmDeploymentService(_connection);

        _deviceGrid = CreateGrid();
        _collectionGrid = CreateGrid();
        _memberGrid = CreateGrid();
        _deploymentGrid = CreateGrid();
        _activityGrid = CreateGrid();

        _addToCollectionButton = CreateButton("Add to Collection");
        _addToCollectionButton.IsEnabled = false;
        _addToCollectionButton.Click += async (_, _) =>
            await AddSelectedDeviceToCollectionAsync();

        _removeFromCollectionButton = CreateButton("Remove Selected Device");
        _removeFromCollectionButton.IsEnabled = false;
        _removeFromCollectionButton.Click += async (_, _) =>
            await RemoveSelectedCollectionMemberAsync();

        Content = BuildInterface();

        _ = LoadDevicesAsync();
    }

    // ---------------------------------------------------------
    // MAIN INTERFACE
    // ---------------------------------------------------------

    private UIElement BuildInterface()
    {
        var root = new Grid
        {
            Background = BackgroundBrush,
            Margin = new Thickness(20)
        };

        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        root.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        root.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel
        {
            Margin = new Thickness(0, 0, 0, 18)
        };

        header.Children.Add(new TextBlock
        {
            Text = "MECM Management",
            FontSize = 24,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush
        });

        header.Children.Add(new TextBlock
        {
            Text = "Manage devices, collections, deployments and activity.",
            FontSize = 13,
            Foreground = MutedBrush,
            Margin = new Thickness(0, 5, 0, 0)
        });

        Grid.SetRow(header, 0);
        root.Children.Add(header);

        var tabs = new TabControl
        {
            Background = BackgroundBrush,
            BorderThickness = new Thickness(0),
            Foreground = TextBrush
        };

        tabs.Items.Add(CreateDevicesTab());
        tabs.Items.Add(CreateCollectionsTab());
        tabs.Items.Add(CreateDeploymentsTab());
        tabs.Items.Add(CreateActivityTab());

        tabs.SelectionChanged += (_, e) =>
        {
            if (!ReferenceEquals(e.Source, tabs))
                return;

            if (tabs.SelectedItem is not TabItem selected)
                return;

            switch (selected.Header?.ToString())
            {
                case "Devices":
                    _ = LoadDevicesAsync();
                    break;

                case "Collections":
                    _ = LoadCollectionsAsync();
                    break;

                case "Deployments":
                    _ = LoadDeploymentsAsync();
                    break;

                case "Activity Log":
                    _ = LoadActivityLogAsync();
                    break;
            }
        };

        Grid.SetRow(tabs, 1);
        root.Children.Add(tabs);

        _statusText.Text = "Ready";
        _statusText.Foreground = MutedBrush;
        _statusText.FontSize = 12;
        _statusText.Margin = new Thickness(0, 12, 0, 0);

        Grid.SetRow(_statusText, 2);
        root.Children.Add(_statusText);

        return root;
    }

    // ---------------------------------------------------------
    // DEVICES TAB
    // ---------------------------------------------------------

    private TabItem CreateDevicesTab()
    {
        var layout = new Grid
        {
            Margin = new Thickness(8)
        };

        layout.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        layout.RowDefinitions.Add(
            new RowDefinition { Height = new GridLength(185) });

        var toolbar = new DockPanel
        {
            LastChildFill = true,
            Margin = new Thickness(0, 0, 0, 12)
        };

        DockPanel.SetDock(_addToCollectionButton, Dock.Right);
        toolbar.Children.Add(_addToCollectionButton);

        var refreshButton = CreateButton("Refresh");
        refreshButton.Click += async (_, _) =>
            await LoadDevicesAsync(force: true);

        DockPanel.SetDock(refreshButton, Dock.Right);
        toolbar.Children.Add(refreshButton);

        var searchButton = CreateButton("Search");
        searchButton.Click += async (_, _) =>
            await LoadDevicesAsync(force: true);

        DockPanel.SetDock(searchButton, Dock.Right);
        toolbar.Children.Add(searchButton);

        ConfigureSearchBox(
            _deviceSearchBox,
            "Search device name");

        _deviceSearchBox.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                await LoadDevicesAsync(force: true);
            }
        };

        toolbar.Children.Add(_deviceSearchBox);

        Grid.SetRow(toolbar, 0);
        layout.Children.Add(toolbar);

        ConfigureDeviceGrid();

        _deviceGrid.SelectionChanged += (_, _) =>
        {
            if (_deviceGrid.SelectedItem is MecmDevice device)
            {
                ShowDeviceDetails(device);
                _addToCollectionButton.IsEnabled = true;
            }
            else
            {
                _deviceDetails.Text =
                    "Select a device to view its details.";

                _addToCollectionButton.IsEnabled = false;
            }
        };

        Grid.SetRow(_deviceGrid, 1);
        layout.Children.Add(_deviceGrid);

        var detailsPanel = CreatePanel();

        detailsPanel.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        detailsPanel.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        var detailsTitle = new TextBlock
        {
            Text = "Device Details",
            FontSize = 14,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            Margin = new Thickness(0, 0, 0, 8)
        };

        Grid.SetRow(detailsTitle, 0);
        detailsPanel.Children.Add(detailsTitle);

        _deviceDetails.Text =
            "Select a device to view its details.";

        _deviceDetails.Foreground = TextBrush;
        _deviceDetails.TextWrapping = TextWrapping.Wrap;
        _deviceDetails.Margin = new Thickness(0, 0, 8, 0);

        var detailsScroll = new ScrollViewer
        {
            Content = _deviceDetails,
            VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
            HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
        };

        Grid.SetRow(detailsScroll, 1);
        detailsPanel.Children.Add(detailsScroll);

        Grid.SetRow(detailsPanel, 2);
        layout.Children.Add(detailsPanel);

        return new TabItem
        {
            Header = "Devices",
            Content = layout
        };
    }

    private void ConfigureDeviceGrid()
    {
        AddColumn(_deviceGrid, "Device Name", "Name", 180);
        AddColumn(_deviceGrid, "Resource ID", "ResourceId", 90);
        AddColumn(_deviceGrid, "Last Logged-on User", "LastLogonUser", 180);
        AddColumn(_deviceGrid, "Operating System", "OperatingSystem", 180);
        AddColumn(_deviceGrid, "Client Version", "ClientVersion", 120);
        AddColumn(_deviceGrid, "Client Installed", "ClientInstalled", 100);
        AddColumn(_deviceGrid, "Domain", "Domain", 120);
    }

    private async Task LoadDevicesAsync(bool force = false)
    {
        if (_devicesLoading || (_devicesLoaded && !force))
            return;

        _devicesLoading = true;

        try
        {
            string search = _deviceSearchBox.Text.Trim();

            SetStatus("Loading devices...");

            List<MecmDevice> devices = await Task.Run(() =>
                _deviceService.SearchDevices(search, 200));

            _deviceGrid.ItemsSource = devices;
            _deviceGrid.SelectedItem = null;

            _deviceDetails.Text =
                "Select a device to view its details.";

            _addToCollectionButton.IsEnabled = false;

            _devicesLoaded = true;

            SetStatus($"Loaded {devices.Count} device(s).");
        }
        catch (Exception ex)
        {
            SetStatus("Unable to load devices.");
            ShowError("Device Search", ex);
        }
        finally
        {
            _devicesLoading = false;
        }
    }

    private void ShowDeviceDetails(MecmDevice device)
    {
        _deviceDetails.Text =
            $"Name: {device.Name}\n" +
            $"Resource ID: {device.ResourceId}\n" +
            $"Username: {device.UserName ?? "N/A"}\n" +
            $"Last Logon User: {device.LastLogonUser ?? "N/A"}\n" +
            $"Operating System: {device.OperatingSystem ?? "N/A"}\n" +
            $"Client Version: {device.ClientVersion ?? "N/A"}\n" +
            $"Client Installed: {device.ClientInstalled}\n" +
            $"Site Code: {device.SiteCode ?? "N/A"}\n" +
            $"Domain: {device.Domain ?? "N/A"}\n" +
            $"MAC Address: {device.MacAddress ?? "N/A"}\n" +
            $"IP Address: {device.IpAddress ?? "N/A"}\n" +
            $"SMS Unique ID: {device.SmsUniqueIdentifier ?? "N/A"}";
    }

    // ---------------------------------------------------------
    // COLLECTIONS TAB
    // ---------------------------------------------------------

    private TabItem CreateCollectionsTab()
    {
        var layout = new Grid
        {
            Margin = new Thickness(8)
        };

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1, GridUnitType.Star)
            });

        layout.ColumnDefinitions.Add(
            new ColumnDefinition
            {
                Width = new GridLength(1.3, GridUnitType.Star)
            });

        var collectionsPanel = CreatePanel();
        collectionsPanel.Margin = new Thickness(0, 0, 8, 0);

        var collectionsLayout = new Grid();

        collectionsLayout.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        collectionsLayout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        var collectionHeader = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 10),
            LastChildFill = true
        };

        var collectionTitle = new TextBlock
        {
            Text = "Device Collections",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(0, 0, 12, 0)
        };

        DockPanel.SetDock(collectionTitle, Dock.Left);
        collectionHeader.Children.Add(collectionTitle);

        var collectionRefresh = CreateButton("Refresh");
        collectionRefresh.Click += async (_, _) =>
            await LoadCollectionsAsync(force: true);

        DockPanel.SetDock(collectionRefresh, Dock.Right);
        collectionHeader.Children.Add(collectionRefresh);

        var collectionSearchButton = CreateButton("Search");
        collectionSearchButton.Click += async (_, _) =>
            await LoadCollectionsAsync(force: true);

        DockPanel.SetDock(collectionSearchButton, Dock.Right);
        collectionHeader.Children.Add(collectionSearchButton);

        ConfigureSearchBox(
            _collectionSearchBox,
            "Search device collections by name");

        _collectionSearchBox.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                await LoadCollectionsAsync(force: true);
            }
        };

        collectionHeader.Children.Add(_collectionSearchBox);

        Grid.SetRow(collectionHeader, 0);
        collectionsLayout.Children.Add(collectionHeader);

        _collectionGrid.SelectionChanged += async (_, _) =>
        {
            if (_collectionGrid.SelectedItem is MecmCollection collection)
            {
                _memberGrid.ItemsSource = null;
                _removeFromCollectionButton.IsEnabled = false;

                await LoadCollectionMembersAsync(collection);
            }
            else
            {
                _memberGrid.ItemsSource = null;
                _collectionDetails.Text =
                    "Select a collection to view its members.";

                _removeFromCollectionButton.IsEnabled = false;
            }
        };

        AddColumn(_collectionGrid, "Collection Name", "Name", 220);
        AddColumn(_collectionGrid, "Collection ID", "CollectionId", 110);
        AddColumn(_collectionGrid, "Type", "CollectionType", 70);
        AddColumn(_collectionGrid, "Members", "MemberCount", 80);

        AddColumn(_collectionGrid, "Limiting Collection", "LimitToCollectionId", 140);

        Grid.SetRow(_collectionGrid, 1);
        collectionsLayout.Children.Add(_collectionGrid);

        collectionsPanel.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        Grid.SetRow(collectionsLayout, 0);
        collectionsPanel.Children.Add(collectionsLayout);

        Grid.SetColumn(collectionsPanel, 0);
        layout.Children.Add(collectionsPanel);

        var membersPanel = CreatePanel();
        membersPanel.Margin = new Thickness(8, 0, 0, 0);

        var membersLayout = new Grid();

        membersLayout.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        membersLayout.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        membersLayout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        var memberHeader = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 8)
        };

        var memberTitle = new TextBlock
        {
            Text = "Collection Members",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        DockPanel.SetDock(memberTitle, Dock.Left);
        memberHeader.Children.Add(memberTitle);

        DockPanel.SetDock(_removeFromCollectionButton, Dock.Right);
        memberHeader.Children.Add(_removeFromCollectionButton);

        Grid.SetRow(memberHeader, 0);
        membersLayout.Children.Add(memberHeader);

        _collectionDetails.Text =
            "Select a collection to view its members.";

        _collectionDetails.Foreground = MutedBrush;
        _collectionDetails.Margin = new Thickness(0, 0, 0, 8);

        Grid.SetRow(_collectionDetails, 1);
        membersLayout.Children.Add(_collectionDetails);

        AddColumn(_memberGrid, "Device Name", "Name", 180);
        AddColumn(_memberGrid, "Resource ID", "ResourceId", 100);
        AddColumn(_memberGrid, "Username", "UserName", 180);

        _memberGrid.SelectionChanged += (_, _) =>
        {
            _removeFromCollectionButton.IsEnabled =
                _collectionGrid.SelectedItem is MecmCollection &&
                _memberGrid.SelectedItem is MecmCollectionMember;
        };

        Grid.SetRow(_memberGrid, 2);
        membersLayout.Children.Add(_memberGrid);

        membersPanel.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        Grid.SetRow(membersLayout, 0);
        membersPanel.Children.Add(membersLayout);

        Grid.SetColumn(membersPanel, 1);
        layout.Children.Add(membersPanel);

        return new TabItem
        {
            Header = "Collections",
            Content = layout
        };
    }

    private async Task LoadCollectionsAsync(bool force = false)
    {
        if (_collectionsLoading || (_collectionsLoaded && !force))
            return;

        _collectionsLoading = true;

        try
        {
            SetStatus("Loading collections...");

            string search = _collectionSearchBox.Text.Trim();

            List<MecmCollection> collections = await Task.Run(() =>
                _collectionService.SearchDeviceCollections(search));

            _collectionGrid.ItemsSource = collections;
            _memberGrid.ItemsSource = null;
            _removeFromCollectionButton.IsEnabled = false;

            _memberCache.Clear();

            _collectionDetails.Text =
                "Select a collection to view its members.";

            _collectionsLoaded = true;

            SetStatus($"Loaded {collections.Count} collection(s).");
        }
        catch (Exception ex)
        {
            SetStatus("Unable to load collections.");
            ShowError("Collections", ex);
        }
        finally
        {
            _collectionsLoading = false;
        }
    }

    private async Task LoadCollectionMembersAsync(
        MecmCollection collection)
    {
        string collectionId = collection.CollectionId;

        if (_memberCache.TryGetValue(
            collectionId, out List<MecmCollectionMember>? cachedMembers))
        {
            _memberGrid.ItemsSource = cachedMembers;
            _memberGrid.SelectedItem = null;
            _removeFromCollectionButton.IsEnabled = false;

            UpdateCollectionDetails(collection, cachedMembers.Count);
            return;
        }

        if (!_membersLoading.Add(collectionId))
            return;

        try
        {
            SetStatus($"Loading members of {collection.Name}...");

            List<MecmCollectionMember> members = await Task.Run(() =>
                _collectionService.GetCollectionMembers(
                    collectionId,
                    1000));

            if (_collectionGrid.SelectedItem is not MecmCollection selected ||
                selected.CollectionId != collectionId)
            {
                _memberCache[collectionId] = members;
                return;
            }

            _memberCache[collectionId] = members;
            _memberGrid.ItemsSource = members;
            _memberGrid.SelectedItem = null;
            _removeFromCollectionButton.IsEnabled = false;

            UpdateCollectionDetails(collection, members.Count);

            SetStatus(
                $"Loaded {members.Count} member(s) from {collection.Name}.");
        }
        catch (Exception ex)
        {
            SetStatus("Unable to load collection members.");
            ShowError("Collection Members", ex);
        }
        finally
        {
            _membersLoading.Remove(collectionId);
        }
    }

    private void UpdateCollectionDetails(
        MecmCollection collection,
        int memberCount)
    {
        _collectionDetails.Text =
            $"{collection.Name}\n" +
            $"Collection ID: {collection.CollectionId}\n" +
            $"Members returned: {memberCount}";
    }

    // ---------------------------------------------------------
    // ADD DEVICE TO COLLECTION
    // ---------------------------------------------------------


    private async Task AddSelectedDeviceToCollectionAsync()
    {
        if (_deviceGrid.SelectedItem is not MecmDevice device)
            return;

        try
        {
            SetStatus("Loading device collections...");

            List<MecmCollection> collections = await Task.Run(() =>
                    _collectionService.SearchDeviceCollections());

            if (collections.Count == 0)
            {
                MessageBox.Show(
                    "No device collections were found.",
                    "Add to Collection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                return;
            }

            var picker = new Window
            {
                Title = "Add Device to Collection",
                Width = 480,
                Height = 270,
                MinWidth = 440,
                MinHeight = 250,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Window.GetWindow(this),
                ResizeMode = ResizeMode.NoResize,
                Background = BackgroundBrush,
                Foreground = TextBrush
            };

            var content = new Grid
            {
                Margin = new Thickness(18)
            };

            content.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });

            content.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });

            content.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });

            content.RowDefinitions.Add(
                new RowDefinition { Height = GridLength.Auto });

            // Device information
            var prompt = new TextBlock
            {
                Text = $"Select a collection for {device.Name}:",
                Foreground = TextBrush,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 12)
            };

            Grid.SetRow(prompt, 0);
            content.Children.Add(prompt);

            // Search filter
            var filterBox = new TextBox
            {
                Height = 36,
                Margin = new Thickness(0, 0, 0, 12),
                Background = PanelBrush,
                Foreground = TextBrush,
                BorderBrush = HeaderBorderBrush,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 5, 8, 5)
            };

            // DECLARE THE COMBOBOX HERE, BEFORE ANY EVENT HANDLERS
            ComboBox collectionSelector = CreateCollectionSelector();

            // Create the collection filter
            ICollectionView collectionView =
                CollectionViewSource.GetDefaultView(collections);

            collectionView.Filter = item =>
            {
                if (item is not MecmCollection collection)
                    return false;

                string filter = filterBox.Text.Trim();

                return string.IsNullOrWhiteSpace(filter)
                    || collection.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)
                    || collection.CollectionId.Contains(filter, StringComparison.OrdinalIgnoreCase);
            };

            collectionSelector.ItemsSource = collectionView;
            collectionSelector.SelectedIndex = 0;

            // Attach the event handler AFTER declaration
            filterBox.TextChanged += (_, _) =>
            {
                collectionView.Refresh();

                if (collectionSelector.SelectedItem == null &&
                    collectionSelector.Items.Count > 0)
                {
                    collectionSelector.SelectedIndex = 0;
                }
            };

            Grid.SetRow(filterBox, 1);
            content.Children.Add(filterBox);

            Grid.SetRow(collectionSelector, 2);
            content.Children.Add(collectionSelector);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 18, 0, 0)
            };

            var cancel = CreateButton("Cancel");
            cancel.Background = HeaderBrush;
            cancel.Click += (_, _) => picker.DialogResult = false;

            var add = CreateButton("Add Device");
            add.Margin = new Thickness(8, 0, 0, 0);

            add.Click += (_, _) =>
            {
                if (collectionSelector.SelectedItem is MecmCollection)
                    picker.DialogResult = true;
            };

            buttons.Children.Add(cancel);
            buttons.Children.Add(add);

            Grid.SetRow(buttons, 3);
            content.Children.Add(buttons);

            picker.Content = content;

            if (picker.ShowDialog() != true ||
                collectionSelector.SelectedItem is not MecmCollection selectedCollection)
            {
                SetStatus("Add to collection cancelled.");
                return;
            }

            SetStatus($"Adding {device.Name} to {selectedCollection.Name}...");

            await Task.Run(() =>
                _collectionService.AddDeviceToCollection(
                    selectedCollection.CollectionId,
                    device.ResourceId,
                    device.Name));

            _memberCache.Remove(selectedCollection.CollectionId);

            RecordActivity(
                "Add Device to Collection",
                $"{device.Name} -> {selectedCollection.Name}",
                "Success",
                $"Resource ID: {device.ResourceId}. " +
                "A collection refresh was requested.");

            MessageBox.Show(
                $"{device.Name} was added to {selectedCollection.Name}.\n\n" +
                "MECM has been asked to refresh the collection. " +
                "Membership may take some time to update.",
                "Device Added",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            SetStatus($"Added {device.Name} to {selectedCollection.Name}.");

            if (_collectionGrid.SelectedItem is MecmCollection current &&
                current.CollectionId == selectedCollection.CollectionId)
            {
                await LoadCollectionMembersAsync(current);
            }
        }
        catch (Exception ex)
        {
            RecordActivity(
                "Add Device to Collection",
                device.Name,
                "Failed",
                ex.ToString());

            SetStatus("Unable to add device to collection.");

            if (ex is InvalidOperationException &&
                ex.Message.Contains(
                    "already has a direct membership rule",
                    StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show(
                    ex.Message,
                    "Device Already in Collection",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                ShowError("Add to Collection", ex);
            }
        }
    }


    // ---------------------------------------------------------
    // REMOVE DEVICE FROM COLLECTION
    // ---------------------------------------------------------

    private async Task RemoveSelectedCollectionMemberAsync()
    {
        if (_collectionGrid.SelectedItem is not MecmCollection collection ||
            _memberGrid.SelectedItem is not MecmCollectionMember member)
        {
            return;
        }

        MessageBoxResult confirmation = MessageBox.Show(
            $"Remove {member.Name} from the direct membership rules for " +
            $"'{collection.Name}'?\n\n" +
            "This does not remove the device if it is also included by a query rule.",
            "Confirm Removal",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirmation != MessageBoxResult.Yes)
            return;

        try
        {
            SetStatus($"Removing {member.Name} from {collection.Name}...");

            await Task.Run(() =>
                _collectionService.RemoveDeviceFromCollection(
                    collection.CollectionId,
                    member.ResourceId,
                    member.Name));

            RecordActivity(
                "Remove Device from Collection",
                $"{member.Name} -> {collection.Name}",
                "Success",
                $"Resource ID: {member.ResourceId}. " +
                "A collection refresh was requested.");

            _memberCache.Remove(collection.CollectionId);
            _memberGrid.ItemsSource = null;
            _removeFromCollectionButton.IsEnabled = false;

            await LoadCollectionMembersAsync(collection);

            MessageBox.Show(
                $"{member.Name} was removed from the direct membership rules. " +
                "The collection refresh may take some time.",
                "Device Removed",
                MessageBoxButton.OK,
                MessageBoxImage.Information);

            SetStatus($"Removed {member.Name} from {collection.Name}.");
        }
        catch (Exception ex)
        {
            RecordActivity(
                "Remove Device from Collection",
                $"{member.Name} -> {collection.Name}",
                "Failed",
                ex.ToString());

            SetStatus("Unable to remove device from collection.");
            ShowError("Remove from Collection", ex);
        }
    }


    private static ComboBox CreateCollectionSelector()
    {
        var itemStyle = new Style(typeof(ComboBoxItem));

        itemStyle.Setters.Add(
            new Setter(Control.BackgroundProperty, PanelBrush));

        itemStyle.Setters.Add(
            new Setter(Control.ForegroundProperty, TextBrush));

        itemStyle.Setters.Add(
            new Setter(
                Control.PaddingProperty,
                new Thickness(8, 6, 8, 6)));

        var highlighted = new Trigger
        {
            Property = ComboBoxItem.IsHighlightedProperty,
            Value = true
        };

        highlighted.Setters.Add(
            new Setter(Control.BackgroundProperty, AccentBrush));

        highlighted.Setters.Add(
            new Setter(Control.ForegroundProperty, Brushes.White));

        itemStyle.Triggers.Add(highlighted);

        // Explicitly display the collection Name.
        var itemTemplate = new DataTemplate(typeof(MecmCollection));

        var textFactory = new FrameworkElementFactory(typeof(TextBlock));

        textFactory.SetBinding(
            TextBlock.TextProperty,
            new Binding(nameof(MecmCollection.Name)));

        textFactory.SetValue(
            TextBlock.ForegroundProperty,
            TextBrush);

        textFactory.SetValue(
            TextBlock.TextTrimmingProperty,
            TextTrimming.CharacterEllipsis);

        itemTemplate.VisualTree = textFactory;

        return new ComboBox
        {
            Height = 36,
            MinWidth = 360,
            Background = PanelBrush,
            Foreground = TextBrush,
            BorderBrush = HeaderBorderBrush,
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 5, 8, 5),
            ItemContainerStyle = itemStyle,
            ItemTemplate = itemTemplate,
            HorizontalContentAlignment = HorizontalAlignment.Stretch
        };
    }


    // ---------------------------------------------------------
    // DEPLOYMENTS TAB
    // ---------------------------------------------------------

    private TabItem CreateDeploymentsTab()
    {
        var layout = new Grid
        {
            Margin = new Thickness(8)
        };

        layout.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        var toolbar = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 12)
        };

        var refreshButton = CreateButton("Refresh");
        refreshButton.Click += async (_, _) =>
            await LoadDeploymentsAsync(force: true);

        DockPanel.SetDock(refreshButton, Dock.Right);
        toolbar.Children.Add(refreshButton);

        var searchButton = CreateButton("Search");
        searchButton.Click += async (_, _) =>
            await LoadDeploymentsAsync(force: true);

        DockPanel.SetDock(searchButton, Dock.Right);
        toolbar.Children.Add(searchButton);

        ConfigureSearchBox(
            _deploymentSearchBox,
            "Search deployments");

        _deploymentSearchBox.KeyDown += async (_, e) =>
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                e.Handled = true;
                await LoadDeploymentsAsync(force: true);
            }
        };

        toolbar.Children.Add(_deploymentSearchBox);

        Grid.SetRow(toolbar, 0);
        layout.Children.Add(toolbar);

        AddColumn(_deploymentGrid, "Deployment", "DeploymentName", 200);
        AddColumn(_deploymentGrid, "Deployment ID", "DeploymentId", 130);
        AddColumn(_deploymentGrid, "Collection", "CollectionName", 180);
        AddColumn(_deploymentGrid, "Targeted", "Targeted", 75);
        AddColumn(_deploymentGrid, "Success", "Success", 75);
        AddColumn(_deploymentGrid, "Errors", "Errors", 75);
        AddColumn(_deploymentGrid, "In Progress", "InProgress", 90);
        AddColumn(_deploymentGrid, "Unknown", "Unknown", 75);
        AddColumn(_deploymentGrid, "Intent", "DeploymentIntent", 90);

        Grid.SetRow(_deploymentGrid, 1);
        layout.Children.Add(_deploymentGrid);

        return new TabItem
        {
            Header = "Deployments",
            Content = layout
        };
    }

    private async Task LoadDeploymentsAsync(bool force = false)
    {
        if (_deploymentsLoading || (_deploymentsLoaded && !force))
            return;

        _deploymentsLoading = true;

        try
        {
            string search = _deploymentSearchBox.Text.Trim();

            SetStatus("Loading deployment summaries...");

            List<MecmDeployment> deployments = await Task.Run(() =>
                _deploymentService.SearchDeployments(search, 200));

            _deploymentGrid.ItemsSource = deployments;

            _deploymentsLoaded = true;

            SetStatus($"Loaded {deployments.Count} deployment(s).");
        }
        catch (Exception ex)
        {
            SetStatus("Unable to load deployments.");
            ShowError("Deployments", ex);
        }
        finally
        {
            _deploymentsLoading = false;
        }
    }

    // ---------------------------------------------------------
    // ACTIVITY LOG TAB
    // ---------------------------------------------------------

    private TabItem CreateActivityTab()
    {
        var layout = new Grid
        {
            Margin = new Thickness(8)
        };

        layout.RowDefinitions.Add(
            new RowDefinition { Height = GridLength.Auto });

        layout.RowDefinitions.Add(
            new RowDefinition
            {
                Height = new GridLength(1, GridUnitType.Star)
            });

        var toolbar = new DockPanel
        {
            Margin = new Thickness(0, 0, 0, 12)
        };

        var refreshButton = CreateButton("Refresh Log");
        refreshButton.Click += async (_, _) =>
            await LoadActivityLogAsync(force: true);

        DockPanel.SetDock(refreshButton, Dock.Right);
        toolbar.Children.Add(refreshButton);

        var title = new TextBlock
        {
            Text = "Recent MECM Activity",
            FontSize = 15,
            FontWeight = FontWeights.SemiBold,
            Foreground = TextBrush,
            VerticalAlignment = VerticalAlignment.Center
        };

        toolbar.Children.Add(title);

        Grid.SetRow(toolbar, 0);
        layout.Children.Add(toolbar);

        AddColumn(_activityGrid, "Timestamp", "Timestamp", 150);
        AddColumn(_activityGrid, "Action", "Action", 150);
        AddColumn(_activityGrid, "Target", "Target", 180);
        AddColumn(_activityGrid, "Result", "Result", 100);
        AddColumn(_activityGrid, "Account", "Account", 150);
        AddColumn(_activityGrid, "Details", "Details", 250);

        Grid.SetRow(_activityGrid, 1);
        layout.Children.Add(_activityGrid);

        return new TabItem
        {
            Header = "Activity Log",
            Content = layout
        };
    }

    private async Task LoadActivityLogAsync(bool force = false)
    {
        if (_activityLoading || (_activityLoaded && !force))
            return;

        _activityLoading = true;

        try
        {
            SetStatus("Loading activity log...");

            List<MecmActivityEntry> entries = await Task.Run(() =>
                _activityLogService.GetRecent(200));

            _activityGrid.ItemsSource = entries;

            _activityLoaded = true;

            SetStatus($"Loaded {entries.Count} activity record(s).");
        }
        catch (Exception ex)
        {
            SetStatus("Unable to load activity log.");
            ShowError("Activity Log", ex);
        }
        finally
        {
            _activityLoading = false;
        }
    }

    // ---------------------------------------------------------
    // SHARED HELPERS
    // ---------------------------------------------------------

    private static void ConfigureSearchBox(
        TextBox textBox,
        string tooltip)
    {
        textBox.Height = 34;
        textBox.MinWidth = 250;
        textBox.Margin = new Thickness(0, 0, 8, 0);
        textBox.Padding = new Thickness(8, 5, 8, 5);
        textBox.Background = PanelBrush;
        textBox.Foreground = TextBrush;
        textBox.BorderBrush = MutedBrush;
        textBox.BorderThickness = new Thickness(1);
        textBox.VerticalContentAlignment = VerticalAlignment.Center;
        textBox.ToolTip = tooltip;
    }
    private void RecordActivity(
    string action,
    string target,
    string result,
    string? details = null)
    {
        try
        {
            _activityLogService.Record(
                action,
                target,
                result,
                details,
                _connection.ConnectedUsername);

            // Make the Activity Log reload the next time it is opened.
            _activityLoaded = false;
        }
        catch (Exception ex)
        {
            // Logging must not cause an otherwise successful MECM operation
            // to appear to have failed.
            System.Diagnostics.Debug.WriteLine(
                $"Unable to write MECM activity log: {ex}");
        }
    }
    private void SetStatus(string message)
    {
        _statusText.Text = message;
    }

    private static void ShowError(string title, Exception ex)
    {
        MessageBox.Show(
            $"{ex.Message}\n\n{ex.GetType().Name}",
            title,
            MessageBoxButton.OK,
            MessageBoxImage.Error);
    }

    private static DataGrid CreateGrid()
    {
        var headerStyle = new Style(typeof(DataGridColumnHeader));

        headerStyle.Setters.Add(
            new Setter(Control.BackgroundProperty, HeaderBrush));

        headerStyle.Setters.Add(
            new Setter(Control.ForegroundProperty, TextBrush));

        headerStyle.Setters.Add(
            new Setter(Control.BorderBrushProperty, HeaderBorderBrush));

        headerStyle.Setters.Add(
            new Setter(
                Control.BorderThicknessProperty,
                new Thickness(0, 0, 1, 1)));

        headerStyle.Setters.Add(
            new Setter(
                Control.PaddingProperty,
                new Thickness(8, 5, 8, 5)));

        headerStyle.Setters.Add(
            new Setter(
                Control.FontWeightProperty,
                FontWeights.SemiBold));

        headerStyle.Setters.Add(
            new Setter(
                Control.HorizontalContentAlignmentProperty,
                HorizontalAlignment.Left));

        headerStyle.Setters.Add(
            new Setter(
                Control.VerticalContentAlignmentProperty,
                VerticalAlignment.Center));

        var grid = new DataGrid
        {
            AutoGenerateColumns = false,
            IsReadOnly = true,
            CanUserAddRows = false,
            CanUserDeleteRows = false,

            SelectionMode = DataGridSelectionMode.Single,
            SelectionUnit = DataGridSelectionUnit.FullRow,

            Background = PanelBrush,
            Foreground = TextBrush,

            RowBackground = PanelBrush,
            AlternatingRowBackground = AlternateRowBrush,

            BorderBrush = HeaderBorderBrush,
            BorderThickness = new Thickness(1),

            GridLinesVisibility =
                DataGridGridLinesVisibility.Horizontal,

            HorizontalGridLinesBrush = HeaderBorderBrush,
            VerticalGridLinesBrush = HeaderBorderBrush,

            HeadersVisibility = DataGridHeadersVisibility.Column,

            RowHeight = 34,
            ColumnHeaderHeight = 38,

            ColumnHeaderStyle = headerStyle
        };

        // Explicit dark row styling, including selected and hovered rows.
        var rowStyle = new Style(typeof(DataGridRow));

        rowStyle.Setters.Add(
            new Setter(Control.BackgroundProperty, PanelBrush));

        rowStyle.Setters.Add(
            new Setter(Control.ForegroundProperty, TextBrush));

        rowStyle.Setters.Add(
            new Setter(Control.BorderBrushProperty, HeaderBorderBrush));

        rowStyle.Setters.Add(
            new Setter(
                Control.BorderThicknessProperty,
                new Thickness(0, 0, 0, 1)));

        var hoverTrigger = new Trigger
        {
            Property = DataGridRow.IsMouseOverProperty,
            Value = true
        };

        hoverTrigger.Setters.Add(
            new Setter(Control.BackgroundProperty, HoverRowBrush));

        rowStyle.Triggers.Add(hoverTrigger);

        var selectedTrigger = new Trigger
        {
            Property = DataGridRow.IsSelectedProperty,
            Value = true
        };

        selectedTrigger.Setters.Add(
            new Setter(Control.BackgroundProperty, AccentBrush));

        selectedTrigger.Setters.Add(
            new Setter(Control.ForegroundProperty, Brushes.White));

        rowStyle.Triggers.Add(selectedTrigger);

        grid.RowStyle = rowStyle;

        // Prevent the default WPF cell selection colours.
        var cellStyle = new Style(typeof(DataGridCell));

        cellStyle.Setters.Add(
            new Setter(Control.BackgroundProperty, Brushes.Transparent));

        cellStyle.Setters.Add(
            new Setter(Control.ForegroundProperty, TextBrush));

        cellStyle.Setters.Add(
            new Setter(
                Control.BorderThicknessProperty,
                new Thickness(0)));

        cellStyle.Setters.Add(
            new Setter(
                Control.PaddingProperty,
                new Thickness(4, 0, 4, 0)));

        var cellSelectedTrigger = new Trigger
        {
            Property = DataGridCell.IsSelectedProperty,
            Value = true
        };

        cellSelectedTrigger.Setters.Add(
            new Setter(Control.BackgroundProperty, AccentBrush));

        cellSelectedTrigger.Setters.Add(
            new Setter(Control.ForegroundProperty, Brushes.White));

        cellStyle.Triggers.Add(cellSelectedTrigger);

        grid.CellStyle = cellStyle;

        VirtualizingPanel.SetIsVirtualizing(grid, true);

        VirtualizingPanel.SetVirtualizationMode(
            grid,
            VirtualizationMode.Recycling);

        ScrollViewer.SetCanContentScroll(grid, true);

        return grid;
    }

    private static void AddColumn(
        DataGrid grid,
        string header,
        string property,
        double width)
    {
        var textStyle = new Style(typeof(TextBlock));

        textStyle.Setters.Add(
            new Setter(TextBlock.ForegroundProperty, TextBrush));

        textStyle.Setters.Add(
            new Setter(
                TextBlock.VerticalAlignmentProperty,
                VerticalAlignment.Center));

        textStyle.Setters.Add(
            new Setter(
                TextBlock.MarginProperty,
                new Thickness(6, 0, 6, 0)));

        grid.Columns.Add(new DataGridTextColumn
        {
            Header = header,
            Binding = new Binding(property),
            Width = new DataGridLength(width),
            IsReadOnly = true,
            ElementStyle = textStyle
        });
    }

    private static Grid CreatePanel()
    {
        return new Grid
        {
            Background = PanelBrush,
            Margin = new Thickness(0)
        };
    }

    private static Button CreateButton(string text)
    {
        return new Button
        {
            Content = text,
            Background = AccentBrush,
            Foreground = Brushes.White,
            BorderThickness = new Thickness(0),
            Padding = new Thickness(14, 7, 14, 7),
            Margin = new Thickness(4, 0, 0, 0),
            Cursor = System.Windows.Input.Cursors.Hand,
            MinWidth = 75
        };
    }
}
