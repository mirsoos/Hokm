using Hokm.Application.DTOs.Sms;
using Hokm.Application.Interfaces;
using Hokm.Infrastructure.Configurations;
using System.Text;
using System.Text.Json;

namespace Hokm.Infrastructure.Services.Sms
{
    public class SmsIrService : ISmsService
    {
        private readonly HttpClient _httpClient;
        private readonly InfrastructureSettings _settings;

        public SmsIrService(HttpClient httpClient,InfrastructureSettings settings)
        {
            _httpClient = httpClient;
            _settings = settings;
            _httpClient.DefaultRequestHeaders.Clear();
            _httpClient.DefaultRequestHeaders.Add("x-api-key", _settings.SmsIrApiKey);
            _httpClient.DefaultRequestHeaders.Add("Accept", "text/plain");
        }

        public async Task<SmsSendResult> SendVerificationCodeAsync(string phoneNumber, string code)
        {
            var requestBody = new
            {
                mobile = phoneNumber,
                templateId = _settings.SmsIrTemplateId,
                parameters = new[]
                {
                    new { name = "Code", value = code }
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            try
            {
                var response = await _httpClient.PostAsync("https://api.sms.ir/v1/send/verify",content);

                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    var result = JsonSerializer.Deserialize<SmsIrResponse>(responseBody,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    return result?.Status == 1
                        ? new SmsSendResult(true, "ارسال موفق", result.Data?.MessageId)
                        : new SmsSendResult(false, result?.Message ?? "خطای نامشخص");
                }

                return new SmsSendResult(false, $"خطای HTTP: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                return new SmsSendResult(false, $"خطا در ارسال: {ex.Message}");
            }
        }


        private class SmsIrResponse
        {
            public int Status { get; set; }
            public string Message { get; set; } = string.Empty;
            public SmsIrData? Data { get; set; }
        }

        private class SmsIrData
        {
            public long MessageId { get; set; }
            public double Cost { get; set; }
        }
    }
}
