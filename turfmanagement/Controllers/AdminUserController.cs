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
                    u.Name,
                    u.PhoneNumber,
                    u.LastBookingDate,
                    (
                        SELECT MIN(b.BookingDate)
                        FROM Bookings b
                        WHERE b.UserId = u.UserId AND b.BookingDate > CURRENT_DATE
                    ) AS UpcomingBooking
                FROM Users u;
            ";

            using var cmd = new NpgsqlCommand(query, conn);
            using var reader = cmd.ExecuteReader();

            while (reader.Read())
            {
                string lastBooking = reader["LastBookingDate"] is DBNull
                    ? "N/A"
                    : Convert.ToDateTime(reader["LastBookingDate"]).ToString("dd/MM/yyyy");

                string upcoming = reader["UpcomingBooking"] is DBNull
                    ? "Not yet"
                    : Convert.ToDateTime(reader["UpcomingBooking"]).ToString("dd/MM/yyyy");

                result.Add(new UserDetailDto
                {
                    Name = reader["Name"].ToString(),
                    PhoneNumber = reader["PhoneNumber"].ToString(),
                    LastBooking = lastBooking,
                    UpcomingBooking = upcoming
                });
            }

            return Ok(result);
        }
    }

    public class UserDetailDto
    {
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string LastBooking { get; set; }
        public string UpcomingBooking { get; set; }
    }
}
