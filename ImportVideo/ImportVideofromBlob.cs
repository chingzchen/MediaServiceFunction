﻿using System.IO;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace ImportVideo
{
    public static class ImportVideofromBlob
    {
        [FunctionName("BlobTriggerCSharp")]        
        public static void Run([BlobTrigger("samples-workitems/{name}", Connection = "AzureWebJobsStorage")]Stream myBlob, string name, TraceWriter log)
        {
            log.Info($"C# Blob trigger function Processed blob\n Name:{name} \n Size: {myBlob.Length} Bytes");
        }
    }
}