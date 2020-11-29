using System;
namespace NetCore_WebService.Models
{
    public class User
    {
        public long Seq { get; set; }
        public string Account { get; set; }
        public string Password { get; set; }
        public string Email { get; set; }
        public string CreateTime { get; set; }

        public string RefreshToken { get; set; }
        public DateTime RefreshTokenLimitTime { get; set; }
    }
}
