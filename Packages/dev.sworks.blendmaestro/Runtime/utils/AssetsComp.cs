using System.IO;
using System.IO.Compression;
using System.Text;

namespace dev.sworks.blendmaestro.runtime.utils
{
    public static class AssetsComp
    {
        private static readonly Encoding ENCODING = Encoding.UTF8;

        public static byte[] Compress( string rawString )
        {
            var bytes = ENCODING.GetBytes( rawString );

            using var memoryStream = new MemoryStream();

            using ( var gZipStream = new GZipStream( memoryStream, CompressionMode.Compress ) )
            {
                gZipStream.Write( bytes, 0, bytes.Length );
            }

            return memoryStream.ToArray();
        }

        public static string Decompress( byte[] bytes )
        {
            using var memoryStream = new MemoryStream( bytes );
            using var gZipStream   = new GZipStream( memoryStream, CompressionMode.Decompress );
            using var streamReader = new StreamReader( gZipStream, ENCODING );
            return streamReader.ReadToEnd();
        }
    }
}