using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using YanaulKioskPerfect.Helpers;

namespace YanaulKioskPerfect
{
    public partial class IdentificationPage : Page
    {
        public IdentificationPage()
        {
            InitializeComponent();
            if (PolicyTextBox != null)
                PolicyTextBox.Focus();
        }

        private void PolicyTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (PolicyTextBox == null) return;

            string text = PolicyTextBox.Text;
            PolicyTextBox.Text = new string(text.Where(char.IsDigit).ToArray());
            PolicyTextBox.SelectionStart = PolicyTextBox.Text.Length;
        }

        private void BtnIdentify_Click(object sender, RoutedEventArgs e)
        {
            if (PolicyTextBox == null) return;

            string policyNumber = PolicyTextBox.Text.Trim();

            if (policyNumber.Length < 16)
            {
                MessageBox.Show("Введите полный номер полиса ОМС (16 цифр)",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
            using (var db = new YanaulCRBEntities2())
            {
                var patient = db.MedicalCards
                    .FirstOrDefault(p => p.PolicyNumber == policyNumber);

                if (patient != null)
                {
                    MainWindow.CurrentPatientPolicy = policyNumber;
                    MainWindow.CurrentPatient = patient;
                    MainWindow.ReloadPatientAppointments(policyNumber);
                    MainWindow.Instance?.UpdatePatientBadge();

                    if (MainWindow.Instance != null)
                        MainWindow.Instance.NavigateToMainMenu();
                }
                else
                {
                    var result = MessageBox.Show(
                        $"Пациент с полисом {policyNumber} не найден.\nСоздать новую медицинскую карту?",
                        "Новый пациент",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        if (MainWindow.Instance != null)
                        {
                            MainWindow.Instance.NavigateToNewPatientCard(policyNumber);
                        }
                    }
                }
            }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка базы данных:\n" + ex.Message + "\n\nЗапустите Database\\Install-Database.bat",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnQuickAccess_Click(object sender, RoutedEventArgs e)
        {
            if (!DatabaseHelper.TryLoginGuest(out var guest))
            {
                MessageBox.Show(
                    "Не удалось войти в демо-режим. Установите базу данных (Database\\Install-Database.bat).",
                    "Ошибка БД",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
                return;
            }

            MainWindow.CurrentPatientPolicy = guest.PolicyNumber;
            MainWindow.CurrentPatient = guest;
            MainWindow.ReloadPatientAppointments(guest.PolicyNumber);
            MainWindow.Instance?.UpdatePatientBadge();
            MainWindow.Instance?.NavigateToMainMenu();
        }

        private void BtnPhoneAccess_Click(object sender, RoutedEventArgs e)
        {
            // Создаем свое окно ввода
            var inputWindow = new Window
            {
                Title = "Вход по телефону",
                Width = 400,
                Height = 200,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Введите номер телефона:",
                FontSize = 16,
                Margin = new Thickness(20, 20, 20, 10)
            };
            Grid.SetRow(label, 0);
            Grid.SetColumn(label, 0);
            Grid.SetColumnSpan(label, 3);

            var textBox = new TextBox
            {
                FontSize = 16,
                Height = 40,
                Margin = new Thickness(20, 0, 20, 20),
                Text = "+7"
            };
            Grid.SetRow(textBox, 1);
            Grid.SetColumn(textBox, 0);
            Grid.SetColumnSpan(textBox, 3);

            var btnOk = new Button
            {
                Content = "OK",
                Width = 80,
                Height = 35,
                Margin = new Thickness(20, 0, 10, 20),
                Background = System.Windows.Media.Brushes.Green,
                Foreground = System.Windows.Media.Brushes.White
            };
            Grid.SetRow(btnOk, 2);
            Grid.SetColumn(btnOk, 1);

            var btnCancel = new Button
            {
                Content = "Отмена",
                Width = 80,
                Height = 35,
                Margin = new Thickness(10, 0, 20, 20),
                Background = System.Windows.Media.Brushes.Red,
                Foreground = System.Windows.Media.Brushes.White
            };
            Grid.SetRow(btnCancel, 2);
            Grid.SetColumn(btnCancel, 2);

            string resultPhone = null;

            btnOk.Click += (s, args) =>
            {
                resultPhone = textBox.Text;
                inputWindow.DialogResult = true;
                inputWindow.Close();
            };

            btnCancel.Click += (s, args) =>
            {
                inputWindow.DialogResult = false;
                inputWindow.Close();
            };

            grid.Children.Add(label);
            grid.Children.Add(textBox);
            grid.Children.Add(btnOk);
            grid.Children.Add(btnCancel);

            inputWindow.Content = grid;

            bool? dialogResult = inputWindow.ShowDialog();

            if (dialogResult == true && !string.IsNullOrEmpty(resultPhone))
            {
                using (var db = new YanaulCRBEntities2())
                {
                    string digits = new string(resultPhone.Where(char.IsDigit).ToArray());
                    var patient = db.MedicalCards.FirstOrDefault(p =>
                        (!string.IsNullOrEmpty(p.Phone) && p.Phone.Replace(" ", "").Contains(digits)) ||
                        p.PolicyNumber.Contains(digits) ||
                        p.PatientName.Contains(resultPhone.Trim()));

                    if (patient != null)
                    {
                        MainWindow.CurrentPatientPolicy = patient.PolicyNumber;
                        MainWindow.CurrentPatient = patient;
                        MainWindow.ReloadPatientAppointments(patient.PolicyNumber);
                        MainWindow.Instance?.UpdatePatientBadge();
                        MainWindow.Instance?.NavigateToMainMenu();
                    }
                    else
                    {
                        MessageBox.Show("Пациент не найден",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
        }
    }
}