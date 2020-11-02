using System;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MySql.Data.MySqlClient;
using NetCore_WebService.Models;

namespace NetCore_WebService.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class UserController: ControllerBase
    {
        private readonly ILogger<UserController> _logger;
        private readonly string _connStr;

        public UserController(ILogger<UserController> logger, IOptions<AppSettings> settings)
        {
            _logger = logger;
            _connStr = settings.Value.ConnectionString;
        }

        [HttpGet]
        public async Task<IActionResult> GetUserAsync()
        {
            using var conn = new MySqlConnection(_connStr);
            var userList = await conn.QueryAsync<User>("select * from user;");

            if (!userList.Any()) return NoContent();

            return Ok(userList);
        }

        [HttpPost]
        public async Task<IActionResult> InsertUserAsync([FromBody] User user)
        {
            if (!ModelState.IsValid) return BadRequest();

            using var conn = new MySqlConnection(_connStr);
            await conn.OpenAsync();
            MySqlTransaction dbt = await conn.BeginTransactionAsync();

            try
            {
                await dbt.Connection.ExecuteAsync(
                    "insert into user(`account`, `password`, `email`, `createTime`) values(@account, @password, @email, now());",
                    new { user.Account, user.Password, user.Email }
                );
            }
            catch(Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                await dbt.RollbackAsync();
                await conn.CloseAsync();
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }

        [HttpPut]
        public async Task<IActionResult> UpdateUserAsync([FromBody] User user)
        {
            if (!ModelState.IsValid) return BadRequest();

            using var conn = new MySqlConnection(_connStr);
            await conn.OpenAsync();
            MySqlTransaction dbt = await conn.BeginTransactionAsync();

            var userInfo = await dbt.Connection.QueryFirstOrDefaultAsync<User>(
                    "select * from user where account = @account limit 1;",
                    new { user.Account });

            if (userInfo == null) return NotFound();

            try
            {
                await dbt.Connection.ExecuteAsync(
                    "update user set password = @password, email = @email where account = @account;",
                    new { user.Password, user.Email, user.Account }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                await dbt.RollbackAsync();
                await conn.CloseAsync();
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }

        [HttpDelete]
        public async Task<IActionResult> DeleteUserAsync(string account)
        {
            if (string.IsNullOrEmpty(account)) return BadRequest();

            using var conn = new MySqlConnection(_connStr);
            await conn.OpenAsync();
            MySqlTransaction dbt = await conn.BeginTransactionAsync();

            var userInfo = await dbt.Connection.QueryFirstOrDefaultAsync<User>(
                    "select * from user where account = @account limit 1;",
                    new { account });

            if (userInfo == null)
            {
                await conn.CloseAsync();
                return NotFound();
            }

            try
            {
                await dbt.Connection.ExecuteAsync(
                    "delete from user where account = @account;",
                    new { account }
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(ex.StackTrace);
                await dbt.RollbackAsync();
                await conn.CloseAsync();
                return StatusCode(StatusCodes.Status500InternalServerError);
            }

            return Ok();
        }
    }
}
