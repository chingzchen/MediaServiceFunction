using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.MediaServices.Client;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.MediaServices.Client.ContentKeyAuthorization;
using Newtonsoft.Json;
using Microsoft.WindowsAzure.MediaServices.Client.FairPlay;

namespace JobMonitor.Helper
{
    public class MediaServiceHelper
    {
        public static async Task<List<IAsset>> GetAssetbyName(CloudMediaContext mediaContext, string filename, TraceWriter log)
        {
            //AssetBaseCollection assetList =new AssetBaseCollection();

            List<IAsset> assetList = mediaContext.Assets.Where(r => r.Name.Equals(filename)).OrderBy(r => r.LastModified).ToList<IAsset>();
            log.Info(string.Format("Find {0} files to be deleted!", assetList.Count));
            IAsset latestAsset = assetList.First();
            //remove the latest modified one
            // assetList.RemoveRange(1, assetList.Count - 1);
            assetList.RemoveAt(0);
            //Remove the lastest upated one

            return assetList;
        }

        /// <summary>
        /// Selete Asset Aync
        /// </summary>
        /// <param name="mediaContext"></param>
        /// <param name="asset"></param>
        /// <returns></returns>

        public static async Task DeleteAssetAsync(CloudMediaContext mediaContext, List<IAsset> assets, TraceWriter log)
        {
            foreach (IAsset asset in assets)
            {
                log.Info(string.Format("Start deleting asset name: {0}, id: {1}", asset.Name, asset.Id));
                //Delete locatir
                foreach (var locator in asset.Locators.ToArray())
                {
                    await locator.DeleteAsync();
                }

                //Delete Policy
                foreach (var policy in asset.DeliveryPolicies.ToArray())
                {
                    asset.DeliveryPolicies.Remove(policy);
                    await policy.DeleteAsync();
                }
                foreach (var key in asset.ContentKeys.ToArray())
                {
                    CleanupKey(mediaContext, key);
                    try // because we have an error for FairPlay key
                    {
                        asset.ContentKeys.Remove(key);
                    }
                    catch (Exception ex)
                    {
                        log.Error(string.Format("Delete asset name: {0}, id: {1} filed", asset.Name, asset.Id));
                        log.Info($"StackTrace : {ex.StackTrace}");
                        throw ex;
                    }
                }
                await asset.DeleteAsync();
            }

            return;

        }

        public static void CleanupKey(CloudMediaContext mediaContext, IContentKey key)
        {
            IContentKeyAuthorizationPolicy policy = null;

            if (key.AuthorizationPolicyId != null)
            {
                policy = mediaContext.ContentKeyAuthorizationPolicies
             .Where(o => o.Id == key.AuthorizationPolicyId)
             .SingleOrDefault();
            }

            if (policy != null)
            {
                if (key.ContentKeyType == ContentKeyType.CommonEncryptionCbcs)
                {
                    string template = policy.Options.Single().KeyDeliveryConfiguration;

                    var config = JsonConvert.DeserializeObject<FairPlayConfiguration>(template);

                    IContentKey ask = mediaContext
                        .ContentKeys
                        .Where(k => k.Id == Constants.ContentKeyIdPrefix + config.ASkId.ToString())
                        .SingleOrDefault();

                    if (ask != null)
                    {
                        ask.Delete();
                    }

                    IContentKey pfxPassword = mediaContext
                        .ContentKeys
                        .Where(k => k.Id == Constants.ContentKeyIdPrefix + config.FairPlayPfxPasswordId.ToString())
                        .SingleOrDefault();

                    if (pfxPassword != null)
                    {
                        pfxPassword.Delete();
                    }
                }

                policy.Delete();
            }
        }

        public static string PublishAndBuildStreamingURLs(CloudMediaContext mediaContext, String jobID)
        {
            string poclicyname= "Streaming policy";
            string urlForClientStreaming = string.Empty;
            IJob job = mediaContext.Jobs.Where(j => j.Id == jobID).FirstOrDefault();
            IAsset asset = job.OutputMediaAssets.FirstOrDefault();
            ILocator originLocator;
            if (asset.Locators.Count ==0)
            {
                //Create a locator
                IAccessPolicy policy = mediaContext.AccessPolicies.Where(p => p.Name.Equals(poclicyname)).FirstOrDefault();
                if (policy == null)
                {
                    //Create a new one policy with 180 days readonly access
                    policy = mediaContext.AccessPolicies.Create(poclicyname, TimeSpan.FromDays(180), AccessPermissions.Read);
                }

                // Create a locator to the streaming content on an origin. 
                originLocator = mediaContext.Locators.CreateLocator(LocatorType.OnDemandOrigin, asset,
                policy,
                DateTime.UtcNow.AddMinutes(-5));
            }
            else
            {
                originLocator = asset.Locators.FirstOrDefault();
            }

            // Get a reference to the streaming manifest file from the  
            // collection of files in the asset. 
            var manifestFile = asset.AssetFiles.Where(f => f.Name.ToLower().
                        EndsWith(".ism")).
                        FirstOrDefault();

            // Create a full URL to the manifest file. Use this for playback
            // in streaming media clients. 
            urlForClientStreaming = originLocator.Path + manifestFile.Name + "/manifest" + "(format=m3u8-aapl)";
            return urlForClientStreaming;

        }

        public static IAsset GetAsset(CloudMediaContext mediaContext, String jobID)
        {
            IJob job = mediaContext.Jobs.Where(j => j.Id == jobID).FirstOrDefault();
            IAsset asset = job.OutputMediaAssets.FirstOrDefault();
            return asset;
        }

        public static string GetAssetName(CloudMediaContext mediaContext, String jobID)
        {
            IJob job = mediaContext.Jobs.Where(j => j.Id == jobID).FirstOrDefault();
            IAsset asset = job.OutputMediaAssets.FirstOrDefault();
            return asset.Name;
        }
    }
}
