﻿using System;
using System.Text;
using System.Security.Cryptography;
using System.Collections.Generic;
using System.Text.RegularExpressions;

using UnityEngine;

using LitJson;

namespace BMS {
    internal class BMSHashGenerator {
        Encoding encoding;
        HashAlgorithm hashAlgorithm;

        public BMSHashGenerator(Encoding encoding, HashAlgorithm hashAlgorithm) {
            this.hashAlgorithm = hashAlgorithm ?? MD5.Create();
            this.encoding = encoding ?? Encoding.Default;
        }

        public string GetHash(string[] content) {
            var groupedBMS = string.Join("\n", content);
            var bytes = encoding.GetBytes(groupedBMS);
            var hash = hashAlgorithm.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }

    public partial class BMSManager: MonoBehaviour {
        JsonData bmsonContent;
        string[] bmsContent;
        bool bmsLoaded = false;

        public bool BMSLoaded {
            get { return bmsLoaded; }
        }

        public event Action OnBMSLoaded;

        public void LoadBMS(string bmsContent, string resourcePath, BMSFileType bmsFileType, bool direct = false) {
            StopPreviousBMSLoadJob();
            fileType = bmsFileType;
            this.bmsContent = null;
            this.bmsonContent = null;
            switch(bmsFileType) {
                case BMSFileType.Standard:
                case BMSFileType.Extended:
                case BMSFileType.Long:
                case BMSFileType.Popn:
                    var bmsContentList = new List<string>();
                    foreach(var line in Regex.Split(bmsContent, "\r\n|\r|\n"))
                        if(!string.IsNullOrEmpty(line) && line[0] == '#')
                            bmsContentList.Add(line);
                    this.bmsContent = bmsContentList.ToArray();
                    break;
                case BMSFileType.Bmson:
                    this.bmsonContent = JsonMapper.ToObject(bmsContent);
                    break;
            }
            this.resourcePath = resourcePath;
            bmsLoaded = false;
            ClearDataObjects(true, direct);
            ReloadBMS(BMSReloadOperation.Header, direct);
        }

        public void ReloadBMS(BMSReloadOperation reloadType, bool direct = false) {
            bool header = (reloadType & BMSReloadOperation.Header) == BMSReloadOperation.Header;
            bool body = (reloadType & BMSReloadOperation.Body) == BMSReloadOperation.Body;
            bool res = (reloadType & BMSReloadOperation.Resources) == BMSReloadOperation.Resources;
            bool resHeader = (reloadType & BMSReloadOperation.ResourceHeader) == BMSReloadOperation.ResourceHeader;
            if(header || body) {
                if(res && !resHeader)
                    ClearDataObjects(true, direct);
                ReloadTimeline(header, body, resHeader, direct);
            } else if(res)
                ClearDataObjects(false, direct);
            if(res)
                ReloadResources();
        }

        public string GetHash(Encoding encoding, HashAlgorithm hashAlgorithm) {
            return new BMSHashGenerator(encoding, hashAlgorithm).GetHash(bmsContent);
        }
    }
}
