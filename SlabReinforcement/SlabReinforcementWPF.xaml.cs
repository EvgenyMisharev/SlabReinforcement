using Accord.MachineLearning;
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
                Ellipse ellipse = new Ellipse
                {
                    Width = 1,
                    Height = 1,
                    Fill = new SolidColorBrush(wpfColor),
                };

                Canvas.SetLeft(ellipse, pointItem.Point.X);
                Canvas.SetTop(ellipse, pointItem.Point.Y);

                pointCanvas.Children.Add(ellipse);
            }

            List<ColorItem> colorItemsList = new List<ColorItem>();
            foreach (Color color in colorList)
            {
                colorItemsList.Add(new ColorItem { Description = $"R{color.R}, G{color.G}, B{color.B}", Color = new SolidColorBrush(color)});
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
                    Ellipse ellipse = new Ellipse
                    {
                        Width = 1,
                        Height = 1,
                        Fill = new SolidColorBrush(wpfColor),
                    };

                    Canvas.SetLeft(ellipse, pointItem.Point.X);
                    Canvas.SetTop(ellipse, pointItem.Point.Y);

                    pointCanvas.Children.Add(ellipse);
                }
            }
        }

        private void button_Click(object sender, RoutedEventArgs e)
        {
            int numberOfClusters = 30;
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

            KMeans kmeans = new KMeans(numberOfClusters);
            double[][] data = pointColorItemFilteredList.Select(item =>
            {
                return new double[]
                {
                    item.Point.X,
                    item.Point.Y,
                };
            }).ToArray();

            KMeansClusterCollection clusters = kmeans.Learn(data);
            int[] clusterAssignments = clusters.Decide(data);

            List<List<double[]>> clusteredPoints = new List<List<double[]>>();
            for (int clusterIndex = 0; clusterIndex < numberOfClusters; clusterIndex++)
            {
                clusteredPoints.Add(new List<double[]>());
            }

            for (int i = 0; i < data.Length; i++)
            {
                int clusterIndex = clusterAssignments[i];
                clusteredPoints[clusterIndex].Add(data[i]);
            }

            foreach (List<double[]> pointCluster in clusteredPoints)
            {
                if (pointCluster.Count > 4)
                {
                    double minX = pointCluster.Min(p => p[0]);
                    double minY = pointCluster.Min(p => p[1]);
                    double maxX = pointCluster.Max(p => p[0]);
                    double maxY = pointCluster.Max(p => p[1]);

                    // Размеры прямоугольника
                    double width = maxX - minX;
                    double height = maxY - minY;

                    // Создаем Rectangle
                    Rectangle rect = new Rectangle
                    {
                        Width = width,
                        Height = height,
                        Stroke = Brushes.Black,
                        StrokeThickness = 2
                    };

                    // Устанавливаем координаты для прямоугольника
                    Canvas.SetLeft(rect, minX);
                    Canvas.SetTop(rect, minY);

                    // Добавляем прямоугольник на Canvas
                    pointCanvas.Children.Add(rect);
                }
            }
        }
    }
}
