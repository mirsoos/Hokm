
namespace Hokm.Application.DTOs.Sms
{
    public class SmsRequest
    {
        public string Mobile { get; set; }
        public int TemplateId { get; set; }
        public SmsParameter[] Parameters { get; set; }
    }
}