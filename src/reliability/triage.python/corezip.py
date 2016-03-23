# Licensed to the .NET Foundation under one or more agreements.
# The .NET Foundation licenses this file to you under the MIT license.
# See the LICENSE file in the project root for more information.

import argparse
import os
import zipfile
import string

def pack(strCorePath, strZipPath):
    """creates a zip file containing core dump and all related images"""

    import lldb
    #load the coredump in lldb
    debugger = lldb.SBDebugger.Create()

    debugger.SetAsync(False)
        
    interpreter = debugger.GetCommandInterpreter()

    result = run_command("target create --no-dependents --arch x86_64 --core " + strCorePath, interpreter)

    target = debugger.GetSelectedTarget()
    
    try:
        import zlib
        compressionType = zipfile.ZIP_DEFLATED
    except:
        compressionType = zipfile.ZIP_STORED

    zip = zipfile.ZipFile(strZipPath, mode='w', compression=compressionType, allowZip64=True)

    add_to_zip(os.path.abspath(strCorePath), zip)

    #iterate through image list and pack the zip file
    for m in target.modules:
        add_to_zip(m.file.fullpath, zip)
        if m.file.basename == 'libcoreclr.so':
            add_to_zip(os.path.join(m.file.dirname, 'libmscordaccore.so'), zip)
            add_to_zip(os.path.join(m.file.dirname, 'libsos.so'), zip)
    zip.close()

    print 'core dump and images written to ' + strZipPath 

def add_to_zip(strPath, zipFile):
    if os.path.exists(strPath):
        print 'adding ' + strPath
        zipFile.write(strPath)

def unpack(strZipPath):
    """unpacks zip restoring all files to their original paths"""
    with open(strZipPath, 'rb') as f:
        zip = zipfile.ZipFile(f)

        for path in zip.namelist():
            if os.path.exists('/' + path):
                print 'skipping     /' + path
            else:
                print 'extracting   /' + path
                zip.extract(path, '/')
        zip.close()
    print '\nall files extracted\n'

def run_command(strCmd, interpreter):
    strOut = ""
    result = lldb.SBCommandReturnObject()
    interpreter.HandleCommand(strCmd, result)
    if result.Succeeded():
        #print "INFO: Command SUCCEEDED: '" + strCmd + "'"
        strOut = result.GetOutput()
        print result.GetOutput()
        #print strOut
    else:
        print "ERROR: Command FAILED: '" + strCmd + "'"
        print result.GetOutput()
    return strOut

if __name__ == '__main__':
    parser = argparse.ArgumentParser(description='Zips and extracts core dumps and their dependent images.', epilog='If the -c option is specified corezip will archive the core along with all its dependent images into the specified zip file (-z).  Otherwise corezip will restore the specifed the coredump and images from the zip file.')
    parser.add_argument('-z', 
                      required=True, 
                      type=str,
                      help='path to the zipfile to create or unpack')
    parser.add_argument('-c', 
                      type=str,
                      help='path to the coredump to be packaged with images')

    args = parser.parse_args()

    if args.c is not None and args.c <> "":
        pack(args.c, args.z)
    else:
        unpack(args.z)

  