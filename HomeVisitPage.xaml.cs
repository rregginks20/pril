using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YanaulKioskPerfect.Helpers;
using YanaulKioskPerfect.Services;

namespace YanaulKioskPerfect
{
    public partial class HomeVisitPage : Page, ILocalizable
    {
        public HomeVisitPage()
        {
            InitializeComponent();
            Loaded += (s, e) => LoadPatientContext();
        }

        public void UpdateLanguage()
        {
            // Заголовок задаётся в XAML
        }

        private string ResolvePolicy()
        {
            string policy = ChatService.NormalizePolicy(MainWindow.CurrentPatientPolicy);
            if (string.IsNullOrWhiteSpace(policy))
                return null;
            return policy;
        }

        private void LoadPatientContext()
        {
            string policy = ResolvePolicy();
            if (string.IsNullOrEmpty(policy))
            {
                MessageBox.Show(
                    "Для вызова на дом войдите по номеру полиса ОМС на главном экране.",
                    "Идентификация",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);
                MainWindow.Instance?.NavigateToIdentification();
                return;
            }

            try
            {
                DatabaseHelper.EnsureGuestPatient();

                using (var db = new YanaulCRBEntities2())
                {
                    var patient = db.MedicalCards.FirstOrDefault(p => p.PolicyNumber == policy);
                    if (patient == null)
                    {
                        MessageBox.Show(
                            "Пациент с таким полисом не найден в базе. Создайте карту или войдите снова.",
                            "Ошибка",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                        MainWindow.Instance?.NavigateToIdentification();
                        return;
                    }

                    PatientInfoBar.Visibility = Visibility.Visible;
                    LblPatientName.Text = patient.PatientName;
                    LblPatientPolicy.Text = $"Полис: {policy}";
                    if (!string.IsNullOrWhiteSpace(patient.Phone))
                        TxtPhone.Text = patient.Phone;

                    var lastVisit = db.HomeVisits
                        .Where(h => h.PolicyNumber == policy)
                        .OrderByDescending(h => h.CreatedAt)
                        .FirstOrDefault();
                    if (lastVisit != null && !string.IsNullOrWhiteSpace(lastVisit.Address))
                    {
                        var addr = lastVisit.Address;
                        int telIdx = addr.IndexOf("тел:", StringComparison.OrdinalIgnoreCase);
                        if (telIdx > 0)
                            TxtAddress.Text = addr.Substring(0, telIdx).Replace("|", "").Trim();
                        else
                            TxtAddress.Text = addr;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки данных: " + ex.Message, "База данных",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCallDoctor_Click(object sender, RoutedEventArgs e)
        {
            string policy = ResolvePolicy();
            if (string.IsNullOrEmpty(policy))
            {
                MessageBox.Show("Войдите по полису ОМС.", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                MainWindow.Instance?.NavigateToIdentification();
                return;
            }

            string address = TxtAddress?.Text?.Trim() ?? "";
            string phone = TxtPhone?.Text?.Trim() ?? "";
            string symptoms = TxtSymptoms?.Text?.Trim() ?? "";

            if (address.Length < 5)
            {
                MessageBox.Show("Укажите полный адрес (улица, дом, квартира).", "Адрес", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtAddress?.Focus();
                return;
            }

            if (symptoms.Length < 3)
            {
                MessageBox.Show("Опишите симптомы — это нужно врачу выездной бригады.", "Симптомы", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtSymptoms?.Focus();
                return;
            }

            if (phone.Length < 7)
            {
                MessageBox.Show("Укажите телефон для связи с диспетчером.", "Телефон", MessageBoxButton.OK, MessageBoxImage.Warning);
                TxtPhone?.Focus();
                return;
            }

            if (CheckFever.IsChecked == true) symptoms += " [Температура >38°C]";
            if (CheckChild.IsChecked == true) symptoms += " [Ребёнок]";
            if (CheckElderly.IsChecked == true) symptoms += " [Пожилой]";

            string status = "В ожидании";
            DateTime visitDate = DateTime.Now.AddHours(6);
            string eta = "в течение рабочего дня";
            if (RbUrgent.IsChecked == true) { status = "Срочно"; visitDate = DateTime.Now.AddHours(3); eta = "2–4 часа"; }
            if (RbEmergency.IsChecked == true) { status = "Экстренно"; visitDate = DateTime.Now.AddHours(1); eta = "до 1 часа"; }

            string patientName = MainWindow.CurrentPatient?.PatientName ?? "Пациент";
            string fullAddress = address + " | тел: " + phone;

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    if (!db.MedicalCards.Any(p => p.PolicyNumber == policy))
                        throw new Exception("Полис не найден в MedicalCard. Перезайдите в систему.");

                    var patient = db.MedicalCards.First(p => p.PolicyNumber == policy);
                    patientName = patient.PatientName;

                    var visit = new HomeVisit
                    {
                        PolicyNumber = policy,
                        PatientName = patientName,
                        Address = fullAddress,
                        Symptoms = symptoms,
                        VisitDate = visitDate,
                        Status = status,
                        CreatedAt = DateTime.Now
                    };
                    db.HomeVisits.Add(visit);

                    db.Notifications.Add(new Notification
                    {
                        PatientPolicy = policy,
                        Message = $"🚑 Заявка на выезд принята ({status}). Диспетчер перезвонит на {phone}.",
                        Type = "вызов",
                        IsRead = false,
                        CreatedAt = DateTime.Now
                    });

                    db.SaveChanges();
                }

                MessageBox.Show(
                    $"✅ Заявка отправлена в диспетчерскую!\n\n" +
                    $"Пациент: {patientName}\n" +
                    $"Статус: {status}\n" +
                    $"Ориентировочно: {eta}\n\n" +
                    "Заявка видна в панели администратора → «Диспетчерская».",
                    "Вызов на дом",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                MainWindow.Instance?.NavigateToMainMenu();
            }
            catch (Exception ex)
            {
                string hint = ex.Message.Contains("FOREIGN KEY") || ex.Message.Contains("MedicalCard")
                    ? "\n\nУбедитесь, что вы вошли по реальному полису из базы (например 1234567890123456)."
                    : "";
                MessageBox.Show("Не удалось сохранить заявку: " + ex.Message + hint,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance?.NavigateToMainMenu();
    }
}
