using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Structure;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
        private List<ColorStat> _selectedColors;
        ObservableCollection<ColorStat> ColorStatsCollection;

        public string ConcreteClass;
        public string AdjacentElementsTolerance;
        public string ZoneMergeTolerance;
        public string SelectedReinforcementDirection;
        public bool UseCutLengths;
        public string RoundIncrement;

        private SlabReinforcementSettings SettingsItem;

        public SlabReinforcementWPF(List<ColorStat> colorStats, List<RebarBarType> availableRebarTypes)
        {
            InitializeComponent();

            // Загрузка настроек
            SettingsItem = SlabReinforcementSettings.LoadSettings();

            // Применение настроек в интерфейсе
            ComboBox_ConcreteClass.ItemsSource = new List<string> { "B15", "B20", "B25", "B30" };
            ComboBox_ConcreteClass.SelectedItem = SettingsItem.ConcreteClass;
            TextBox_AdjacentElementsTolerance.Text = SettingsItem.AdjacentElementsTolerance;
            TextBox_ZoneMergeTolerance.Text = SettingsItem.ZoneMergeTolerance;

            if (SettingsItem.SelectedReinforcementDirection == "Низ X")
                RadioButton_LowerX.IsChecked = true;
            else if (SettingsItem.SelectedReinforcementDirection == "Верх X")
                RadioButton_UpperX.IsChecked = true;
            else if (SettingsItem.SelectedReinforcementDirection == "Низ Y")
                RadioButton_LowerY.IsChecked = true;
            else if (SettingsItem.SelectedReinforcementDirection == "Верх Y")
                RadioButton_UpperY.IsChecked = true;

            // Настройка метода округления
            RadioButton_RoundIncrement.IsChecked = !SettingsItem.UseCutLengths;
            RadioButton_RoundCutLengths.IsChecked = SettingsItem.UseCutLengths;
            TextBox_RoundIncrement.Text = SettingsItem.RoundIncrement;

            // Инициализация арматуры и восстановление цветовых настроек
            foreach (var stat in colorStats)
            {
                stat.RebarTypes = availableRebarTypes;

                // Ищем сохранённые настройки для текущего цвета
                var savedSetting = SettingsItem.ColorSettings.FirstOrDefault(s => s.Color == stat.Color);

                if (savedSetting != null)
                {
                    stat.SelectedRebarType = availableRebarTypes.FirstOrDefault(r => r.Name == savedSetting.RebarTypeName) ?? availableRebarTypes.FirstOrDefault();
                    stat.Spacing = savedSetting.Spacing;
                    stat.IsSelected = savedSetting.IsSelected;
                }
                else
                {
                    stat.SelectedRebarType = availableRebarTypes.FirstOrDefault();
                    stat.Spacing = 200; // Значение по умолчанию
                }
            }

            ColorStatsCollection = new ObservableCollection<ColorStat>(colorStats);
            ColorStatsDataGrid.ItemsSource = ColorStatsCollection;
        }


        private void Button_Ok_Click(object sender, RoutedEventArgs e)
        {
            _selectedColors = ColorStatsCollection.Where(stat => stat.IsSelected).ToList();

            // Сохраняем общие настройки
            ConcreteClass = ComboBox_ConcreteClass.SelectedItem.ToString();
            SettingsItem.ConcreteClass = ConcreteClass;

            AdjacentElementsTolerance = TextBox_AdjacentElementsTolerance.Text;
            SettingsItem.AdjacentElementsTolerance = AdjacentElementsTolerance;

            ZoneMergeTolerance = TextBox_ZoneMergeTolerance.Text;
            SettingsItem.ZoneMergeTolerance = ZoneMergeTolerance;

            if (RadioButton_LowerX.IsChecked == true)
                SelectedReinforcementDirection = "Низ X";
            else if (RadioButton_UpperX.IsChecked == true)
                SelectedReinforcementDirection = "Верх X";
            else if (RadioButton_LowerY.IsChecked == true)
                SelectedReinforcementDirection = "Низ Y";
            else if (RadioButton_UpperY.IsChecked == true)
                SelectedReinforcementDirection = "Верх Y";

            SettingsItem.SelectedReinforcementDirection = SelectedReinforcementDirection;

            // Сохраняем настройки метода округления
            UseCutLengths = RadioButton_RoundCutLengths.IsChecked == true;
            SettingsItem.UseCutLengths = UseCutLengths;

            RoundIncrement = TextBox_RoundIncrement.Text;
            SettingsItem.RoundIncrement = RoundIncrement;

            // Сохраняем настройки для каждого цвета
            SettingsItem.ColorSettings.Clear();
            foreach (var stat in ColorStatsCollection)
            {
                SettingsItem.ColorSettings.Add(new ColorReinforcementSettings
                {
                    Color = stat.Color,
                    RebarTypeName = stat.SelectedRebarType?.Name,
                    Spacing = stat.Spacing,
                    IsSelected = stat.IsSelected
                });
            }

            SettingsItem.SaveSettings();

            DialogResult = true;
            Close();
        }

        private void Button_Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                Button_Ok_Click(sender, e);
            }
            else if (e.Key == Key.Escape)
            {
                Button_Cancel_Click(sender, e);
            }
        }

        // Метод для получения выбранных цветов
        public List<ColorStat> GetSelectedColors()
        {
            return _selectedColors ?? new List<ColorStat>();
        }
    }
}
