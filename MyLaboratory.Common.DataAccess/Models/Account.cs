using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading.Tasks;

namespace MyLaboratory.Common.DataAccess.Models
{
    public partial class Account
    {
        public Account()
        {
            Assets = new HashSet<Asset>();
        }

        public string Email { get; set; }
        public string HashedPassword { get; set; }
        public string FullName { get; set; }
        public string AvatarImagePath { get; set; }
        public string Role { get; set; }
        public bool Locked { get; set; }
        public int LoginAttempt { get; set; }
        public bool EmailConfirmed { get; set; }
        public bool AgreedServiceTerms { get; set; }
        public string RegistrationToken { get; set; }
        public string ResetPasswordToken { get; set; }
        public DateTime Created { get; set; }
        public DateTime Updated { get; set; }
        public string Message { get; set; }
        public bool Deleted { get; set; }

        public virtual ICollection<Asset> Assets { get; set; }
    }
}
