using Autodesk.Revit.DB.Structure;
using System.ComponentModel;
using System.Windows.Media;

namespace SlabReinforcement
{
    public class PathRebarItem : INotifyPropertyChanged
    {
        private Color _clusterColor;
        public Color ClusterColor
        {
            get { return _clusterColor; }
            set
            {
                _clusterColor = value;
                NotifyPropertyChanged(nameof(ClusterColor));
            }
        }

        public double MinX;
        public double MinY;
        public double MaxX;
        public double MaxY;

        private RebarBarType _rebarBarType;
        public RebarBarType RebarBarType
        {
            get { return _rebarBarType; }
            set
            {
                _rebarBarType = value;
                NotifyPropertyChanged(nameof(RebarBarType));
            }
        }

        private double _rebarAnchor;
        public double RebarAnchor
        {
            get { return _rebarAnchor; }
            set
            {
                _rebarAnchor = value;
                NotifyPropertyChanged(nameof(RebarAnchor));
            }
        }
        private double _pathReinSpacing;
        public double PathReinSpacing
        {
            get { return _pathReinSpacing; }
            set
            {
                _pathReinSpacing = value;
                NotifyPropertyChanged(nameof(PathReinSpacing));
            }
        }
        public double PathReinLength; //PATH_REIN_LENGTH_1
        public RebarShape RebarShape; //PATH_REIN_SHAPE_1

        public event PropertyChangedEventHandler PropertyChanged;

        protected void NotifyPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
