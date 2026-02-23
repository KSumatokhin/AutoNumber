using AutoNumber.Models;
using AutoNumber.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace AutoNumber
{
    public partial class NumberingDialog : Window
    {
        public NumberingSettingsViewModel ViewModel { get; private set; }

        public NumberingDialog(NumberingSettingsViewModel viewModel)
        {
            InitializeComponent();
            ViewModel = viewModel;
            DataContext = viewModel;

            // Устанавливаем начальное значение
            UpdateSelectedMode();
        }

        private void RadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (ViewModel == null) return;

            var radio = sender as RadioButton;
            if (radio == null) return;

            if (radio.Name == "rbSelectByWindow")
                ViewModel.SelectedMode = NumberingMode.SelectByWindow;
            else if (radio.Name == "rbAllOnLayouts")
                ViewModel.SelectedMode = NumberingMode.AllOnLayouts;
            else if (radio.Name == "rbByBlockName")
                ViewModel.SelectedMode = NumberingMode.ByBlockName;
        }

        private void UpdateSelectedMode()
        {
            switch (ViewModel.SelectedMode)
            {
                case NumberingMode.SelectByWindow:
                    rbSelectByWindow.IsChecked = true;
                    break;
                case NumberingMode.AllOnLayouts:
                    rbAllOnLayouts.IsChecked = true;
                    break;
                case NumberingMode.ByBlockName:
                    rbByBlockName.IsChecked = true;
                    break;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }

    }
}
