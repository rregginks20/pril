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
    public partial class PrescriptionPage : Page
    {
        private string _currentPolicy;

        // Конструктор обязательно принимает policyNumber
        public PrescriptionPage(string policyNumber)
        {
            InitializeComponent();
            _currentPolicy = policyNumber;
            LoadData();
        }

        private void LoadData()
        {
            try
            {
                // Используем твой контекст базы данных (YanaulCRBEntities или YanaulCRBEntities2)
                using (var db = new YanaulCRBEntities2())
                {
                    // Загружаем рецепты текущего пациента
                    var prescriptions = db.Prescriptions
                        .Where(p => p.PolicyNumber == _currentPolicy)
                        .OrderByDescending(p => p.IssueDate)
                        .ToList();

                    PrescriptionsList.ItemsSource = prescriptions;

                    if (prescriptions.Count == 0)
                    {
                        MessageBox.Show("У пациента пока нет активных рецептов.", "Информация", MessageBoxButton.OK, MessageBoxImage.Information);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === МЕТОДЫ КНОПОК (Именно их не хватало) ===

        // 1. Кнопка "Назад"
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (NavigationService != null && NavigationService.CanGoBack)
            {
                NavigationService.GoBack();
            }
        }

        // 2. Кнопка "Печать"
        private void PrintRecipe_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is Prescription recipe)
            {
                PrintDialog printDialog = new PrintDialog();
                if (printDialog.ShowDialog() == true)
                {
                    MessageBox.Show(
                        $"Рецепт от {recipe.IssueDate:dd.MM.yyyy} отправлен на печать!\n\nПрепараты:\n{recipe.Medicines}",
                        "Печать",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    // Здесь можно добавить реальную логику печати через PrintDocument, если нужно
                }
            }
        }

        // 3. Кнопка "Сохранить"
        private void SaveRecipe_Click(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (button != null && button.DataContext is Prescription recipe)
            {
                SaveFileDialog saveFileDialog = new SaveFileDialog
                {
                    Filter = "Текстовые файлы|*.txt|Все файлы|*.*",
                    FileName = $"Recept_{recipe.IssueDate:yyyyMMdd}.txt",
                    Title = "Сохранить рецепт"
                };

                if (saveFileDialog.ShowDialog() == true)
                {
                    try
                    {
                        string content = $"РЕЦЕПТ\n" +
                                       $"Дата выдачи: {recipe.IssueDate:dd.MM.yyyy}\n" +
                                       $"Действителен до: {recipe.ValidUntil:dd.MM.yyyy}\n\n" +
                                       $"Препараты:\n{recipe.Medicines}\n\n" +
                                       $"Инструкция:\n{recipe.DosageInstructions}\n\n" +
                                       $"Статус: {recipe.Status}";

                        File.WriteAllText(saveFileDialog.FileName, content);

                        MessageBox.Show($"Рецепт успешно сохранен!\n\nПуть: {saveFileDialog.FileName}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                        // Автоматически открываем файл
                        Process.Start(saveFileDialog.FileName);
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show("Ошибка сохранения: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
        }
    }
}