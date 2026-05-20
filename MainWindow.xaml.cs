using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YanaulKioskPerfect.Helpers;

namespace YanaulKioskPerfect
{
    public partial class MainWindow : Window
    {
        public static bool IsBashkir { get; set; } = false;
        public static MainWindow Instance { get; private set; }

        // Глобальные кэши данных
        public static List<Doctor> Doctors { get; set; } = new List<Doctor>();
        public static List<Appointment> Appointments { get; set; } = new List<Appointment>();
        public static List<Payment> Payments { get; set; } = new List<Payment>();

        public static string CurrentPatientPolicy { get; set; } = "";
        public static MedicalCard CurrentPatient { get; set; }

        public static List<Vaccine> Vaccines { get; set; } = new List<Vaccine>();
        public static List<AnalysisType> AnalysisTypes { get; set; } = new List<AnalysisType>();
        public static List<Prescription> PrescriptionsList { get; set; } = new List<Prescription>();
        public static List<ChatMessage> ChatHistory { get; set; } = new List<ChatMessage>();
        public static List<MedicalCard> MedicalCards { get; set; } = new List<MedicalCard>();
        public static List<AnalysisResult> AnalysisResults { get; set; } = new List<AnalysisResult>();

        // Для передачи данных оплаты
        public static decimal PaymentAmount { get; set; }
        public static string PaymentServiceName { get; set; }
        public static Doctor PaymentDoctor { get; set; }
        public static DateTime PaymentDate { get; set; }
        public static TimeSpan PaymentTime { get; set; }

        private DispatcherTimer idleTimer;
        private DateTime lastActivity;
        private DispatcherTimer clockTimer;

        public MainWindow()
        {
            InitializeComponent();
            Instance = this;

            // Загружаем справочники (врачи, вакцины, типы анализов), они меняются редко
            LoadReferenceData();

            SetupIdleTimer();
            SetupClockTimer();
            lastActivity = DateTime.Now;

            // СНАЧАЛА СТРАНИЦА ИДЕНТИФИКАЦИИ!
            NavigateToIdentification();
        }

        private void LoadReferenceData()
        {
            var check = DatabaseHelper.TestConnection();
            if (!check.Success)
            {
                DatabaseHelper.ShowConnectionHelpIfFailed(check);
                return;
            }

            try
            {
                DatabaseHelper.EnsureGuestPatient();

                using (var db = new YanaulCRBEntities2())
                {
                    Doctors = db.Doctors.ToList();
                    Vaccines = db.Vaccines.Where(v => v.Availability == "В наличии").ToList();
                    AnalysisTypes = db.AnalysisTypes.Where(a => a.IsActive == true).ToList();
                    MedicalCards = db.MedicalCards.ToList();
                }

                System.Diagnostics.Debug.WriteLine(check.Message);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка загрузки данных из БД:\n{ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void SetupIdleTimer()
        {
            idleTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            idleTimer.Tick += (s, e) =>
            {
                if ((DateTime.Now - lastActivity).TotalSeconds > 60)
                {
                    NavigateToIdentification();
                    idleTimer.Stop();
                }
            };
            idleTimer.Start();
        }

        private void SetupClockTimer()
        {
            clockTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            clockTimer.Tick += (s, e) =>
            {
                DateTime now = DateTime.Now;
                if (ClockTime != null) ClockTime.Text = now.ToString("HH:mm:ss");
                if (ClockDate != null) ClockDate.Text = now.ToString("dd.MM.yyyy");
            };
            clockTimer.Start();
        }

        private void Window_MouseMove(object sender, MouseEventArgs e) => lastActivity = DateTime.Now;
        private void Window_KeyDown(object sender, KeyEventArgs e) => lastActivity = DateTime.Now;

        // === МЕТОДЫ НАВИГАЦИИ ===

        public void NavigateToIdentification()
        {
            CurrentPatientPolicy = "";
            CurrentPatient = null;
            UpdatePatientBadge();
            MainFrame.Navigate(new IdentificationPage());
        }

        public void NavigateToNewPatientCard(string policyNumber)
        {
            MainFrame.Navigate(new NewPatientCardPage(policyNumber));
        }

        public void NavigateToMainMenu()
        {
            MainFrame.Navigate(new MainMenuPage());
            UpdatePatientBadge();
        }

        public void UpdatePatientBadge()
        {
            if (PatientBadge == null) return;

            if (!string.IsNullOrEmpty(CurrentPatientPolicy) && CurrentPatient != null)
            {
                PatientBadge.Text = $"👤 {CurrentPatient.PatientName} • полис …{CurrentPatientPolicy.Substring(Math.Max(0, CurrentPatientPolicy.Length - 4))}";
                PatientBadge.Visibility = Visibility.Visible;
            }
            else if (!string.IsNullOrEmpty(CurrentPatientPolicy))
            {
                PatientBadge.Text = $"👤 Полис …{CurrentPatientPolicy.Substring(Math.Max(0, CurrentPatientPolicy.Length - 4))}";
                PatientBadge.Visibility = Visibility.Visible;
            }
            else
            {
                PatientBadge.Visibility = Visibility.Collapsed;
            }
        }
        public void NavigateToSpecialties() => MainFrame.Navigate(new SpecialtySelectionPage());
        public void NavigateToDoctorSelection(string specialty) => MainFrame.Navigate(new DoctorSelectionPage(specialty));
        public void NavigateToTimeSelection(Doctor doctor) => MainFrame.Navigate(new TimeSelectionPage(doctor));
        public void NavigateToTicket(Appointment appointment) => MainFrame.Navigate(new TicketPage(appointment));
        public void NavigateToAnalysesMenu() => MainFrame.Navigate(new AnalysesMenuPage());
        public void NavigateToPaymentMenu() => MainFrame.Navigate(new PaymentMenuPage());

        public void NavigateToMedicalCard()
        {
            // Проверяем, выбран ли пациент
            if (string.IsNullOrEmpty(CurrentPatientPolicy))
            {
                MessageBox.Show("Сначала выберите пациента!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MainFrame.Navigate(new MedicalCardPage());
        }

        public void NavigateToInfo() => MainFrame.Navigate(new InfoPage());
        public void NavigateToVirtualQueue() => MainFrame.Navigate(new VirtualQueuePage());

        // Исправлено: Передаем полис в конструктор страницы
        public void NavigateToPrescriptions()
        {
            if (!string.IsNullOrEmpty(CurrentPatientPolicy))
                MainFrame.Navigate(new PrescriptionPage(CurrentPatientPolicy));
            else
                MessageBox.Show("Пациент не выбран", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void NavigateToHospitalMap() => MainFrame.Navigate(new HospitalMapPage());

        // Исправлено: Передаем полис в конструктор страницы
        public void NavigateToVaccination()
        {
            if (!string.IsNullOrEmpty(CurrentPatientPolicy))
                MainFrame.Navigate(new VaccinationsPage(CurrentPatientPolicy));
            else
                MessageBox.Show("Пациент не выбран", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        public void NavigateToFeedback() => MainFrame.Navigate(new FeedbackFormPage());
        public void NavigateToStatistics() => MainFrame.Navigate(new StatisticsPage());
        public void NavigateToHomeVisit() => MainFrame.Navigate(new HomeVisitPage());
        public void NavigateToHealthChart()
        {
            if (string.IsNullOrEmpty(CurrentPatientPolicy))
            {
                MessageBox.Show("Сначала войдите по полису.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MainFrame.Navigate(new HealthChartPage(CurrentPatientPolicy));
        }

        public void NavigateToHealthChart(string policyNumber)
            => MainFrame.Navigate(new HealthChartPage(policyNumber));

        public void NavigateToPaymentHistory(string policyNumber)
        {
            if (string.IsNullOrWhiteSpace(policyNumber))
            {
                MessageBox.Show("Пациент не выбран.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }
            MainFrame.Navigate(new PaymentHistoryPage(policyNumber));
        }
        public void NavigateToTriage() => MainFrame.Navigate(new TriagePage());
        public void NavigateToMyTickets()
        {
            if (string.IsNullOrEmpty(CurrentPatientPolicy))
            {
                MessageBox.Show("Сначала войдите по номеру полиса.", "Мои записи", MessageBoxButton.OK, MessageBoxImage.Information);
                NavigateToIdentification();
                return;
            }
            ReloadPatientAppointments(CurrentPatientPolicy);
            MainFrame.Navigate(new MyTicketsPage());
        }

        public static void ReloadPatientAppointments(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy)) return;
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    Appointments = db.Appointments
                        .Include("Doctor")
                        .Where(a => a.PolicyNumber == policy)
                        .OrderByDescending(a => a.AppointmentTime)
                        .ToList();
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ReloadPatientAppointments: " + ex.Message);
            }
        }

        private void BtnLangRU_Click(object sender, RoutedEventArgs e) { IsBashkir = false; UpdateUI(); }
        private void BtnLangBA_Click(object sender, RoutedEventArgs e) { IsBashkir = true; UpdateUI(); }

        private void UpdateUI()
        {
            if (BtnLangRU != null) BtnLangRU.Background = IsBashkir ? Brushes.LightGray : new SolidColorBrush(Color.FromRgb(0, 137, 123));
            if (BtnLangBA != null) BtnLangBA.Background = IsBashkir ? new SolidColorBrush(Color.FromRgb(0, 137, 123)) : Brushes.LightGray;
            if (BtnExit != null) BtnExit.Content = IsBashkir ? "🚪 СЫҒЫУ" : "🚪 ВЫХОД";
            if (FooterText != null) FooterText.Text = IsBashkir ? "Үҙенә хеҙмәтләшеү терминалы" : "Терминал самообслуживания";

            if (MainFrame.Content is ILocalizable page)
                page.UpdateLanguage();
        }

        private void BtnExit_Click(object sender, RoutedEventArgs e)
        {
            if (MessageBox.Show("Завершить работу?", "Выход", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
                Application.Current.Shutdown();
        }

        public void SaveChatMessage(ChatMessage msg)
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    db.ChatMessages.Add(msg);
                    db.SaveChanges();
                    ChatHistory.Add(msg);
                }
            }
            catch { }
        }

        public void SaveAppointment(Appointment appt)
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    db.Appointments.Add(appt);
                    db.SaveChanges();
                    Appointments.Add(appt);
                }
            }
            catch { }
        }

        public void SavePayment(Payment pay)
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    db.Payments.Add(pay);
                    db.SaveChanges();
                    Payments.Add(pay);
                }
            }
            catch { }
        }
    }

    public interface ILocalizable { void UpdateLanguage(); }
}