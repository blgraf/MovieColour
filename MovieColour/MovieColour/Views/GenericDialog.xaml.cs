using MahApps.Metro.Controls;
using System.Windows;

namespace MovieColour.Views
{
    public partial class GenericDialog : MetroWindow
    {
        public GenericDialog(string message, string title = null)
        {
            InitializeComponent();
            MessageTextBlock.Text = message;
            this.Title = title ?? Strings.Error;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
            this.Close();
        }
    }
}
