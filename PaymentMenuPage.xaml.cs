using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using YanaulKioskPerfect.Helpers;

namespace YanaulKioskPerfect
{
    public partial class PaymentMenuPage : Page, ILocalizable
    {
        private string _selectedPaymentMethod = "card";
        private decimal _amount;
        private string _serviceName;
        private string _doctorName;
        private DateTime _appointmentDateTime;
        private string _receiptNumber; // ДОБАВЛЕНО: храним номер чека

        public PaymentMenuPage()
        {
            InitializeComponent();
            LoadPaymentData();
            SelectPaymentMethod("card");
        }

        private void LoadPaymentData()
        {
            _amount = MainWindow.PaymentAmount;
            _serviceName = MainWindow.PaymentServiceName;
            _doctorName = MainWindow.PaymentDoctor?.FullNameRU ?? "Врач";
            _appointmentDateTime = MainWindow.PaymentDate.Date + MainWindow.PaymentTime;

            if (ServiceNameText != null)
                ServiceNameText.Text = _serviceName;

            if (DoctorNameText != null)
                DoctorNameText.Text = _doctorName;

            if (DateTimeText != null)
                DateTimeText.Text = _appointmentDateTime.ToString("dd.MM.yyyy HH:mm");

            if (AmountText != null)
                AmountText.Text = $"{_amount} ₽";
        }

        private void SelectPaymentMethod(string method)
        {
            _selectedPaymentMethod = method;

            if (CardRadio != null) CardRadio.Fill = Brushes.White;
            if (SberPayRadio != null) SberPayRadio.Fill = Brushes.White;
            if (TinkoffPayRadio != null) TinkoffPayRadio.Fill = Brushes.White;
            if (CashRadio != null) CashRadio.Fill = Brushes.White;

            if (CardFormPanel != null)
                CardFormPanel.Visibility = Visibility.Collapsed;

            switch (method)
            {
                case "card":
                    if (CardRadio != null) CardRadio.Fill = new SolidColorBrush(Color.FromRgb(33, 150, 243));
                    if (CardFormPanel != null) CardFormPanel.Visibility = Visibility.Visible;
                    break;
                case "sberpay":
                    if (SberPayRadio != null) SberPayRadio.Fill = new SolidColorBrush(Color.FromRgb(33, 160, 56));
                    break;
                case "tinkoff":
                    if (TinkoffPayRadio != null) TinkoffPayRadio.Fill = new SolidColorBrush(Color.FromRgb(255, 221, 45));
                    break;
                case "cash":
                    if (CashRadio != null) CashRadio.Fill = new SolidColorBrush(Color.FromRgb(117, 117, 117));
                    break;
            }
        }

        private void CardNumberTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (CardNumberTextBox == null) return;

            string text = new string(CardNumberTextBox.Text.Where(char.IsDigit).ToArray());
            string formatted = "";

            for (int i = 0; i < text.Length && i < 16; i++)
            {
                if (i > 0 && i % 4 == 0)
                    formatted += " ";
                formatted += text[i];
            }

            CardNumberTextBox.Text = formatted;
            CardNumberTextBox.SelectionStart = CardNumberTextBox.Text.Length;
        }

        private void ExpiryTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (ExpiryTextBox == null) return;

            string text = new string(ExpiryTextBox.Text.Where(char.IsDigit).ToArray());

            if (text.Length >= 2)
            {
                int month = int.Parse(text.Substring(0, Math.Min(2, text.Length)));
                if (month > 12) month = 12;
                text = month.ToString("D2") + (text.Length > 2 ? "/" + text.Substring(2, Math.Min(2, text.Length - 2)) : "");
            }

            ExpiryTextBox.Text = text;
            ExpiryTextBox.SelectionStart = ExpiryTextBox.Text.Length;
        }

        private void CardPaymentBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectPaymentMethod("card");
        }

        private void SberPayBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectPaymentMethod("sberpay");
        }

        private void TinkoffPayBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectPaymentMethod("tinkoff");
        }

        private void CashBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            SelectPaymentMethod("cash");
        }

        private void BtnPay_Click(object sender, RoutedEventArgs e)
        {
            if (!ValidatePaymentData())
                return;

            ShowProcessingAnimation();
        }

        private bool ValidatePaymentData()
        {
            if (_selectedPaymentMethod == "card")
            {
                string cardNumber = new string(CardNumberTextBox.Text.Where(char.IsDigit).ToArray());
                if (cardNumber.Length != 16)
                {
                    MessageBox.Show("Введите корректный номер карты (16 цифр)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (string.IsNullOrEmpty(ExpiryTextBox.Text) || ExpiryTextBox.Text.Length < 5)
                {
                    MessageBox.Show("Введите срок действия карты (ММ/ГГ)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (CvvPasswordBox.Password.Length != 3)
                {
                    MessageBox.Show("Введите CVV код (3 цифры)", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(CardHolderTextBox.Text))
                {
                    MessageBox.Show("Введите имя владельца карты", "Ошибка",
                        MessageBoxButton.OK, MessageBoxImage.Warning);
                    return false;
                }
            }

            return true;
        }

        private void ShowProcessingAnimation()
        {
            BtnPay.IsEnabled = false;
            BtnPay.Content = "⏳ Обработка платежа...";

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };

            timer.Tick += (s, e) =>
            {
                timer.Stop();
                ProcessPayment();
            };

            timer.Start();
        }

        private void ProcessPayment()
        {
            try
            {
                System.Diagnostics.Debug.WriteLine($"\n=== ОБРАБОТКА ПЛАТЕЖА ===");
                System.Diagnostics.Debug.WriteLine($"Сумма: {_amount} ₽");
                System.Diagnostics.Debug.WriteLine($"Способ: {_selectedPaymentMethod}");
                System.Diagnostics.Debug.WriteLine($"Услуга: {_serviceName}\n");

                using (var db = new YanaulCRBEntities2())
                {
                    _receiptNumber = $"RCP-{DateTime.Now:yyMMddHHmmss}-{new Random().Next(1000, 9999)}";

                    string policy = MainWindow.CurrentPatientPolicy;
                    if (string.IsNullOrWhiteSpace(policy))
                        policy = DatabaseHelper.DemoGuestPolicy;

                    var payment = new Payment
                    {
                        PolicyNumber = policy,
                        Amount = _amount,
                        PaymentMethod = _selectedPaymentMethod,
                        PaymentDate = DateTime.Now,
                        Status = "успешно",
                        ServiceRU = _serviceName ?? "Платная услуга",
                        ServiceBA = _serviceName ?? "Түләүле хеҙмәт",
                        ReceiptNumber = _receiptNumber
                    };

                    db.Payments.Add(payment);

                    var validationErrors = db.GetValidationErrors();
                    if (validationErrors.Any())
                    {
                        string errors = string.Join("\n", validationErrors.SelectMany(v => v.ValidationErrors.Select(e => e.ErrorMessage)));
                        throw new Exception("Ошибка валидации: " + errors);
                    }

                    int saved = db.SaveChanges();
                    System.Diagnostics.Debug.WriteLine($"Платеж сохранен! ID: {payment.PaymentId}, Receipt: {_receiptNumber}\n");

                    if (saved > 0)
                    {
                        MessageBox.Show(
                            $"✅ Оплата {_amount} ₽ прошла успешно!\n\n" +
                            $"Способ: {GetPaymentMethodName()}\n" +
                            $"Чек: {_receiptNumber}\n" +
                            $"Дата: {DateTime.Now:dd.MM.yyyy HH:mm}\n\n" +
                            "Формируем талон...",
                            "Оплата успешна",
                            MessageBoxButton.OK,
                            MessageBoxImage.Information);

                        CreateAppointmentAndShowTicket();
                    }
                    else
                    {
                        throw new Exception("Не удалось сохранить платеж");
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("ERROR: " + ex.ToString());

                MessageBox.Show(
                    "Ошибка оплаты: " + ex.Message + "\n\n" +
                    "Попробуйте другую карту или выберите наличные.",
                    "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);

                BtnPay.IsEnabled = true;
                BtnPay.Content = "💳 ОПЛАТИТЬ";
            }
        }

        private string GetPaymentMethodName()
        {
            switch (_selectedPaymentMethod)
            {
                case "card": return "Банковская карта";
                case "sberpay": return "SberPay";
                case "tinkoff": return "Tinkoff Pay";
                case "cash": return "Наличные";
                default: return _selectedPaymentMethod;
            }
        }

        private void CreateAppointmentAndShowTicket()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var appointmentTime = MainWindow.PaymentDate.Date + MainWindow.PaymentTime;
                    string ticketNumber = $"YNA-{DateTime.Now:yyMMddHHmmss}-{new Random().Next(1000, 9999)}";

                    var appointment = new Appointment
                    {
                        PatientName = MainWindow.CurrentPatient?.PatientName ?? "Пациент",
                        PolicyNumber = string.IsNullOrWhiteSpace(MainWindow.CurrentPatientPolicy)
                            ? DatabaseHelper.DemoGuestPolicy
                            : MainWindow.CurrentPatientPolicy,
                        PatientPhone = MainWindow.CurrentPatient?.Phone ?? "",
                        DoctorId = MainWindow.PaymentDoctor?.DoctorId ?? 1,
                        AppointmentTime = appointmentTime,
                        TicketNumber = ticketNumber,
                        CreatedAt = DateTime.Now,
                        IsVaccination = false,
                        IsOnline = false,
                        Status = "активна",
                        Notes = $"Платная услуга: {_serviceName}. Оплачено. Чек: {_receiptNumber}"
                    };

                    db.Appointments.Add(appointment);
                    db.SaveChanges();

                    System.Diagnostics.Debug.WriteLine($"Запись создана! ID: {appointment.AppointmentId}");

                    if (MainWindow.Instance != null)
                    {
                        MainWindow.Instance.NavigateToTicket(appointment);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания записи: " + ex.Message, "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                "Отменить оплату и вернуться к выбору времени?",
                "Отмена оплаты",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                if (MainWindow.Instance != null)
                {
                    MainWindow.Instance.NavigateToTimeSelection(MainWindow.PaymentDoctor);
                }
            }
        }

        public void UpdateLanguage()
        {
            if (BtnPay != null)
                BtnPay.Content = MainWindow.IsBashkir ? "💳 ТҮЛӘҮ" : "💳 ОПЛАТИТЬ";

            if (BtnCancel != null)
                BtnCancel.Content = MainWindow.IsBashkir ? "❌ БАШ ТАРТЫУ" : "❌ ОТМЕНА";
        }
    }
}