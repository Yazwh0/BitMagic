del /s ..\*.nupkg
dotnet build -c Release
dotnet publish -c Release
dotnet pack -c Release --no-restore
dotnet nuget push "..\**\BitMagic.*.nupkg" -s Local
