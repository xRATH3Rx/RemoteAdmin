using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Server
{
    public partial class StartupManagerWindow : Window
    {
        private readonly ConnectedClient _client;
        private List<StartupItem> _startupItems = new List<StartupItem>();

        public StartupManagerWindow(ConnectedClient client)
        {
            InitializeComponent();
            _client = client;

            this.Title = $"Startup Manager - {client.ComputerName} ({client.Username})";
            this.Loaded += StartupManagerWindow_Loaded;
            this.Closing += StartupManagerWindow_Closing;
        }

        private async void StartupManagerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _client.StartupManagerWindow = this;
            await RefreshStartupItems();
        }

        private void StartupManagerWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_client.StartupManagerWindow == this)
                _client.StartupManagerWindow = null;
        }

        public void UpdateStartupItemsList(List<StartupItem> items)
        {
            _startupItems = items ?? new List<StartupItem>();

            var displayItems = _startupItems.Select(item => new StartupItemDisplay
            {
                Name = item.Name,
                Path = item.Path,
                Type = item.Type,
                TypeDisplay = GetTypeDisplay(item.Type)
            }).ToList();

            var groupedView = CollectionViewSource.GetDefaultView(displayItems);
            groupedView.GroupDescriptions.Clear();
            groupedView.GroupDescriptions.Add(new PropertyGroupDescription("TypeDisplay"));

            lvStartupItems.ItemsSource = groupedView;

            txtStatus.Text = $"Loaded {items.Count} startup item(s)";
        }

        public void HandleOperationResult(StartupItemOperationResponseMessage response)
        {
            if (!response.Success)
            {
                MessageBox.Show($"Operation failed: {response.ErrorMessage}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            _ = RefreshStartupItems();
        }

        private async Task RefreshStartupItems()
        {
            try
            {
                txtStatus.Text = "Loading startup items...";
                btnRefresh.IsEnabled = false;

                var message = new GetStartupItemsMessage();
                await NetworkHelper.SendMessageAsync(_client.Stream, message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh startup items: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error loading items";
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private string GetTypeDisplay(StartupType type)
        {
            return type switch
            {
                StartupType.LocalMachineRun => "HKLM\\...\\Run",
                StartupType.LocalMachineRunOnce => "HKLM\\...\\RunOnce",
                StartupType.CurrentUserRun => "HKCU\\...\\Run",
                StartupType.CurrentUserRunOnce => "HKCU\\...\\RunOnce",
                StartupType.LocalMachineRunX86 => "HKLM\\...\\Run (x86)",
                StartupType.LocalMachineRunOnceX86 => "HKLM\\...\\RunOnce (x86)",
                StartupType.StartMenu => "Start Menu\\Programs\\Startup",
                _ => type.ToString()
            };
        }

        private async void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new StartupItemAddDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    txtStatus.Text = "Adding startup item...";

                    var message = new AddStartupItemMessage
                    {
                        Item = dialog.StartupItem
                    };

                    await NetworkHelper.SendMessageAsync(_client.Stream, message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to add startup item: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtStatus.Text = "Error adding item";
                }
            }
        }

        private async void btnRemove_Click(object sender, RoutedEventArgs e)
        {
            if (lvStartupItems.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more items to remove.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to remove {lvStartupItems.SelectedItems.Count} item(s)?",
                "Confirm Removal",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                txtStatus.Text = "Removing startup item(s)...";

                // FIX: Create a copy of selected items to avoid collection modification during enumeration
                var selectedItemsCopy = lvStartupItems.SelectedItems.Cast<StartupItemDisplay>().ToList();

                foreach (StartupItemDisplay displayItem in selectedItemsCopy)
                {
                    var originalItem = _startupItems.FirstOrDefault(i =>
                        i.Name == displayItem.Name && i.Type == displayItem.Type);

                    if (originalItem != null)
                    {
                        var message = new RemoveStartupItemMessage
                        {
                            Item = originalItem
                        };

                        await NetworkHelper.SendMessageAsync(_client.Stream, message);
                        await Task.Delay(100);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to remove startup items: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error removing items";
            }
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshStartupItems();
        }
    }

    public class StartupItemDisplay
    {
        public string Name { get; set; }
        public string Path { get; set; }
        public StartupType Type { get; set; }
        public string TypeDisplay { get; set; }
    }
}