using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;
using System;
using System.Collections.Generic;

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

        [HttpPost("mark-maintenance")]
        public IActionResult MarkSlotsAsMaintenance([FromBody] SlotMaintenanceRequest request)
        {
            if (request.TimeSlots == null || request.TimeSlots.Count == 0)
                return BadRequest("No time slots provided.");

            using var conn = _db.GetConnection();
            conn.Open();

            foreach (var time in request.TimeSlots)
            {
                string checkQuery = @"
                    SELECT COUNT(*) FROM Slots
                    WHERE SlotDate = @date AND SlotTime = @time;
                ";

                using (var checkCmd = new NpgsqlCommand(checkQuery, conn))
                {
                    checkCmd.Parameters.AddWithValue("@date", request.Date);
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
                        updateCmd.Parameters.AddWithValue("@date", request.Date);
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
                        insertCmd.Parameters.AddWithValue("@date", request.Date);
                        insertCmd.Parameters.AddWithValue("@time", time);
                        insertCmd.ExecuteNonQuery();
                    }
                }
            }

            return Ok(new { message = "Selected slots marked as Maintenance." });
        }
    }

    public class SlotMaintenanceRequest
    {
        public DateTime Date { get; set; }
        public List<string> TimeSlots { get; set; }
    }
}
