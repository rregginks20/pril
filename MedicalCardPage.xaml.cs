using iTextSharp.text;
using iTextSharp.text.pdf;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YanaulKioskPerfect.Services; // Добавлено для EmailService

namespace YanaulKioskPerfect
{
    public partial class MedicalCardPage : Page, ILocalizable
    {
        private string _currentPolicy;
        private MedicalCard _patient;
        private int _analysesCount = 0;
        private int _prescriptionsCount = 0;
        private int _certificatesCount = 0;
        private int _vaccinationsCount = 0;
        private int _notificationsCount = 0;

        public MedicalCardPage()
        {
            InitializeComponent();
            _currentPolicy = MainWindow.CurrentPatientPolicy;

            if (string.IsNullOrEmpty(_currentPolicy))
            {
                MessageBox.Show("Ошибка: Полис пациента не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            LoadPatientData();
            LoadCounts();
            LoadNotifications();
            UpdateLanguage();

        }

        private void LoadPatientData()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    _patient = db.MedicalCards.FirstOrDefault(p => p.PolicyNumber == _currentPolicy);

                    if (_patient != null)
                    {
                        if (PatientNameText != null) PatientNameText.Text = _patient.PatientName;
                        if (PolicyNumberText != null) PolicyNumberText.Text = _patient.PolicyNumber;

                        if (BirthDateText != null && _patient.DateOfBirth.HasValue)
                        {
                            int age = DateTime.Now.Year - _patient.DateOfBirth.Value.Year;
                            if (DateTime.Now.DayOfYear < _patient.DateOfBirth.Value.DayOfYear) age--;
                            BirthDateText.Text = $"{_patient.DateOfBirth.Value:dd.MM.yyyy} ({age} лет)";
                        }

                        if (PhoneText != null)
                            PhoneText.Text = !string.IsNullOrEmpty(_patient.Phone) ? _patient.Phone : "Не указан";

                        if (EmailText != null)
                            EmailText.Text = !string.IsNullOrEmpty(_patient.Email) ? _patient.Email : "Не указан";
                    }
                    else
                    {
                        MessageBox.Show("Пациент не найден в базе данных.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных пациента: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void LoadCounts()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    _analysesCount = db.AnalysisResults.Count(a => a.PolicyNumber == _currentPolicy);
                    if (AnalysesCountText != null) AnalysesCountText.Text = $"{_analysesCount} результатов";

                    _prescriptionsCount = db.Prescriptions.Count(p => p.PolicyNumber == _currentPolicy && p.Status == "активен");
                    if (PrescriptionsCountText != null) PrescriptionsCountText.Text = $"{_prescriptionsCount} рецептов";

                    _certificatesCount = db.Certificates.Count(c => c.PatientPolicy == _currentPolicy && c.Status == "активен");
                    if (CertificatesCountText != null) CertificatesCountText.Text = $"{_certificatesCount} документов";

                    _vaccinationsCount = db.Vaccinations.Count(v => v.PatientPolicy == _currentPolicy);
                    if (VaccinationsCountText != null) VaccinationsCountText.Text = $"{_vaccinationsCount} прививок";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка подсчета: " + ex.Message);
            }
        }

        private void LoadNotifications()
        {
            try
            {
                if (NotificationsPanel == null) return;
                NotificationsPanel.Children.Clear();

                using (var db = new YanaulCRBEntities2())
                {
                    var notifications = db.Notifications
                        .Where(n => n.PatientPolicy == _currentPolicy && n.IsRead == false)
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(5)
                        .ToList();

                    _notificationsCount = notifications.Count;
                    if (NotificationsCountText != null)
                        NotificationsCountText.Text = $" ({_notificationsCount})";

                    if (notifications.Count == 0)
                    {
                        NotificationsPanel.Children.Add(new TextBlock
                        {
                            Text = "Нет новых уведомлений",
                            FontSize = 14,
                            Foreground = Brushes.Gray,
                            FontStyle = FontStyles.Italic,
                            Margin = new Thickness(0, 10, 0, 10)
                        });
                        return;
                    }

                    foreach (var notification in notifications)
                    {
                        var border = new Border
                        {
                            Background = new SolidColorBrush(GetNotificationColor(notification.Type)),
                            CornerRadius = new CornerRadius(10),
                            Padding = new Thickness(15, 12, 15, 12),
                            Margin = new Thickness(0, 0, 0, 10),
                            Cursor = Cursors.Hand
                        };

                        var stack = new StackPanel { Orientation = Orientation.Horizontal };

                        stack.Children.Add(new TextBlock
                        {
                            Text = GetNotificationIcon(notification.Type),
                            FontSize = 20,
                            VerticalAlignment = VerticalAlignment.Center,
                            Margin = new Thickness(0, 0, 15, 0)
                        });

                        var textStack = new StackPanel();
                        textStack.Children.Add(new TextBlock
                        {
                            Text = notification.Message,
                            FontSize = 14,
                            Foreground = Brushes.Black,
                            TextWrapping = TextWrapping.Wrap
                        });

                        if (notification.CreatedAt.HasValue)
                        {
                            textStack.Children.Add(new TextBlock
                            {
                                Text = notification.CreatedAt.Value.ToString("dd.MM.yyyy HH:mm"),
                                FontSize = 11,
                                Foreground = Brushes.Gray,
                                Margin = new Thickness(0, 5, 0, 0)
                            });
                        }

                        stack.Children.Add(textStack);
                        border.Child = stack;

                        border.MouseLeftButtonDown += (s, e) => MarkNotificationAsRead(notification.NotificationId);

                        NotificationsPanel.Children.Add(border);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка уведомлений: " + ex.Message);
            }
        }

        private Color GetNotificationColor(string type)
        {
            switch (type?.ToLower())
            {
                case "анализ": return Color.FromRgb(232, 245, 233);
                case "рецепт": return Color.FromRgb(255, 243, 224);
                case "справка": return Color.FromRgb(227, 242, 253);
                case "напоминание": return Color.FromRgb(255, 235, 238);
                default: return Color.FromRgb(245, 245, 245);
            }
        }

        private string GetNotificationIcon(string type)
        {
            switch (type?.ToLower())
            {
                case "анализ": return "📋";
                case "рецепт": return "💊";
                case "справка": return "📄";
                case "напоминание": return "⏰";
                default: return "🔔";
            }
        }

        private void MarkNotificationAsRead(int id)
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var n = db.Notifications.Find(id);
                    if (n != null)
                    {
                        n.IsRead = true;
                        db.SaveChanges();
                        LoadNotifications();
                    }
                }
            }
            catch { }
        }

        // === НАВИГАЦИЯ ПО РАЗДЕЛАМ ===

        private void Analyses_Click(object sender, MouseButtonEventArgs e)
        {
            NavigationService?.Navigate(new AnalysisResultsPage(_currentPolicy));
        }

        private void Prescriptions_Click(object sender, MouseButtonEventArgs e)
        {
            NavigationService?.Navigate(new PrescriptionPage(_currentPolicy));
        }

        private void Certificates_Click(object sender, MouseButtonEventArgs e)
        {
            NavigationService?.Navigate(new CertificatesPage(_currentPolicy));
        }

        private void Vaccinations_Click(object sender, MouseButtonEventArgs e)
        {
            NavigationService?.Navigate(new VaccinationsPage(_currentPolicy));
        }

        private void Payments_Click(object sender, MouseButtonEventArgs e)
        {
            MainWindow.Instance?.NavigateToPaymentHistory(_currentPolicy);
        }

        // === 🔧 ТЕСТ ОТПРАВКИ ПОЧТЫ ===
        private void BtnTestEmail_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var emailService = new EmailService();
                string testEmail = !string.IsNullOrEmpty(_patient?.Email) ? _patient.Email : "rregginks@gmail.com";

                var result = MessageBox.Show(
                    $"Отправить тестовое письмо на:\n{testEmail}?",
                    "🔧 Тест отправки почты",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Question);

                if (result == MessageBoxResult.Yes)
                {
                    emailService.TestSendEmail(testEmail);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка теста: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // === 📧 ОТПРАВКА ВСЕХ ДОКУМЕНТОВ НА ПОЧТУ ===

        private void BtnSendAll_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_patient?.Email))
            {
                MessageBox.Show("У пациента не указан Email. Введите его вручную.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
            }

            var inputWindow = new Window
            {
                Title = "Отправка медицинских документов",
                Width = 450,
                Height = 250,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var label = new TextBlock
            {
                Text = "Введите Email для получения всех документов:",
                FontSize = 14,
                Margin = new Thickness(20, 20, 20, 10),
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(label, 0);
            Grid.SetColumnSpan(label, 3);

            var textBox = new TextBox
            {
                FontSize = 16,
                Height = 40,
                Margin = new Thickness(20, 0, 20, 20),
                Text = _patient?.Email ?? ""
            };
            Grid.SetRow(textBox, 1);
            Grid.SetColumnSpan(textBox, 3);

            var btnOk = new Button
            {
                Content = "📧 Отправить",
                Width = 110,
                Height = 40,
                Margin = new Thickness(20, 0, 10, 20),
                Background = new SolidColorBrush(Color.FromRgb(46, 204, 113)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold
            };
            Grid.SetRow(btnOk, 2);
            Grid.SetColumn(btnOk, 1);

            var btnCancel = new Button
            {
                Content = "Отмена",
                Width = 100,
                Height = 40,
                Margin = new Thickness(10, 0, 20, 20),
                Background = Brushes.LightGray,
                Foreground = Brushes.Black
            };
            Grid.SetRow(btnCancel, 2);
            Grid.SetColumn(btnCancel, 2);

            string resultEmail = null;

            btnOk.Click += (s, args) =>
            {
                if (!string.IsNullOrWhiteSpace(textBox.Text) && textBox.Text.Contains("@"))
                {
                    resultEmail = textBox.Text;
                    inputWindow.DialogResult = true;
                    inputWindow.Close();
                }
                else
                {
                    MessageBox.Show("Введите корректный Email адрес", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
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

            if (dialogResult == true && !string.IsNullOrEmpty(resultEmail))
            {
                SendAllToEmail(resultEmail);
            }
        }

        private async void SendAllToEmail(string email)
        {
            try
            {
                var loadingWindow = new Window
                {
                    Title = "Отправка документов",
                    Width = 400,
                    Height = 200,
                    WindowStartupLocation = WindowStartupLocation.CenterScreen,
                    Content = new StackPanel
                    {
                        Margin = new Thickness(20),
                        Children =
                        {
                            new TextBlock
                            {
                                Text = "Генерация документов и отправка...\nПожалуйста, подождите.",
                                FontSize = 16,
                                TextWrapping = TextWrapping.Wrap,
                                Margin = new Thickness(0, 0, 0, 15)
                            },
                            new ProgressBar
                            {
                                IsIndeterminate = true,
                                Height = 20
                            }
                        }
                    }
                };

                loadingWindow.Show();

                var emailService = new EmailService();

                using (var db = new YanaulCRBEntities2())
                {
                    var patient = db.MedicalCards.FirstOrDefault(p => p.PolicyNumber == _currentPolicy);

                    if (patient == null)
                    {
                        loadingWindow.Close();
                        MessageBox.Show("Пациент не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }

                    await Task.Run(() =>
                    {
                        emailService.SendDocumentsToPatient(email, patient, db);
                    });
                }

                loadingWindow.Close();

                using (var db = new YanaulCRBEntities2())
                {
                    var notifications = db.Notifications
                        .Where(n => n.PatientPolicy == _currentPolicy && n.IsRead == false)
                        .ToList();

                    foreach (var n in notifications)
                    {
                        n.IsRead = true;
                    }
                    db.SaveChanges();
                }

                LoadNotifications();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при отправке: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnPrintAll_Click(object sender, RoutedEventArgs e)
        {
            PrintDialog printDialog = new PrintDialog();
            if (printDialog.ShowDialog() == true)
            {
                MessageBox.Show("Документы отправлены на принтер", "Печать", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnViewAllNotifications_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show($"Всего unread уведомлений: {_notificationsCount}", "Уведомления", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
                MainWindow.Instance.NavigateToMainMenu();
            else if (NavigationService != null && NavigationService.CanGoBack)
                NavigationService.GoBack();
        }

        public void UpdateLanguage()
        {
            if (BtnBack != null)
                BtnBack.Content = MainWindow.IsBashkir ? "⇦ Кире" : "⇦ Назад";
        }

        // ==================== CHECK-IN (Я ПРИШЁЛ) → живая очередь ====================
        private void BtnCheckIn_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(_currentPolicy))
            {
                MessageBox.Show("Ошибка: Полис не определен.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            try
            {
                var queueService = new Services.QueueService();
                var queueItem = queueService.RegisterCheckIn(_currentPolicy);

                if (queueItem != null)
                {
                    var status = queueService.GetPatientQueueStatus(_currentPolicy);
                    MessageBox.Show(
                        $"✅ Вы в живой очереди!\n\n" +
                        $"Позиция: № {queueItem.QueuePosition}\n" +
                        $"Врач: {status.DoctorName}\n" +
                        $"Кабинет: {status.Room}\n" +
                        $"Ожидание: ~{queueItem.EstimatedWaitMinutes ?? 15} мин\n\n" +
                        $"Следите за экраном «Виртуальная очередь» — вас вызовут голосом.",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("На сегодня у вас нет активной записи к врачу.", "Инфо", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка регистрации: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}