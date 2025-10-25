using System;
using System.Collections.Generic;
using System.Windows;
using Microsoft.Win32;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Server
{
    public partial class TaskCreateDialog : Window
    {
        public ScheduledTask CreatedTask { get; private set; }

        public TaskCreateDialog()
        {
            InitializeComponent();
            Loaded += (_, __) =>
            {
                // Guard in case XAML name differs or control missing
                if (dpStartDate != null)
                    dpStartDate.SelectedDate = DateTime.Now.Date;
            };
        }

        private void cmbTriggerType_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (cmbTriggerType?.SelectedItem is System.Windows.Controls.ComboBoxItem selectedItem)
            {
                string triggerType = selectedItem.Tag as string;
                if (panelTriggerDetails != null)
                    panelTriggerDetails.Visibility = (triggerType == "Boot" || triggerType == "Logon")
                        ? Visibility.Collapsed
                        : Visibility.Visible;
            }
        }

        private void btnBrowse_Click(object sender, RoutedEventArgs e)
        {
            var openDialog = new OpenFileDialog
            {
                Filter = "Executable files (*.exe)|*.exe|Batch files (*.bat)|*.bat|Command files (*.cmd)|*.cmd|All files (*.*)|*.*",
                Title = "Select Program or Script"
            };

            if (openDialog.ShowDialog() == true)
            {
                txtProgramPath.Text = openDialog.FileName;
            }
        }

        private void btnCreate_Click(object sender, RoutedEventArgs e)
        {
            // Validate inputs
            if (string.IsNullOrWhiteSpace(txtName.Text))
            {
                MessageBox.Show("Please enter a task name.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtName.Focus();
                return;
            }

            if (string.IsNullOrWhiteSpace(txtProgramPath.Text))
            {
                MessageBox.Show("Please enter a program or script path.", "Validation Error",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                txtProgramPath.Focus();
                return;
            }

            try
            {
                CreatedTask = new ScheduledTask
                {
                    Name = txtName.Text.Trim(),
                    Description = txtDescription.Text.Trim(),
                    Enabled = chkEnabled.IsChecked == true,
                    Hidden = chkHidden.IsChecked == true,
                    Author = Environment.UserName,
                    RunAsUser = txtRunAsUser.Text.Trim(),
                    RunWithHighest = chkRunWithHighest.IsChecked == true,
                    RunOnlyWhenLoggedOn = rbLoggedOn.IsChecked == true,
                    Triggers = new List<TaskTrigger>(),
                    Actions = new List<Shared.TaskAction>()
                };

                // Add trigger
                var trigger = CreateTrigger();
                if (trigger != null)
                {
                    CreatedTask.Triggers.Add(trigger);
                }

                // Add action
                CreatedTask.Actions.Add(new Shared.TaskAction
                {
                    Type = ActionType.Execute,
                    Path = txtProgramPath.Text.Trim(),
                    Arguments = txtArguments.Text.Trim(),
                    WorkingDirectory = txtWorkingDirectory.Text.Trim()
                });

                this.DialogResult = true;
                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to create task: {ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private TaskTrigger CreateTrigger()
        {
            if (cmbTriggerType.SelectedItem is not System.Windows.Controls.ComboBoxItem selectedItem)
                return null;

            string triggerTypeStr = selectedItem.Tag?.ToString();
            TriggerType triggerType = triggerTypeStr switch
            {
                "Daily" => TriggerType.Daily,
                "Weekly" => TriggerType.Weekly,
                "Boot" => TriggerType.Boot,
                "Logon" => TriggerType.Logon,
                "Time" => TriggerType.Time,
                _ => TriggerType.Time
            };

            var trigger = new TaskTrigger
            {
                Type = triggerType,
                Enabled = true,
                Details = GetTriggerDetails(triggerType)
            };

            // Set start boundary for time-based triggers
            if (triggerType != TriggerType.Boot && triggerType != TriggerType.Logon)
            {
                if (dpStartDate.SelectedDate.HasValue)
                {
                    int hour = 12;
                    int minute = 0;

                    if (int.TryParse(txtHour.Text, out int parsedHour))
                        hour = Math.Max(0, Math.Min(23, parsedHour));

                    if (int.TryParse(txtMinute.Text, out int parsedMinute))
                        minute = Math.Max(0, Math.Min(59, parsedMinute));

                    trigger.StartBoundary = dpStartDate.SelectedDate.Value
                        .AddHours(hour)
                        .AddMinutes(minute);
                }
            }

            return trigger;
        }

        private string GetTriggerDetails(TriggerType type)
        {
            return type switch
            {
                TriggerType.Daily => "Runs daily",
                TriggerType.Weekly => "Runs weekly",
                TriggerType.Boot => "Runs at system startup",
                TriggerType.Logon => "Runs at user logon",
                TriggerType.Time => "Runs once",
                _ => "Custom trigger"
            };
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
            this.Close();
        }
    }
}