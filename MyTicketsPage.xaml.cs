using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using YanaulKioskPerfect.Services;

namespace YanaulKioskPerfect
{
    public partial class MyTicketsPage : Page, ILocalizable
    {
        public MyTicketsPage()
        {
            InitializeComponent();
            UpdateLanguage();
            LoadTickets();
        }

        public void UpdateLanguage()
        {
            if (TitleText != null)
                TitleText.Text = MainWindow.IsBashkir ? "МИНЕҢ ЯЗМАЛАРЫМ" : "МОИ ЗАПИСИ";
        }

        private void LoadTickets()
        {
            TicketsPanel.Children.Clear();
            string policy = MainWindow.CurrentPatientPolicy;

            if (string.IsNullOrWhiteSpace(policy))
            {
                ShowEmpty("Войдите по полису на главном экране.");
                return;
            }

            MainWindow.ReloadPatientAppointments(policy);
            var appointments = MainWindow.Appointments
                .Where(a => a.Status != "отменена")
                .OrderByDescending(a => a.AppointmentTime)
                .ToList();

            if (appointments.Count == 0)
            {
                ShowEmpty(MainWindow.IsBashkir ? "Һеҙҙең актив яҙмаларығыҙ юҡ" : "У вас нет записей в базе данных");
                return;
            }

            foreach (var appt in appointments)
                TicketsPanel.Children.Add(BuildTicketCard(appt));
        }

        private void ShowEmpty(string text)
        {
            TicketsPanel.Children.Add(new TextBlock
            {
                Text = text,
                FontSize = 22,
                FontStyle = System.Windows.FontStyles.Italic,
                Foreground = System.Windows.Media.Brushes.Gray,
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 80, 0, 0),
                TextAlignment = TextAlignment.Center
            });
        }

        private UIElement BuildTicketCard(Appointment appt)
        {
            bool isPast = appt.AppointmentTime < DateTime.Now;
            var card = new Border
            {
                Style = (Style)Application.Current.Resources["CardStyle"],
                Width = 700,
                Margin = new Thickness(0, 0, 0, 20),
                Padding = new Thickness(20)
            };

            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = (MainWindow.IsBashkir ? "Талон: " : "Талон № ") + appt.TicketNumber,
                FontSize = 22,
                FontWeight = FontWeights.Bold,
                Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 105, 92))
            });
            stack.Children.Add(new TextBlock
            {
                Text = appt.AppointmentTime.ToString("dd.MM.yyyy HH:mm"),
                FontSize = 18,
                Margin = new Thickness(0, 8, 0, 0)
            });

            string doctorName = "—";
            if (appt.Doctor != null)
                doctorName = MainWindow.IsBashkir ? appt.Doctor.FullNameBA : appt.Doctor.FullNameRU;
            else
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var doc = db.Doctors.Find(appt.DoctorId);
                    if (doc != null)
                        doctorName = MainWindow.IsBashkir ? doc.FullNameBA : doc.FullNameRU;
                }
            }

            stack.Children.Add(new TextBlock { Text = "Врач: " + doctorName, FontSize = 17, Margin = new Thickness(0, 6, 0, 0) });
            stack.Children.Add(new TextBlock
            {
                Text = "Статус: " + (appt.Status ?? "активна"),
                FontSize = 16,
                FontWeight = FontWeights.SemiBold,
                Foreground = new System.Windows.Media.SolidColorBrush(isPast ? System.Windows.Media.Colors.Gray : System.Windows.Media.Colors.Green),
                Margin = new Thickness(0, 8, 0, 0)
            });

            var btnPanel = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 16, 0, 0) };

            if (!isPast && appt.Status == "активна")
            {
                var cancelBtn = new Button
                {
                    Content = "❌ Отменить запись",
                    Width = 200,
                    Height = 48,
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(211, 47, 47)),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Tag = appt
                };
                cancelBtn.Click += BtnCancel_Click;
                btnPanel.Children.Add(cancelBtn);

                var queueBtn = new Button
                {
                    Content = "📱 Я пришёл",
                    Width = 180,
                    Height = 48,
                    Margin = new Thickness(12, 0, 0, 0),
                    Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 152, 0)),
                    Foreground = System.Windows.Media.Brushes.White,
                    FontWeight = FontWeights.Bold,
                    Tag = appt
                };
                queueBtn.Click += BtnCheckIn_Click;
                btnPanel.Children.Add(queueBtn);
            }

            var printBtn = new Button
            {
                Content = "📄 Справка",
                Width = 160,
                Height = 48,
                Margin = new Thickness(12, 0, 0, 0),
                Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(33, 150, 243)),
                Foreground = System.Windows.Media.Brushes.White,
                Tag = appt
            };
            printBtn.Click += BtnGeneratePdf_Click;
            btnPanel.Children.Add(printBtn);

            stack.Children.Add(btnPanel);
            card.Child = stack;
            return card;
        }

        private void BtnCheckIn_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is Appointment appt)) return;

            var qs = new QueueService();
            var item = qs.RegisterCheckIn(appt.PolicyNumber);
            if (item != null)
            {
                var status = qs.GetPatientQueueStatus(appt.PolicyNumber);
                MessageBox.Show(
                    $"✅ Вы в очереди!\nПозиция №{item.QueuePosition}\nВрач: {status.DoctorName}\nКабинет: {status.Room}",
                    "Я пришёл", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
                MessageBox.Show("Нет активной записи на сегодня.", "Очередь", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private void BtnGeneratePdf_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is Appointment appt)) return;
            try
            {
                string path = Path.Combine(Path.GetTempPath(), $"Spravka_{appt.TicketNumber}.txt");
                File.WriteAllText(path,
                    $"СПРАВКА О ЗАПИСИ\nПациент: {appt.PatientName}\nПолис: {appt.PolicyNumber}\nВремя: {appt.AppointmentTime:dd.MM.yyyy HH:mm}\nТалон: {appt.TicketNumber}\nЯнаульская ЦРБ",
                    System.Text.Encoding.UTF8);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(path) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка");
            }
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            if (!(sender is Button btn) || !(btn.Tag is Appointment appt)) return;
            if (MessageBox.Show("Отменить запись в базе данных?", "Отмена", MessageBoxButton.YesNo) != MessageBoxResult.Yes)
                return;

            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    var a = db.Appointments.Find(appt.AppointmentId);
                    if (a != null) a.Status = "отменена";
                    var q = db.VirtualQueues.FirstOrDefault(v => v.AppointmentId == appt.AppointmentId);
                    if (q != null) q.Status = QueueService.StatusSkipped;
                    db.SaveChanges();
                }
                MainWindow.ReloadPatientAppointments(appt.PolicyNumber);
                LoadTickets();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Ошибка");
            }
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => MainWindow.Instance.NavigateToMainMenu();
    }
}
