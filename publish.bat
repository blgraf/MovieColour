del /S /Q .\release\*
dotnet publish ./MovieColour/MovieColour/MovieColour.csproj^
 -r win-x64^
 -p:PublishSingleFile=true^
 --self-contained false^
 -p:DebugType=None^
 -p:DebugSymbols=false^
 -o ./release