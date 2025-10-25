using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using Microsoft.Win32;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Server
{
    public partial class TaskSchedulerWindow : Window
    {
        private readonly ConnectedClient _client;
        private List<ScheduledTask> _tasks = new List<ScheduledTask>();
        private List<TaskDisplay> _displayTasks = new List<TaskDisplay>();

        public TaskSchedulerWindow(ConnectedClient client)
        {
            InitializeComponent();
            _client = client;

            this.Title = $"Task Scheduler - {client.ComputerName} ({client.Username})";
            this.Loaded += TaskSchedulerWindow_Loaded;
            this.Closing += TaskSchedulerWindow_Closing;
        }

        private async void TaskSchedulerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            _client.TaskSchedulerWindow = this;
            await RefreshTasks();
        }

        private void TaskSchedulerWindow_Closing(object sender, CancelEventArgs e)
        {
            if (_client.TaskSchedulerWindow == this)
                _client.TaskSchedulerWindow = null;
        }

        public void UpdateTasksList(List<ScheduledTask> tasks)
        {
            _tasks = tasks ?? new List<ScheduledTask>();

            _displayTasks = _tasks.Select(task => new TaskDisplay(task)).ToList();

            ApplyFilter();

            txtTaskCount.Text = $"{_tasks.Count} task(s)";
            txtStatus.Text = $"Loaded {_tasks.Count} scheduled task(s)";
        }

        public void HandleOperationResult(ScheduledTaskOperationResponseMessage response)
        {
            if (!response.Success)
            {
                MessageBox.Show($"Operation failed: {response.ErrorMessage}",
                    $"{response.Operation} Failed",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
            else
            {
                txtStatus.Text = $"{response.Operation} operation completed successfully";
            }

            _ = RefreshTasks();
        }

        private void ApplyFilter()
        {
            var filteredTasks = _displayTasks.AsEnumerable();

            // Apply search filter
            string searchText = txtSearch.Text?.Trim().ToLower();
            if (!string.IsNullOrEmpty(searchText))
            {
                filteredTasks = filteredTasks.Where(t =>
                    t.Name.ToLower().Contains(searchText) ||
                    t.Path.ToLower().Contains(searchText) ||
                    t.Author.ToLower().Contains(searchText));
            }

            var view = CollectionViewSource.GetDefaultView(filteredTasks.ToList());
            lvTasks.ItemsSource = view;
        }

        private async Task RefreshTasks()
        {
            try
            {
                txtStatus.Text = "Loading scheduled tasks...";
                btnRefresh.IsEnabled = false;

                var message = new GetScheduledTasksMessage();
                await NetworkHelper.SendMessageAsync(_client.Stream, message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to refresh tasks: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error loading tasks";
            }
            finally
            {
                btnRefresh.IsEnabled = true;
            }
        }

        private void txtSearch_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            ApplyFilter();
        }

        private void lvTasks_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (lvTasks.SelectedItem is TaskDisplay selectedTask)
            {
                ShowTaskDetails(selectedTask);
            }
            else
            {
                txtDetails.Text = "Select a task to view details";
            }
        }

        private void ShowTaskDetails(TaskDisplay task)
        {
            var originalTask = _tasks.FirstOrDefault(t => t.Path == task.Path);
            if (originalTask == null)
            {
                txtDetails.Text = "Task details not available";
                return;
            }

            var details = new System.Text.StringBuilder();
            details.AppendLine($"Task Name: {originalTask.Name}");
            details.AppendLine($"Path: {originalTask.Path}");
            details.AppendLine($"State: {originalTask.State}");
            details.AppendLine($"Enabled: {originalTask.Enabled}");
            details.AppendLine($"Hidden: {originalTask.Hidden}");
            details.AppendLine($"Run As: {originalTask.RunAsUser}");
            details.AppendLine($"Author: {originalTask.Author}");
            details.AppendLine();

            if (!string.IsNullOrEmpty(originalTask.Description))
            {
                details.AppendLine($"Description:");
                details.AppendLine($"  {originalTask.Description}");
                details.AppendLine();
            }

            details.AppendLine($"Last Run: {(originalTask.LastRunTime.HasValue ? originalTask.LastRunTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Never")}");
            details.AppendLine($"Next Run: {(originalTask.NextRunTime.HasValue ? originalTask.NextRunTime.Value.ToString("yyyy-MM-dd HH:mm:ss") : "Not scheduled")}");
            details.AppendLine($"Last Result: {TaskResultCodes.GetResultDescription(originalTask.LastTaskResult)} (0x{originalTask.LastTaskResult:X})");
            details.AppendLine();

            if (originalTask.Triggers?.Count > 0)
            {
                details.AppendLine("Triggers:");
                foreach (var trigger in originalTask.Triggers)
                {
                    details.AppendLine($"  • {trigger.Type}: {trigger.Details}");
                    details.AppendLine($"    Enabled: {trigger.Enabled}");
                    if (trigger.StartBoundary.HasValue)
                        details.AppendLine($"    Start: {trigger.StartBoundary.Value:yyyy-MM-dd HH:mm:ss}");
                    if (trigger.EndBoundary.HasValue)
                        details.AppendLine($"    End: {trigger.EndBoundary.Value:yyyy-MM-dd HH:mm:ss}");
                }
                details.AppendLine();
            }

            if (originalTask.Actions?.Count > 0)
            {
                details.AppendLine("Actions:");
                foreach (var action in originalTask.Actions)
                {
                    details.AppendLine($"  • {action.Type}");
                    if (!string.IsNullOrEmpty(action.Path))
                        details.AppendLine($"    Program: {action.Path}");
                    if (!string.IsNullOrEmpty(action.Arguments))
                        details.AppendLine($"    Arguments: {action.Arguments}");
                    if (!string.IsNullOrEmpty(action.WorkingDirectory))
                        details.AppendLine($"    Working Directory: {action.WorkingDirectory}");
                }
            }

            txtDetails.Text = details.ToString();
        }

        private async void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new TaskCreateDialog();
            if (dialog.ShowDialog() == true)
            {
                try
                {
                    txtStatus.Text = "Creating scheduled task...";

                    var message = new CreateScheduledTaskMessage
                    {
                        Task = dialog.CreatedTask
                    };

                    await NetworkHelper.SendMessageAsync(_client.Stream, message);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to create task: {ex.Message}",
                        "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    txtStatus.Text = "Error creating task";
                }
            }
        }

        private async void btnRun_Click(object sender, RoutedEventArgs e)
        {
            if (lvTasks.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select a task to run.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedTask = lvTasks.SelectedItem as TaskDisplay;
            if (selectedTask == null) return;

            try
            {
                txtStatus.Text = $"Running task: {selectedTask.Name}...";

                var message = new RunScheduledTaskMessage
                {
                    TaskPath = selectedTask.Path
                };

                await NetworkHelper.SendMessageAsync(_client.Stream, message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to run task: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error running task";
            }
        }

        private async void btnEnable_Click(object sender, RoutedEventArgs e)
        {
            await ToggleSelectedTasks(true);
        }

        private async void btnDisable_Click(object sender, RoutedEventArgs e)
        {
            await ToggleSelectedTasks(false);
        }

        private async Task ToggleSelectedTasks(bool enable)
        {
            if (lvTasks.SelectedItems.Count == 0)
            {
                MessageBox.Show($"Please select one or more tasks to {(enable ? "enable" : "disable")}.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                txtStatus.Text = $"{(enable ? "Enabling" : "Disabling")} task(s)...";

                var selectedTasksCopy = lvTasks.SelectedItems.Cast<TaskDisplay>().ToList();

                foreach (var task in selectedTasksCopy)
                {
                    var message = new ToggleScheduledTaskMessage
                    {
                        TaskPath = task.Path,
                        Enable = enable
                    };

                    await NetworkHelper.SendMessageAsync(_client.Stream, message);
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to {(enable ? "enable" : "disable")} tasks: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = $"Error {(enable ? "enabling" : "disabling")} tasks";
            }
        }

        private async void btnDelete_Click(object sender, RoutedEventArgs e)
        {
            if (lvTasks.SelectedItems.Count == 0)
            {
                MessageBox.Show("Please select one or more tasks to delete.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to delete {lvTasks.SelectedItems.Count} task(s)?",
                "Confirm Deletion",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result != MessageBoxResult.Yes)
                return;

            try
            {
                txtStatus.Text = "Deleting task(s)...";

                var selectedTasksCopy = lvTasks.SelectedItems.Cast<TaskDisplay>().ToList();

                foreach (var task in selectedTasksCopy)
                {
                    var message = new DeleteScheduledTaskMessage
                    {
                        TaskPath = task.Path
                    };

                    await NetworkHelper.SendMessageAsync(_client.Stream, message);
                    await Task.Delay(100);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to delete tasks: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error deleting tasks";
            }
        }

        private async void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (lvTasks.SelectedItem == null)
            {
                MessageBox.Show("Please select a task to export.",
                    "No Selection", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var selectedTask = lvTasks.SelectedItem as TaskDisplay;
            if (selectedTask == null) return;

            try
            {
                txtStatus.Text = $"Exporting task: {selectedTask.Name}...";

                var message = new ExportScheduledTaskMessage
                {
                    TaskPath = selectedTask.Path
                };

                // Store the task path for the export response handler
                _client.PendingTaskExport = selectedTask.Path;

                await NetworkHelper.SendMessageAsync(_client.Stream, message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to export task: {ex.Message}",
                    "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                txtStatus.Text = "Error exporting task";
            }
        }

        public void HandleExportResult(ExportScheduledTaskResponseMessage response)
        {
            if (!response.Success)
            {
                MessageBox.Show($"Failed to export task: {response.ErrorMessage}",
                    "Export Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Show save file dialog
            var saveDialog = new SaveFileDialog
            {
                Filter = "XML files (*.xml)|*.xml|All files (*.*)|*.*",
                FileName = $"{_client.PendingTaskExport?.Replace("\\", "_")}.xml"
            };

            if (saveDialog.ShowDialog() == true)
            {
                try
                {
                    System.IO.File.WriteAllText(saveDialog.FileName, response.TaskXml);
                    MessageBox.Show($"Task exported successfully to:\n{saveDialog.FileName}",
                        "Export Successful", MessageBoxButton.OK, MessageBoxImage.Information);
                    txtStatus.Text = "Task exported successfully";
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save exported task: {ex.Message}",
                        "Save Failed", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }

            _client.PendingTaskExport = null;
        }

        private async void btnRefresh_Click(object sender, RoutedEventArgs e)
        {
            await RefreshTasks();
        }
    }

    public class TaskDisplay
    {
        private readonly ScheduledTask _task;

        public TaskDisplay(ScheduledTask task)
        {
            _task = task;
        }

        public string Name => _task.Name;
        public string Path => _task.Path;
        public TaskState State => _task.State;
        public string Author => _task.Author ?? "Unknown";

        public string StateDisplay => _task.State.ToString();

        public Brush StateColor
        {
            get
            {
                return _task.State switch
                {
                    TaskState.Running => new SolidColorBrush(Color.FromRgb(16, 185, 129)), // Green
                    TaskState.Ready => new SolidColorBrush(Color.FromRgb(59, 130, 246)), // Blue
                    TaskState.Disabled => new SolidColorBrush(Color.FromRgb(156, 163, 175)), // Gray
                    TaskState.Queued => new SolidColorBrush(Color.FromRgb(245, 158, 11)), // Yellow
                    _ => new SolidColorBrush(Color.FromRgb(239, 68, 68)) // Red
                };
            }
        }

        public string NextRunTimeDisplay => _task.NextRunTime.HasValue
            ? _task.NextRunTime.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : "Not scheduled";

        public string LastRunTimeDisplay => _task.LastRunTime.HasValue
            ? _task.LastRunTime.Value.ToString("yyyy-MM-dd HH:mm:ss")
            : "Never";

        public string LastResultDisplay
        {
            get
            {
                if (_task.LastTaskResult == 0)
                    return "Success";
                else if (_task.LastTaskResult == 0x41303)
                    return "Never run";
                else
                    return $"0x{_task.LastTaskResult:X}";
            }
        }
    }
}