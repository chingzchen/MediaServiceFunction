using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;
using System.Collections.Generic;
using System;
using JobMonitor.Helper;
using Microsoft.WindowsAzure.MediaServices.Client;
using System.Text;
using Newtonsoft.Json;

namespace JobMonitor
{
    public static class JobMonitor
    {
        
        static string _webHookEndpoint =  Environment.GetEnvironmentVariable(Constants.WebHookEndpoint);
        static string _duplicateActionEndpoint= Environment.GetEnvironmentVariable(Constants.DuplicateActionEndpoint);
        static string _signingKey = Environment.GetEnvironmentVariable(Constants.SigningKey);
        static string _mediaServicesAccountName = Environment.GetEnvironmentVariable(Constants.MediaServiceAccout);
        static string _mediaServicesAccountKey = Environment.GetEnvironmentVariable(Constants.MediaServiceKey);

        static CloudMediaContext _context = null;

        [FunctionName("HttpTriggerCSharp")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("Monitor Job function processed a request.");

            //// parse query parameter
            //string name = req.GetQueryNameValuePairs()
            //    .FirstOrDefault(q => string.Compare(q.Key, "name", true) == 0)
            //    .Value;

            //// Get request body
            //dynamic data = await req.Content.ReadAsAsync<object>();

            //// Set name to query string or body data
            //name = name ?? data?.name;           

            Task<byte[]> taskForRequestBody = req.Content.ReadAsByteArrayAsync();
            byte[] requestBody = await taskForRequestBody;

            string jsonContent = await req.Content.ReadAsStringAsync();
            log.Info($"Request Body = {jsonContent}");

            IEnumerable<string> values = null;
            if (req.Headers.TryGetValues("ms-signature", out values))
            {
                byte[] signingKey = Convert.FromBase64String(_signingKey);
                string signatureFromHeader = values.FirstOrDefault();

                if (SecurityHelper.VerifyWebHookRequestSignature(requestBody, signatureFromHeader, signingKey))
                {
                    string requestMessageContents = Encoding.UTF8.GetString(requestBody);

                    NotificationMessage msg = JsonConvert.DeserializeObject<NotificationMessage>(requestMessageContents);

                    if (SecurityHelper.VerifyHeaders(req, msg, log))
                    {
                        string newJobStateStr = (string)msg.Properties.Where(j => j.Key == "NewState").FirstOrDefault().Value;
                        if (newJobStateStr == "Finished")
                        {
                            _context = new CloudMediaContext(new MediaServicesCredentials(
                            _mediaServicesAccountName,
                            _mediaServicesAccountKey));

                            if (_context != null)
                            {
                                string urlForClientStreaming = MediaServiceHelper.PublishAndBuildStreamingURLs(_context, msg.Properties["JobId"]);
                                log.Info($"URL to the manifest for client streaming using HLS protocol: {urlForClientStreaming}");

                                if(!string.IsNullOrEmpty(urlForClientStreaming))
                                {
                                    //Call Delete duplicate Function for deleting duplicate assets

                                }
                            }
                        }

                        return req.CreateResponse(HttpStatusCode.OK, string.Empty);
                    }
                    else
                    {
                        log.Info($"VerifyHeaders failed.");
                        return req.CreateResponse(HttpStatusCode.BadRequest, "VerifyHeaders failed.");
                    }
                }
                else
                {
                    log.Info($"VerifyWebHookRequestSignature failed.");
                    return req.CreateResponse(HttpStatusCode.BadRequest, "VerifyWebHookRequestSignature failed.");
                }
            }

            return req.CreateResponse(HttpStatusCode.BadRequest, "Generic Error.");
        }


    }
}

