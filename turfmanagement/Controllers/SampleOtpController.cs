using Microsoft.AspNetCore.Mvc;
using Npgsql;
using turfmanagement.Connection;
using System;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace turfmanagement.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SampleOtpController : Controller
    {
        private static readonly ConcurrentDictionary<string, (string Otp, DateTime Expiry)> _otpStore = new();

        // POST /api/otp/send
        [HttpPost("send")]
        
        public IActionResult SendOtp([FromBody] PhoneDto dto)
        {
            var phone = dto.PhoneNumber;

             var otp = new Random().Next(100000, 999999).ToString();
            var expiry = DateTime.UtcNow.AddMinutes(5); // OTP valid for 5 minutes

            _otpStore[phone] = (otp, expiry);

           Console.WriteLine($"OTP for {phone} is {otp}");

            return Ok(new { message = "OTP sent successfully" });
        }

        [HttpPost("verify")]
        public IActionResult VerifyOtp([FromBody] OtpVerifyDto dto)
        {
            if (_otpStore.TryGetValue(dto.PhoneNumber, out var entry))
            {
                if (entry.Expiry < DateTime.UtcNow)
                    return BadRequest(new { message = "OTP expired" });

                if (entry.Otp == dto.Otp)
                {
                    _otpStore.TryRemove(dto.PhoneNumber, out _); // clear OTP
                    return Ok(new { message = "OTP verified successfully" });
                }

                return BadRequest(new { message = "Invalid OTP" });
            }

            return BadRequest(new { message = "No OTP found for this number" });
        }
    }

    public class PhoneDto
    {
        public string PhoneNumber { get; set; }
    }

    public class OtpVerifyDto
    {
        public string PhoneNumber { get; set; }
        public string Otp { get; set; }
    }
}
