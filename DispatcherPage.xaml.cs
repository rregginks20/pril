using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class DispatcherPage : Page
    {
        public DispatcherPage()
        {
            InitializeComponent();
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    // 1. Загружаем ВЫЗОВЫ НА ДОМ
                    var visits = db.HomeVisits.OrderByDescending(v => v.CreatedAt).Take(50).ToList();
                    GridHomeVisits.ItemsSource = visits;

                    // 2. Загружаем ИСТОРИЮ ТРИАЖА
                    var triageLogs = db.TriageRecords.OrderByDescending(t => t.CreatedAt).Take(50).ToList();
                    GridTriage.ItemsSource = triageLogs;

                    if (visits.Count == 0 && triageLogs.Count == 0)
                        MessageBox.Show("Пока нет данных.", "Инфо");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных:\n" + ex.Message, "Ошибка");
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e)
        {
            LoadData();
            MessageBox.Show("Данные обновлены!", "Инфо");
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (Application.Current.MainWindow is MainWindow main)
                main.MainFrame.Navigate(new AdminDashboardPage());
        }
    }
}