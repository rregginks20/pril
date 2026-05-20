using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class InfoPage : Page, ILocalizable
    {
        public InfoPage()
        {
            InitializeComponent();
            UpdateLanguage();
        }

        public void UpdateLanguage()
        {
            if (MainWindow.IsBashkir)
            {
                TitleText.Text = "БЕЛЬНИЦА ТУРАҺЫНДА МӘҒЛҮМӘТ";
                // Можно добавить перевод содержимого, если нужно, но пока оставим русский для универсальности
                // Или заменить тексты в XAML на Binding с конвертером
            }
            else
            {
                TitleText.Text = "ИНФОРМАЦИЯ О БОЛЬНИЦЕ";
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.NavigateToMainMenu();
        }
    }
}