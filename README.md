[![Issue Stats](http://issuestats.com/github/fjoppe/FsIntegrator/badge/issue)](http://github.com/fjoppe/FsIntegrator)
[![Issue Stats](http://issuestats.com/github/fjoppe/FsIntegrator/badge/pr)](http://github.com/fjoppe/FsIntegrator)

# FsIntegrator
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

Notes:
This project currently builds under Visual Studio 2015. Due to the renaming process, the Build scripts do not work yet... but that'll change soon.

## Maintainer(s)

- [@fjoppe](https://github.com/fjoppe)

