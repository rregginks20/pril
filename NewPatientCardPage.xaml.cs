using System;
using System.Windows;
using System.Windows.Controls;

namespace YanaulKioskPerfect
{
    public partial class NewPatientCardPage : Page
    {
        private string _policyNumber;

        public NewPatientCardPage(string policyNumber)
        {
            InitializeComponent();
            _policyNumber = policyNumber;
            if (PolicyText != null)
                PolicyText.Text = policyNumber;
        }

        private void BtnCreateCard_Click(object sender, RoutedEventArgs e)
        {
            string fullName = FullNameTextBox?.Text.Trim() ?? "";

            if (string.IsNullOrEmpty(fullName))
            {
                MessageBox.Show("Введите ФИО пациента", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var newPatient = new MedicalCard
                    {
                        PolicyNumber = _policyNumber,
                        PatientName = fullName,
                        // Phone убираем - нет такого поля в БД
                        DateOfBirth = BirthDatePicker?.SelectedDate,
                        CreatedAt = DateTime.Now,
                        UpdatedAt = DateTime.Now
                    };

                    db.MedicalCards.Add(newPatient);
                    db.SaveChanges();

                    MainWindow.CurrentPatientPolicy = _policyNumber;
                    MainWindow.CurrentPatient = newPatient;

                    MessageBox.Show("Медицинская карта успешно создана!\nТеперь вы можете записаться к врачу.",
                        "Успех", MessageBoxButton.OK, MessageBoxImage.Information);

                    if (MainWindow.Instance != null)
                    {
                        MainWindow.Instance.NavigateToMainMenu();
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка создания карты: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (MainWindow.Instance != null)
            {
                MainWindow.Instance.NavigateToIdentification();
            }
        }
    }
}