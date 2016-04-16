namespace Camel.Core

open System.IO
open System.Xml.XPath

module internal InternalUtility =
    
    let substituteSingleXPath (xpath:XPathNavigator) (path:string) =
        try
            let node = xpath.SelectSingleNode(path)
            if node = null
                then ""
                else node.Value
        with
        | e -> ""



  