using System;
using System.Linq;
using System.Speech.Synthesis;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Data.Entity;
using YanaulKioskPerfect.Services;

namespace YanaulKioskPerfect
{
    public partial class VirtualQueuePage : Page, ILocalizable
    {
        private readonly QueueService _queueService = new QueueService();
        private DispatcherTimer refreshTimer;
        private SpeechSynthesizer speaker;
        private string lastAnnouncedTicket = "";

        public VirtualQueuePage()
        {
            InitializeComponent();
            UpdateLanguage();

            speaker = new SpeechSynthesizer();
            try { speaker.SelectVoiceByHints(VoiceGender.Female, VoiceAge.Adult); } catch { }
            speaker.Rate = 0;
            speaker.Volume = 100;

            LoadQueue();

            refreshTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            refreshTimer.Tick += (s, e) => LoadQueue();
            refreshTimer.Start();

            Unloaded += (s, e) =>
            {
                refreshTimer?.Stop();
                if (speaker != null) { speaker.SpeakAsyncCancelAll(); speaker.Dispose(); }
            };
        }

        private void LoadQueue()
        {
            if (QueuePanel == null) return;
            QueuePanel.Children.Clear();

            try
            {
                string policy = ChatService.NormalizePolicy(MainWindow.CurrentPatientPolicy);
                var myStatus = _queueService.GetPatientQueueStatus(policy);

                using (var db = new YanaulCRBEntities2())
                {
                    int? filterDoctorId = myStatus.IsInQueue ? (int?)myStatus.DoctorId : null;

                    // Сначала загружаем данные в память, чтобы избежать ошибок EF
                    var allQueues = db.VirtualQueues
                        .Include(v => v.Appointment)
                        .Include(v => v.Appointment.Doctor)
                        .ToList();

                    var today = DateTime.Now.Date;
                    var tomorrow = today.AddDays(1);

                    var query = allQueues
                        .Where(v => (v.Status == QueueService.StatusWaiting || v.Status == QueueService.StatusCalled)
                            && v.Appointment != null
                            && v.Appointment.AppointmentTime >= today
                            && v.Appointment.AppointmentTime < tomorrow)
                        .OrderBy(v => v.QueuePosition)
                        .Take(12);

                    if (filterDoctorId.HasValue)
                        query = query.Where(v => v.Appointment.DoctorId == filterDoctorId.Value);

                    var queueList = query.ToList();

                    if (QueueHeaderText != null)
                    {
                        QueueHeaderText.Text = filterDoctorId.HasValue
                            ? $"ОЧЕРЕДЬ: {myStatus.DoctorName} (каб. {myStatus.Room})"
                            : "ОБЩАЯ ОЧЕРЕДЬ — войдите по полису для персональной позиции";
                    }

                    if (queueList.Count == 0)
                    {
                        QueuePanel.Children.Add(new TextBlock
                        {
                            Text = "Очередь пуста.\nЗапишитесь к врачу и нажмите «Я пришёл» в медкарте.",
                            FontSize = 22,
                            Foreground = Brushes.Gray,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            Margin = new Thickness(0, 40, 0, 0),
                            TextAlignment = TextAlignment.Center
                        });
                        if (PositionText != null) PositionText.Text = "—";
                        if (WaitTimeText != null) WaitTimeText.Text = "—";
                        if (MyStatusText != null) MyStatusText.Text = "Не в очереди";
                        return;
                    }

                    int displayPos = 1;
                    VirtualQueue called = null;

                    foreach (var item in queueList)
                    {
                        bool isCalled = item.Status == QueueService.StatusCalled;
                        if (isCalled) called = item;

                        AddQueueItem(
                            item.Appointment?.PatientName ?? "Пациент",
                            item.Appointment?.TicketNumber ?? "",
                            item.Appointment?.Doctor?.Room ?? "?",
                            isCalled ? "📢 ПРОЙДИТЕ В КАБИНЕТ" : "Ожидает",
                            isCalled,
                            displayPos++,
                            item.EstimatedWaitMinutes ?? 15);
                    }

                    if (called != null && called.Appointment != null
                        && called.Appointment.TicketNumber != lastAnnouncedTicket
                        && (called.Notified == false || !called.Notified.HasValue))
                    {
                        lastAnnouncedTicket = called.Appointment.TicketNumber;
                        SpeakAppointment(called.Appointment);
                        called.Notified = true;
                        db.SaveChanges();
                    }

                    if (myStatus.IsInQueue)
                    {
                        if (PositionText != null)
                            PositionText.Text = myStatus.Status == QueueService.StatusCalled ? "СЕЙЧАС" : $"№ {myStatus.Position}";
                        if (WaitTimeText != null)
                            WaitTimeText.Text = myStatus.Status == QueueService.StatusCalled
                                ? "Идите в кабинет!"
                                : $"~{Math.Max(5, myStatus.PeopleAhead * 12)} мин";
                        if (MyStatusText != null)
                            MyStatusText.Text = myStatus.Status == QueueService.StatusCalled
                                ? "🔔 Вас вызывают!"
                                : $"Перед вами: {myStatus.PeopleAhead} чел.";
                    }
                    else
                    {
                        if (PositionText != null) PositionText.Text = "—";
                        if (WaitTimeText != null) WaitTimeText.Text = "—";
                        if (MyStatusText != null) MyStatusText.Text = "Войдите по полису";
                    }
                }
            }
            catch (Exception ex)
            {
                QueuePanel.Children.Add(new TextBlock
                {
                    Text = "Ошибка: " + ex.Message,
                    Foreground = Brushes.Red,
                    TextWrapping = TextWrapping.Wrap
                });
            }
        }

        private void SpeakAppointment(Appointment appt)
        {
            string room = appt.Doctor?.Room ?? "неизвестно";
            string text = $"Внимание. Пациент {appt.PatientName}, талон {appt.TicketNumber}. Пройдите в кабинет {room}.";
            speaker?.SpeakAsync(text);
        }

        private void AddQueueItem(string name, string ticket, string room, string status, bool isCurrent, int position, int waitTime)
        {
            var border = new Border
            {
                Background = isCurrent ? new SolidColorBrush(Color.FromRgb(76, 175, 80)) : Brushes.White,
                Padding = new Thickness(18),
                Margin = new Thickness(0, 0, 0, 10),
                BorderThickness = new Thickness(2),
                BorderBrush = isCurrent ? Brushes.White : new SolidColorBrush(Color.FromRgb(33, 150, 243)),
                CornerRadius = new CornerRadius(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 8, ShadowDepth = 2, Opacity = 0.15 }
            };

            var stack = new StackPanel();
            var header = new StackPanel { Orientation = Orientation.Horizontal };
            header.Children.Add(new TextBlock
            {
                Text = isCurrent ? "📢 " : $"{position}. ",
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = isCurrent ? Brushes.White : Brushes.Black
            });
            header.Children.Add(new TextBlock
            {
                Text = name,
                FontSize = 18,
                FontWeight = FontWeights.Bold,
                Foreground = isCurrent ? Brushes.White : Brushes.Black,
                Margin = new Thickness(6, 0, 0, 0)
            });
            stack.Children.Add(header);
            stack.Children.Add(new TextBlock
            {
                Text = $"Талон {ticket} • каб. {room} • {status}" + (isCurrent ? "" : $" • ~{waitTime} мин"),
                FontSize = 14,
                Foreground = isCurrent ? Brushes.White : Brushes.DimGray,
                Margin = new Thickness(0, 4, 0, 0)
            });
            border.Child = stack;
            QueuePanel.Children.Add(border);
        }

        public void UpdateLanguage()
        {
            if (TitleText != null) TitleText.Text = MainWindow.IsBashkir ? "ВИРТУАЛЬ ҺӘМ" : "ВИРТУАЛЬНАЯ ОЧЕРЕДЬ";
            if (RefreshBtn != null) RefreshBtn.Content = MainWindow.IsBashkir ? "🔄 ЯҢЫЛАТ" : "🔄 Обновить";
        }

        private void BtnBack_Click(object sender, RoutedEventArgs e) => MainWindow.Instance.NavigateToMainMenu();

        private void BtnMyTickets_Click(object sender, RoutedEventArgs e)
            => MainWindow.Instance.NavigateToMyTickets();
        private void RefreshBtn_Click(object sender, RoutedEventArgs e) => LoadQueue();

        private void NotifyBtn_Click(object sender, RoutedEventArgs e)
        {
            var st = _queueService.GetPatientQueueStatus(ChatService.NormalizePolicy(MainWindow.CurrentPatientPolicy));
            if (!st.IsInQueue)
            {
                MessageBox.Show("Сначала нажмите «Я пришёл» в медкарте.", "Очередь", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            MessageBox.Show($"Вы в очереди №{st.Position}.\nСледите за экраном — вас вызовут голосом.", "Уведомление", MessageBoxButton.OK);
        }
    }
}
