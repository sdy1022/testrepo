using System.ComponentModel.DataAnnotations;

namespace Code.Models
{
    public enum LogLevel
    {
        Debug,
        Information,
        Warning,
        Error,
        Fatal
    }
    /// <summary>
    /*
         {
      "ApplicationName": "YourApp",
      "Timestamp": "2025-02-05T10:30:00Z",
      "Message": "Your log message",
      "Environment": "Production",
      "Level": "Information"
    }
     */
    /// </summary>
    public class LogMessage
    {
        [Required]
        public string ApplicationName { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        [Required]
        public string Message { get; set; }

        [Required]
        public string Environment { get; set; }

        [Required]
        public LogLevel Level { get; set; }

        
    }
}
