using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public AdminController(DatabaseConnection db)
        {
            _db = db;
        }

        // 1. CREATE: Register new admin
        [HttpPost("register")]
        
        public IActionResult RegisterAdmin([FromBody] AdminDto admin)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            string query = "INSERT INTO Admin (username, password) VALUES (@username, @password)";
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@username", admin.Username);
            cmd.Parameters.AddWithValue("@password", admin.Password);

            try
            {
                cmd.ExecuteNonQuery();
                return Ok("Admin registered successfully.");
            }
            catch (PostgresException ex) when (ex.SqlState == "23505")
            {
                return Conflict("Username already exists.");
            }
        }

        // 2. LOGIN: Check credentials
        [HttpPost("login")]
        public IActionResult Login([FromBody] AdminDto admin)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            string query = "SELECT COUNT(*) FROM Admin WHERE username = @username AND password = @password";
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@username", admin.Username);
            cmd.Parameters.AddWithValue("@password", admin.Password);

            long count = (long)cmd.ExecuteScalar();
            if (count > 0)
                return Ok("Success");
            else
                return Unauthorized("Decline");
        }

        // 3. RESET: Update password
        [HttpPut("reset-password")]
        public IActionResult ResetPassword([FromBody] AdminDto admin)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            string query = "UPDATE Admin SET password = @password WHERE username = @username";
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@username", admin.Username);
            cmd.Parameters.AddWithValue("@password", admin.Password);

            int rows = cmd.ExecuteNonQuery();
            if (rows > 0)
                return Ok("Password updated successfully.");
            else
                return NotFound("Username not found.");
        }
    }

    public class AdminDto
    {
        public string Username { get; set; }
        public string Password { get; set; }
    }
}
