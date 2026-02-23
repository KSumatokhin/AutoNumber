using AutoNumber.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace AutoNumber.ViewModels
{
    public class NumberingSettingsViewModel : INotifyPropertyChanged
    {
        private string _tagName = "NUM";
        private int _startNumber = 1;
        private int _increment = 1;
        private string _prefix = "";
        private string _suffix = "";
        private bool _includeModelSpace = false;
        private NumberingMode _selectedMode = NumberingMode.SelectByWindow;
        private string _blockNameFilter = "*";
        private bool _sortByX = true;
        private bool _sortByY = true;
        private SortDirection _xDirection = SortDirection.Ascending;
        private SortDirection _yDirection = SortDirection.Descending;

        private ObservableCollection<string> _previewItems = new ObservableCollection<string>();
        private string _infoText = "Готов к работе";

        public ObservableCollection<string> AvailableTags { get; set; }
        public ObservableCollection<string> AvailableBlocks { get; set; }

        // НОВЫЕ СВОЙСТВА
        public ObservableCollection<string> PreviewItems
        {
            get => _previewItems;
            set { _previewItems = value; OnPropertyChanged(); }
        }

        public string InfoText
        {
            get => _infoText;
            set { _infoText = value; OnPropertyChanged(); }
        }

        public string TagName
        {
            get => _tagName;
            set { _tagName = value; OnPropertyChanged(); }
        }

        public int StartNumber
        {
            get => _startNumber;
            set { _startNumber = value; OnPropertyChanged(); }
        }

        public int Increment
        {
            get => _increment;
            set { _increment = value; OnPropertyChanged(); }
        }

        public string Prefix
        {
            get => _prefix;
            set { _prefix = value; OnPropertyChanged(); }
        }

        public string Suffix
        {
            get => _suffix;
            set { _suffix = value; OnPropertyChanged(); }
        }

        public bool IncludeModelSpace
        {
            get => _includeModelSpace;
            set { _includeModelSpace = value; OnPropertyChanged(); }
        }

        public NumberingMode SelectedMode
        {
            get => _selectedMode;
            set { _selectedMode = value; OnPropertyChanged(); }
        }

        public string BlockNameFilter
        {
            get => _blockNameFilter;
            set { _blockNameFilter = value; OnPropertyChanged(); }
        }

        public bool SortByX
        {
            get => _sortByX;
            set { _sortByX = value; OnPropertyChanged(); }
        }

        public bool SortByY
        {
            get => _sortByY;
            set { _sortByY = value; OnPropertyChanged(); }
        }

        public SortDirection XDirection
        {
            get => _xDirection;
            set { _xDirection = value; OnPropertyChanged(); }
        }

        public SortDirection YDirection
        {
            get => _yDirection;
            set { _yDirection = value; OnPropertyChanged(); }
        }

        public ICommand OkCommand { get; set; }
        public ICommand CancelCommand { get; set; }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string name = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
        }

        public NumberingSettingsViewModel()
        {
            AvailableTags = new ObservableCollection<string>();
            AvailableBlocks = new ObservableCollection<string>();

            OkCommand = new RelayCommand(Ok);
            CancelCommand = new RelayCommand(Cancel);
        }

        private void Ok()
        {
            // Просто закрываем диалог с успехом
        }

        private void Cancel()
        {
            // Просто закрываем диалог с отменой
        }
    }

    // Простая реализация RelayCommand
    public class RelayCommand : ICommand
    {
        private Action<object> _execute;
        private Func<object, bool> _canExecute;

        public RelayCommand(Action<object> execute, Func<object, bool> canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object parameter) => _canExecute?.Invoke(parameter) ?? true;
        public void Execute(object parameter) => _execute(parameter);
    }

}