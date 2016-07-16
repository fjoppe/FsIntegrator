namespace FsIntegrator

module Core =  
    let ftp = FtpBuilder()

    /// Empty FTP Script (do nothing)
    let NoFTPScript = fun _ -> FtpScript.Empty

