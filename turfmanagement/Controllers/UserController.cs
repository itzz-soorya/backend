using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class UserController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public UserController(DatabaseConnection db)
        {
            _db = db;
        }

        
        [HttpGet("check")]
        public IActionResult CheckUserExists([FromQuery] string phoneNumber) {
            using var conn = _db.GetConnection();
            conn.Open();

            string query = "SELECT UserId, Name FROM Users WHERE PhoneNumber = @phone";
            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@phone", phoneNumber);

            using var reader = cmd.ExecuteReader();
            if (reader.Read())
            {
                return Ok(new
                {
                    message = "User exists",
                    userId = reader["UserId"],
                    name = reader["Name"]
                });
            }

            return NotFound(new { message = "User not found" });
        }

        [HttpPost("register")]
        public IActionResult RegisterUser([FromBody] UserDto userDto)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            // Check if user already exists
            string checkQuery = "SELECT 1 FROM Users WHERE PhoneNumber = @phone";
            using var checkCmd = new NpgsqlCommand(checkQuery, conn);
            checkCmd.Parameters.AddWithValue("@phone", userDto.PhoneNumber);

            using var reader = checkCmd.ExecuteReader();
            if (reader.Read())
            {
                return Conflict(new { message = "User already exists" });
            }
            reader.Close();

            // Insert new user
            string insertQuery = "INSERT INTO Users (PhoneNumber, Name) VALUES (@phone, @name) RETURNING UserId";
            using var insertCmd = new NpgsqlCommand(insertQuery, conn);
            insertCmd.Parameters.AddWithValue("@phone", userDto.PhoneNumber);
            insertCmd.Parameters.AddWithValue("@name", userDto.Name ?? (object)DBNull.Value);

            int newUserId = (int)insertCmd.ExecuteScalar();

            return Created("", new
            {
                message = "User created",
                userId = newUserId,
                name = userDto.Name,
                phoneNumber = userDto.PhoneNumber
            });
        }
        [HttpPut("rename")]
        public IActionResult RenameUser([FromBody] RenameUserRequest request)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            string updateQuery = @"
        UPDATE Users
        SET Name = @newName
        WHERE PhoneNumber = @phoneNumber;
    ";

            using var cmd = new NpgsqlCommand(updateQuery, conn);
            cmd.Parameters.AddWithValue("@newName", request.NewName);
            cmd.Parameters.AddWithValue("@phoneNumber", request.PhoneNumber);

            int rowsAffected = cmd.ExecuteNonQuery();

            if (rowsAffected > 0)
            {
                return Ok(new { success = true, message = "User name updated successfully." });
            }
            else
            {
                return NotFound(new { success = false, message = "User not found for given phone number." });
            }
        }

        public class RenameUserRequest
        {
            public string PhoneNumber { get; set; }
            public string NewName { get; set; }
        }


    }

    public class UserDto
    {
        public string PhoneNumber { get; set; }
        public string? Name { get; set; }
    }

}
