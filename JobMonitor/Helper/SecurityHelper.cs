using Microsoft.Azure.WebJobs.Host;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace JobMonitor.Helper
{
    public class SecurityHelper
    {
        public static bool VerifyWebHookRequestSignature(byte[] data, string actualValue, byte[] verificationKey)
        {
            using (var hasher = new HMACSHA256(verificationKey))
            {
                byte[] sha256 = hasher.ComputeHash(data);
                string expectedValue = string.Format(CultureInfo.InvariantCulture, Constants.SignatureHeaderValueTemplate, ToHex(sha256));

                return (0 == String.Compare(actualValue, expectedValue, System.StringComparison.Ordinal));
            }
        }

        internal static bool VerifyHeaders(HttpRequestMessage req, NotificationMessage msg, TraceWriter log)
        {
            bool headersVerified = false;

            try
            {
                IEnumerable<string> values = null;
                if (req.Headers.TryGetValues("ms-mediaservices-accountid", out values))
                {
                    string accountIdHeader = values.FirstOrDefault();
                    string accountIdFromMessage = msg.Properties["AccountId"];

                    if (0 == string.Compare(accountIdHeader, accountIdFromMessage, StringComparison.OrdinalIgnoreCase))
                    {
                        headersVerified = true;
                    }
                    else
                    {
                        log.Info($"accountIdHeader={accountIdHeader} does not match accountIdFromMessage={accountIdFromMessage}");
                    }
                }
                else
                {
                    log.Info($"Header ms-mediaservices-accountid not found.");
                }
            }
            catch (Exception e)
            {
                log.Info($"VerifyHeaders hit exception {e}");
                headersVerified = false;
            }

            return headersVerified;
        }

        public static readonly char[] HexLookup = new char[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

        /// <summary>
        /// Converts a <see cref="T:byte[]"/> to a hex-encoded string.
        /// </summary>
        public static string ToHex(byte[] data)
        {
            if (data == null)
            {
                return string.Empty;
            }

            char[] content = new char[data.Length * 2];
            int output = 0;
            byte d;
            for (int input = 0; input < data.Length; input++)
            {
                d = data[input];
                content[output++] = HexLookup[d / 0x10];
                content[output++] = HexLookup[d % 0x10];
            }
            return new string(content);
        }

    }
}
