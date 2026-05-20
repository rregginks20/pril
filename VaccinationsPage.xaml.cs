using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Win32;
using System.IO;
using System.Diagnostics;

namespace YanaulKioskPerfect
{
    public partial class VaccinationsPage : Page
    {
        private string _currentPolicy;

        public VaccinationsPage(string policyNumber)
        {
            InitializeComponent();
            _currentPolicy = policyNumber;
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                using (var db = new YanaulCRBEntities2()) // Или YanaulCRBEntities
                {
                    // ВАЖНО: В таблице Vaccination поле называется PatientPolicy
                    var vaccinations = db.Vaccinations
                        .Where(v => v.PatientPolicy == _currentPolicy)
                        .OrderByDescending(v => v.VaccinationDate)
                        .ToList();

                    VaccinesList.ItemsSource = vaccinations;

                    if (vaccinations.Count == 0)
                    {
                        MessageBox.Show("Записей о прививках не найдено.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // Метод кнопки "Назад"
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        // Метод кнопки "Сохранить"
        private void SaveVaccine_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is Vaccination vaccine)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Текстовые файлы|*.txt",
                    FileName = $"Privivka_{vaccine.VaccineName}_{vaccine.VaccinationDate:yyyyMMdd}.txt",
                    Title = "Сохранить сертификат о прививке"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    string nextDose = vaccine.NextVaccinationDate.HasValue
                        ? vaccine.NextVaccinationDate.Value.ToString("dd.MM.yyyy")
                        : "Не требуется";

                    string content = $"СЕРТИФИКАТ О ПРИВИВКЕ\n\n" +
                                   $"Вакцина: {vaccine.VaccineName}\n" +
                                   $"Дата вакцинации: {vaccine.VaccinationDate:dd.MM.yyyy}\n" +
                                   $"Серийный номер: {vaccine.SeriesNumber}\n" +
                                   $"Следующая доза: {nextDose}\n\n" +
                                   $"Пациент (Полис): {_currentPolicy}";

                    File.WriteAllText(saveFileDialog.FileName, content);

                    MessageBox.Show($"Сертификат сохранен!\n\nПуть: {saveFileDialog.FileName}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    // Автоматически открываем файл
                    Process.Start(saveFileDialog.FileName);
                }
            }
        }

        // Метод кнопки "Печать"
        private void PrintVaccine_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is Vaccination vaccine)
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    MessageBox.Show($"Сертификат о прививке ({vaccine.VaccineName}) отправлен на печать!", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }
    }
}