namespace YanaulKioskPerfect
{
    public class MessageViewModel
    {
        public int MessageId { get; set; }
        public string SenderType { get; set; }
        public string SenderName { get; set; }
        public string MessageText { get; set; }
        public string TimeString { get; set; }
        public string Alignment { get; set; }
        public bool IsTypingIndicator { get; set; }
    }
}
