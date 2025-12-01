using Isopoh.Cryptography.Argon2;

namespace WebApplication1.Services
{
    public class Argon2PasswordHasherService: IPasswordHasherService
    {
        public string Hash(string password)
        {
            return Argon2.Hash(password);
        }

        public bool Verify(string password, string hash)
        {
            return Argon2.Verify(hash, password);
        }
    }
}
