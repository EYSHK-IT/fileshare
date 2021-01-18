﻿#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2020 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.

    Optionally you can also view the license at <http://www.gnu.org/licenses/>.
*/

#endregion License Information (GPL v3)

using FluentFTP;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ShareX.HelpersLib;
using ShareX.UploadersLib.Properties;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace ShareX.UploadersLib.FileUploaders
{
    public class OwnCloudFileUploaderService : FileUploaderService
    {
        public override FileDestination EnumValue { get; } = FileDestination.OwnCloud;

        public override Image ServiceImage => Resources.OwnCloud;

        public override bool CheckConfig(UploadersConfig config)
        {
            return !string.IsNullOrEmpty(config.OwnCloudHost) && !string.IsNullOrEmpty(config.OwnCloudUsername) && !string.IsNullOrEmpty(config.OwnCloudPassword);
        }

        public override GenericUploader CreateUploader(UploadersConfig config, TaskReferenceHelper taskInfo)
        {
            return new OwnCloud(config.OwnCloudHost, config.OwnCloudUsername, config.OwnCloudPassword)
            {
                EncryptPassword = config.OwnCloudEncryptPassword,
                Path = config.OwnCloudPath,
                CreateShare = config.OwnCloudCreateShare,
                DirectLink = config.OwnCloudDirectLink,
                PreviewLink = config.OwnCloudUsePreviewLinks,
                IsCompatibility81 = config.OwnCloud81Compatibility,
                AutoExpireTime = config.OwnCloudExpiryTime,
                AutoExpire = config.OwnCloudAutoExpire,
                CreateFolderOfNonExistent = config.OwnCloudCreateFolderOfNonExistent,
                UsePathFilter = config.OwnCloudUsePathFilter,
                PathFilters = config.OwnCloudPathFilters
            };
        }

        public override TabPage GetUploadersConfigTabPage(UploadersConfigForm form) => form.tpOwnCloud;
    }

    public sealed class OwnCloud : FileUploader
    {
        public string Host { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool EncryptPassword { get; set; }
        public string Path { get; set; }
        public int AutoExpireTime { get; set; }
        public bool CreateShare { get; set; }
        public bool DirectLink { get; set; }
        public bool PreviewLink { get; set; }
        public bool IsCompatibility81 { get; set; }
        public bool AutoExpire { get; set; }
        public bool CreateFolderOfNonExistent { get; set; }
        public bool UsePathFilter { get; set; }
        public List<OwnCloudPathFilterItem> PathFilters { get; set; }

        public OwnCloud(string host, string username, string password)
        {
            Host = host;
            Username = username;
            Password = password;
            PathFilters = new List<OwnCloudPathFilterItem>();
        }

        public override UploadResult Upload(Stream stream, string fileName)
        {
            if (string.IsNullOrEmpty(Host))
            {
                throw new Exception("ownCloud Host is empty.");
            }

            if (string.IsNullOrEmpty(Username) || string.IsNullOrEmpty(Password))
            {
                throw new Exception("ownCloud Username or Password is empty.");
            }

            if (string.IsNullOrEmpty(Path))
            {
                Path = "/";
            }

            string uploadPath = GetUploadPath(Path, fileName);

            // Original, unencoded path. Necessary for shared files
            string path = URLHelpers.CombineURL(uploadPath, fileName);
            // Encoded path, necessary when sent in the URL
            string encodedPath = URLHelpers.CombineURL(uploadPath, URLHelpers.URLEncode(fileName));

            string url = URLHelpers.CombineURL(Host, "remote.php/webdav", encodedPath);
            url = URLHelpers.FixPrefix(url);

            NameValueCollection headers = RequestHelpers.CreateAuthenticationHeader(Username, EncryptPassword ? Password.DPAPIUnprotectAndBase64() : Password);
            headers["OCS-APIREQUEST"] = "true";

            // Check if folder exists and if not create it
            AllowReportProgress = false;

            if (CreateFolderOfNonExistent)
            {
                string[] pathParts = uploadPath.Split('/');
                int i = pathParts.Length - 1;
                bool foldersMissing = false;

                string pathToCreate = pathParts.Take(i + 1).ToArray().Join("/");

                // Go backwards to the first folder that exists
                while (i > 0 && !FolderExists(pathToCreate, headers))
                {
                    i--;
                    foldersMissing = true;
                    pathToCreate = pathParts.Take(i + 1).ToArray().Join("/");
                }

                // Create all non existent folders
                while (foldersMissing && i < pathParts.Length)
                {
                    i++;
                    pathToCreate = pathParts.Take(i + 1).ToArray().Join("/");
                    CreateFolder(pathToCreate, headers);
                }
            }

            // Upload file
            AllowReportProgress = true;

            string response = SendRequest(HttpMethod.PUT, url, stream, RequestHelpers.GetMimeType(fileName), null, headers);

            UploadResult result = new UploadResult(response);

            if (!IsError)
            {
                if (CreateShare)
                {
                    AllowReportProgress = false;
                    result.URL = ShareFile(path, headers);
                }
                else
                {
                    result.IsURLExpected = false;
                }
            }

            return result;
        }

        private string GetUploadPath(string defaultPath, string fileName)
        {
            if (UsePathFilter)
            {
                foreach (OwnCloudPathFilterItem pathFilter in PathFilters)
                {
                    if (pathFilter.Filter.IsValidRegEx())
                    {
                        Match filterMatch = Regex.Match(fileName, pathFilter.Filter);

                        if (filterMatch.Success)
                        {
                            return ReplaceGroupMatchingInFilter(pathFilter.Path, filterMatch);
                        }
                    }
                }
            }

            return defaultPath;
        }

        private string ReplaceGroupMatchingInFilter(string path, Match filterMatch)
        {
            Match groupMatch = Regex.Match(path, @"%(\d+)(\{([^\}]+)\})?");

            while (groupMatch.Success)
            {
                string fullMatch = groupMatch.Groups[0].Value;
                string caseFunction = groupMatch.Groups[3].Value;
                int groupNumber = int.Parse(groupMatch.Groups[1].Value);

                if (0 <= groupNumber && groupNumber < filterMatch.Groups.Count)
                {
                    string groupValue = filterMatch.Groups[groupNumber].Value;

                    if (!string.IsNullOrWhiteSpace(caseFunction))
                    {
                        string[] caseFunctionParams = caseFunction.Split(',');

                        if (caseFunctionParams.Length > 0)
                        {
                            switch (caseFunctionParams[0].ToLower())
                            {
                                case "l":
                                case "lower":
                                    groupValue = groupValue.ToLower();

                                    if (caseFunctionParams.Length > 1)
                                    {
                                        groupValue = groupValue.Replace(" ", caseFunctionParams[1]);
                                    }
                                    break;
                                case "u":
                                case "upper":
                                    groupValue = groupValue.ToUpper();

                                    if (caseFunctionParams.Length > 1)
                                    {
                                        groupValue = groupValue.Replace(" ", caseFunctionParams[1]);
                                    }
                                    break;
                                case "c":
                                case "camel":
                                    groupValue = caseFunctionParams.Length > 1 ? groupValue.ToCamelCase(caseFunctionParams[1]) : groupValue.ToCamelCase();
                                    break;
                                case "p":
                                case "pascal":
                                    groupValue = caseFunctionParams.Length > 1 ? groupValue.ToPascalCase(caseFunctionParams[1]) : groupValue.ToPascalCase();
                                    break;
                                case "pi":
                                case "pinv":
                                case "pascali":
                                case "pascalinv":
                                case "pascalinverted":
                                    groupValue = caseFunctionParams.Length > 1 ? groupValue.ToInvertedPascalCase(caseFunctionParams[1]) : groupValue.ToInvertedPascalCase();
                                    break;
                                case "s":
                                case "snake":
                                    groupValue = caseFunctionParams.Length > 1 ? groupValue.ToSnakeCase(caseFunctionParams[1]) : groupValue.ToSnakeCase();
                                    break;
                                case "k":
                                case "kebab":
                                    groupValue = caseFunctionParams.Length > 1 ? groupValue.ToKebabCase(caseFunctionParams[1]) : groupValue.ToKebabCase();
                                    break;
                                default:
                                    throw new ArgumentException($"Unknown function name \"{ caseFunctionParams[0] }\".");
                            }
                        }

                        int k = 0;
                    }

                    path = path.Replace(fullMatch, groupValue);
                }

                groupMatch = groupMatch.NextMatch();
            }

            path = NameParser.Parse(NameParserType.FolderPath, path);

            return path;
        }

        public bool FolderExists(string uploadPath, NameValueCollection headers)
        {
            string folderUrl = URLHelpers.CombineURL(Host, "remote.php/webdav", uploadPath);
            folderUrl = URLHelpers.FixPrefix(folderUrl);

            string folderExistsResponse = SendRequest(HttpMethod.PROPFIND, folderUrl, null, headers, null);

            return !string.IsNullOrWhiteSpace(folderExistsResponse);
        }

        public void CreateFolder(string uploadPath, NameValueCollection headers)
        {
            string folderUrl = URLHelpers.CombineURL(Host, "remote.php/webdav", uploadPath);
            folderUrl = URLHelpers.FixPrefix(folderUrl);

            string folderCreateResponse = SendRequest(HttpMethod.MKCOL, folderUrl, null, headers, null);
        }

        // https://doc.owncloud.org/server/10.0/developer_manual/core/ocs-share-api.html#create-a-new-share
        public string ShareFile(string path, NameValueCollection headers)
        {
            Dictionary<string, string> args = new Dictionary<string, string>();
            args.Add("path", path); // path to the file/folder which should be shared
            args.Add("shareType", "3"); // ‘0’ = user; ‘1’ = group; ‘3’ = public link
            // args.Add("shareWith", ""); // user / group id with which the file should be shared
            // args.Add("publicUpload", "false"); // allow public upload to a public shared folder (true/false)
            // args.Add("password", ""); // password to protect public link Share with
            args.Add("permissions", "1"); // 1 = read; 2 = update; 4 = create; 8 = delete; 16 = share; 31 = all (default: 31, for public shares: 1)

            if (AutoExpire)
            {
                if (AutoExpireTime == 0)
                {
                    throw new Exception("ownCloud Auto Epxire Time is not valid.");
                }
                else
                {
                    try
                    {
                        DateTime expireTime = DateTime.UtcNow.AddDays(AutoExpireTime);
                        args.Add("expireDate", $"{expireTime.Year}-{expireTime.Month}-{expireTime.Day}");
                    }
                    catch
                    {
                        throw new Exception("ownCloud Auto Expire time is invalid");
                    }
                }
            }

            string url = URLHelpers.CombineURL(Host, "ocs/v1.php/apps/files_sharing/api/v1/shares?format=json");
            url = URLHelpers.FixPrefix(url);

            string response = SendRequestMultiPart(url, args, headers);

            if (!string.IsNullOrEmpty(response))
            {
                OwnCloudShareResponse result = JsonConvert.DeserializeObject<OwnCloudShareResponse>(response);

                if (result != null && result.ocs != null && result.ocs.meta != null)
                {
                    if (result.ocs.data != null && result.ocs.meta.statuscode == 100)
                    {
                        OwnCloudShareResponseData data = ((JObject)result.ocs.data).ToObject<OwnCloudShareResponseData>();
                        string link = data.url;
                        if (PreviewLink && Helpers.IsImageFile(path))
                        {
                            link += "/preview";
                        }
                        else if (DirectLink)
                        {
                            link += (IsCompatibility81 ? "/" : "&") + "download";
                        }
                        return link;
                    }
                    else
                    {
                        Errors.Add(string.Format("Status: {0}\r\nStatus code: {1}\r\nMessage: {2}", result.ocs.meta.status, result.ocs.meta.statuscode, result.ocs.meta.message));
                    }
                }
            }

            return null;
        }

        public class OwnCloudShareResponse
        {
            public OwnCloudShareResponseOcs ocs { get; set; }
        }

        public class OwnCloudShareResponseOcs
        {
            public OwnCloudShareResponseMeta meta { get; set; }
            public object data { get; set; }
        }

        public class OwnCloudShareResponseMeta
        {
            public string status { get; set; }
            public int statuscode { get; set; }
            public string message { get; set; }
        }

        public class OwnCloudShareResponseData
        {
            public int id { get; set; }
            public string url { get; set; }
            public string token { get; set; }
        }

        public class OwnCloudPathFilterItem
        {
            public string Path { get; set; }
            public string Filter { get; set; }
        }
    }
}