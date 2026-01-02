if exist .\release rmdir /S /Q .\release
mkdir .\release
dotnet publish ./MovieColour/MovieColour/MovieColour.csproj^
 -c Release^
 -r win-x64^
 -p:PublishSingleFile=true^
 -p:SelfContained=false^
 -p:DebugType=None^
 -p:DebugSymbols=false^
 -o ./release