using System;
using System.Windows;
using System.Windows.Controls;
using YanaulKioskPerfect.Helpers;
using YanaulKioskPerfect.Services;

namespace YanaulKioskPerfect
{
    public partial class MainMenuPage : Page, ILocalizable
    {
        private readonly EmergencyService _emergencyService = new EmergencyService();

        public MainMenuPage()
        {
            InitializeComponent();
            UpdateLanguage();
        }

        public void UpdateLanguage()
        {
            if (TitleText != null)
                TitleText.Text = MainWindow.IsBashkir ? "ХЕҘМӘТТӘРҺЕҢДЕ ҺАЙЛАГЫҘ" : "ВЫБЕРИТЕ УСЛУГУ";
        }

        private void BtnBookDoctor_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance.NavigateToSpecialties();

        private void BtnMedicalCard_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance.NavigateToMedicalCard();

        private void BtnHomeVisit_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MainWindow.CurrentPatientPolicy))
            {
                MessageBox.Show("Для вызова врача на дом войдите по номеру полиса.", "Вызов на дом",
                    MessageBoxButton.OK, MessageBoxImage.Information);
                MainWindow.Instance.NavigateToIdentification();
                return;
            }
            MainWindow.Instance.NavigateToHomeVisit();
        }

        private void BtnVirtualQueue_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance.NavigateToVirtualQueue();

        private void BtnTriage_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance.NavigateToTriage();

        private void BtnInfo_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance.NavigateToInfo();

        private void BtnHospitalMap_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance.NavigateToHospitalMap();

        private void BtnChat_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(MainWindow.CurrentPatientPolicy))
            {
                MessageBox.Show("Для чата войдите по номеру полиса.", "Чат", MessageBoxButton.OK, MessageBoxImage.Information);
                MainWindow.Instance.NavigateToIdentification();
                return;
            }
            MainWindow.Instance.MainFrame.Navigate(new ChatPage(ChatService.NormalizePolicy(MainWindow.CurrentPatientPolicy)));
        }

        private void BtnFeedback_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance.NavigateToFeedback();

        private void BtnAdminLogin_Click(object sender, RoutedEventArgs e)
        {
            var login = new AdminLoginWindow();
            if (login.ShowDialog() == true)
                MainWindow.Instance.MainFrame.Navigate(new AdminDashboardPage());
        }

        private void BtnSOS_Click(object sender, RoutedEventArgs e)
        {
            var confirm = MessageBox.Show(
                "🆘 ПОДТВЕРДИТЕ ЭКСТРЕННЫЙ ВЫЗОВ\n\nСигнал будет отправлен в диспетчерскую ЦРБ и зарегистрирован в системе.",
                "SOS — экстренная помощь",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (confirm != MessageBoxResult.Yes) return;

            string policy = MainWindow.CurrentPatientPolicy;
            if (string.IsNullOrWhiteSpace(policy))
                policy = DatabaseHelper.DemoGuestPolicy;

            string name = MainWindow.CurrentPatient?.PatientName ?? "Пациент терминала";

            if (_emergencyService.RegisterSosAlert(policy, name))
            {
                MessageBox.Show(
                    "✅ Сигнал SOS зарегистрирован!\n\n" +
                    "• Заявка создана в диспетчерской\n" +
                    "• Уведомление отправлено в систему\n" +
                    "• Оставайтесь у терминала\n\n" +
                    "При необходимости звоните 103",
                    "SOS отправлен",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show(
                    "Сигнал зарегистрирован локально. Проверьте подключение к базе данных.",
                    "SOS",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
        }
    }
}
