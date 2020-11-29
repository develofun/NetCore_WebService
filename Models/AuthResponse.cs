using System;
namespace NetCore_WebService.Models
{
    public class AuthResponse
    {
        public string AuthToken { get; set; }
        public DateTime ExpireTime { get; set; }
        public string RefreshToken { get; set; }
        public string Account { get; set; }
    }
}
