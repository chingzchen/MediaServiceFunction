using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JobMonitor.Helper
{
    public class Constants
    {
        public const string ContentKeyIdPrefix = "nb:kid:UUID:";
        public const string MediaServiceAccout = "AMSAccount";
        public const string MediaServiceKey = "AMSKey";
        public const string MediaBlobName = "MediaServicesStorageAccountName";
        public const string MediaBlobKey = "MediaServicesStorageAccountKey";
        public const string WebHookEndpoint = "WebHookEndpoint";
        public const string DeleteWebHookEndpoint = "DeleteWebHookEndpoint";
        public const string SigningKey = "SigningKey";
        public const string SignatureHeaderValueTemplate = "sha256 ={0}";
        public const string DuplicateActionEndpoint = "DuplicateActionEndpoint";
        public const string strtemplate = "{{name:\"{0}\"}}";
    }
}
