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

    let substituteAllXPath<'a when 'a : comparison> (map:Map<'a,string>) msg =
        let xpath = XPathDocument(new StringReader(msg)).CreateNavigator()
        map 
        |> Map.toSeq 
        |> Seq.map(fun (k, v) -> k, substituteSingleXPath xpath v)
        |> Map.ofSeq


    let CallWithMapping mapper f (msg:General.Message) =
        let mapping = substituteAllXPath mapper msg.Body
        f mapping msg

    let CallAndReturnInput f m = f m ; m    // returns m

  