#I __SOURCE_DIRECTORY__
#I ".."
#r "Camel.FTP/bin/Debug/System.Net.FtpClient.dll"

open System
open System.IO
open System.Net.FtpClient

let connectionString = Uri("ftp://test:test@TestRemoteVM")
let client = FtpClient.Connect(connectionString)
client.Connect()

let workingdir = client.GetWorkingDirectory()
let remotedir = client.GetListing(workingdir)
remotedir |> List.ofArray |> List.iter(fun e -> printfn "%s %A" e.FullName e.Type)

let remotefile = client.OpenRead("/home/frank/testfile.xml",FtpDataType.Binary) :?> System.Net.FtpClient.FtpDataStream
let localfile = File.Create(__SOURCE_DIRECTORY__ + @"/../Testing/Download/testfile.xml")

remotefile.CopyTo(localfile)

remotefile.Close()
localfile.Close()


client.CreateDirectory("/home/frank/.camel", true)
client.Rename("/home/frank/testfile.xml", "/home/frank/.camel/testfile.xml")

