using System.ComponentModel;
using System.Windows.Media;

namespace SlabReinforcement
{
    class ColorItem : INotifyPropertyChanged
    {
        private bool isSelected;

        public string Description { get; set; }
        public SolidColorBrush Color { get; set; }

        public bool IsSelected
        {
            get { return isSelected; }
            set
            {
                if (isSelected != value)
                {
                    isSelected = value;
                    OnPropertyChanged(nameof(IsSelected));
                }
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
