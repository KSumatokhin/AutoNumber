using AutoNumber.ViewModels;
using System.Windows;
using System.Windows.Controls;

namespace AutoNumber
{
    public partial class NumberingDialog : Window
    {
        public NumberingDialog(NumberingSettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            StartNumberBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
            IncrementBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();

            var settings = (NumberingSettingsViewModel)DataContext;
            if (string.IsNullOrWhiteSpace(settings.TagName))
            {
                ErrorText.Text = "Укажите тег атрибута.";
                return;
            }

            if (Validation.GetHasError(StartNumberBox) || Validation.GetHasError(IncrementBox))
            {
                ErrorText.Text = "Номер и шаг должны быть целыми числами.";
                return;
            }

            if (settings.Increment == 0)
            {
                ErrorText.Text = "Шаг не может быть равен нулю.";
                return;
            }

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
