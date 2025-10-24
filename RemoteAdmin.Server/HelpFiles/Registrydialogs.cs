using System;
using System.Windows;
using System.Windows.Controls;
using RemoteAdmin.Shared;
using RemoteAdmin.Shared.Enums;

namespace RemoteAdmin.Server
{
    // Simple input dialog for registry key names
    public class RegistryInputDialog : Window
    {
        public string ResponseText { get; private set; }

        public RegistryInputDialog(string title, string prompt)
        {
            Width = 400;
            Height = 180;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Title = title;
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(30, 30, 30));

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.Margin = new Thickness(15);

            var lblPrompt = new TextBlock
            {
                Text = prompt,
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 10)
            };
            Grid.SetRow(lblPrompt, 0);

            var txtInput = new TextBox
            {
                Height = 30,
                Padding = new Thickness(5),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 15)
            };
            Grid.SetRow(txtInput, 1);

            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Grid.SetRow(btnPanel, 2);

            var btnOK = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            btnOK.Click += (s, e) =>
            {
                ResponseText = txtInput.Text;
                DialogResult = true;
            };

            var btnCancel = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            btnPanel.Children.Add(btnOK);
            btnPanel.Children.Add(btnCancel);

            grid.Children.Add(lblPrompt);
            grid.Children.Add(txtInput);
            grid.Children.Add(btnPanel);

            Content = grid;

            txtInput.Focus();
        }
    }

    // Registry value editor dialog
    public class RegistryValueDialog : Window
    {
        public string ValueName { get; private set; }
        public RegistryValueType ValueType { get; private set; }
        public object ValueData { get; private set; }

        private TextBox txtName;
        private ComboBox cmbType;
        private TextBox txtData;
        private TextBox txtMultiData;
        private bool isEditMode;

        public RegistryValueDialog(RegistryValueInfo existingValue = null)
        {
            Width = 500;
            Height = 350;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            Title = existingValue != null ? "Edit Value" : "New Value";
            Background = new System.Windows.Media.SolidColorBrush(
                System.Windows.Media.Color.FromRgb(30, 30, 30));

            isEditMode = existingValue != null;

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Auto) });
            grid.Margin = new Thickness(15);

            // Value Name
            var lblName = new TextBlock
            {
                Text = "Value Name:",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(lblName, 0);

            txtName = new TextBox
            {
                Height = 30,
                Padding = new Thickness(5),
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 15),
                IsReadOnly = isEditMode
            };
            if (existingValue != null)
            {
                txtName.Text = existingValue.Name == "(Default)" ? "" : existingValue.Name;
            }
            Grid.SetRow(txtName, 0);
            txtName.Margin = new Thickness(0, 25, 0, 15);

            // Value Type
            var lblType = new TextBlock
            {
                Text = "Value Type:",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5)
            };
            Grid.SetRow(lblType, 1);

            cmbType = new ComboBox
            {
                Height = 30,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 15)
            };

            // Add enum values to combo box
            foreach (RegistryValueType valueType in Enum.GetValues(typeof(RegistryValueType)))
            {
                cmbType.Items.Add(valueType);
            }
            cmbType.SelectedIndex = 0;

            if (existingValue != null)
            {
                cmbType.SelectedItem = existingValue.ValueType;
            }

            cmbType.SelectionChanged += (s, e) => UpdateDataFields();
            Grid.SetRow(cmbType, 1);
            cmbType.Margin = new Thickness(0, 25, 0, 15);

            // Value Data
            var dataPanel = new Grid();
            Grid.SetRow(dataPanel, 2);

            var lblData = new TextBlock
            {
                Text = "Value Data:",
                Foreground = System.Windows.Media.Brushes.White,
                FontSize = 13,
                Margin = new Thickness(0, 0, 0, 5)
            };

            txtData = new TextBox
            {
                Height = 30,
                Padding = new Thickness(5),
                FontSize = 13,
                VerticalAlignment = VerticalAlignment.Top,
                Margin = new Thickness(0, 25, 0, 0)
            };

            txtMultiData = new TextBox
            {
                Padding = new Thickness(5),
                FontSize = 13,
                AcceptsReturn = true,
                TextWrapping = TextWrapping.Wrap,
                VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                VerticalAlignment = VerticalAlignment.Stretch,
                Margin = new Thickness(0, 25, 0, 0),
                Visibility = Visibility.Collapsed
            };

            if (existingValue != null && existingValue.Data != null)
            {
                txtData.Text = existingValue.Data.ToString();
                txtMultiData.Text = existingValue.Data.ToString();
            }

            dataPanel.Children.Add(lblData);
            dataPanel.Children.Add(txtData);
            dataPanel.Children.Add(txtMultiData);

            // Buttons
            var btnPanel = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 15, 0, 0)
            };
            Grid.SetRow(btnPanel, 3);

            var btnOK = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 30,
                Margin = new Thickness(0, 0, 10, 0),
                IsDefault = true
            };
            btnOK.Click += BtnOK_Click;

            var btnCancel = new Button
            {
                Content = "Cancel",
                Width = 80,
                Height = 30,
                IsCancel = true
            };

            btnPanel.Children.Add(btnOK);
            btnPanel.Children.Add(btnCancel);

            grid.Children.Add(lblName);
            grid.Children.Add(txtName);
            grid.Children.Add(lblType);
            grid.Children.Add(cmbType);
            grid.Children.Add(dataPanel);
            grid.Children.Add(btnPanel);

            Content = grid;

            UpdateDataFields();
            txtName.Focus();
        }

        private void UpdateDataFields()
        {
            if (cmbType.SelectedItem == null) return;

            RegistryValueType selectedType = (RegistryValueType)cmbType.SelectedItem;

            if (selectedType == RegistryValueType.MultiString)
            {
                txtData.Visibility = Visibility.Collapsed;
                txtMultiData.Visibility = Visibility.Visible;
            }
            else
            {
                txtData.Visibility = Visibility.Visible;
                txtMultiData.Visibility = Visibility.Collapsed;
            }
        }

        private void BtnOK_Click(object sender, RoutedEventArgs e)
        {
            ValueName = txtName.Text;
            ValueType = (RegistryValueType)cmbType.SelectedItem;

            // Get data based on type
            if (ValueType == RegistryValueType.MultiString)
            {
                ValueData = txtMultiData.Text;
            }
            else if (ValueType == RegistryValueType.DWord || ValueType == RegistryValueType.QWord)
            {
                // Validate numeric input
                string dataText = txtData.Text.Trim();

                // Support hex input (0x prefix)
                if (dataText.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        if (ValueType == RegistryValueType.DWord)
                            ValueData = Convert.ToInt32(dataText.Substring(2), 16);
                        else
                            ValueData = Convert.ToInt64(dataText.Substring(2), 16);
                    }
                    catch
                    {
                        MessageBox.Show("Invalid hexadecimal value.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
                else
                {
                    try
                    {
                        if (ValueType == RegistryValueType.DWord)
                            ValueData = Convert.ToInt32(dataText);
                        else
                            ValueData = Convert.ToInt64(dataText);
                    }
                    catch
                    {
                        MessageBox.Show($"Invalid numeric value for {ValueType}.", "Error",
                            MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }
            }
            else
            {
                ValueData = txtData.Text;
            }

            DialogResult = true;
        }
    }
}