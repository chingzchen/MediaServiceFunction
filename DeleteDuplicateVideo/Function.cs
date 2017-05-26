using System.Linq;
using System.Net;
using System.Net.Http;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using System;
using Microsoft.WindowsAzure.Storage.Auth;
using System.Collections.Generic;
using DeleteDuplicateVideo.Helper;

namespace DeleteDuplicateVideo
{
    public static class Function
    {
        private static MediaServicesCredentials cachedcredential = null;

        //Media Service Account Info
        private static readonly string _mediaServicesAccountName = GetEnvironmentVariable(Constants.MediaServiceAccout);
        private static readonly string _mediaServicesAccountKey = GetEnvironmentVariable(Constants.MediaServiceKey);

        //Storage Account Info
        static string _storageAccountName = GetEnvironmentVariable(Constants.MediaBlobName);
        static string _storageAccountKey = GetEnvironmentVariable(Constants.MediaBlobKey);

        //Security
        private static string _webHookEndpoint = GetEnvironmentVariable("WebHookEndpoint");
        private static string _signingKey = GetEnvironmentVariable("SigningKey");

        //MEdia Service Contect
        private static CloudMediaContext _context = null;

        [FunctionName("HttpTriggerWithParametersCSharp")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post")]HttpRequestMessage req, TraceWriter log)
        {//, Route = "HttpTriggerCSharp/name/{filename}"
            log.Info("C# HTTP trigger function processed a request.");
            string filename = string.Empty;
            string responsestr = string.Empty; 
            try
            {
                // Create and cache the Media Services credentials in a static class variable.
                cachedcredential = new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey);

                // Used the chached credentials to create CloudMediaContext.
                _context = new CloudMediaContext(cachedcredential);
         
                StorageCredentials mediaServicesStorageCredentials =
                    new StorageCredentials(_storageAccountName, _storageAccountKey);

                //Get Content Body
                dynamic data = await req.Content.ReadAsAsync<object>();
                filename = data?.name;

                if (filename == null)
                {
                    return req.CreateResponse(HttpStatusCode.BadRequest, "Please pass a name in the request body");
                }
                
                //Query Asset by filename
                List<IAsset> resultAssets = Task.Run(async() => await DeleteDuplicateVideo.Helper.MediaServiceHelper.GetAssetbyName(_context, filename, log)).Result;

                //Remove all duplicated assets expect for the last modified one
                //Future improve to read the keep asset list from the asset manage db
                await DeleteDuplicateVideo.Helper.MediaServiceHelper.DeleteAssetAsync(_context, resultAssets, log);

                responsestr = string.Format("Find {0} Assets to be deleted!", resultAssets.Count);
            }
            catch (Exception ex)
            {
                log.Error("ERROR: failed.");
                log.Info($"StackTrace : {ex.StackTrace}");
                throw ex;
            }
            responsestr = responsestr + string.Format("filename {0} process succeed!", filename);

            return req.CreateResponse(HttpStatusCode.OK, responsestr);
        }     

        /// <summary>
        /// Get Environmnry Variable
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public static string GetEnvironmentVariable(string name)
        {
            return name + ": " +
                System.Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        }

    }
}