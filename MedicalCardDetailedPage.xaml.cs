using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Data.Entity; // Важно для Include

namespace YanaulKioskPerfect
{
    public partial class MedicalCardDetailedPage : Page, ILocalizable
    {
        private string _currentPolicy;

        public MedicalCardDetailedPage(string policyNumber)
        {
            InitializeComponent();
            _currentPolicy = policyNumber;
            LoadPatientData();
            UpdateLanguage();
        }

        private void LoadPatientData()
        {
            using (var db = new YanaulCRBEntities2())
            {
                var patient = db.MedicalCards.FirstOrDefault(p => p.PolicyNumber == _currentPolicy);

                if (patient == null)
                {
                    MessageBox.Show("Пациент не найден!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Заполняем профиль слева
                PatientNameText.Text = patient.PatientName;
                PolicyText.Text = patient.PolicyNumber;
                DobText.Text = patient.DateOfBirth?.ToString("dd.MM.yyyy") ?? "-";
                GenderText.Text = (patient.Gender == "М" || patient.Gender == "Male") ? "Мужской" : "Женский";
                BloodTypeText.Text = string.IsNullOrEmpty(patient.BloodType) ? "Не указана" : patient.BloodType;

                AllergiesText.Text = string.IsNullOrEmpty(patient.Allergies) ? "Не выявлено" : patient.Allergies;
                if (!string.IsNullOrEmpty(patient.Allergies)) AllergiesText.Foreground = System.Windows.Media.Brushes.Red;

                ChronicText.Text = string.IsNullOrEmpty(patient.ChronicDiseases) ? "Не выявлено" : patient.ChronicDiseases;

                // === ИСПРАВЛЕНИЕ 1: История посещений ===
                // Сначала .ToList() загружает данные в память, потом мы делаем ToString
                var visitsList = db.Appointments
                    .Where(a => a.PolicyNumber == _currentPolicy)
                    .Include("Doctor")
                    .OrderByDescending(a => a.AppointmentTime)
                    .ToList(); // <--- ВАЖНО: Загружаем список в память

                var visits = visitsList.Select(a => new {
                    Date = a.AppointmentTime.ToString("dd.MM.yyyy HH:mm"), // Теперь это работает!
                    Doctor = a.Doctor != null ? $"{a.Doctor.FullNameRU} ({a.Doctor.SpecialtyRU})" : "Врач не указан",
                    Diagnosis = !string.IsNullOrEmpty(a.Notes) ? a.Notes : "Без записей"
                }).ToList();

                VisitsGrid.ItemsSource = visits;

                // === ИСПРАВЛЕНИЕ 2: Прививки ===
                var vaccinesList = db.Appointments
                    .Where(a => a.PolicyNumber == _currentPolicy && a.IsVaccination == true)
                    .Include("Doctor")
                    .OrderByDescending(a => a.AppointmentTime)
                    .ToList(); // <--- ВАЖНО: Загружаем список в память

                var vaccines = vaccinesList.Select(a => new {
                    Date = a.AppointmentTime.ToString("dd.MM.yyyy"), // Теперь это работает!
                    Name = $"Вакцинация: {(a.Doctor != null ? a.Doctor.FullNameRU : "Врач")}",
                    Series = $"Талон: {a.TicketNumber}"
                }).ToList();

                VaccinesGrid.ItemsSource = vaccines;

                // === ИСПРАВЛЕНИЕ 3: Анализы ===
                var analysesList = db.AnalysisResults
                    .Where(ar => ar.PolicyNumber == _currentPolicy)
                    .Include("AnalysisType")
                    .OrderByDescending(ar => ar.ResultDate)
                    .ToList(); // <--- ВАЖНО: Загружаем список в память

                var analyses = analysesList.Select(ar => new {
                    Date = ar.ResultDate.ToString("dd.MM.yyyy"), // Теперь это работает!
                    Name = ar.AnalysisType != null ? ar.AnalysisType.NameRU : "Анализ",
                    Result = !string.IsNullOrEmpty(ar.Details) ? ar.Details : "Готов",
                    Status = !string.IsNullOrEmpty(ar.StatusRU) ? ar.StatusRU : "Готов"
                }).ToList();

                AnalysesGrid.ItemsSource = analyses;
            }
        }

        public void UpdateLanguage()
        {
            if (TitleText != null)
                TitleText.Text = MainWindow.IsBashkir ? "ПАЦИЕНТТЫҢ МЕДИЦИНА КАРТАҺЫ" : "МЕДИЦИНСКАЯ КАРТА ПАЦИЕНТА";
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.NavigateToMainMenu();
        }
    }
}