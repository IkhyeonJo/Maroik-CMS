using System;
using System.Linq;

namespace MyLaboratory.WebSite.Common
{
    public class GUIDToken
    {
        public static string Generate() 
        {
            #region Unique token
            //string token = Convert.ToBase64String(Guid.NewGuid().ToByteArray()); 
            #endregion

            #region Basic example of creating a unique token containing a time stamp: https://stackoverflow.com/questions/14643735/how-to-generate-a-unique-token-which-expires-after-24-hours
            byte[] time = BitConverter.GetBytes(DateTime.UtcNow.ToBinary());
            byte[] key = Guid.NewGuid().ToByteArray();
            string token = Convert.ToBase64String(time.Concat(key).ToArray());
            #endregion
            return token;
        }

        public static bool IsTokenAlive(string token)
        {
            byte[] data = Convert.FromBase64String(token);
            DateTime when = DateTime.FromBinary(BitConverter.ToInt64(data, 0));
            if (when < DateTime.UtcNow.AddHours(-24)) // how to generate a unique token which expires after 24 hours?
            {
                return false; // token dead
            }
            else
            {
                return true; // token alive 
            }
        }
    }
}