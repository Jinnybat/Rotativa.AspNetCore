using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Rotativa.AspNetCore.Options;
using System.Threading.Tasks;
#if NET5_0_OR_GREATER
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.AspNetCore.Http;
#elif NETCOREAPP3_1
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
#elif NETSTANDARD2_0
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
#endif

namespace Rotativa.AspNetCore
{
    public abstract class AsResultBase : ViewResult //IActionResult
    {
        protected AsResultBase()
        {
            this.WkhtmlPath = string.Empty;
            this.FormsAuthenticationCookieName = ".ASPXAUTH";
        }

        /// <summary>
        /// This will be send to the browser as a name of the generated PDF file.
        /// </summary>
        public string FileName { get; set; }

        /// <summary>
        /// Path to wkhtmltopdf\wkhtmltoimage binary.
        /// </summary>
        public string WkhtmlPath { get; set; }

        /// <summary>
        /// Custom name of authentication cookie used by forms authentication.
        /// </summary>
        [Obsolete("Use FormsAuthenticationCookieName instead of CookieName.")]
        public string CookieName
        {
            get { return this.FormsAuthenticationCookieName; }
            set { this.FormsAuthenticationCookieName = value; }
        }

        /// <summary>
        /// Custom name of authentication cookie used by forms authentication.
        /// </summary>
        public string FormsAuthenticationCookieName { get; set; }

        /// <summary>
        /// Sets custom headers.
        /// </summary>
        [OptionFlag("--custom-header")]
        public Dictionary<string, string> CustomHeaders { get; set; }

        /// <summary>
        /// Sets cookies.
        /// </summary>
        [OptionFlag("--cookie")]
        public Dictionary<string, string> Cookies { get; set; }

        /// <summary>
        /// Sets post values.
        /// </summary>
        [OptionFlag("--post")]
        public Dictionary<string, string> Post { get; set; }

        /// <summary>
        /// Indicates whether the page can run JavaScript.
        /// </summary>
        [OptionFlag("-n")]
        public bool IsJavaScriptDisabled { get; set; }

        /// <summary>
        /// Minimum font size.
        /// </summary>
        [OptionFlag("--minimum-font-size")]
        public int? MinimumFontSize { get; set; }

        /// <summary>
        /// Sets proxy server.
        /// </summary>
        [OptionFlag("-p")]
        public string Proxy { get; set; }

        /// <summary>
        /// HTTP Authentication username.
        /// </summary>
        [OptionFlag("--username")]
        public string UserName { get; set; }

        /// <summary>
        /// HTTP Authentication password.
        /// </summary>
        [OptionFlag("--password")]
        public string Password { get; set; }

        /// <summary>
        /// Use this if you need another switches that are not currently supported by Rotativa.
        /// </summary>
        [OptionFlag("")]
        public string CustomSwitches { get; set; }

        [Obsolete(@"Use BuildFile(this.ControllerContext) method instead and use the resulting binary data to do what needed.")]
        public string SaveOnServerPath { get; set; }

        public ContentDisposition ContentDisposition { get; set; }

        protected abstract string GetUrl(Microsoft.AspNetCore.Mvc.ActionContext context);

        /// <summary>
        /// Returns properties with OptionFlag attribute as one line that can be passed to wkhtmltopdf binary.
        /// </summary>
        /// <returns>Command line parameter that can be directly passed to wkhtmltopdf binary.</returns>
        protected virtual string GetConvertOptions()
        {
            var result = new StringBuilder();

            var fields = this.GetType().GetProperties();
            foreach (var fi in fields)
            {
                var of = fi.GetCustomAttributes(typeof(OptionFlag), true).FirstOrDefault() as OptionFlag;
                if (of == null)
                    continue;

                object value = fi.GetValue(this, null);
                if (value == null)
                    continue;

                if (fi.PropertyType == typeof(Dictionary<string, string>))
                {
                    var dictionary = (Dictionary<string, string>)value;
                    foreach (var d in dictionary)
                    {
                        result.AppendFormat(" {0} {1} {2}", of.Name, d.Key, d.Value);
                    }
                }
                else if (fi.PropertyType == typeof(bool))
                {
                    if ((bool)value)
                        result.AppendFormat(CultureInfo.InvariantCulture, " {0}", of.Name);
                }
                else
                {
                    result.AppendFormat(CultureInfo.InvariantCulture, " {0} {1}", of.Name, value);
                }
            }

            return result.ToString().Trim();
        }

        private string GetWkParams(ActionContext context)
        {
            var switches = string.Empty;

            string authenticationCookie = null;
            if (context.HttpContext.Request.Cookies != null && context.HttpContext.Request.Cookies.Keys.Contains(FormsAuthenticationCookieName))
            {
                authenticationCookie = context.HttpContext.Request.Cookies[FormsAuthenticationCookieName];
            }
            if (authenticationCookie != null)
            {
                //var authCookieValue = authenticationCookie.Value;
                switches += " --cookie " + this.FormsAuthenticationCookieName + " " + authenticationCookie;
            }

            switches += " " + this.GetConvertOptions();

            var url = this.GetUrl(context);
            switches += " " + url;

            return switches;
        }

        protected virtual async Task<byte[]> CallTheDriver(ActionContext context)
        {
            var switches = this.GetWkParams(context);
            var fileContent = this.WkhtmlConvert(switches);
            return fileContent;
        }
        //protected abstract Task<byte[]> CallTheDriver(ActionContext context);

        protected abstract byte[] WkhtmlConvert(string switches);

        public async Task<byte[]> BuildFile(ActionContext context)
        {
            if (context == null)
                throw new ArgumentNullException("context");

            //if (this.WkhtmlPath == string.Empty)
            //    this.WkhtmlPath = context.HttpContext.Server.MapPath("~/Rotativa");

            this.WkhtmlPath = RotativaConfiguration.RotativaPath;

            var fileContent = await CallTheDriver(context);

            if (string.IsNullOrEmpty(this.SaveOnServerPath) == false)
            {
                File.WriteAllBytes(this.SaveOnServerPath, fileContent);
            }

            return fileContent;
        }

        public async override Task ExecuteResultAsync(ActionContext context)
        {
            var fileContent = await this.BuildFile(context);

            var response = this.PrepareResponse(context.HttpContext.Response);

            await response.Body.WriteAsync(fileContent, 0, fileContent.Length);
        }

        private static string SanitizeFileName(string name)
        {
            string invalidChars = Regex.Escape(new string(Path.GetInvalidPathChars()) + new string(Path.GetInvalidFileNameChars()));
            string invalidCharsPattern = string.Format(@"[{0}]+", invalidChars);

            string result = Regex.Replace(name, invalidCharsPattern, "_");
            return result;
        }

        protected HttpResponse PrepareResponse(HttpResponse response)
        {
            response.ContentType = this.GetContentType();

            if (!String.IsNullOrEmpty(this.FileName))
            {
                var contentDisposition = this.ContentDisposition == ContentDisposition.Attachment
                    ? "attachment"
                    : "inline";

                response.Headers.Add("Content-Disposition", string.Format("{0}; filename=\"{1}\"", contentDisposition, SanitizeFileName(this.FileName)));
            }
            //response.Headers.Add("Content-Type", this.GetContentType());

            return response;
        }

        protected abstract string GetContentType();
    }
}