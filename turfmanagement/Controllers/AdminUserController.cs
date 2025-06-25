using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;
using System;
using System.Collections.Generic;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AdminUserController : ControllerBase
    {
        private readonly DatabaseConnection _db;

        public AdminUserController(DatabaseConnection db)
        {
            _db = db;
        }

        [HttpGet("details")]
        public IActionResult GetUserDetails()
        {
            var result = new List<UserDetailDto>();

            using var conn = _db.GetConnection();
            conn.Open();

            string query = @"
                SELECT 
                    u.UserId,
                    u.Name,
                    u.PhoneNumber,
                    u.LastBookingDate,
                    (
                        SELECT MIN(b.BookingDate)
                        FROM Bookings b
                        WHERE b.UserId = u.UserId AND b.BookingDate > CURRENT_DATE
                    ) AS UpcomingBooking,
                    (
                        SELECT COUNT(*) 
                        FROM Bookings b 
                        WHERE b.UserId = u.UserId
                    ) AS TotalBookings
                FROM Users u;
            ";

            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            int index = 1;
            while (reader.Read())
            {
                string lastBooking = reader["LastBookingDate"] is DBNull
                    ? "-"
                    : Convert.ToDateTime(reader["LastBookingDate"]).ToString("dd/MM/yyyy");

                string upcoming = reader["UpcomingBooking"] is DBNull
                    ? "-"
                    : Convert.ToDateTime(reader["UpcomingBooking"]).ToString("dd/MM/yyyy");

                int totalBookings = reader["TotalBookings"] is DBNull
                    ? 0
                    : Convert.ToInt32(reader["TotalBookings"]);

                result.Add(new UserDetailDto
                {
                    No = index++,
                    Name = reader["Name"].ToString(),
                    PhoneNumber = reader["PhoneNumber"].ToString(),
                    LastBooking = lastBooking,
                    UpcomingBooking = upcoming,
                    TotalBookings = totalBookings
                });
            }

            return Ok(result);
        }
    }

    public class UserDetailDto
    {
        public int No { get; set; }  // serial index
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string LastBooking { get; set; }
        public string UpcomingBooking { get; set; }
        public int TotalBookings { get; set; }
    }
}
