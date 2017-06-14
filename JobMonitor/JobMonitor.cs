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
using System.Net.Http.Headers;
using Newtonsoft.Json;

namespace JobMonitor
{
    public static class JobMonitor
    {
        //Webhook & security
        static string _webHookEndpoint = Environment.GetEnvironmentVariable(Constants.WebHookEndpoint);     
        static string _signingKey = Environment.GetEnvironmentVariable(Constants.SigningKey);

        //Media Service Account Info
        static string _mediaServicesAccountName = Environment.GetEnvironmentVariable(Constants.MediaServiceAccout);
        static string _mediaServicesAccountKey = Environment.GetEnvironmentVariable(Constants.MediaServiceKey);

        static CloudMediaContext _context = null;

        [FunctionName("JobMonitor")]
        public static async Task<HttpResponseMessage> Run([HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = null)]HttpRequestMessage req, TraceWriter log)
        {
            log.Info("Monitor Job function processed a request.");

            Task<byte[]> taskForRequestBody = req.Content.ReadAsByteArrayAsync();
            byte[] requestBody = await taskForRequestBody;

            string jsonContent = await req.Content.ReadAsStringAsync();
            log.Info($"Request Body = {jsonContent}");

            IEnumerable<string> values = null;
            if (req.Headers.TryGetValues("ms-signature", out values))
            {
                byte[] signingKey = Convert.FromBase64String(_signingKey);
                string signatureFromHeader = values.FirstOrDefault();

                string requestMessageContents = Encoding.UTF8.GetString(requestBody);

                NotificationMessage msg = JsonConvert.DeserializeObject<NotificationMessage>(requestMessageContents);
                log.Info("msg:" + msg);

                if (SecurityHelper.VerifyHeaders(req, msg, log))
                {
                    log.Info("VerifyHeaders Succeed!");
                    string newJobStateStr = (string)msg.Properties.Where(j => j.Key == "NewState").FirstOrDefault().Value;
                    if (newJobStateStr == "Finished")
                    {
                        _context = new CloudMediaContext(new MediaServicesCredentials(
                        _mediaServicesAccountName,
                        _mediaServicesAccountKey));

                        if (_context != null)
                        {
                            IAsset asset = MediaServiceHelper.GetAsset(_context, msg.Properties["JobId"]);
                            string urlForClientStreaming;
                            if (asset != null)
                            {

                                urlForClientStreaming = MediaServiceHelper.PublishAndBuildStreamingURLs(_context, msg.Properties["JobId"]);
                                log.Info($"URL to the manifest for client streaming using HLS protocol: {urlForClientStreaming}");

                                if (!string.IsNullOrEmpty(urlForClientStreaming))
                                {
                                    try
                                    {
                                        string assetName = MediaServiceHelper.GetAssetName(_context, msg.Properties["JobId"]);
                                        //Call Delete duplicate Function for deleting duplicate assets

                                        HttpResponseMessage response;
                                        var client = new HttpClient();
                                        // Request body. Send Asset fileName and fileID
                                        string body = string.Format(Helper.Constants.strtemplate, assetName);

                                        log.Info("body:{0}", body);

                                        // Request body
                                        byte[] byteData = Encoding.UTF8.GetBytes(body);
                                        using (var content = new ByteArrayContent(byteData))
                                        {
                                            content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                                            log.Info("try to call delete duplicate api with asset name:" + assetName);
                                            response = await client.PostAsync(_webHookEndpoint, content);
                                        }

                                    }
                                    catch (Exception ex)
                                    {
                                        log.Error(ex.Message);
                                        throw ex;
                                    }
                                }
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

            return req.CreateResponse(HttpStatusCode.BadRequest, "Generic Error.");
        }


    }
}

