using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Server
{
    public partial class RegistryEditorWindow : Window
    {
        private readonly ConnectedClient client;
        private ObservableCollection<RegistryTreeNode> rootNodes;
        private string currentPath = "";
        private List<string> navigationHistory = new List<string>();
        private int historyIndex = -1;

        public RegistryEditorWindow(ConnectedClient connectedClient)
        {
            InitializeComponent();

            client = connectedClient;
            client.RegistryEditorWindow = this; // Register this window

            txtComputerName.Text = $"Computer: {client.ComputerName} ({client.IPAddress})";

            // Initialize root nodes
            InitializeRootNodes();

            // Request initial data
            var message = new OpenRegistryEditorMessage();
            SendMessageToClient(message);
        }

        private void InitializeRootNodes()
        {
            rootNodes = new ObservableCollection<RegistryTreeNode>
            {
                new RegistryTreeNode("HKEY_CLASSES_ROOT", "HKEY_CLASSES_ROOT"),
                new RegistryTreeNode("HKEY_CURRENT_USER", "HKEY_CURRENT_USER"),
                new RegistryTreeNode("HKEY_LOCAL_MACHINE", "HKEY_LOCAL_MACHINE"),
                new RegistryTreeNode("HKEY_USERS", "HKEY_USERS"),
                new RegistryTreeNode("HKEY_CURRENT_CONFIG", "HKEY_CURRENT_CONFIG")
            };

            // Add dummy child to show expander
            foreach (var node in rootNodes)
            {
                node.Children.Add(new RegistryTreeNode("Loading...", node.FullPath + "\\Loading"));
            }

            treeRegistry.ItemsSource = rootNodes;
        }

        // PUBLIC METHODS called from ClientHandler
        public void UpdateRegistryData(RegistryDataMessage message)
        {
            // Hide loading overlay
            ShowLoading(false);

            if (!message.Success)
            {
                ShowError(message.ErrorMessage);
                return;
            }

            currentPath = message.CurrentPath;
            txtCurrentPath.Text = string.IsNullOrEmpty(currentPath) ? "Computer" : currentPath;

            // Update values grid
            dgValues.ItemsSource = message.Values;

            // Update tree if this is a new path
            if (!string.IsNullOrEmpty(currentPath))
            {
                UpdateTreeNode(currentPath, message.SubKeys);
            }

            txtStatus.Text = $"Loaded {message.SubKeys.Count} keys, {message.Values.Count} values";
        }

        public void HandleOperationResult(RegistryOperationResultMessage message)
        {
            if (message.Success)
            {
                txtStatus.Text = $"{message.Operation} completed successfully";

                // Refresh current view
                if (!string.IsNullOrEmpty(currentPath))
                {
                    RequestEnumerate(currentPath);
                }
            }
            else
            {
                ShowError($"{message.Operation} failed: {message.ErrorMessage}");
            }
        }

        // Helper to send messages to client
        private async Task SendMessageToClientAsync(Message message)
        {
            try
            {
                if (client.Stream != null)
                {
                    await NetworkHelper.SendMessageAsync(client.Stream, message);
                }
            }
            catch (Exception ex)
            {
                ShowError($"Failed to send message: {ex.Message}");
            }
        }

        // Synchronous wrapper for places that can't use async
        private void SendMessageToClient(Message message)
        {
            _ = SendMessageToClientAsync(message);
        }

        private void UpdateTreeNode(string path, List<RegistryKeyInfo> subKeys)
        {
            RegistryTreeNode node = FindNodeByPath(path);
            if (node == null)
                return;

            node.Children.Clear();

            foreach (var subKey in subKeys)
            {
                var childNode = new RegistryTreeNode(subKey.Name, $"{path}\\{subKey.Name}");

                // Add dummy child if this key has subkeys
                if (subKey.SubKeyCount > 0 || subKey.SubKeyCount == -1)
                {
                    childNode.Children.Add(new RegistryTreeNode("Loading...", childNode.FullPath + "\\Loading"));
                }

                node.Children.Add(childNode);
            }

            node.IsExpanded = true;
        }

        private RegistryTreeNode FindNodeByPath(string path)
        {
            if (string.IsNullOrEmpty(path))
                return null;

            string[] parts = path.Split('\\');

            // Find root
            var root = rootNodes.FirstOrDefault(n => n.Name == parts[0]);
            if (root == null || parts.Length == 1)
                return root;

            // Navigate to target node
            RegistryTreeNode current = root;
            for (int i = 1; i < parts.Length; i++)
            {
                current = current.Children.FirstOrDefault(n => n.Name == parts[i]);
                if (current == null)
                    return null;
            }

            return current;
        }

        private async void treeRegistry_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is RegistryTreeNode node)
            {
                // Show loading overlay
                ShowLoading(true);

                try
                {
                    // Check if children need to be loaded
                    if (node.Children.Count == 1 && node.Children[0].Name == "Loading...")
                    {
                        await RequestEnumerateAsync(node.FullPath);
                    }
                    else if (node.Children.Count > 0)
                    {
                        // Just update the current path and values
                        await RequestEnumerateAsync(node.FullPath);
                    }

                    AddToHistory(node.FullPath);
                }
                finally
                {
                    // Hide loading overlay - but wait a tiny bit for smooth UX
                    await Task.Delay(100);
                    ShowLoading(false);
                }
            }
        }

        private void ShowLoading(bool show)
        {
            loadingOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private async Task RequestEnumerateAsync(string path)
        {
            var message = new RegistryEnumerateMessage
            {
                KeyPath = path
            };

            txtStatus.Text = "Loading...";

            // Send message in background thread
            await Task.Run(() => SendMessageToClient(message));

            // Set a timeout in case the response never comes
            _ = Task.Run(async () =>
            {
                await Task.Delay(10000); // 10 second timeout
                Dispatcher.Invoke(() =>
                {
                    if (loadingOverlay.Visibility == Visibility.Visible)
                    {
                        ShowLoading(false);
                        txtStatus.Text = "Request timed out - registry key may be too large or access denied";
                    }
                });
            });
        }

        private void RequestEnumerate(string path)
        {
            _ = RequestEnumerateAsync(path);
        }

        private async void btnBack_Click(object sender, RoutedEventArgs e)
        {
            if (historyIndex > 0)
            {
                historyIndex--;
                string path = navigationHistory[historyIndex];
                await RequestEnumerateAsync(path);
                SelectNodeByPath(path);
            }
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(currentPath))
            {
                await RequestEnumerateAsync(currentPath);
            }
        }

        private async void btnNewKey_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPath))
            {
                MessageBox.Show("Please select a registry key first.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new RegistryInputDialog("New Key", "Enter key name:");
            if (dialog.ShowDialog() == true)
            {
                string keyName = dialog.ResponseText;
                if (!string.IsNullOrWhiteSpace(keyName))
                {
                    var message = new RegistryCreateKeyMessage
                    {
                        ParentPath = currentPath,
                        KeyName = keyName
                    };

                    await SendMessageToClientAsync(message);
                    txtStatus.Text = "Creating key...";
                }
            }
        }

        private async void btnNewValue_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(currentPath))
            {
                MessageBox.Show("Please select a registry key first.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var dialog = new RegistryValueDialog();
            if (dialog.ShowDialog() == true)
            {
                var message = new RegistrySetValueMessage
                {
                    KeyPath = currentPath,
                    ValueName = dialog.ValueName,
                    ValueType = dialog.ValueType,
                    ValueData = dialog.ValueData
                };

                await SendMessageToClientAsync(message);
                txtStatus.Text = "Creating value...";
            }
        }

        private async void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (dgValues.SelectedItem is RegistryValueInfo selectedValue)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete value '{selectedValue.Name}'?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var message = new RegistryDeleteValueMessage
                    {
                        KeyPath = currentPath,
                        ValueName = selectedValue.Name == "(Default)" ? "" : selectedValue.Name
                    };

                    await SendMessageToClientAsync(message);
                    txtStatus.Text = "Deleting value...";
                }
            }
            else if (treeRegistry.SelectedItem is RegistryTreeNode selectedKey &&
                     !string.IsNullOrEmpty(selectedKey.FullPath))
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to delete key '{selectedKey.Name}' and all its subkeys?",
                    "Confirm Delete",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    var message = new RegistryDeleteKeyMessage
                    {
                        KeyPath = selectedKey.FullPath
                    };

                    await SendMessageToClientAsync(message);
                    txtStatus.Text = "Deleting key...";
                }
            }
            else
            {
                MessageBox.Show("Please select a key or value to delete.", "Information",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private async void dgValues_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (dgValues.SelectedItem is RegistryValueInfo selectedValue)
            {
                var dialog = new RegistryValueDialog(selectedValue);
                if (dialog.ShowDialog() == true)
                {
                    var message = new RegistrySetValueMessage
                    {
                        KeyPath = currentPath,
                        ValueName = dialog.ValueName,
                        ValueType = dialog.ValueType,
                        ValueData = dialog.ValueData
                    };

                    await SendMessageToClientAsync(message);
                    txtStatus.Text = "Updating value...";
                }
            }
        }

        private void AddToHistory(string path)
        {
            // Remove any forward history
            if (historyIndex < navigationHistory.Count - 1)
            {
                navigationHistory.RemoveRange(historyIndex + 1, navigationHistory.Count - historyIndex - 1);
            }

            navigationHistory.Add(path);
            historyIndex = navigationHistory.Count - 1;

            btnBack.IsEnabled = historyIndex > 0;
        }

        private void SelectNodeByPath(string path)
        {
            var node = FindNodeByPath(path);
            if (node != null)
            {
                node.IsSelected = true;
            }
        }

        private void ShowError(string message)
        {
            txtStatus.Text = $"Error: {message}";
            MessageBox.Show(message, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    // TreeNode class for registry keys
    public class RegistryTreeNode : INotifyPropertyChanged
    {
        private bool isExpanded;
        private bool isSelected;

        public string Name { get; set; }
        public string FullPath { get; set; }
        public ObservableCollection<RegistryTreeNode> Children { get; set; }

        public bool IsExpanded
        {
            get => isExpanded;
            set
            {
                isExpanded = value;
                OnPropertyChanged(nameof(IsExpanded));
            }
        }

        public bool IsSelected
        {
            get => isSelected;
            set
            {
                isSelected = value;
                OnPropertyChanged(nameof(IsSelected));
            }
        }

        public RegistryTreeNode(string name, string fullPath)
        {
            Name = name;
            FullPath = fullPath;
            Children = new ObservableCollection<RegistryTreeNode>();
        }

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
