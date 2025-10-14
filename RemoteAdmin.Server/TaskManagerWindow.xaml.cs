using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using RemoteAdmin.Shared;

namespace RemoteAdmin.Server
{
    public partial class TaskManagerWindow : Window
    {
        private ConnectedClient client;
        private ObservableCollection<ProcessInfo> processes;
        private ObservableCollection<ServiceInfo> services;

        public TaskManagerWindow(ConnectedClient client)
        {
            InitializeComponent();
            this.client = client;

            processes = new ObservableCollection<ProcessInfo>();
            services = new ObservableCollection<ServiceInfo>();

            ProcessesGrid.ItemsSource = processes;
            ServicesGrid.ItemsSource = services;

            txtClientInfo.Text = $"Task Manager: {client.ComputerName} ({client.Username})";

            // IMPORTANT: Set the reference BEFORE loading data
            client.TaskManagerWindow = this;

            // Load data after window is shown
            this.Loaded += async (s, e) =>
            {
                await RefreshData();
            };
        }

        private async System.Threading.Tasks.Task RefreshData()
        {
            try
            {
                
                UpdateStatus("Loading...", false);

                

                // Request process list
                await NetworkHelper.SendMessageAsync(client.Stream, new ProcessListRequestMessage());
                

                // Request service list
                await NetworkHelper.SendMessageAsync(client.Stream, new ServiceListRequestMessage());
                

                UpdateStatus("Ready", false);
            }
            catch (Exception ex)
            {
                
                UpdateStatus($"Error: {ex.Message}", true);
            }
        }

        public void UpdateProcessList(System.Collections.Generic.List<ProcessInfo> processList)
        {
            Dispatcher.Invoke(() =>
            {
                
                processes.Clear();
                foreach (var process in processList.OrderBy(p => p.Name))
                {
                    processes.Add(process);
                }
            });
        }

        public void UpdateServiceList(System.Collections.Generic.List<ServiceInfo> serviceList)
        {
            Dispatcher.Invoke(() =>
            {
                services.Clear();
                foreach (var service in serviceList.OrderBy(s => s.DisplayName))
                {
                    services.Add(service);
                }
            });
        }

        public void UpdateStatus(string status, bool isError)
        {
            Dispatcher.Invoke(() =>
            {
                txtStatus.Text = $"Status: {status}";
                txtStatus.Foreground = isError ?
                    System.Windows.Media.Brushes.Red :
                    System.Windows.Media.Brushes.LimeGreen;
            });
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshData();
        }

        private async void KillProcess_Click(object sender, RoutedEventArgs e)
        {
            var selectedProcess = ProcessesGrid.SelectedItem as ProcessInfo;
            if (selectedProcess != null)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to kill process '{selectedProcess.Name}' (PID: {selectedProcess.Id})?",
                    "Confirm Kill Process",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var killMsg = new KillProcessMessage
                        {
                            ProcessId = selectedProcess.Id,
                            ProcessName = selectedProcess.Name
                        };

                        await NetworkHelper.SendMessageAsync(client.Stream, killMsg);
                        UpdateStatus($"Killing process {selectedProcess.Name}...", false);

                        // Refresh after a moment
                        await System.Threading.Tasks.Task.Delay(1000);
                        await RefreshData();
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error: {ex.Message}", true);
                        MessageBox.Show($"Error killing process: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private async void StartService_Click(object sender, RoutedEventArgs e)
        {
            await ControlService("Start");
        }

        private async void StopService_Click(object sender, RoutedEventArgs e)
        {
            await ControlService("Stop");
        }

        private async void RestartService_Click(object sender, RoutedEventArgs e)
        {
            await ControlService("Restart");
        }

        private async System.Threading.Tasks.Task ControlService(string action)
        {
            var selectedService = ServicesGrid.SelectedItem as ServiceInfo;
            if (selectedService != null)
            {
                var result = MessageBox.Show(
                    $"Are you sure you want to {action.ToLower()} service '{selectedService.DisplayName}'?",
                    $"Confirm {action} Service",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    try
                    {
                        var controlMsg = new ServiceControlMessage
                        {
                            ServiceName = selectedService.Name,
                            Action = action
                        };

                        await NetworkHelper.SendMessageAsync(client.Stream, controlMsg);
                        UpdateStatus($"{action}ing service {selectedService.DisplayName}...", false);

                        // Refresh after a moment
                        await System.Threading.Tasks.Task.Delay(2000);
                        await RefreshData();
                    }
                    catch (Exception ex)
                    {
                        UpdateStatus($"Error: {ex.Message}", true);
                        MessageBox.Show($"Error controlling service: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            client.TaskManagerWindow = null;
        }
    }
}