namespace FsIntegrator

open System.IO
open System.Xml.XPath
open NLog

module internal InternalUtility =
    let logger = LogManager.GetLogger("FsIntegrator.InternalUtility")

    
    let substituteSingleXPath (xpath:XPathNavigator) (path:string) =
        try
            let node = xpath.SelectSingleNode(path)
            if node = null
                then ""
                else node.Value
        with
        | e -> logger.Error e
               ""



  