using Autodesk.Revit.DB.Structure;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Media;

public class ColorStat : INotifyPropertyChanged
{
    private bool _isSelected;
    private double _spacing;
    private RebarBarType _selectedRebarType;

    public string Color { get; set; }
    public SolidColorBrush ColorBrush { get; set; } 

    public List<RebarBarType> RebarTypes { get; set; }

    public double Spacing
    {
        get => _spacing;
        set
        {
            if (_spacing != value)
            {
                _spacing = value;
                OnPropertyChanged(nameof(Spacing));
            }
        }
    }

    public RebarBarType SelectedRebarType
    {
        get => _selectedRebarType;
        set
        {
            if (_selectedRebarType != value)
            {
                _selectedRebarType = value;
                OnPropertyChanged(nameof(SelectedRebarType));
            }
        }
    }

    public bool IsSelected
    {
        get => _isSelected;
        set
        {
            if (_isSelected != value)
            {
                _isSelected = value;
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
