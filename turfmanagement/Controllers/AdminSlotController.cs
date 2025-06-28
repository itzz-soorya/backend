using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminSlotController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public AdminSlotController(DatabaseConnection db)
        {
            _db = db;
        }

        // GET: /api/AdminSlot/2025-06-25
        [HttpGet("{date}")]
        public IActionResult GetFormattedSlots(string date)
        {
            if (!DateTime.TryParse(date, out DateTime parsedDate))
                return BadRequest(new { message = "Invalid date format. Use YYYY-MM-DD" });

            var slots = new List<FormattedSlotDto>();

            using var conn = _db.GetConnection();
            conn.Open();

            string query = @"
                SELECT SlotId, SlotDate, SlotTime, Status
                FROM Slots
                WHERE SlotDate = @date
                ORDER BY SlotTime;
            ";

            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@date", parsedDate);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                string fromTimeRaw = reader["SlotTime"].ToString(); // e.g., "16:00" or "4 PM"
                string formattedSlot = FormatSlotTime(fromTimeRaw);

                slots.Add(new FormattedSlotDto
                {
                    SlotId = (int)reader["SlotId"],
                    SlotDate = (DateTime)reader["SlotDate"],
                    SlotTime = formattedSlot,
                    Status = reader["Status"].ToString()
                });
            }

            return Ok(slots);
        }

        private string FormatSlotTime(string startTime)
        {
            if (DateTime.TryParseExact(startTime, "h tt", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedShort))
            {
                var endShort = parsedShort.AddHours(1);
                return $"{parsedShort:h tt} TO {endShort:h tt}".ToUpper();
            }
            else if (DateTime.TryParse(startTime, out var parsed))
            {
                var end = parsed.AddHours(1);
                return $"{parsed:h tt} TO {end:h tt}".ToUpper();
            }

            return startTime; // fallback (unformatted)
        }
    }

    public class FormattedSlotDto
    {
        public int SlotId { get; set; }
        public DateTime SlotDate { get; set; }
        public string SlotTime { get; set; } // "4 PM TO 5 PM"
        public string Status { get; set; }
    }
}
