using System;
using System.Data.Entity;
using System.Linq;

namespace YanaulKioskPerfect.Services
{
    public class QueueService
    {
        public const string StatusWaiting = "waiting";
        public const string StatusCalled = "called";
        public const string StatusCompleted = "completed";

        public class PatientQueueStatus
        {
            public bool IsInQueue { get; set; }
            public int Position { get; set; }
            public int PeopleAhead { get; set; }
            public string Status { get; set; }
            public int DoctorId { get; set; }
            public string DoctorName { get; set; }
            public string Room { get; set; }
        }

        public PatientQueueStatus GetPatientQueueStatus(string policy)
        {
            using (var db = new YanaulCRBEntities2())
            {
                var appointment = db.Appointments
                    .Include(a => a.Doctor)
                    .FirstOrDefault(a => a.PatientPolicy == policy 
                        && a.AppointmentTime >= DateTime.Now.Date 
                        && a.AppointmentTime < DateTime.Now.Date.AddDays(1));

                if (appointment == null)
                    return new PatientQueueStatus { IsInQueue = false };

                var queueEntry = db.VirtualQueues
                    .FirstOrDefault(v => v.AppointmentId == appointment.Id 
                        && (v.Status == StatusWaiting || v.Status == StatusCalled));

                if (queueEntry == null)
                    return new PatientQueueStatus { IsInQueue = false };

                var doctor = appointment.Doctor;
                
                // Считаем количество людей перед текущим пациентом
                // Сначала загружаем данные в память, чтобы избежать ошибок EF
                var allQueues = db.VirtualQueues
                    .Include(v => v.Appointment)
                    .ToList();
                
                int peopleAhead = allQueues
                    .Count(v => v.Appointment != null 
                        && v.Appointment.DoctorId == doctor.Id 
                        && (v.Status == StatusWaiting || v.Status == StatusCalled)
                        && v.QueuePosition < queueEntry.QueuePosition);

                return new PatientQueueStatus
                {
                    IsInQueue = true,
                    Position = queueEntry.QueuePosition,
                    PeopleAhead = peopleAhead,
                    Status = queueEntry.Status,
                    DoctorId = doctor.Id,
                    DoctorName = doctor.Name,
                    Room = doctor.Room
                };
            }
        }

        /// <summary>
        /// Регистрация пациента в очереди по полису (кнопка "Я пришёл")
        /// </summary>
        public VirtualQueue RegisterCheckIn(string policy)
        {
            using (var db = new YanaulCRBEntities2())
            {
                // Находим активную запись на сегодня
                var appointment = db.Appointments
                    .Include(a => a.Doctor)
                    .FirstOrDefault(a => a.PatientPolicy == policy
                        && a.AppointmentTime >= DateTime.Now.Date
                        && a.AppointmentTime < DateTime.Now.Date.AddDays(1));

                if (appointment == null)
                    return null;

                // Проверяем, есть ли уже запись в очереди
                var existing = db.VirtualQueues
                    .FirstOrDefault(v => v.AppointmentId == appointment.Id);

                if (existing != null)
                {
                    if (existing.Status == StatusCompleted)
                    {
                        // Если уже завершён, не добавляем снова
                        return null;
                    }
                    existing.Status = StatusWaiting;
                    existing.UpdatedAt = DateTime.Now;
                    db.SaveChanges();
                    return existing;
                }
                else
                {
                    // Определяем следующую позицию в очереди для этого врача
                    // Сначала загружаем данные в память, чтобы избежать ошибок EF
                    var allQueues = db.VirtualQueues
                        .Include(v => v.Appointment)
                        .ToList();
                        
                    int maxPos = allQueues
                        .Where(v => v.Appointment != null 
                            && v.Appointment.DoctorId == appointment.DoctorId 
                            && (v.Status == StatusWaiting || v.Status == StatusCalled))
                        .Select(v => (int?)v.QueuePosition)
                        .Max() ?? 0;

                    var newEntry = new VirtualQueue
                    {
                        AppointmentId = appointment.Id,
                        QueuePosition = maxPos + 1,
                        EstimatedWaitMinutes = maxPos * 12,
                        Status = StatusWaiting,
                        Notified = false,
                        UpdatedAt = DateTime.Now
                    };
                    db.VirtualQueues.Add(newEntry);
                    db.SaveChanges();
                    return newEntry;
                }
            }
        }

        public void JoinQueue(int appointmentId)
        {
            using (var db = new YanaulCRBEntities2())
            {
                var appointment = db.Appointments
                    .Include(a => a.Doctor)
                    .FirstOrDefault(a => a.Id == appointmentId);

                if (appointment == null)
                    throw new Exception("Приём не найден");

                // Проверяем, есть ли уже запись в очереди
                var existing = db.VirtualQueues
                    .FirstOrDefault(v => v.AppointmentId == appointmentId);

                if (existing != null)
                {
                    existing.Status = StatusWaiting;
                    existing.UpdatedAt = DateTime.Now;
                }
                else
                {
                    // Определяем следующую позицию в очереди для этого врача
                    // Сначала загружаем данные в память, чтобы избежать ошибок EF
                    var allQueues = db.VirtualQueues
                        .Include(v => v.Appointment)
                        .ToList();
                        
                    int maxPos = allQueues
                        .Where(v => v.Appointment != null 
                            && v.Appointment.DoctorId == appointment.DoctorId 
                            && (v.Status == StatusWaiting || v.Status == StatusCalled))
                        .Select(v => (int?)v.QueuePosition)
                        .Max() ?? 0;

                    var newEntry = new VirtualQueue
                    {
                        AppointmentId = appointmentId,
                        QueuePosition = maxPos + 1,
                        EstimatedWaitMinutes = maxPos * 12,
                        Status = StatusWaiting,
                        Notified = false,
                        UpdatedAt = DateTime.Now
                    };
                    db.VirtualQueues.Add(newEntry);
                }

                db.SaveChanges();
            }
        }

        public void CallNextPatient(int doctorId)
        {
            using (var db = new YanaulCRBEntities2())
            {
                // Находим первого ожидающего пациента у этого врача
                var nextPatient = db.VirtualQueues
                    .Include(v => v.Appointment)
                    .FirstOrDefault(v => v.Appointment.DoctorId == doctorId 
                        && v.Status == StatusWaiting
                        && v.Appointment.AppointmentTime >= DateTime.Now.Date
                        && v.Appointment.AppointmentTime < DateTime.Now.Date.AddDays(1));

                if (nextPatient != null)
                {
                    // Сбрасываем статус у текущего вызванного пациента (если есть)
                    var currentCalled = db.VirtualQueues
                        .FirstOrDefault(v => v.Appointment.DoctorId == doctorId 
                            && v.Status == StatusCalled);
                    
                    if (currentCalled != null)
                    {
                        currentCalled.Status = StatusCompleted;
                        currentCalled.UpdatedAt = DateTime.Now;
                    }

                    // Вызываем следующего
                    nextPatient.Status = StatusCalled;
                    nextPatient.Notified = false;
                    nextPatient.UpdatedAt = DateTime.Now;
                    
                    db.SaveChanges();
                }
            }
        }

        public void CompleteVisit(int appointmentId)
        {
            using (var db = new YanaulCRBEntities2())
            {
                var queueEntry = db.VirtualQueues
                    .FirstOrDefault(v => v.AppointmentId == appointmentId);

                if (queueEntry != null)
                {
                    queueEntry.Status = StatusCompleted;
                    queueEntry.UpdatedAt = DateTime.Now;
                    db.SaveChanges();
                }
            }
        }

        public void LeaveQueue(int appointmentId)
        {
            using (var db = new YanaulCRBEntities2())
            {
                var queueEntry = db.VirtualQueues
                    .FirstOrDefault(v => v.AppointmentId == appointmentId);

                if (queueEntry != null)
                {
                    db.VirtualQueues.Remove(queueEntry);
                    db.SaveChanges();
                }
            }
        }
    }
}
