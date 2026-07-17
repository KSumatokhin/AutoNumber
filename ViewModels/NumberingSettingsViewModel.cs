using AutoNumber.Models;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AutoNumber.ViewModels
{
    public class NumberingSettingsViewModel : INotifyPropertyChanged
    {
        private string _tagName = "NUM";
        private int _startNumber = 1;
        private int _increment = 1;
        private string _prefix = string.Empty;
        private string _suffix = string.Empty;
        private bool _includeModelSpace;
        private NumberingScope _scope = NumberingScope.AllLayouts;

        public string BlockName { get; set; }
        public ObservableCollection<string> AvailableTags { get; set; }
        public bool CreateDrawingSchedule { get; set; }

        public string TagName
        {
            get { return _tagName; }
            set { _tagName = value; OnPropertyChanged(); }
        }

        public int StartNumber
        {
            get { return _startNumber; }
            set { _startNumber = value; OnPropertyChanged(); }
        }

        public int Increment
        {
            get { return _increment; }
            set { _increment = value; OnPropertyChanged(); }
        }

        public string Prefix
        {
            get { return _prefix; }
            set { _prefix = value; OnPropertyChanged(); }
        }

        public string Suffix
        {
            get { return _suffix; }
            set { _suffix = value; OnPropertyChanged(); }
        }

        public bool IncludeModelSpace
        {
            get { return _includeModelSpace; }
            set { _includeModelSpace = value; OnPropertyChanged(); }
        }

        public NumberingScope Scope
        {
            get { return _scope; }
            set
            {
                _scope = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsAllLayouts));
                OnPropertyChanged(nameof(IsSelectedObjects));
            }
        }

        public bool IsAllLayouts
        {
            get { return Scope == NumberingScope.AllLayouts; }
            set { if (value) Scope = NumberingScope.AllLayouts; }
        }

        public bool IsSelectedObjects
        {
            get { return Scope == NumberingScope.SelectedObjects; }
            set { if (value) Scope = NumberingScope.SelectedObjects; }
        }

        public NumberingSettingsViewModel()
        {
            AvailableTags = new ObservableCollection<string>();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
