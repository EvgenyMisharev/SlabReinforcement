using Dbscan.RBush;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SlabReinforcement
{
    public partial class SlabReinforcementWPF : Window
    {
        IList<PointColorItem> PointColorItemList;
        public SlabReinforcementWPF(IList<PointColorItem> pointColorItemList)
        {
            PointColorItemList = pointColorItemList;
            InitializeComponent();

            List<Color> colorList = new List<Color>();
            // Отобразите точки на холсте
            foreach (var pointItem in PointColorItemList)
            {
                Color wpfColor = Color.FromRgb(pointItem.Color.Red, pointItem.Color.Green, pointItem.Color.Blue);
                if (!colorList.Contains(wpfColor))
                {
                    colorList.Add(wpfColor);
                }
                double centerX = pointItem.Point.X; 
                double centerY = pointItem.Point.Y; 
                double radiusX = 0.5; 
                double radiusY = 0.5; 

                Ellipse ellipse = new Ellipse
                {
                    Width = radiusX * 2,
                    Height = radiusY * 2,
                    Fill = new SolidColorBrush(wpfColor),
                };

                Canvas.SetLeft(ellipse, centerX - radiusX);
                Canvas.SetTop(ellipse, centerY - radiusY);

                pointCanvas.Children.Add(ellipse);
            }

            List<ColorItem> colorItemsList = new List<ColorItem>();
            foreach (Color color in colorList)
            {
                colorItemsList.Add(new ColorItem { Description = $"R{color.R}, G{color.G}, B{color.B}", Color = new SolidColorBrush(color) });
            }

            colorListBox.ItemsSource = colorItemsList;
        }

        private void CheckBox_Checked(object sender, RoutedEventArgs e)
        {
            pointCanvas.Children.Clear();
            List<ColorItem> selectedColorsForIgnor = colorListBox.Items.Cast<ColorItem>().Where(ci => ci.IsSelected == true).ToList();
            foreach (var pointItem in PointColorItemList)
            {
                ColorItem findColorItem = selectedColorsForIgnor.FirstOrDefault(si => si.Color.Color.R.ToString() == pointItem.Color.Red.ToString()
                && si.Color.Color.G.ToString() == pointItem.Color.Green.ToString()
                && si.Color.Color.B.ToString() == pointItem.Color.Blue.ToString());
                if (findColorItem == null)
                {
                    Color wpfColor = Color.FromRgb(pointItem.Color.Red, pointItem.Color.Green, pointItem.Color.Blue);
                    double centerX = pointItem.Point.X;
                    double centerY = pointItem.Point.Y;
                    double radiusX = 0.5;
                    double radiusY = 0.5;

                    Ellipse ellipse = new Ellipse
                    {
                        Width = radiusX * 2,
                        Height = radiusY * 2,
                        Fill = new SolidColorBrush(wpfColor),
                    };

                    Canvas.SetLeft(ellipse, centerX - radiusX);
                    Canvas.SetTop(ellipse, centerY - radiusY);

                    pointCanvas.Children.Add(ellipse);
                }
            }
        }
        private void button_Click(object sender, RoutedEventArgs e)
        {
            //pointCanvas.Children.Clear();
            List<PointColorItem> pointColorItemFilteredList = new List<PointColorItem>();
            List<ColorItem> selectedColorsForIgnor = colorListBox.Items.Cast<ColorItem>().Where(ci => ci.IsSelected == true).ToList();
            foreach (PointColorItem pointItem in PointColorItemList)
            {
                ColorItem findColorItem = selectedColorsForIgnor.FirstOrDefault(si => si.Color.Color.R.ToString() == pointItem.Color.Red.ToString()
                && si.Color.Color.G.ToString() == pointItem.Color.Green.ToString()
                && si.Color.Color.B.ToString() == pointItem.Color.Blue.ToString());
                if (findColorItem == null)
                {
                    pointColorItemFilteredList.Add(pointItem);
                }
            }

            IList<SimplePoint> data = pointColorItemFilteredList.Select(point => new SimplePoint(point.Point.X, point.Point.Y)).ToList();

            var clusters = DbscanRBush.CalculateClusters(
               data,
               epsilon: 800 / 304.8,
               minimumPointsPerCluster: 6);

            // Создайте экземпляр класса Random
            Random random = new Random();
            for (int i = 0; i < clusters.Clusters.Count; i++)
            {
                Dbscan.Cluster<SimplePoint> cluster = clusters.Clusters[i];
                if (cluster.Objects.Count > 4)
                {
                    byte red = (byte)random.Next(256);
                    byte green = (byte)random.Next(256);
                    byte blue = (byte)random.Next(256);
                    Color randomColor = Color.FromRgb(red, green, blue);
                    //foreach (SimplePoint p in cluster.Objects)
                    //{
                    //    double centerX = p.Point.X;
                    //    double centerY = p.Point.Y;
                    //    double radiusX = 0.5;
                    //    double radiusY = 0.5;

                    //    Ellipse ellipse = new Ellipse
                    //    {
                    //        Width = radiusX * 2,
                    //        Height = radiusY * 2,
                    //        Fill = new SolidColorBrush(randomColor),
                    //    };

                    //    Canvas.SetLeft(ellipse, centerX - radiusX);
                    //    Canvas.SetTop(ellipse, centerY - radiusY);

                    //    pointCanvas.Children.Add(ellipse);
                    //}

                    double minX = cluster.Objects.Min(p => p.Point.X);
                    double minY = cluster.Objects.Min(p => p.Point.Y);
                    double maxX = cluster.Objects.Max(p => p.Point.X);
                    double maxY = cluster.Objects.Max(p => p.Point.Y);

                    Brush lineBrush = new SolidColorBrush(randomColor);
                    double lineThickness = 1; // Толщина линии

                    // Создайте линии для каждой стороны прямоугольника
                    Line topLine = new Line();
                    topLine.X1 = minX;
                    topLine.Y1 = minY;
                    topLine.X2 = maxX;
                    topLine.Y2 = minY;
                    topLine.Stroke = lineBrush;
                    topLine.StrokeThickness = lineThickness;

                    Line bottomLine = new Line();
                    bottomLine.X1 = minX;
                    bottomLine.Y1 = maxY;
                    bottomLine.X2 = maxX;
                    bottomLine.Y2 = maxY;
                    bottomLine.Stroke = lineBrush;
                    bottomLine.StrokeThickness = lineThickness;

                    Line leftLine = new Line();
                    leftLine.X1 = minX;
                    leftLine.Y1 = minY;
                    leftLine.X2 = minX;
                    leftLine.Y2 = maxY;
                    leftLine.Stroke = lineBrush;
                    leftLine.StrokeThickness = lineThickness;

                    Line rightLine = new Line();
                    rightLine.X1 = maxX;
                    rightLine.Y1 = minY;
                    rightLine.X2 = maxX;
                    rightLine.Y2 = maxY;
                    rightLine.Stroke = lineBrush;
                    rightLine.StrokeThickness = lineThickness;

                    // Добавьте линии на Canvas
                    pointCanvas.Children.Add(topLine);
                    pointCanvas.Children.Add(bottomLine);
                    pointCanvas.Children.Add(leftLine);
                    pointCanvas.Children.Add(rightLine);
                }
            }
        }
        private void pointCanvas_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            var matTrans = pointCanvas.RenderTransform as MatrixTransform;
            var pos1 = e.GetPosition(canvasScrollViewer);

            var scale = e.Delta > 0 ? 1.3 : 1 / 1.3;

            var mat = matTrans.Matrix;
            mat.ScaleAt(scale, scale, pos1.X, pos1.Y);
            matTrans.Matrix = mat;
            e.Handled = true;
        }
    }
}
