﻿using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web.Http;
using System.Web.Mvc;
using Web.Storage;
using Web.Storage.Connection;
using Web.Utility;

namespace Web.Api.Content
{
    public class ContentUploadController : Controller
    {
        public async Task<ActionResult> UploadFromExternal(string url)
        {
            ImageUploaderManager manager=new ImageUploaderManager(new BlobStorageConnection());
            var st=await manager.AddUrlResourceAsync(url);
            return new ImageResult(st.Item1, st.Item2);
        }

        public ActionResult FromServerCache(string url)
        {
            ImageUploaderManager manager = new ImageUploaderManager(new BlobStorageConnection());
            string contentType = "";
            var st = manager.DownloadUrlResource(url, out contentType);
            return new ImageResult(st, contentType);
        }
    }
}