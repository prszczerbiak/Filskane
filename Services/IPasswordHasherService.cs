namespace WebApplication1.Services
{
    /// <summary>
    /// Interfejs definiujący kontrakt dla serwisów haszowania haseł.
    /// </summary>
    public interface IPasswordHasherService
    {
        /// <summary>
        /// Generuje hash dla podanego hasła.
        /// </summary>
        /// <param name="password">Hasło w formie jawnej.</param>
        /// <returns>Wygenerowany hash.</returns>
        string Hash(string password);

        /// <summary>
        /// Weryfikuje zgodność podanego hasła z przechowywanym hashem.
        /// </summary>
        /// <param name="password">Hasło do sprawdzenia.</param>
        /// <param name="hash">Hash, z którym porównujemy hasło.</param>
        /// <returns>True, jeśli hasło jest poprawne.</returns>
        bool Verify(string password, string hash);
    }
}