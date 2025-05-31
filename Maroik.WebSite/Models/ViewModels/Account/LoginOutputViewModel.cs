﻿using System.ComponentModel.DataAnnotations;

namespace Maroik.WebSite.Models.ViewModels.Account
{
    public class LoginOutputViewModel
    {
        [Required(ErrorMessage = "Email is required")]
        [DataType(DataType.EmailAddress)]
        [EmailAddress(ErrorMessage = "Invalid Email Address")]
        public string Email { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [RegularExpression("^((?=.*?[A-Z])(?=.*?[a-z])(?=.*?[0-9])|(?=.*?[A-Z])(?=.*?[a-z])(?=.*?[^a-zA-Z0-9])|(?=.*?[A-Z])(?=.*?[0-9])(?=.*?[^a-zA-Z0-9])|(?=.*?[a-z])(?=.*?[0-9])(?=.*?[^a-zA-Z0-9])).{8,}$", ErrorMessage = "Password must be at least 8 characters and contain at 3 of 4 of the following: upper case (A-Z), lower case (a-z), number (0-9) and special character (e.g. !@#$%^&*)")]
        public string Password { get; set; }


        [Required(ErrorMessage = "Confirm Password is required")]
        [StringLength(100, ErrorMessage = "The {0} must be at least {2} characters long.", MinimumLength = 8)]
        [DataType(DataType.Password)]
        [Compare(nameof(Password), ErrorMessage = "The password do not match")]
        public string ConfirmPassword { get; set; }

        [Required(ErrorMessage = "Nickname is required")]
        [StringLength(255, ErrorMessage = "Must be between 1 and 255 characters", MinimumLength = 1)]
        public string Nickname { get; set; }

        public bool AgreedServiceTerms { get; set; }

        public string TimeZoneIanaId { get; set; }
    }
}
