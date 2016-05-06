//  ============================================================================================================
//
//  This script explores the System.Net.FtpClient library, how to use this from F#
//
//  Prerequisites:
//      1.  A running FTP Server
//      2.  A user account test/test
//      3.  The existence of this file on the remote system: /home/test/testfile.xml
//
//  ============================================================================================================

#I __SOURCE_DIRECTORY__
#I ".."
#r "FsIntegrator.FTP/bin/Debug/System.Net.FtpClient.dll"

open System
open System.IO
open System.Net.FtpClient


//  The FTP Credentials, here, the vsftpd server is running under Linux with hostname TestRemoteVM"
let connectionString = Uri("ftp://test:test@TestRemoteVM")
let client = FtpClient.Connect(connectionString)
client.Connect()

let workingdir = client.GetWorkingDirectory()
let remotedir = client.GetListing(workingdir)
remotedir |> List.ofArray |> List.iter(fun e -> printfn "%s %A" e.FullName e.Type)

let remotefile = client.OpenRead("/home/test/testfile.xml",FtpDataType.Binary) :?> System.Net.FtpClient.FtpDataStream
let localfile = File.Create(__SOURCE_DIRECTORY__ + @"/../TestExamples/Download/testfile.xml")

remotefile.CopyTo(localfile)

remotefile.Close()
localfile.Close()

client.CreateDirectory("/home/test/.camel", true)
client.Rename("/home/test/testfile.xml", "/home/test/.camel/testfile.xml")

