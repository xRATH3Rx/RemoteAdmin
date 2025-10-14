using System.Windows;
using System.Windows.Input;

namespace RemoteAdmin.Server
{
    public partial class InputDialog : Window
    {
        public string ResponseText => txtInput.Text;

        public InputDialog(string title, string prompt, string defaultValue = "")
        {
            InitializeComponent();
            Title = title;
            txtPrompt.Text = prompt;
            txtInput.Text = defaultValue;
            txtInput.SelectAll();
            txtInput.Focus();
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

        private void txtInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                DialogResult = true;
            }
        }
    }
}