using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SlotsController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public SlotsController(DatabaseConnection db)
        {
            _db = db;
        }

        // GET: /api/slots/exceptions
        [HttpGet("exceptions")]
        public IActionResult GetUpcomingExceptionSlots()
        {
            var slots = new List<SlotDto>();

            using var conn = _db.GetConnection();
            conn.Open();

            string query = @"
        SELECT SlotId, SlotDate, SlotTime, Status
        FROM Slots
        WHERE SlotDate >= CURRENT_DATE
        ORDER BY SlotDate, SlotTime;
    ";

            using var cmd = new NpgsqlCommand(query, conn);

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                slots.Add(new SlotDto
                {
                    SlotId = (int)reader["SlotId"],
                    SlotDate = (DateTime)reader["SlotDate"],
                    SlotTime = reader["SlotTime"].ToString(),
                    Status = reader["Status"].ToString()
                });
            }

            return Ok(slots);
        }

    }

    public class SlotDto
    {
        public int SlotId { get; set; }
        public DateTime SlotDate { get; set; }
        public string SlotTime { get; set; }
        public string Status { get; set; } // 'Unavailable' or 'Maintenance'
    }
}
