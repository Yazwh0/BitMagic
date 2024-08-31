del /s ..\*.nupkg
dotnet build -c Release ..\BitMagic.sln
dotnet publish -c Release ..\BitMagic.sln
dotnet pack -c Release --no-restore ..\BitMagic.sln
dotnet nuget push "..\**\BitMagic.*.nupkg" -s Local
