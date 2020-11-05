using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using MySql.Data.MySqlClient;
using NetCore_WebService.Models;

namespace NetCore_WebService
{
    public interface IJwtTokenHelper
    {
        (string, DateTime) GenerateToken(byte[] key, User user);
        bool IsValidToken(HttpContext context, byte[] key);
        JwtSecurityToken ValidateToken(HttpContext context, byte[] key);
        Task<(string, DateTime)> ValidateRefreshToken(string account, string refreshToken, DateTime refreshTokenLimitTime);
        Task<AuthResponse> ValidateTokens(byte[] key, User user);
    }

    public class JwtTokenHelper: IJwtTokenHelper
    {
        private readonly ILogger<JwtTokenHelper> _logger;
        private readonly string _connStr;

        public JwtTokenHelper(ILogger<JwtTokenHelper> logger, IOptions<AppSettings> settings)
        {
            _logger = logger;
            _connStr = settings.Value.ConnectionString;
        }

        private string CreateRefreshToken()
        {
            var randomNumber = new byte[32];
            using var generator = new RNGCryptoServiceProvider();
            generator.GetBytes(randomNumber);
            return Convert.ToBase64String(randomNumber);
        }

        public (string, DateTime) GenerateToken(byte[] key, User user)
        {
            var tokenLimitTime = DateTime.UtcNow.AddDays(1);
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                new Claim("account", user.Account),
                }),
                Expires = tokenLimitTime,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha512Signature)
            };

            var tokenHandler = new JwtSecurityTokenHandler();
            var token = tokenHandler.CreateToken(tokenDescriptor);
            var authToken = tokenHandler.WriteToken(token);

            return (authToken, tokenLimitTime);
        }

        public bool IsValidToken(HttpContext context, byte[] key)
        {
            try
            {
                var validJwtToken = ValidateToken(context, key);
            }
            catch
            {
                return false;
            }

            return true;
        }

        public JwtSecurityToken ValidateToken(HttpContext context, byte[] key)
        {
            var authHeader = context.Request.Headers["authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader))
            {
                _logger.LogError("Authorization header value is null.");
                throw new ArgumentException($"Invalid JWT");
            }
            var authToken = authHeader.Replace("Bearer ", "");
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ValidateIssuer = false,
                ValidateAudience = false
            };
            var jwtHandler = new JwtSecurityTokenHandler();
            var principal = jwtHandler.ValidateToken(authToken, validationParameters, out var validToken);

            return validToken as JwtSecurityToken;
        }

        public async Task<(string, DateTime)> ValidateRefreshToken(string account, string refreshToken, DateTime refreshTokenLimitTime)
        {
            if (refreshToken == null || refreshTokenLimitTime < DateTime.UtcNow)
            {
                refreshToken = CreateRefreshToken();
                refreshTokenLimitTime = DateTime.UtcNow.AddDays(14);

                using var conn = new MySqlConnection(_connStr);
                await conn.OpenAsync();
                MySqlTransaction dbt = await conn.BeginTransactionAsync();

                try
                {
                    await dbt.Connection.ExecuteAsync(
                        "update user set refreshToken = @refreshToken, refreshTokenLimitTime = @refreshTokenLimitTime where account = @account;",
                        new { account });
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex.StackTrace);
                    await dbt.RollbackAsync();
                    await conn.CloseAsync();
                    throw new Exception("RefreshToken is not saved.");
                }

                _logger.LogInformation($"Refresh token re-issued | Reason: Refresh token expired | Target: {account}");
            }

            return (refreshToken, refreshTokenLimitTime);
        }

        public async Task<AuthResponse> ValidateTokens(byte[] key, User user)
        {
            try
            {
                // 1. 만료 여부 체크 후 재발급
                (user.RefreshToken, user.RefreshTokenLimitTime) =
                    await ValidateRefreshToken(user.Account, user.RefreshToken, user.RefreshTokenLimitTime);

                // 2. auth token 신규 발급
                var (authToken, tokenLimitTime) = GenerateToken(key, user);

                // 3. token, expire time, refresh token 리턴
                return new AuthResponse
                {
                    AuthToken = authToken,
                    ExpireTime = tokenLimitTime,
                    RefreshToken = user.RefreshToken
                };
            }
            catch (Exception)
            {
                throw new Exception();
            }
        }
    }
}
