using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;

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
            using var conn = _db.GetConnection();
            conn.Open();
            using var tran = conn.BeginTransaction();

            try
            {
                // 1. Insert booking
                string insertBooking = @"
                INSERT INTO Bookings (UserId, BookingDate, SlotTimeFrom, SlotTimeTo, Amount)
                VALUES (@userId, @date, @from, @to, @amount)
                RETURNING BookingId;
            ";

                using var cmdBooking = new NpgsqlCommand(insertBooking, conn);
                cmdBooking.Parameters.AddWithValue("@userId", dto.UserId);
                cmdBooking.Parameters.AddWithValue("@date", dto.BookingDate);
                cmdBooking.Parameters.AddWithValue("@from", dto.SlotTimeFrom);
                cmdBooking.Parameters.AddWithValue("@to", dto.SlotTimeTo);
                cmdBooking.Parameters.AddWithValue("@amount", dto.Amount);
                cmdBooking.Transaction = tran;

                int bookingId = (int)cmdBooking.ExecuteScalar();

                // 2. Insert each slot into Slots table
                DateTime from = DateTime.ParseExact(dto.SlotTimeFrom, "hh:mm tt", null);
                DateTime to = DateTime.ParseExact(dto.SlotTimeTo, "hh:mm tt", null);

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
                    cmdSlot.ExecuteNonQuery();
                }

                // 3. Update user's LastBookingDate
                string updateUser = @"
                UPDATE Users
                SET LastBookingDate = @date
                WHERE UserId = @userId;
            ";

                using var cmdUser = new NpgsqlCommand(updateUser, conn);
                cmdUser.Parameters.AddWithValue("@date", dto.BookingDate);
                cmdUser.Parameters.AddWithValue("@userId", dto.UserId);
                cmdUser.Transaction = tran;
                cmdUser.ExecuteNonQuery();

                tran.Commit();
                return Ok(new { message = "Booking successful", bookingId });
            }
            catch (Exception ex)
            {
                tran.Rollback();
                return StatusCode(500, new { message = "Booking failed", error = ex.Message });
            }
        }
    }

    public class BookSlotDto
    {
        public int UserId { get; set; }
        public DateTime BookingDate { get; set; }
        public string SlotTimeFrom { get; set; }  // e.g., "02:00 PM"
        public string SlotTimeTo { get; set; }    // e.g., "05:00 PM"
        public decimal Amount { get; set; }
    }


}
