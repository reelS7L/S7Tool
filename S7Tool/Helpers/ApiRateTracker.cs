namespace S7Tool.Services
{
    public class ApiRateTracker
    {
        public int RequestsToday { get; set; }
        public int MaxRequestsPerDay { get; set; } = 50;

        public DateTime? RetryAt { get; set; }

        public int Remaining => Math.Max(0, MaxRequestsPerDay - RequestsToday);

        public bool CanSend()
        {
            if (RetryAt != null && DateTime.Now < RetryAt)
                return false;

            return RequestsToday < MaxRequestsPerDay;
        }

        public void RegisterRequest()
        {
            RequestsToday++;
        }

        public void SetCooldown(int seconds)
        {
            RetryAt = DateTime.Now.AddSeconds(seconds);
        }

        public string GetStatus()
        {
            if (RetryAt != null && DateTime.Now < RetryAt)
            {
                var sec = (RetryAt.Value - DateTime.Now).Seconds;
                return string.Format(LocalizationManager.T("Str_AiChat_Cooldown"), sec);
            }

            return string.Format(LocalizationManager.T("Str_AiChat_RemainingRequests"), Remaining, MaxRequestsPerDay);
        }
    }
}