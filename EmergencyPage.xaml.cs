using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class EmergencyPage : Page, ILocalizable
    {
        public EmergencyPage()
        {
            InitializeComponent();
            UpdateLanguage();
        }

        public void UpdateLanguage()
        {
            TitleText.Text = MainWindow.IsBashkir ? "ШАҠ ҠОТСАҢҒЫС ХӘЛ" : "ЭКСТРЕННЫЙ ВЫЗОВ";
            DescText.Text = MainWindow.IsBashkir ?
                "Түбәндәге төймәне баҫып, шаҡ ҡотсаңғыс ярҙамын саҡырығыҙ" :
                "Нажмите кнопку ниже, чтобы вызвать скорую помощь";
            SOSBtn.Content = MainWindow.IsBashkir ? "❗ ШАҠ ҠОТСАҢҒЫС ЯРЫМЫН САҠЫРЫҒЫЗ ❗" : "❗ ВЫЗВАТЬ СКОРУЮ ❗";
        }

        private void SOSBtn_Click(object sender, RoutedEventArgs e)
        {
            // В реальном приложении здесь будет вызов API или отправка сигнала в регистратуру
            MessageBox.Show(MainWindow.IsBashkir ?
                "Шаҡ ҡотсаңғыс ярҙамы юллана!\nКүсәргә кәрәкмәй — ул һеҙҙе эҙләйәсәк." :
                "Скорая помощь выехала!\nНе покидайте место ожидания.",
                "Вызов отправлен", MessageBoxButton.OK, MessageBoxImage.Exclamation);
            MainWindow.Instance.NavigateToMainMenu();
        }
    }
}