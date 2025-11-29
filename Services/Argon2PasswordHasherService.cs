using Isopoh.Cryptography.Argon2;

namespace WebApplication1.Services
{
    public class Argon2PasswordHasherService: IPasswordHasherService
    {
        public string Hash(string password)
        {
            //var config = new Argon2Config
            //{
            //    Type = Argon2Type.HybridAddressing, // Argon2id
            //    Version = Argon2Version.Nineteen,
            //    TimeCost = 4,
            //    MemoryCost = 1024 * 64, // 64MB
            //    Lanes = 4,
            //    Threads = 4,
            //    Password = System.Text.Encoding.UTF8.GetBytes(password),
            //    Salt = null
            //};

            return Argon2.Hash(password);
        }

        public bool Verify(string password, string hash)
        {
            return Argon2.Verify(hash, password);
        }
    }
}
