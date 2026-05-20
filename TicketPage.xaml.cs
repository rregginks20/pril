using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;
using System.IO;
using System.Printing;
using System.Windows.Documents;
using Microsoft.Win32;
using System.Windows.Media;
using PdfSharp.Pdf;
using PdfSharp.Drawing;
using PdfSharp.Drawing.Layout;
using PdfSharp.Fonts;

namespace YanaulKioskPerfect
{
    public partial class TicketPage : Page, ILocalizable
    {
        private Appointment _appointment;
        private Doctor _doctor;
        private MedicalCard _patient;

        public TicketPage(Appointment appointment)
        {
            InitializeComponent();
            _appointment = appointment;
            LoadAppointmentData();
            GenerateQRCode();
        }

        private void LoadAppointmentData()
        {
            try
            {
                using (var db = new YanaulCRBEntities2())
                {
                    _doctor = db.Doctors.FirstOrDefault(d => d.DoctorId == _appointment.DoctorId);
                    _patient = db.MedicalCards.FirstOrDefault(p => p.PolicyNumber == _appointment.PolicyNumber);

                    if (TicketNumberText != null)
                        TicketNumberText.Text = _appointment.TicketNumber ?? "YNA-XXXXXXXXXX";

                    if (AppointmentDateText != null)
                        AppointmentDateText.Text = _appointment.AppointmentTime.ToString("dd.MM.yyyy HH:mm");

                    if (AppointmentDoctorText != null)
                        AppointmentDoctorText.Text = _doctor?.FullNameRU ?? "Врач не найден";

                    if (AppointmentSpecialtyText != null)
                    {
                        string specialty = _doctor?.SpecialtyRU ?? "Специальность не указана";
                        string room = _doctor?.Room ?? "—";
                        AppointmentSpecialtyText.Text = specialty + " • Кабинет " + room;
                    }

                    if (AppointmentPatientText != null)
                        AppointmentPatientText.Text = _patient?.PatientName ?? "Пациент не найден";

                    if (AppointmentPolicyText != null)
                        AppointmentPolicyText.Text = "Полис: " + _appointment.PolicyNumber;

                    if (AppointmentPriceText != null)
                    {
                        if (!string.IsNullOrEmpty(_appointment.Notes) && _appointment.Notes.Contains("Платная услуга"))
                        {
                            AppointmentPriceText.Text = MainWindow.IsBashkir ? "Түләүле хеҙмәт (түләнгән)" : "Платная услуга (оплачено)";
                            AppointmentPriceText.Foreground = System.Windows.Media.Brushes.Orange;
                        }
                        else
                        {
                            AppointmentPriceText.Text = MainWindow.IsBashkir ? "Буш (ОМС буйынса)" : "Бесплатно (по ОМС)";
                            AppointmentPriceText.Foreground = System.Windows.Media.Brushes.Green;
                        }
                    }

                    if (ImportantInfoText != null)
                    {
                        ImportantInfoText.TextWrapping = TextWrapping.Wrap;
                        ImportantInfoText.Text = MainWindow.IsBashkir ?
                            "Ҡабул итеү башланғансы 15 минут алдан килегеҙ. Үҙегеҙ менән паспорт һәм ОМС полисы алығыҙ." :
                            "Подойдите к кабинету за 15 минут до начала приема. При себе иметь паспорт и полис ОМС.";
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("Ошибка загрузки данных талона: " + ex.Message);
            }
        }

        private void GenerateQRCode()
        {
            if (QRCodeImage == null) return;

            try
            {
                string qrData = "https://health.bashkortostan.ru/appointment/" + _appointment.TicketNumber;
                string qrUrl = "https://api.qrserver.com/v1/create-qr-code/?size=200x200&data=" + Uri.EscapeDataString(qrData);

                var bitmap = new BitmapImage();
                bitmap.BeginInit();
                bitmap.UriSource = new Uri(qrUrl);
                bitmap.CacheOption = BitmapCacheOption.OnLoad;
                bitmap.EndInit();

                QRCodeImage.Source = bitmap;

                if (QRCodeUrlText != null)
                    QRCodeUrlText.Text = "health.bashkortostan.ru";
            }
            catch
            {
                if (QRCodeUrlText != null)
                    QRCodeUrlText.Text = "health.bashkortostan.ru";
            }
        }

        private void BtnPrint_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                PrintDialog printDialog = new PrintDialog();
                printDialog.UserPageRangeEnabled = true;

                if (printDialog.ShowDialog() == true)
                {
                    FlowDocument doc = CreateTicketDocument();
                    IDocumentPaginatorSource idps = doc;
                    printDialog.PrintDocument(idps.DocumentPaginator, MainWindow.IsBashkir ? "Ҡабул итеү талоны" : "Талон на прием");

                    MessageBox.Show(
                        MainWindow.IsBashkir ?
                            "✅ Талон баҫтырыуға ебәрелде!\n\nНомер: " + _appointment.TicketNumber :
                            "✅ Талон отправлен на печать!\n\nНомер: " + _appointment.TicketNumber,
                        MainWindow.IsBashkir ? "Баҫтырыу" : "Печать",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    MainWindow.IsBashkir ? "Баҫтырыу хатаһы: " + ex.Message : "Ошибка печати: " + ex.Message,
                    MainWindow.IsBashkir ? "Хата" : "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                SaveFileDialog saveDialog = new SaveFileDialog
                {
                    Filter = "PDF файл|*.pdf",
                    FileName = "Ticket_" + _appointment.TicketNumber,
                    DefaultExt = ".pdf"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    string filePath = saveDialog.FileName;
                    SaveAsPDF(filePath);

                    MessageBox.Show(
                        MainWindow.IsBashkir ?
                            "✅ Талон PDF форматында һаҡланды!\n\nНомер: " + _appointment.TicketNumber + "\nПуть: " + filePath :
                            "✅ Талон сохранен в PDF!\n\nНомер: " + _appointment.TicketNumber + "\nПуть: " + filePath,
                        MainWindow.IsBashkir ? "Һаҡланған" : "Сохранено",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);

                    System.Diagnostics.Process.Start(filePath);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    MainWindow.IsBashkir ? "Һаҡлау хатаһы: " + ex.Message : "Ошибка сохранения: " + ex.Message,
                    MainWindow.IsBashkir ? "Хата" : "Ошибка",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
            }
        }

        private void SaveAsPDF(string filePath)
        {
            GlobalFontSettings.FontResolver = new FontResolver();

            PdfDocument document = new PdfDocument();
            document.Info.Title = MainWindow.IsBashkir ? "Ҡабул итеү талоны" : "Талон на прием";
            document.Info.Author = "Yanaulskaya CRB";

            // МАЛЕНЬКИЙ РАЗМЕР A5 (как реальный талон)
            PdfPage page = document.AddPage();
            page.Size = PdfSharp.PageSize.A5;
            page.Orientation = PdfSharp.PageOrientation.Portrait;

            XGraphics gfx = XGraphics.FromPdfPage(page);

            XFont titleFont = new XFont("Helvetica", 12, XFontStyleEx.Bold);
            XFont headerFont = new XFont("Helvetica", 9, XFontStyleEx.Bold);
            XFont normalFont = new XFont("Helvetica", 8, XFontStyleEx.Regular);
            XFont smallFont = new XFont("Helvetica", 7, XFontStyleEx.Regular);
            XFont ticketNumberFont = new XFont("Courier", 14, XFontStyleEx.Bold);

            double yPosition = 20;
            double leftMargin = 20;
            double rightMargin = page.Width.Point - 20;
            double contentWidth = rightMargin - leftMargin;

            // Заголовок - КИРИЛЛИЦЕЙ!
            string hospitalName = MainWindow.IsBashkir ?
                "ЯҢАУЫЛ ҮҘӘК РАЙОН ДАУАХАНАҺЫ" :
                "ЯНАУЛЬСКАЯ ЦРБ";

            gfx.DrawString(hospitalName,
                titleFont, XBrushes.DarkGreen,
                new XRect(leftMargin, yPosition, contentWidth, 20),
                XStringFormats.Center);
            yPosition += 22;

            string ticketTitle = MainWindow.IsBashkir ? "ҠАБУЛ ИТЕҮ ТАЛОНЫ" : "ТАЛОН НА ПРИЕМ";
            gfx.DrawString(ticketTitle,
                headerFont, XBrushes.Black,
                new XRect(leftMargin, yPosition, contentWidth, 16),
                XStringFormats.Center);
            yPosition += 25;

            // Номер талона
            XPen greenPen = new XPen(XColors.Green, 1.5);
            gfx.DrawRectangle(greenPen, XBrushes.LightGreen,
                new XRect(leftMargin, yPosition, contentWidth, 35));

            string ticketNumberLabel = "ТАЛОН №: ";
            gfx.DrawString(ticketNumberLabel + _appointment.TicketNumber,
                ticketNumberFont, XBrushes.DarkRed,
                new XRect(leftMargin, yPosition + 6, contentWidth, 25),
                XStringFormats.Center);
            yPosition += 45;

            // Цена
            string priceText = MainWindow.IsBashkir ?
                "Буш (ОМС)" :
                "Бесплатно (ОМС)";

            if (!string.IsNullOrEmpty(_appointment.Notes) && _appointment.Notes.Contains("Платная услуга"))
            {
                priceText = MainWindow.IsBashkir ?
                    "Түләүле" :
                    "Платно";
            }

            // Компактная информация
            yPosition += 8;
            double labelWidth = contentWidth / 2 - 5;

            // Левая колонка - Дата
            string dateLabel = MainWindow.IsBashkir ? "Дата:" : "Дата:";
            gfx.DrawString(dateLabel, headerFont, XBrushes.DarkGreen,
                new XRect(leftMargin, yPosition, labelWidth, 14), XStringFormats.TopLeft);
            gfx.DrawString(_appointment.AppointmentTime.ToString("dd.MM.yyyy HH:mm"),
                normalFont, XBrushes.Black,
                new XRect(leftMargin, yPosition + 14, labelWidth, 12),
                XStringFormats.TopLeft);

            // Правая колонка - Врач
            string doctorLabel = MainWindow.IsBashkir ? "Табип:" : "Врач:";
            gfx.DrawString(doctorLabel, headerFont, XBrushes.DarkGreen,
                new XRect(leftMargin + labelWidth + 10, yPosition, labelWidth, 14), XStringFormats.TopLeft);
            gfx.DrawString(_doctor?.FullNameRU ?? "",
                normalFont, XBrushes.Black,
                new XRect(leftMargin + labelWidth + 10, yPosition + 14, labelWidth, 12),
                XStringFormats.TopLeft);

            yPosition += 35;

            // Пациент
            string patientLabel = MainWindow.IsBashkir ? "Пациент:" : "Пациент:";
            gfx.DrawString(patientLabel, headerFont, XBrushes.DarkGreen,
                new XRect(leftMargin, yPosition, labelWidth, 14), XStringFormats.TopLeft);
            gfx.DrawString(_patient?.PatientName ?? "",
                normalFont, XBrushes.Black,
                new XRect(leftMargin, yPosition + 14, labelWidth, 12),
                XStringFormats.TopLeft);

            // Полис
            string policyLabel = MainWindow.IsBashkir ? "Полис:" : "Полис:";
            gfx.DrawString(policyLabel, headerFont, XBrushes.DarkGreen,
                new XRect(leftMargin + labelWidth + 10, yPosition, labelWidth, 14), XStringFormats.TopLeft);
            gfx.DrawString(_appointment.PolicyNumber,
                normalFont, XBrushes.Black,
                new XRect(leftMargin + labelWidth + 10, yPosition + 14, labelWidth, 12),
                XStringFormats.TopLeft);

            yPosition += 35;

            // Кабинет и стоимость
            string roomLabel = MainWindow.IsBashkir ? "Кабинет:" : "Кабинет:";
            gfx.DrawString(roomLabel + " " + (_doctor?.Room ?? ""), headerFont, XBrushes.Black,
                new XRect(leftMargin, yPosition, labelWidth, 12), XStringFormats.TopLeft);

            string priceLabel = MainWindow.IsBashkir ? "Оплата:" : "Оплата:";
            gfx.DrawString(priceLabel + " " + priceText, headerFont, XBrushes.Black,
                new XRect(leftMargin + labelWidth + 10, yPosition, labelWidth, 12), XStringFormats.TopLeft);

            yPosition += 25;

            // === QR-КОД (как в приложении!) ===
            double qrSize = 80;
            double qrX = (page.Width.Point - qrSize) / 2;

            try
            {
                string qrData = "https://health.bashkortostan.ru/appointment/" + _appointment.TicketNumber;
                string qrUrl = "https://api.qrserver.com/v1/create-qr-code/?size=80x80&data=" + Uri.EscapeDataString(qrData);

                var qrImage = new BitmapImage();
                qrImage.BeginInit();
                qrImage.UriSource = new Uri(qrUrl);
                qrImage.CacheOption = BitmapCacheOption.OnLoad;
                qrImage.EndInit();

                using (var stream = new MemoryStream())
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(qrImage));
                    encoder.Save(stream);
                    stream.Position = 0;

                    XImage xImage = XImage.FromStream(stream);
                    gfx.DrawImage(xImage, qrX, yPosition, qrSize, qrSize);
                }
            }
            catch { }

            yPosition += qrSize + 10;

            gfx.DrawString("health.bashkortostan.ru",
                smallFont, XBrushes.Gray,
                new XRect(0, yPosition, page.Width.Point, 12),
                XStringFormats.Center);

            yPosition += 20;

            // ВАЖНО
            double boxHeight = 50;
            gfx.DrawRectangle(XBrushes.LightPink,
                new XRect(leftMargin, yPosition, contentWidth, boxHeight));

            string importantTitle = MainWindow.IsBashkir ? "⚠️ МӨҺИМ!" : "⚠️ ВАЖНО!";
            gfx.DrawString(importantTitle, headerFont, XBrushes.Red,
                new XRect(leftMargin + 5, yPosition + 3, contentWidth - 10, 14),
                XStringFormats.TopLeft);

            string importantText = MainWindow.IsBashkir ?
                "15 минут алдан килегеҙ. Паспорт һәм ОМС алығыҙ." :
                "Придите за 15 мин. Паспорт и ОМС с собой.";

            XTextFormatter tf = new XTextFormatter(gfx);
            tf.Alignment = XParagraphAlignment.Left;
            tf.DrawString(importantText, smallFont, XBrushes.Black,
                new XRect(leftMargin + 5, yPosition + 18, contentWidth - 10, 30),
                XStringFormats.TopLeft);

            yPosition += boxHeight + 10;

            // Контакты
            gfx.DrawRectangle(XBrushes.LightBlue,
                new XRect(leftMargin, yPosition, contentWidth, 35));

            string contactsTitle = MainWindow.IsBashkir ? "Теркәлеү:" : "Регистратура:";
            gfx.DrawString(contactsTitle, headerFont, XBrushes.DarkBlue,
                new XRect(leftMargin + 5, yPosition + 3, contentWidth - 10, 12),
                XStringFormats.TopLeft);
            gfx.DrawString("+7 (34783) 2-14-79", normalFont, XBrushes.Black,
                new XRect(leftMargin + 5, yPosition + 16, contentWidth - 10, 12),
                XStringFormats.TopLeft);

            yPosition += 45;

            // Footer
            string footerText = MainWindow.IsBashkir ?
                "Талон: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm") :
                "Выдан: " + DateTime.Now.ToString("dd.MM.yyyy HH:mm");

            gfx.DrawString(footerText,
                smallFont, XBrushes.Gray,
                new XRect(leftMargin, page.Height.Point - 25, contentWidth, 15),
                XStringFormats.Center);

            document.Save(filePath);
            document.Close();
        }

        private FlowDocument CreateTicketDocument()
        {
            FlowDocument doc = new FlowDocument();
            doc.PagePadding = new Thickness(50);
            doc.ColumnWidth = 500;

            string priceText = MainWindow.IsBashkir ? "Буш (по ОМС)" : "Бесплатно (по ОМС)";
            if (!string.IsNullOrEmpty(_appointment.Notes) && _appointment.Notes.Contains("Платная услуга"))
            {
                priceText = MainWindow.IsBashkir ? "Түләүле хеҙмәт (түләнгән)" : "Платная услуга (оплачено)";
            }

            string titleText = MainWindow.IsBashkir ? "ЯҢАУЫЛ ҮҘӘК РАЙОН ДАУАХАНАҺЫ" : "ЯНАУЛЬСКАЯ ЦЕНТРАЛЬНАЯ РАЙОННАЯ БОЛЬНИЦА";
            Paragraph title = new Paragraph(new Run(titleText));
            title.FontSize = 24;
            title.FontWeight = FontWeights.Bold;
            title.TextAlignment = TextAlignment.Center;
            title.Foreground = Brushes.DarkGreen;
            doc.Blocks.Add(title);

            string subtitleText = MainWindow.IsBashkir ? "📋 ҠАБУЛ ИТЕҮ ТАЛОНЫ" : "📋 ТАЛОН НА ПРИЕМ";
            Paragraph subtitle = new Paragraph(new Run(subtitleText));
            subtitle.FontSize = 20;
            subtitle.FontWeight = FontWeights.Bold;
            subtitle.TextAlignment = TextAlignment.Center;
            subtitle.Margin = new Thickness(0, 20, 0, 20);
            doc.Blocks.Add(subtitle);

            Border ticketBorder = new Border
            {
                BorderBrush = Brushes.Green,
                BorderThickness = new Thickness(2),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(20),
                Child = new TextBlock
                {
                    Text = "ТАЛОН №: " + _appointment.TicketNumber,
                    FontSize = 28,
                    FontWeight = FontWeights.Bold,
                    Foreground = Brushes.DarkRed,
                    TextAlignment = TextAlignment.Center,
                    FontFamily = new FontFamily("Consolas")
                }
            };
            BlockUIContainer blockUI = new BlockUIContainer(ticketBorder);
            blockUI.Margin = new Thickness(0, 0, 0, 20);
            doc.Blocks.Add(blockUI);

            AddSection(doc, "📅 " + (MainWindow.IsBashkir ? "КӨН ҺӘМ ВАҚЫТ:" : "ДАТА И ВРЕМЯ:"), _appointment.AppointmentTime.ToString("dd.MM.yyyy HH:mm"));
            AddSection(doc, "👨‍⚕️ " + (MainWindow.IsBashkir ? "ТАБИП:" : "ВРАЧ:"), (_doctor?.FullNameRU ?? "Не указан") + "\n" + (_doctor?.SpecialtyRU ?? "") + " • Кабинет " + (_doctor?.Room ?? "—"));
            AddSection(doc, "👤 " + (MainWindow.IsBashkir ? "ПАЦИЕНТ:" : "ПАЦИЕНТ:"), (_patient?.PatientName ?? "Не указан") + "\nПолис: " + _appointment.PolicyNumber);
            AddSection(doc, "💰 " + (MainWindow.IsBashkir ? "БАҺА:" : "СТОИМОСТЬ:"), priceText);

            Paragraph important = new Paragraph();
            important.Inlines.Add(new Run((MainWindow.IsBashkir ? "⚠️ МӨҺИМ!\n" : "⚠️ ВАЖНО!\n")) { FontWeight = FontWeights.Bold, Foreground = Brushes.Red });
            important.Inlines.Add(new Run(MainWindow.IsBashkir ?
                "Ҡабул итеү башланғансы 15 минут алдан килегеҙ. Үҙегеҙ менән паспорт һәм ОМС полисы алығыҙ." :
                "Подойдите к кабинету за 15 минут до начала приема.\nПри себе иметь паспорт и полис ОМС."));
            important.Background = Brushes.LightPink;
            important.Padding = new Thickness(15);
            important.Margin = new Thickness(0, 20, 0, 20);
            doc.Blocks.Add(important);

            Paragraph contacts = new Paragraph();
            contacts.Inlines.Add(new Run((MainWindow.IsBashkir ? "📞 Теркәлеү кабинеттары:\n" : "📞 Контакты регистратуры:\n")) { FontWeight = FontWeights.Bold });
            contacts.Inlines.Add(new Run("+7 (34783) 2-14-79\n"));
            contacts.Inlines.Add(new Run("ул. Худайбердина, 65А, г. Янаул"));
            contacts.Background = Brushes.LightBlue;
            contacts.Padding = new Thickness(15);
            doc.Blocks.Add(contacts);

            return doc;
        }

        private void AddSection(FlowDocument doc, string title, string content)
        {
            Paragraph p = new Paragraph();
            p.Inlines.Add(new Run(title + "\n") { FontWeight = FontWeights.Bold, Foreground = Brushes.DarkGreen });
            p.Inlines.Add(new Run(content));
            p.Margin = new Thickness(0, 10, 0, 10);
            doc.Blocks.Add(p);
        }

        private void BtnHome_Click(object sender, RoutedEventArgs e)
        {
            var result = MessageBox.Show(
                MainWindow.IsBashkir ? "Төп менюға ҡайтырға?" : "Вернуться в главное меню?",
                MainWindow.IsBashkir ? "Сығыу" : "Выход",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question);

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
            LoadAppointmentData();

            if (BtnPrint != null)
                BtnPrint.Content = MainWindow.IsBashkir ? "🖨️ Талонды баҫтырыу" : "🖨️ Распечатать талон";

            if (BtnSave != null)
                BtnSave.Content = MainWindow.IsBashkir ? "💾 PDF-та һаҡлау" : "💾 Сохранить в PDF";

            if (BtnHome != null)
                BtnHome.Content = MainWindow.IsBashkir ? "🏠 Төп менюға" : "🏠 В главное меню";
        }
    }

    // === FONT RESOLVER ===
    public class FontResolver : IFontResolver
    {
        public byte[] GetFont(string faceName)
        {
            try
            {
                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                if (File.Exists(fontPath))
                {
                    return File.ReadAllBytes(fontPath);
                }

                fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "times.ttf");
                if (File.Exists(fontPath))
                {
                    return File.ReadAllBytes(fontPath);
                }

                fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "cour.ttf");
                if (File.Exists(fontPath))
                {
                    return File.ReadAllBytes(fontPath);
                }
            }
            catch { }

            return null;
        }

        public FontResolverInfo ResolveTypeface(string familyName, bool bold, bool italic)
        {
            return new FontResolverInfo(familyName, bold, italic);
        }
    }
}