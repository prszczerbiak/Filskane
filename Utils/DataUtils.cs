using BitMiracle.LibTiff.Classic;
using OSGeo.GDAL;

namespace WebApplication1.Utils
{
    public class DataUtils
    {
        public static byte[] ConvertTiffFloat32ToUInt16(byte[] inputBytes)
        {
            using var inputStream = new MemoryStream(inputBytes);
            using var input = Tiff.ClientOpen("MemTIFF", "r", inputStream, new TiffStream());

            if (input == null)
                throw new InvalidOperationException("Nie można otworzyć TIFF z pamięci.");

            int width = input.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            int height = input.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            int samplesPerPixel = input.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();

            // Odczytaj dane float32
            float[] floatData = new float[width * height * samplesPerPixel];
            for (int row = 0; row < height; row++)
            {
                byte[] buffer = new byte[input.ScanlineSize()];
                input.ReadScanline(buffer, row);
                Buffer.BlockCopy(buffer, 0, floatData, row * width * samplesPerPixel * sizeof(float), buffer.Length);
            }

            // PROSTA KONWERSJA: float * 65535 → uint16 (jak w Pythonie)
            ushort[] uint16Data = new ushort[floatData.Length];
            for (int i = 0; i < floatData.Length; i++)
            {
                uint16Data[i] = (ushort)(floatData[i] * 65535.0f);
            }

            // Zapisz jako uint16 TIFF
            using var outputStream = new MemoryStream();
            using (var output = Tiff.ClientOpen("MemTIFFOut", "w", outputStream, new TiffStream()))
            {
                output.SetField(TiffTag.IMAGEWIDTH, width);
                output.SetField(TiffTag.IMAGELENGTH, height);
                output.SetField(TiffTag.SAMPLESPERPIXEL, samplesPerPixel);
                output.SetField(TiffTag.BITSPERSAMPLE, 16);
                output.SetField(TiffTag.SAMPLEFORMAT, SampleFormat.UINT);
                output.SetField(TiffTag.PHOTOMETRIC, samplesPerPixel == 3 ? Photometric.RGB : Photometric.MINISBLACK);
                output.SetField(TiffTag.PLANARCONFIG, PlanarConfig.CONTIG);
                output.SetField(TiffTag.ROWSPERSTRIP, height);
                output.SetField(TiffTag.COMPRESSION, Compression.NONE);

                byte[] rowBuffer = new byte[width * samplesPerPixel * sizeof(ushort)];
                for (int row = 0; row < height; row++)
                {
                    Buffer.BlockCopy(uint16Data, row * width * samplesPerPixel * sizeof(ushort),
                                   rowBuffer, 0, rowBuffer.Length);
                    output.WriteScanline(rowBuffer, row);
                }

                output.WriteDirectory();
            }

            return outputStream.ToArray();
        }

    }
}
