# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

import urllib
import zipfile
import sys
import os

#remember to run with sudo since we're going to be stomping on home directory stuff.

def download_and_unzip(url):
    # intermediate zip file. This will just get overwritten each time.
    zipPath = "core.zip"
        
    print("DOWNLOADING " + zipPath + " FROM " + url)

    urllib.urlretrieve(url, filename = zipPath)
        
    print("UNZIPPING " + zipPath + " to /")

    with open(zipPath, 'r') as fh:
        z = zipfile.ZipFile(fh)

        for name in z.namelist():
            outpath = "/"
            print("---- EXTRACTING TO " + name)
            z.extract(name, outpath)

    os.remove(zipPath)

    print("ZIP FILE DOWNLOADED AND EXTRACTED")

if __name__ == '__main__':
    if len(sys.argv) < 2:
        print("Pass in a url to the zip to retrieve. I'll download and unzip all of those things.");
    url = sys.argv[1]
    download_and_unzip(url)