using System.ComponentModel.DataAnnotations;

namespace Maroik.WebSite.Models.ViewModels.Management
{
    public class AccountInputViewModel
    {
        [Required(ErrorMessage = "Please enter Email")]
        [Display(Name = "Email")]
        public string Email { get; set; }
        [Required(ErrorMessage = "Please enter Password")]
        [Display(Name = "Password")]
        public string Password { get; set; }
        [Required(ErrorMessage = "Please enter Nickname")]
        [Display(Name = "Nickname")]
        public string Nickname { get; set; }
        [Required(ErrorMessage = "Please enter AvatarImagePath")]
        [Display(Name = "AvatarImagePath")]
        public string AvatarImagePath { get; set; }
        [Required(ErrorMessage = "Please enter Role")]
        [Display(Name = "Role")]
        public string Role { get; set; }
        [Required(ErrorMessage = "Please select time zone")]
        [Display(Name = "Time zone")]
        public string TimeZoneIanaId { get; set; }
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
        [Required(ErrorMessage = "Please enter Message")]
        [Display(Name = "Message")]
        public string Message { get; set; }
        [Required(ErrorMessage = "Please enter Deleted")]
        [Display(Name = "Deleted")]
        public bool Deleted { get; set; }
    }
}