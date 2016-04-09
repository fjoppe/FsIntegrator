[![Issue Stats](http://issuestats.com/github/fjoppe/Camel.Net/badge/issue)](http://github.com/fjoppe/Camel.Net)
[![Issue Stats](http://issuestats.com/github/fjoppe/Camel.Net/badge/pr)](http://github.com/fjoppe/Camel.Net)

# Camel.Net
F# DSL for Enterprise Integration Patterns

Create easy message routes, like this:

```fsharp
From.File fileListenerPath 
    =>= To.Process f1
    =>= To.Process (maps, f2)
```
 
Current focus of this DSL is on the DSL user experience, and less on the implementation of the machine-behind-the-scenes.


In order to build this project run: 

    > build.cmd // on windows    
    $ ./build.sh  // on unix
    

## Build Status

Mono | .NET
---- | ----
[![Mono CI Build Status](https://img.shields.io/travis/fsprojects/ProjectScaffold/master.svg)](https://travis-ci.org/fsprojects/ProjectScaffold) | [![.NET Build Status](https://img.shields.io/appveyor/ci/fsgit/ProjectScaffold/master.svg)](https://ci.appveyor.com/project/fsgit/projectscaffold)

## Maintainer(s)

- [@fjoppe](https://github.com/fjoppe)

