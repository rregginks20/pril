using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Windows;
using iTextSharp.text;
using iTextSharp.text.pdf;
using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using YanaulKioskPerfect;

namespace YanaulKioskPerfect
{
    public class EmailService
    {
        // ================= НАСТРОЙКИ ЯНДЕКС ПОЧТЫ =================
        private const string SmtpServer = "smtp.yandex.ru";
        private const int SmtpPort = 587;

        private const string SenderEmail = "terminalsamoobsluzhivania@yandex.ru";
        private const string SenderPassword = "lcegwthtwzftddpx";
        // ============================================================

        public void SendDocumentsToPatient(string recipientEmail, MedicalCard patient, YanaulCRBEntities2 db)
        {
            string logFile = Path.Combine(Environment.CurrentDirectory, "email_debug.log");
            File.AppendAllText(logFile, $"\n[{DateTime.Now}] НАЧАЛО ОТПРАВКИ на {recipientEmail}\n");

            if (string.IsNullOrWhiteSpace(recipientEmail) || !recipientEmail.Contains("@"))
            {
                MessageBox.Show("Введите корректный Email адрес!", "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            string tempFolder = "";
            string zipPath = "";

            try
            {
                tempFolder = Path.Combine(Path.GetTempPath(), $"MedDocs_{Guid.NewGuid()}");
                Directory.CreateDirectory(tempFolder);
                int filesCount = 0;

                // СПРАВКИ
                var certs = db.Certificates.Where(c => c.PatientPolicy == patient.PolicyNumber && c.Status == "активен").ToList();
                foreach (var cert in certs)
                {
                    string filePath = Path.Combine(tempFolder, $"Spravka_{cert.CertificateType}.pdf");
                    CreatePdfFile(filePath, patient, "СПРАВКА", $"{cert.CertificateType}\n\n{cert.CertificateText}", $"Дата: {cert.IssueDate:dd.MM.yyyy}");
                    filesCount++;
                }

                // РЕЦЕПТЫ
                var prescs = db.Prescriptions.Where(p => p.PolicyNumber == patient.PolicyNumber && p.Status == "активен").ToList();
                foreach (var p in prescs)
                {
                    string filePath = Path.Combine(tempFolder, $"Recept_{p.IssueDate:dd.MM.yyyy}.pdf");
                    CreatePdfFile(filePath, patient, "РЕЦЕПТ", $"{p.Medicines}\n\n{p.DosageInstructions}", $"{p.IssueDate:dd.MM.yyyy}");
                    filesCount++;
                }

                // АНАЛИЗЫ
                var analyses = db.AnalysisResults.Where(a => a.PolicyNumber == patient.PolicyNumber).ToList();
                foreach (var a in analyses)
                {
                    string filePath = Path.Combine(tempFolder, $"Analiz_{a.ResultDate:dd.MM.yyyy}.pdf");
                    CreatePdfFile(filePath, patient, "АНАЛИЗ", $"{a.Details}\n\n{a.DoctorComment}", $"{a.ResultDate:dd.MM.yyyy}");
                    filesCount++;
                }

                if (filesCount == 0)
                {
                    MessageBox.Show("Нет документов для отправки.", "Инфо", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                // ZIP
                zipPath = Path.Combine(Path.GetTempPath(), $"MedicalRecords_{patient.PatientName}_{DateTime.Now:yyyyMMddHHmm}.zip");
                if (File.Exists(zipPath)) File.Delete(zipPath);
                ZipFile.CreateFromDirectory(tempFolder, zipPath);
                System.Threading.Thread.Sleep(1000);

                // ОТПРАВКА ЧЕРЕЗ MAILKIT
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("ГБУЗ РБ Янаульская ЦРБ", SenderEmail));
                message.To.Add(new MailboxAddress(patient.PatientName, recipientEmail));
                message.Subject = $"Медицинские документы (Полис: {patient.PolicyNumber})";

                var bodyBuilder = new BodyBuilder
                {
                    TextBody = $"Здравствуйте, {patient.PatientName}!\n\n" +
                               $"Во вложении ваши документы из Янаульской ЦРБ.\n\n" +
                               $"С уважением,\nАдминистрация больницы"
                };
                bodyBuilder.Attachments.Add(zipPath);
                message.Body = bodyBuilder.ToMessageBody();

                using (var client = new SmtpClient())
                {
                    File.AppendAllText(logFile, $"[CONNECT] {SmtpServer}:{SmtpPort}...\n");
                    client.Connect(SmtpServer, SmtpPort, SecureSocketOptions.StartTls);

                    File.AppendAllText(logFile, $"[AUTHENTICATE] {SenderEmail}...\n");
                    client.Authenticate(SenderEmail, SenderPassword);

                    File.AppendAllText(logFile, $"[SEND]...\n");
                    client.Send(message);

                    File.AppendAllText(logFile, $"[DISCONNECT]...\n");
                    client.Disconnect(true);
                    File.AppendAllText(logFile, "[SUCCESS] Отправлено!\n");
                }

                MessageBox.Show($"✅ Документы отправлены на:\n{recipientEmail}", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string msg = $"❌ Ошибка:\n{ex.Message}\n";
                if (ex.InnerException != null) msg += $"\n{ex.InnerException.Message}";

                File.AppendAllText(logFile, $"[ERROR] {msg}\n");
                MessageBox.Show(msg, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                try
                {
                    if (!string.IsNullOrEmpty(zipPath) && File.Exists(zipPath)) File.Delete(zipPath);
                    if (!string.IsNullOrEmpty(tempFolder) && Directory.Exists(tempFolder)) Directory.Delete(tempFolder, true);
                }
                catch { }
            }
        }

        private void CreatePdfFile(string path, MedicalCard patient, string title, string content, string footer)
        {
            try
            {
                Document doc = new Document(PageSize.A4, 40, 40, 40, 40);
                PdfWriter.GetInstance(doc, new FileStream(path, FileMode.Create));
                doc.Open();

                string fontPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Fonts), "arial.ttf");
                BaseFont bf = BaseFont.CreateFont(fontPath, BaseFont.IDENTITY_H, BaseFont.EMBEDDED);

                Font titleFont = new Font(bf, 16, Font.BOLD);
                Font textFont = new Font(bf, 12, Font.NORMAL);

                doc.Add(new Paragraph(title, titleFont));
                doc.Add(new Paragraph("\n"));
                doc.Add(new Paragraph($"Пациент: {patient.PatientName}", textFont));
                doc.Add(new Paragraph($"Полис: {patient.PolicyNumber}", textFont));
                doc.Add(new Paragraph("\n"));
                doc.Add(new Paragraph(content, textFont));
                doc.Add(new Paragraph("\n\n"));
                doc.Add(new Paragraph(footer, textFont));

                doc.Close();
            }
            catch (Exception ex)
            {
                File.WriteAllText(path.Replace(".pdf", ".txt"), $"Error: {ex.Message}\n\n{content}");
            }
        }

        public void TestSendEmail(string recipientEmail)
        {
            string logFile = Path.Combine(Environment.CurrentDirectory, "email_test.log");
            File.AppendAllText(logFile, $"\n[{DateTime.Now}] === ТЕСТ ОТПРАВКИ ===\n");

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("ТЕСТ", SenderEmail));
                message.To.Add(new MailboxAddress("Тест", recipientEmail));
                message.Subject = $"ТЕСТ {DateTime.Now:dd.MM.yyyy HH:mm:ss}";
                message.Body = new TextPart("plain")
                {
                    Text = "Яндекс почта работает! Тест прошел успешно через MailKit."
                };

                using (var client = new SmtpClient())
                {
                    File.AppendAllText(logFile, $"Connecting to {SmtpServer}:{SmtpPort}...\n");
                    client.Connect(SmtpServer, SmtpPort, SecureSocketOptions.StartTls);

                    File.AppendAllText(logFile, $"Authenticating as {SenderEmail}...\n");
                    client.Authenticate(SenderEmail, SenderPassword);

                    File.AppendAllText(logFile, "Sending message...\n");
                    client.Send(message);

                    File.AppendAllText(logFile, "Disconnecting...\n");
                    client.Disconnect(true);

                    File.AppendAllText(logFile, "✅ SUCCESS!\n");
                }

                MessageBox.Show("✅ ТЕСТ ПРОЙДЕН!\n\nПисьмо отправлено через MailKit.\nПроверьте почту!", "Успех", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                string error = $"❌ ОШИБКА:\n{ex.Message}";
                if (ex.InnerException != null)
                    error += $"\n\nДетали: {ex.InnerException.Message}";

                File.AppendAllText(logFile, $"❌ {error}\n");
                MessageBox.Show(error, "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}