﻿using System.IO;
using System.Runtime.InteropServices;

namespace Rotativa.AspNetCore
{
    public class WkhtmltopdfDriver : WkhtmlDriver
    {
        /// <summary>
        /// wkhtmltopdf only has a .exe extension in Windows.
        /// </summary>
        private static readonly string wkhtmlExe =
            RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "wkhtmltopdf.exe" : "wkhtmltopdf";

        /// <summary>
        /// Converts given HTML string to PDF.
        /// </summary>
        /// <param name="wkhtmltopdfPath">Path to wkthmltopdf.</param>
        /// <param name="switches">Switches that will be passed to wkhtmltopdf binary.</param>
        /// <param name="html">String containing HTML code that should be converted to PDF.</param>
        /// <returns>PDF as byte array.</returns>
        public static byte[] ConvertHtml(string wkhtmltopdfPath, string switches, string html)
        {
            return Convert(wkhtmltopdfPath, switches, html, wkhtmlExe);
        }

        /// <summary>
        /// Converts given URL to PDF.
        /// </summary>
        /// <param name="wkhtmltopdfPath">Path to wkthmltopdf.</param>
        /// <param name="switches">Switches that will be passed to wkhtmltopdf binary.</param>
        /// <returns>PDF as byte array.</returns>
        public static byte[] Convert(string wkhtmltopdfPath, string switches)
        {
            return Convert(wkhtmltopdfPath, switches, null, wkhtmlExe);
        }
    }
}

