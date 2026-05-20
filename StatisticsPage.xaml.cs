using System;
using System.Windows;
using System.Windows.Controls;
using YanaulKioskPerfect.Services;

namespace YanaulKioskPerfect
{
    public partial class StatisticsPage : Page
    {
        private readonly AdminStatisticsService _statsService = new AdminStatisticsService();

        public StatisticsPage()
        {
            InitializeComponent();
            LoadStats();
        }

        private void LoadStats()
        {
            try
            {
                var s = _statsService.LoadStats();
                LblDbInfo.Text = s.DatabaseStatus;
                LblPatients.Text = s.PatientsCount.ToString();
                LblDoctorsCount.Text = s.DoctorsCount.ToString();
                LblAppointmentsCount.Text = $"{s.ActiveAppointmentsToday} / {s.TodayAppointments}";
                LblCancelled.Text = s.CancelledToday.ToString();
                LblHomeVisitsCount.Text = s.PendingHomeVisits.ToString();
                LblPaymentsSum.Text = s.PaymentsTotal.ToString("N0") + " ₽";
                LblPaymentsToday.Text = s.PaymentsToday.ToString("N0") + " ₽";
                LblFeedbacks.Text = s.FeedbacksCount > 0
                    ? $"{s.FeedbacksCount} (★{s.AverageDoctorRating:F1})"
                    : "0";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка статистики: " + ex.Message);
            }
        }

        private void BtnRefresh_Click(object sender, RoutedEventArgs e) => LoadStats();

        private void BtnBackAdmin_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
                NavigationService.GoBack();
            else
                MainWindow.Instance?.MainFrame.Navigate(new AdminDashboardPage());
        }
    }
}
