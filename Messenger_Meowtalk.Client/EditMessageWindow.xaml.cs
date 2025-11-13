// EditMessageWindow.xaml.cs
using System.Windows;

namespace Messenger_Meowtalk.Client
{
    public partial class EditMessageWindow : Window
    {
        public string EditedText { get; private set; }

        public EditMessageWindow(string currentText)
        {
            InitializeComponent();
            EditTextBox.Text = currentText;
            EditTextBox.Focus();
            EditTextBox.SelectAll();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            EditedText = EditTextBox.Text.Trim();
            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void EditTextBox_KeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key == System.Windows.Input.Key.Enter)
            {
                SaveButton_Click(sender, e);
            }
            else if (e.Key == System.Windows.Input.Key.Escape)
            {
                CancelButton_Click(sender, e);
            }
        }
    }
}