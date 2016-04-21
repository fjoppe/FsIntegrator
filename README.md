[![Issue Stats](http://issuestats.com/github/fjoppe/Camel.Net/badge/issue)](http://github.com/fjoppe/Camel.Net)
[![Issue Stats](http://issuestats.com/github/fjoppe/Camel.Net/badge/pr)](http://github.com/fjoppe/Camel.Net)

# Camel.Net
F# DSL for Enterprise Integration Patterns

Create easy message routes, like this:

```fsharp
let Route1 = 
    From.File fileListenerPath 
        =>= SetMessageType
        =>= To.Choose [
                When(Header("type") &= "add")
                    =>= To.Process(fun m -> logger.Debug("Add Process"))
                    =>= PrintMessageContent
                When(Header("type") &= "delete") 
                    =>= To.Process(fun m -> logger.Debug("Delete Process"))
                    =>= PrintMessageContent
                When(Header("type") &= "update") 
                    =>= To.Process(fun m -> logger.Debug("Update Process"))
                    =>= PrintMessageContent
            ]
        =>= To.Process(fun m -> logger.Debug("After Choose"))
```

The current focus is on user-experience of this DSL. The machine below the hood is working, but this machinery may lack features. 


In order to build this project run: 

    > build.cmd // on windows    
    $ ./build.sh  // on unix

   

## Maintainer(s)

- [@fjoppe](https://github.com/fjoppe)

