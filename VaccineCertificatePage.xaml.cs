using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class VaccineCertificatePage : Page
    {
        public VaccineCertificatePage()
        {
            InitializeComponent();
        }

        private void BtnGetCert_Click(object sender, RoutedEventArgs e)
        {
            string policy = PolicyInput.Text.Replace(" ", "");
            if (policy.Length < 10)
            {
                MessageBox.Show("Введите корректный номер полиса");
                return;
            }

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var patient = db.MedicalCards.FirstOrDefault(p => p.PolicyNumber == policy);
                    if (patient == null)
                    {
                        MessageBox.Show("Пациент с таким полисом не найден");
                        CertResult.Visibility = Visibility.Collapsed;
                        return;
                    }

                    // Ищем прививки (записи с флагом IsVaccination = true)
                    var vaccines = db.Appointments
                        .Where(a => a.PolicyNumber == policy && a.IsVaccination == true)
                        .Select(a => new
                        {
                            Date = a.AppointmentTime.ToString("dd.MM.yyyy"),
                            Name = "Вакцинация (" + (a.Doctor != null ? a.Doctor.FullNameRU : "Врач") + ")",
                            Series = a.TicketNumber
                        })
                        .ToList();

                    if (vaccines.Count == 0)
                    {
                        MessageBox.Show("Записей о вакцинации не найдено.");
                        CertResult.Visibility = Visibility.Collapsed;
                        return;
                    }

                    CertPatientName.Text = "Пациент: " + patient.PatientName;
                    CertPolicy.Text = "Полис: " + patient.PolicyNumber;
                    VaccinesGrid.ItemsSource = vaccines;
                    CertResult.Visibility = Visibility.Visible;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка: " + ex.Message);
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            MainWindow.Instance.NavigateToMainMenu();
        }
    }
}