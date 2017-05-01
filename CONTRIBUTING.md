### Building this Project

To simplify building and debugging, this C# project is expected to be part of a Visual Studio solution with at least the following project siblings:

* [TShock](https://github.com/NyxStudios/TShock)
* [TerrariaServerAPI](https://github.com/NyxStudios/TerrariaAPI-Server)
* [Plugin Common Library](https://github.com/CoderCow/PluginCommonLibrary)
* SEconomyPlugin

You can alternatively build this project standalone by removing these project references and adding new references to  already compiled binaries instead.

Also note that this project utilizes Microsoft Code Contracts and thus may require [Code Contract for .NET](https://marketplace.visualstudio.com/items?itemName=RiSEResearchinSoftwareEngineering.CodeContractsforNET) to be installed in your Visual Studio. However, the Code Contracts integration is not available for Visual Studio 2017! You can resolve this by following one of [the answers here](http://stackoverflow.com/questions/40767941/does-vs2017-work-with-codecontracts).

### Commit Message Guidelines

This repository follows the [Angular Guidelines](https://github.com/conventional-changelog/conventional-changelog/blob/a5505865ff3dd710cf757f50530e73ef0ca641da/conventions/angular.md) and uses publishing tools which also expect this commit format.

### Coding Style

Use spaces, not tabs. Curly braces layout is K&R. Otherwise refer to the existing code.
