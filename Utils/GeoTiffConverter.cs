

using OSGeo.GDAL;
using OSGeo.OGR;
using OSGeo.OSR;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace WebApplication1.Utils
{
    public class GeoTiffConverter
    {
        [DllImport("gdal.dll", CallingConvention = CallingConvention.Cdecl)]
        public static extern IntPtr VSIGetMemFileBuffer(string filename, out long size, int bAllowShare);
        public static byte[] ConvertTiffFloat32ToUInt16(byte[] tiffBytes)
        {
            try
            {
                Gdal.AllRegister();

                // Wirtualne pliki w pamięci
                string inMem = "/vsimem/in_float32.tif";
                string outMem = "/vsimem/out_uint16.tif";
                Gdal.FileFromMemBuffer(inMem, tiffBytes);

                using (Dataset srcDs = Gdal.Open(inMem, Access.GA_ReadOnly))
                {
                    if (srcDs == null)
                        throw new Exception("Nie udało się otworzyć datasetu wejściowego.");

                    // Opcje dla gdal_translate
                    string[] translateOptions = new[] { "-ot", "UInt16", "-scale", "0", "1", "0", "10000" };

                    using (Dataset dstDs = Gdal.wrapper_GDALTranslate(outMem, srcDs, new GDALTranslateOptions(translateOptions), null, null))
                    {
                        if (dstDs == null)
                            throw new Exception("Błąd przy konwersji (wrapper_GDALTranslate).");

                        // Pobranie danych TIFF jako byte[]
                        long size;
                        IntPtr ptr = VSIGetMemFileBuffer(outMem, out size, 0);
                        if (ptr == IntPtr.Zero)
                            throw new Exception("Nie udało się pobrać bufora z pamięci.");

                        byte[] tiffBytesOut = new byte[size];
                        Marshal.Copy(ptr, tiffBytesOut, 0, (int)size);

                        
                        return tiffBytesOut;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Błąd: {ex.Message}");
                return null;
            }
            finally
            {
                // Usunięcie wirtualnych plików z pamięci
                Gdal.Unlink("/vsimem/in_float32.tif");
                Gdal.Unlink("/vsimem/out_uint16.tif");
            }
        }
    }

}
