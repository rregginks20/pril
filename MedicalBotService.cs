using System;
using System.Collections.Generic;
using System.Linq;

namespace YanaulKioskPerfect.Services
{
    public class MedicalBotService
    {
        private readonly Dictionary<string, BotResponse> _knowledgeBase = new Dictionary<string, BotResponse>
        {
            { "запис", new BotResponse("📅 Записаться к врачу можно в разделе 'Запись на приём'.", new[] { "📅 Записаться сейчас" }) },
            { "врач", new BotResponse("👨‍⚕️ Выберите специалиста в меню или опишите симптомы.", new[] { "🔍 Подобрать врача" }) },
            { "справк", new BotResponse("📄 Справку можно получить у терапевта после осмотра.", new[] { "📅 Записаться к терапевту" }) },
            { "анализ", new BotResponse("📋 Результаты анализов обычно готовы через 3-5 дней. Проверьте в разделе 'Медкарта'.", new[] { "📋 Открыть анализы" }) },
            { "температур", new BotResponse("🌡️ При температуре выше 38.5 рекомендуется вызвать врача на дом.", new[] { "🏠 Вызвать врача" }) },
            { "боль", new BotResponse("🤕 Если у вас острая боль, обратитесь в регистратуру или вызовите 112.", Array.Empty<string>()) },
            { "адрес", new BotResponse("📍 Мы находимся: г. Янаул, ул. Худайбердина, 65А.", Array.Empty<string>()) },
            { "телефон", new BotResponse("📞 Телефон регистратуры: +7 (34759) 2-12-34.", Array.Empty<string>()) }
        };

        public BotResponse GetBotResponse(string userMessage)
        {
            if (string.IsNullOrEmpty(userMessage))
                return new BotResponse("Пожалуйста, введите ваш вопрос.", Array.Empty<string>());

            string lowerMessage = userMessage.ToLower();

            foreach (var kvp in _knowledgeBase)
            {
                if (lowerMessage.Contains(kvp.Key))
                {
                    return kvp.Value;
                }
            }

            return new BotResponse(
                "Я не совсем понял вопрос 🤖. Попробуйте перефразировать или нажмите кнопку '👨‍⚕️ Связаться с врачом'.",
                new[] { "👨‍⚕️ Связаться с врачом" }
            );
        }

        public List<string> GetQuickActions()
        {
            return new List<string>
            {
                "📅 Записаться к врачу",
                "📄 Получить справку",
                "📋 Результаты анализов",
                "💳 Оплата услуг",
                "📍 Как вас найти?"
            };
        }

        public bool ShouldEscalateToDoctor(string userMessage, int botAttempts)
        {
            if (botAttempts >= 2) return true;
            string lower = userMessage.ToLower();
            return lower.Contains("врач") || lower.Contains("консультация") || lower.Contains("диагноз");
        }
    }

    public class BotResponse
    {
        public string Text { get; set; }
        public string[] QuickButtons { get; set; }

        public BotResponse(string text, string[] quickButtons = null)
        {
            Text = text;
            QuickButtons = quickButtons ?? Array.Empty<string>();
        }
    }
}