using System;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YanaulKioskPerfect.Services;

namespace YanaulKioskPerfect
{
    public partial class AdminDashboardPage : Page
    {
        private Appointment _selectedAppointment;
        private HomeVisit _selectedHomeVisit;
        private readonly ChatService _chatService = new ChatService();
        private readonly QueueService _queueService = new QueueService();
        private readonly AdminStatisticsService _statsService = new AdminStatisticsService();
        private readonly FeedbackService _feedbackService = new FeedbackService();

        public AdminDashboardPage()
        {
            InitializeComponent();
            Loaded += (s, e) => RefreshAll();
        }

        private void RefreshAll()
        {
            LoadKpi();
            LoadQueue();
            LoadAllAppointments();
            LoadPatients();
            LoadPayments();
            LoadDoctors();
            LoadDispatcher();
            LoadFeedbacks();
            LoadNotifications();
            UpdateChatBadge();
        }

        private void LoadKpi()
        {
            var stats = _statsService.LoadStats();
            LblDbStatus.Text = stats.DatabaseStatus;
            KpiPatients.Text = stats.PatientsCount.ToString();
            KpiAppointments.Text = $"{stats.ActiveAppointmentsToday} / {stats.TodayAppointments}";
            KpiHomeVisits.Text = stats.PendingHomeVisits.ToString();
            KpiPayments.Text = stats.PaymentsTotal.ToString("N0") + " ₽";
            KpiFeedbacks.Text = stats.FeedbacksCount > 0
                ? $"{stats.FeedbacksCount} (★ {stats.AverageDoctorRating:F1})"
                : "0";
            KpiNotifications.Text = stats.UnreadNotifications.ToString();
        }

        private void LoadQueue()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var todayStart = DateTime.Now.Date;
                    var todayEnd = todayStart.AddDays(1);
                    QueueList.ItemsSource = db.Appointments
                        .Where(a => a.AppointmentTime >= todayStart && a.AppointmentTime < todayEnd && a.Status != "отменена")
                        .OrderBy(a => a.AppointmentTime)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Очередь: " + ex.Message);
            }
        }

        private void LoadAllAppointments()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    GridAllAppointments.ItemsSource = db.Appointments
                        .OrderByDescending(a => a.AppointmentTime)
                        .Take(200)
                        .ToList();
                }
            }
            catch { }
        }

        private void LoadPatients(string filter = null)
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var q = db.MedicalCards.AsQueryable();
                    if (!string.IsNullOrWhiteSpace(filter))
                    {
                        filter = filter.Trim();
                        q = q.Where(p => p.PolicyNumber.Contains(filter) || p.PatientName.Contains(filter));
                    }
                    GridPatients.ItemsSource = q.OrderBy(p => p.PatientName).Take(100).ToList();
                }
            }
            catch { }
        }

        private void LoadPayments()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var list = db.Payments.OrderByDescending(p => p.PaymentDate).Take(100).ToList();
                    GridPayments.ItemsSource = list;
                    var today = DateTime.Now.Date;
                    var todaySum = list.Where(p => p.PaymentDate >= today).Sum(p => p.Amount);
                    LblPaymentsSummary.Text = $"Показано {list.Count} операций. Сегодня: {todaySum:N0} ₽";
                }
            }
            catch { }
        }

        private void LoadDoctors()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                    GridDoctors.ItemsSource = db.Doctors.OrderBy(d => d.SpecialtyRU).ToList();
            }
            catch { }
        }

        private void LoadDispatcher()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    GridHomeVisits.ItemsSource = db.HomeVisits.OrderByDescending(h => h.CreatedAt).Take(50).ToList();
                    GridTriage.ItemsSource = db.TriageRecords.OrderByDescending(t => t.CreatedAt).Take(50).ToList();
                }
            }
            catch { }
        }

        private void LoadFeedbacks()
        {
            try
            {
                GridFeedbacks.ItemsSource = _feedbackService.GetRecentFeedbacks(50);
            }
            catch
            {
                GridFeedbacks.ItemsSource = null;
            }
        }

        private void LoadNotifications()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    GridNotifications.ItemsSource = db.Notifications
                        .OrderByDescending(n => n.CreatedAt)
                        .Take(80)
                        .ToList();
                }
            }
            catch { }
        }

        private void UpdateChatBadge()
        {
            try
            {
                int unread = _chatService.LoadConversations().Sum(c => c.UnreadCount);
                if (unread > 0)
                {
                    AdminChatBadgeText.Text = unread.ToString();
                    AdminChatBadge.Visibility = Visibility.Visible;
                }
                else
                    AdminChatBadge.Visibility = Visibility.Collapsed;
            }
            catch
            {
                AdminChatBadge.Visibility = Visibility.Collapsed;
            }
        }

        private void NavigateAdmin(Page page) => MainWindow.Instance?.MainFrame.Navigate(page);

        private void BtnOpenChat_Click(object sender, RoutedEventArgs e)
            => NavigateAdmin(new DoctorWorkplacePage(openChatTab: true));

        private void BtnDoctorWorkplace_Click(object sender, RoutedEventArgs e)
            => NavigateAdmin(new DoctorWorkplacePage(openChatTab: false));

        private void BtnStatistics_Click(object sender, RoutedEventArgs e)
            => NavigateAdmin(new StatisticsPage());

        private void BtnRefreshAll_Click(object sender, RoutedEventArgs e)
        {
            RefreshAll();
            MessageBox.Show("Все данные обновлены из базы YanaulCRB.", "Обновление", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance?.NavigateToMainMenu();

        private void BtnSearchPatients_Click(object sender, RoutedEventArgs e)
            => LoadPatients(PatientSearchBox?.Text);

        private void GridPatients_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (GridPatients.SelectedItem is MedicalCard card)
                PatientSearchBox.Text = card.PolicyNumber;
        }

        private void QueueList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (QueueList.SelectedItem is Appointment appt)
            {
                _selectedAppointment = appt;
                SelectedPatient.Text = $"{appt.PatientName}\nТалон {appt.TicketNumber}\n{appt.AppointmentTime:dd.MM.yyyy HH:mm}";
            }
        }

        private void BtnCallNext_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAppointment == null)
            {
                MessageBox.Show("Выберите пациента из очереди.", "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            var result = _queueService.CallPatientByAppointmentId(_selectedAppointment.AppointmentId);
            MessageBox.Show(result.Success ? $"✅ Вызван: {result.PatientName}" : result.Message,
                "Очередь", MessageBoxButton.OK, result.Success ? MessageBoxImage.Information : MessageBoxImage.Warning);
            if (result.Success) RefreshAll();
        }

        private void BtnCancelAppointment_Click(object sender, RoutedEventArgs e)
        {
            if (_selectedAppointment == null) return;
            if (MessageBox.Show($"Отменить запись {_selectedAppointment.PatientName}?", "Подтверждение",
                    MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var appt = db.Appointments.Find(_selectedAppointment.AppointmentId);
                    if (appt != null) appt.Status = "отменена";
                    var q = db.VirtualQueues.FirstOrDefault(v => v.AppointmentId == _selectedAppointment.AppointmentId);
                    if (q != null) q.Status = QueueService.StatusSkipped;
                    db.SaveChanges();
                }
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void GridHomeVisits_SelectionChanged(object sender, SelectionChangedEventArgs e)
            => _selectedHomeVisit = GridHomeVisits.SelectedItem as HomeVisit;

        private void UpdateHomeVisitStatus(string newStatus)
        {
            if (_selectedHomeVisit == null)
            {
                MessageBox.Show("Выберите заявку.", "Вызов на дом", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var v = db.HomeVisits.Find(_selectedHomeVisit.VisitId);
                    if (v == null) return;
                    v.Status = newStatus;
                    db.Notifications.Add(new Notification
                    {
                        PatientPolicy = v.PolicyNumber,
                        Message = $"🚑 Статус вызова на дом: {newStatus}",
                        Type = "вызов",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });
                    db.SaveChanges();
                }
                RefreshAll();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnConfirmHomeVisit_Click(object sender, RoutedEventArgs e) => UpdateHomeVisitStatus("Подтвержден");
        private void BtnCompleteHomeVisit_Click(object sender, RoutedEventArgs e) => UpdateHomeVisitStatus("Завершен");
        private void BtnCancelHomeVisit_Click(object sender, RoutedEventArgs e) => UpdateHomeVisitStatus("Отменен");
    }
}
