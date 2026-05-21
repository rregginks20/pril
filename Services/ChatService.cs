using System;
using System.Text.RegularExpressions;

namespace YanaulKioskPerfect.Services
{
    public static class ChatService
    {
        /// <summary>
        /// Нормализует номер полиса: удаляет пробелы, дефисы, приводит к верхнему регистру
        /// </summary>
        public static string NormalizePolicy(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy))
                return string.Empty;

            // Удаляем все не буквенно-цифровые символы
            string cleaned = Regex.Replace(policy.ToUpper(), @"[^A-Z0-9]", "");
            
            return cleaned;
        }

        /// <summary>
        /// Проверяет корректность номера полиса ОМС (базовая проверка)
        /// </summary>
        public static bool IsValidPolicy(string policy)
        {
            if (string.IsNullOrWhiteSpace(policy))
                return false;

            string normalized = NormalizePolicy(policy);
            
            // Полис ОМС может быть 16 цифр (новый) или 9-12 символов (старый)
            return normalized.Length >= 9 && normalized.Length <= 18;
        }
    }
}
