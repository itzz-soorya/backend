using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;
using System;
using System.Collections.Generic;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminSingleUserDetailsController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public AdminSingleUserDetailsController(DatabaseConnection db)
        {
            _db = db;
        }

        [HttpGet("user-details/{phoneNumber}")]
        public IActionResult GetUserFullDetails(string phoneNumber)
        {
            using var conn = _db.GetConnection();
            conn.Open();

            // 1. Get user basic info + total bookings + total hours
            string query = @"
                SELECT 
                    u.UserId,
                    u.Name,
                    u.PhoneNumber,
                    u.LastBookingDate,
                    COUNT(b.BookingId) AS TotalBookings,
                    COALESCE(SUM(
                        CAST(SPLIT_PART(b.SlotTimeTo, ':', 1) AS INT) - 
                        CAST(SPLIT_PART(b.SlotTimeFrom, ':', 1) AS INT)
                    ), 0) AS TotalHours
                FROM Users u
                LEFT JOIN Bookings b ON u.UserId = b.UserId
                WHERE u.PhoneNumber = @phone
                GROUP BY u.UserId;
            ";

            using var cmd = new NpgsqlCommand(query, conn);
            cmd.Parameters.AddWithValue("@phone", phoneNumber);

            var reader = cmd.ExecuteReader();
            if (!reader.Read()) return NotFound(new { message = "User not found" });

            int userId = Convert.ToInt32(reader["UserId"]);
            DateTime? lastBookingDate = reader["LastBookingDate"] is DBNull
                ? (DateTime?)null
                : Convert.ToDateTime(reader["LastBookingDate"]);

            var user = new FullUserDetailDto
            {
                Name = reader["Name"].ToString(),
                PhoneNumber = reader["PhoneNumber"].ToString(),
                TotalBookings = Convert.ToInt32(reader["TotalBookings"]),
                LastBooking = lastBookingDate == null ? "N/A" : lastBookingDate.Value.ToString("dd/MM/yyyy"),
                TotalHours = reader["TotalHours"].ToString() + "Hrs",
                UpcomingBookings = new List<BookingDto>(),
                PastBookings = new List<BookingDto>()
            };
            reader.Close();

            // 2. Get upcoming bookings (BookingDate > today)
            string upcomingQuery = @"
    SELECT BookingDate, SlotTimeFrom, SlotTimeTo
    FROM Bookings
    WHERE TO_TIMESTAMP(BookingDate || ' ' || SlotTimeFrom, 'YYYY-MM-DD hh:mi AM') > NOW()
    AND UserId = @uid
    ORDER BY BookingDate;
";

            using var cmdUpcoming = new NpgsqlCommand(upcomingQuery, conn);
            cmdUpcoming.Parameters.AddWithValue("@uid", userId);
            using var upReader = cmdUpcoming.ExecuteReader();
            while (upReader.Read())
            {
                user.UpcomingBookings.Add(new BookingDto
                {
                    Date = Convert.ToDateTime(upReader["BookingDate"]).ToString("dd/MM/yyyy"),
                    TimeFrom = upReader["SlotTimeFrom"].ToString(),
                    TimeTo = upReader["SlotTimeTo"].ToString()
                });
            }
            upReader.Close();

            // 3. Get past bookings (BookingDate <= today)
            string pastQuery = @"
    SELECT BookingDate, SlotTimeFrom, SlotTimeTo
    FROM Bookings
    WHERE TO_TIMESTAMP(BookingDate || ' ' || SlotTimeTo, 'YYYY-MM-DD hh:mi AM') <= NOW()
    AND UserId = @uid
    ORDER BY BookingDate DESC;
";

            using var cmdPast = new NpgsqlCommand(pastQuery, conn);
            cmdPast.Parameters.AddWithValue("@uid", userId);
            using var pastReader = cmdPast.ExecuteReader();
            while (pastReader.Read())
            {
                user.PastBookings.Add(new BookingDto
                {
                    Date = Convert.ToDateTime(pastReader["BookingDate"]).ToString("dd/MM/yyyy"),
                    TimeFrom = pastReader["SlotTimeFrom"].ToString(),
                    TimeTo = pastReader["SlotTimeTo"].ToString()
                });
            }
            pastReader.Close();

            return Ok(user);
        }
    }

    public class FullUserDetailDto
    {
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public int TotalBookings { get; set; }
        public string LastBooking { get; set; }
        public string TotalHours { get; set; }
        public List<BookingDto> UpcomingBookings { get; set; }
        public List<BookingDto> PastBookings { get; set; }
    }

    public class BookingDto
    {
        public string Date { get; set; }
        public string TimeFrom { get; set; }
        public string TimeTo { get; set; }
    }
}
