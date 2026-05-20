using System;

namespace YanaulKioskPerfect
{
    // Простой класс для хранения отзыва, если его нет в базе данных
    public class Feedback
    {
        public int Id { get; set; }
        public string PatientName { get; set; }
        public string Message { get; set; }
        public int Rating { get; set; }
        public DateTime Date { get; set; }
        public int? DoctorId { get; set; }
    }
}