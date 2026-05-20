using System;
using System.Collections.ObjectModel;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using iTextSharp.text;
using iTextSharp.text.pdf;
using System.IO;
using Font = iTextSharp.text.Font;
using Paragraph = iTextSharp.text.Paragraph;
using Phrase = iTextSharp.text.Phrase;
using PdfPCell = iTextSharp.text.pdf.PdfPCell;
using PdfPTable = iTextSharp.text.pdf.PdfPTable;
using BaseColor = iTextSharp.text.BaseColor;
using YanaulKioskPerfect.Services;

namespace YanaulKioskPerfect
{
    public partial class DoctorWorkplacePage : Page
    {
        private MedicalCard _currentPatient;
        private Doctor _currentDoctor;
        private string _selectedChatPolicy;
        private readonly ChatService _chatService = new ChatService();
        private readonly QueueService _queueService = new QueueService();
        private readonly DispatcherTimer _chatRefreshTimer;
        private readonly DispatcherTimer _queueRefreshTimer;
        private readonly bool _openChatTab;
        private ObservableCollection<MessageViewModel> DoctorMessages { get; set; } = new ObservableCollection<MessageViewModel>();

        public DoctorWorkplacePage() : this(false) { }

        /// <param name="openChatTab">true — сразу открыть вкладку «Чат с пациентами»</param>
        public DoctorWorkplacePage(bool openChatTab)
        {
            _openChatTab = openChatTab;
            InitializeComponent();
            LoadCurrentDoctor();
            LoadMedicinesForCombo();
            DoctorMessagesList.ItemsSource = DoctorMessages;
            Loaded += DoctorWorkplacePage_Loaded;

            _chatRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(4) };
            _chatRefreshTimer.Tick += (s, e) => LoadChatRequests();

            _queueRefreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(5) };
            _queueRefreshTimer.Tick += (s, e) => LoadLiveQueue();
        }

        private void DoctorWorkplacePage_Loaded(object sender, RoutedEventArgs e)
        {
            LoadChatRequests();
            LoadLiveQueue();
            _chatRefreshTimer.Start();
            _queueRefreshTimer.Start();

            if (_openChatTab && MainTabControl != null && MainTabControl.Items.Count > 2)
                MainTabControl.SelectedIndex = 2;
        }

        private void LoadLiveQueue()
        {
            if (_currentDoctor == null || LiveQueueGrid == null) return;
            try
            {
                var items = _queueService.GetLiveQueueForDoctor(_currentDoctor.DoctorId);
                LiveQueueGrid.ItemsSource = items;
                if (LblQueueDoctor != null)
                    LblQueueDoctor.Text = $"Врач: {_currentDoctor.FullNameRU} • каб. {_currentDoctor.Room} • в очереди: {items.Count}";
            }
            catch { }
        }

        private void BtnRefreshQueue_Click(object sender, RoutedEventArgs e) => LoadLiveQueue();

        private void BtnCallNextPatient_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDoctor == null)
            {
                MessageBox.Show("Врач не определён в системе.", "Очередь", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var result = _queueService.CallNextPatient(_currentDoctor.DoctorId);
            if (result.Success)
            {
                LblLastCalled.Text = $"✅ Вызван: {result.PatientName} • талон {result.TicketNumber} → каб. {result.Room}";
                LoadLiveQueue();

                if (!string.IsNullOrEmpty(result.PatientName))
                {
                    SearchPolicyBox.Text = "";
                    using (var db = new YanaulCRBEntities2())
                    {
                        var appt = db.Appointments
                            .Where(a => a.TicketNumber == result.TicketNumber && a.AppointmentTime >= DateTime.Now.Date)
                            .FirstOrDefault();
                        if (appt != null)
                        {
                            SearchPolicyBox.Text = appt.PolicyNumber;
                            SearchPolicyBox_TextChanged(null, null);
                        }
                    }
                }
            }
            else
            {
                MessageBox.Show(result.Message, "Очередь", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void LoadCurrentDoctor()
        {
            using (var db = new YanaulCRBEntities2())
            {
                _currentDoctor = db.Doctors.FirstOrDefault();
                if (_currentDoctor == null)
                    MessageBox.Show("Внимание: В базе нет врачей!", "Предупреждение", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadMedicinesForCombo()
        {
            MedicineComboBox.ItemsSource = new[] {
                "Амоксициллин 500мг", "Арбидол 100мг", "Аспирин 500мг", "Ибупрофен 200мг",
                "Парацетамол 500мг", "Ципрофлоксацин 500мг", "Омез 20мг", "Нурофен 400мг",
                "Сумамед 500мг", "Эреспал 80мг", "Азитромицин 500мг", "Лоратадин 10мг"
            };
            if (MedicineComboBox.Items.Count > 0) MedicineComboBox.SelectedIndex = 0;
        }

        private void SearchPolicyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string policy = SearchPolicyBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(policy)) return;

            using (var db = new YanaulCRBEntities2())
            {
                _currentPatient = db.MedicalCards.FirstOrDefault(p => p.PolicyNumber == policy);

                if (_currentPatient != null)
                {
                    FoundPatientName.Text = _currentPatient.PatientName;
                    int age = DateTime.Now.Year - _currentPatient.DateOfBirth.Value.Year;
                    FoundPatientAge.Text = $"{age} лет | Полис: {_currentPatient.PolicyNumber}";
                    FoundPatientName.Foreground = Brushes.Green;

                    LoadPatientHistory(_currentPatient.PolicyNumber);
                    SetButtonsEnabled(_currentDoctor != null);

                    DosageTextBox.Clear();
                    DurationTextBox.Text = "7";
                }
                else
                {
                    FoundPatientName.Text = "Пациент не найден";
                    FoundPatientAge.Text = "";
                    FoundPatientName.Foreground = Brushes.Red;
                    HistoryGrid.ItemsSource = null;
                    SetButtonsEnabled(false);
                }
            }
        }

        private void SetButtonsEnabled(bool isEnabled)
        {
            BtnFinish.IsEnabled = isEnabled;
            BtnRefer.IsEnabled = isEnabled;
            BtnSickLeave.IsEnabled = isEnabled;
            BtnPrescribe.IsEnabled = isEnabled;
        }

        private void LoadPatientHistory(string policy)
        {
            using (var db = new YanaulCRBEntities2())
            {
                DateTime todayStart = DateTime.Now.Date;
                DateTime todayEnd = todayStart.AddDays(1);

                var history = db.Appointments
                    .Include("Doctor")
                    .Where(a => a.PolicyNumber == policy && a.AppointmentTime < todayStart)
                    .OrderByDescending(a => a.AppointmentTime)
                    .Take(10)
                    .ToList();

                HistoryGrid.ItemsSource = history;
            }
        }

        private void TemplateCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ConclusionBox == null) return;
            if (TemplateCombo.SelectedItem is ComboBoxItem item)
            {
                ConclusionBox.Text = item.Tag?.ToString() ?? "";
            }
        }

        // ============================================================================
        // ЗАВЕРШЕНИЕ ПРИЕМА
        // ============================================================================
        private void FinishAppointment_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDoctor == null || _currentPatient == null || string.IsNullOrWhiteSpace(ConclusionBox.Text))
            {
                MessageBox.Show("Заполните все поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    DateTime todayStart = DateTime.Now.Date;
                    DateTime todayEnd = todayStart.AddDays(1);

                    var todayAppt = db.Appointments
                        .FirstOrDefault(a => a.PolicyNumber == _currentPatient.PolicyNumber
                                          && a.AppointmentTime >= todayStart
                                          && a.AppointmentTime < todayEnd);

                    if (todayAppt != null)
                    {
                        todayAppt.Diagnosis = ConclusionBox.Text;
                        todayAppt.Status = "Завершен";
                    }
                    else
                    {
                        var newAppt = new Appointment
                        {
                            PolicyNumber = _currentPatient.PolicyNumber,
                            PatientName = _currentPatient.PatientName,
                            DoctorId = _currentDoctor.DoctorId,
                            AppointmentTime = DateTime.Now,
                            Status = "Завершен",
                            Diagnosis = ConclusionBox.Text,
                            TicketNumber = "AUTO-" + DateTime.Now.ToString("HHmmss")
                        };
                        db.Appointments.Add(newAppt);
                    }

                    var analysisResult = new AnalysisResult
                    {
                        PolicyNumber = _currentPatient.PolicyNumber,
                        AnalysisId = 1,
                        OrderDate = DateTime.Now,
                        ResultDate = DateTime.Now,
                        StatusRU = "Готов",
                        StatusBA = "Әҙер",
                        Details = ConclusionBox.Text,
                        DoctorComment = RecommendationsBox.Text
                    };
                    db.AnalysisResults.Add(analysisResult);

                    string pdfPath = GenerateMedicalReportPDF(_currentPatient, ConclusionBox.Text, RecommendationsBox.Text);

                    db.Notifications.Add(new Notification
                    {
                        PatientPolicy = _currentPatient.PolicyNumber,
                        Message = "Сформирован новый документ: Протокол осмотра",
                        Type = "справка",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });

                    db.SaveChanges();

                    _queueService.CompleteQueueForPolicy(_currentPatient.PolicyNumber);
                    LoadLiveQueue();

                    MessageBox.Show("Приём завершён! Пациент снят с очереди. Документ открыт.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Diagnostics.Process.Start(pdfPath);

                    ConclusionBox.Clear();
                    RecommendationsBox.Clear();
                    TemplateCombo.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================================
        // НАПРАВЛЕНИЕ
        // ============================================================================
        private void Refer_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDoctor == null || _currentPatient == null) return;

            var referWindow = new Window
            {
                Title = "Выдача направления",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelSpecialty = new TextBlock { Text = "Выберите специалиста:", Margin = new Thickness(20, 20, 20, 10), FontWeight = FontWeights.Bold };
            Grid.SetRow(labelSpecialty, 0);

            var comboSpecialty = new ComboBox
            {
                Margin = new Thickness(20, 0, 20, 20),
                Height = 40,
                FontSize = 14,
                ItemsSource = new[] {
                    "Терапевт", "Хирург", "Кардиолог", "Невролог", "Офтальмолог",
                    "ЛОР", "Дерматолог", "Эндокринолог", "Гастроэнтеролог",
                    "Уролог", "Гинеколог", "Педиатр", "Онколог", "Ревматолог"
                }
            };
            Grid.SetRow(comboSpecialty, 1);

            var labelReason = new TextBlock { Text = "Причина направления:", Margin = new Thickness(20, 0, 20, 10), FontWeight = FontWeights.Bold };
            Grid.SetRow(labelReason, 2);

            var textReason = new TextBox { Margin = new Thickness(20, 0, 20, 20), Height = 80, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, FontSize = 14 };
            Grid.SetRow(textReason, 3);

            var btnOk = new Button { Content = "Выдать направление", Width = 150, Height = 40, Margin = new Thickness(0, 0, 20, 20), HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Orange, Foreground = Brushes.White, FontWeight = FontWeights.Bold };
            Grid.SetRow(btnOk, 4);

            string selectedSpecialty = null;
            string reason = null;

            btnOk.Click += (s, args) =>
            {
                selectedSpecialty = comboSpecialty.SelectedItem as string;
                reason = textReason.Text;

                if (string.IsNullOrEmpty(selectedSpecialty))
                {
                    MessageBox.Show("Выберите специалиста!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                referWindow.DialogResult = true;
                referWindow.Close();
            };

            grid.Children.Add(labelSpecialty);
            grid.Children.Add(comboSpecialty);
            grid.Children.Add(labelReason);
            grid.Children.Add(textReason);
            grid.Children.Add(btnOk);

            referWindow.Content = grid;

            bool? dialogResult = referWindow.ShowDialog();

            if (dialogResult == true && !string.IsNullOrEmpty(selectedSpecialty))
            {
                try
                {
                    using (var db = new YanaulCRBEntities2())
                    {
                        var referral = new Referral
                        {
                            PatientPolicy = _currentPatient.PolicyNumber,
                            DoctorId = _currentDoctor.DoctorId,
                            IssueDate = DateTime.Now,
                            SpecialtyTarget = selectedSpecialty,
                            Reason = reason ?? "Консультация",
                            Status = "Активно"
                        };
                        db.Referrals.Add(referral);

                        var certificate = new Certificate
                        {
                            PatientPolicy = _currentPatient.PolicyNumber,
                            DoctorId = _currentDoctor.DoctorId,
                            IssueDate = DateTime.Now,
                            CertificateType = "Направление",
                            CertificateText = $"Направляется к врачу специальности: {selectedSpecialty}\n\nПричина: {reason ?? "Консультация"}",
                            ValidUntil = DateTime.Now.AddDays(30),
                            Status = "активен"
                        };
                        db.Certificates.Add(certificate);

                        string pdfPath = GenerateReferralPDF(_currentPatient, selectedSpecialty, reason ?? "Консультация");

                        db.Notifications.Add(new Notification
                        {
                            PatientPolicy = _currentPatient.PolicyNumber,
                            Message = $"Выдано направление к врачу: {selectedSpecialty}",
                            Type = "справка",
                            IsRead = false,
                            CreatedAt = DateTime.Now
                        });

                        db.SaveChanges();
                        MessageBox.Show("Направление выдано! Документ открыт.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        System.Diagnostics.Process.Start(pdfPath);
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка"); }
            }
        }

        // ============================================================================
        // БОЛЬНИЧНЫЙ ЛИСТ
        // ============================================================================
        private void SickLeave_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDoctor == null || _currentPatient == null) return;

            var sickWindow = new Window
            {
                Title = "Открытие больничного листа",
                Width = 600,
                Height = 500,
                WindowStartupLocation = WindowStartupLocation.CenterScreen,
                ResizeMode = ResizeMode.NoResize
            };

            var grid = new Grid();
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var labelDays = new TextBlock { Text = "На сколько дней открыть больничный?", Margin = new Thickness(20, 20, 20, 10), FontWeight = FontWeights.Bold };
            Grid.SetRow(labelDays, 0);

            var textDays = new TextBox { Margin = new Thickness(20, 0, 20, 20), Height = 40, Text = "5", FontSize = 14 };
            Grid.SetRow(textDays, 1);

            var labelDiagnosis = new TextBlock { Text = "Диагноз:", Margin = new Thickness(20, 0, 20, 10), FontWeight = FontWeights.Bold };
            Grid.SetRow(labelDiagnosis, 2);

            var textDiagnosis = new TextBox { Margin = new Thickness(20, 0, 20, 20), Height = 80, TextWrapping = TextWrapping.Wrap, AcceptsReturn = true, Text = ConclusionBox.Text, FontSize = 14 };
            Grid.SetRow(textDiagnosis, 3);

            var btnOk = new Button { Content = "Открыть больничный", Width = 150, Height = 40, Margin = new Thickness(0, 0, 20, 20), HorizontalAlignment = HorizontalAlignment.Right, Background = Brushes.Red, Foreground = Brushes.White, FontWeight = FontWeights.Bold };
            Grid.SetRow(btnOk, 4);

            int days = 5;
            string diagnosis = "";

            btnOk.Click += (s, args) =>
            {
                if (!int.TryParse(textDays.Text, out days) || days <= 0 || days > 30)
                {
                    MessageBox.Show("Введите корректное количество дней (от 1 до 30)!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                diagnosis = textDiagnosis.Text;
                sickWindow.DialogResult = true;
                sickWindow.Close();
            };

            grid.Children.Add(labelDays);
            grid.Children.Add(textDays);
            grid.Children.Add(labelDiagnosis);
            grid.Children.Add(textDiagnosis);
            grid.Children.Add(btnOk);

            sickWindow.Content = grid;

            bool? dialogResult = sickWindow.ShowDialog();

            if (dialogResult == true)
            {
                try
                {
                    using (var db = new YanaulCRBEntities2())
                    {
                        var sickLeave = new SickLeave
                        {
                            PatientPolicy = _currentPatient.PolicyNumber,
                            DoctorId = _currentDoctor.DoctorId,
                            IssueDate = DateTime.Now,
                            StartDate = DateTime.Now,
                            EndDate = DateTime.Now.AddDays(days),
                            Reason = diagnosis,
                            Status = "Открыт"
                        };
                        db.SickLeaves.Add(sickLeave);

                        var certificate = new Certificate
                        {
                            PatientPolicy = _currentPatient.PolicyNumber,
                            DoctorId = _currentDoctor.DoctorId,
                            IssueDate = DateTime.Now,
                            CertificateType = "Листок нетрудоспособности",
                            CertificateText = $"Период нетрудоспособности:\nс {sickLeave.StartDate:dd.MM.yyyy} по {sickLeave.EndDate:dd.MM.yyyy}\n\nДиагноз: {diagnosis}",
                            ValidUntil = sickLeave.EndDate,
                            Status = "активен"
                        };
                        db.Certificates.Add(certificate);

                        string pdfPath = GenerateSickLeavePDF(_currentPatient, diagnosis, days);

                        db.Notifications.Add(new Notification
                        {
                            PatientPolicy = _currentPatient.PolicyNumber,
                            Message = $"Открыт больничный лист до {sickLeave.EndDate:dd.MM.yyyy}",
                            Type = "справка",
                            IsRead = false,
                            CreatedAt = DateTime.Now
                        });

                        db.SaveChanges();
                        MessageBox.Show("Больничный открыт! Документ открыт.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                        System.Diagnostics.Process.Start(pdfPath);
                    }
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "Ошибка"); }
            }
        }

        // ============================================================================
        // РЕЦЕПТ
        // ============================================================================
        private void Prescribe_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDoctor == null || _currentPatient == null)
            {
                MessageBox.Show("Найдите пациента!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string medicine = MedicineComboBox.SelectedItem as string;
            string dosage = DosageTextBox.Text.Trim();
            string duration = DurationTextBox.Text.Trim();

            if (string.IsNullOrEmpty(medicine) || string.IsNullOrEmpty(dosage) || string.IsNullOrEmpty(duration))
            {
                MessageBox.Show("Заполните все поля рецепта!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int days;
            if (!int.TryParse(duration, out days) || days <= 0)
            {
                MessageBox.Show("Укажите корректный срок лечения (дни)!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var prescription = new Prescription
                    {
                        PolicyNumber = _currentPatient.PolicyNumber,
                        DoctorId = _currentDoctor.DoctorId,
                        IssueDate = DateTime.Now,
                        ValidUntil = DateTime.Now.AddDays(days),
                        Medicines = $"{medicine} ({dosage})",
                        DosageInstructions = $"Принимать в течение {days} дней по схеме: {dosage}",
                        Status = "активен"
                    };
                    db.Prescriptions.Add(prescription);

                    string pdfPath = GeneratePrescriptionPDF(_currentPatient, medicine, dosage, days);

                    db.Notifications.Add(new Notification
                    {
                        PatientPolicy = _currentPatient.PolicyNumber,
                        Message = $"Вам выписан новый рецепт: {medicine}",
                        Type = "рецепт",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });

                    db.SaveChanges();
                    MessageBox.Show($"Рецепт выписан! Документ открыт.", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
                    System.Diagnostics.Process.Start(pdfPath);

                    DosageTextBox.Clear();
                    DurationTextBox.Text = "7";
                }
            }
            catch (Exception ex)
            {
                string errorMsg = "Ошибка при сохранении рецепта.\n\n";
                if (ex.InnerException != null) errorMsg += "Подробности: " + ex.InnerException.Message;
                else errorMsg += ex.Message;
                MessageBox.Show(errorMsg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ============================================================================
        // === МЕТОДЫ ДЛЯ ЧАТА (НОВЫЕ) ===
        // ============================================================================

        private void BtnRefreshChat_Click(object sender, RoutedEventArgs e)
        {
            LoadChatRequests();
        }

        private void ChatRequestsGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            OpenSelectedChatPatient();
        }

        private void ChatRequestsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            // одиночный клик тоже открывает диалог
            if (ChatRequestsGrid.SelectedItem is ChatConversationItem)
                OpenSelectedChatPatient();
        }

        private void OpenSelectedChatPatient()
        {
            if (ChatRequestsGrid.SelectedItem is ChatConversationItem selected
                && !string.IsNullOrWhiteSpace(selected.Policy))
            {
                LoadDoctorChat(ChatService.NormalizePolicy(selected.Policy));
            }
        }

        private void LoadChatRequests()
        {
            try
            {
                var requests = _chatService.LoadConversations();
                ChatRequestsGrid.ItemsSource = requests;

                int totalUnread = requests.Sum(r => r.UnreadCount);
                if (UnreadCountBadge != null)
                {
                    var badgeText = UnreadCountBadge.Child as TextBlock;
                    if (badgeText != null)
                    {
                        badgeText.Text = totalUnread > 0 ? totalUnread.ToString() : "";
                        UnreadCountBadge.Visibility = totalUnread > 0 ? Visibility.Visible : Visibility.Collapsed;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось загрузить обращения: " + ex.Message, "Чат", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void LoadDoctorChat(string policy)
        {
            _selectedChatPolicy = policy;
            DoctorMessages.Clear();

            using (var db = new YanaulCRBEntities2())
            {
                var patient = db.MedicalCards.FirstOrDefault(p => p.PolicyNumber == policy);
                ChatPatientName.Text = patient?.PatientName ?? "Пациент";
                ChatPatientPolicy.Text = $"Полис: {policy}";
            }

            foreach (var vm in _chatService.LoadHistory(policy, doctorView: true))
                DoctorMessages.Add(vm);

            _chatService.MarkPatientMessagesRead(policy);
            DoctorMessagesScroll.ScrollToEnd();

            DoctorInput.IsEnabled = true;
            BtnSendDoctorMessage.IsEnabled = true;
            DoctorInput.Focus();
        }

        private void BtnSendDoctorMessage_Click(object sender, RoutedEventArgs e)
        {
            SendDoctorMessage();
        }

        private void SendDoctorMessage()
        {
            if (string.IsNullOrWhiteSpace(_selectedChatPolicy) || string.IsNullOrWhiteSpace(DoctorInput.Text)) return;

            string text = DoctorInput.Text.Trim();
            try
            {
                var saved = _chatService.SaveMessage(_selectedChatPolicy, ChatSenderKind.Doctor, text, isRead: false);
                DoctorMessages.Add(ChatService.ToViewModel(saved, doctorView: true));
                DoctorInput.Clear();
                DoctorMessagesScroll.ScrollToEnd();

                string doctorName = _currentDoctor?.FullNameRU ?? "Врач";
                _chatService.NotifyPatientAboutDoctorReply(_selectedChatPolicy, doctorName);
                LoadChatRequests();

                MessageBox.Show(
                    $"Ответ отправлен пациенту.\nПолис: {_selectedChatPolicy}\n\nПациент увидит сообщение в «Чат поддержки» на терминале.",
                    "Сообщение доставлено",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось сохранить ответ в базу данных.\n\n" + ex.Message +
                    "\n\nУбедитесь, что полис пациента есть в таблице MedicalCard.",
                    "Ошибка чата",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void DoctorInput_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
            {
                e.Handled = true;
                SendDoctorMessage();
            }
        }

        // ============================================================================
        // PDF ГЕНЕРАТОРЫ
        // ============================================================================

        private string GenerateMedicalReportPDF(MedicalCard patient, string diagnosis, string recommendations)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"Протокол_{patient.PolicyNumber}_{DateTime.Now:yyyyMMddHHmm}.pdf");
            Document doc = new Document(PageSize.A4, 40, 40, 40, 40);

            using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                string fontPath = @"C:\Windows\Fonts\ARIAL.TTF";
                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font titleFont = new Font(baseFont, 16, Font.BOLD);
                Font headerFont = new Font(baseFont, 14, Font.BOLD);
                Font textFont = new Font(baseFont, 12, Font.NORMAL);
                Font boldFont = new Font(baseFont, 12, Font.BOLD);

                Paragraph title = new Paragraph("ГБУЗ \"ЯНАУЛЬСКАЯ ЦЕНТРАЛЬНАЯ РАЙОННАЯ БОЛЬНИЦА\"", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                doc.Add(title);

                Paragraph sub = new Paragraph("ПРОТОКОЛ МЕДИЦИНСКОГО ОСМОТРА", headerFont);
                sub.Alignment = Element.ALIGN_CENTER;
                sub.SpacingAfter = 30f;
                doc.Add(sub);

                PdfPTable infoTable = new PdfPTable(2);
                infoTable.WidthPercentage = 100;

                AddRow(infoTable, "ФИО пациента:", patient.PatientName, boldFont, textFont);
                AddRow(infoTable, "Дата рождения:", patient.DateOfBirth?.ToString("dd.MM.yyyy"), boldFont, textFont);
                AddRow(infoTable, "Полис ОМС:", patient.PolicyNumber, boldFont, textFont);
                AddRow(infoTable, "Дата приема:", DateTime.Now.ToString("dd.MM.yyyy HH:mm"), boldFont, textFont);
                AddRow(infoTable, "Врач:", _currentDoctor?.FullNameRU ?? "Дежурный врач", boldFont, textFont);

                doc.Add(infoTable);
                doc.Add(new Paragraph("\n"));

                Paragraph diagTitle = new Paragraph("ДИАГНОЗ:", headerFont);
                doc.Add(diagTitle);
                doc.Add(new Paragraph(diagnosis, textFont));
                doc.Add(new Paragraph("\n"));

                if (!string.IsNullOrEmpty(recommendations))
                {
                    Paragraph recTitle = new Paragraph("РЕКОМЕНДАЦИИ:", headerFont);
                    doc.Add(recTitle);
                    doc.Add(new Paragraph(recommendations, textFont));
                    doc.Add(new Paragraph("\n"));
                }

                PdfPTable footerTable = new PdfPTable(2);
                footerTable.WidthPercentage = 100;
                footerTable.TotalWidth = 400f;
                footerTable.HorizontalAlignment = Element.ALIGN_LEFT;
                footerTable.DefaultCell.Border = Rectangle.NO_BORDER;

                PdfPCell signCell = new PdfPCell();
                signCell.Border = Rectangle.NO_BORDER;
                Paragraph signPara = new Paragraph($"Врач: _________________ / {_currentDoctor?.FullNameRU ?? ""}", textFont);
                signPara.SpacingBefore = 20f;
                signCell.AddElement(signPara);
                footerTable.AddCell(signCell);

                DrawOfficialStamp(writer, baseFont);

                PdfPCell stampCell = new PdfPCell();
                stampCell.Border = Rectangle.NO_BORDER;
                stampCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                footerTable.AddCell(stampCell);

                doc.Add(footerTable);
                doc.Close();
            }
            return tempPath;
        }

        private string GenerateReferralPDF(MedicalCard patient, string specialty, string reason)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"Направление_{patient.PolicyNumber}_{DateTime.Now:yyyyMMddHHmm}.pdf");
            Document doc = new Document(PageSize.A4, 40, 40, 40, 40);

            using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                string fontPath = @"C:\Windows\Fonts\ARIAL.TTF";
                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font titleFont = new Font(baseFont, 16, Font.BOLD);
                Font headerFont = new Font(baseFont, 14, Font.BOLD);
                Font textFont = new Font(baseFont, 12, Font.NORMAL);
                Font boldFont = new Font(baseFont, 12, Font.BOLD);

                Paragraph title = new Paragraph("ГБУЗ \"ЯНАУЛЬСКАЯ ЦЕНТРАЛЬНАЯ РАЙОННАЯ БОЛЬНИЦА\"", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                doc.Add(title);

                Paragraph sub = new Paragraph("НАПРАВЛЕНИЕ К СПЕЦИАЛИСТУ", headerFont);
                sub.Alignment = Element.ALIGN_CENTER;
                sub.SpacingAfter = 30f;
                doc.Add(sub);

                PdfPTable infoTable = new PdfPTable(2);
                infoTable.WidthPercentage = 100;

                AddRow(infoTable, "ФИО пациента:", patient.PatientName, boldFont, textFont);
                AddRow(infoTable, "Дата рождения:", patient.DateOfBirth?.ToString("dd.MM.yyyy"), boldFont, textFont);
                AddRow(infoTable, "Полис ОМС:", patient.PolicyNumber, boldFont, textFont);
                AddRow(infoTable, "Дата выдачи:", DateTime.Now.ToString("dd.MM.yyyy"), boldFont, textFont);
                AddRow(infoTable, "Направлен к врачу:", specialty, boldFont, textFont);

                doc.Add(infoTable);
                doc.Add(new Paragraph("\n"));

                Paragraph reasonTitle = new Paragraph("ПРИЧИНА НАПРАВЛЕНИЯ:", headerFont);
                doc.Add(reasonTitle);
                doc.Add(new Paragraph(reason, textFont));
                doc.Add(new Paragraph("\n"));

                Paragraph validTitle = new Paragraph("СРОК ДЕЙСТВИЯ:", headerFont);
                doc.Add(validTitle);
                doc.Add(new Paragraph($"Действительно до: {DateTime.Now.AddDays(30):dd.MM.yyyy}", textFont));
                doc.Add(new Paragraph("\n"));

                PdfPTable footerTable = new PdfPTable(2);
                footerTable.WidthPercentage = 100;
                footerTable.TotalWidth = 400f;
                footerTable.HorizontalAlignment = Element.ALIGN_LEFT;
                footerTable.DefaultCell.Border = Rectangle.NO_BORDER;

                PdfPCell signCell = new PdfPCell();
                signCell.Border = Rectangle.NO_BORDER;
                Paragraph signPara = new Paragraph($"Врач: _________________ / {_currentDoctor?.FullNameRU ?? ""}", textFont);
                signPara.SpacingBefore = 20f;
                signCell.AddElement(signPara);
                footerTable.AddCell(signCell);

                DrawOfficialStamp(writer, baseFont);

                PdfPCell stampCell = new PdfPCell();
                stampCell.Border = Rectangle.NO_BORDER;
                stampCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                footerTable.AddCell(stampCell);

                doc.Add(footerTable);
                doc.Close();
            }
            return tempPath;
        }

        private string GenerateSickLeavePDF(MedicalCard patient, string diagnosis, int days)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"Больничный_{patient.PolicyNumber}_{DateTime.Now:yyyyMMddHHmm}.pdf");
            Document doc = new Document(PageSize.A4, 40, 40, 40, 40);

            using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                string fontPath = @"C:\Windows\Fonts\ARIAL.TTF";
                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font titleFont = new Font(baseFont, 16, Font.BOLD);
                Font headerFont = new Font(baseFont, 14, Font.BOLD);
                Font textFont = new Font(baseFont, 12, Font.NORMAL);
                Font boldFont = new Font(baseFont, 12, Font.BOLD);

                Paragraph title = new Paragraph("ГБУЗ \"ЯНАУЛЬСКАЯ ЦЕНТРАЛЬНАЯ РАЙОННАЯ БОЛЬНИЦА\"", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                doc.Add(title);

                Paragraph sub = new Paragraph("ЛИСТОК НЕТРУДОСПОСОБНОСТИ", headerFont);
                sub.Alignment = Element.ALIGN_CENTER;
                sub.SpacingAfter = 30f;
                doc.Add(sub);

                PdfPTable infoTable = new PdfPTable(2);
                infoTable.WidthPercentage = 100;

                AddRow(infoTable, "ФИО пациента:", patient.PatientName, boldFont, textFont);
                AddRow(infoTable, "Дата рождения:", patient.DateOfBirth?.ToString("dd.MM.yyyy"), boldFont, textFont);
                AddRow(infoTable, "Полис ОМС:", patient.PolicyNumber, boldFont, textFont);
                AddRow(infoTable, "Дата выдачи:", DateTime.Now.ToString("dd.MM.yyyy"), boldFont, textFont);

                doc.Add(infoTable);
                doc.Add(new Paragraph("\n"));

                Paragraph periodTitle = new Paragraph("ПЕРИОД НЕТРУДОСПОСОБНОСТИ:", headerFont);
                doc.Add(periodTitle);

                DateTime startDate = DateTime.Now;
                DateTime endDate = startDate.AddDays(days);

                Paragraph periodText = new Paragraph($"с {startDate:dd.MM.yyyy} по {endDate:dd.MM.yyyy} ({days} календарных дней)", textFont);
                periodText.SpacingAfter = 20f;
                doc.Add(periodText);

                Paragraph diagTitle = new Paragraph("ДИАГНОЗ:", headerFont);
                doc.Add(diagTitle);
                doc.Add(new Paragraph(diagnosis, textFont));
                doc.Add(new Paragraph("\n"));

                Paragraph recTitle = new Paragraph("РЕКОМЕНДАЦИИ:", headerFont);
                doc.Add(recTitle);
                doc.Add(new Paragraph("Домашний режим, лечение по назначению врача.\nЯвка на повторный прием: " + endDate.ToString("dd.MM.yyyy"), textFont));
                doc.Add(new Paragraph("\n"));

                PdfPTable footerTable = new PdfPTable(2);
                footerTable.WidthPercentage = 100;
                footerTable.TotalWidth = 400f;
                footerTable.HorizontalAlignment = Element.ALIGN_LEFT;
                footerTable.DefaultCell.Border = Rectangle.NO_BORDER;

                PdfPCell signCell = new PdfPCell();
                signCell.Border = Rectangle.NO_BORDER;
                Paragraph signPara = new Paragraph($"Врач: _________________ / {_currentDoctor?.FullNameRU ?? ""}", textFont);
                signPara.SpacingBefore = 20f;
                signCell.AddElement(signPara);
                footerTable.AddCell(signCell);

                DrawOfficialStamp(writer, baseFont);

                PdfPCell stampCell = new PdfPCell();
                stampCell.Border = Rectangle.NO_BORDER;
                stampCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                footerTable.AddCell(stampCell);

                doc.Add(footerTable);
                doc.Close();
            }
            return tempPath;
        }

        private string GeneratePrescriptionPDF(MedicalCard patient, string medicine, string dosage, int days)
        {
            string tempPath = Path.Combine(Path.GetTempPath(), $"Рецепт_{patient.PolicyNumber}_{DateTime.Now:yyyyMMddHHmm}.pdf");
            Document doc = new Document(PageSize.A4, 40, 40, 40, 40);

            using (FileStream fs = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                PdfWriter writer = PdfWriter.GetInstance(doc, fs);
                doc.Open();

                string fontPath = @"C:\Windows\Fonts\ARIAL.TTF";
                BaseFont baseFont = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font titleFont = new Font(baseFont, 18, Font.BOLD);
                Font headerFont = new Font(baseFont, 14, Font.BOLD);
                Font textFont = new Font(baseFont, 12, Font.NORMAL);
                Font boldFont = new Font(baseFont, 12, Font.BOLD);
                Font bigFont = new Font(baseFont, 16, Font.BOLD);

                Paragraph title = new Paragraph("ГБУЗ \"ЯНАУЛЬСКАЯ ЦЕНТРАЛЬНАЯ РАЙОННАЯ БОЛЬНИЦА\"", titleFont);
                title.Alignment = Element.ALIGN_CENTER;
                doc.Add(title);

                Paragraph sub = new Paragraph("МЕДИЦИНСКИЙ РЕЦЕПТ", headerFont);
                sub.Alignment = Element.ALIGN_CENTER;
                sub.SpacingAfter = 30f;
                doc.Add(sub);

                PdfPTable infoTable = new PdfPTable(2);
                infoTable.WidthPercentage = 100;

                AddRow(infoTable, "ФИО пациента:", patient.PatientName, boldFont, textFont);
                AddRow(infoTable, "Дата рождения:", patient.DateOfBirth?.ToString("dd.MM.yyyy"), boldFont, textFont);
                AddRow(infoTable, "Полис ОМС:", patient.PolicyNumber, boldFont, textFont);
                AddRow(infoTable, "Дата выписки:", DateTime.Now.ToString("dd.MM.yyyy"), boldFont, textFont);
                AddRow(infoTable, "Врач:", _currentDoctor?.FullNameRU ?? "Дежурный врач", boldFont, textFont);

                doc.Add(infoTable);
                doc.Add(new Paragraph("\n"));

                Paragraph medTitle = new Paragraph("НАЗНАЧЕНО:", headerFont);
                doc.Add(medTitle);

                Paragraph medName = new Paragraph(medicine.ToUpper(), bigFont);
                medName.SpacingAfter = 10f;
                doc.Add(medName);

                Paragraph doseTitle = new Paragraph("СХЕМА ПРИЕМА:", headerFont);
                doc.Add(doseTitle);

                string schemeText = $"Принимать по: {dosage}\nКурс лечения: {days} дн.\nДата окончания: {DateTime.Now.AddDays(days):dd.MM.yyyy}";
                doc.Add(new Paragraph(schemeText, textFont));
                doc.Add(new Paragraph("\n"));

                Paragraph recTitle = new Paragraph("РЕКОМЕНДАЦИИ ВРАЧА:", headerFont);
                doc.Add(recTitle);
                doc.Add(new Paragraph("Принимать после еды. Обильное питье. При ухудшении состояния обратиться к врачу.", textFont));
                doc.Add(new Paragraph("\n"));

                PdfPTable footerTable = new PdfPTable(2);
                footerTable.WidthPercentage = 100;
                footerTable.TotalWidth = 400f;
                footerTable.HorizontalAlignment = Element.ALIGN_LEFT;
                footerTable.DefaultCell.Border = Rectangle.NO_BORDER;

                PdfPCell signCell = new PdfPCell();
                signCell.Border = Rectangle.NO_BORDER;
                Paragraph signPara = new Paragraph($"Врач: _________________ / {_currentDoctor?.FullNameRU ?? ""}", textFont);
                signPara.SpacingBefore = 20f;
                signCell.AddElement(signPara);
                footerTable.AddCell(signCell);

                DrawOfficialStamp(writer, baseFont);

                PdfPCell stampCell = new PdfPCell();
                stampCell.Border = Rectangle.NO_BORDER;
                stampCell.HorizontalAlignment = Element.ALIGN_RIGHT;
                footerTable.AddCell(stampCell);

                doc.Add(footerTable);
                doc.Close();
            }
            return tempPath;
        }

        private void DrawOfficialStamp(PdfWriter writer, BaseFont baseFont)
        {
            PdfContentByte cb = writer.DirectContent;
            cb.SaveState();

            cb.SetColorStroke(BaseColor.RED);
            cb.SetLineWidth(3);
            cb.Circle(450, 150, 50);
            cb.Stroke();

            cb.SetLineWidth(1);
            cb.Circle(450, 150, 45);
            cb.Stroke();

            cb.BeginText();
            cb.SetFontAndSize(baseFont, 7);
            cb.SetColorFill(BaseColor.RED);

            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, "ГБУЗ ЯНАУЛЬСКАЯ ЦРБ", 450, 180, 0);
            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, "МИНИСТЕРСТВО ЗДРАВООХРАНЕНИЯ РБ", 450, 120, 0);

            cb.SetFontAndSize(baseFont, 12);
            cb.ShowTextAligned(PdfContentByte.ALIGN_CENTER, "М.П.", 450, 150, 0);

            cb.EndText();
            cb.RestoreState();
        }

        private void AddRow(PdfPTable table, string label, string value, Font labelFont, Font valueFont)
        {
            PdfPCell labelCell = new PdfPCell(new Phrase(label, labelFont));
            labelCell.Padding = 5;
            table.AddCell(labelCell);

            PdfPCell valueCell = new PdfPCell(new Phrase(value ?? "-", valueFont));
            valueCell.Padding = 5;
            table.AddCell(valueCell);
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            _chatRefreshTimer?.Stop();
            _queueRefreshTimer?.Stop();
            if (NavigationService != null && NavigationService.CanGoBack) NavigationService.GoBack();
        }
    }
}