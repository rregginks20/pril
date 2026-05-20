using System;
using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class SpecialtySelectionPage : Page, ILocalizable
    {
        public SpecialtySelectionPage()
        {
            InitializeComponent();
            UpdateLanguage();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.NavigateToMainMenu();
            }
        }

        private void BtnSpecialty_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is string specialty)
            {
                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.NavigateToDoctorSelection(specialty);
                }
            }
        }

        public void UpdateLanguage()
        {
            if (TitleText != null)
            {
                TitleText.Text = MainWindow.IsBashkir ? "ТАБИПКА ЯЗЫЛЫРҒА" : "ЗАПИСАТЬСЯ К ВРАЧУ";
            }

            if (BtnBack != null)
            {
                BtnBack.Content = MainWindow.IsBashkir ? "⇦ Кире" : "⇦ Назад";
            }
        }
    }
}