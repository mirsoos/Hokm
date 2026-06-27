
namespace Hokm.Application.DTOs.Sms
{
    public class SmsSendResult
    {
        public bool IsSuccess { get; set; }
        public string Message { get; set; }
        public long? MessageId { get; set; }

        public SmsSendResult() { }

        public SmsSendResult(bool isSuccess, string message, long? messageId = null)
        {
            IsSuccess = isSuccess;
            Message = message;
            MessageId = messageId;
        }
    }
}
