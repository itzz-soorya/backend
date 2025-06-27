using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;
using System;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class BookingController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public BookingController(DatabaseConnection db)
        {
            _db = db;
        }

        [HttpPost("book")]
        public IActionResult BookSlot([FromBody] BookSlotDto dto)
        {
            if (dto.UserId <= 0 || dto.BookingDate == default ||
                string.IsNullOrWhiteSpace(dto.SlotTimeFrom) ||
                string.IsNullOrWhiteSpace(dto.SlotTimeTo) || dto.Amount <= 0)
            {
                Console.WriteLine("❌ Invalid booking input data.");
                return BadRequest(new { message = "Invalid input data. Please fill all fields correctly." });
            }

            using var conn = _db.GetConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {
                // 1. Insert into Bookings
                int bookingId = 0;
                string insertBooking = @"
                    INSERT INTO Bookings (UserId, BookingDate, SlotTimeFrom, SlotTimeTo, Amount)
                    VALUES (@userId, @date, @from, @to, @amount)
                    RETURNING BookingId;
                ";

                using (var cmdBooking = new NpgsqlCommand(insertBooking, conn))
                {
                    cmdBooking.Parameters.AddWithValue("@userId", dto.UserId);
                    cmdBooking.Parameters.AddWithValue("@date", dto.BookingDate);
                    cmdBooking.Parameters.AddWithValue("@from", dto.SlotTimeFrom);
                    cmdBooking.Parameters.AddWithValue("@to", dto.SlotTimeTo);
                    cmdBooking.Parameters.AddWithValue("@amount", dto.Amount);
                    cmdBooking.Transaction = tran;

                    var result = cmdBooking.ExecuteScalar();
                    if (result == null)
                        throw new Exception("Booking insert failed (no ID returned)");

                    bookingId = Convert.ToInt32(result);
                }

                // 2. Insert each Slot
                DateTime from = DateTime.ParseExact(dto.SlotTimeFrom, "hh:mm tt", null);
                DateTime to = DateTime.ParseExact(dto.SlotTimeTo, "hh:mm tt", null);
                if (to <= from) to = to.AddDays(1); // Handle overnight

                for (DateTime time = from; time < to; time = time.AddHours(1))
                {
                    string timeStr = time.ToString("hh:mm tt");
                    string insertSlot = @"
                        INSERT INTO Slots (SlotDate, SlotTime, Status)
                        VALUES (@date, @time, 'Unavailable');
                    ";

                    using var cmdSlot = new NpgsqlCommand(insertSlot, conn);
                    cmdSlot.Parameters.AddWithValue("@date", dto.BookingDate);
                    cmdSlot.Parameters.AddWithValue("@time", timeStr);
                    cmdSlot.Transaction = tran;

                    int affectedRows = cmdSlot.ExecuteNonQuery();
                    if (affectedRows != 1)
                        throw new Exception($"Slot insert failed for time {timeStr}");
                }

                // 3. Update User's LastBookingDate
                string updateUser = @"
                    UPDATE Users
                    SET LastBookingDate = @date
                    WHERE UserId = @userId;
                ";

                using (var cmdUser = new NpgsqlCommand(updateUser, conn))
                {
                    cmdUser.Parameters.AddWithValue("@date", dto.BookingDate);
                    cmdUser.Parameters.AddWithValue("@userId", dto.UserId);
                    cmdUser.Transaction = tran;

                    int rows = cmdUser.ExecuteNonQuery();
                    if (rows != 1)
                        throw new Exception("User update failed (no rows affected)");
                }

                tran.Commit();
                Console.WriteLine($"✅ Booking completed. Booking ID: {bookingId}");
                return Ok(new { message = "Booking successful", bookingId });
            }
            catch (Exception ex)
            {
                try { tran.Rollback(); } catch { /* optional: log rollback failure */ }

                Console.WriteLine($"❌ Booking failed: {ex.Message}");
                return StatusCode(500, new { message = "Booking failed", error = ex.Message });
            }
        }
    }

    public class BookSlotDto
    {
        public int UserId { get; set; }
        public DateTime BookingDate { get; set; }
        public string SlotTimeFrom { get; set; }  // e.g., "11:00 PM"
        public string SlotTimeTo { get; set; }    // e.g., "02:00 AM"
        public decimal Amount { get; set; }
    }
}
