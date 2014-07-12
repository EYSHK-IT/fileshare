﻿// Contributed by https://github.com/LRNAB

using System;
using System.Net;

namespace UploadersLib.URLShorteners
{
    public class AdflyURLShortener : URLShortener
    {
        public string APIKEY { get; set; }
        public string APIUID { get; set; }

        private string URL = "http://api.adf.ly/api.php?key={0}&uid={1}&advert_type=int&domain=adf.ly&url={2}";
        private WebClient client = new WebClient();

        public override UploadResult ShortenURL(string url)
        {
            UploadResult result = new UploadResult { URL = url };
            var response = client.DownloadString(String.Format(URL, APIKEY, APIUID, url));

            if (response != "error")
                result.ShortenedURL = response;

            return result;
        }
    }
}
