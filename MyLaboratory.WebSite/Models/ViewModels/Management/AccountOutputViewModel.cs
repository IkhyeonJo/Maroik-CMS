using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;

namespace MyLaboratory.WebSite.Models.ViewModels.Management
{
    public class AccountOutputViewModel
    {
        [Required(ErrorMessage = "Please enter Email")]
        [Display(Name = "Email")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Please enter HashedPassword")]
        [Display(Name = "HashedPassword")]
        public string HashedPassword { get; set; }
        [Required(ErrorMessage = "Please enter FullName")]
        [Display(Name = "FullName")]
        public string FullName { get; set; }
        [Required(ErrorMessage = "Please enter AvatarImagePath")]
        [Display(Name = "AvatarImagePath")]
        public string AvatarImagePath { get; set; }
        [Required(ErrorMessage = "Please enter Role")]
        [Display(Name = "Role")]
        public string Role { get; set; }
        [Required(ErrorMessage = "Please enter Locked")]
        [Display(Name = "Locked")]
        public bool Locked { get; set; }
        [Required(ErrorMessage = "Please enter LoginAttempt")]
        [Display(Name = "LoginAttempt")]
        public int LoginAttempt { get; set; }
        [Required(ErrorMessage = "Please enter EmailConfirmed")]
        [Display(Name = "EmailConfirmed")]
        public bool EmailConfirmed { get; set; }
        [Required(ErrorMessage = "Please enter AgreedServiceTerms")]
        [Display(Name = "AgreedServiceTerms")]
        public bool AgreedServiceTerms { get; set; }
        [Required(ErrorMessage = "Please enter RegistrationToken")]
        [Display(Name = "RegistrationToken")]
        public string RegistrationToken { get; set; }
        [Required(ErrorMessage = "Please enter ResetPasswordToken")]
        [Display(Name = "ResetPasswordToken")]
        public string ResetPasswordToken { get; set; }
        [Required(ErrorMessage = "Please enter Created")]
        [Display(Name = "Created")]
        public DateTime Created { get; set; }
        [Required(ErrorMessage = "Please enter Updated")]
        [Display(Name = "Updated")]
        public DateTime Updated { get; set; }
        [Required(ErrorMessage = "Please enter Message")]
        [Display(Name = "Message")]
        public string Message { get; set; }
        [Required(ErrorMessage = "Please enter Deleted")]
        [Display(Name = "Deleted")]
        public bool Deleted { get; set; }
    }
}