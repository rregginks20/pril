using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using LiveCharts;
using LiveCharts.Wpf;

namespace YanaulKioskPerfect
{
    public partial class HealthChartPage : Page
    {
        private readonly string _currentPolicy;

        public HealthChartPage()
            : this(MainWindow.CurrentPatientPolicy ?? "1234567890123456")
        {
        }

        public HealthChartPage(string policyNumber)
        {
            InitializeComponent();
            _currentPolicy = string.IsNullOrWhiteSpace(policyNumber)
                ? "1234567890123456"
                : policyNumber;
            LoadData("Давление");
        }

        private void LoadData(string type)
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var metrics = db.HealthMetrics
                        .Where(m => m.PolicyNumber == _currentPolicy && m.MetricType == type)
                        .OrderBy(m => m.MeasureDate)
                        .ToList();

                    if (metrics.Count == 0)
                    {
                        LblNoData.Visibility = Visibility.Visible;
                        MainChart.Series = new SeriesCollection();
                        return;
                    }

                    LblNoData.Visibility = Visibility.Collapsed;
                    var values = new ChartValues<double>();
                    var labels = new List<string>();

                    foreach (var m in metrics)
                    {
                        values.Add((double)m.Value1);
                        labels.Add(m.MeasureDate.ToString("dd.MM"));
                    }

                    MainChart.Series = new SeriesCollection
                    {
                        new LineSeries { Title = type, Values = values, PointGeometrySize = 10, StrokeThickness = 3 }
                    };

                    if (MainChart.AxisX.Count > 0)
                        MainChart.AxisX[0].Labels = labels;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка графика: " + ex.Message);
            }
        }

        private void BtnPressure_Click(object sender, RoutedEventArgs e) => LoadData("Давление");
        private void BtnWeight_Click(object sender, RoutedEventArgs e) => LoadData("Вес");
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService.CanGoBack) NavigationService.GoBack();
            else MainWindow.Instance.NavigateToMainMenu();
        }
    }
}