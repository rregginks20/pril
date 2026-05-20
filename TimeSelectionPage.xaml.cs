using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace YanaulKioskPerfect
{
    public partial class TimeSelectionPage : Page, ILocalizable
    {
        private Doctor _selectedDoctor;
        private DateTime _selectedDate;
        private TimeSpan? _selectedTime;
        private bool _isPaidService = false;
        private decimal _servicePrice = 0;
        private string _selectedServiceName = "";
        private List<TimeSpan> _availableSlots;
        private List<TimeSpan> _busySlots;
        private readonly string _currentPatientPolicy;

        private readonly TimeSpan _workStart = new TimeSpan(8, 0, 0);
        private readonly TimeSpan _workEnd = new TimeSpan(20, 0, 0);
        private readonly int _slotDurationMinutes = 30;

        public TimeSelectionPage(Doctor doctor)
        {
            InitializeComponent();
            _selectedDoctor = doctor;
            _selectedDate = DateTime.Today;
            _currentPatientPolicy = MainWindow.CurrentPatientPolicy;

            System.Diagnostics.Debug.WriteLine($"\n=== TimeSelectionPage ===");
            System.Diagnostics.Debug.WriteLine($"Врач: {doctor.FullNameRU}");
            System.Diagnostics.Debug.WriteLine($"Полис: {_currentPatientPolicy}\n");

            LoadDoctorInfo();
            LoadPaidServices();
            GenerateTimeSlots();
            UpdatePatientInfo();
            UpdateLanguage();
        }

        private void LoadDoctorInfo()
        {
            if (_selectedDoctor == null) return;

            if (DoctorNameText != null)
            {
                DoctorNameText.Text = MainWindow.IsBashkir && !string.IsNullOrEmpty(_selectedDoctor.FullNameBA)
                    ? _selectedDoctor.FullNameBA
                    : _selectedDoctor.FullNameRU;
            }

            if (DoctorSpecialtyText != null)
            {
                DoctorSpecialtyText.Text = MainWindow.IsBashkir && !string.IsNullOrEmpty(_selectedDoctor.SpecialtyBA)
                    ? _selectedDoctor.SpecialtyBA
                    : _selectedDoctor.SpecialtyRU;
            }

            if (DoctorRoomText != null)
                DoctorRoomText.Text = _selectedDoctor.Room ?? "—";

            if (DoctorCategoryText != null)
                DoctorCategoryText.Text = _selectedDoctor.Category ?? "—";

            if (DoctorExperienceText != null)
                DoctorExperienceText.Text = (_selectedDoctor.ExperienceYears ?? 0) + " лет";

            if (DoctorRatingText != null)
                DoctorRatingText.Text = (_selectedDoctor.Rating ?? 0).ToString("F1");

            LoadDoctorPhoto();

            if (SummaryDoctorText != null)
                SummaryDoctorText.Text = DoctorNameText?.Text;
        }

        private void LoadDoctorPhoto()
        {
            if (DoctorPhoto == null) return;

            string photoUrl = _selectedDoctor.PhotoUrl;

            if (!string.IsNullOrEmpty(photoUrl))
            {
                try
                {
                    if (photoUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
                    {
                        DoctorPhoto.Source = new BitmapImage(new Uri(photoUrl));
                    }
                    else
                    {
                        string fullPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, photoUrl.TrimStart('/', '\\'));
                        if (File.Exists(fullPath))
                        {
                            var bitmap = new BitmapImage();
                            bitmap.BeginInit();
                            bitmap.UriSource = new Uri(fullPath);
                            bitmap.CacheOption = BitmapCacheOption.OnLoad;
                            bitmap.EndInit();
                            DoctorPhoto.Source = bitmap;
                        }
                    }
                }
                catch { }
            }
        }

        private void LoadPaidServices()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var services = db.AnalysisTypes
                        .Where(a => a.IsActive == true && a.Price > 0)
                        .Select(a => new { a.AnalysisId, a.NameRU, a.NameBA, a.Price })
                        .ToList();

                    if (PaidServicesCombo != null)
                    {
                        PaidServicesCombo.ItemsSource = services;
                        PaidServicesCombo.DisplayMemberPath = MainWindow.IsBashkir ? "NameBA" : "NameRU";
                        PaidServicesCombo.SelectedValuePath = "AnalysisId";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка загрузки услуг: " + ex.Message);
            }
        }

        private void GenerateTimeSlots()
        {
            if (TimeSlotsPanel == null) return;

            TimeSlotsPanel.Children.Clear();
            _availableSlots = new List<TimeSpan>();
            _busySlots = GetBusyTimeSlots(_selectedDate);

            System.Diagnostics.Debug.WriteLine($"\n=== Генерация слотов на {_selectedDate:dd.MM.yyyy} ===");
            System.Diagnostics.Debug.WriteLine($"Занятые слоты: {_busySlots.Count}");
            foreach (var slot in _busySlots)
            {
                System.Diagnostics.Debug.WriteLine($"  Занято: {slot}");
            }

            TimeSpan currentTime = _workStart;

            while (currentTime < _workEnd)
            {
                bool isBusy = _busySlots.Contains(currentTime);

                if (!isBusy)
                {
                    _availableSlots.Add(currentTime);
                    CreateTimeSlotButton(currentTime, false);
                }
                else
                {
                    // Создаем кнопку но ЗАБЛОКИРОВАННУЮ
                    CreateTimeSlotButton(currentTime, true);
                }

                currentTime = currentTime.Add(TimeSpan.FromMinutes(_slotDurationMinutes));
            }

            if (SlotsCountText != null)
            {
                SlotsCountText.Text = $" ({_availableSlots.Count} из {_availableSlots.Count + _busySlots.Count} слотов)";
            }

            if (NoSlotsMessage != null)
            {
                NoSlotsMessage.Visibility = !_availableSlots.Any() ? Visibility.Visible : Visibility.Collapsed;
            }

            UpdateDateDisplay();
        }

        private List<TimeSpan> GetBusyTimeSlots(DateTime date)
        {
            var busySlots = new List<TimeSpan>();

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    int doctorId = _selectedDoctor.DoctorId;

                    System.Diagnostics.Debug.WriteLine($"Ищем записи для врача ID={doctorId} на {date.Date}");

                    var appointments = db.Appointments
                        .Where(a => a.DoctorId == doctorId &&
                                   DbFunctions.TruncateTime(a.AppointmentTime) == date.Date &&
                                   (a.Status == "активна" || a.Status == "подтверждена"))
                        .Select(a => a.AppointmentTime)
                        .ToList();

                    foreach (var appt in appointments)
                    {
                        busySlots.Add(appt.TimeOfDay);
                        System.Diagnostics.Debug.WriteLine($"  Найдена запись: {appt}");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка загрузки слотов: " + ex.Message);
            }

            return busySlots;
        }

        private void CreateTimeSlotButton(TimeSpan time, bool isBusy)
        {
            var btn = new Button
            {
                Content = time.ToString(@"hh\:mm"),
                Width = 90,
                Height = 40,
                FontSize = 14,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(5, 5, 5, 5),
                Cursor = isBusy ? Cursors.No : Cursors.Hand,
                Tag = time,
                IsEnabled = !isBusy
            };

            if (isBusy)
            {
                btn.Background = Brushes.LightGray;
                btn.Foreground = Brushes.Gray;
                btn.BorderThickness = new Thickness(2, 2, 2, 2);
                btn.BorderBrush = Brushes.Gray;
                btn.ToolTip = "Время уже занято";
            }
            else
            {
                btn.Background = new SolidColorBrush(Color.FromRgb(224, 242, 241));
                btn.Foreground = new SolidColorBrush(Color.FromRgb(0, 105, 92));
                btn.BorderThickness = new Thickness(2, 2, 2, 2);
                btn.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 150, 136));
                btn.Click += TimeSlot_Click;
            }

            TimeSlotsPanel.Children.Add(btn);
        }

        private void TimeSlot_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TimeSpan time)
            {
                foreach (var child in TimeSlotsPanel.Children)
                {
                    if (child is Button b && b.IsEnabled)
                    {
                        b.Background = new SolidColorBrush(Color.FromRgb(224, 242, 241));
                        b.Foreground = new SolidColorBrush(Color.FromRgb(0, 105, 92));
                    }
                }

                btn.Background = new SolidColorBrush(Color.FromRgb(0, 150, 136));
                btn.Foreground = Brushes.White;

                _selectedTime = time;

                System.Diagnostics.Debug.WriteLine($"\n=== Выбрано время ===");
                System.Diagnostics.Debug.WriteLine($"Дата: {_selectedDate:dd.MM.yyyy}");
                System.Diagnostics.Debug.WriteLine($"Время: {_selectedTime}\n");

                UpdateSummary();
            }
        }

        private void BtnPrevDay_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(-1);
            if (_selectedDate < DateTime.Today)
            {
                _selectedDate = DateTime.Today;
            }
            GenerateTimeSlots();
        }

        private void BtnNextDay_Click(object sender, RoutedEventArgs e)
        {
            _selectedDate = _selectedDate.AddDays(1);
            if (_selectedDate > DateTime.Today.AddDays(30))
            {
                _selectedDate = DateTime.Today.AddDays(30);
            }
            GenerateTimeSlots();
        }

        private void UpdateDateDisplay()
        {
            if (SelectedDateText == null) return;

            string dateStr = _selectedDate.ToString("dd MMMM yyyy",
                new CultureInfo(MainWindow.IsBashkir ? "ba-RU" : "ru-RU"));

            if (dateStr.Length > 0)
            {
                dateStr = char.ToUpper(dateStr[0]) + dateStr.Substring(1);
            }

            SelectedDateText.Text = dateStr;

            string dayOfWeek = _selectedDate.ToString("dddd",
                new CultureInfo(MainWindow.IsBashkir ? "ba-RU" : "ru-RU"));
            dayOfWeek = char.ToUpper(dayOfWeek[0]) + dayOfWeek.Substring(1);

            SelectedDateText.Text += $" ({dayOfWeek})";
        }

        private void RadioFree_Checked(object sender, RoutedEventArgs e)
        {
            _isPaidService = false;
            _servicePrice = 0;

            if (PaidServicesPanel != null)
            {
                PaidServicesPanel.Visibility = Visibility.Collapsed;
            }

            if (ServiceDescriptionText != null)
            {
                ServiceDescriptionText.Text = "";
            }

            UpdateSummary();
        }

        private void RadioPaid_Checked(object sender, RoutedEventArgs e)
        {
            _isPaidService = true;

            if (PaidServicesPanel != null)
            {
                PaidServicesPanel.Visibility = Visibility.Visible;
            }

            UpdateSummary();
        }

        private void PaidServicesCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PaidServicesCombo.SelectedItem != null)
            {
                dynamic service = PaidServicesCombo.SelectedItem;
                _selectedServiceName = MainWindow.IsBashkir && !string.IsNullOrEmpty(service.NameBA)
                    ? service.NameBA
                    : service.NameRU;
                _servicePrice = service.Price ?? 0;

                if (ServicePriceText != null)
                {
                    ServicePriceText.Text = $"Стоимость: {_servicePrice} ₽";
                }

                if (ServiceDescriptionText != null)
                {
                    ServiceDescriptionText.Text = GetServiceDescription(_selectedServiceName);
                }

                UpdateSummary();
            }
        }

        private string GetServiceDescription(string serviceName)
        {
            switch (serviceName)
            {
                case "Общий анализ крови":
                    return "📊 Развернутый анализ крови с лейкоцитарной формулой. Срок выполнения: 1 день.";
                case "Биохимический анализ":
                    return "🧪 Анализ на глюкозу, холестерин, билирубин и другие показатели. Срок выполнения: 2 дня.";
                case "Анализ на гормоны":
                    return "💉 Определение уровня гормонов щитовидной железы. Срок выполнения: 3 дня.";
                case "ЭКГ с расшифровкой":
                    return "❤️ Электрокардиограмма с подробной расшифровкой кардиолога. Срок выполнения: в день обращения.";
                case "УЗИ органов":
                    return "📷 Ультразвуковое исследование органов брюшной полости. Срок выполнения: в день обращения.";
                default:
                    return "Платная медицинская услуга. Срок выполнения уточняйте у врача.";
            }
        }

        private void UpdateSummary()
        {
            if (SummaryDateTimeText != null)
            {
                if (_selectedTime.HasValue)
                {
                    SummaryDateTimeText.Text = _selectedDate.ToString("dd.MM.yyyy") + " " +
                                              _selectedTime.Value.ToString(@"hh\:mm");
                }
                else
                {
                    SummaryDateTimeText.Text = "—";
                }
            }

            if (SummaryPriceText != null)
            {
                if (_isPaidService)
                {
                    SummaryPriceText.Text = _servicePrice + " ₽";
                    SummaryPriceText.Foreground = Brushes.Orange;
                }
                else
                {
                    SummaryPriceText.Text = "Бесплатно (по ОМС)";
                    SummaryPriceText.Foreground = Brushes.Green;
                }
            }

            if (BtnConfirm != null)
            {
                BtnConfirm.IsEnabled = _selectedTime.HasValue;
                BtnConfirm.Background = _selectedTime.HasValue ?
                    new SolidColorBrush(Color.FromRgb(76, 175, 80)) : Brushes.Gray;
            }
        }

        private void UpdatePatientInfo()
        {
            if (string.IsNullOrEmpty(_currentPatientPolicy)) return;

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var patient = db.MedicalCards
                        .FirstOrDefault(p => p.PolicyNumber == _currentPatientPolicy);

                    if (patient != null)
                    {
                        if (PatientNameText != null)
                            PatientNameText.Text = patient.PatientName;

                        if (PolicyNumberText != null)
                            PolicyNumberText.Text = patient.PolicyNumber;

                        if (PatientPhoneText != null)
                        {
                            PatientPhoneText.Text = "+7 (___) ___-__-__";
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка загрузки данных пациента: " + ex.Message);
            }
        }

        private void BtnConfirm_Click(object sender, RoutedEventArgs e)
        {
            if (!_selectedTime.HasValue)
            {
                MessageBox.Show(
                    MainWindow.IsBashkir ? "Вакыт сайлагыз!" : "Выберите время!",
                    "Внимание", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (_busySlots.Contains(_selectedTime.Value))
            {
                MessageBox.Show(
                    "Это время только что занял другой пациент!\nПожалуйста, выберите другое время.",
                    "Время занято", MessageBoxButton.OK, MessageBoxImage.Warning);

                GenerateTimeSlots();
                return;
            }

            var confirmResult = MessageBox.Show(
                $"Запись подтверждается!\n\n👤 Пациент: {PatientNameText?.Text}\n🏥 Врач: {DoctorNameText?.Text}\n📅 Дата: {_selectedDate:dd.MM.yyyy}\n🕐 Время: {_selectedTime}\n💰 Стоимость: {(_isPaidService ? _servicePrice + " ₽" : "Бесплатно (по ОМС)")}\n\nПродолжить?",
                MainWindow.IsBashkir ? "Расписание" : "Подтверждение записи",
                MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (confirmResult != MessageBoxResult.Yes) return;

            if (_isPaidService && _servicePrice > 0)
            {
                NavigateToPayment();
                return;
            }

            CreateAppointment();
        }

        private void CreateAppointment()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"\n=== СОЗДАНИЕ ЗАПИСИ ===");

                using (var db = new YanaulCRBEntities2())
                {
                    var appointmentTime = _selectedDate.Date + _selectedTime.Value;
                    string ticketNumber = GenerateTicketNumber();

                    System.Diagnostics.Debug.WriteLine($"Время: {appointmentTime}");
                    System.Diagnostics.Debug.WriteLine($"Номер талона: {ticketNumber}");

                    var appointment = new Appointment
                    {
                        PatientName = PatientNameText != null ? PatientNameText.Text : "Пациент",
                        PolicyNumber = _currentPatientPolicy,
                        PatientPhone = PatientPhoneText != null ? PatientPhoneText.Text.Replace(" ", "") : "",
                        DoctorId = _selectedDoctor.DoctorId,
                        AppointmentTime = appointmentTime,
                        TicketNumber = ticketNumber,
                        CreatedAt = DateTime.Now,
                        IsVaccination = false,
                        IsOnline = false,
                        Status = "активна",
                        Notes = _isPaidService ? $"Платная услуга: {_selectedServiceName}" : ""
                    };

                    db.Appointments.Add(appointment);
                    int saved = db.SaveChanges();

                    System.Diagnostics.Debug.WriteLine($"Записей сохранено: {saved}");
                    System.Diagnostics.Debug.WriteLine($"ID записи: {appointment.AppointmentId}\n");

                    if (saved > 0)
                    {
                        MainWindow.Appointments.Add(appointment);

                        if (MainWindow.Instance != null)
                        {
                            System.Diagnostics.Debug.WriteLine("Переход к талону...");
                            MainWindow.Instance.NavigateToTicket(appointment);
                        }
                    }
                    else
                    {
                        MessageBox.Show("Не удалось сохранить запись в базу данных",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания записи: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
                System.Diagnostics.Debug.WriteLine("ERROR: " + ex.ToString());
                System.Diagnostics.Debug.WriteLine("Stack: " + ex.StackTrace);
            }
        }

        private string GenerateTicketNumber()
        {
            return $"YNA-{DateTime.Now:yyMMddHHmmss}-{new Random().Next(1000, 9999)}";
        }

        private void NavigateToPayment()
        {
            MainWindow.PaymentAmount = _servicePrice;
            MainWindow.PaymentServiceName = _selectedServiceName;
            MainWindow.PaymentDoctor = _selectedDoctor;
            MainWindow.PaymentDate = _selectedDate;
            MainWindow.PaymentTime = _selectedTime.Value;

            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.NavigateToPaymentMenu();
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                MainWindow.IsBashkir ? "Запись отменена. К главному меню?" : "Запись отменена. Вернуться к главному меню?",
                "Отмена", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.NavigateToMainMenu();
                }
            }
        }

        public void UpdateLanguage()
        {
            if (TitleText != null)
                TitleText.Text = MainWindow.IsBashkir ? "ВАКЫТТЫ ҺАЙЛАУ" : "ВЫБОР ВРЕМЕНИ ПРИЕМА";

            if (SubtitleText != null)
                SubtitleText.Text = MainWindow.IsBashkir ? "5 аҙымдан 4-се: Уңайлы ваҡытты һайлағыҙ" : "Шаг 4 из 5: Выберите удобное время";

            if (BtnBack != null)
                BtnBack.Content = MainWindow.IsBashkir ? "⇦ Кире" : "⇦ Назад";

            if (BtnConfirm != null)
                BtnConfirm.Content = MainWindow.IsBashkir ? "✅ ЯЗЫЛЫУЫН РАҠЛАРҒА" : "✅ ПОДТВЕРДИТЬ ЗАПИСЬ";

            if (BtnCancel != null)
                BtnCancel.Content = MainWindow.IsBashkir ? "❌ ҒӘМӘЛГӘ АШЫРМАУ" : "❌ ОТМЕНА";

            LoadDoctorInfo();
            GenerateTimeSlots();
            UpdateSummary();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.NavigateToDoctorSelection(_selectedDoctor.SpecialtyRU);
            }
        }
    }
}