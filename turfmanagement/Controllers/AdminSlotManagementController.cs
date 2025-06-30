using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminSlotManagementController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public AdminSlotManagementController(DatabaseConnection db)
        {
            _db = db;
        }

        // ✅ Helper: Normalize slot time to "h tt" (like "10 PM")
        private string NormalizeToSlotFormat(string rawTime)
        {
            string[] allowedFormats = { "h tt", "hh tt", "h:mm tt", "hh:mm tt", "H:mm", "HH:mm" };

            if (DateTime.TryParseExact(rawTime.Trim(), allowedFormats, CultureInfo.InvariantCulture,
                DateTimeStyles.None, out var parsedTime))
            {
                return parsedTime.ToString("h tt", CultureInfo.InvariantCulture);
            }

            return rawTime.Trim(); // fallback
        }

        // ✅ Mark slots as Maintenance
        [HttpPost("mark-maintenance")]
        public IActionResult MarkSlotsAsMaintenance([FromBody] SlotMaintenanceRequest request)
        {
            if (request.TimeSlots == null || request.TimeSlots.Count == 0)
                return BadRequest("No time slots provided.");

            using var conn = _db.GetConnection();
            conn.Open();

            foreach (var timeRaw in request.TimeSlots)
            {
                string time = NormalizeToSlotFormat(timeRaw);

                string checkQuery = @"
                    SELECT COUNT(*) FROM Slots
                    WHERE SlotDate = @date AND SlotTime = @time;
                ";

                using var checkCmd = new NpgsqlCommand(checkQuery, conn);
                checkCmd.Parameters.AddWithValue("@date", request.Date.Date);
                checkCmd.Parameters.AddWithValue("@time", time);
                int count = Convert.ToInt32(checkCmd.ExecuteScalar());

                if (count > 0)
                {
                    string updateQuery = @"
                        UPDATE Slots
                        SET Status = 'Maintenance'
                        WHERE SlotDate = @date AND SlotTime = @time;
                    ";

                    using var updateCmd = new NpgsqlCommand(updateQuery, conn);
                    updateCmd.Parameters.AddWithValue("@date", request.Date.Date);
                    updateCmd.Parameters.AddWithValue("@time", time);
                    updateCmd.ExecuteNonQuery();
                }
                else
                {
                    string insertQuery = @"
                        INSERT INTO Slots (SlotDate, SlotTime, Status)
                        VALUES (@date, @time, 'Maintenance');
                    ";

                    using var insertCmd = new NpgsqlCommand(insertQuery, conn);
                    insertCmd.Parameters.AddWithValue("@date", request.Date.Date);
                    insertCmd.Parameters.AddWithValue("@time", time);
                    insertCmd.ExecuteNonQuery();
                }
            }

            return Ok(new { message = "Selected slots marked as Maintenance." });
        }

        // ✅ DELETE slots with Status = Maintenance
        [HttpDelete]
        public IActionResult DeleteSlots([FromBody] SlotMaintenanceRequest request)
        {
            if (request.TimeSlots == null || request.TimeSlots.Count == 0)
                return BadRequest("No time slots provided.");

            using var conn = _db.GetConnection();
            conn.Open();

            int totalDeleted = 0;

            foreach (var timeRaw in request.TimeSlots)
            {
                string time = NormalizeToSlotFormat(timeRaw);

                string deleteQuery = @"
                    DELETE FROM Slots
                    WHERE SlotDate = @date AND SlotTime = @time AND Status = 'Maintenance';
                ";

                using var deleteCmd = new NpgsqlCommand(deleteQuery, conn);
                deleteCmd.Parameters.AddWithValue("@date", request.Date.Date);
                deleteCmd.Parameters.AddWithValue("@time", time);

                totalDeleted += deleteCmd.ExecuteNonQuery();
            }

            if (totalDeleted == 0)
                return NotFound(new { message = "No matching maintenance slots found to delete." });

            return Ok(new { message = $"{totalDeleted} slot(s) deleted successfully." });
        }
    }

    public class SlotMaintenanceRequest
    {
        [DataType(DataType.Date)] 
        [Required]
        public DateTime Date { get; set; }

        [Required]
        public List<string> TimeSlots { get; set; }
    }
}
