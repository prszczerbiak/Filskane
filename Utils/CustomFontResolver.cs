using PdfSharp.Fonts;
using System.Reflection;

namespace Filskane.Utils
{
    public class CustomFontResolver: IFontResolver
    {
        public byte[]? GetFont(string faceName)
        {
            // Tutaj ładujemy fizyczne bajty czcionki z zasobów
            if (faceName.Equals("Arial-Bold", StringComparison.OrdinalIgnoreCase))
                return LoadFontData("Filskane.Services.Fonts.ARIALBD.TTF");

            if (faceName.Equals("Arial-Regular", StringComparison.OrdinalIgnoreCase))
                return LoadFontData("Filskane.Services.Fonts.ARIAL.TTF");

            return null;
        }

        public FontResolverInfo? ResolveTypeface(string familyName, bool isBold, bool isItalic)
        {
            // Kiedy PDFsharp szuka "Arial", my mówimy mu, jakiej nazwy (faceName) ma użyć
            if (familyName.Equals("Arial", StringComparison.CurrentCultureIgnoreCase))
            {
                if (isBold) return new FontResolverInfo("Arial-Bold");

                return new FontResolverInfo("Arial-Regular");
            }

            // Zwróć null dla nieznanych czcionek (spowoduje to błąd, jeśli użyjesz czegoś innego niż Arial)
            return null;
        }

        private byte[] LoadFontData(string resourceName)
        {
            var assembly = Assembly.GetExecutingAssembly();

            // Ważne: nazwa zasobu to "NamespaceProjektu.NazwaFolderu.NazwaPliku.Rozszerzenie"
            using Stream? stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null)
                throw new ArgumentException($"Nie znaleziono zasobu czcionki: {resourceName}. Upewnij się, że plik ma ustawione Build Action na 'Embedded Resource'.");

            using var ms = new MemoryStream();
            stream.CopyTo(ms);
            return ms.ToArray();
        }
    }
}
