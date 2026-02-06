namespace U_Wii_X_Fusion.GameTransfer.Models
{
    public class TransferProgress
    {
        public string GameName { get; set; }
        public long TotalSize { get; set; }
        public long TransferredSize { get; set; }
        public int Percentage { get; set; }
        public string Status { get; set; }
        public bool IsComplete { get; set; }
        public bool IsError { get; set; }
        public string ErrorMessage { get; set; }
    }
}
