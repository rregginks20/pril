using System;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class PatientFormPage : Page, ILocalizable
    {
        private Doctor doctor;
        private DateTime appointmentTime;
        private bool isPolicyFormatting = false;
        private bool isPhoneFormatting = false;

        public PatientFormPage(Doctor doctor, DateTime appointmentTime)
        {
            InitializeComponent();
            this.doctor = doctor;
            this.appointmentTime = appointmentTime;
            UpdateLanguage();
            LoadData();
        }

        private void LoadData()
        {
            DoctorInfo.Text = MainWindow.IsBashkir ?
                $"Доктор: {doctor.FullNameRU} • {doctor.SpecialtyRU} • Каб. {doctor.Room}" :
                $"Врач: {doctor.FullNameRU} • {doctor.SpecialtyRU} • Каб. {doctor.Room}";
        }

        public void UpdateLanguage()
        {
            TitleText.Text = MainWindow.IsBashkir ? "МӘҒЛҮМӘТТӘР" : "ВВОД ДАННЫХ";
            NameLabel.Text = MainWindow.IsBashkir ? "ФИО тулыһынса:" : "ФИО полностью:";
            PolicyLabel.Text = MainWindow.IsBashkir ? "Полис ОМС:" : "Номер полиса ОМС:";
            PhoneLabel.Text = MainWindow.IsBashkir ? "Телефон:" : "Номер телефона:";
            SubmitBtn.Content = MainWindow.IsBashkir ? "ТАЛОН АЛЫРҒА" : "ПОЛУЧИТЬ ТАЛОН";
            LoadData();
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) =>
            MainWindow.Instance.NavigateToTimeSelection(doctor);

        // Маска для полиса
        private void PolicyBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isPolicyFormatting) return;
            isPolicyFormatting = true;
            TextBox textBox = sender as TextBox;
            string digitsOnly = Regex.Replace(textBox.Text, @"[^\d]", "");
            string formatted = "";
            for (int i = 0; i < digitsOnly.Length; i++)
            {
                if (i > 0 && i % 4 == 0) formatted += " ";
                formatted += digitsOnly[i];
            }
            textBox.Text = formatted;
            textBox.CaretIndex = textBox.Text.Length;
            isPolicyFormatting = false;
        }

        // Маска для телефона
        private void PhoneBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (isPhoneFormatting) return;
            isPhoneFormatting = true;
            TextBox textBox = sender as TextBox;
            string digitsOnly = Regex.Replace(textBox.Text, @"[^\d]", "");
            if (digitsOnly.Length > 0 && digitsOnly[0] != '7' && digitsOnly[0] != '8')
                digitsOnly = "7" + digitsOnly;
            else if (digitsOnly.Length > 0 && digitsOnly[0] == '8')
                digitsOnly = "7" + digitsOnly.Substring(1);

            if (digitsOnly.Length > 11) digitsOnly = digitsOnly.Substring(0, 11);

            string formatted = "";
            if (digitsOnly.Length > 0)
            {
                formatted = "+7";
                if (digitsOnly.Length > 1)
                {
                    formatted += " (" + digitsOnly.Substring(1, Math.Min(3, digitsOnly.Length - 1));
                    if (digitsOnly.Length > 4)
                    {
                        formatted += ") " + digitsOnly.Substring(4, Math.Min(3, digitsOnly.Length - 4));
                        if (digitsOnly.Length > 7)
                        {
                            formatted += "-" + digitsOnly.Substring(7, Math.Min(2, digitsOnly.Length - 7));
                            if (digitsOnly.Length > 9)
                                formatted += "-" + digitsOnly.Substring(9);
                        }
                    }
                }
            }
            textBox.Text = formatted;
            textBox.CaretIndex = textBox.Text.Length;
            isPhoneFormatting = false;
        }

        private void BtnSubmit_Click(object sender, RoutedEventArgs e)
        {
            string policyNumber = Regex.Replace(PolicyBox.Text, @"[^\d]", "");
            string phoneNumber = Regex.Replace(PhoneBox.Text, @"[^\d]", "");

            if (string.IsNullOrWhiteSpace(NameBox.Text) ||
                string.IsNullOrWhiteSpace(policyNumber) ||
                string.IsNullOrWhiteSpace(phoneNumber))
            {
                MessageBox.Show("Заполните все поля!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (policyNumber.Length != 16)
            {
                MessageBox.Show("Номер полиса должен содержать 16 цифр!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            if (phoneNumber.Length != 11)
            {
                MessageBox.Show("Номер телефона должен содержать 11 цифр!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            // Проверка пациента в БД
            bool patientExists = false;
            using (var db = new YanaulCRBEntities2())
            {
                var patient = db.MedicalCards.FirstOrDefault(m => m.PolicyNumber == policyNumber && m.PatientName == NameBox.Text);
                patientExists = patient != null;
            }

            if (!patientExists)
            {
                MessageBox.Show("Пациент с таким полисом и ФИО не найден в базе данных!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string ticketNumber = "YNA-" + DateTime.Now.ToString("yyMMddHHmmss");

            // === ГЛАВНОЕ ИСПРАВЛЕНИЕ: СОХРАНЕНИЕ В БАЗУ ДАННЫХ ===
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var newAppointment = new Appointment
                    {
                        PatientName = NameBox.Text,
                        PolicyNumber = policyNumber,
                        PatientPhone = PhoneBox.Text,
                        DoctorId = doctor.DoctorId,
                        AppointmentTime = appointmentTime,
                        TicketNumber = ticketNumber,
                        CreatedAt = DateTime.Now,
                        IsVaccination = false,
                        IsOnline = false,
                        Status = "активна" // Статус должен совпадать с тем, что ищут в очереди
                    };

                    // Добавляем в контекст базы данных
                    db.Appointments.Add(newAppointment);

                    // !!! ВАЖНО: Сохраняем изменения в базе !!!
                    db.SaveChanges();

                    // Также добавляем в локальный список для мгновенного отображения без перезагрузки
                    MainWindow.Appointments.Add(newAppointment);
                }

                MessageBox.Show("✅ Запись успешно создана и сохранена в базе!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                // Переход к талону
                // Находим только что созданную запись, чтобы передать её на страницу талона
                Appointment savedAppt = MainWindow.Appointments.FirstOrDefault(a => a.TicketNumber == ticketNumber);
                if (savedAppt != null)
                {
                    // Передаем врача вручную, так как в новой записи он может не подгрузиться сразу
                    savedAppt.Doctor = doctor;
                    MainWindow.Instance.NavigateToTicket(savedAppt);
                }
                else
                {
                    MainWindow.Instance.NavigateToMainMenu();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка при сохранении в базу: " + ex.Message, "Критическая ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}